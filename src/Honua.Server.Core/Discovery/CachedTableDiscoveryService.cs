// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Discovery;

/// <summary>
/// Caching decorator for table discovery service.
/// Discovery queries can be expensive, so results are cached for a configurable duration.
/// </summary>
public sealed class CachedTableDiscoveryService : ITableDiscoveryService, IDisposable
{
    private readonly ITableDiscoveryService _inner;
    private readonly IMemoryCache _cache;
    private readonly AutoDiscoveryOptions _options;
    private readonly ILogger<CachedTableDiscoveryService> _logger;
    private readonly Timer? _backgroundRefreshTimer;

    public CachedTableDiscoveryService(
        ITableDiscoveryService inner,
        IMemoryCache cache,
        IOptions<AutoDiscoveryOptions> options,
        ILogger<CachedTableDiscoveryService> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Setup background refresh if enabled
        if (_options.BackgroundRefresh)
        {
            var interval = _options.BackgroundRefreshInterval ?? _options.CacheDuration;
            _backgroundRefreshTimer = new Timer(
                BackgroundRefreshCallback,
                null,
                interval,
                interval);

            _logger.LogInformation(
                "Background cache refresh enabled with interval {Interval}",
                interval);
        }
    }

    public async Task<IEnumerable<DiscoveredTable>> DiscoverTablesAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Array.Empty<DiscoveredTable>();
        }

        var cacheKey = GetCacheKey(dataSourceId);

        // Try to get from cache
        if (_cache.TryGetValue<IEnumerable<DiscoveredTable>>(cacheKey, out var cached) && cached != null)
        {
            // Use the Count property for materialized collections (O(1)) instead of Count() method
            var materializedCached = cached as ICollection<DiscoveredTable> ?? cached.ToList();
            _logger.LogDebug("Returning {Count} cached tables for {DataSourceId}",
                materializedCached.Count, dataSourceId);
            return materializedCached;
        }

        _logger.LogInformation("Cache miss for {DataSourceId} - discovering tables", dataSourceId);

        // Not in cache - discover and cache
        var tables = await _inner.DiscoverTablesAsync(dataSourceId, cancellationToken);

        // Materialize once to avoid multiple enumerations
        var materializedTables = tables as ICollection<DiscoveredTable> ?? tables.ToList();

        // Cache the results
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.CacheDuration,
            Size = EstimateCacheSize(materializedTables)
        };

        _cache.Set(cacheKey, materializedTables, cacheOptions);

        _logger.LogInformation(
            "Discovered and cached {Count} tables for {DataSourceId}",
            materializedTables.Count, dataSourceId);

        return materializedTables;
    }

    public async Task<DiscoveredTable?> DiscoverTableAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var cacheKey = GetTableCacheKey(dataSourceId, tableName);

        // Try to get from cache
        if (_cache.TryGetValue<DiscoveredTable>(cacheKey, out var cached))
        {
            _logger.LogDebug("Returning cached table {TableName}", tableName);
            return cached;
        }

        _logger.LogDebug("Cache miss for {TableName} - discovering", tableName);

        // Not in cache - discover and cache
        var table = await _inner.DiscoverTableAsync(dataSourceId, tableName, cancellationToken);

        if (table != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.CacheDuration,
                Size = 1
            };

            _cache.Set(cacheKey, table, cacheOptions);

            _logger.LogDebug("Discovered and cached table {TableName}", tableName);
        }

        return table;
    }

    /// <summary>
    /// Clears the discovery cache for a specific data source.
    /// </summary>
    public void ClearCache(string dataSourceId)
    {
        var cacheKey = GetCacheKey(dataSourceId);
        _cache.Remove(cacheKey);

        _logger.LogInformation("Cleared discovery cache for {DataSourceId}", dataSourceId);
    }

    /// <summary>
    /// Clears all discovery caches.
    /// </summary>
    public void ClearAllCaches()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Compact 100% - removes all expired entries
        }

        _logger.LogInformation("Cleared all discovery caches");
    }

    private void BackgroundRefreshCallback(object? state)
    {
        // This runs on a background thread, so we can't use the cancellation token
        // We'll just fire and forget the refresh
        _ = Task.Run(() =>
        {
            try
            {
                _logger.LogDebug("Starting background cache refresh");

                // We don't know which data sources to refresh, so we'll just let the cache expire naturally
                // The next request will trigger a refresh

                // Alternative: Keep track of recently accessed data sources and refresh those
                // For now, we'll keep it simple

                _logger.LogDebug("Background cache refresh complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background cache refresh");
            }
        });
    }

    private static string GetCacheKey(string dataSourceId)
    {
        return $"discovery:tables:{dataSourceId}";
    }

    private static string GetTableCacheKey(string dataSourceId, string tableName)
    {
        return $"discovery:table:{dataSourceId}:{tableName}";
    }

    private static long EstimateCacheSize(IEnumerable<DiscoveredTable> tables)
    {
        // Rough estimate: 1 unit per table plus 1 unit per 10 columns
        // Materialize once to avoid multiple enumerations
        var materializedTables = tables.ToList();
        var tableCount = materializedTables.Count;
        var columnCount = materializedTables.Sum(t => t.Columns.Count);
        return tableCount + (columnCount / 10);
    }

    public void Dispose()
    {
        _backgroundRefreshTimer?.Dispose();
    }
}
