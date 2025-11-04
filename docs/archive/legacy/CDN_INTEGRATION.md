# CDN Integration

Honua Server provides built-in CDN caching support for tile endpoints, allowing you to configure HTTP cache headers per dataset for optimal edge caching performance.

## Overview

CDN integration adds Cache-Control headers to tile responses from:

- **WMS GetMap** - Rendered map tiles
- **WMTS GetTile** - Pre-tiled map tiles
- **WCS GetCoverage** - Coverage data
- **OGC API - Tiles** - RESTful tile endpoint

Capabilities endpoints (GetCapabilities, DescribeCoverage, etc.) do **not** include CDN headers, as metadata may change and should not be aggressively cached.

## Configuration

CDN caching is configured per dataset in your raster configuration:

### Named Policies

Use predefined cache policies:

```json
{
  "rasters": [
    {
      "id": "satellite_imagery",
      "title": "Satellite Imagery",
      "source": {
        "type": "file",
        "uri": "/data/satellite.tif"
      },
      "cdn": {
        "enabled": true,
        "policy": "LongLived"
      }
    }
  ]
}
```

**Available Policies:**

| Policy | Max-Age | Use Case |
|--------|---------|----------|
| `NoCache` | 0 seconds | Dynamic data that changes frequently |
| `ShortLived` | 5 minutes | Frequently updated tiles |
| `MediumLived` | 1 hour | Moderately dynamic data |
| `LongLived` | 1 day | Static or slowly changing data (default) |
| `VeryLongLived` | 30 days | Stable datasets |
| `Immutable` | 1 year | Content that never changes |

### Custom Configuration

Configure cache directives explicitly:

```json
{
  "cdn": {
    "enabled": true,
    "maxAge": 86400,
    "sharedMaxAge": 2592000,
    "public": true,
    "immutable": false,
    "mustRevalidate": false,
    "noStore": false,
    "noTransform": true,
    "staleWhileRevalidate": 86400,
    "staleIfError": 604800
  }
}
```

**Configuration Options:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | boolean | false | Enable CDN headers for this dataset |
| `policy` | string | null | Named policy (overrides individual settings) |
| `maxAge` | integer | 86400 | Browser cache time (seconds) |
| `sharedMaxAge` | integer | null | CDN edge cache time (seconds) |
| `public` | boolean | true | Allow caching in shared caches |
| `immutable` | boolean | false | Content never changes |
| `mustRevalidate` | boolean | false | Must revalidate after expiration |
| `noStore` | boolean | false | Do not store in any cache |
| `noTransform` | boolean | true | Prevent CDN transformations |
| `staleWhileRevalidate` | integer | null | Serve stale while revalidating (seconds) |
| `staleIfError` | integer | null | Serve stale on error (seconds) |

## Cache Headers

When CDN is enabled, responses include:

```http
Cache-Control: public, max-age=86400, s-maxage=2592000, no-transform, stale-while-revalidate=86400, stale-if-error=604800
Vary: Accept-Encoding
```

### Cache-Control Directives

- **public** - Cacheable by browsers and CDN
- **max-age** - Browser cache TTL
- **s-maxage** - Shared cache (CDN) TTL
- **no-transform** - Prevent image optimization/compression by CDN
- **immutable** - Content never changes (can cache indefinitely)
- **must-revalidate** - Must check with origin after expiration
- **stale-while-revalidate** - Serve stale content while fetching fresh copy
- **stale-if-error** - Serve stale content if origin is unavailable

### Vary Header

The `Vary: Accept-Encoding` header ensures the CDN caches different versions for different compression encodings (gzip, br, etc.).

## Use Cases

### Weather Data (Frequently Updated)

```json
{
  "id": "weather_radar",
  "cdn": {
    "enabled": true,
    "policy": "ShortLived"
  }
}
```

Cache for 5 minutes - balances freshness with reduced origin load.

### Satellite Imagery (Daily Updates)

```json
{
  "id": "landsat",
  "cdn": {
    "enabled": true,
    "policy": "LongLived"
  }
}
```

Cache for 1 day - tiles update daily, CDN serves most requests.

### Base Maps (Static)

```json
{
  "id": "world_basemap",
  "cdn": {
    "enabled": true,
    "policy": "VeryLongLived"
  }
}
```

Cache for 30 days - static base maps rarely change.

### Historical Data (Immutable)

```json
{
  "id": "historical_2020",
  "cdn": {
    "enabled": true,
    "policy": "Immutable"
  }
}
```

Cache for 1 year with immutable flag - historical data never changes.

### High Availability with Stale Content

```json
{
  "id": "critical_infrastructure",
  "cdn": {
    "enabled": true,
    "maxAge": 3600,
    "sharedMaxAge": 86400,
    "staleWhileRevalidate": 3600,
    "staleIfError": 604800
  }
}
```

Serve stale tiles if origin is down - ensures availability during outages.

## CDN Provider Integration

### Cloudflare

Cloudflare respects standard Cache-Control headers. Additional configuration:

1. **Cache Level**: Set to "Standard" or "Cache Everything"
2. **Browser Cache TTL**: Use "Respect Existing Headers"
3. **Edge Cache TTL**: Honors `s-maxage`

### CloudFront

CloudFront configuration:

1. **Cache Policy**: Create custom policy
2. **TTL Settings**: Use headers from origin
3. **Query Strings**: Forward `TIME`, `datetime` parameters for temporal data
4. **Headers**: Forward `Accept-Encoding`

### Fastly

Fastly VCL configuration:

```vcl
sub vcl_fetch {
  if (beresp.http.Cache-Control ~ "s-maxage") {
    set beresp.ttl = std.atoi(regsub(beresp.http.Cache-Control, ".*s-maxage=(\d+).*", "\1")) s;
  }
}
```

### Akamai

Akamai caching rules:

1. **Caching Option**: Honor Cache-Control
2. **Max-Age**: Use `s-maxage` if present
3. **Query String Parameters**: Include for cache key if temporal

## Temporal Data Caching

When using temporal dimensions, cache keys include the TIME/datetime parameter:

```json
{
  "id": "sea_surface_temp",
  "temporal": {
    "enabled": true,
    "defaultValue": "2024-01-15T00:00:00Z"
  },
  "cdn": {
    "enabled": true,
    "policy": "LongLived"
  }
}
```

Each timestamp is cached separately:
- `/wms?TIME=2024-01-15&...` → Cached independently
- `/wms?TIME=2024-01-16&...` → Separate cache entry

CDN must include TIME parameter in cache key (configure via query string forwarding).

## Performance Optimization

### Separate Browser and CDN TTLs

```json
{
  "cdn": {
    "enabled": true,
    "maxAge": 3600,
    "sharedMaxAge": 2592000
  }
}
```

- Browsers cache for 1 hour
- CDN caches for 30 days
- Allows invalidation at CDN without affecting all users

### Graceful Degradation

```json
{
  "cdn": {
    "enabled": true,
    "maxAge": 3600,
    "staleWhileRevalidate": 86400,
    "staleIfError": 604800
  }
}
```

- Normal operation: 1 hour cache
- During revalidation: Serve stale up to 1 day
- During outage: Serve stale up to 7 days

### Pre-warming CDN Cache

For important zoom levels/regions, pre-warm the cache:

```bash
# Pre-warm zoom levels 0-10
for z in {0..10}; do
  for x in $(seq 0 $((2**z - 1))); do
    for y in $(seq 0 $((2**z - 1))); do
      curl -s "https://cdn.example.com/wmts?LAYER=basemap&TILEMATRIX=$z&TILEROW=$y&TILECOL=$x" > /dev/null
    done
  done
done
```

## Monitoring

### Cache Hit Rate

Monitor `CF-Cache-Status` (Cloudflare) or `X-Cache` (CloudFront) headers:

- `HIT` - Served from CDN
- `MISS` - Fetched from origin
- `EXPIRED` - Cache expired, revalidating
- `STALE` - Served stale content

### Origin Load

CDN should handle 95%+ of tile requests. If origin load is high:

1. Increase `sharedMaxAge`
2. Add `staleWhileRevalidate`
3. Pre-warm cache for popular tiles

## Disabling CDN

To disable CDN caching for a dataset:

```json
{
  "cdn": {
    "enabled": false
  }
}
```

Or omit the `cdn` configuration entirely (defaults to disabled).

## Best Practices

1. **Static Data** - Use `VeryLongLived` or `Immutable` policies
2. **Dynamic Data** - Use `ShortLived` with `staleWhileRevalidate`
3. **Temporal Data** - Ensure CDN cache key includes time parameter
4. **Historical Data** - Set `immutable: true` for archives
5. **High Availability** - Configure `staleIfError` for critical datasets
6. **Separate TTLs** - Use different `maxAge` and `sharedMaxAge` for flexibility

## Troubleshooting

### Tiles Not Caching

**Check:**
- `cdn.enabled` is `true`
- CDN forwards query parameters for cache key
- CDN respects `Cache-Control` headers

### Stale Tiles After Update

**Solutions:**
- Purge CDN cache manually
- Reduce `sharedMaxAge`
- Use versioned URLs (e.g., `/v2/tiles/...`)

### Different Tiles for Same Request

**Cause:** CDN not including all parameters in cache key

**Fix:** Configure CDN to forward:
- `TIME` / `datetime` (temporal data)
- `styleId` (different styles)
- `format` (image format)

## Security Considerations

- **Public Data Only** - CDN caching is for public tile endpoints
- **No Authentication** - Do not cache authenticated requests
- **HTTPS** - Always use HTTPS with CDN
- **No-Transform** - Prevents CDN image optimization (preserves data integrity)

## Next Steps

- [Temporal Rasters](./TEMPORAL_RASTERS.md)
- [Raster Configuration](./RASTER_CONFIGURATION.md)
- [Performance Tuning](./PERFORMANCE.md)
