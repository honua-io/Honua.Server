// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Intake.Configuration;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Interface for managing the build queue.
/// </summary>
public interface IBuildQueueManager
{
    /// <summary>
    /// Enqueues a new build job.
    /// </summary>
    Task<Guid> EnqueueBuildAsync(BuildJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next pending build to process (highest priority first).
    /// </summary>
    Task<BuildJob?> GetNextBuildAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a build job.
    /// </summary>
    Task UpdateBuildStatusAsync(
        Guid jobId,
        BuildJobStatus status,
        string? outputPath = null,
        string? imageUrl = null,
        string? downloadUrl = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates build progress.
    /// </summary>
    Task UpdateProgressAsync(Guid jobId, BuildProgress progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a build job by ID.
    /// </summary>
    Task<BuildJob?> GetBuildJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue statistics.
    /// </summary>
    Task<QueueStatistics> GetQueueStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a build as started.
    /// </summary>
    Task MarkBuildStartedAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the retry count for a failed build.
    /// </summary>
    Task IncrementRetryCountAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages build queue operations and database interactions.
/// </summary>
public sealed class BuildQueueManager : IBuildQueueManager
{
    private readonly string connectionString;
    private readonly ILogger<BuildQueueManager> logger;
    private readonly BuildQueueOptions options;

    public BuildQueueManager(
        IOptions<BuildQueueOptions> options,
        ILogger<BuildQueueManager> logger,
        string? connectionString = null)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use provided connection string or fall back to options
        this.connectionString = connectionString ?? this.options.ConnectionString
            ?? throw new InvalidOperationException("Connection string is required");

        this.logger.LogInformation("BuildQueueManager initialized with max concurrent builds: {MaxConcurrent}",
            this.options.MaxConcurrentBuilds);
    }

    /// <inheritdoc/>
    public async Task<Guid> EnqueueBuildAsync(BuildJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            commandText: @"
                INSERT INTO build_queue (
                    id, customer_id, customer_name, customer_email,
                    manifest_path, configuration_name, tier, architecture, cloud_provider,
                    status, priority, progress_percent, enqueued_at, updated_at
                )
                VALUES (
                    @Id, @CustomerId, @CustomerName, @CustomerEmail,
                    @ManifestPath, @ConfigurationName, @Tier, @Architecture, @CloudProvider,
                    @Status, @Priority, @ProgressPercent, @EnqueuedAt, @UpdatedAt
                )
                RETURNING id",
            parameters: new
            {
                job.Id,
                job.CustomerId,
                job.CustomerName,
                job.CustomerEmail,
                job.ManifestPath,
                job.ConfigurationName,
                job.Tier,
                job.Architecture,
                job.CloudProvider,
                Status = job.Status.ToString().ToLowerInvariant(),
                Priority = (int)job.Priority,
                job.ProgressPercent,
                job.EnqueuedAt,
                job.UpdatedAt
            },
            cancellationToken: cancellationToken));

        this.logger.LogInformation(
            "Enqueued build job {JobId} for customer {CustomerId} with priority {Priority}",
            id, job.CustomerId, job.Priority);

        return id;
    }

    /// <inheritdoc/>
    public async Task<BuildJob?> GetNextBuildAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get the next pending build ordered by priority (highest first), then by enqueued time
        var flatDto = await connection.QuerySingleOrDefaultAsync<BuildJobFlatDto?>(new CommandDefinition(
            commandText: @"
                SELECT
                    id, customer_id, customer_name, customer_email,
                    manifest_path, configuration_name, tier, architecture, cloud_provider,
                    status, priority, progress_percent, current_step,
                    output_path, image_url, download_url, error_message,
                    retry_count, enqueued_at, started_at, completed_at, updated_at,
                    build_duration_seconds
                FROM build_queue
                WHERE status = 'pending'
                    AND (retry_count < @MaxRetries OR retry_count = 0)
                ORDER BY priority DESC, enqueued_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED",
            parameters: new { MaxRetries = this.options.MaxRetryAttempts },
            cancellationToken: cancellationToken));

        if (flatDto == null)
        {
            return null;
        }

        var dto = MapFlatDtoToDto(flatDto);
        return MapDtoToJob(dto);
    }

    /// <inheritdoc/>
    public async Task UpdateBuildStatusAsync(
        Guid jobId,
        BuildJobStatus status,
        string? outputPath = null,
        string? imageUrl = null,
        string? downloadUrl = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var isCompleted = status is BuildJobStatus.Success or BuildJobStatus.Failed
            or BuildJobStatus.Cancelled or BuildJobStatus.TimedOut;

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                UPDATE build_queue
                SET
                    status = @Status,
                    output_path = COALESCE(@OutputPath, output_path),
                    image_url = COALESCE(@ImageUrl, image_url),
                    download_url = COALESCE(@DownloadUrl, download_url),
                    error_message = COALESCE(@ErrorMessage, error_message),
                    completed_at = CASE WHEN @IsCompleted THEN @Now ELSE completed_at END,
                    build_duration_seconds = CASE
                        WHEN @IsCompleted AND started_at IS NOT NULL
                        THEN EXTRACT(EPOCH FROM (@Now - started_at))
                        ELSE build_duration_seconds
                    END,
                    updated_at = @Now
                WHERE id = @JobId",
            parameters: new
            {
                JobId = jobId,
                Status = status.ToString().ToLowerInvariant(),
                OutputPath = outputPath,
                ImageUrl = imageUrl,
                DownloadUrl = downloadUrl,
                ErrorMessage = errorMessage,
                IsCompleted = isCompleted,
                Now = now
            },
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Updated build job {JobId} status to {Status}", jobId, status);
    }

