# Rate Limiting Quick Reference

## Default Rate Limits

| User Type | Requests/Minute | Identification |
|-----------|-----------------|----------------|
| Anonymous | 30 | IP Address |
| Authenticated | 200 | User ID (JWT) |
| Premium | 1000 | User ID + premium role |
| Administrator | 500 | User ID + admin role |

## Apply Rate Limiting to Endpoints

```csharp
// Per-user rate limiting (recommended)
app.MapGet("/api/data", handler)
    .RequireRateLimiting(RateLimitingConfiguration.PerUserPolicy);

// Per-IP rate limiting
app.MapGet("/api/public", handler)
    .RequireRateLimiting(RateLimitingConfiguration.PerIpPolicy);

// Endpoint-specific policies
app.MapGet("/ogc/features", handler)
    .RequireRateLimiting(RateLimitingConfiguration.OgcApiPolicy);
```

## Minimal Configuration

```json
{
  "RateLimiting": {
    "Enabled": true
  }
}
```

## Production Configuration with Redis

```json
{
  "ConnectionStrings": {
    "Redis": "redis.example.com:6379,password=secret"
  },
  "RateLimiting": {
    "Enabled": true,
    "Anonymous": { "PermitLimit": 30, "WindowMinutes": 1 },
    "Authenticated": { "PermitLimit": 200, "WindowMinutes": 1 },
    "Premium": { "PermitLimit": 1000, "WindowMinutes": 1 }
  }
}
```

## Response Headers

**Success (200 OK):**
- `X-RateLimit-Policy: active`

**Rate Limited (429 Too Many Requests):**
- `X-RateLimit-Limit: 30`
- `X-RateLimit-Remaining: 0`
- `X-RateLimit-Reset: 1697654400`
- `Retry-After: 60`

## Making Users Premium

Add to JWT claims:
```json
{
  "sub": "user123",
  "role": "premium"
}
```

Or:
```json
{
  "sub": "user123",
  "tier": "premium"
}
```

## Monitoring

Prometheus metrics available at `/metrics`:
- `honua_ratelimit_requests_rejected_total`
- `honua_ratelimit_requests_allowed_total`
- `honua_ratelimit_policy_usage`

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Rate limiting not working | Check `RateLimiting:Enabled` is `true` |
| Different counts per instance | Configure Redis |
| Too many 429s | Increase limits or add premium tier |
| Redis connection fails | System falls back to in-memory (check logs) |

## Testing

```bash
# Test rate limiting
for i in {1..35}; do
  curl -i http://localhost:5000/api/data
done

# Expected: First 30 succeed, remaining 5 return 429

# Test with authentication
for i in {1..205}; do
  curl -i -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/data
done

# Expected: First 200 succeed, remaining 5 return 429
```

## Disable for Development

```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

**Note:** Rate limiting is always enabled in production environments regardless of this setting.
