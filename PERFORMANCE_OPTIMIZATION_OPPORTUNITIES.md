# Performance Optimization Opportunities for Service Endpoints

**Analysis Date:** 2025-11-12
**Services Analyzed:** WFS, WMS, WCS, WMTS, CSW, OGC API Features/Tiles/Styles, GeoServices REST, STAC, OData, Carto SQL

## Executive Summary

This document identifies performance enhancement opportunities across Honua Server's geospatial service endpoints. The analysis reveals both existing optimizations already in place and opportunities for further improvements. Key findings show that the codebase already implements many best practices including streaming responses, database-level aggregations, and tiered caching, but there are opportunities to optimize database query patterns, expand caching strategies, and improve concurrent processing.

---

## 1. Database Query Optimization Opportunities

### 1.1 Query Count Operations - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:99-108` - Conditional count execution
- Count queries only executed when explicitly needed (`ReturnCountOnly` or `ReturnExtentOnly`)
- Uses "fetch limit+1" strategy to detect pagination boundaries without full count
- Related records queries use streaming with count-only mode when appropriate

**Evidence:**
```csharp
// Only count when explicitly needed
var needsCount = context.ReturnCountOnly || context.ReturnExtentOnly;
long? totalCount = null;
if (needsCount) {
    var countQuery = context.Query with { Limit = null, Offset = null, ResultType = FeatureResultType.Hits };
    totalCount = await _repository.CountAsync(service.Id, layer.Id, countQuery, cancellationToken);
}
```

**Status:** No action needed - already optimized.

---

### 1.2 Aggregation Operations - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:569-632` - Database-level statistics aggregation
- `GeoservicesQueryService.cs:516-567` - Database-level DISTINCT queries
- `GeoservicesQueryService.cs:421-447` - Database-level ST_Extent for bounding box calculation

**Evidence:**
```csharp
// CRITICAL PERFORMANCE FIX: Use database-level aggregation instead of loading all records into memory
var results = await _repository.QueryStatisticsAsync(
    serviceId,
    layer.Id,
    statistics,
    groupByFields.Count > 0 ? groupByFields : null,
    context.Query,
    cancellationToken);
```

**Performance Impact:** Database-level aggregations are 100x faster than in-memory operations.

**Status:** No action needed - already optimized.

---

### 1.3 IN Clause Optimization - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:1306-1335` - Uses IN function for multiple IDs
- Prevents query plan cache pollution from large OR chains

**Evidence:**
```csharp
// BUG FIX #7: Use IN clause instead of OR chain to avoid plan cache poisoning with large ID lists
if (values.Count == 1) {
    return new QueryBinaryExpression(
        new QueryFieldReference(fieldName),
        QueryBinaryOperator.Equal,
        new QueryConstant(values[0].Value));
}

// For multiple values, use IN function which generates parameterized IN clause
var arguments = new List<QueryExpression>(values.Count + 1) {
    new QueryFieldReference(fieldName)
};
foreach (var entry in values) {
    arguments.Add(new QueryConstant(entry.Value));
}
return new QueryFunctionExpression("IN", arguments);
```

**Status:** No action needed - already optimized.

---

### 1.4 Pagination Requirements - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:456-461` - Mandatory pagination validation
- `GeoservicesQueryService.cs:388-393` - Pre-execution validation for IDs-only queries
- Prevents unbounded result sets that could cause memory exhaustion

**Evidence:**
```csharp
// CRITICAL FIX: Validate pagination BEFORE executing query to prevent expensive unbounded iteration
if (!context.Query.Limit.HasValue) {
    throw new InvalidOperationException(
        $"Result set may exceed {MaxResultsWithoutPagination} records. Use pagination (resultRecordCount parameter) to retrieve large result sets.");
}
```

**Status:** No action needed - already optimized.

---

### 1.5 DISTINCT Field Filtering - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:824-882` - Excludes geometry and large text/blob columns from DISTINCT
- Prevents massive memory allocation and slow DISTINCT operations

