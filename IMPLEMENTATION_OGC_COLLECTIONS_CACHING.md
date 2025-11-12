# OGC API Collections List Response Caching Implementation

**Implementation Date:** 2025-11-12
**Performance Optimization:** Based on PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md Section 2.3

## Overview

This implementation adds comprehensive caching for OGC API Collections list responses (`GET /ogc/collections`) to improve performance by avoiding repeated metadata queries and serialization operations.

## Implementation Details

### Files Created

1. **`src/Honua.Server.Host/Ogc/OgcMetrics.cs`**
   - Metrics for OGC API operations
   - Counters for cache hits, misses, and invalidations
   - Uses System.Diagnostics.Metrics for observability

2. **`src/Honua.Server.Host/Ogc/IOgcCollectionsCache.cs`**
   - Interface defining collections cache operations
   - Includes cache entry record and statistics record
   - Comprehensive documentation on cache strategy

3. **`src/Honua.Server.Host/Ogc/OgcCollectionsCache.cs`**
   - Concrete implementation using IMemoryCache
   - Similar pattern to WfsSchemaCache for consistency
   - Features:
     - Cache key format: `ogc:collections:{service_id}:{format}:{accept_language}`
     - Default TTL: 10 minutes (600 seconds)
     - Default max entries: 500
     - Metrics tracking (hits, misses, evictions, invalidations)
     - Configurable via OgcApiOptions
     - Concurrent dictionary for cache key tracking
     - Eviction callbacks for cleanup

