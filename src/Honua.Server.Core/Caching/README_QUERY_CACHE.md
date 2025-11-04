# Query Result Cache Service

Comprehensive Redis-based query result caching for 10-100x performance improvement.

## Overview

The `QueryResultCacheService` provides a high-performance, distributed caching layer with:
- **Redis + In-Memory L2 Cache**: Two-tier caching for optimal performance
- **Automatic Compression**: Compresses large results (>1KB by default)
- **Cache-Aside Pattern**: Automatic cache population on miss
- **Pattern-Based Invalidation**: Invalidate entire groups of cache entries
- **Comprehensive Metrics**: Hit/miss rates, latency, compression ratios
- **Graceful Degradation**: Falls back to in-memory cache if Redis is unavailable

## Configuration

Add to `appsettings.json`:

```json
{
  "QueryResultCache": {
    "DefaultExpiration": "00:05:00",
    "LayerMetadataExpiration": "00:30:00",
    "TileExpiration": "24:00:00",
    "QueryResultExpiration": "00:01:00",
    "CrsTransformExpiration": "7.00:00:00",
    "EnableCompression": true,
    "CompressionThreshold": 1024,
    "UseDistributedCache": true,
    "RedisInstanceName": "Honua:"
  },
  "Redis": {
    "ConnectionString": "localhost:6379,ssl=false,abortConnect=false",
    "InstanceName": "Honua:",
    "Enabled": true
  }
}
```

## Usage Examples

### 1. Basic Cache-Aside Pattern

```csharp
public class LayerMetadataService
{
    private readonly IQueryResultCacheService _cache;
    private readonly IMetadataRegistry _metadata;

    public LayerMetadataService(
        IQueryResultCacheService cache,
        IMetadataRegistry metadata)
    {
        _cache = cache;
        _metadata = metadata;
    }

    public async Task<LayerMetadata> GetLayerMetadataAsync(
        string serviceId,
        string layerId,
        CancellationToken ct = default)
    {
        // Build cache key
        var cacheKey = CacheKeyBuilder
            .ForLayer(serviceId, layerId)
            .WithSuffix("metadata")
            .Build();

        // Get from cache or execute factory
        var metadata = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _metadata.GetLayerAsync(serviceId, layerId, ct),
            expiration: TimeSpan.FromMinutes(30),
            cancellationToken: ct
        );

        return metadata;
    }
}
```

### 2. Caching with Query Parameters

```csharp
public class FeatureQueryService
{
    private readonly IQueryResultCacheService _cache;
    private readonly IDataStoreProvider _dataStore;

    public async Task<FeatureCollection> QueryFeaturesAsync(
        string layerId,
        BoundingBox bbox,
        FilterExpression filter,
        CancellationToken ct = default)
    {
        // Build cache key with query parameters
        var cacheKey = CacheKeyBuilder
            .ForQuery(layerId)
            .WithBoundingBox(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY)
            .WithFilter(filter.ToString())
            .Build();

        // Cache for 1 minute (frequently changing data)
        var features = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await ExecuteQueryAsync(layerId, bbox, filter, ct),
            expiration: TimeSpan.FromMinutes(1),
            cancellationToken: ct
        );

        return features;
    }
}
```

### 3. Caching STAC Collections

```csharp
public class StacCollectionService
{
    private readonly IQueryResultCacheService _cache;
    private readonly IStacCatalogStorage _storage;

    public async Task<StacCollection> GetCollectionAsync(
        string collectionId,
        CancellationToken ct = default)
    {
        var cacheKey = CacheKeyBuilder
            .ForStacCollection(collectionId)
            .WithSuffix("metadata")
            .Build();

        var collection = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _storage.GetCollectionAsync(collectionId, ct),
            expiration: TimeSpan.FromMinutes(10),
            cancellationToken: ct
        );

        return collection;
    }

    public async Task<StacItemCollection> SearchItemsAsync(
        StacSearchRequest request,
        CancellationToken ct = default)
    {
        var cacheKey = CacheKeyBuilder
            .ForStacSearch()
            .WithObjectHash(request) // Hash the entire request object
            .Build();

        var items = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _storage.SearchItemsAsync(request, ct),
            expiration: TimeSpan.FromSeconds(30),
            cancellationToken: ct
        );

        return items;
    }
}
```

