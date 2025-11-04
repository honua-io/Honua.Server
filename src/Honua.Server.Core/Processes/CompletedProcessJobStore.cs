// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Store for completed process jobs with results.
/// Keeps jobs in memory for a limited time for retrieval.
/// </summary>
public sealed class CompletedProcessJobStore
{
    private readonly Dictionary<string, CompletedJobEntry> _jobs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);

    private sealed class CompletedJobEntry
    {
        public required ProcessJob Job { get; init; }
        public required DateTimeOffset CompletedAt { get; init; }
    }

    /// <summary>
    /// Adds a completed job to the store.
    /// </summary>
    public async Task AddAsync(ProcessJob job, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _jobs[job.JobId] = new CompletedJobEntry
            {
                Job = job,
                CompletedAt = DateTimeOffset.UtcNow
            };

            // Clean up old entries
            await CleanupExpiredEntriesAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a completed job by ID.
    /// </summary>
    public async Task<ProcessJob?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_jobs.TryGetValue(jobId, out var entry))
            {
                return entry.Job;
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes a completed job from the store.
    /// </summary>
    public async Task<bool> RemoveAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _jobs.Remove(jobId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all completed job IDs.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAllJobIdsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return new List<string>(_jobs.Keys);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task CleanupExpiredEntriesAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - _retentionPeriod;
        var toRemove = new List<string>();

        foreach (var (jobId, entry) in _jobs)
        {
            if (entry.CompletedAt < cutoff)
            {
                toRemove.Add(jobId);
            }
        }

        foreach (var jobId in toRemove)
        {
            _jobs.Remove(jobId);
        }

        return Task.CompletedTask;
    }
}
