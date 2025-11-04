// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Data;

/// <summary>
/// Base class for in-memory stores tracking active background jobs.
/// Provides common patterns for job lifecycle management.
/// </summary>
/// <typeparam name="TJob">The job type.</typeparam>
public abstract class ActiveJobStore<TJob> : InMemoryStoreBase<TJob, Guid>
    where TJob : class
{
    protected ActiveJobStore() : base()
    {
    }

    /// <summary>
    /// Gets the job ID from a job entity.
    /// </summary>
    protected abstract Guid GetJobId(TJob job);

    /// <summary>
    /// Extracts the key (job ID) from the job entity.
    /// </summary>
    protected override Guid GetKey(TJob entity) => GetJobId(entity);

    /// <summary>
    /// Registers a new job in the store.
    /// </summary>
    /// <param name="job">The job to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was registered, false if it already exists.</returns>
    public virtual Task<bool> RegisterAsync(TJob job, CancellationToken cancellationToken = default)
    {
        return TryAddAsync(job, cancellationToken);
    }

    /// <summary>
    /// Unregisters a job from the store.
    /// </summary>
    /// <param name="jobId">The job ID to unregister.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the job was unregistered, false if it didn't exist.</returns>
    public virtual Task<bool> UnregisterAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(jobId, cancellationToken);
    }

    /// <summary>
    /// Gets all active jobs, ordered by most recent first.
    /// </summary>
    public virtual async Task<IReadOnlyList<TJob>> GetActiveJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return jobs;
    }
}
