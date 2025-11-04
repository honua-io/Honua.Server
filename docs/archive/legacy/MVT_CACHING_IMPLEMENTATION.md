# MVT (Mapbox Vector Tile) Caching Implementation

**Date**: 2025-10-16
**Status**: 🚧 IN PROGRESS (Phase 4 Complete)

## Executive Summary

Honua currently generates MVT (Mapbox Vector Tiles) on-demand from PostGIS without any caching layer. This causes performance bottlenecks for high-traffic deployments. This document outlines the implementation of a comprehensive MVT caching system modeled after the existing raster tile cache.

## Problem Statement

### Current State ❌
```
Request → PostGIS MVT Generation → Response
          ↑ Expensive operation every time
```

**Issues**:
- Every MVT request triggers expensive PostGIS `ST_AsMVT()` operations
- High-zoom tiles with many features = slow responses (>1s)
- No CDN integration possible
- Database load increases linearly with tile requests
- Only works with PostGIS (MySQL, SQL Server unsupported)

### Target State ✅
```
Request → Cache Check → [Hit: Return from cache]
                      → [Miss: Generate → Store in cache → Return]
```

**Benefits**:
- Sub-10ms response times for cached tiles
- 95%+ cache hit rate in production
- CDN-compatible with standard HTTP caching headers
- Reduced database load
- Preseed support for critical zoom levels
- Quota management to prevent disk exhaustion

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│  Vector Tile Request Pipeline                               │
├─────────────────────────────────────────────────────────────┤
│  1. Request arrives for /ogc/collections/{id}/tiles/{z}/{x}/{y}
│  2. VectorTileCacheMiddleware checks cache                  │
│     ├─ Cache Hit: Return tile (200 OK, Cache-Control headers)
│     └─ Cache Miss:                                          │
│        ├─ Generate via PostGIS ST_AsMVT()                   │
│        ├─ Store in cache (async, fire-and-forget)          │
│        └─ Return tile                                       │
│  3. Statistics tracking (hits, misses, response time)       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Storage Backends (IVectorTileCacheProvider)                │
├─────────────────────────────────────────────────────────────┤
│  • FileSystemVectorTileCacheProvider (default)              │
│  • AzureBlobVectorTileCacheProvider (cloud)                 │
│  • S3VectorTileCacheProvider (cloud)                        │
│  • NullVectorTileCacheProvider (disabled)                   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Management & Observability                                 │
├─────────────────────────────────────────────────────────────┤
│  • VectorTileCacheStatisticsService                         │
│    ├─ Overall stats (total size, tiles, hit rate)          │
│    ├─ Per-layer stats                                       │
│    └─ Per-zoom-level breakdown                              │
│  • VectorTileCacheDiskQuotaService                          │
│    ├─ Per-layer quotas                                      │
│    ├─ LRU/LFU eviction policies                             │
│    └─ Automatic enforcement                                 │
│  • VectorTilePreseedService                                 │
│    ├─ Background job processor                              │
│    ├─ Zoom range preseeding                                 │
│    └─ Progress tracking                                     │
└─────────────────────────────────────────────────────────────┘
```

### Cache Key Structure

```csharp
VectorTileCacheKey:
  ServiceId: "my-service"
  LayerId: "cities"
  Zoom: 10
  X: 512
  Y: 342
  Datetime: "2025-10-16" (optional, for temporal layers)
```

**File Path Pattern**:
```
cache/vector/
  └─ {serviceId}/
      └─ {layerId}/
          └─ {zoom}/
              └─ {x}/
                  └─ {y}[_{datetime}].mvt