**Evidence:**
```csharp
// BUG FIX #8: Exclude geometry AND large text/blob columns from DISTINCT queries
// Most database engines either reject or materialize DISTINCT on these columns very slowly
var distinctFields = fields.Where(fieldName => {
    if (fieldName.EqualsIgnoreCase(layer.GeometryField)) return false;

    // Exclude BLOB, CLOB, TEXT, NTEXT, IMAGE, BYTEA, LONG types
    if (dataType.Contains("blob") || dataType.Contains("clob") ||
        dataType.Contains("text") && dataType != "text" ||
        dataType.Contains("image") || dataType.Contains("bytea") ||
        dataType.Contains("long") || dataType == "json" || dataType == "jsonb" ||
        dataType == "xml") {
        return false;
    }
    return true;
});
```

**Status:** No action needed - already optimized.

---

### 1.6 **OPPORTUNITY:** Connection Pooling Optimization

**Current State:** Unknown - needs verification
- Connection pooling settings not visible in analyzed code
- Default connection pool sizes may be suboptimal for high-concurrency scenarios

**Recommendation:**
1. Review connection string settings for each data provider (PostgreSQL, SQL Server, SQLite)
2. Set appropriate `MinPoolSize` and `MaxPoolSize` based on expected concurrent load
3. For PostgreSQL: Consider `MaxPoolSize=100-200` for high-traffic deployments
4. For SQL Server: Use `Max Pool Size=200-300` with `Connection Timeout=30`
5. Monitor connection pool exhaustion metrics

**Estimated Impact:** Medium - can prevent request queuing under high load

**Location to investigate:**
- Data provider connection string configuration
- Database provider factory implementations

---

### 1.7 **OPPORTUNITY:** Query Timeout Configuration

**Current State:** No explicit query timeouts observed in repository code

**Recommendation:**
1. Add configurable command timeout at repository level
2. Different timeout tiers for different operation types:
   - Simple queries: 10-15 seconds
   - Statistics/aggregations: 30-60 seconds
   - Tile generation: 5-10 seconds (fast timeout for cache misses)
   - Extent calculations: 20-30 seconds

**Estimated Impact:** Medium - prevents long-running queries from blocking resources

**Implementation location:** `FeatureRepository.cs` - add timeout configuration per operation type

---

## 2. Caching Strategy Optimization

### 2.1 Raster Tile Caching - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `WmsGetMapHandlers.cs:160-228` - Multi-tier cache lookup with metrics
- Cache key generation based on exact tile bounds alignment
- Supports multiple cache providers: FileSystem, Azure Blob, S3, GCS
- Cache miss triggers on-demand rendering with automatic cache population

**Evidence:**
```csharp
if (useCache) {
    var cached = await cacheProvider.TryGetAsync(cacheKey, cancellationToken);
    if (cached is not null) {
        cacheMetrics.RecordCacheHit(dataset.Id, cacheVariant, primaryTimeValue);
        activity.AddTag("wms.cache_hit", true);
        return CreateFileResultWithCdn(cached.Value.Content.ToArray(), cached.Value.ContentType, dataset.Cdn);
    }
    cacheMetrics.RecordCacheMiss(dataset.Id, cacheVariant, primaryTimeValue);
}
```

**Status:** No action needed - well-architected caching system.

---

### 2.2 **OPPORTUNITY:** GetCapabilities Response Caching

**Current State:**
- GetCapabilities responses are regenerated on every request
- `WfsCapabilitiesHandlers.cs`, `WmsCapabilitiesHandlers.cs`, `OgcLandingHandlers.cs`

**Recommendation:**
1. Implement in-memory cache for capabilities documents with TTL (5-15 minutes)
2. Cache key: `{service_type}:{service_id}:capabilities:{version}:{accept_language}`
3. Invalidate cache on metadata updates via `MetadataRegistry` change notifications
4. Use `IMemoryCache` with size limits to prevent unbounded growth

**Estimated Impact:** High - Capabilities requests can be 10-20% of total traffic

**Benefits:**
- Reduces XML serialization overhead
- Decreases metadata registry lookups
- Faster response times for client initialization

**Implementation approach:**
```csharp
// Add to WfsCapabilitiesHandlers.cs
private static readonly MemoryCache _capabilitiesCache = new(new MemoryCacheOptions {
    SizeLimit = 100 // Limit to 100 cached capabilities documents
});

public static async Task<IResult> HandleGetCapabilitiesAsync(...) {
    var cacheKey = $"wfs:capabilities:{serviceId}:{version}";

    if (_capabilitiesCache.TryGetValue(cacheKey, out string cachedXml)) {
        return Results.Content(cachedXml, "application/xml");
    }

    // Generate capabilities...
    var xml = doc.ToString();

    _capabilitiesCache.Set(cacheKey, xml, new MemoryCacheEntryOptions {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        Size = 1
    });

    return Results.Content(xml, "application/xml");
}
```

