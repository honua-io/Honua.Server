// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Service for managing vector tile preseed jobs
/// </summary>
public interface IVectorTilePreseedService
{
    /// <summary>
    /// Enqueue a new preseed job
    /// </summary>
    Task<VectorTilePreseedJobSnapshot> EnqueueAsync(VectorTilePreseedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific job by ID
    /// </summary>
    Task<VectorTilePreseedJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all jobs (active and completed)
    /// </summary>
    Task<IReadOnlyList<VectorTilePreseedJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a running job
    /// </summary>
    Task<VectorTilePreseedJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
}