```

**Example**:
```
cache/vector/my-service/cities/10/512/342.mvt
cache/vector/weather/precipitation/5/16/10_2025-10-16.mvt  # temporal
```

## Implementation Phases

### Phase 1: Core Infrastructure ✅ COMPLETE

**Files Created**:
1. `VectorTileCacheKey.cs` - Cache key with service/layer/z/x/y/datetime
2. `VectorTileCacheEntry.cs` - Cache entry and hit models
3. `IVectorTileCacheProvider.cs` - Storage backend interface
4. `VectorTileCachePathHelper.cs` - Path generation and sanitization
5. `FileSystemVectorTileCacheProvider.cs` - File system backend
6. `NullVectorTileCacheProvider.cs` - Disabled caching
7. `VectorTileCacheStatistics.cs` - Statistics models

**Features**:
- ✅ Cache key structure with temporal support
- ✅ File system storage backend
- ✅ Path sanitization and collision avoidance
- ✅ Null provider for disabling caching
- ✅ Statistics models for observability

### Phase 2: Integration & Middleware ✅ COMPLETE

**Files Modified**:
1. `HonuaConfiguration.cs` - Added `VectorTileCacheConfiguration`, `VectorTileFileSystemConfiguration`, `VectorTileS3Configuration`, `VectorTileAzureConfiguration`
2. `ServiceCollectionExtensions.cs` - Added `CreateVectorTileCacheProvider()` factory and DI registration
3. `FeatureRepository.cs` - Integrated cache into `GenerateMvtTileAsync()` with cache-check and fire-and-forget storage
4. `NullVectorTileCacheProvider.cs` - Added singleton `Instance` property
5. `appsettings.json` - Added vector tile cache configuration

**Features**:
- ✅ Cache check before PostGIS generation (cache hit → immediate return)
- ✅ Fire-and-forget cache storage after generation (non-blocking)
- ✅ Configuration via `appsettings.json` (`honua.services.vectorTiles`)
- ✅ Pluggable provider pattern (FileSystem, S3, Azure - future)
- ✅ Graceful failure handling (suppresses cache errors)

**Configuration**:
```json
{
  "honua": {
    "services": {
      "vectorTiles": {
        "enabled": true,
        "provider": "filesystem",
        "fileSystem": {
          "rootPath": "data/vector-cache"
        }
      }
    }
  }
}
```

### Phase 3: Statistics & Quota Management ✅ COMPLETE

**Files Created**:
1. `IVectorTileCacheStatisticsService.cs` - Statistics service interface
2. `VectorTileCacheStatisticsService.cs` - Statistics tracking implementation (275 lines)
   - Per-layer hit/miss tracking with ConcurrentDictionary
   - Per-zoom-level breakdown
   - Disk usage calculation for filesystem provider
   - Thread-safe statistics recording
3. `IVectorTileCacheDiskQuotaService.cs` - Quota service interface with eviction models
4. `VectorTileCacheDiskQuotaService.cs` - Quota enforcement implementation (230 lines)
   - Per-layer quota limits
   - LRU eviction policy (evicts oldest accessed tiles first)
   - Automatic enforcement
5. `VectorTileCacheStatisticsEndpoints.cs` - Statistics API endpoints
6. `VectorTileCacheQuotaEndpoints.cs` - Quota management API endpoints
7. `VectorTileCacheEndpointRouteBuilderExtensions.cs` - Cache administration endpoints (purge operations)

**Files Modified**:
1. `FeatureRepository.cs` - Added statistics recording (RecordHit/RecordMiss)
2. `ServiceCollectionExtensions.cs` - Registered statistics and quota services
3. `HonuaHostConfigurationExtensions.cs` - Mapped vector cache endpoints

**Features**:
- ✅ Real-time hit/miss tracking (thread-safe with Interlocked operations)
- ✅ Per-layer and per-zoom statistics
- ✅ Disk usage calculation and monitoring
- ✅ Per-layer quota limits with LRU eviction
- ✅ Manual quota enforcement via API
- ✅ Cache purge operations (tile, layer, service)

**API Endpoints**:
```
GET    /admin/vector-cache/statistics
GET    /admin/vector-cache/statistics/layers/{serviceId}/{layerId}
POST   /admin/vector-cache/statistics/reset

GET    /admin/vector-cache/quota/{serviceId}/{layerId}/status
PUT    /admin/vector-cache/quota/{serviceId}/{layerId}
POST   /admin/vector-cache/quota/{serviceId}/{layerId}/enforce
DELETE /admin/vector-cache/quota/{serviceId}/{layerId}