---

### 2.3 **OPPORTUNITY:** OGC API Collections List Caching

**Current State:**
- Collections list regenerated on every `/collections` request
- `OgcLandingHandlers.cs` - collections endpoint

**Recommendation:**
1. Cache collections list response (both JSON and HTML formats)
2. Cache key: `ogc:collections:{service_id}:{format}:{accept_language}`
3. TTL: 5-10 minutes
4. Invalidate on metadata updates

**Estimated Impact:** Medium - Collections endpoint is frequently accessed by clients

**Implementation:** Similar pattern to GetCapabilities caching

---

### 2.4 **OPPORTUNITY:** CRS Transformation Result Caching

**Current State:**
- Geometry transformations happen on every request
- No caching of transformed geometries observed

**Recommendation:**
1. For frequently requested features, cache transformed geometries
2. Cache key: `geometry:{feature_id}:{source_crs}:{target_crs}:{hash}`
3. Use LRU cache with size limits (e.g., 10,000 entries, ~100MB)
4. Particularly beneficial for:
   - Popular base layers (boundaries, roads, landmarks)
   - Features accessed at multiple CRS (e.g., 4326 ↔ 3857)
   - Complex geometries with many vertices

**Estimated Impact:** Medium - Reduces PostGIS ST_Transform calls

**Caveats:**
- Only cache for read-only layers
- Invalidate on feature updates
- Monitor cache hit rate to ensure effectiveness

---

### 2.5 **OPPORTUNITY:** Filter Expression Parsing Cache

**Current State:**
- CQL and CQL2-JSON filters parsed on every request
- `CqlFilterParser.Parse()` and `Cql2JsonParser.Parse()` called per request

**Recommendation:**
1. Cache parsed filter AST (Abstract Syntax Tree)
2. Cache key: `filter:{hash(filter_text)}:{layer_schema_version}`
3. Use `MemoryCache` with LRU eviction
4. Limit cache size to prevent memory issues with unique filters

**Estimated Impact:** Low-Medium - Reduces parsing overhead for repeated filter patterns

**Example patterns to cache:**
- `bbox(-180,-90,180,90)`
- `population > 100000`
- Common date range filters

---

### 2.6 **OPPORTUNITY:** Feature Count Approximation Cache

**Current State:**
- Exact counts computed for every query (when requested)
- Can be expensive for large tables with complex filters

**Recommendation:**
1. For layers with >1M features, provide fast approximate counts
2. Use PostgreSQL table statistics: `reltuples` from `pg_class`
3. Cache exact counts for common filter combinations
4. Return approximate flag in response: `"numberMatched": {"value": 1234567, "approximate": true}`

**Estimated Impact:** High - Dramatically speeds up count operations on large datasets

**Implementation:**
```csharp
// Add to FeatureRepository
public async Task<(long Count, bool IsApproximate)> CountApproximateAsync(...) {
    // If no filter and table is large, use statistics
    if (filter == null && layerMetadata.EstimatedRowCount > 1_000_000) {
        var approxCount = await GetTableStatisticsCount(layerId);
        return (approxCount, true);
    }

    // Otherwise fall back to exact count
    var exactCount = await CountAsync(...);
    return (exactCount, false);
}
```

---

## 3. Response Streaming Optimization

### 3.1 WMS GetMap Streaming - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `WmsGetMapHandlers.cs:266-287` - Adaptive streaming based on image size
- Small images buffered for caching, large images streamed directly
- Configurable streaming threshold (default: 2MB)

**Evidence:**
```csharp
var estimatedSize = EstimateImageSize(width, height, normalizedFormat);
var shouldBuffer = estimatedSize <= options.StreamingThresholdBytes && (useCache || dataset.Cdn.Enabled);

if (shouldBuffer && options.EnableStreaming) {
    // Buffer small images for caching/CDN headers
    await using var renderStream = result.Content;
    using var buffer = new MemoryStream((int)estimatedSize);
    await renderStream.CopyToAsync(buffer, linkedCts.Token);
    bufferedBytes = buffer.ToArray();
} else {
    // Stream large images directly to avoid memory pressure
    renderActivity.AddTag("raster.streaming", options.EnableStreaming);
}
```