4. **`src/Honua.Server.Host/Ogc/OgcCollectionsCacheInvalidationService.cs`**
   - Background hosted service for cache invalidation
   - Subscribes to metadata change events
   - Intelligent invalidation strategy:
     - Service changes: Invalidate all entries for that service
     - Layer changes: Invalidate parent service entries
     - Catalog changes: Invalidate all entries
     - Folder changes: No invalidation (doesn't affect collections)
     - Layer group changes: Invalidate parent service entries

### Files Modified

1. **`src/Honua.Server.Host/Configuration/OgcApiOptions.cs`**
   - Added `CollectionsCacheDurationSeconds` property (default: 600)
   - Added `MaxCachedCollections` property (default: 500)
   - Both properties are nullable and configurable via appsettings.json

2. **`src/Honua.Server.Host/Ogc/OgcLandingHandlers.cs`**
   - Updated `GetCollections` method signature to include `IOgcCollectionsCache` parameter
   - Added Accept-Language header extraction for i18n support
   - Added format detection (json vs html)
   - Added cache lookup before generating response
   - Added cache storage after generating response
   - Caches both JSON and HTML representations separately
   - Added comprehensive documentation

3. **`src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs`**
   - Added `AddHonuaOgcCollectionsCache()` extension method
   - Registers IOgcCollectionsCache as singleton
   - Registers OgcCollectionsCacheInvalidationService as hosted service
   - Added OgcApiOptions registration with validation

## Cache Key Strategy

### Format
```
ogc:collections:{service_id}:{format}:{accept_language}
```

### Components
- **Prefix:** `ogc:collections:` - Namespace isolation
- **Service ID:** Service identifier or "all" for global collections
- **Format:** "json" or "html" - Separate caching per format
- **Accept-Language:** Language identifier or "default" - i18n support

### Examples
```
ogc:collections:all:json:en-us
ogc:collections:all:html:fr-fr
ogc:collections:my-service:json:default
ogc:collections:my-service:html:es-es
```

## Configuration

### appsettings.json
```json
{
  "OgcApi": {
    "CollectionsCacheDurationSeconds": 600,
    "MaxCachedCollections": 500
  }
}
```

### Disable Caching
Set `CollectionsCacheDurationSeconds` to 0 or null:
```json
{
  "OgcApi": {
    "CollectionsCacheDurationSeconds": 0
  }
}
```

## Metrics

### Available Metrics

1. **honua.ogc.collections_cache.hits**
   - Type: Counter
   - Tags: service_id, format, language
   - Description: Number of cache hits

2. **honua.ogc.collections_cache.misses**
   - Type: Counter
   - Tags: service_id, format, language
   - Description: Number of cache misses

3. **honua.ogc.collections_cache.invalidations**
   - Type: Counter
   - Tags: service_id (optional), scope
   - Description: Number of cache invalidations

4. **honua.ogc.collections_cache.evictions**
   - Type: Counter
   - Tags: service_id, format, reason
   - Description: Number of cache evictions

5. **honua.ogc.collections_cache.entries**
   - Type: Gauge (Observable)
   - Description: Current number of cached entries

### Monitoring Cache Performance

Query cache statistics programmatically:
```csharp
var stats = collectionsCache.GetStatistics();
Console.WriteLine($"Hit Rate: {stats.HitRate:P2}");
Console.WriteLine($"Total Hits: {stats.Hits}");
Console.WriteLine($"Total Misses: {stats.Misses}");
Console.WriteLine($"Current Entries: {stats.EntryCount}/{stats.MaxEntries}");
```

## Invalidation Strategy

### Automatic Invalidation

The cache is automatically invalidated when:
- Service metadata is created, updated, or deleted
- Layer metadata is created, updated, or deleted
- Layer group metadata is created, updated, or deleted
- Catalog-level changes occur

### Manual Invalidation

Programmatic invalidation:
```csharp
// Invalidate specific service
collectionsCache.InvalidateService("my-service-id");

// Invalidate all entries
collectionsCache.InvalidateAll();
```

### TTL Expiration

Entries automatically expire after the configured TTL (default: 10 minutes).

## Performance Impact

### Expected Improvements

- **Response Time Reduction:** 50-80% for cache hits
- **Database Load Reduction:** Eliminates repeated metadata queries
- **Serialization Overhead Reduction:** Avoids JSON/HTML generation on cache hits
- **Network Bandwidth Savings:** ETags enable 304 Not Modified responses

### Typical Cache Hit Rates

- **Cold Start:** 0% (all cache misses)
- **Warm Cache:** 70-90% hit rate for frequently accessed collections
- **Expected Traffic Pattern:** 10-20% of OGC API requests are to `/collections`

### Resource Usage

- **Memory per Entry:** ~2-10 KB (JSON) or ~5-20 KB (HTML)
- **Maximum Memory (default config):** ~2.5-10 MB for 500 entries
- **CPU Impact:** Negligible (cache operations are O(1))

## Testing Recommendations

### Manual Testing

1. **Test cache miss (cold cache):**
   ```bash
   curl -i http://localhost:5000/ogc/collections
   # Should see cache miss in logs
   ```

2. **Test cache hit (warm cache):**
   ```bash
   curl -i http://localhost:5000/ogc/collections
   # Should see cache hit in logs, faster response
   ```

3. **Test format-specific caching:**
   ```bash
   # JSON request
   curl -H "Accept: application/json" http://localhost:5000/ogc/collections

   # HTML request (different cache entry)
   curl -H "Accept: text/html" http://localhost:5000/ogc/collections
   ```

4. **Test language-aware caching:**
   ```bash
   # English
   curl -H "Accept-Language: en-US" http://localhost:5000/ogc/collections

   # French (different cache entry)
   curl -H "Accept-Language: fr-FR" http://localhost:5000/ogc/collections
   ```

5. **Test cache invalidation:**
   ```bash
   # Modify service metadata via Admin API
   # Then request collections again - should be cache miss
   ```

### Load Testing

Use tools like Apache Bench or k6 to measure cache effectiveness:

```bash
# Warm up cache
curl http://localhost:5000/ogc/collections

# Run load test
ab -n 1000 -c 10 http://localhost:5000/ogc/collections
```

Expected results:
- First request: ~50-200ms (cache miss)
- Subsequent requests: ~5-20ms (cache hits)

## Compatibility

- **Minimum .NET Version:** .NET 6.0+
- **Dependencies:**
  - Microsoft.Extensions.Caching.Memory (already in use)
  - System.Diagnostics.Metrics (.NET 6.0+)
- **Breaking Changes:** None (purely additive)

## Future Enhancements

Potential improvements identified but not implemented:

1. **Distributed Caching:**
   - Use Redis for multi-instance deployments
   - Share cache across server instances
   - Requires implementing IDistributedCache adapter

2. **Compression:**
   - Store compressed responses in cache
   - Trade CPU for memory savings
   - Particularly beneficial for HTML responses

3. **Per-Service Cache Control:**
   - Different TTLs for different services
   - Service-specific cache policies
   - Requires extending configuration model

4. **Cache Warming:**
   - Pre-populate cache on startup
   - Reduce initial cache misses
   - Requires background task on application start

5. **Advanced Metrics:**
   - Response size distribution
   - Time-to-generate distribution
   - Memory usage per service

## Troubleshooting

### Cache Not Working

Check configuration:
```csharp
// In appsettings.json
"OgcApi": {
  "CollectionsCacheDurationSeconds": 600  // Ensure > 0
}
```

Check logs for cache operations:
```
Debug: OGC collections cache miss for service: all, format: json, language: default
Debug: OGC collections cached for service all, format json, language default with TTL of 600 seconds
```

### High Memory Usage

If cache is consuming too much memory:
1. Reduce `MaxCachedCollections` in configuration
2. Reduce `CollectionsCacheDurationSeconds` for faster eviction
3. Monitor eviction metrics for capacity-based evictions

### Cache Invalidation Issues

If cache isn't being invalidated on metadata changes:
1. Verify metadata provider supports change notifications
2. Check logs for invalidation service status:
   ```
   Info: Starting OGC Collections Cache Invalidation Service
   Info: Subscribed to metadata change notifications for OGC collections cache invalidation
   ```
3. Verify metadata changes are triggering events

## References

- Original recommendation: `PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md` Section 2.3
- Similar implementation: `src/Honua.Server.Host/Wfs/WfsSchemaCache.cs`
- Configuration model: `src/Honua.Server.Host/Configuration/OgcApiOptions.cs`
- OGC API Features Standard: https://docs.ogc.org/is/17-069r4/17-069r4.html
