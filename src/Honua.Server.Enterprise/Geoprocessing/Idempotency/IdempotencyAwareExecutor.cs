// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing.Idempotency;

/// <summary>
/// Wraps job execution with idempotency checking.
/// Checks cache before execution and stores results after successful completion.
/// </summary>
public sealed class IdempotencyAwareExecutor
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyAwareExecutor> _logger;

    public IdempotencyAwareExecutor(
        IIdempotencyService idempotencyService,
        ILogger<IdempotencyAwareExecutor> logger)
    {
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a job with idempotency guarantees.
    /// Checks cache before execution, executes if not cached, stores result after completion.
    /// </summary>
    /// <param name="job">The job to execute</param>
    /// <param name="executeFunc">Function to execute the job (only called if not cached)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Job result (from cache or fresh execution)</returns>
    public async Task<IdempotencyExecutionResult> ExecuteWithIdempotencyAsync(
        ProcessRun job,
        Func<ProcessRun, CancellationToken, Task<ProcessResult>> executeFunc,
        CancellationToken ct = default)
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));
        if (executeFunc == null)
            throw new ArgumentNullException(nameof(executeFunc));

        var stopwatch = Stopwatch.StartNew();

        // Step 1: Compute idempotency key
        var idempotencyKey = _idempotencyService.ComputeIdempotencyKey(job);

        _logger.LogDebug(
            "Computed idempotency key for job {JobId}: {IdempotencyKey}",
            job.JobId,
            idempotencyKey);

        // Step 2: Check cache
        var cached = await _idempotencyService.GetCachedResultAsync(idempotencyKey, ct);

        if (cached != null)
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "Job {JobId} already processed (cached as {CachedJobId}), returning cached result. " +
                "Cached at: {CachedAt}, Expires: {ExpiresAt}, Lookup time: {LookupMs}ms",
                job.JobId,
                cached.JobId,
                cached.CompletedAt,
                cached.ExpiresAt,
                stopwatch.ElapsedMilliseconds);

            return new IdempotencyExecutionResult
            {
                Result = cached.Result,
                WasCached = true,
                IdempotencyKey = idempotencyKey,
                OriginalJobId = cached.JobId,
                CacheHitTimestamp = cached.CompletedAt,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        _logger.LogDebug(
            "Job {JobId} not in cache, executing fresh. Cache lookup time: {LookupMs}ms",
            job.JobId,
            stopwatch.ElapsedMilliseconds);

        // Step 3: Execute job (cache miss)
        stopwatch.Restart();
        ProcessResult result;

        try
        {
            result = await executeFunc(job, ct);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Job {JobId} execution failed, will NOT cache error result. Execution time: {ExecutionMs}ms",
                job.JobId,
                stopwatch.ElapsedMilliseconds);

            // Re-throw - do not cache failed executions
            throw;
        }

        stopwatch.Stop();
        var executionTimeMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Job {JobId} executed successfully in {ExecutionMs}ms, storing in idempotency cache",
            job.JobId,
            executionTimeMs);

        // Step 4: Store result in cache (only for successful executions)
        if (result.Success)
        {
            try
            {
                await _idempotencyService.StoreCachedResultAsync(
                    idempotencyKey,
                    job,
                    result,
                    ttl: TimeSpan.FromDays(7),
                    ct: ct);

                _logger.LogDebug(
                    "Successfully cached result for job {JobId}, key: {IdempotencyKey}",
                    job.JobId,
                    idempotencyKey);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the job - caching is best-effort
                _logger.LogWarning(
                    ex,
                    "Failed to cache result for job {JobId}, key: {IdempotencyKey}. Job completed successfully but result not cached.",
                    job.JobId,
                    idempotencyKey);
            }
        }
        else
        {
            _logger.LogWarning(
                "Job {JobId} completed with Success=false, NOT caching result. Error: {ErrorMessage}",
                job.JobId,
                result.ErrorMessage);
        }

        return new IdempotencyExecutionResult
        {
            Result = result,
            WasCached = false,
            IdempotencyKey = idempotencyKey,
            OriginalJobId = job.JobId,
            CacheHitTimestamp = null,
            ExecutionTimeMs = executionTimeMs
        };
    }
}

/// <summary>
/// Result of idempotency-aware execution
/// </summary>
public class IdempotencyExecutionResult
{
    /// <summary>The job result (from cache or fresh execution)</summary>
    public required ProcessResult Result { get; init; }

    /// <summary>Whether result was returned from cache</summary>
    public required bool WasCached { get; init; }

    /// <summary>The idempotency key used</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Original job ID (may differ from requested job if cache hit)</summary>
    public required string OriginalJobId { get; init; }

    /// <summary>When the cached result was originally completed (null if fresh execution)</summary>
    public DateTimeOffset? CacheHitTimestamp { get; init; }

    /// <summary>Execution or cache lookup time in milliseconds</summary>
    public required long ExecutionTimeMs { get; init; }
}