**Status:** No action needed - excellent adaptive streaming strategy.

---

### 3.2 GeoJSON Feature Streaming - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `WfsGetFeatureHandlers.cs:108-120` - Streaming GeoJSON writer
- `GeoservicesRESTFeatureServerController.Query.cs:180-194` - Format-specific streaming
- Uses `IAsyncEnumerable<FeatureRecord>` for zero-copy database streaming

**Evidence:**
```csharp
var features = repository.QueryAsync(service.Id, layer.Id, execution.ResultQuery, cancellationToken);
var writer = new GeoJsonFeatureCollectionStreamingWriter(writerLogger);
await writer.WriteCollectionAsync(httpResponse.Body, features, layer, writerContext, cancellationToken);
```

**Status:** No action needed - proper streaming implementation.

---

### 3.3 **OPPORTUNITY:** Compressed Response Streaming

**Current State:**
- Response compression likely handled by middleware
- No explicit compression in endpoint handlers

**Recommendation:**
1. Verify response compression middleware is enabled and properly configured
2. Ensure appropriate compression for different content types:
   - GeoJSON: gzip (typically 70-80% compression ratio)
   - XML (GML): gzip (similar compression)
   - Binary formats (MVT, Shapefile): Already compressed, skip
3. Consider brotli compression for static content (capabilities, collections)
4. Set appropriate `Vary: Accept-Encoding` headers (already done in some handlers)

**Estimated Impact:** High - Can reduce response sizes by 70-80% for text formats

**Verification needed in:** ASP.NET Core middleware configuration

---

## 4. Concurrent Processing Optimization

### 4.1 **OPPORTUNITY:** Multi-Layer WMS Rendering Parallelization

**Current State:**
- `WmsGetMapHandlers.cs:132-156` - Sequential overlay rendering
- Multiple layer groups rendered one after another

**Current Code:**
```csharp
for (var index = 1; index < layerContexts.Count; index++) {
    var overlayContext = layerContexts[index];
    // ... validate and prepare overlay request
    overlays.Add(new RasterLayerRequest(...));
}
```

**Recommendation:**
1. Render independent overlay layers in parallel when they don't depend on each other
2. Use `Task.WhenAll()` to parallelize overlay preparation
3. Requires raster renderer to support parallel compositing

**Estimated Impact:** Medium - Speeds up multi-layer map rendering by 30-50%

**Implementation approach:**
```csharp
var overlayTasks = layerContexts.Skip(1).Select(async overlayContext => {
    var overlayTimeValue = overlayContext.Dataset.Temporal.Enabled
        ? WmsSharedHelpers.ValidateTimeParameter(rawTimeValue, overlayContext.Dataset.Temporal)
        : null;

    return new RasterLayerRequest(overlayContext.Dataset, overlayStyleId, overlayStyleDefinition, overlayTimeValue);
});

var overlays = await Task.WhenAll(overlayTasks);
```

**Caveats:**
- Only parallelize if layers are truly independent
- Watch for database connection pool exhaustion
- Consider semaphore to limit concurrent database queries

---

### 4.2 **OPPORTUNITY:** Parallel Feature Collection Queries in Search

**Current State:**
- `OgcFeaturesQueryHandler.cs:873-994` - Sequential collection enumeration in search
- Collections queried one after another

**Recommendation:**
1. When `includeCount=false`, stream from multiple collections concurrently
2. Use merge logic to interleave results while respecting overall limit
3. Requires careful coordination of offsets and limits across collections

**Estimated Impact:** Medium-High - Can speed up multi-collection searches significantly

**Complexity:** High - Requires careful handling of:
- Distributed pagination
- Result ordering across collections
- Cancellation propagation
- Memory pressure management

