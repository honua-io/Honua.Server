// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Data;

/// <summary>
/// Base class for in-memory stores tracking completed background jobs with LRU eviction.
/// Automatically maintains a fixed-size history of completed jobs.
/// </summary>
/// <typeparam name="TJobSnapshot">The job snapshot type.</typeparam>
public abstract class CompletedJobStore<TJobSnapshot> : InMemoryStoreBase<TJobSnapshot, Guid>
    where TJobSnapshot : class
{
    private readonly ConcurrentQueue<Guid> _completionOrder = new();
    private readonly int _maxCompletedJobs;

    /// <summary>
    /// Initializes a new instance with the specified maximum job history size.
    /// </summary>
    /// <param name="maxCompletedJobs">Maximum number of completed jobs to retain. Defaults to 100.</param>
    protected CompletedJobStore(int maxCompletedJobs = 100)
    {
        if (maxCompletedJobs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCompletedJobs), "Must be greater than zero.");
        }
        _maxCompletedJobs = maxCompletedJobs;
    }

    /// <summary>
    /// Gets the job ID from a job snapshot.
    /// </summary>
    protected abstract Guid GetJobId(TJobSnapshot snapshot);

    /// <summary>
    /// Extracts the key (job ID) from the job snapshot.
    /// </summary>
    protected override Guid GetKey(TJobSnapshot entity) => GetJobId(entity);

    /// <summary>
    /// Records a completed job and automatically trims the history if needed.
    /// </summary>
    /// <param name="snapshot">The completed job snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual async Task RecordCompletionAsync(TJobSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var jobId = GetJobId(snapshot);

        await PutAsync(snapshot, cancellationToken).ConfigureAwait(false);
        _completionOrder.Enqueue(jobId);

        // Trim oldest entries if we exceed the limit
        await TrimHistoryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Trims the job history to maintain the maximum size limit.
    /// </summary>
    protected virtual async Task TrimHistoryAsync(CancellationToken cancellationToken = default)
    {
        var count = await CountAsync(cancellationToken).ConfigureAwait(false);

        while (count > _maxCompletedJobs && _completionOrder.TryDequeue(out var oldestId))
        {
            await DeleteAsync(oldestId, cancellationToken).ConfigureAwait(false);
            count--;
        }
    }

    /// <summary>
    /// Gets all completed jobs, ordered by most recent first.
    /// </summary>
    public virtual async Task<IReadOnlyList<TJobSnapshot>> GetCompletedJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return jobs;
    }
}
