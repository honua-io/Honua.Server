# Cache Consistency Operations Guide

## Overview

This guide provides operational procedures for managing cache consistency in Honua Server, troubleshooting cache-database drift, and responding to cache invalidation failures.

## Architecture

### Cache Invalidation Resilience

The system implements a robust cache invalidation mechanism with the following components:

1. **Retry Policy**: Exponential backoff with configurable retry attempts
2. **Circuit Breaker**: Prevents cascading failures when cache is unhealthy
3. **Metrics Tracking**: Monitors invalidation success/failure rates
4. **Health Checks**: Detects cache-database drift

### Consistency Strategies

Three strategies are available:

#### Strict (Default)
- **Behavior**: Operations fail if cache invalidation fails
- **Guarantee**: Strong consistency - cache never serves stale data
- **Use case**: Critical data where stale cache is unacceptable
- **Trade-off**: Reduced availability during cache outages

#### Eventual
- **Behavior**: Operations succeed even if cache invalidation fails
- **Guarantee**: Eventual consistency - cache will be correct after TTL expires
- **Use case**: High availability scenarios where brief inconsistency is acceptable
- **Trade-off**: May serve stale data temporarily

#### ShortTTL
- **Behavior**: Sets short TTL on entries that failed to invalidate
- **Guarantee**: Reduced stale data window (default: 30 seconds)
- **Use case**: Middle ground between Strict and Eventual
- **Trade-off**: Slightly higher cache miss rate

## Configuration

### Default Configuration

```json
{
  "CacheInvalidation": {
    "RetryCount": 3,
    "RetryDelayMs": 100,
    "MaxRetryDelayMs": 5000,
    "Strategy": "Strict",
    "HealthCheckSampleSize": 100,
    "MaxDriftPercentage": 1.0,
    "ShortTtl": "00:00:30",
    "EnableDetailedLogging": true,
    "EnableMetrics": true,
    "OperationTimeout": "00:00:10"
  }
}
```

### Configuration Parameters

| Parameter | Description | Default | Recommended Range |
|-----------|-------------|---------|-------------------|
| `RetryCount` | Number of retry attempts | 3 | 1-5 |
| `RetryDelayMs` | Initial retry delay | 100ms | 50-500ms |
| `MaxRetryDelayMs` | Maximum retry delay (cap for exponential backoff) | 5000ms | 1000-10000ms |
| `Strategy` | Consistency strategy | Strict | Strict, Eventual, ShortTTL |
| `HealthCheckSampleSize` | Number of entries to sample for drift detection | 100 | 50-500 |
| `MaxDriftPercentage` | Acceptable drift threshold | 1.0% | 0.1-5.0% |
| `ShortTtl` | TTL for ShortTTL strategy | 30s | 10-300s |
| `OperationTimeout` | Timeout for invalidation operations | 10s | 5-30s |

## Monitoring

### Metrics

The following OpenTelemetry metrics are available:

1. **honua.metadata.cache.invalidation.successes**
   - Counter of successful invalidations
   - Use to track overall health

2. **honua.metadata.cache.invalidation.failures**
   - Counter of failed invalidations
   - Alert if rate exceeds threshold

3. **honua.metadata.cache.invalidation.retries**
   - Counter of retry attempts
   - Indicates transient issues

4. **honua.metadata.cache.operation.duration**
   - Histogram of operation durations
   - Tag: `operation=invalidate`

### Health Checks

#### Cache Consistency Check

- **Endpoint**: `/health/ready` (tag: `cache`)
- **Frequency**: Per health check poll
- **Method**: Samples random entries and compares cache vs database
- **States**:
  - **Healthy**: Cache is consistent
  - **Degraded**: Drift detected but within threshold
  - **Unhealthy**: Drift exceeds threshold or check failed

#### Check Health Manually

```bash
curl http://localhost:5000/health/ready | jq '.entries.cache_consistency'
```

Example response:
```json
{
  "status": "Healthy",
  "description": "Cache is consistent with database",
  "data": {
    "cacheEnabled": true,
    "cachePopulated": true,
    "sampledEntries": 100,
    "inconsistentSamples": 0,
    "driftPercentage": 0.0,
    "checkDurationMs": 45
  }
}
```

## Troubleshooting

### Scenario 1: Cache Invalidation Failures

**Symptoms**:
- Logs show "CRITICAL: Cache invalidation failed"
- Metadata updates return 500 errors
- Metrics show `honua.metadata.cache.invalidation.failures` increasing

**Diagnosis**:
```bash
# Check Redis connectivity
redis-cli -h <redis-host> ping

# Check cache metrics
curl http://localhost:5000/metrics | grep cache_invalidation

# Review recent logs
tail -n 100 logs/honua-*.log | grep -i "cache invalidation"
```

**Resolution**:

1. **Immediate**: Switch to Eventual strategy to restore service
   ```json
   {
     "CacheInvalidation": {
       "Strategy": "Eventual"
     }
   }
   ```

2. **Root Cause**: Investigate Redis/cache issues
   - Check Redis server health and logs
   - Verify network connectivity
   - Check Redis memory usage

3. **Long-term**: Fix underlying issue and switch back to Strict
   ```json
   {
     "CacheInvalidation": {
       "Strategy": "Strict"
     }
   }
   ```

### Scenario 2: Cache-Database Drift

**Symptoms**:
- Health check reports "Degraded" or "Unhealthy"
- Users report seeing stale data
- `driftPercentage` > `MaxDriftPercentage`

**Diagnosis**:
```bash
# Check health status
curl http://localhost:5000/health/ready | jq '.entries.cache_consistency'

# Check invalidation metrics
curl http://localhost:5000/metrics | grep cache_invalidation
```

