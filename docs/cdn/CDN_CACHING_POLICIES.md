# CDN Caching Policies for Honua Server

**Version**: 1.0
**Last Updated**: 2025-10-18
**Status**: Production Ready

## Table of Contents

1. [Overview](#overview)
2. [Caching Strategy](#caching-strategy)
3. [Cache Policies by Content Type](#cache-policies-by-content-type)
4. [Query String Handling](#query-string-handling)
5. [HTTP Headers](#http-headers)
6. [Provider-Specific Configurations](#provider-specific-configurations)
7. [Performance Optimization](#performance-optimization)
8. [Monitoring and Metrics](#monitoring-and-metrics)

## Overview

Honua Server serves multiple types of content with different caching requirements:

| Content Type | Volatility | Cache Strategy | Typical TTL |
|--------------|------------|----------------|-------------|
| **Map Tiles** | Low | Aggressive caching | 1-30 days |
| **Vector Tiles (MVT)** | Low | Aggressive caching | 1-30 days |
| **Raster Tiles** | Low | Aggressive caching | 1-30 days |
| **Metadata** | Medium | Short-lived cache | 5 minutes |
| **STAC Catalog** | Medium | Short-lived cache | 5 minutes |
| **Admin APIs** | High | No caching | 0 seconds |
| **Health Checks** | High | No caching | 0 seconds |

## Caching Strategy

### Three-Tier Caching Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Client Browser Cache                                       │
│  TTL: 1 hour - 1 day (configurable)                        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  CDN Edge Cache (CloudFront/Azure/Cloudflare)              │
│  TTL: 1 day - 30 days (configurable)                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Origin Server Cache (Honua internal cache)                │
│  - Vector tile cache (PostgreSQL generated)                │
│  - Raster tile cache (S3/Azure/GCS)                        │
│  - Metadata cache (in-memory)                               │
└─────────────────────────────────────────────────────────────┘
```

### Cache Key Components

CDN cache keys include these parameters to ensure correct tile serving:

**Required for all tile requests:**
- `FORMAT` - Image format (png, jpeg, webp)
- `CRS` or `srs` - Coordinate reference system

**For WMS tiles:**
- `LAYERS` - Layer name(s)
- `STYLES` - Style identifier
- `BBOX` - Bounding box
- `WIDTH` - Image width
- `HEIGHT` - Image height
- `TIME` - Temporal dimension (if applicable)

**For WMTS tiles:**
- `LAYER` - Layer name
- `TILEMATRIX` - Zoom level
- `TILEROW` - Row index
- `TILECOL` - Column index
- `TILEMATRIXSET` - Tile matrix set

**For OGC API Tiles:**
- `f` - Format
- `datetime` - Temporal dimension
- `styleId` - Style identifier

## Cache Policies by Content Type

### 1. Map Tiles (WMS, WMTS, OGC Tiles)

**Characteristics:**
- Immutable or rarely changing
- Large bandwidth consumption
- High request volume
- CPU-intensive generation

**Cache Policy:**
```http
Cache-Control: public, max-age=86400, s-maxage=2592000, stale-while-revalidate=3600, stale-if-error=604800
Vary: Accept-Encoding
```

**Breakdown:**
- `public` - Cacheable by browsers and CDN
- `max-age=86400` - Browser cache for 1 day
- `s-maxage=2592000` - CDN cache for 30 days
- `stale-while-revalidate=3600` - Serve stale while fetching fresh (1 hour)
- `stale-if-error=604800` - Serve stale if origin down (7 days)
- `Vary: Accept-Encoding` - Cache different versions per encoding

**Rationale:**
- Long CDN TTL (30 days) reduces origin load
- Shorter browser TTL (1 day) allows faster invalidation
- Stale-while-revalidate ensures zero-downtime updates
- Stale-if-error provides resilience during outages

### 2. Vector Tiles (MVT)

**Characteristics:**
- Generated from PostGIS database
- Expensive to generate (ST_AsMVT operations)
- Highly cacheable
- Zoom-level dependent size

**Cache Policy:**
```http
Cache-Control: public, max-age=86400, s-maxage=2592000, immutable
Vary: Accept-Encoding
Content-Type: application/vnd.mapbox-vector-tile
```

**Breakdown:**
- `immutable` - Content never changes (for versioned tiles)
- Same TTL strategy as map tiles
- Compressed with gzip or brotli

**Special Considerations:**
- Low zoom levels (0-5): Cache forever (stable)
- High zoom levels (15+): Shorter cache (may update)
- Include `datetime` parameter in cache key for temporal layers

### 3. Metadata Endpoints

**Endpoints:**
- `/stac/*` - STAC catalog
- `/ogc/collections` - OGC API collections
- `/ogc/conformance` - Conformance declaration
- GetCapabilities requests

**Cache Policy:**
```http
Cache-Control: public, max-age=300, s-maxage=300
Vary: Accept, Accept-Encoding
```

**Breakdown:**
- `max-age=300` - 5 minute cache (browser and CDN)
- Short TTL allows metadata updates to propagate quickly
- Include `Accept` header for content negotiation (JSON vs XML)

**Rationale:**
- Metadata changes when layers are added/removed
- 5 minutes balances freshness with performance
- Supports content negotiation (JSON, XML, HTML)

### 4. Static Assets

**Content:**
- OpenAPI specifications
- Landing pages
- Style files (SLD, Mapbox GL styles)

**Cache Policy:**
```http
Cache-Control: public, max-age=3600, s-maxage=86400
Vary: Accept-Encoding
```

**Breakdown:**
- 1 hour browser cache
- 1 day CDN cache
- Compressed delivery

### 5. Admin and Authenticated Endpoints

**Endpoints:**
- `/admin/*` - Admin APIs
- Any endpoint with `Authorization` header

**Cache Policy:**
```http
Cache-Control: no-store, no-cache, must-revalidate
Pragma: no-cache
```

**Breakdown:**
- `no-store` - Do not store in any cache
- `no-cache` - Must revalidate every time
- `must-revalidate` - Must check with origin
- `Pragma: no-cache` - HTTP/1.0 compatibility

**Security:**
- Never cache authenticated content
- Forward `Authorization` header to origin
- Bypass CDN for these endpoints

### 6. Health Checks

**Endpoints:**
- `/health`
- `/health/ready`
- `/health/live`

**Cache Policy:**
```http
Cache-Control: no-cache, no-store, must-revalidate
```

**Rationale:**
- Health status changes dynamically
- Load balancers need real-time data
- No caching ensures accurate health reporting

## Query String Handling

### WMS Requests

**Cache Key Includes:**
```
SERVICE, VERSION, REQUEST, LAYERS, STYLES, CRS, BBOX, WIDTH, HEIGHT, FORMAT, TIME, TRANSPARENT, BGCOLOR
```

**Ignored:**
```
_dc, timestamp, nocache (cache-busting parameters)
```

**Example:**
```
/wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=cities&STYLES=default&CRS=EPSG:3857&BBOX=-180,-90,180,90&WIDTH=256&HEIGHT=256&FORMAT=image/png&TIME=2024-01-15
```

### WMTS Requests

**Cache Key Includes:**
```
SERVICE, VERSION, REQUEST, LAYER, TILEMATRIXSET, TILEMATRIX, TILEROW, TILECOL, FORMAT
```

**Example:**
```
/wmts?SERVICE=WMTS&VERSION=1.0.0&REQUEST=GetTile&LAYER=cities&TILEMATRIXSET=WebMercatorQuad&TILEMATRIX=10&TILEROW=342&TILECOL=512&FORMAT=image/png
```

### OGC API Tiles

**Cache Key Includes:**
```
f, datetime, styleId
```

**Example:**
```
/ogc/collections/cities/tiles/WebMercatorQuad/10/342/512?f=mvt&datetime=2024-01-15
```

### STAC API

**Cache Key Includes:**
```
limit, offset, bbox, datetime, collections
```

**Example:**
```
/stac/search?limit=10&bbox=-180,-90,180,90&datetime=2024-01-01/2024-12-31
```

## HTTP Headers

### Response Headers Set by Honua

```http
Content-Type: image/png | application/json | application/vnd.mapbox-vector-tile
Cache-Control: public, max-age=86400, s-maxage=2592000
Vary: Accept-Encoding
ETag: "abc123def456"
Last-Modified: Wed, 18 Oct 2025 12:00:00 GMT
X-Content-Type-Options: nosniff
```

### Headers Added by CDN

**CloudFront:**
```http
X-Cache: Hit from cloudfront
X-Amz-Cf-Pop: SEA19-C1
X-Amz-Cf-Id: abc123...
Age: 3600
```

**Azure Front Door:**
```http
X-Azure-Ref: abc123...
X-Cache: TCP_HIT
Age: 3600
```

**Cloudflare:**
```http
CF-Cache-Status: HIT
CF-Ray: abc123-SEA
Age: 3600
Server: cloudflare
```

### Compression Headers

```http
Content-Encoding: gzip | br
Vary: Accept-Encoding
```

**Supported Encodings:**
- gzip - Universal support
- brotli (br) - Modern browsers, better compression

## Provider-Specific Configurations

### AWS CloudFront

**Cache Behaviors:**
1. **Tiles** - Managed Cache Policy (CachingOptimized)
2. **Metadata** - Custom Cache Policy (5 min TTL)
3. **Admin** - No caching

**Origin Request Policy:**
- Forward `Accept-Encoding` header
- Include query strings in cache key
- Forward viewer country headers (optional)

**Compression:**
- Automatic gzip compression
- Brotli compression (enable via response header policy)

### Azure Front Door

**Caching Rules:**
1. **Tiles** - Cache everything, 30 day TTL
2. **Metadata** - Cache, 5 min TTL
3. **Admin** - Bypass cache

**Compression:**
- Automatic compression enabled
- Supports gzip and brotli

**Query String Caching:**
- Include specified query strings
- Ignore cache-busting parameters

### Cloudflare

**Page Rules:**
1. **Tiles** - Cache Everything, Edge TTL 30 days
2. **Metadata** - Cache Everything, Edge TTL 5 min
3. **Admin** - Bypass cache

**Cache Rules (Modern):**
- Use Ruleset API for fine-grained control
- Cache key customization
- Origin cache control override

**Tiered Cache:**
- Enable Argo Tiered Caching (Business+ plan)
- Reduces origin requests
- Improves cache hit ratio

## Performance Optimization

### 1. Separate Browser and CDN TTLs

**Strategy:**
```http
Cache-Control: public, max-age=3600, s-maxage=2592000
```

**Benefits:**
- Longer CDN cache (30 days) = fewer origin requests
- Shorter browser cache (1 hour) = faster invalidation for users
- Allows targeted cache purging at CDN level

### 2. Stale Content Serving

**Strategy:**
```http
Cache-Control: stale-while-revalidate=3600, stale-if-error=604800
```

**Benefits:**
- Zero-downtime cache updates
- Resilience during origin outages
- Improved user experience (no waiting)

**Use Cases:**
- Tile updates: Serve cached tile while fetching new version
- Origin failure: Serve stale tiles for up to 7 days

### 3. Compression

**Brotli Compression:**
- 20-30% better compression than gzip
- Supported by all modern browsers
- Enable at CDN level

**Example Savings:**
```
JSON metadata: 100 KB → 15 KB (brotli) vs 20 KB (gzip)
PNG tiles: Minimal benefit (already compressed)
MVT tiles: 50 KB → 10 KB (brotli)
```

### 4. Conditional Requests

**ETag Support:**
```http
ETag: "abc123def456"
If-None-Match: "abc123def456"
→ 304 Not Modified
```

**Last-Modified Support:**
```http
Last-Modified: Wed, 18 Oct 2025 12:00:00 GMT
If-Modified-Since: Wed, 18 Oct 2025 12:00:00 GMT
→ 304 Not Modified
```

**Benefits:**
- Reduced bandwidth (no content transfer)
- Faster responses (304 is lightweight)
- Client-side validation

### 5. Pre-warming Cache

**Strategy:**
Preseed CDN cache for critical zoom levels and regions.

**CLI Commands:**
```bash
# Preseed vector tiles
honua vector-cache preseed --service-id my-service --layer-id cities --min-zoom 0 --max-zoom 12

# Preseed raster tiles
honua raster-cache preseed --collection satellite --min-zoom 0 --max-zoom 10
```

**When to Preseed:**
- Before launch: Populate cache for popular tiles
- After data updates: Refresh affected zoom levels
- Scheduled: Nightly preseed for new temporal data

## Monitoring and Metrics

### Key Metrics

**Cache Hit Ratio:**
```
Cache Hit Ratio = Cache Hits / (Cache Hits + Cache Misses)
```

**Target:** 95%+ in production

**Origin Offload:**
```
Origin Offload = 1 - (Origin Requests / Total Requests)
```

**Target:** 95%+ (CDN handles 95% of requests)

### CloudWatch Metrics (CloudFront)

```
CacheHitRate - Percentage of cache hits
OriginLatency - Origin response time
BytesDownloaded - Total bytes served
Requests - Total requests
4xxErrorRate - Client errors
5xxErrorRate - Server errors
```

### Azure Monitor Metrics (Front Door)

```
RequestCount - Total requests
CacheHitRatio - Cache hit percentage
OriginLatency - Origin response time
OriginHealthPercentage - Origin health
TotalLatency - Total request latency
```

### Cloudflare Analytics

```
Requests - Total requests
Bandwidth - Total bandwidth
Cache Hit Ratio - Cache effectiveness
Threats Stopped - WAF blocks
Status Codes - HTTP status distribution
```

### Prometheus Queries

```promql
# Cache hit rate
sum(rate(cdn_cache_hits_total[5m])) /
  (sum(rate(cdn_cache_hits_total[5m])) + sum(rate(cdn_cache_misses_total[5m])))

# Average response time
avg(cdn_response_time_seconds)

# Bandwidth usage
sum(rate(cdn_bytes_sent_total[5m]))
```

### Recommended Alerts

**Low Cache Hit Rate:**
```yaml
alert: CDNCacheHitRateLow
expr: cdn_cache_hit_ratio < 0.80
for: 15m
severity: warning
```

**High Origin Load:**
```yaml
alert: CDNOriginLoadHigh
expr: rate(cdn_origin_requests_total[5m]) > 1000
for: 10m
severity: warning
```

**Increased 5xx Errors:**
```yaml
alert: CDN5xxErrorsHigh
expr: rate(cdn_5xx_errors_total[5m]) > 10
for: 5m
severity: critical
```

## Best Practices Summary

1. **Use Separate TTLs** - Longer CDN cache, shorter browser cache
2. **Enable Compression** - Brotli for text content, gzip fallback
3. **Implement Stale Content** - Serve stale during updates/outages
4. **Monitor Cache Hit Ratio** - Target 95%+ in production
5. **Preseed Critical Tiles** - Populate cache before traffic spike
6. **Version Static Assets** - Immutable URLs for long caching
7. **Never Cache Admin** - Security and correctness
8. **Include Query Strings** - Proper cache key for tile parameters
9. **Use ETags** - Enable conditional requests
10. **Test Invalidation** - Verify purge procedures work

## Related Documentation

- [Cache Invalidation Procedures](./CDN_CACHE_INVALIDATION.md)
- [CDN Deployment Guide](./CDN_DEPLOYMENT_GUIDE.md)
- [CDN Integration](../CDN_INTEGRATION.md)
- [MVT Caching Implementation](../MVT_CACHING_IMPLEMENTATION.md)
- [Performance Tuning](../rag/04-operations/performance-tuning.md)
