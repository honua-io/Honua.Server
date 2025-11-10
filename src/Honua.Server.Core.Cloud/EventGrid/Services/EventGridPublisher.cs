// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Threading.Channels;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Honua.Server.Core.Cloud.EventGrid.Configuration;
using Honua.Server.Core.Cloud.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Honua.Server.Core.Cloud.EventGrid.Services;

/// <summary>
/// Azure Event Grid publisher with batching, retry, and circuit breaker.
/// </summary>
public class EventGridPublisher : IEventGridPublisher, IDisposable
{
    private readonly EventGridOptions _options;
    private readonly ILogger<EventGridPublisher> _logger;
    private readonly EventGridPublisherClient? _client;
    private readonly Channel<HonuaCloudEvent> _eventQueue;
    private readonly EventGridMetrics _metrics = new();
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _disposed;

    public EventGridPublisher(
        IOptions<EventGridOptions> options,
        ILogger<EventGridPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Validate configuration
        _options.Validate();

        if (!_options.Enabled)
        {
            _logger.LogInformation("Event Grid publishing is disabled");
            _eventQueue = Channel.CreateUnbounded<HonuaCloudEvent>();
            _resiliencePipeline = new ResiliencePipelineBuilder().Build();
            return;
        }

        // Create Event Grid client
        _client = CreateEventGridClient();

        // Create event queue (bounded or unbounded based on config)
        var channelOptions = new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = _options.BackpressureMode == BackpressureMode.Block
                ? BoundedChannelFullMode.Wait
                : BoundedChannelFullMode.DropOldest
        };
        _eventQueue = Channel.CreateBounded<HonuaCloudEvent>(channelOptions);

        // Create resilience pipeline (retry + circuit breaker)
        _resiliencePipeline = CreateResiliencePipeline();