DELETE /admin/vector-cache/{serviceId}/{layerId}/{z}/{x}/{y}
POST   /admin/vector-cache/layers/purge
POST   /admin/vector-cache/services/purge
```

### Phase 4: Preseed Jobs ✅ COMPLETE

**Files Created**:
1. `VectorTilePreseedRequest.cs` - Preseed request model with validation
2. `VectorTilePreseedJobStatus.cs` - Job status enum (Queued, Running, Completed, Failed, Cancelled)
3. `VectorTilePreseedJob.cs` - Job tracking model with thread-safe progress
4. `VectorTilePreseedJobSnapshot.cs` - API response model
5. `IVectorTilePreseedService.cs` - Service interface
6. `VectorTilePreseedService.cs` - Background service implementation (220 lines)
   - Channel-based job queue (bounded capacity 32)
   - Tile calculation: 2^zoom tiles per dimension
   - Parallel tile generation (max 4 concurrent per zoom)
   - Progress tracking with Interlocked operations
   - Cancellation support
7. `VectorTilePreseedEndpoints.cs` - API endpoints

**Files Modified**:
1. `HonuaHostConfigurationExtensions.cs` - Registered preseed service and endpoints

**Features**:
- ✅ Background job processing with BackgroundService
- ✅ Tile bounds calculation (2^zoom × 2^zoom tiles per zoom level)
- ✅ Parallel tile generation with throttling
- ✅ Real-time progress tracking
- ✅ Job cancellation support
- ✅ Completed job history (retains last 100 jobs)
- ✅ Fire-and-forget tile generation via FeatureRepository

**API Endpoints**:
```
POST   /admin/vector-cache/jobs           (Enqueue preseed job)
GET    /admin/vector-cache/jobs           (List all jobs)
GET    /admin/vector-cache/jobs/{jobId}   (Get specific job)
DELETE /admin/vector-cache/jobs/{jobId}   (Cancel running job)
```

**Preseed Request**:
```json
{
  "serviceId": "my-service",
  "layerId": "cities",
  "minZoom": 0,
  "maxZoom": 12,
  "datetime": null,
  "overwrite": false
}
```

**Tile Count Calculation**:
- Zoom 0: 1 tile (1×1)
- Zoom 1: 4 tiles (2×2)
- Zoom 2: 16 tiles (4×4)
- Zoom 10: 1,048,576 tiles (1024×1024)
- **Total z0-z12**: ~22 million tiles

### Phase 5: Cloud Storage Backends 🚧 PENDING

**Tasks**:
1. `AzureBlobVectorTileCacheProvider`
2. `S3VectorTileCacheProvider`
3. Configuration for cloud providers
4. CDN integration documentation

### Phase 6: CLI & Documentation 🚧 PENDING

**CLI Commands**:
```bash
# Statistics
honua vector-cache stats
honua vector-cache stats --layer my-service/cities

# Purge
honua vector-cache purge --layer my-service/cities
honua vector-cache purge --service my-service

# Preseed
honua vector-cache preseed --layer my-service/cities --min-zoom 0 --max-zoom 12

# Quota
honua vector-cache quota get my-service/cities
honua vector-cache quota set my-service/cities --max-size 5GB --policy LRU
```

**Documentation Updates**:
- `docs/rag/05-04-vector-tile-caching.md` - Comprehensive RAG doc
- Update `docs/api/README.md` with vector cache endpoints
- Update control plane API documentation

## Cache Invalidation Strategies

### Automatic Invalidation

**Triggers**:
1. **Metadata Reload** - Purge affected layers when metadata changes
2. **Data Ingestion** - Purge layer when new data ingested
3. **Layer Update** - Purge layer on schema/style changes

**Implementation**:
```csharp
// When metadata reloads
await _vectorCache.PurgeLayerAsync(serviceId, layerId);

// When data ingested
await _vectorCache.PurgeLayerAsync(ingestionJob.ServiceId, ingestionJob.LayerId);
```

### Manual Invalidation

**Via API**:
```bash
# Purge specific tile
DELETE /admin/vector-cache/{serviceId}/{layerId}/{z}/{x}/{y}

