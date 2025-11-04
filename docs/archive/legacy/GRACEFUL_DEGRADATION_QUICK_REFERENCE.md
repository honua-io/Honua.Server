# Graceful Degradation Quick Reference

## TL;DR

The Honua system automatically degrades non-critical features when unhealthy, ensuring the system remains operational during partial failures.

## Quick Examples

### Use Adaptive Caching

```csharp
// Automatically uses Redis when available, in-memory when degraded
var cache = serviceProvider.GetRequiredService<AdaptiveCacheService>();

// Set
await cache.SetAsync("my-key", dataBytes);

// Get
var data = await cache.GetAsync("my-key");
```

### Use Adaptive AI

```csharp
var aiService = serviceProvider.GetRequiredService<AdaptiveAIService>();

var response = await aiService.ExecuteAsync(
    "Deploy to AWS",
    aiFunc: async (prompt, ct) => await llmClient.GenerateAsync(prompt, ct),
    fallbackFunc: async (prompt, ct) => await ManualProcessAsync(prompt, ct)
);

if (!response.Success)
{
    // AI unavailable - handle gracefully
    return BadRequest(new
    {
        error = response.Error,
        suggestedActions = response.SuggestedActions
    });
}
```

### Check Feature Availability

```csharp
var featureMgmt = serviceProvider.GetRequiredService<IFeatureManagementService>();

if (await featureMgmt.IsFeatureAvailableAsync("Search"))
{
    // Use full-text search
}
else
{
    // Use database scan fallback
}
```

### Get Recommended Strategy

```csharp
var adaptive = serviceProvider.GetRequiredService<AdaptiveFeatureService>();

// Get appropriate search strategy
var strategy = await adaptive.GetSearchStrategyAsync();
// Returns: FullTextSearch, BasicIndex, or DatabaseScan

// Get appropriate metadata strategy
var metadataStrategy = await adaptive.GetMetadataStrategyAsync();
// Returns: FullStac, CachedStac, or BasicMetadata

// Get caching mode
var cachingMode = await adaptive.GetCachingModeAsync();
// Returns: Distributed or InMemory

// Get recommended tile resolution
var resolution = await adaptive.GetRecommendedTileResolutionAsync(TileResolution.High);
// Returns: High, Medium, or Low based on system health
```

## API Quick Reference

### Check System Status

```bash
# Get all feature statuses
curl http://localhost:5000/api/admin/features/status

# Get specific feature
curl http://localhost:5000/api/admin/features/status/AdvancedCaching

# Get active degradations only
curl http://localhost:5000/api/admin/features/degradations
```

### Manual Control

```bash
# Disable feature for maintenance
curl -X POST http://localhost:5000/api/admin/features/status/Search/disable \
  -H "Content-Type: application/json" \
  -d '{"reason": "Index rebuild"}'

# Re-enable feature
curl -X POST http://localhost:5000/api/admin/features/status/Search/enable

# Force health check
curl -X POST http://localhost:5000/api/admin/features/status/Search/check-health
```

## Configuration Quick Reference

### Enable/Disable Features

```json
{
  "Features": {
    "AdvancedCaching": {
      "Enabled": true,
      "Required": false,
      "MinHealthScore": 50,
      "RecoveryCheckInterval": 60,
      "Strategy": {
        "Type": "Fallback",
        "Message": "Using in-memory cache"
      }
    }
  }
}
```

### Strategy Types

- **Disable**: Turn off feature completely
- **Fallback**: Use simpler implementation
- **ReduceQuality**: Lower quality output (e.g., resolution)
- **ReduceFunctionality**: Basic features only
- **ReducePerformance**: More aggressive limits

## Response Headers

When features are degraded, responses include:

```http
X-Service-Status: Degraded
X-Feature-Status: AdvancedCaching=Degraded,Search=Unavailable
```

## Metrics

Query degradation metrics:

```bash
# Prometheus metrics endpoint
curl http://localhost:5000/metrics | grep honua_

# Key metrics:
# - honua_degraded_features_count
# - honua_unavailable_features_count
# - honua_feature_health_score
```

## Common Patterns

### Pattern 1: Try Feature, Fall Back on Error

```csharp
var isAvailable = await featureMgmt.IsFeatureAvailableAsync("AdvancedFeature");

if (isAvailable)
{
    try
    {
        return await AdvancedProcessAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Advanced processing failed, using fallback");
    }
}

// Fallback
return await BasicProcessAsync();
```