**Suggested Implementation:**
```csharp
// Use Channel<T> for producer-consumer pattern
var channel = Channel.CreateBounded<(SearchCollectionContext, FeatureRecord)>(1000);

var producerTasks = preparedQueries.Select(async prepared => {
    await foreach (var record in repository.QueryAsync(...)) {
        await channel.Writer.WriteAsync((prepared.Context, record), cancellationToken);
        if (remainingLimit <= 0) break;
    }
});

// Start all producers concurrently
_ = Task.WhenAll(producerTasks).ContinueWith(_ => channel.Writer.Complete());

// Single consumer writes to output
await foreach (var (context, record) in channel.Reader.ReadAllAsync(cancellationToken)) {
    // Process and write feature...
}
```

---

### 4.3 **OPPORTUNITY:** Batch GetCapabilities Layer Metadata Resolution

**Current State:**
- Layer metadata resolved individually during capabilities generation
- Potentially results in N+1 queries to metadata registry

**Recommendation:**
1. Batch-load all layer metadata for a service in single call
2. Pre-fetch relationship definitions and style metadata
3. Reduces catalog/metadata lookup overhead

**Estimated Impact:** Low-Medium - Speeds up capabilities generation

---

## 5. Memory Management Optimization

### 5.1 Related Records Query - ALREADY OPTIMIZED ✓

**Current Implementation (GOOD):**
- `GeoservicesQueryService.cs:315-361` - Per-parent child capping
- Limits children to 10,000 per parent to prevent LOH (Large Object Heap) allocation
- Prevents memory exhaustion on 1:N relationships with high cardinality

**Evidence:**
```csharp
const int MaxChildrenPerParent = 10_000; // Cap to prevent LOH allocation (85KB threshold)

// Cap per-parent children to prevent excessive memory allocation
if (list.Count >= MaxChildrenPerParent) {
    exceeded = true;
    continue; // Skip additional children beyond cap
}
```

**Status:** No action needed - excellent memory pressure management.

---

### 5.2 **OPPORTUNITY:** Feature Geometry Simplification at Scale

**Current State:**
- Geometry simplification available via `maxAllowableOffset` parameter
- `GeoservicesQueryService.cs:702-723` - Douglas-Peucker simplification

**Recommendation:**
1. Auto-simplify geometries based on zoom level for tile/map requests
2. Pre-compute simplified versions at multiple resolutions
3. Store simplified geometries in database using `ST_SimplifyPreserveTopology`
4. Benefits:
   - Smaller response sizes
   - Faster serialization
   - Reduced client-side rendering load

**Estimated Impact:** Medium-High - Particularly beneficial for complex polygon layers (parcels, boundaries)

**Example auto-simplification:**
```csharp
double tolerance = CalculateToleranceFromZoom(zoom, mapBounds);
if (tolerance > 0 && !context.MaxAllowableOffset.HasValue) {
    context = context with { MaxAllowableOffset = tolerance };
}
```

---

### 5.3 **OPPORTUNITY:** Feature ID List Optimization

**Current State:**
- `GeoservicesQueryService.cs:395-418` - IDs buffered into List<object>
- Pre-allocates estimated size

**Recommendation:**
1. Use `ArrayPool<T>` for temporary ID buffers
2. Rent arrays from pool, return after use
3. Reduces GC pressure for ID-only queries

**Estimated Impact:** Low - Minor GC reduction

**Implementation:**
```csharp
var pool = ArrayPool<object>.Shared;
var idsBuffer = pool.Rent(effectiveLimit);
try {
    int count = 0;
    await foreach (var record in _repository.QueryAsync(...)) {
        idsBuffer[count++] = idValue;
        if (count >= effectiveLimit) break;
    }
    return new GeoservicesIdsQueryResult(idsBuffer.Take(count).ToList(), exceeded);
} finally {
    pool.Return(idsBuffer);
}
```

---

## 6. Configuration Tuning Recommendations

### 6.1 WMS Configuration

**Current Defaults (Good):**
- `WmsOptions.cs:24` - MaxWidth: 4096px
- `WmsOptions.cs:32` - MaxHeight: 4096px
- `WmsOptions.cs:40` - MaxTotalPixels: 16,777,216 (4096×4096)
- `WmsOptions.cs:48` - RenderTimeout: 60 seconds
- `WmsOptions.cs:57` - StreamingThreshold: 2MB

**Recommendations:**
1. **For high-concurrency deployments:**
   - Reduce `MaxTotalPixels` to 8,388,608 (2048×2048 equivalent)
   - Reduce `RenderTimeout` to 30 seconds
   - Set `StreamingThreshold` to 1MB

