// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Store for active process jobs with size limits to prevent memory exhaustion.
/// </summary>
public sealed class ProcessJobStore : ActiveJobStore<ProcessJob>
{
    private readonly ILogger<ProcessJobStore> _logger;
    private readonly InMemoryStoreOptions _options;

    public ProcessJobStore(
        ILogger<ProcessJobStore> logger,
        IOptions<InMemoryStoreOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Configure size limit to prevent unbounded growth
        MaxSize = _options.MaxActiveJobs;

        if (MaxSize > 0)
        {
            _logger.LogInformation(
                "ProcessJobStore initialized with size limit: {MaxJobs} active jobs",
                MaxSize);
        }
    }

    protected override Guid GetJobId(ProcessJob job)
    {
        if (Guid.TryParse(job.JobId, out var guid))
        {
            return guid;
        }

        // Fallback for non-GUID job IDs (shouldn't happen in practice)
        return Guid.NewGuid();
    }

    /// <summary>
    /// Gets a job by its string ID.
    /// </summary>
    public async Task<ProcessJob?> GetJobByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(jobId, out var guid))
        {
            return null;
        }

        return await GetAsync(guid, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unregisters a job by its string ID.
    /// </summary>
    public async Task<bool> UnregisterByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(jobId, out var guid))
        {
            return false;
        }

        return await UnregisterAsync(guid, cancellationToken).ConfigureAwait(false);
    }

    protected override void OnEntryEvicted(Guid key)
    {
        _logger.LogWarning(
            "Process job {JobId} was evicted from active job store due to capacity limit ({MaxJobs}). " +
            "Consider increasing MaxActiveJobs or investigating long-running jobs.",
            key,
            MaxSize);
    }
}
