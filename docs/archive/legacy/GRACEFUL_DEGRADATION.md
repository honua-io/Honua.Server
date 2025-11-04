# Graceful Degradation Patterns

## Overview

Honua implements comprehensive graceful degradation patterns to ensure the system remains partially functional even when some components fail. This document describes the degradation strategies, feature flags, and recovery mechanisms.

## Architecture

### Feature Management System

The graceful degradation system is built on three core components:

1. **FeatureManagementService**: Monitors feature health and manages degradation state
2. **AdaptiveFeatureService**: Provides feature-specific degradation logic
3. **Health Checks**: Integrate with ASP.NET Core health checks to trigger degradation

### Feature Criticality Levels

Features are categorized by criticality:

#### Critical Features (Must Work)
- Core feature serving (OGC API Features, WFS)
- Authentication and authorization
- Database connectivity

**Behavior**: Application will not start if these features fail health checks.

#### Important Features (Degrade Gracefully)
- Distributed caching (Redis) → Falls back to in-memory cache
- Metrics collection → Falls back to log-only mode
- Search/indexing → Falls back to database queries

**Behavior**: Features automatically degrade when unhealthy, with automatic recovery attempts.

#### Optional Features (Can Disable)
- AI consultant features
- Analytics and usage tracking
- Advanced rendering options

**Behavior**: Features are disabled when unavailable, system continues operating normally.

## Configuration

### Feature Flags

Feature flags are configured in `appsettings.json` under the `Features` section:

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
        "Message": "Using in-memory cache (Redis unavailable)"
      }
    }
  }
}
```

### Configuration Options

- **Enabled**: Whether the feature is enabled (default: true)
- **Required**: If true, application won't start if feature is unhealthy (default: false)
- **MinHealthScore**: Minimum health score (0-100) to keep feature enabled (default: 50)
- **RecoveryCheckInterval**: Seconds between recovery checks for degraded features (default: 60)
- **Strategy**: Degradation strategy configuration

### Degradation Strategy Types

1. **Disable**: Completely disable the feature
2. **ReduceQuality**: Serve lower quality (e.g., lower resolution tiles)
3. **ReduceFunctionality**: Reduce to basic functionality (e.g., simple search vs. full-text)
4. **ReducePerformance**: More aggressive rate limiting, disable optimizations
5. **Fallback**: Use fallback implementation (e.g., in-memory cache vs. Redis)

## Degradation Scenarios

### Scenario 1: Redis Unavailable

**Trigger**: Redis health check fails or connection lost

**Degradation**:
- Feature: `AdvancedCaching`
- Strategy: Fallback to in-memory cache
- User Impact: Slightly higher latency, cache not shared across instances
- Recovery: Automatic when Redis returns

**Response Headers**:
```http
X-Service-Status: Degraded
X-Feature-Status: AdvancedCaching=Degraded
```

**Code Example**:
```csharp
var cacheService = serviceProvider.GetRequiredService<AdaptiveCacheService>();
var data = await cacheService.GetAsync("my-key"); // Automatically uses in-memory if Redis down
```

### Scenario 2: AI Service Unavailable

**Trigger**: LLM API unreachable or quota exceeded

**Degradation**:
- Feature: `AIConsultant`
- Strategy: Disable with helpful error message
- User Impact: No AI assistance, manual configuration required
- Recovery: Automatic when LLM API returns

**API Response**:
```json
{
  "success": false,
  "mode": "Unavailable",
  "error": "AI features are currently unavailable. Please try again later or configure your deployment manually.",
  "suggestedActions": [
    "Check the documentation at /docs",
    "Use the manual configuration wizard",
    "Contact support if the issue persists"
  ]
}
```

**Code Example**:
```csharp
var aiService = serviceProvider.GetRequiredService<AdaptiveAIService>();
var response = await aiService.ExecuteAsync(
    "Deploy to AWS",
    aiFunc: (prompt, ct) => llmClient.GenerateAsync(prompt, ct),
    fallbackFunc: (prompt, ct) => ManualConfigurationAsync(prompt, ct)
);

