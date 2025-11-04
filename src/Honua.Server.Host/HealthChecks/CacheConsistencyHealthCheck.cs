// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Health;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check that verifies cache-database consistency for metadata.
/// Samples random entries and compares cached vs fresh data to detect drift.
/// </summary>
public sealed class CacheConsistencyHealthCheck : HealthCheckBase
{
    private readonly IMetadataRegistry _registry;
    private readonly IDistributedCache? _cache;
    private readonly IOptionsMonitor<CacheInvalidationOptions> _options;
    private readonly IOptionsMonitor<MetadataCacheOptions> _cacheOptions;

    public CacheConsistencyHealthCheck(
        IMetadataRegistry registry,
        IDistributedCache? cache,
        IOptionsMonitor<CacheInvalidationOptions> options,
        IOptionsMonitor<MetadataCacheOptions> cacheOptions,
        ILogger<CacheConsistencyHealthCheck> logger)
        : base(logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _cache = cache;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        // If cache is not configured, skip check
        if (_cache == null)
        {
            data["cacheEnabled"] = false;
            data["message"] = "Distributed cache is not configured";
            return HealthCheckResult.Healthy("Cache consistency check skipped (cache disabled)", data);
        }

        var stopwatch = Stopwatch.StartNew();
        var config = _options.CurrentValue;

        try
        {
            // Check if metadata cache key exists
            var cacheKey = _cacheOptions.CurrentValue.GetSnapshotCacheKey();
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);

            data["cacheEnabled"] = true;
            data["cacheKey"] = cacheKey;
            data["strategy"] = config.Strategy.ToString();
            data["checkDurationMs"] = stopwatch.ElapsedMilliseconds;

            if (cachedBytes == null || cachedBytes.Length == 0)
            {
                // Cache is empty - not necessarily unhealthy, might be warming up
                data["cachePopulated"] = false;
                data["message"] = "Cache is empty (may be warming up)";
                return HealthCheckResult.Degraded("Cache is empty", null, data);
            }

            data["cachePopulated"] = true;
            data["cacheSizeBytes"] = cachedBytes.Length;

            // Get fresh snapshot from source
            var freshSnapshot = await _registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

            // Compare key properties to detect drift
            var inconsistencies = new List<string>();

            // Basic sanity checks - if these differ, cache is definitely stale
            if (freshSnapshot.Catalog.Id != _registry.Snapshot.Catalog.Id)
            {
                inconsistencies.Add("Catalog ID mismatch");
            }

            if (freshSnapshot.Services.Count != _registry.Snapshot.Services.Count)
            {
                inconsistencies.Add($"Service count mismatch (fresh: {freshSnapshot.Services.Count}, cached: {_registry.Snapshot.Services.Count})");
            }

            if (freshSnapshot.Layers.Count != _registry.Snapshot.Layers.Count)
            {
                inconsistencies.Add($"Layer count mismatch (fresh: {freshSnapshot.Layers.Count}, cached: {_registry.Snapshot.Layers.Count})");
            }

            // Sample random layers to check for detailed inconsistencies
            var sampleSize = Math.Min(config.HealthCheckSampleSize, freshSnapshot.Layers.Count);
            if (sampleSize > 0)
            {
                var random = new Random();
                var sampledIndices = Enumerable.Range(0, freshSnapshot.Layers.Count)
                    .OrderBy(_ => random.Next())
                    .Take(sampleSize)
                    .ToList();

                var inconsistentSamples = 0;
                foreach (var index in sampledIndices)
                {
                    var freshLayer = freshSnapshot.Layers[index];
                    var cachedLayer = _registry.Snapshot.Layers.FirstOrDefault(l => l.Id == freshLayer.Id);

                    if (cachedLayer == null)
                    {
                        inconsistentSamples++;
                        continue;
                    }

                    // Check key properties
                    if (freshLayer.Title != cachedLayer.Title ||
                        freshLayer.Description != cachedLayer.Description ||
                        freshLayer.GeometryType != cachedLayer.GeometryType)
                    {
                        inconsistentSamples++;
                    }
                }

                var driftPercentage = sampleSize > 0 ? (inconsistentSamples * 100.0 / sampleSize) : 0;
                data["sampledEntries"] = sampleSize;
                data["inconsistentSamples"] = inconsistentSamples;
                data["driftPercentage"] = driftPercentage;

                if (driftPercentage > config.MaxDriftPercentage)
                {
                    inconsistencies.Add($"Cache drift detected: {driftPercentage:F2}% of sampled entries are inconsistent (threshold: {config.MaxDriftPercentage}%)");
                }
            }

            data["inconsistencyCount"] = inconsistencies.Count;
            data["checkDurationMs"] = stopwatch.ElapsedMilliseconds;

            if (inconsistencies.Any())
            {
                data["inconsistencies"] = inconsistencies;
                return HealthCheckResult.Degraded(
                    $"Cache-database inconsistency detected: {string.Join(", ", inconsistencies)}",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy("Cache is consistent with database", data);
        }
        catch (Exception ex)
        {
            data["checkDurationMs"] = stopwatch.ElapsedMilliseconds;
            Logger.LogWarning(ex, "Cache consistency check failed");
            return CreateUnhealthyResult(ex, data);
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        return HealthCheckResult.Unhealthy(
            "Cache consistency check failed: " + ex.Message,
            ex,
            data);
    }
}
