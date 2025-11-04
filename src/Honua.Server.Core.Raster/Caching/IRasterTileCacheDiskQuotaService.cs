// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Manages disk quotas for the tile cache similar to GeoWebCache
/// </summary>
public interface IRasterTileCacheDiskQuotaService
{
    /// <summary>
    /// Check if a dataset is within its quota
    /// </summary>
    Task<bool> IsWithinQuotaAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get quota status for a dataset
    /// </summary>
    Task<DatasetQuotaStatus> GetQuotaStatusAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enforce quota by removing oldest tiles if over quota
    /// </summary>
    Task<QuotaEnforcementResult> EnforceQuotaAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update quota configuration for a dataset
    /// </summary>
    Task UpdateQuotaAsync(string datasetId, DiskQuotaConfiguration quota, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all quota configurations
    /// </summary>
    Task<IReadOnlyDictionary<string, DiskQuotaConfiguration>> GetAllQuotasAsync(CancellationToken cancellationToken = default);
}

public sealed record DiskQuotaConfiguration(
    long MaxSizeBytes,
    QuotaExpirationPolicy ExpirationPolicy = QuotaExpirationPolicy.LeastRecentlyUsed);

public enum QuotaExpirationPolicy
{
    /// <summary>
    /// Remove least recently used tiles first
    /// </summary>
    LeastRecentlyUsed,

    /// <summary>
    /// Remove least frequently used tiles first
    /// </summary>
    LeastFrequentlyUsed,

    /// <summary>
    /// Remove oldest tiles first
    /// </summary>
    OldestFirst
}

public sealed record DatasetQuotaStatus(
    string DatasetId,
    long CurrentSizeBytes,
    long MaxSizeBytes,
    double UsagePercent,
    bool IsOverQuota,
    long TileCount);

public sealed record QuotaEnforcementResult(
    string DatasetId,
    int TilesRemoved,
    long BytesFreed,
    TimeSpan Duration);