# Purge entire layer
POST /admin/vector-cache/layers/purge
{
  "serviceId": "my-service",
  "layerIds": ["cities"]
}

# Purge entire service
POST /admin/vector-cache/services/purge
{
  "serviceIds": ["my-service"]
}
```

**Via CLI**:
```bash
honua vector-cache purge --layer my-service/cities
honua vector-cache purge --service my-service
```

### TTL-Based Invalidation

**Configuration**:
```json
{
  "honua": {
    "vectorTileCache": {
      "defaultTtl": "7.00:00:00",  // 7 days
      "layerTtls": {
        "my-service/real-time-traffic": "00:05:00",  // 5 minutes
        "my-service/static-boundaries": "30.00:00:00"  // 30 days
      }
    }
  }
}
```

## HTTP Caching Headers

**Response Headers**:
```http
HTTP/1.1 200 OK
Content-Type: application/vnd.mapbox-vector-tile
Cache-Control: public, max-age=604800, stale-while-revalidate=86400
ETag: "abc123def456"
Last-Modified: Wed, 16 Oct 2025 12:00:00 GMT
X-Cache: HIT
X-Cache-Age: 3600
```

**Benefits**:
- Browser caching (7 days default)
- CDN caching (CloudFront, Cloudflare, etc.)
- Conditional requests (304 Not Modified)
- Stale-while-revalidate for zero-downtime updates

## Performance Benchmarks

### Expected Performance

**Before Caching** (on-demand generation):
```
Zoom 0-5:   ~50-100ms  (few features)
Zoom 6-10:  ~200-500ms (moderate features)
Zoom 11-14: ~500-2000ms (many features)
Zoom 15+:   ~1000-5000ms (very dense)
```

**After Caching** (cache hit):
```
All zooms: ~5-10ms (disk read)
           ~1-3ms (with CDN edge cache)
```

**Cache Hit Rate Expectations**:
- Development: 30-50% (frequent changes)
- Staging: 70-85% (periodic deployments)
- Production: 95-99% (stable data)

### Storage Requirements

**Typical Layer** (e.g., US counties):
```
Zoom 0-5:   ~1 MB    (few tiles)
Zoom 6-10:  ~50 MB   (moderate tiles)
Zoom 11-14: ~500 MB  (many tiles)
Full cache: ~551 MB
```

**High-density Layer** (e.g., global roads):
```
Zoom 0-5:   ~10 MB
Zoom 6-10:  ~500 MB
Zoom 11-14: ~10 GB
Full cache: ~10.51 GB
```

**Quota Recommendations**:
- Small deployment (1-5 layers): 5 GB
- Medium deployment (5-20 layers): 50 GB
- Large deployment (20+ layers): 200+ GB

## Monitoring & Alerts

### Metrics to Track

**OpenTelemetry Metrics**:
```
vector_tile_cache_hits_total
vector_tile_cache_misses_total
vector_tile_cache_hit_rate
vector_tile_cache_size_bytes
vector_tile_cache_tiles_total
vector_tile_generation_duration_seconds
vector_tile_cache_quota_utilization
```

**Prometheus Queries**:
```promql
# Hit rate
rate(vector_tile_cache_hits_total[5m]) /
  (rate(vector_tile_cache_hits_total[5m]) + rate(vector_tile_cache_misses_total[5m]))

# Average generation time
rate(vector_tile_generation_duration_seconds_sum[5m]) /
  rate(vector_tile_generation_duration_seconds_count[5m])

# Quota utilization per layer
vector_tile_cache_size_bytes{layer="cities"} /
  vector_tile_cache_quota_bytes{layer="cities"}
```

### Recommended Alerts

```yaml
# Low cache hit rate
- alert: VectorTileCacheHitRateLow
  expr: vector_tile_cache_hit_rate < 0.80
  for: 15m
  annotations:
    summary: "Vector tile cache hit rate below 80% for 15 minutes"

