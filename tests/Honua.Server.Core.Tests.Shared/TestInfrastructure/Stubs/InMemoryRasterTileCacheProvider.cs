using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Caching;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// In-memory implementation of IRasterTileCacheProvider for testing.
/// Stores cached tiles in a dictionary without requiring external storage.
/// </summary>
/// <remarks>
/// This implementation:
/// - Stores all tiles in memory (not persistent across process restarts)
/// - Supports full cache operations (get, store, remove, purge)
/// - Uses default equality comparison for cache keys
/// - Thread-safe for concurrent operations
/// - Suitable for unit and integration tests
///
/// Note: This cache has no size limits or eviction policy. For production
/// use cases, use FileSystemRasterTileCacheProvider or another persistent implementation.
/// </remarks>
public sealed class InMemoryRasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly Dictionary<RasterTileCacheKey, RasterTileCacheEntry> _entries = new();
    private readonly object _lock = new();

    /// <summary>
    /// Tries to retrieve a cached tile.
    /// </summary>
    /// <param name="key">The cache key identifying the tile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A cache hit if found, otherwise null.</returns>
    public ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                return ValueTask.FromResult<RasterTileCacheHit?>(
                    new RasterTileCacheHit(entry.Content, entry.ContentType, entry.CreatedUtc));
            }

            return ValueTask.FromResult<RasterTileCacheHit?>(null);
        }
    }

    /// <summary>
    /// Stores a tile in the cache.
    /// </summary>
    /// <param name="key">The cache key identifying the tile.</param>
    /// <param name="entry">The cache entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries[key] = entry;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a specific tile from the cache.
    /// </summary>
    /// <param name="key">The cache key identifying the tile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Purges all cached tiles for a specific dataset.
    /// </summary>
    /// <param name="datasetId">The dataset ID to purge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var keysToRemove = _entries.Keys
                .Where(k => string.Equals(k.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _entries.Remove(key);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current number of cached entries. Useful for test assertions.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Clears all cached entries. Useful for test cleanup.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
