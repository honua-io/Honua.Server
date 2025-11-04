// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Queue service for managing geoprocessing jobs
/// </summary>
public interface IGeoprocessingJobQueue
{
    /// <summary>
    /// Submits a new geoprocessing job to the queue
    /// </summary>
    Task<GeoprocessingJob> SubmitJobAsync(GeoprocessingJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next job from the queue (FIFO with priority)
    /// </summary>
    Task<GeoprocessingJob?> DequeueJobAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates job status and progress
    /// </summary>
    Task UpdateJobStatusAsync(Guid jobId, GeoprocessingJobStatus status, int progress = 0, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks job as completed with result
    /// </summary>
    Task CompleteJobAsync(Guid jobId, Dictionary<string, object> result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks job as failed with error
    /// </summary>
    Task FailJobAsync(Guid jobId, string errorMessage, string? errorDetails = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a job
    /// </summary>
    Task CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific job by ID
    /// </summary>
    Task<GeoprocessingJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries jobs with filtering and pagination
    /// </summary>
    Task<GeoprocessingJobResult> QueryJobsAsync(GeoprocessingJobQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets job statistics
    /// </summary>
    Task<GeoprocessingStatistics> GetStatisticsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue depth (number of pending jobs)
    /// </summary>
    Task<int> GetQueueDepthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old completed jobs (cleanup)
    /// </summary>
    Task<long> CleanupOldJobsAsync(int olderThanDays = 30, CancellationToken cancellationToken = default);
}