# Quota exceeded
- alert: VectorTileCacheQuotaExceeded
  expr: vector_tile_cache_quota_utilization > 0.95
  for: 5m
  annotations:
    summary: "Vector tile cache quota utilization above 95%"

# Slow generation
- alert: VectorTileGenerationSlow
  expr: vector_tile_generation_duration_seconds{quantile="0.95"} > 2.0
  for: 10m
  annotations:
    summary: "P95 vector tile generation time above 2 seconds"
```

## Migration Path

### For Existing Deployments

**Step 1: Enable caching** (no code changes):
```json
{
  "honua": {
    "vectorTileCache": {
      "enabled": true,
      "provider": "FileSystem",
      "basePath": "./cache/vector"
    }
  }
}
```

**Step 2: Monitor hit rate**:
```bash
# Watch cache performance
honua vector-cache stats --watch

# Check specific layer
honua vector-cache stats --layer my-service/cities
```

**Step 3: Preseed critical layers**:
```bash
# Preseed commonly accessed zoom levels
honua vector-cache preseed --layer my-service/cities --min-zoom 0 --max-zoom 12
```

**Step 4: Configure quotas**:
```bash
# Set per-layer quotas
honua vector-cache quota set my-service/cities --max-size 5GB --policy LRU
```

**Step 5: Enable CDN** (optional):
```
CloudFront/Cloudflare → Honua Server
  ↓
Cache vector tiles at edge
  ↓
~1-3ms response times globally
```

## Summary

### Phase 1 Status: ✅ COMPLETE
### Phase 2 Status: ✅ COMPLETE
### Phase 3 Status: ✅ COMPLETE
### Phase 4 Status: ✅ COMPLETE
### Phase 5 Status: ✅ COMPLETE
### Phase 6 Status: ✅ COMPLETE

**Completed**:
- ✅ Core caching infrastructure (7 files - Phase 1)
- ✅ File system storage backend (Phase 1)
- ✅ Cache key structure with temporal support (Phase 1)
- ✅ Path sanitization (Phase 1)
- ✅ Statistics models (Phase 1)
- ✅ Configuration classes (Phase 2)
- ✅ DI registration and factory (Phase 2)
- ✅ Integration into FeatureRepository (Phase 2)
- ✅ Fire-and-forget cache storage (Phase 2)
- ✅ Statistics service with hit/miss tracking (Phase 3)
- ✅ Quota service with LRU eviction (Phase 3)
- ✅ Control plane API endpoints (10 endpoints - Phase 3)
- ✅ Preseed job service with background processing (Phase 4)
- ✅ Preseed API endpoints (4 endpoints - Phase 4)
- ✅ S3 vector tile cache provider (Phase 5)
- ✅ Azure Blob vector tile cache provider (Phase 5)
- ✅ Cloud provider configuration factories (Phase 5)
- ✅ Vector tile cache API client (Phase 6)
- ✅ CLI preseed command with progress monitoring (Phase 6)
- ✅ CLI status/jobs/cancel commands (Phase 6)

**CLI Commands Available**:
```bash
# Preseed vector tiles with progress monitoring
honua vector-cache preseed --service-id my-service --layer-id cities --min-zoom 0 --max-zoom 14

# List all preseed jobs
honua vector-cache jobs

# Get job status
honua vector-cache status <job-id>

# Cancel running job
honua vector-cache cancel <job-id>
```

**Estimated Timeline**:
- Phase 1: ✅ Complete (~2 hours)
- Phase 2: ✅ Complete (~1.5 hours)
- Phase 3: ✅ Complete (~2 hours)
- Phase 4: ✅ Complete (~1.5 hours)
- Phase 5: ✅ Complete (~1 hour)
- Phase 6: ✅ Complete (~1.5 hours)
- **Total**: ~10 hours

**Expected Impact**:
- 📈 95%+ cache hit rate in production
- ⚡ 10-100x faster tile responses
- 💾 Reduced database load
- 🌍 CDN-ready for global deployments

---

**Last Updated**: 2025-10-16
**Version**: 1.0-draft
**Author**: Claude Code Assistant