if (!response.Success)
{
    // Handle unavailability gracefully
    logger.LogWarning("AI unavailable: {Error}", response.Error);
}
```

### Scenario 3: High Database Load

**Trigger**: Database response time exceeds threshold

**Degradation**:
- Feature: `StacCatalog`
- Strategy: Serve cached STAC items
- User Impact: Slightly stale metadata
- Recovery: Gradual as load decreases

**Headers Indicating Stale Data**:
```http
X-Service-Status: Degraded
X-Feature-Status: StacCatalog=Degraded
X-Data-Age: 300
X-Cache-Status: HIT
```

**Code Example**:
```csharp
var stacService = serviceProvider.GetRequiredService<AdaptiveStacService>();
var metadata = await stacService.GetDatasetMetadataAsync(
    datasetId,
    getStacFunc: (id, ct) => stacCatalog.GetItemAsync(id, ct),
    getBasicFunc: (id, ct) => metadataProvider.GetBasicAsync(id, ct)
);

if (metadata.IsCached)
{
    // Include warning in response
    response.Headers.Add("Warning", metadata.Warning);
}
```

### Scenario 4: Search Index Unavailable

**Trigger**: Search service down or index corrupted

**Degradation**:
- Feature: `Search`
- Strategy: Fall back to database full scan
- User Impact: Slower search performance
- Recovery: Automatic when index service returns

**Performance Warning**:
```http
X-Service-Status: Degraded
X-Feature-Status: Search=Degraded
Warning: 299 - "Search degraded to database scan - results may be slower"
```

**Code Example**:
```csharp
var searchService = serviceProvider.GetRequiredService<AdaptiveSearchService>();
var results = await searchService.SearchAsync(
    query,
    fullTextSearchFunc: (q, ct) => searchEngine.SearchAsync(q, ct),
    basicSearchFunc: (q, ct) => basicSearch.SearchAsync(q, ct),
    databaseScanFunc: (q, ct) => repository.ScanAsync(q, ct)
);

// Results include performance metadata
logger.LogInformation(
    "Search completed using {Strategy} in {Duration}ms",
    results.SearchStrategy,
    results.ExecutionTime.TotalMilliseconds
);
```

### Scenario 5: Multiple Features Degraded

**Trigger**: >30% of features unhealthy

**Degradation**:
- System-wide: Aggressive rate limiting
- User Impact: Reduced throughput to protect system stability
- Recovery: Rate limits gradually relax as features recover

**Rate Limit Calculation**:
```csharp
var adaptiveFeature = serviceProvider.GetRequiredService<AdaptiveFeatureService>();
var multiplier = await adaptiveFeature.GetRateLimitMultiplierAsync();
// multiplier = 0.25 if >50% degraded, 0.5 if >30%, 0.75 if >10%, 1.0 otherwise

var effectiveLimit = baseRateLimit * multiplier;
```

## Monitoring and Alerts

### Metrics

The system exposes Prometheus metrics for monitoring degradation:

```promql
# Number of degraded features
honua_degraded_features_count

# Number of unavailable features
honua_unavailable_features_count

# Feature health scores
honua_feature_health_score{feature="AdvancedCaching",state="Healthy"}

# Degradation events
honua_feature_degradation_events_total{feature="AdvancedCaching"}

# Recovery events
honua_feature_recovery_events_total{feature="AdvancedCaching"}
```

### Alert Rules

Example Prometheus alert rules:

```yaml
groups:
  - name: feature_degradation
    interval: 30s
    rules:
      - alert: FeatureDegradedTooLong
        expr: honua_degraded_features_count > 0
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Features degraded for >5 minutes"
          description: "{{ $value }} features are currently degraded"

      - alert: MultipleFeaturesDegraded
        expr: honua_degraded_features_count >= 3
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Multiple features degraded"
          description: "{{ $value }} features are degraded - system stability at risk"

      - alert: CriticalFeatureDown
        expr: honua_feature_health_score{feature="Authentication",state!="Healthy"} < 100
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Critical feature unhealthy"
          description: "Authentication is unhealthy - immediate action required"
