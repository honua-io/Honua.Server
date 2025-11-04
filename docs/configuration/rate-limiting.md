# Rate Limiting Configuration

Honua implements comprehensive per-user and per-IP rate limiting to protect against abuse and ensure fair resource allocation.

## Features

- **Per-User Rate Limiting**: Different limits based on user roles (anonymous, authenticated, premium, administrator)
- **Per-IP Rate Limiting**: IP-based limits for anonymous users
- **Distributed Counting**: Redis-backed distributed rate limit tracking for multi-instance deployments
- **Sliding Window Algorithm**: More accurate than fixed windows, prevents burst traffic
- **Standard Headers**: Returns `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, and `Retry-After` headers
- **429 Status Code**: Returns proper HTTP 429 Too Many Requests with JSON error response

## Configuration

Add the following to your `appsettings.json`:

```json
{
  "RateLimiting": {
    "Enabled": true,

    "Anonymous": {
      "PermitLimit": 30,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 2
    },

    "Authenticated": {
      "PermitLimit": 200,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4
    },

    "Premium": {
      "PermitLimit": 1000,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 8
    },

    "Administrator": {
      "PermitLimit": 500,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 6
    },

    "Default": {
      "PermitLimit": 100,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 10
    },

    "OgcApi": {
      "PermitLimit": 200,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 20
    },

    "OpenRosa": {
      "PermitLimit": 50,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 5
    },

    "Geoservices": {
      "PermitLimit": 150,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 15
    },

    "PerIp": {
      "PermitLimit": 100,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 10
    }
  }
}
```

## Rate Limit Tiers

### Anonymous Users
- **Limit**: 30 requests per minute
- **Use Case**: Unauthenticated API access, public data browsing
- **Rate limited by**: Client IP address

### Authenticated Users
- **Limit**: 200 requests per minute
- **Use Case**: Standard authenticated users
- **Rate limited by**: User ID from JWT claims

### Premium Users
- **Limit**: 1000 requests per minute
- **Use Case**: Paid or premium tier users
- **Rate limited by**: User ID with premium role check
- **How to enable**: Add `premium` role or `tier: premium` claim to JWT

### Administrator Users
- **Limit**: 500 requests per minute
- **Use Case**: System administrators
- **Rate limited by**: User ID with administrator role

## Policy Configuration

### Endpoint-Specific Policies

Apply rate limiting to specific endpoints:

```csharp
app.MapGet("/api/data", () => "Data")
    .RequireRateLimiting(RateLimitingConfiguration.PerUserPolicy);

app.MapGet("/api/ogc", () => "OGC Data")
    .RequireRateLimiting(RateLimitingConfiguration.OgcApiPolicy);
```

### Available Policies

- `DefaultPolicy`: General-purpose rate limiting
- `OgcApiPolicy`: For OGC API endpoints (read-heavy)
- `OpenRosaPolicy`: For OpenRosa endpoints (write-heavy)
- `GeoservicesPolicy`: For Esri Geoservices endpoints
- `PerUserPolicy`: Dynamic per-user/per-IP limiting based on authentication
- `PerIpPolicy`: Strict per-IP limiting

## Redis Configuration

For distributed deployments, configure Redis:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

Rate limiting will automatically use Redis when available. In single-instance deployments or when Redis is unavailable, an in-memory store is used.

## Response Headers

When rate limiting is active, responses include:

- `X-RateLimit-Limit`: Maximum requests allowed in the window
- `X-RateLimit-Remaining`: Requests remaining in current window
- `X-RateLimit-Reset`: Unix timestamp when the window resets
- `Retry-After`: Seconds to wait before retrying (on 429 responses)

## Error Response

When rate limit is exceeded (HTTP 429):

```json
{
  "error": "rate_limit_exceeded",
  "message": "Too many requests. Please try again later.",
  "retry_after_seconds": 60
}
```

## Sliding Window Algorithm

The sliding window algorithm divides the time window into segments for more accurate tracking:

- **Window**: Total time period (e.g., 1 minute)
- **Segments**: Number of sub-windows (e.g., 4 segments = 15 seconds each)
- **Benefit**: Prevents burst traffic at window boundaries

Example: With `PermitLimit: 100`, `WindowMinutes: 1`, `SegmentsPerWindow: 4`:
- Each 15-second segment can handle up to 25 requests
- Total of 100 requests evenly distributed over 1 minute
- Smoother traffic patterns than fixed windows

## IP Address Detection

Rate limiting automatically detects client IP from:
1. `X-Forwarded-For` header (first IP)
2. `X-Real-IP` header
3. Connection remote IP address

This works correctly behind reverse proxies like nginx, Traefik, or Caddy.

## Disabling Rate Limiting

To disable rate limiting (not recommended for production):

```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

Rate limiting is automatically enabled in production environments regardless of this setting.

## Monitoring

Rate limit metrics are exposed via the metrics endpoint:

- `honua_ratelimit_requests_rejected_total`: Total rejected requests
- `honua_ratelimit_requests_allowed_total`: Total allowed requests
- `honua_ratelimit_policy_usage`: Usage by policy

## Best Practices

1. **Set Appropriate Limits**: Balance protection with user experience
2. **Use Redis in Production**: Required for multi-instance deployments
3. **Monitor Metrics**: Track rejection rates and adjust limits
4. **Communicate Limits**: Document rate limits in API documentation
5. **Return Headers**: Always include rate limit headers in responses
6. **Graceful Degradation**: Rate limiter fails open on Redis errors

## Troubleshooting

### Rate Limiting Not Working
- Check `RateLimiting:Enabled` is `true`
- Verify middleware is registered in pipeline
- Check logs for initialization messages

### Different Instances Have Different Counts
- Ensure Redis is properly configured
- Check Redis connectivity
- Verify all instances use same Redis database

### Too Many 429 Errors
- Review and increase limits in configuration
- Check for legitimate high-volume users
- Consider adding premium tier for power users