### 4. Tile Caching

```csharp
public class TileService
{
    private readonly IQueryResultCacheService _cache;
    private readonly ITileGenerator _generator;

    public async Task<byte[]> GetTileAsync(
        string tileMatrixSet,
        int z,
        int x,
        int y,
        string format,
        CancellationToken ct = default)
    {
        var cacheKey = CacheKeyBuilder
            .ForTile(tileMatrixSet, z, x, y, format)
            .Build();

        var tileData = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _generator.GenerateTileAsync(tileMatrixSet, z, x, y, format, ct),
            expiration: TimeSpan.FromHours(24), // Long cache for static tiles
            cancellationToken: ct
        );

        return tileData;
    }
}
```

### 5. CRS Transformation Caching

```csharp
public class CrsTransformService
{
    private readonly IQueryResultCacheService _cache;
    private readonly ICrsTransformer _transformer;

    public async Task<TransformationMatrix> GetTransformAsync(
        string sourceCrs,
        string targetCrs,
        BoundingBox bounds,
        CancellationToken ct = default)
    {
        var cacheKey = CacheKeyBuilder
            .ForCrsTransform(sourceCrs, targetCrs)
            .WithBoundingBox(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY)
            .Build();

        var matrix = await _cache.GetOrSetAsync(
            cacheKey,
            async ct => await _transformer.ComputeTransformAsync(sourceCrs, targetCrs, bounds, ct),
            expiration: TimeSpan.FromDays(7), // CRS transforms rarely change
            cancellationToken: ct
        );

        return matrix;
    }
}
```

## Cache Invalidation

### Invalidate Single Key

```csharp
await _cache.RemoveAsync("layer:service1:layer1:metadata");
```

### Invalidate by Pattern (Requires Redis)

```csharp
private readonly IDistributedCacheInvalidationService _invalidation;

// Invalidate all cache entries for a layer
var pattern = CacheInvalidationPatterns.ForLayer("service1", "layer1");
await _invalidation.InvalidateByPatternAsync(pattern);

// Invalidate all tiles at a specific zoom level
var tilePattern = CacheInvalidationPatterns.ForTileZoomLevel("WebMercatorQuad", 5);
await _invalidation.InvalidateByPatternAsync(tilePattern);

// Invalidate all STAC collections
var stacPattern = CacheInvalidationPatterns.ForStacCollections();
await _invalidation.InvalidateByPatternAsync(stacPattern);
```

### Invalidate on Data Updates

```csharp
public class LayerUpdateService
{
    private readonly IDistributedCacheInvalidationService _cacheInvalidation;
    private readonly IDataStoreProvider _dataStore;

    public async Task UpdateLayerAsync(
        string serviceId,
        string layerId,
        LayerUpdate update,
        CancellationToken ct = default)
    {
        // Update the data
        await _dataStore.UpdateLayerAsync(serviceId, layerId, update, ct);

        // Invalidate all cached data for this layer
        var pattern = CacheInvalidationPatterns.ForLayer(serviceId, layerId);
        await _cacheInvalidation.InvalidateByPatternAsync(pattern, ct);
    }
}
```

## Performance Considerations

### Cache Hit Ratios
- **Layer Metadata**: 90-99% hit ratio (rarely changes)
- **Static Tiles**: 95-99% hit ratio (immutable once generated)
- **Query Results**: 60-80% hit ratio (depends on query diversity)
- **STAC Collections**: 80-95% hit ratio (moderate update frequency)

### Expected Performance Improvements
- **Layer Metadata**: 10-50x faster (30ms -> 1ms)
- **Static Tiles**: 50-100x faster (500ms -> 5ms)
- **Frequent Queries**: 5-20x faster (100ms -> 10ms)
- **CRS Transforms**: 100-1000x faster (expensive computation)

