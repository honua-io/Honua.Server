// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Geoprocessing.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Background service that processes geoprocessing jobs from the queue.
/// Handles concurrent job execution, progress tracking, and failure handling.
/// </summary>
public sealed class GeoprocessingWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GeoprocessingWorkerService> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Dictionary<string, IGeoprocessingOperation> _operations;
    private int _activeJobs = 0;

    // Configuration
    private const int MaxConcurrentJobs = 5;
    private const int PollIntervalSeconds = 5;
    private const int JobTimeoutMinutes = 30;

    public GeoprocessingWorkerService(
        IServiceProvider serviceProvider,
        ILogger<GeoprocessingWorkerService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
            "GeoprocessingWorkerService initialized: MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s, Timeout={Timeout}min, Operations={OperationCount}",
            MaxConcurrentJobs, PollIntervalSeconds, JobTimeoutMinutes, _operations.Count);
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
                    _logger.LogInformation(
                        "Found pending job {JobId} for process {ProcessId} from tenant {TenantId}",
                        job.JobId, job.ProcessId, job.TenantId);

                    // Wait for available slot (respects concurrency limit)
                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                    Interlocked.Increment(ref _activeJobs);

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
                            Interlocked.Decrement(ref _activeJobs);
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

        try
        {
            _logger.LogInformation("Starting job {JobId} for process {ProcessId}", job.JobId, job.ProcessId);

            // Get the operation handler
            if (!_operations.TryGetValue(job.ProcessId, out var operation))
            {
                throw new InvalidOperationException($"Operation '{job.ProcessId}' not found or not supported");
            }

            // Progress callback
            var progress = new Progress<GeoprocessingProgress>(p =>
            {
                try
                {
                    _logger.LogDebug(
                        "Job {JobId} progress: {Percent}% - {Message}",
                        job.JobId, p.ProgressPercent, p.Message);

                    // TODO: Update progress in database
                    // This would require adding an UpdateProgressAsync method to IControlPlane
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update progress for job {JobId}", job.JobId);
                }
            });

            // Convert ProcessRun inputs to GeoprocessingInputs
            var inputs = ConvertInputs(job.Inputs);

            // Execute the operation
            var result = await operation.ExecuteAsync(
                job.Inputs ?? new Dictionary<string, object>(),
                inputs,
                progress,
                jobCts.Token);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Job {JobId} completed successfully in {Duration:0.0}s, processed {Features} features",
                    job.JobId, result.DurationMs / 1000.0, result.FeaturesProcessed);

                // Record completion
                var processResult = new ProcessResult
                {
                    JobId = job.JobId,
                    ProcessId = job.ProcessId,
                    Status = ProcessRunStatus.Completed,
                    Success = true,
                    Output = result.Data,
                    Metadata = new Dictionary<string, object>
                    {
                        { "features_processed", result.FeaturesProcessed },
                        { "duration_ms", result.DurationMs }
                    },
                    DurationMs = result.DurationMs,
                    FeaturesProcessed = result.FeaturesProcessed
                };

                await controlPlane.RecordCompletionAsync(
                    job.JobId,
                    processResult,
                    job.ActualTier ?? ProcessExecutionTier.NTS,
                    TimeSpan.FromMilliseconds(result.DurationMs),
                    cancellationToken);
            }
            else
            {
                await HandleJobFailureAsync(
                    job,
                    new Exception(result.ErrorMessage ?? "Unknown error"),
                    controlPlane,
                    stopwatch.Elapsed,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (jobCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Job {JobId} timed out after {Timeout} minutes", job.JobId, JobTimeoutMinutes);

            var error = new TimeoutException($"Job timed out after {JobTimeoutMinutes} minutes");
            await controlPlane.RecordFailureAsync(
                job.JobId,
                error,
                job.ActualTier ?? ProcessExecutionTier.NTS,
                stopwatch.Elapsed,
                cancellationToken);
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
    /// Handles job failures.
    /// </summary>
    private async Task HandleJobFailureAsync(
        ProcessRun job,
        Exception error,
        IControlPlane controlPlane,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Job {JobId} failed: {Error}", job.JobId, error.Message);

        await controlPlane.RecordFailureAsync(
            job.JobId,
            error,
            job.ActualTier ?? ProcessExecutionTier.NTS,
            duration,
            cancellationToken);
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