### Pattern 2: Adaptive Quality

```csharp
var adaptive = serviceProvider.GetRequiredService<AdaptiveFeatureService>();
var resolution = await adaptive.GetRecommendedTileResolutionAsync(requestedResolution);

// Use recommended resolution (may be lower if degraded)
return await GenerateTileAsync(resolution);
```

### Pattern 3: Graceful Error with Suggestions

```csharp
var aiService = serviceProvider.GetRequiredService<AdaptiveAIService>();
var response = await aiService.ExecuteAsync(prompt, aiFunc);

if (!response.Success)
{
    return new ProblemDetails
    {
        Status = 503,
        Title = "Service Temporarily Unavailable",
        Detail = response.Error,
        Extensions =
        {
            ["suggestedActions"] = response.SuggestedActions,
            ["mode"] = response.Mode.ToString()
        }
    };
}
```

### Pattern 4: Include Degradation Info in Response

```csharp
var metadata = await stacService.GetDatasetMetadataAsync(datasetId, getStac, getBasic);

return Ok(new
{
    data = metadata,
    degraded = metadata.IsCached,
    warning = metadata.Warning,
    metadataType = metadata.MetadataType
});
```

## Testing Degradation

### Unit Test Example

```csharp
[Fact]
public async Task WhenRedisDown_ShouldUseInMemoryCache()
{
    // Arrange - simulate Redis unavailable
    featureManagementMock
        .Setup(x => x.IsFeatureAvailableAsync("AdvancedCaching", default))
        .ReturnsAsync(false);

    // Act
    var mode = await adaptiveFeature.GetCachingModeAsync();

    // Assert
    Assert.Equal(CachingMode.InMemory, mode);
}
```

### Manual Test

```bash
# 1. Stop Redis
docker stop redis

# 2. Verify degradation
curl http://localhost:5000/api/admin/features/status/AdvancedCaching
# Should show: "state": "Degraded"

# 3. Test that caching still works (using in-memory)
curl http://localhost:5000/api/your-cached-endpoint

# 4. Start Redis
docker start redis

# 5. Wait for recovery (60s by default)
sleep 60

# 6. Verify recovery
curl http://localhost:5000/api/admin/features/status/AdvancedCaching
# Should show: "state": "Healthy"
```

## Troubleshooting

### Feature Won't Recover

```bash
# Force health check
curl -X POST http://localhost:5000/api/admin/features/status/MyFeature/check-health

# If still degraded, manually enable
curl -X POST http://localhost:5000/api/admin/features/status/MyFeature/enable

# Check logs for errors
docker logs honua-server | grep MyFeature
```

### Too Many 429 (Rate Limited)

```bash
# Check how many features are degraded
curl http://localhost:5000/api/admin/features/degradations

# If many degraded, rate limits are more aggressive
# Fix underlying issues or temporarily disable non-critical features
```

## Best Practices

1. **Always check feature availability** before using optional features
2. **Provide fallbacks** for degraded features
3. **Include degradation info** in API responses
4. **Monitor metrics** for degradation events
5. **Set alerts** for features degraded >5 minutes
6. **Test degradation scenarios** in CI/CD
7. **Document user impact** for each degradation

## Feature List

| Feature | Fallback | User Impact |
|---------|----------|-------------|
| AdvancedCaching | In-memory | Slight latency |
| AIConsultant | Graceful error | No AI help |
| Search | Database scan | Slower search |
| RealTimeMetrics | Logs only | No metrics export |
| StacCatalog | Basic metadata | Stale data |
| AdvancedRasterProcessing | Lower resolution | Lower quality |
| VectorTiles | Disabled | Unavailable |
| Analytics | Disabled | No tracking |
| ExternalStorage | Local only | Limited storage |
| OidcAuthentication | Local auth | No SSO |

## Related Docs

- Full Guide: [GRACEFUL_DEGRADATION.md](./GRACEFUL_DEGRADATION.md)
- Implementation: [GRACEFUL_DEGRADATION_IMPLEMENTATION.md](../GRACEFUL_DEGRADATION_IMPLEMENTATION.md)
- Health Checks: [HEALTH_CHECKS.md](./HEALTH_CHECKS.md)
- Monitoring: [observability/README.md](./observability/README.md)