### Memory Usage
- **Compression**: 30-70% size reduction for JSON data
- **L2 Cache**: Max 300MB in-memory (5-minute TTL)
- **Redis**: Configured per deployment (recommendation: 2-10GB)

### Compression Effectiveness
```
Small objects (<1KB): No compression (overhead > savings)
Medium objects (1-10KB): 30-50% reduction
Large objects (10-100KB): 50-70% reduction
Very large objects (>100KB): 60-80% reduction
```

## Monitoring and Metrics

The cache service automatically records OpenTelemetry metrics:

### Metrics Available
- `honua.cache.hits` - Cache hit counter
- `honua.cache.misses` - Cache miss counter
- `honua.cache.errors` - Cache error counter
- `honua.cache.operation.duration` - Operation latency histogram
- `honua.cache.write_size` - Cache entry size distribution

### Example Prometheus Queries

```promql
# Cache hit rate
rate(honua_cache_hits_total[5m]) / (rate(honua_cache_hits_total[5m]) + rate(honua_cache_misses_total[5m]))

# Average cache latency
histogram_quantile(0.95, rate(honua_cache_operation_duration_bucket[5m]))

# Compression ratio
avg(honua_cache_write_size{compressed="true"}) / avg(honua_cache_write_size{compressed="false"})
```

## Health Checks

Check cache availability:

```csharp
var isAvailable = await _invalidation.IsCacheAvailableAsync();
if (!isAvailable)
{
    _logger.LogWarning("Redis cache is unavailable, falling back to in-memory cache");
}
```

## Best Practices

### 1. Choose Appropriate TTLs
- **Immutable Data**: Long TTL (24h - 7d)
- **Slowly Changing**: Medium TTL (5-60m)
- **Frequently Changing**: Short TTL (30s - 5m)

### 2. Use Hierarchical Keys
```
Good:  layer:service1:layer1:metadata
Bad:   service1_layer1_metadata
```

### 3. Hash Complex Parameters
```csharp
// Good: Hash large filter objects
.WithObjectHash(filterObject)

// Bad: Serialize entire object in key
.WithComponent(JsonSerializer.Serialize(filterObject))
```

### 4. Implement Cache Warming
```csharp
// Pre-populate frequently accessed data on startup
var frequentLayers = await GetMostAccessedLayers();
foreach (var layer in frequentLayers)
{
    await GetLayerMetadataAsync(layer.ServiceId, layer.LayerId);
}
```

### 5. Handle Cache Failures Gracefully
```csharp
try
{
    return await _cache.GetOrSetAsync(key, factory);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Cache operation failed, executing factory directly");
    return await factory(ct);
}
```

## Troubleshooting

### Cache Not Working
1. Verify Redis connection string in configuration
2. Check `UseDistributedCache` is `true`
3. Verify `IConnectionMultiplexer` is registered for pattern invalidation
4. Check logs for cache errors

### Poor Hit Ratios
1. TTL too short - increase expiration times
2. Keys not deterministic - ensure consistent key generation
3. Too much invalidation - reduce invalidation frequency
4. Query diversity too high - add query normalization

### High Memory Usage
1. Reduce L2 cache TTL (default: 5 minutes)
2. Lower compression threshold
3. Reduce `CompressionThreshold` to compress more aggressively
4. Review cache entry sizes in metrics

## Migration from Existing Cache

If you have existing caching code:

```csharp
// Before: Direct IDistributedCache usage
var bytes = await _distributedCache.GetAsync(key);
if (bytes == null)
{
    var data = await FetchData();
    bytes = JsonSerializer.SerializeToUtf8Bytes(data);
    await _distributedCache.SetAsync(key, bytes, options);
}
return JsonSerializer.Deserialize<T>(bytes);

// After: QueryResultCacheService
return await _cache.GetOrSetAsync(
    key,
    async ct => await FetchData(ct),
    expiration: TimeSpan.FromMinutes(5)
);
```

Benefits:
- Automatic JSON serialization/deserialization
- Automatic compression
- L2 in-memory cache
- Metrics and observability
- Graceful degradation
