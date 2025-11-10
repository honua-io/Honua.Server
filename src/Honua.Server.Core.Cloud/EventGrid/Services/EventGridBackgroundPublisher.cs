// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.EventGrid.Services;

/// <summary>
/// Background service that periodically flushes batched events to Event Grid.
/// </summary>
public class EventGridBackgroundPublisher : BackgroundService
{
    private readonly EventGridPublisher _publisher;
    private readonly EventGridOptions _options;
    private readonly ILogger<EventGridBackgroundPublisher> _logger;

    public EventGridBackgroundPublisher(
        IEventGridPublisher publisher,
        IOptions<EventGridOptions> options,
        ILogger<EventGridBackgroundPublisher> logger)
    {
        // We need the concrete type to access internal methods
        _publisher = (EventGridPublisher)publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Event Grid background publisher is disabled");
            return;
        }

        _logger.LogInformation(
            "Event Grid background publisher started: FlushInterval={FlushInterval}s",
            _options.FlushIntervalSeconds);

        var flushInterval = TimeSpan.FromSeconds(_options.FlushIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(flushInterval, stoppingToken).ConfigureAwait(false);

                // Process and publish batch
                await _publisher.ProcessBatchAsync(stoppingToken).ConfigureAwait(false);

                // Log metrics periodically
                var metrics = _publisher.GetMetrics();
                if (metrics.EventsPublished > 0 || metrics.EventsFailed > 0 || metrics.EventsDropped > 0)
                {
                    _logger.LogDebug(
                        "Event Grid metrics: Published={Published}, Failed={Failed}, Dropped={Dropped}, Filtered={Filtered}, Queue={Queue}, Circuit={Circuit}",
                        metrics.EventsPublished,
                        metrics.EventsFailed,
                        metrics.EventsDropped,
                        metrics.EventsFiltered,
                        metrics.CurrentQueueSize,
                        metrics.CircuitBreakerState);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Event Grid background publisher");
                // Continue running despite errors
            }
        }

        // Flush remaining events on shutdown
        try
        {
            _logger.LogInformation("Flushing remaining events on shutdown");
            await _publisher.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing events on shutdown");
        }

        _logger.LogInformation("Event Grid background publisher stopped");
    }
}