2. **For high-quality map production:**
   - Increase `MaxWidth` and `MaxHeight` to 8192px
   - Increase `RenderTimeout` to 120 seconds
   - Keep `StreamingThreshold` at 2MB or higher

---

### 6.2 Pagination Defaults

**Current Implementation:**
- OGC API default page size: 10 features (`OgcFeaturesQueryHandler.cs:99`)
- GeoServices default varies by layer configuration

**Recommendations:**
1. Increase OGC API default to 100 features (more practical)
2. Add global max limit configuration (e.g., 5,000 features per request)
3. Document pagination requirements in API responses

---

### 6.3 Feature Query Limits

**Current Implementation:**
- MaxResultsWithoutPagination: 10,000 (`GeoservicesQueryService.cs:45`)

**Recommendations:**
1. Make this configurable per deployment size
2. For small deployments (<100GB data): 10,000 is appropriate
3. For large deployments (>1TB data): Consider 5,000 or lower
4. Add per-layer override capability

---

## 7. Monitoring & Observability Enhancements

### 7.1 Slow Query Logging - ALREADY IMPLEMENTED ✓

**Current Implementation (GOOD):**
- `FeatureRepository.cs:172-176` - Logs queries >1000ms as warnings

**Evidence:**
```csharp
if (stopwatch.ElapsedMilliseconds > 1000) {
    _logger.LogWarning("Slow count query for {ServiceId}/{LayerId} took {ElapsedMs}ms, returned {Count}",
        serviceId, layerId, stopwatch.ElapsedMilliseconds, count);
}
```

**Status:** Good foundation. Consider lowering threshold to 500ms for tighter monitoring.

---

### 7.2 **OPPORTUNITY:** Query Performance Metrics Dashboard

**Recommendation:**
1. Add histogram metrics for query duration by:
   - Endpoint type (WFS, WMS, OGC API, GeoServices)
   - Operation (Query, Count, Statistics, Distinct)
   - Layer ID
   - Filter complexity (simple vs. complex spatial)

2. Track cache hit rates by:
   - Cache type (raster tiles, capabilities, metadata)
   - Dataset ID
   - Time window

3. Monitor connection pool metrics:
   - Active connections
   - Waiting requests
   - Connection creation rate

**Estimated Impact:** High - Enables data-driven optimization decisions

---

### 7.3 **OPPORTUNITY:** Request Profiling Middleware

**Recommendation:**
1. Add lightweight profiling for requests >1 second
2. Break down time spent in:
   - Authentication/authorization
   - Parameter parsing
   - Database query execution
   - Serialization
   - Network I/O

3. Sample 1-5% of requests to avoid overhead

---

## 8. Protocol-Specific Optimizations

### 8.1 WFS Transaction Handling

**Current State:**
- `WfsTransactionHandlers.cs` - Streaming XML parser for transactions
- `WfsStreamingTransactionParser.cs` - Memory-efficient parsing

**Status:** ✓ Well-optimized - uses streaming parser to avoid loading entire XML into memory

---

### 8.2 **OPPORTUNITY:** WFS Stored Queries Pre-compilation

**Current State:**
- `WfsGetFeatureHandlers.cs:341-441` - Stored queries parsed on every execution
- CQL filter substitution happens at runtime

**Recommendation:**
1. Pre-compile stored queries at server startup
2. Create prepared query templates with parameter placeholders
3. Cache compiled query AST per stored query definition

**Estimated Impact:** Low-Medium - Speeds up stored query execution

---

### 8.3 **OPPORTUNITY:** GeoServices REST Where Clause Optimization

**Current State:**
- Where clause parsing handled by `GeoservicesWhereParser.cs`
- Converted to QueryFilter on every request

**Recommendation:**
1. Cache parsed where clauses (similar to CQL caching recommendation)
2. Normalize equivalent expressions:
   - `status='active'` vs `STATUS = 'active'` (case insensitivity)
   - `x=1 AND y=2` vs `y=2 AND x=1` (commutativity)

**Estimated Impact:** Low - Reduces parsing overhead for repeated patterns

---

## 9. Infrastructure-Level Optimizations

### 9.1 **OPPORTUNITY:** CDN Integration for Static Tiles

