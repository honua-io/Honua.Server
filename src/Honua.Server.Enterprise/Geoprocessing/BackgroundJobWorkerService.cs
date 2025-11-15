// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.BackgroundJobs;
using Honua.Server.Enterprise.Geoprocessing.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Generic background job worker service using the new IBackgroundJobQueue abstraction.
/// Processes jobs from pluggable queue backends (PostgreSQL, AWS SQS, Azure Service Bus, RabbitMQ).
/// </summary>
/// <remarks>
/// Key improvements over the original GeoprocessingWorkerService:
///
/// 1. Pluggable queue backend: Works with any IBackgroundJobQueue implementation
/// 2. Idempotency support: Prevents duplicate processing using IIdempotencyStore
/// 3. Retry with exponential backoff: Uses Polly for resilient retry logic
/// 4. Comprehensive metrics: OpenTelemetry instrumentation for observability
/// 5. Graceful shutdown: Completes in-flight jobs before stopping
///
/// Architecture:
/// - Main loop: Continuously receives messages from queue
/// - Concurrency control: Semaphore limits concurrent job execution
/// - Idempotency check: Cache lookup before processing
/// - Retry logic: Polly resilience pipeline for transient failures
/// - Metrics: Counter, histogram, and gauge metrics via OpenTelemetry
/// </remarks>
public sealed class BackgroundJobWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<BackgroundJobWorkerService> _logger;
    private readonly BackgroundJobMetrics _metrics;
    private readonly BackgroundJobsOptions _options;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Dictionary<string, IGeoprocessingOperation> _operations;
    private int _activeJobs = 0;

    private readonly AsyncRetryPolicy _retryPolicy;

    public BackgroundJobWorkerService(
        IServiceProvider serviceProvider,
        IBackgroundJobQueue jobQueue,
        IIdempotencyStore idempotencyStore,
        ILogger<BackgroundJobWorkerService> logger,
        BackgroundJobMetrics metrics,
        IOptions<BackgroundJobsOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _jobQueue = jobQueue ?? throw new ArgumentNullException(nameof(jobQueue));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _concurrencySemaphore = new SemaphoreSlim(
            _options.MaxConcurrentJobs,
            _options.MaxConcurrentJobs);

        // Register all available geoprocessing operations
        _operations = new Dictionary<string, IGeoprocessingOperation>
        {
            [GeoprocessingOperation.Buffer] = new BufferOperation(),
            [GeoprocessingOperation.Intersection] = new IntersectionOperation(),
            [GeoprocessingOperation.Union] = new UnionOperation(),
            [GeoprocessingOperation.Difference] = new DifferenceOperation(),
            [GeoprocessingOperation.Simplify] = new SimplifyOperation(),
            [GeoprocessingOperation.ConvexHull] = new ConvexHullOperation(),
            [GeoprocessingOperation.Dissolve] = new DissolveOperation()
        };

        // Configure Polly retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s: {ErrorMessage}",
                        retryCount,
                        _options.MaxRetries,
                        timeSpan.TotalSeconds,
                        exception.Message);
                });

        // Set queue depth provider for metrics
        _metrics.SetQueueDepthProvider(() =>
        {
            try
            {
                return _jobQueue.GetQueueDepthAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return 0;
            }
        });

        _logger.LogInformation(
            "BackgroundJobWorkerService initialized: Mode={Mode}, Provider={Provider}, MaxConcurrent={MaxConcurrent}, Operations={OperationCount}",
            _options.Mode,
            _options.Provider,
            _options.MaxConcurrentJobs,
            _operations.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundJobWorkerService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Receive jobs from queue (long polling for message queues, immediate for database)
                var messages = await _jobQueue.ReceiveAsync<ProcessRun>(
                    maxMessages: _options.MaxConcurrentJobs,
                    cancellationToken: stoppingToken);

                if (!messages.Any())
                {
                    // No messages available
                    // For database polling: wait before next poll
                    // For message queues: this shouldn't happen due to long polling
                    if (_options.Mode == BackgroundJobMode.Polling)
                    {
                        _logger.LogDebug(
                            "No pending jobs, waiting {Seconds}s before next poll",
                            _options.PollIntervalSeconds);

                        await Task.Delay(
                            TimeSpan.FromSeconds(_options.PollIntervalSeconds),
                            stoppingToken);
                    }

                    continue;
                }

                _logger.LogDebug("Received {Count} jobs from queue", messages.Count());

                // Process each message concurrently (up to MaxConcurrentJobs)
                foreach (var message in messages)
                {
                    // Wait for available slot
                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                    Interlocked.Increment(ref _activeJobs);

                    // Process job in background task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessJobAsync(message, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception processing job {JobId}", message.Body.JobId);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeJobs);
                            _concurrencySemaphore.Release();
                        }
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("BackgroundJobWorkerService stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundJobWorkerService main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("BackgroundJobWorkerService stopped");
    }

    /// <summary>
    /// Processes a single job from start to finish with idempotency checks and retry logic.
    /// </summary>
    private async Task ProcessJobAsync(QueueMessage<ProcessRun> message, CancellationToken cancellationToken)
    {
        var job = message.Body;
        var stopwatch = Stopwatch.StartNew();

        using var scope = _serviceProvider.CreateScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<IControlPlane>();

        try
        {
            _logger.LogInformation(
                "Processing job {JobId} for process {ProcessId} from tenant {TenantId} (delivery count: {DeliveryCount})",
                job.JobId,
                job.ProcessId,
                job.TenantId,
                message.DeliveryCount);

            // Calculate queue wait time for metrics
            var queueWait = message.EnqueuedAt.HasValue
                ? DateTimeOffset.UtcNow - message.EnqueuedAt.Value
                : TimeSpan.Zero;

            // Check idempotency (if enabled)
            if (_options.EnableIdempotency)
            {
                var idempotencyKey = ComputeIdempotencyKey(job);
                var cachedResult = await _idempotencyStore.GetAsync<ProcessResult>(
                    idempotencyKey,
                    cancellationToken);

                if (cachedResult != null)
                {
                    _logger.LogInformation(
                        "Job {JobId} already processed (idempotency cache hit), using cached result",
                        job.JobId);

                    // Complete message (don't reprocess)
                    await _jobQueue.CompleteAsync(message.ReceiptHandle, cancellationToken);

                    _metrics.RecordJobCompleted(job.ProcessId, stopwatch.Elapsed, queueWait);
                    return;
                }
            }

            // Get the operation handler
            if (!_operations.TryGetValue(job.ProcessId, out var operation))
            {
                throw new InvalidOperationException($"Operation '{job.ProcessId}' not found or not supported");
            }

            // Execute job with retry policy
            var result = await _retryPolicy.ExecuteAsync(async ct =>
            {
                return await ExecuteJobWithTimeoutAsync(job, operation, controlPlane, ct);
            }, cancellationToken);

            // Store result in idempotency cache
            if (_options.EnableIdempotency && result.Success)
            {
                var idempotencyKey = ComputeIdempotencyKey(job);
                var ttl = TimeSpan.FromDays(_options.IdempotencyTtlDays);

                await _idempotencyStore.StoreAsync(idempotencyKey, result, ttl, cancellationToken);
            }

            // Complete message (remove from queue)
            await _jobQueue.CompleteAsync(message.ReceiptHandle, cancellationToken);

            // Record metrics
            if (result.Success)
            {
                _metrics.RecordJobCompleted(job.ProcessId, stopwatch.Elapsed, queueWait);
                _logger.LogInformation(
                    "Job {JobId} completed successfully in {Duration:0.0}s",
                    job.JobId,
                    stopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                _metrics.RecordJobFailed(
                    job.ProcessId,
                    "ProcessingError",
                    isTransient: false,
                    stopwatch.Elapsed);

                _logger.LogError(
                    "Job {JobId} failed: {ErrorMessage}",
                    job.JobId,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed with exception", job.JobId);

            // Abandon message (retry if transient error)
            if (IsTransientError(ex) && message.DeliveryCount < _options.MaxRetries)
            {
                _logger.LogWarning(
                    "Job {JobId} failed with transient error (attempt {Attempt}/{MaxRetries}), will retry",
                    job.JobId,
                    message.DeliveryCount,
                    _options.MaxRetries);

                await _jobQueue.AbandonAsync(message.ReceiptHandle, cancellationToken);

                _metrics.RecordJobRetry(job.ProcessId, message.DeliveryCount, ex.GetErrorType());
            }
            else
            {
                _logger.LogError(
                    "Job {JobId} failed permanently after {Attempts} attempts",
                    job.JobId,
                    message.DeliveryCount);

                // Complete message (don't retry further, move to DLQ if configured)
                await _jobQueue.CompleteAsync(message.ReceiptHandle, cancellationToken);

                _metrics.RecordJobFailed(
                    job.ProcessId,
                    ex.GetErrorType(),
                    IsTransientError(ex),
                    stopwatch.Elapsed);

                // Record failure in control plane
                await controlPlane.RecordFailureAsync(
                    job.JobId,
                    ex,
                    job.ActualTier ?? ProcessExecutionTier.NTS,
                    stopwatch.Elapsed,
                    cancellationToken);
            }
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Executes a job with timeout control
    /// </summary>
    private async Task<ProcessResult> ExecuteJobWithTimeoutAsync(
        ProcessRun job,
        IGeoprocessingOperation operation,
        IControlPlane controlPlane,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(_options.JobTimeoutMinutes));

        var progress = new Progress<GeoprocessingProgress>(p =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await controlPlane.UpdateJobProgressAsync(
                        job.JobId,
                        p.ProgressPercent,
                        p.Message,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update progress for job {JobId}", job.JobId);
                }
            }, cancellationToken);
        });

        // Convert ProcessRun inputs to GeoprocessingInputs
        var inputs = ConvertInputs(job.Inputs);

        // Execute the operation
        var opResult = await operation.ExecuteAsync(
            job.Inputs ?? new Dictionary<string, object>(),
            inputs,
            progress,
            timeoutCts.Token);

        // Convert to ProcessResult
        if (!opResult.Success)
        {
            return new ProcessResult
            {
                JobId = job.JobId,
                ProcessId = job.ProcessId,
                Status = ProcessRunStatus.Failed,
                Success = false,
                ErrorMessage = opResult.ErrorMessage ?? "Unknown error",
                DurationMs = opResult.DurationMs,
                FeaturesProcessed = opResult.FeaturesProcessed
            };
        }

        var result = new ProcessResult
        {
            JobId = job.JobId,
            ProcessId = job.ProcessId,
            Status = ProcessRunStatus.Completed,
            Success = true,
            Output = opResult.Data,
            Metadata = new Dictionary<string, object>
            {
                { "features_processed", opResult.FeaturesProcessed },
                { "duration_ms", opResult.DurationMs }
            },
            DurationMs = opResult.DurationMs,
            FeaturesProcessed = opResult.FeaturesProcessed
        };

        // Record completion in control plane
        await controlPlane.RecordCompletionAsync(
            job.JobId,
            result,
            job.ActualTier ?? ProcessExecutionTier.NTS,
            TimeSpan.FromMilliseconds(opResult.DurationMs),
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Computes idempotency key for a job
    /// </summary>
    private static string ComputeIdempotencyKey(ProcessRun job)
    {
        // Use job ID as idempotency key (jobs are already unique)
        // In more complex scenarios, you might hash the inputs as well
        return $"job:{job.JobId}";
    }

    /// <summary>
    /// Converts ProcessRun inputs to GeoprocessingInputs
    /// </summary>
    private static List<GeoprocessingInput> ConvertInputs(Dictionary<string, object>? inputs)
    {
        if (inputs == null || inputs.Count == 0)
            return new List<GeoprocessingInput>();

        var result = new List<GeoprocessingInput>();

        foreach (var kvp in inputs)
        {
            if (kvp.Value is Dictionary<string, object> dict)
            {
                result.Add(new GeoprocessingInput
                {
                    Name = kvp.Key,
                    Type = dict.GetValueOrDefault("type")?.ToString() ?? "geojson",
                    Source = dict.GetValueOrDefault("source")?.ToString() ?? string.Empty,
                    Filter = dict.GetValueOrDefault("filter")?.ToString()
                });
            }
            else if (kvp.Value is string str)
            {
                result.Add(new GeoprocessingInput
                {
                    Name = kvp.Key,
                    Type = "geojson",
                    Source = str
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried
    /// </summary>
    private static bool IsTransientError(Exception exception)
    {
        // Network errors
        if (exception is System.Net.Http.HttpRequestException ||
            exception is System.Net.Sockets.SocketException ||
            exception is TimeoutException)
        {
            return true;
        }

        // Database errors
        if (exception is Npgsql.NpgsqlException npgsqlEx)
        {
            // PostgreSQL transient error codes
            return npgsqlEx.SqlState switch
            {
                "40001" => true,  // serialization_failure
                "40P01" => true,  // deadlock_detected
                "55P03" => true,  // lock_not_available
                "57014" => true,  // query_canceled
                "08006" => true,  // connection_failure
                "08003" => true,  // connection_does_not_exist
                "08001" => true,  // sqlclient_unable_to_establish_sqlconnection
                _ => false
            };
        }

        return false;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BackgroundJobWorkerService stopping gracefully");

        if (_activeJobs > 0)
        {
            _logger.LogInformation(
                "Waiting for {Count} active jobs to complete (timeout: 30s)",
                _activeJobs);

            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);

            while (_activeJobs > 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(1000, cancellationToken);
            }

            if (_activeJobs > 0)
            {
                _logger.LogWarning(
                    "{Count} jobs still in progress after timeout, forcing shutdown",
                    _activeJobs);
            }
            else
            {
                _logger.LogInformation("All active jobs completed, shutting down");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        base.Dispose();
    }
}
