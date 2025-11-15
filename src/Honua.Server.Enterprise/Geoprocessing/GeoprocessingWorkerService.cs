// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Geoprocessing.Operations;
using Honua.Server.Enterprise.Geoprocessing.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly.Timeout;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Background service that processes geoprocessing jobs from the queue.
/// Handles concurrent job execution, progress tracking, and failure handling.
/// </summary>
public sealed class GeoprocessingWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GeoprocessingWorkerService> _logger;
    private readonly IGeoprocessingToAlertBridgeService? _alertBridge;
    private readonly IGeoprocessingMetrics? _metrics;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Dictionary<string, IGeoprocessingOperation> _operations;
    private int _activeJobs = 0;

    // Configuration
    private const int MaxConcurrentJobs = 5;
    private const int PollIntervalSeconds = 5;
    private const int JobTimeoutMinutes = 30;
    private const int SlaThresholdMinutes = 5; // Alert if job queued > 5 minutes

    // Progress update throttling configuration
    private const int MinProgressUpdateIntervalMs = 2000; // Minimum 2 seconds between database updates
    private const int MinProgressPercentDelta = 5; // Only update if progress changed by at least 5%

    public GeoprocessingWorkerService(
        IServiceProvider serviceProvider,
        ILogger<GeoprocessingWorkerService> logger,
        IGeoprocessingToAlertBridgeService? alertBridge = null,
        IGeoprocessingMetrics? metrics = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alertBridge = alertBridge; // Optional - allows graceful degradation if alerting is not configured
        _metrics = metrics; // Optional - allows graceful degradation if metrics are not configured

        // Create semaphore for concurrency control
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentJobs, MaxConcurrentJobs);

        // Register all available operations
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

        _logger.LogInformation(
            "GeoprocessingWorkerService initialized: MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s, Timeout={Timeout}min, SLA={SlaMinutes}min, Operations={OperationCount}, AlertBridge={AlertBridgeEnabled}, Metrics={MetricsEnabled}",
            MaxConcurrentJobs, PollIntervalSeconds, JobTimeoutMinutes, SlaThresholdMinutes, _operations.Count, _alertBridge != null, _metrics != null);
    }

    /// <summary>
    /// Main execution loop for the background service.
    /// Continuously polls for pending jobs and processes them concurrently.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GeoprocessingWorkerService starting");

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var controlPlane = scope.ServiceProvider.GetRequiredService<IControlPlane>();

                // Get next job from queue
                var job = await controlPlane.DequeueNextJobAsync(stoppingToken);

                if (job != null)
                {
                    // Check for SLA breach (job queued too long)
                    var queueWait = job.GetQueueWait();
                    if (queueWait.HasValue && queueWait.Value.TotalMinutes > SlaThresholdMinutes)
                    {
                        _logger.LogWarning(
                            "Job {JobId} SLA breach: queued for {QueueWaitMinutes:0.1}min (SLA: {SlaThresholdMinutes}min)",
                            job.JobId, queueWait.Value.TotalMinutes, SlaThresholdMinutes);

                        // Record SLA breach metrics
                        _metrics?.RecordSlaBreach(
                            job.ProcessId,
                            job.Priority,
                            queueWait.Value,
                            TimeSpan.FromMinutes(SlaThresholdMinutes));

                        // Fire and forget alert delivery - don't wait for alert to be sent
                        if (_alertBridge != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                var alertStopwatch = Stopwatch.StartNew();
                                var alertType = "sla_breach";
                                var severity = DetermineAlertSeverity(job.Priority, queueWait.Value.TotalMinutes / SlaThresholdMinutes);

                                _metrics?.RecordAlertAttempt(alertType, severity);

                                try
                                {
                                    await _alertBridge.ProcessJobSlaBreachAsync(
                                        job,
                                        (int)queueWait.Value.TotalMinutes,
                                        SlaThresholdMinutes,
                                        stoppingToken);

                                    alertStopwatch.Stop();
                                    _metrics?.RecordAlertSuccess(alertType, severity, alertStopwatch.Elapsed);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to send SLA breach alert for job {JobId}", job.JobId);
                                    _metrics?.RecordAlertFailure(alertType, severity, ex.GetType().Name);
                                }
                            }, stoppingToken);
                        }
                    }

                    // Record SLA compliance for all jobs
                    if (queueWait.HasValue)
                    {
                        _metrics?.RecordSlaCompliance(
                            job.ProcessId,
                            job.Priority,
                            queueWait.Value,
                            queueWait.Value.TotalMinutes > SlaThresholdMinutes);
                    }

                    _logger.LogInformation(
                        "Found pending job {JobId} for process {ProcessId} from tenant {TenantId} (queued: {QueueWaitMinutes:0.1}min)",
                        job.JobId, job.ProcessId, job.TenantId, queueWait?.TotalMinutes ?? 0);

                    // Wait for available slot (respects concurrency limit)
                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                    var newActiveCount = Interlocked.Increment(ref _activeJobs);
                    _metrics?.RecordActiveJobCount(newActiveCount);

                    // Process job in background task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessJobAsync(job, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception processing job {JobId}", job.JobId);
                        }
                        finally
                        {
                            var remainingActive = Interlocked.Decrement(ref _activeJobs);
                            _metrics?.RecordActiveJobCount(remainingActive);
                            _concurrencySemaphore.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No pending jobs, wait before polling again
                    _logger.LogDebug("No pending jobs, waiting {Seconds}s before next poll", PollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("GeoprocessingWorkerService stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GeoprocessingWorkerService main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("GeoprocessingWorkerService stopped");
    }

    /// <summary>
    /// Processes a single geoprocessing job from start to finish.
    /// </summary>
    private async Task ProcessJobAsync(ProcessRun job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<IControlPlane>();

        var stopwatch = Stopwatch.StartNew();
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        jobCts.CancelAfter(TimeSpan.FromMinutes(JobTimeoutMinutes));

        // Progress tracking for throttling
        var lastProgressUpdateTime = DateTimeOffset.MinValue;
        var lastProgressPercent = 0;

        try
        {
            _logger.LogInformation("Starting job {JobId} for process {ProcessId}", job.JobId, job.ProcessId);

            // Record job started metrics
            var tier = job.ActualTier?.ToString() ?? "NTS";
            _metrics?.RecordJobStarted(job.ProcessId, job.Priority, tier);

            // Get the operation handler
            if (!_operations.TryGetValue(job.ProcessId, out var operation))
            {
                throw new InvalidOperationException($"Operation '{job.ProcessId}' not found or not supported");
            }

            // Progress callback with throttling to prevent excessive database updates
            var progress = new Progress<GeoprocessingProgress>(p =>
            {
                try
                {
                    _logger.LogDebug(
                        "Job {JobId} progress: {Percent}% - {Message}",
                        job.JobId, p.ProgressPercent, p.Message);

                    var now = DateTimeOffset.UtcNow;
                    var timeSinceLastUpdate = (now - lastProgressUpdateTime).TotalMilliseconds;
                    var progressDelta = Math.Abs(p.ProgressPercent - lastProgressPercent);

                    // Throttle progress updates to database:
                    // - Always update if it's been more than 2 seconds since last update AND progress changed by 5%+
                    // - Always update at 0%, 25%, 50%, 75%, and 100% milestones
                    // - This prevents excessive database load while ensuring meaningful progress visibility
                    var shouldUpdate = p.ProgressPercent == 0 ||
                                     p.ProgressPercent == 25 ||
                                     p.ProgressPercent == 50 ||
                                     p.ProgressPercent == 75 ||
                                     p.ProgressPercent == 100 ||
                                     (timeSinceLastUpdate >= MinProgressUpdateIntervalMs && progressDelta >= MinProgressPercentDelta);

                    if (shouldUpdate)
                    {
                        // Fire-and-forget pattern for progress updates to avoid blocking the operation
                        // If update fails, it's logged but doesn't affect job execution
                        _metrics?.RecordProgressUpdateAttempt(job.ProcessId);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await controlPlane.UpdateJobProgressAsync(
                                    job.JobId,
                                    p.ProgressPercent,
                                    p.Message,
                                    jobCts.Token);

                                lastProgressUpdateTime = now;
                                lastProgressPercent = p.ProgressPercent;

                                _metrics?.RecordProgressUpdateSuccess(job.ProcessId, p.ProgressPercent);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to persist progress update for job {JobId}", job.JobId);
                                _metrics?.RecordProgressUpdateFailure(job.ProcessId, ex.GetType().Name);
                            }
                        }, jobCts.Token);
                    }
                    else if (progressDelta > 0)
                    {
                        // Record throttled update
                        var throttleReason = timeSinceLastUpdate < MinProgressUpdateIntervalMs
                            ? "time_interval"
                            : "progress_delta";
                        _metrics?.RecordProgressUpdateThrottled(job.ProcessId, throttleReason);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to handle progress update for job {JobId}", job.JobId);
                }
            });

            // Convert ProcessRun inputs to GeoprocessingInputs
            var inputs = ConvertInputs(job.Inputs);

            // Execute the operation with idempotency guarantees
            // This will check cache before execution and store results after successful completion
            var processResult = await _serviceProvider.ExecuteWithIdempotencyAsync(
                job,
                async (j, ct) =>
                {
                    // Execute the actual geoprocessing operation
                    var opResult = await operation.ExecuteAsync(
                        j.Inputs ?? new Dictionary<string, object>(),
                        inputs,
                        progress,
                        ct);

                    // Convert GeoprocessingResult to ProcessResult
                    if (!opResult.Success)
                    {
                        return new ProcessResult
                        {
                            JobId = j.JobId,
                            ProcessId = j.ProcessId,
                            Status = ProcessRunStatus.Failed,
                            Success = false,
                            ErrorMessage = opResult.ErrorMessage ?? "Unknown error",
                            DurationMs = opResult.DurationMs,
                            FeaturesProcessed = opResult.FeaturesProcessed
                        };
                    }

                    return new ProcessResult
                    {
                        JobId = j.JobId,
                        ProcessId = j.ProcessId,
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
                },
                jobCts.Token);

            if (processResult.Success)
            {
                _logger.LogInformation(
                    "Job {JobId} completed successfully in {Duration:0.0}s, processed {Features} features",
                    job.JobId,
                    processResult.DurationMs.HasValue ? processResult.DurationMs.Value / 1000.0 : 0,
                    processResult.FeaturesProcessed ?? 0);

                // Record success metrics
                _metrics?.RecordJobCompleted(
                    job.ProcessId,
                    job.Priority,
                    tier,
                    TimeSpan.FromMilliseconds(processResult.DurationMs ?? 0),
                    processResult.FeaturesProcessed ?? 0);

                // Record completion
                await controlPlane.RecordCompletionAsync(
                    job.JobId,
                    processResult,
                    job.ActualTier ?? ProcessExecutionTier.NTS,
                    TimeSpan.FromMilliseconds(processResult.DurationMs ?? 0),
                    cancellationToken);

                // Enqueue webhook delivery if configured
                await controlPlane.EnqueueWebhookAsync(job, processResult, cancellationToken);
            }
            else
            {
                await HandleJobFailureAsync(
                    job,
                    new Exception(processResult.ErrorMessage ?? "Unknown error"),
                    controlPlane,
                    stopwatch.Elapsed,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (jobCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Job {JobId} timed out after {Timeout} minutes", job.JobId, JobTimeoutMinutes);

            var tier = job.ActualTier?.ToString() ?? "NTS";
            var error = new TimeoutException($"Job timed out after {JobTimeoutMinutes} minutes");

            // Record timeout metrics
            _metrics?.RecordJobTimeout(job.ProcessId, job.Priority, tier, stopwatch.Elapsed);

            await controlPlane.RecordFailureAsync(
                job.JobId,
                error,
                job.ActualTier ?? ProcessExecutionTier.NTS,
                stopwatch.Elapsed,
                cancellationToken);

            // Enqueue webhook delivery for timeout notification
            var timeoutResult = new ProcessResult
            {
                JobId = job.JobId,
                ProcessId = job.ProcessId,
                Status = ProcessRunStatus.Timeout,
                Success = false,
                ErrorMessage = error.Message,
                DurationMs = (long)stopwatch.Elapsed.TotalMilliseconds
            };

            await controlPlane.EnqueueWebhookAsync(job, timeoutResult, cancellationToken);

            // Send timeout alert (fire-and-forget)
            if (_alertBridge != null)
            {
                _ = Task.Run(async () =>
                {
                    var alertStopwatch = Stopwatch.StartNew();
                    var alertType = "job_timeout";
                    var severity = job.Priority >= 7 ? "critical" : "warning";

                    _metrics?.RecordAlertAttempt(alertType, severity);

                    try
                    {
                        await _alertBridge.ProcessJobTimeoutAsync(job, JobTimeoutMinutes, cancellationToken);

                        alertStopwatch.Stop();
                        _metrics?.RecordAlertSuccess(alertType, severity, alertStopwatch.Elapsed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send timeout alert for job {JobId}", job.JobId);
                        _metrics?.RecordAlertFailure(alertType, severity, ex.GetType().Name);
                    }
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed with exception", job.JobId);
            await HandleJobFailureAsync(job, ex, controlPlane, stopwatch.Elapsed, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            jobCts.Dispose();
        }
    }

    /// <summary>
    /// Handles job failures with automatic retry for transient errors.
    /// </summary>
    private async Task HandleJobFailureAsync(
        ProcessRun job,
        Exception error,
        IControlPlane controlPlane,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        // Check if error is transient and we haven't exceeded max retries
        var isTransient = IsTransientError(error);
        var canRetry = isTransient && job.RetryCount < job.MaxRetries;
        var tier = job.ActualTier?.ToString() ?? "NTS";
        var errorType = error.GetType().Name;

        if (canRetry)
        {
            var newRetryCount = job.RetryCount + 1;
            var backoffDelay = CalculateExponentialBackoff(newRetryCount);

            _logger.LogWarning(
                "Job {JobId} failed with transient error (attempt {Attempt}/{MaxRetries}). " +
                "Will retry after {DelaySeconds}s backoff. Error: {Error}",
                job.JobId, newRetryCount, job.MaxRetries, backoffDelay.TotalSeconds, error.Message);

            // Record retry metrics
            _metrics?.RecordJobRetry(job.ProcessId, job.Priority, newRetryCount, errorType);

            // Wait for exponential backoff before requeueing
            await Task.Delay(backoffDelay, cancellationToken);

            // Requeue the job for retry
            var requeued = await controlPlane.RequeueJobForRetryAsync(
                job.JobId,
                newRetryCount,
                $"Retry {newRetryCount}/{job.MaxRetries}: {error.Message}",
                cancellationToken);

            if (requeued)
            {
                _logger.LogInformation(
                    "Job {JobId} successfully requeued for retry (attempt {Attempt}/{MaxRetries})",
                    job.JobId, newRetryCount, job.MaxRetries);
                return;
            }
            else
            {
                _logger.LogError(
                    "Failed to requeue job {JobId} for retry. Marking as permanently failed.",
                    job.JobId);
            }
        }
        else if (isTransient)
        {
            _logger.LogError(
                "Job {JobId} failed with transient error after {Attempts} retries. " +
                "Max retries ({MaxRetries}) exceeded. Marking as permanently failed. Error: {Error}",
                job.JobId, job.RetryCount, job.MaxRetries, error.Message);
        }
        else
        {
            _logger.LogError(
                "Job {JobId} failed with non-transient error. No retry will be attempted. Error: {Error}",
                job.JobId, error.Message);
        }

        // Permanent failure - record it
        _metrics?.RecordJobFailed(job.ProcessId, job.Priority, tier, duration, errorType, isTransient);

        await controlPlane.RecordFailureAsync(
            job.JobId,
            error,
            job.ActualTier ?? ProcessExecutionTier.NTS,
            duration,
            cancellationToken);

        // Enqueue webhook delivery for failure notification if this is final failure
        if (!canRetry)
        {
            var failureResult = new ProcessResult
            {
                JobId = job.JobId,
                ProcessId = job.ProcessId,
                Status = ProcessRunStatus.Failed,
                Success = false,
                ErrorMessage = SanitizeErrorMessage(error),
                ErrorDetails = null, // Never include stack traces in webhook responses
                DurationMs = (long)duration.TotalMilliseconds
            };

            await controlPlane.EnqueueWebhookAsync(job, failureResult, cancellationToken);
        }

        // Send failure alert if retries are exhausted or error is non-transient (fire-and-forget)
        // Note: We only alert on final failures after all retries have been attempted
        if (_alertBridge != null && job.RetryCount >= job.MaxRetries)
        {
            _ = Task.Run(async () =>
            {
                var alertStopwatch = Stopwatch.StartNew();
                var alertType = "job_failure";
                var severity = job.Priority >= 9 ? "critical" : job.Priority >= 7 ? "error" : "warning";

                _metrics?.RecordAlertAttempt(alertType, severity);

                try
                {
                    await _alertBridge.ProcessJobFailureAsync(job, error, cancellationToken);

                    alertStopwatch.Stop();
                    _metrics?.RecordAlertSuccess(alertType, severity, alertStopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send failure alert for job {JobId}", job.JobId);
                    _metrics?.RecordAlertFailure(alertType, severity, ex.GetType().Name);
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Sanitizes error messages for public consumption by removing sensitive information.
    /// Prevents leaking internal paths, connection strings, and implementation details.
    /// </summary>
    private static string SanitizeErrorMessage(Exception exception)
    {
        // Use only the top-level exception message, never inner exceptions or stack traces
        // Avoid exposing database connection strings, file paths, or other sensitive data
        return exception switch
        {
            NpgsqlException => "Database operation failed",
            DbException => "Database operation failed",
            TimeoutException => "Operation timed out",
            OperationCanceledException => "Operation was cancelled",
            InvalidOperationException invalidOp => $"Invalid operation: {StripSensitiveInfo(invalidOp.Message)}",
            ArgumentException arg => $"Invalid argument: {StripSensitiveInfo(arg.Message)}",
            _ => $"Operation failed: {StripSensitiveInfo(exception.Message)}"
        };
    }

    /// <summary>
    /// Strips potentially sensitive information from error messages.
    /// Removes file paths, connection strings, and internal implementation details.
    /// </summary>
    private static string StripSensitiveInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "An error occurred";

        // Remove common patterns that may contain sensitive information
        var sanitized = message;

        // Remove file paths (Windows and Unix)
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[A-Za-z]:\\[^:]*|/[^:]*(?=/|$)",
            "[path]");

        // Remove connection strings
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(Server|Host|Database|User|Password|Uid|Pwd)\s*=\s*[^;]*",
            "$1=[redacted]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Limit message length to prevent verbose error messages
        const int maxLength = 200;
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength) + "...";
        }

        return sanitized;
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried.
    /// Based on ResiliencePolicies transient error detection.
    /// </summary>
    private static bool IsTransientError(Exception exception)
    {
        return exception switch
        {
            // Network timeouts
            TimeoutException => true,
            TimeoutRejectedException => true,
            OperationCanceledException ex when ex.InnerException is TimeoutException => true,

            // Network connectivity issues
            SocketException => true,
            HttpRequestException httpEx => httpEx.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable => true,      // 503
                HttpStatusCode.GatewayTimeout => true,          // 504
                HttpStatusCode.RequestTimeout => true,          // 408
                HttpStatusCode.TooManyRequests => true,         // 429
                >= HttpStatusCode.InternalServerError => true,  // 5xx errors
                _ => false
            },

            // Database connection failures and transient errors
            NpgsqlException npgsqlEx => IsTransientDatabaseError(npgsqlEx),
            DbException dbEx => IsTransientDatabaseError(dbEx),

            // TaskCanceledException from timeout (not user cancellation)
            TaskCanceledException taskEx => !taskEx.CancellationToken.IsCancellationRequested,

            // Not a transient error
            _ => false
        };
    }

    /// <summary>
    /// Determines if a database exception is transient and should be retried.
    /// Handles PostgreSQL-specific error codes.
    /// </summary>
    private static bool IsTransientDatabaseError(Exception exception)
    {
        if (exception is NpgsqlException npgsqlEx)
        {
            // PostgreSQL error codes for transient failures
            // See: https://www.postgresql.org/docs/current/errcodes-appendix.html
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

        if (exception is DbException dbEx)
        {
            // Check message for common transient error patterns
            var message = dbEx.Message.ToLowerInvariant();
            return message.Contains("timeout") ||
                   message.Contains("deadlock") ||
                   message.Contains("connection") ||
                   message.Contains("temporarily unavailable");
        }

        return false;
    }

    /// <summary>
    /// Calculates exponential backoff delay for retry attempts.
    /// Uses formula: min(initialDelay * 2^(attempt-1), maxDelay)
    /// </summary>
    private static TimeSpan CalculateExponentialBackoff(int retryAttempt)
    {
        const double initialDelaySeconds = 5.0;  // Start with 5 seconds
        const double maxDelaySeconds = 60.0;     // Cap at 60 seconds

        // Calculate: 5s, 10s, 20s, 40s, 60s (capped)
        var delaySeconds = Math.Min(
            initialDelaySeconds * Math.Pow(2, retryAttempt - 1),
            maxDelaySeconds);

        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Converts ProcessRun inputs to GeoprocessingInputs for operation execution
    /// </summary>
    private List<GeoprocessingInput> ConvertInputs(Dictionary<string, object>? inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            return new List<GeoprocessingInput>();
        }

        var result = new List<GeoprocessingInput>();

        foreach (var kvp in inputs)
        {
            // Simple conversion - in production, this would be more sophisticated
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
    /// Determines alert severity based on job priority and breach factor.
    /// </summary>
    private static string DetermineAlertSeverity(int priority, double breachFactor)
    {
        return (priority, breachFactor) switch
        {
            ( >= 9, >= 3.0) => "critical",
            ( >= 7, _) => "error",
            (_, >= 5.0) => "error",
            _ => "warning"
        };
    }

    /// <summary>
    /// Gracefully stops the service, waiting for in-progress jobs to complete.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GeoprocessingWorkerService stopping gracefully");

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