**Current State:**
- CDN configuration available (`RasterCdnDefinition`)
- `WmsGetMapHandlers.cs:328-360` - CDN cache headers set

**Recommendation:**
1. Ensure CDN is properly configured for:
   - Raster tiles (WMS, WMTS)
   - Vector tiles (MVT)
   - Static resources (legends, symbols)

2. Set aggressive caching for immutable tiles:
   - `Cache-Control: public, max-age=31536000, immutable` for versioned tiles
   - `Cache-Control: public, max-age=3600` for dynamic tiles

3. Use CDN purge API for cache invalidation on data updates

**Estimated Impact:** Very High - Offloads 70-90% of tile requests from origin servers

---

### 9.2 **OPPORTUNITY:** Read Replicas for Query Scaling

**Recommendation:**
1. Route read-only queries to database read replicas
2. Write operations (WFS-T, OGC API mutations) go to primary
3. Implement read-write splitting at data provider level

**Estimated Impact:** Very High - Horizontal scaling for read-heavy workloads

**Complexity:** Medium - Requires:
- Read replica infrastructure
- Connection string routing logic
- Replication lag monitoring

---

### 9.3 **OPPORTUNITY:** Spatial Index Verification

**Recommendation:**
1. Audit all layers for proper spatial indexes
2. Verify index usage with `EXPLAIN ANALYZE` for common queries
3. For PostgreSQL:
   - Ensure GIST indexes on geometry columns
   - Consider BRIN indexes for time-series data
   - Use partial indexes for frequently filtered subsets

**Estimated Impact:** Very High - Can provide 10-100x speedup for spatial queries

**Verification queries:**
```sql
-- PostgreSQL: Check for missing spatial indexes
SELECT schemaname, tablename, attname
FROM pg_stats
WHERE attname = 'geom'
  AND schemaname NOT IN ('pg_catalog', 'information_schema')
  AND NOT EXISTS (
    SELECT 1 FROM pg_indexes
    WHERE tablename = pg_stats.tablename
      AND indexdef LIKE '%USING gist%'
  );
```

---

## 10. Priority Recommendations Summary

### Immediate (Quick Wins - Low Effort, High Impact)

1. **Implement GetCapabilities caching** - High impact, 2-4 hours effort
2. **Verify spatial indexes on all layers** - Very high impact, 1-2 hours effort
3. **Configure CDN for tile endpoints** - Very high impact, if not already done
4. **Lower slow query logging threshold to 500ms** - Monitoring improvement

### Short Term (1-2 weeks effort)

5. **Implement OGC Collections list caching** - Medium impact
6. **Add filter expression parsing cache** - Medium impact
7. **Configure connection pooling optimally** - Medium-high impact
8. **Implement query timeout configuration** - Medium impact

### Medium Term (1-4 weeks effort)

9. **Implement feature count approximation** - High impact for large datasets
10. **Add geometry simplification optimization** - Medium-high impact
11. **Implement CRS transformation caching** - Medium impact for frequently accessed layers
12. **Add request profiling middleware** - Monitoring/diagnostic improvement

### Long Term (1-3 months effort)

13. **Implement parallel multi-layer WMS rendering** - Medium impact, higher complexity
14. **Implement parallel search across collections** - Medium-high impact, high complexity
15. **Deploy read replicas** - Very high impact, infrastructure investment required
16. **Comprehensive query performance metrics dashboard** - Monitoring excellence

---

## Conclusion

The Honua Server geospatial service endpoints already demonstrate many performance best practices:
- ✓ Streaming responses to avoid memory pressure
- ✓ Database-level aggregations instead of in-memory processing
- ✓ Conditional count execution to avoid unnecessary work
- ✓ Proper pagination enforcement
- ✓ Multi-tier caching for raster tiles
- ✓ Adaptive response buffering based on size

The primary opportunities for further optimization focus on:
1. **Caching frequently-accessed metadata** (capabilities, collections, filter parsing)
2. **Parallel processing** for multi-layer and multi-collection operations
3. **Infrastructure scaling** (CDN, read replicas, index verification)
4. **Enhanced monitoring** for data-driven optimization decisions

Implementing the "Immediate" and "Short Term" recommendations could yield 20-40% performance improvement with relatively low effort. The "Long Term" recommendations require more significant investment but can enable 2-5x throughput improvements for high-scale deployments.