```

### Grafana Dashboard

A Grafana dashboard is available to visualize degradation state:

- **Feature Health Panel**: Shows health score of all features over time
- **Degradation Events Panel**: Timeline of degradation and recovery events
- **Service Status Panel**: Overall system health status
- **Rate Limit Impact Panel**: Shows rate limit multiplier adjustments

Import the dashboard from: `docker/grafana/dashboards/degradation-monitoring.json`

## API Endpoints

### Get All Feature Statuses

```http
GET /api/admin/features/status
```

**Response**:
```json
{
  "timestamp": "2025-10-17T10:30:00Z",
  "features": [
    {
      "name": "AdvancedCaching",
      "isAvailable": true,
      "isDegraded": true,
      "state": "Degraded",
      "healthScore": 40,
      "degradationType": "Fallback",
      "degradationReason": "Redis connection failed",
      "stateChangedAt": "2025-10-17T10:25:00Z",
      "nextRecoveryCheck": "2025-10-17T10:31:00Z"
    }
  ]
}
```

### Get Specific Feature Status

```http
GET /api/admin/features/status/{featureName}
```

### Disable Feature Manually

```http
POST /api/admin/features/status/{featureName}/disable
Content-Type: application/json

{
  "reason": "Maintenance window"
}
```

### Enable Feature Manually

```http
POST /api/admin/features/status/{featureName}/enable
```

### Force Health Check

```http
POST /api/admin/features/status/{featureName}/check-health
```

### Get Active Degradations

```http
GET /api/admin/features/degradations
```

**Response**:
```json
{
  "timestamp": "2025-10-17T10:30:00Z",
  "totalFeatures": 10,
  "healthyFeatures": 7,
  "degradedFeatures": 2,
  "unavailableFeatures": 1,
  "activeDegradations": [
    {
      "name": "AdvancedCaching",
      "isAvailable": true,
      "isDegraded": true,
      "state": "Degraded",
      "healthScore": 40,
      "degradationType": "Fallback",
      "degradationReason": "Redis connection failed"
    }
  ]
}
```

## Testing

### Unit Tests

Test individual degradation strategies:

```csharp
[Fact]
public async Task GetCachingModeAsync_AdvancedCachingUnavailable_ReturnsInMemory()
{
    // Arrange
    _featureManagementMock
        .Setup(x => x.IsFeatureAvailableAsync("AdvancedCaching", default))
        .ReturnsAsync(false);

    var service = CreateService();

    // Act
    var mode = await service.GetCachingModeAsync();

    // Assert
    Assert.Equal(CachingMode.InMemory, mode);
}
```

### Integration Tests

Test end-to-end degradation scenarios:

```csharp
[Fact]
public async Task Scenario_RedisUnavailable_FallsBackToInMemoryCache()
{
    // Simulate Redis unavailable
    var cacheService = new AdaptiveCacheService(
        memoryCache,
        featureManagement,
        logger,
        distributedCache: null);

    await featureManagement.DisableFeatureAsync("AdvancedCaching", "Redis unavailable");

    // Should still work using in-memory cache
    await cacheService.SetAsync("test-key", testValue);
    var retrieved = await cacheService.GetAsync("test-key");

    Assert.NotNull(retrieved);
    Assert.Equal(testValue, retrieved);
}
```

### Manual Testing

1. **Test Redis Fallback**:
   ```bash
   # Stop Redis
   docker stop redis

   # Verify cache still works via in-memory
   curl http://localhost:5000/api/admin/features/status/AdvancedCaching

   # Restart Redis and verify recovery
   docker start redis
   sleep 60  # Wait for recovery check
   curl http://localhost:5000/api/admin/features/status/AdvancedCaching
   ```

2. **Test AI Degradation**:
   ```bash
   # Disable AI feature
   curl -X POST http://localhost:5000/api/admin/features/status/AIConsultant/disable \
     -H "Content-Type: application/json" \
     -d '{"reason": "Testing degradation"}'

   # Try AI operation - should get graceful error
   curl http://localhost:5000/api/ai/consultant/chat
   ```

3. **Test Rate Limit Adjustment**:
   ```bash
   # Degrade multiple features
   curl -X POST http://localhost:5000/api/admin/features/status/AdvancedCaching/disable
   curl -X POST http://localhost:5000/api/admin/features/status/Search/disable
   curl -X POST http://localhost:5000/api/admin/features/status/RealTimeMetrics/disable

   # Check degradation summary
   curl http://localhost:5000/api/admin/features/degradations

   # Verify rate limits are more aggressive
   # (make rapid requests and observe 429 responses)
   ```

## Best Practices

### 1. Choose Appropriate Degradation Strategies

- **Critical data services**: Use `ReduceQuality` (serve cached/stale data)
- **Performance features**: Use `Fallback` (simpler implementation)
- **Optional features**: Use `Disable` (with helpful messaging)

### 2. Set Realistic Health Thresholds

- **Authentication**: MinHealthScore = 80 (high availability required)
- **Caching**: MinHealthScore = 50 (can degrade gracefully)
- **Analytics**: MinHealthScore = 30 (not critical to operations)

### 3. Configure Appropriate Recovery Intervals

- **Critical features**: 30 seconds (quick recovery attempts)
- **Standard features**: 60 seconds (balanced)
- **Optional features**: 300 seconds (avoid unnecessary checks)

### 4. Monitor Degradation Duration

Alert when features are degraded for extended periods:
- Warning: >5 minutes
- Critical: >30 minutes

### 5. Test Degradation Scenarios Regularly

Include degradation testing in your CI/CD pipeline:
- Simulate Redis failures
- Test database slow queries
- Verify AI service fallbacks
- Confirm rate limit adjustments

### 6. Document User Impact

For each feature, document:
- What degrades
- User impact
- Expected recovery time
- Manual mitigation steps

### 7. Use Response Headers

Always include degradation information in responses:
```csharp
response.Headers.Add("X-Service-Status", "Degraded");
response.Headers.Add("X-Feature-Status", "AdvancedCaching=Degraded");
```

### 8. Provide Graceful Fallbacks

Always provide actionable alternatives:
```json
{
  "error": "AI features unavailable",
  "suggestedActions": [
    "Use manual configuration wizard",
    "Check documentation",
    "Contact support"
  ]
}
```

## Troubleshooting

### Feature Not Recovering

**Problem**: Feature stays degraded even after issue resolved

**Solution**:
1. Check health check implementation
2. Force health check via API: `POST /api/admin/features/status/{feature}/check-health`
3. Manually enable: `POST /api/admin/features/status/{feature}/enable`

### Excessive Degradation Events

**Problem**: Features flapping between healthy/degraded

**Solution**:
1. Increase `MinHealthScore` threshold
2. Increase `RecoveryCheckInterval`
3. Review health check implementation for stability

### Rate Limiting Too Aggressive

**Problem**: Users experiencing excessive 429 responses

**Solution**:
1. Check how many features are degraded
2. Adjust rate limit multiplier thresholds in `AdaptiveFeatureService`
3. Temporarily disable non-critical features to reduce degradation count

## Related Documentation

- [Health Checks](./HEALTH_CHECKS.md)
- [Monitoring and Observability](./observability/README.md)
- [Rate Limiting](./RATE_LIMITING.md)
- [API Documentation](./api/README.md)
