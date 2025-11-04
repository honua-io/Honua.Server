// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Configuration;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Background service that processes builds from the queue asynchronously.
/// Handles concurrent build execution, progress tracking, failure retry, and notifications.
/// </summary>
public sealed class BuildQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BuildQueueOptions _options;
    private readonly ILogger<BuildQueueProcessor> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly CancellationTokenSource _shutdownCts = new();
    private int _activeBuilds = 0;

    public BuildQueueProcessor(
        IServiceProvider serviceProvider,
        IOptions<BuildQueueOptions> options,
        ILogger<BuildQueueProcessor> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options.Validate();

        // Create semaphore for concurrency control
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentBuilds, _options.MaxConcurrentBuilds);

        // Configure retry policy for individual build operations
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromMinutes(_options.RetryDelayMinutes),
                BackoffType = _options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>()
                    .Handle<TimeoutException>()
            })
            .Build();

        _logger.LogInformation(
            "BuildQueueProcessor initialized: MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s, BuildTimeout={Timeout}min",
            _options.MaxConcurrentBuilds, _options.PollIntervalSeconds, _options.BuildTimeoutMinutes);
    }

    /// <summary>
    /// Main execution loop for the background service.
    /// Continuously polls for pending builds and processes them concurrently.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BuildQueueProcessor starting");

        // Ensure workspace directories exist
        EnsureDirectoriesExist();

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueManager = scope.ServiceProvider.GetRequiredService<IBuildQueueManager>();

                // Get next build from queue
                var job = await queueManager.GetNextBuildAsync(stoppingToken);

                if (job != null)
                {
                    _logger.LogInformation(
                        "Found pending build job {JobId} for customer {CustomerId} (priority: {Priority})",
                        job.Id, job.CustomerId, job.Priority);

                    // Wait for available slot (respects concurrency limit)
                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                    Interlocked.Increment(ref _activeBuilds);

                    // Process build in background task
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessBuildAsync(job, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception processing build job {JobId}", job.Id);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeBuilds);
                            _concurrencySemaphore.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No pending builds, wait before polling again
                    _logger.LogDebug("No pending builds, waiting {Seconds}s before next poll", _options.PollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("BuildQueueProcessor stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BuildQueueProcessor main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("BuildQueueProcessor stopped");
    }

    /// <summary>
    /// Processes a single build job from start to finish.
    /// </summary>
    private async Task ProcessBuildAsync(BuildJob job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var queueManager = scope.ServiceProvider.GetRequiredService<IBuildQueueManager>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IBuildNotificationService>();

        var stopwatch = Stopwatch.StartNew();
        var buildCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        buildCts.CancelAfter(TimeSpan.FromMinutes(_options.BuildTimeoutMinutes));

        try
        {
            _logger.LogInformation("Starting build job {JobId}", job.Id);

            // Mark build as started
            await queueManager.MarkBuildStartedAsync(job.Id, cancellationToken);
            job.Status = BuildJobStatus.Building;
            job.StartedAt = DateTimeOffset.UtcNow;

            // Send started notification
            await notificationService.SendBuildStartedAsync(job, cancellationToken);

            // Load manifest
            var manifest = await LoadManifestAsync(job.ManifestPath, buildCts.Token);

            // Execute build with orchestrator
            var result = await ExecuteBuildAsync(job, manifest, buildCts.Token);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Build job {JobId} completed successfully in {Duration:0.0}s",
                    job.Id, result.Duration.TotalSeconds);

                // Update status to success
                await queueManager.UpdateBuildStatusAsync(
                    job.Id,
                    BuildJobStatus.Success,
                    outputPath: result.OutputPath,
                    imageUrl: result.ImageUrl,
                    downloadUrl: result.DownloadUrl,
                    cancellationToken: cancellationToken);

                // Send success notification
                await notificationService.SendBuildCompletedAsync(job, result, cancellationToken);

                // Cleanup workspace if configured
                if (_options.CleanupWorkspaceAfterBuild)
                {
                    CleanupWorkspace(job);
                }
            }
            else
            {
                await HandleBuildFailureAsync(job, result.Error ?? "Unknown error", queueManager, notificationService, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (buildCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Build job {JobId} timed out after {Timeout} minutes", job.Id, _options.BuildTimeoutMinutes);

            await queueManager.UpdateBuildStatusAsync(
                job.Id,
                BuildJobStatus.TimedOut,
                errorMessage: $"Build timed out after {_options.BuildTimeoutMinutes} minutes",
                cancellationToken: cancellationToken);

            await notificationService.SendBuildFailedAsync(
                job,
                $"Build timed out after {_options.BuildTimeoutMinutes} minutes",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build job {JobId} failed with exception", job.Id);
            await HandleBuildFailureAsync(job, ex.Message, queueManager, notificationService, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            buildCts.Dispose();
        }
    }

    /// <summary>
    /// Loads and parses a build manifest from disk.
    /// </summary>
    private async Task<BuildManifest> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {manifestPath}");
        }

        _logger.LogDebug("Loading manifest from {Path}", manifestPath);

        var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<BuildManifest>(json)
            ?? throw new InvalidOperationException("Failed to deserialize manifest");

        return manifest;
    }

    /// <summary>
    /// Executes the build using the build orchestrator.
    /// </summary>
    private async Task<BuildResult> ExecuteBuildAsync(
        BuildJob job,
        BuildManifest manifest,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        using var scope = _serviceProvider.CreateScope();
        var queueManager = scope.ServiceProvider.GetRequiredService<IBuildQueueManager>();

        // Progress callback
        var progressCallback = new Action<int, string>(async (percent, step) =>
        {
            try
            {
                await queueManager.UpdateProgressAsync(
                    job.Id,
                    new BuildProgress
                    {
                        ProgressPercent = percent,
                        CurrentStep = step
                    });

                _logger.LogDebug("Build {JobId} progress: {Percent}% - {Step}", job.Id, percent, step);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update progress for build {JobId}", job.Id);
            }
        });

        try
        {
            // Create orchestrator (assuming it's a separate tool/library)
            // For now, simulate the build process
            progressCallback(10, "Cloning repositories");
            await Task.Delay(2000, cancellationToken);

            progressCallback(30, "Generating solution");
            await Task.Delay(1000, cancellationToken);

            progressCallback(50, "Building for target platforms");
            await Task.Delay(5000, cancellationToken);

            progressCallback(80, "Packaging artifacts");
            await Task.Delay(2000, cancellationToken);

            progressCallback(95, "Publishing to registry");
            await Task.Delay(1000, cancellationToken);

            progressCallback(100, "Build complete");

            var duration = DateTimeOffset.UtcNow - startTime;
            var outputPath = Path.Combine(_options.OutputDirectory, job.Id.ToString());

            return new BuildResult
            {
                Success = true,
                OutputPath = outputPath,
                ImageUrl = $"honua.io/{job.CustomerId}/{job.ConfigurationName}:latest",
                DownloadUrl = $"{_options.DownloadBaseUrl}/{job.Id}",
                Duration = duration,
                DeploymentInstructions = GenerateDeploymentInstructions(job)
            };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            return new BuildResult
            {
                Success = false,
                Error = ex.Message,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Handles build failures with retry logic.
    /// </summary>
    private async Task HandleBuildFailureAsync(
        BuildJob job,
        string error,
        IBuildQueueManager queueManager,
        IBuildNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Build job {JobId} failed: {Error}", job.Id, error);

        // Check if we should retry
        if (job.RetryCount < _options.MaxRetryAttempts)
        {
            _logger.LogInformation(
                "Scheduling retry for build job {JobId} (attempt {Attempt}/{Max})",
                job.Id, job.RetryCount + 1, _options.MaxRetryAttempts);

            await queueManager.IncrementRetryCountAsync(job.Id, cancellationToken);

            // Update status with error but keep as pending for retry
            await queueManager.UpdateBuildStatusAsync(
                job.Id,
                BuildJobStatus.Pending,
                errorMessage: error,
                cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogError(
                "Build job {JobId} failed after {Attempts} attempts",
                job.Id, _options.MaxRetryAttempts);

            // Mark as permanently failed
            await queueManager.UpdateBuildStatusAsync(
                job.Id,
                BuildJobStatus.Failed,
                errorMessage: error,
                cancellationToken: cancellationToken);

            // Send failure notification
            await notificationService.SendBuildFailedAsync(job, error, cancellationToken);
        }
    }

    /// <summary>
    /// Generates deployment instructions based on the build configuration.
    /// </summary>
    private static string GenerateDeploymentInstructions(BuildJob job)
    {
        return job.CloudProvider.ToLowerInvariant() switch
        {
            "aws" => @"
                <p>Deploy to AWS:</p>
                <pre>
aws ecs create-service \
  --cluster honua-cluster \
  --service-name honua-server \
  --task-definition honua-server:1 \
  --desired-count 1
                </pre>",

            "azure" => @"
                <p>Deploy to Azure:</p>
                <pre>
az container create \
  --resource-group honua-rg \
  --name honua-server \
  --image honua.io/your-image:latest \
  --dns-name-label honua-server
                </pre>",

            "gcp" => @"
                <p>Deploy to GCP:</p>
                <pre>
gcloud run deploy honua-server \
  --image honua.io/your-image:latest \
  --platform managed \
  --region us-central1
                </pre>",

            _ => @"
                <p>Deploy using Docker:</p>
                <pre>
docker run -d \
  -p 8080:8080 \
  --name honua-server \
  honua.io/your-image:latest
                </pre>"
        };
    }

    /// <summary>
    /// Ensures required directories exist.
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(_options.WorkspaceDirectory);
            Directory.CreateDirectory(_options.OutputDirectory);

            _logger.LogInformation(
                "Build directories initialized: workspace={Workspace}, output={Output}",
                _options.WorkspaceDirectory, _options.OutputDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create build directories");
            throw;
        }
    }

    /// <summary>
    /// Cleans up workspace directory after build completion.
    /// </summary>
    private void CleanupWorkspace(BuildJob job)
    {
        try
        {
            var workspaceDir = Path.Combine(_options.WorkspaceDirectory, job.Id.ToString());
            if (Directory.Exists(workspaceDir))
            {
                Directory.Delete(workspaceDir, recursive: true);
                _logger.LogDebug("Cleaned up workspace for build {JobId}", job.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup workspace for build {JobId}", job.Id);
        }
    }

    /// <summary>
    /// Gracefully stops the service, waiting for in-progress builds to complete.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BuildQueueProcessor stopping gracefully");

        if (_options.EnableGracefulShutdown && _activeBuilds > 0)
        {
            _logger.LogInformation(
                "Waiting for {Count} active builds to complete (timeout: {Timeout}s)",
                _activeBuilds, _options.GracefulShutdownTimeoutSeconds);

            var shutdownTimeout = TimeSpan.FromSeconds(_options.GracefulShutdownTimeoutSeconds);
            var deadline = DateTimeOffset.UtcNow + shutdownTimeout;

            while (_activeBuilds > 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(1000, cancellationToken);
            }

            if (_activeBuilds > 0)
            {
                _logger.LogWarning(
                    "{Count} builds still in progress after timeout, forcing shutdown",
                    _activeBuilds);
            }
            else
            {
                _logger.LogInformation("All active builds completed, shutting down");
            }
        }

        _shutdownCts.Cancel();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        _shutdownCts?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Simplified build manifest for deserialization.
/// In production, this would reference the actual BuildManifest from Honua.Build.Orchestrator.
/// </summary>
internal sealed class BuildManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
