// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Coordination;
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
/// Uses leader election in HA deployments to ensure only one instance processes builds.
/// </summary>
public sealed class BuildQueueProcessor : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly BuildQueueOptions options;
    private readonly ILogger<BuildQueueProcessor> logger;
    private readonly LeaderElectionService? leaderElectionService;
    private readonly SemaphoreSlim concurrencySemaphore;
    private readonly ResiliencePipeline retryPipeline;
    private readonly CancellationTokenSource _shutdownCts = new();
    private int _activeBuilds = 0;

    public BuildQueueProcessor(
        IServiceProvider serviceProvider,
        IOptions<BuildQueueOptions> options,
        ILogger<BuildQueueProcessor> logger,
        LeaderElectionService? leaderElectionService = null)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.leaderElectionService = leaderElectionService;

        this.options.Validate();

        // Create semaphore for concurrency control
        this.concurrencySemaphore = new SemaphoreSlim(this.options.MaxConcurrentBuilds, this.options.MaxConcurrentBuilds);

        // Configure retry policy for individual build operations
        this.retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = this.options.MaxRetryAttempts,
                Delay = TimeSpan.FromMinutes(this.options.RetryDelayMinutes),
                BackoffType = this.options.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<IOException>()
                    .Handle<TimeoutException>()
            })
            .Build();

        if (this.leaderElectionService != null)
        {
            this.logger.LogInformation(
                "BuildQueueProcessor initialized with leader election: InstanceId={InstanceId}, MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s, BuildTimeout={Timeout}min",
                this.leaderElectionService.InstanceId, this.options.MaxConcurrentBuilds, this.options.PollIntervalSeconds, this.options.BuildTimeoutMinutes);
        }
        else
        {
            this.logger.LogInformation(
                "BuildQueueProcessor initialized (single instance mode): MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s, BuildTimeout={Timeout}min",
                this.options.MaxConcurrentBuilds, this.options.PollIntervalSeconds, this.options.BuildTimeoutMinutes);
        }
    }

    /// <summary>
    /// Main execution loop for the background service.
    /// Continuously polls for pending builds and processes them concurrently.
    /// In HA deployments, only processes when this instance is the leader.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("BuildQueueProcessor starting");

        // Ensure workspace directories exist
        EnsureDirectoriesExist();

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if leader election is enabled
                if (this.leaderElectionService != null)
                {
                    // Only process if this instance is the leader
                    if (!this.leaderElectionService.IsLeader)
                    {
                        // Not leader - wait and check again
                        this.logger.LogDebug(
                            "This instance is not the leader (InstanceId={InstanceId}), waiting before checking again",
                            this.leaderElectionService.InstanceId);

                        await Task.Delay(TimeSpan.FromSeconds(this.options.PollIntervalSeconds), stoppingToken);
                        continue;
                    }

                    this.logger.LogDebug(
                        "Processing builds as leader (InstanceId={InstanceId})",
                        this.leaderElectionService.InstanceId);
                }

                using var scope = this.serviceProvider.CreateScope();
                var queueManager = scope.ServiceProvider.GetRequiredService<IBuildQueueManager>();

                // Get next build from queue
                var job = await queueManager.GetNextBuildAsync(stoppingToken);

                if (job != null)
                {
                    // Double-check leadership before processing (in case we lost it during GetNextBuildAsync)
                    if (this.leaderElectionService != null && !this.leaderElectionService.IsLeader)
                    {
                        this.logger.LogWarning(
                            "Lost leadership while retrieving build job {JobId}, skipping processing",
                            job.Id);
                        await Task.Delay(TimeSpan.FromSeconds(this.options.PollIntervalSeconds), stoppingToken);
                        continue;
                    }

                    this.logger.LogInformation(
                        "Found pending build job {JobId} for customer {CustomerId} (priority: {Priority})",
                        job.Id, job.CustomerId, job.Priority);

                    // Wait for available slot (respects concurrency limit)
                    await this.concurrencySemaphore.WaitAsync(stoppingToken);

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
                            this.logger.LogError(ex, "Unhandled exception processing build job {JobId}", job.Id);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeBuilds);
                            this.concurrencySemaphore.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No pending builds, wait before polling again
                    this.logger.LogDebug("No pending builds, waiting {Seconds}s before next poll", this.options.PollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(this.options.PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                this.logger.LogInformation("BuildQueueProcessor stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error in BuildQueueProcessor main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        this.logger.LogInformation("BuildQueueProcessor stopped");
    }

    /// <summary>
    /// Processes a single build job from start to finish.
    /// </summary>
    private async Task ProcessBuildAsync(BuildJob job, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var queueManager = scope.ServiceProvider.GetRequiredService<IBuildQueueManager>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IBuildNotificationService>();

        var stopwatch = Stopwatch.StartNew();
        var buildCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        buildCts.CancelAfter(TimeSpan.FromMinutes(this.options.BuildTimeoutMinutes));

        try
        {
            this.logger.LogInformation("Starting build job {JobId}", job.Id);

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
                this.logger.LogInformation(
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
                if (this.options.CleanupWorkspaceAfterBuild)
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
            this.logger.LogWarning("Build job {JobId} timed out after {Timeout} minutes", job.Id, this.options.BuildTimeoutMinutes);

            await queueManager.UpdateBuildStatusAsync(
                job.Id,
                BuildJobStatus.TimedOut,
                errorMessage: $"Build timed out after {this.options.BuildTimeoutMinutes} minutes",
                cancellationToken: cancellationToken);

            await notificationService.SendBuildFailedAsync(
                job,
                $"Build timed out after {this.options.BuildTimeoutMinutes} minutes",
                cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Build job {JobId} failed with exception", job.Id);
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

        this.logger.LogDebug("Loading manifest from {Path}", manifestPath);

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

        using var scope = this.serviceProvider.CreateScope();
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

                this.logger.LogDebug("Build {JobId} progress: {Percent}% - {Step}", job.Id, percent, step);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to update progress for build {JobId}", job.Id);
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
            var outputPath = Path.Combine(this.options.OutputDirectory, job.Id.ToString());

            return new BuildResult
            {
                Success = true,
                OutputPath = outputPath,
                ImageUrl = $"honua.io/{job.CustomerId}/{job.ConfigurationName}:latest",
                DownloadUrl = $"{this.options.DownloadBaseUrl}/{job.Id}",
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
        this.logger.LogWarning("Build job {JobId} failed: {Error}", job.Id, error);

        // Check if we should retry
        if (job.RetryCount < this.options.MaxRetryAttempts)
        {
            this.logger.LogInformation(
                "Scheduling retry for build job {JobId} (attempt {Attempt}/{Max})",
                job.Id, job.RetryCount + 1, this.options.MaxRetryAttempts);

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
            this.logger.LogError(
                "Build job {JobId} failed after {Attempts} attempts",
                job.Id, this.options.MaxRetryAttempts);

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
            Directory.CreateDirectory(this.options.WorkspaceDirectory);
            Directory.CreateDirectory(this.options.OutputDirectory);

            this.logger.LogInformation(
                "Build directories initialized: workspace={Workspace}, output={Output}",
                this.options.WorkspaceDirectory, this.options.OutputDirectory);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to create build directories");
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
            var workspaceDir = Path.Combine(this.options.WorkspaceDirectory, job.Id.ToString());
            if (Directory.Exists(workspaceDir))
            {
                Directory.Delete(workspaceDir, recursive: true);
                this.logger.LogDebug("Cleaned up workspace for build {JobId}", job.Id);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to cleanup workspace for build {JobId}", job.Id);
        }
    }

    /// <summary>
    /// Gracefully stops the service, waiting for in-progress builds to complete.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("BuildQueueProcessor stopping gracefully");

        if (this.options.EnableGracefulShutdown && _activeBuilds > 0)
        {
            this.logger.LogInformation(
                "Waiting for {Count} active builds to complete (timeout: {Timeout}s)",
                _activeBuilds, this.options.GracefulShutdownTimeoutSeconds);

            var shutdownTimeout = TimeSpan.FromSeconds(this.options.GracefulShutdownTimeoutSeconds);
            var deadline = DateTimeOffset.UtcNow + shutdownTimeout;

            while (_activeBuilds > 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(1000, cancellationToken);
            }

            if (_activeBuilds > 0)
            {
                this.logger.LogWarning(
                    "{Count} builds still in progress after timeout, forcing shutdown",
                    _activeBuilds);
            }
            else
            {
                this.logger.LogInformation("All active builds completed, shutting down");
            }
        }

        _shutdownCts.Cancel();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.concurrencySemaphore?.Dispose();
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