    /// <inheritdoc/>
    public async Task UpdateProgressAsync(
        Guid jobId,
        BuildProgress progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                UPDATE build_queue
                SET
                    progress_percent = @ProgressPercent,
                    current_step = @CurrentStep,
                    updated_at = @UpdatedAt
                WHERE id = @JobId",
            parameters: new
            {
                JobId = jobId,
                progress.ProgressPercent,
                progress.CurrentStep,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));

        this.logger.LogDebug(
            "Updated build job {JobId} progress: {Percent}% - {Step}",
            jobId, progress.ProgressPercent, progress.CurrentStep);
    }

    /// <inheritdoc/>
    public async Task<BuildJob?> GetBuildJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        var flatDto = await connection.QuerySingleOrDefaultAsync<BuildJobFlatDto?>(new CommandDefinition(
            commandText: @"
                SELECT
                    id, customer_id, customer_name, customer_email,
                    manifest_path, configuration_name, tier, architecture, cloud_provider,
                    status, priority, progress_percent, current_step,
                    output_path, image_url, download_url, error_message,
                    retry_count, enqueued_at, started_at, completed_at, updated_at,
                    build_duration_seconds
                FROM build_queue
                WHERE id = @JobId",
            parameters: new { JobId = jobId },
            cancellationToken: cancellationToken));

        if (flatDto == null)
        {
            return null;
        }

        var dto = MapFlatDtoToDto(flatDto);
        return MapDtoToJob(dto);
    }

    /// <inheritdoc/>
    public async Task<QueueStatistics> GetQueueStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        var stats = await connection.QuerySingleAsync<QueueStatisticsDto>(new CommandDefinition(
            commandText: @"
                WITH today_builds AS (
                    SELECT
                        status,
                        build_duration_seconds
                    FROM build_queue
                    WHERE enqueued_at >= CURRENT_DATE
                ),
                completed_builds AS (
                    SELECT build_duration_seconds
                    FROM build_queue
                    WHERE status IN ('success', 'failed')
                        AND completed_at >= NOW() - INTERVAL '24 hours'
                        AND build_duration_seconds IS NOT NULL
                )
                SELECT
                    (SELECT COUNT(*) FROM build_queue WHERE status = 'pending') AS pending_count,
                    (SELECT COUNT(*) FROM build_queue WHERE status = 'building') AS building_count,
                    (SELECT COUNT(*) FROM today_builds WHERE status = 'success') AS completed_today,
                    (SELECT COUNT(*) FROM today_builds WHERE status = 'failed') AS failed_today,
                    (SELECT AVG(build_duration_seconds) FROM completed_builds) AS average_build_time_seconds,
                    (SELECT MIN(enqueued_at) FROM build_queue WHERE status = 'pending') AS oldest_pending_build,
                    (
                        SELECT CAST(
                            COUNT(*) FILTER (WHERE status = 'success') * 100.0 /
                            NULLIF(COUNT(*), 0) AS DOUBLE PRECISION
                        )
                        FROM build_queue
                        WHERE completed_at >= NOW() - INTERVAL '24 hours'
                            AND status IN ('success', 'failed')
                    ) AS success_rate",
            cancellationToken: cancellationToken));

        return new QueueStatistics
        {
            PendingCount = stats.pending_count,
            BuildingCount = stats.building_count,
            CompletedToday = stats.completed_today,
            FailedToday = stats.failed_today,
            AverageBuildTimeSeconds = stats.average_build_time_seconds ?? 0,
            SuccessRate = stats.success_rate ?? 0,
            OldestPendingBuild = stats.oldest_pending_build
        };
    }

    /// <inheritdoc/>
    public async Task MarkBuildStartedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                UPDATE build_queue
                SET
                    status = 'building',
                    started_at = @StartedAt,
                    updated_at = @UpdatedAt
                WHERE id = @JobId",
            parameters: new
            {
                JobId = jobId,
                StartedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Marked build job {JobId} as started", jobId);
    }

    /// <inheritdoc/>
    public async Task IncrementRetryCountAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            commandText: @"
                UPDATE build_queue
                SET
                    retry_count = retry_count + 1,
                    status = 'pending',
                    updated_at = @UpdatedAt
                WHERE id = @JobId",
            parameters: new
            {
                JobId = jobId,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Incremented retry count for build job {JobId}", jobId);
    }

    // Private helper methods

    /// <summary>
    /// Maps flat database DTO to structured parameter object DTO.
    /// Converts 23 flat parameters into 9 organized groups (8 parameter objects + Id).
    /// </summary>
    private static BuildJobDto MapFlatDtoToDto(BuildJobFlatDto flat)
    {
        return new BuildJobDto(
            Id: flat.id,
            Customer: new CustomerInfo
            {
                CustomerId = flat.customer_id,
                CustomerName = flat.customer_name,
                CustomerEmail = flat.customer_email
            },
            Configuration: new BuildConfiguration
            {
                ManifestPath = flat.manifest_path,
                ConfigurationName = flat.configuration_name,
                Tier = flat.tier,
                Architecture = flat.architecture,
                CloudProvider = flat.cloud_provider
            },
            JobStatus: new JobStatusInfo
            {
                Status = flat.status,
                Priority = flat.priority,
                RetryCount = flat.retry_count
            },
            Progress: new BuildProgressInfo
            {
                ProgressPercent = flat.progress_percent,
                CurrentStep = flat.current_step
            },
            Artifacts: new BuildArtifacts
            {
                OutputPath = flat.output_path,
                ImageUrl = flat.image_url,
                DownloadUrl = flat.download_url
            },
            Diagnostics: new BuildDiagnostics
            {
                ErrorMessage = flat.error_message
            },
            Timeline: new BuildTimeline
            {
                EnqueuedAt = flat.enqueued_at,
                StartedAt = flat.started_at,
                CompletedAt = flat.completed_at,
                UpdatedAt = flat.updated_at
            },
            Metrics: new BuildMetrics
            {
                BuildDurationSeconds = flat.build_duration_seconds
            }
        );
    }

    /// <summary>
    /// Maps structured DTO to domain model.
    /// </summary>
    private static BuildJob MapDtoToJob(BuildJobDto dto)
    {
        return new BuildJob
        {
            Id = dto.Id,
            CustomerId = dto.Customer.CustomerId,
            CustomerName = dto.Customer.CustomerName,
            CustomerEmail = dto.Customer.CustomerEmail,
            ManifestPath = dto.Configuration.ManifestPath,
            ConfigurationName = dto.Configuration.ConfigurationName,
            Tier = dto.Configuration.Tier,
            Architecture = dto.Configuration.Architecture,
            CloudProvider = dto.Configuration.CloudProvider,
            Status = Enum.Parse<Models.BuildJobStatus>(dto.JobStatus.Status, ignoreCase: true),
            Priority = (BuildPriority)dto.JobStatus.Priority,
            ProgressPercent = dto.Progress.ProgressPercent,
            CurrentStep = dto.Progress.CurrentStep,
            OutputPath = dto.Artifacts.OutputPath,
            ImageUrl = dto.Artifacts.ImageUrl,
            DownloadUrl = dto.Artifacts.DownloadUrl,
            ErrorMessage = dto.Diagnostics.ErrorMessage,
            RetryCount = dto.JobStatus.RetryCount,
            EnqueuedAt = dto.Timeline.EnqueuedAt,
            StartedAt = dto.Timeline.StartedAt,
            CompletedAt = dto.Timeline.CompletedAt,
            UpdatedAt = dto.Timeline.UpdatedAt,
            BuildDurationSeconds = dto.Metrics.BuildDurationSeconds
        };
    }

    // DTOs for Dapper mapping

    /// <summary>
    /// Data transfer object for build job information.
    /// Organizes 23 parameters into 9 logical groups (8 parameter objects + Id).
    /// Suitable for API responses, database records, and queue management.
    /// Achieves 61% reduction in parameter count (23 → 9).
    /// </summary>
    private sealed record BuildJobDto(
        Guid Id,
        CustomerInfo Customer,
        BuildConfiguration Configuration,
        JobStatusInfo JobStatus,
        BuildProgressInfo Progress,
        BuildArtifacts Artifacts,
        BuildDiagnostics Diagnostics,
        BuildTimeline Timeline,
        BuildMetrics Metrics
    );

    // Flat DTO for Dapper database mapping (matches database column structure)
    private sealed record BuildJobFlatDto(
        Guid id,
        string customer_id,
        string customer_name,
        string customer_email,
        string manifest_path,
        string configuration_name,
        string tier,
        string architecture,
        string cloud_provider,
        string status,
        int priority,
        int progress_percent,
        string? current_step,
        string? output_path,
        string? image_url,
        string? download_url,
        string? error_message,
        int retry_count,
        DateTimeOffset enqueued_at,
        DateTimeOffset? started_at,
        DateTimeOffset? completed_at,
        DateTimeOffset updated_at,
        double? build_duration_seconds
    );

    private sealed record QueueStatisticsDto(
        int pending_count,
        int building_count,
        int completed_today,
        int failed_today,
        double? average_build_time_seconds,
        double? success_rate,
        DateTimeOffset? oldest_pending_build
    );
}