**Resolution**:

1. **Immediate**: Force cache refresh
   ```bash
   curl -X POST http://localhost:5000/admin/metadata/reload \
     -H "Authorization: Bearer <admin-token>"
   ```

2. **If reload fails**: Clear cache manually
   ```bash
   redis-cli -h <redis-host> DEL "Honua_honua:metadata:snapshot:v1"
   ```

3. **Warm cache**: Restart service or trigger reload again

4. **Prevent recurrence**: Review invalidation strategy and retry settings

### Scenario 3: High Cache Miss Rate

**Symptoms**:
- `honua.metadata.cache.misses` high relative to `honua.metadata.cache.hits`
- Slow metadata API responses
- High database load

**Diagnosis**:
```bash
# Check cache hit rate
curl http://localhost:5000/metrics | grep cache | grep -E "(hits|misses)"

# Check if cache is populated
redis-cli -h <redis-host> GET "Honua_honua:metadata:snapshot:v1"
```

**Resolution**:

1. **If cache is empty**: Check for invalidation failures or TTL issues
   - Review `MetadataCacheOptions.Ttl` setting
   - Check if ShortTTL strategy is causing rapid expiration

2. **If cache is populated**: Check for excessive invalidations
   - Review metadata update frequency
   - Consider increasing retry count if transient failures are common

### Scenario 4: Metadata Update Fails with Cache Error

**Symptoms**:
- POST `/admin/metadata/apply` returns 500
- Error message: "Metadata applied but cache invalidation failed"
- Database has new metadata but cache serves old data

**Diagnosis**:
```bash
# Check the error response
curl -X POST http://localhost:5000/admin/metadata/apply \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d @metadata.json

# Response (example):
# {
#   "error": "Metadata applied but cache invalidation failed",
#   "cacheName": "metadata",
#   "cacheKey": "honua:metadata:snapshot:v1",
#   "recommendation": "..."
# }
```

**Resolution**:

1. **Verify metadata was applied**: Check database/file system
   ```bash
   cat /path/to/metadata.json
   ```

2. **Clear cache manually**: Force cache refresh
   ```bash
   redis-cli -h <redis-host> DEL "Honua_honua:metadata:snapshot:v1"
   curl -X POST http://localhost:5000/admin/metadata/reload
   ```

3. **Switch to Eventual strategy** if this is recurring

## Operational Procedures

### Changing Consistency Strategy

1. **Update configuration**:
   ```json
   {
     "CacheInvalidation": {
       "Strategy": "Eventual"  // or "Strict", "ShortTTL"
     }
   }
   ```

2. **Reload configuration** (no restart needed - hot reload supported):
   ```bash
   # Configuration is reloaded automatically
   # Verify in logs:
   tail -f logs/honua-*.log | grep "Metadata cache configuration reloaded"
   ```

3. **Verify new strategy**:
   ```bash
   curl http://localhost:5000/health/ready | jq '.entries.cache_consistency.data.strategy'
   ```

### Manual Cache Invalidation

1. **Using admin API**:
   ```bash
   curl -X POST http://localhost:5000/admin/metadata/reload \
     -H "Authorization: Bearer <admin-token>"
   ```

2. **Using Redis CLI**:
   ```bash
   redis-cli -h <redis-host> DEL "Honua_honua:metadata:snapshot:v1"
   ```

3. **Verify invalidation**:
   ```bash
   redis-cli -h <redis-host> EXISTS "Honua_honua:metadata:snapshot:v1"
   # Should return 0 if invalidated
   ```

### Tuning Retry Parameters

For environments with flaky Redis connections:

```json
{
  "CacheInvalidation": {
    "RetryCount": 5,
    "RetryDelayMs": 200,
    "MaxRetryDelayMs": 10000,
    "OperationTimeout": "00:00:30"
  }
}
```

For low-latency environments:

```json
{
  "CacheInvalidation": {
    "RetryCount": 2,
    "RetryDelayMs": 50,
    "MaxRetryDelayMs": 1000,
    "OperationTimeout": "00:00:05"
  }
}
```

## Alerting Recommendations

### Critical Alerts

1. **Cache Invalidation Failure Rate > 1%**
   ```promql
   rate(honua_metadata_cache_invalidation_failures[5m]) /
   rate(honua_metadata_cache_invalidation_successes[5m]) > 0.01
   ```

2. **Cache Drift > Threshold**
   ```promql
   honua_cache_consistency_drift_percentage > 1.0
   ```

3. **Cache Invalidation Timeout**
   ```promql
   rate(honua_metadata_cache_invalidation_failures{error_type="timeout"}[5m]) > 0
   ```

### Warning Alerts

1. **High Retry Rate**
   ```promql
   rate(honua_metadata_cache_invalidation_retries[5m]) > 5
   ```

2. **Degraded Health Check**
   ```promql
   health_check_status{check="cache_consistency"} == 2  # Degraded
   ```

## Best Practices

1. **Use Strict strategy by default** for critical production environments
2. **Monitor cache metrics continuously** - set up dashboards and alerts
3. **Test cache failure scenarios** in staging before production
4. **Document your strategy choice** in operational runbooks
5. **Review cache metrics regularly** (weekly) to identify trends
6. **Keep retry timeouts reasonable** - balance between resilience and response time
7. **Use health checks** in load balancer configuration
8. **Plan for cache failures** - have runbooks ready for common scenarios

## See Also

- [Cache Configuration Reference](../configuration/cache.md)
- [Metrics Reference](../observability/metrics.md)
- [Health Check Reference](../observability/health-checks.md)
