// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing.Idempotency;

/// <summary>
/// Extension methods for integrating idempotency into geoprocessing job execution
/// </summary>
public static class GeoprocessingWorkerServiceExtensions
{
    /// <summary>
    /// Executes a job with idempotency guarantees.
    /// Checks cache before execution and stores results after successful completion.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="job">The job to execute</param>
    /// <param name="executeFunc">Function to execute the job (only called if not cached)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Job result (from cache or fresh execution)</returns>
    public static async Task<ProcessResult> ExecuteWithIdempotencyAsync(
        this IServiceProvider serviceProvider,
        ProcessRun job,
        Func<ProcessRun, CancellationToken, Task<ProcessResult>> executeFunc,
        CancellationToken ct = default)
    {
        var idempotencyService = serviceProvider.GetService<IIdempotencyService>();
        var logger = serviceProvider.GetRequiredService<ILogger<IdempotencyAwareExecutor>>();

        // If idempotency service is not configured, execute without caching
        if (idempotencyService == null)
        {
            logger.LogWarning(
                "IIdempotencyService not registered, executing job {JobId} without idempotency guarantees",
                job.JobId);
            return await executeFunc(job, ct);
        }

        // Execute with idempotency
        var executor = new IdempotencyAwareExecutor(idempotencyService, logger);
        var result = await executor.ExecuteWithIdempotencyAsync(job, executeFunc, ct);

        // Log cache hit/miss metrics
        if (result.WasCached)
        {
            logger.LogInformation(
                "Job {JobId} returned cached result from original job {OriginalJobId}, " +
                "cached at {CachedAt}, lookup time: {LookupMs}ms",
                job.JobId,
                result.OriginalJobId,
                result.CacheHitTimestamp,
                result.ExecutionTimeMs);
        }
        else
        {
            logger.LogInformation(
                "Job {JobId} executed fresh (cache miss), execution time: {ExecutionMs}ms",
                job.JobId,
                result.ExecutionTimeMs);
        }

        return result.Result;
    }
}