        _logger.LogInformation(
            "Event Grid publisher initialized: Endpoint={Endpoint}, BatchSize={BatchSize}, FlushInterval={FlushInterval}s",
            _options.TopicEndpoint ?? _options.DomainEndpoint,
            _options.MaxBatchSize,
            _options.FlushIntervalSeconds);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(HonuaCloudEvent cloudEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        // Apply filters
        if (!PassesFilters(cloudEvent))
        {
            Interlocked.Increment(ref _metrics.EventsFiltered);
            _logger.LogDebug("Event filtered: Type={Type}, Collection={Collection}, Tenant={Tenant}",
                cloudEvent.Type, cloudEvent.Collection, cloudEvent.TenantId);
            return;
        }

        // Queue event for batched publishing
        var written = await _eventQueue.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false);
        if (written)
        {
            await _eventQueue.Writer.WriteAsync(cloudEvent, cancellationToken).ConfigureAwait(false);
            _logger.LogTrace("Event queued: Id={Id}, Type={Type}", cloudEvent.Id, cloudEvent.Type);
        }
        else
        {
            Interlocked.Increment(ref _metrics.EventsDropped);
            _logger.LogWarning("Event dropped (queue full): Id={Id}, Type={Type}", cloudEvent.Id, cloudEvent.Type);
        }
    }

    /// <inheritdoc/>
    public async Task PublishBatchAsync(IEnumerable<HonuaCloudEvent> cloudEvents, CancellationToken cancellationToken = default)
    {
        foreach (var cloudEvent in cloudEvents)
        {
            await PublishAsync(cloudEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _client == null)
            return;

        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var batch = new List<HonuaCloudEvent>();

            // Read all available events from queue
            while (_eventQueue.Reader.TryRead(out var cloudEvent))
            {
                batch.Add(cloudEvent);
            }

            if (batch.Count == 0)
            {
                _logger.LogTrace("FlushAsync: No events to publish");
                return;
            }

            _logger.LogDebug("Flushing {Count} events to Event Grid", batch.Count);

            // Publish in batches
            await PublishBatchInternalAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <inheritdoc/>
    public int GetQueueSize()
    {
        return _eventQueue.Reader.Count;
    }

    /// <inheritdoc/>
    public EventGridMetrics GetMetrics()
    {
        _metrics.CurrentQueueSize = GetQueueSize();
        return _metrics;
    }

    /// <summary>
    /// Internal method to read events from queue and publish in batches.
    /// Called by the hosted service background task.
    /// </summary>
    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || _client == null)
            return;

        var batch = new List<HonuaCloudEvent>();

        try
        {
            // Read events from queue up to batch size
            while (batch.Count < _options.MaxBatchSize &&
                   _eventQueue.Reader.TryRead(out var cloudEvent))
            {
                batch.Add(cloudEvent);
            }

            if (batch.Count == 0)
                return;

            await PublishBatchInternalAsync(batch, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event batch");
        }
    }

    private async Task PublishBatchInternalAsync(List<HonuaCloudEvent> batch, CancellationToken cancellationToken)
    {
        if (_client == null)
            return;

        // Split into chunks based on MaxBatchSize
        var chunks = batch.Chunk(_options.MaxBatchSize);

        foreach (var chunk in chunks)
        {
            try
            {
                // Convert to Azure EventGridEvent
                var eventGridEvents = chunk.Select(ToEventGridEvent).ToList();

                // Publish with resilience (retry + circuit breaker)
                await _resiliencePipeline.ExecuteAsync(async ct =>
                {
                    await _client.SendEventsAsync(eventGridEvents, ct).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                Interlocked.Add(ref _metrics.EventsPublished, chunk.Length);
                _metrics.LastPublishTime = DateTimeOffset.UtcNow;

                _logger.LogInformation("Published {Count} events to Event Grid", chunk.Length);
            }
            catch (BrokenCircuitException ex)
            {
                _metrics.CircuitBreakerState = "Open";
                _metrics.LastError = "Circuit breaker is open";
                Interlocked.Add(ref _metrics.EventsFailed, chunk.Length);

                _logger.LogError(ex, "Circuit breaker is open - events dropped: {Count}", chunk.Length);
            }
            catch (Exception ex)
            {
                _metrics.LastError = ex.Message;
                Interlocked.Add(ref _metrics.EventsFailed, chunk.Length);

                _logger.LogError(ex, "Failed to publish events to Event Grid after retries: {Count}", chunk.Length);
            }
        }
    }

    private EventGridEvent ToEventGridEvent(HonuaCloudEvent cloudEvent)
    {
        // Azure Event Grid EventGridEvent (CloudEvents schema is also supported via CloudEvent class)
        // We'll use EventGridEvent for broader compatibility
        return new EventGridEvent(
            subject: cloudEvent.Subject ?? cloudEvent.Source,
            eventType: cloudEvent.Type,
            dataVersion: "1.0",
            data: new BinaryData(cloudEvent))
        {
            Id = cloudEvent.Id,
            EventTime = cloudEvent.Time
        };
    }

    private bool PassesFilters(HonuaCloudEvent cloudEvent)
    {
        // Event type filter
        if (_options.EventTypeFilter.Count > 0 &&
            !_options.EventTypeFilter.Contains(cloudEvent.Type))
        {
            return false;
        }

        // Collection filter
        if (_options.CollectionFilter.Count > 0 &&
            !string.IsNullOrEmpty(cloudEvent.Collection) &&
            !_options.CollectionFilter.Contains(cloudEvent.Collection))
        {
            return false;
        }

        // Tenant filter
        if (_options.TenantFilter.Count > 0 &&
            !string.IsNullOrEmpty(cloudEvent.TenantId) &&
            !_options.TenantFilter.Contains(cloudEvent.TenantId))
        {
            return false;
        }

        return true;
    }

    private EventGridPublisherClient CreateEventGridClient()
    {
        TokenCredential? credential = null;

        if (_options.UseManagedIdentity)
        {
            credential = new DefaultAzureCredential();
            _logger.LogInformation("Using Managed Identity for Event Grid authentication");
        }

        var endpoint = _options.TopicEndpoint ?? _options.DomainEndpoint;
        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException("Event Grid endpoint is not configured");
        }

        var endpointUri = new Uri(endpoint);

        if (credential != null)
        {
            return new EventGridPublisherClient(endpointUri, credential);
        }
        else
        {
            var accessKey = _options.TopicKey ?? _options.DomainKey;
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new InvalidOperationException("Event Grid access key is not configured");
            }

            return new EventGridPublisherClient(endpointUri, new AzureKeyCredential(accessKey));
        }
    }

    private ResiliencePipeline CreateResiliencePipeline()
    {
        var pipelineBuilder = new ResiliencePipelineBuilder();

        // Retry with exponential backoff
        pipelineBuilder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = _options.Retry.MaxRetries,
            Delay = TimeSpan.FromSeconds(_options.Retry.InitialDelaySeconds),
            MaxDelay = TimeSpan.FromSeconds(_options.Retry.MaxDelaySeconds),
            BackoffType = DelayBackoffType.Exponential,
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Retrying Event Grid publish (attempt {Attempt}): {Exception}",
                    args.AttemptNumber,
                    args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        // Circuit breaker
        if (_options.CircuitBreaker.Enabled)
        {
            pipelineBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = _options.CircuitBreaker.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreaker.DurationOfBreakSeconds),
                OnOpened = args =>
                {
                    _metrics.CircuitBreakerState = "Open";
                    _logger.LogError("Event Grid circuit breaker opened due to failures");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _metrics.CircuitBreakerState = "Closed";
                    _logger.LogInformation("Event Grid circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _metrics.CircuitBreakerState = "HalfOpen";
                    _logger.LogInformation("Event Grid circuit breaker half-open (testing)");
                    return ValueTask.CompletedTask;
                }
            });
        }

        return pipelineBuilder.Build();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _eventQueue.Writer.TryComplete();
        _flushLock.Dispose();
        _disposed = true;
    }
}
