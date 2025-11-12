# Query Timeout Implementation

## Overview

This document describes the configurable query timeout feature implemented for Honua Server's FeatureRepository operations. The implementation provides hierarchical timeout configuration, comprehensive logging, metrics collection, and meaningful error messages for timeout scenarios.

## Implementation Summary

### Files Created

1. **`src/Honua.Server.Core/Configuration/QueryTimeoutOptions.cs`**
   - Configuration class for query timeout settings
   - Supports different timeout tiers for different operation types
   - Includes warning threshold configuration
   - Provides helper methods for timeout and warning threshold calculation

### Files Modified

1. **`src/Honua.Server.Core/Data/FeatureRepository.cs`**
   - Added QueryTimeoutOptions injection via IOptions<QueryTimeoutOptions>
   - Added IQueryMetrics injection for metrics collection (optional)
   - Implemented timeout enforcement for all query operations
   - Added comprehensive logging for slow queries and timeouts
   - Added metrics recording for query duration and timeout events

2. **`src/Honua.Server.Host/appsettings.json`**
   - Added QueryTimeout configuration section with default values
   - Includes comments explaining each timeout setting

## Configuration

### Default Timeout Values

The following default timeout values are configured in `appsettings.json`:

```json
{
  "QueryTimeout": {
    "DefaultTimeout": "00:00:30",              // 30 seconds
    "SimpleQueryTimeout": "00:00:15",          // 15 seconds
    "StatisticsTimeout": "00:01:00",           // 60 seconds
    "TileGenerationTimeout": "00:00:10",       // 10 seconds
    "ExtentCalculationTimeout": "00:00:30",    // 30 seconds
    "CountTimeout": "00:00:30",                // 30 seconds
    "DistinctTimeout": "00:00:30",             // 30 seconds
    "TimeoutWarningThreshold": 0.75,           // 75% of timeout
    "EnableDetailedLogging": true,
    "EnableMetrics": true
  }
}
```

### Customizing Timeouts

You can customize timeouts in environment-specific configuration files:

**For high-concurrency deployments** (`appsettings.Production.json`):
```json
{
  "QueryTimeout": {
    "SimpleQueryTimeout": "00:00:10",    // Reduce to 10 seconds
    "TileGenerationTimeout": "00:00:05", // Reduce to 5 seconds
    "StatisticsTimeout": "00:00:30"      // Reduce to 30 seconds
  }
}
```

**For large dataset deployments** (`appsettings.Production.json`):
```json
{
  "QueryTimeout": {
    "SimpleQueryTimeout": "00:00:30",    // Increase to 30 seconds
    "StatisticsTimeout": "00:02:00",     // Increase to 120 seconds
    "ExtentCalculationTimeout": "00:01:00" // Increase to 60 seconds
  }
}
```

## Operation Types and Default Timeouts

| Operation Type | Default Timeout | Description |
|---------------|----------------|-------------|
| **SimpleQuery** | 15 seconds | Feature queries (QueryAsync, GetAsync) - fast indexed lookups |
| **Statistics** | 60 seconds | Aggregation queries (SUM, AVG, MIN, MAX, COUNT with GROUP BY) |
| **TileGeneration** | 10 seconds | Vector tile generation (MVT) - time-sensitive for map panning |
| **ExtentCalculation** | 30 seconds | Bounding box computation using ST_Extent |
| **Count** | 30 seconds | Record counting with filters |
| **Distinct** | 30 seconds | Unique value retrieval for fields |

## Features Implemented

### 1. Timeout Enforcement

- Uses `CancellationTokenSource.CancelAfter()` to enforce timeouts
- Combines external cancellation tokens with internal timeout tokens
- Distinguishes between user-initiated cancellation and timeout expiration
- Throws `TimeoutException` with actionable error messages

### 2. Warning Threshold Logging

- Logs warnings when operations exceed a configurable percentage of the timeout (default: 75%)
- Example: A 30-second timeout will log a warning if the query takes longer than 22.5 seconds
- Helps identify operations approaching timeout before they fail
- Provides early visibility into performance degradation

### 3. Comprehensive Logging

**Debug Logging (when enabled):**
- Operation start with timeout configuration
- Successful completion with elapsed time

**Warning Logging:**
- Slow queries approaching timeout threshold
- Includes percentage of timeout consumed
- Tracks record count for streaming queries

**Error Logging:**
- Timeout exceptions with full context
- Failed operations with elapsed time
- Includes service ID, layer ID, and operation type

### 4. Metrics Collection

When `IQueryMetrics` is available and `EnableMetrics` is true:
- Records query duration for successful operations
- Records timeout events for failed operations
- Supports histogram-based metrics for duration tracking
- Counter-based metrics for timeout frequency

### 5. Per-Query Timeout Override

The `FeatureQuery` record already has a `CommandTimeout` property that allows per-query timeout overrides:

```csharp
var query = new FeatureQuery
{
    Limit = 1000,
    CommandTimeout = TimeSpan.FromMinutes(5) // Override default timeout
};

var count = await repository.CountAsync(serviceId, layerId, query);
```

If not specified, the configured timeout for the operation type is automatically applied.

## Error Messages

When a timeout occurs, a `TimeoutException` is thrown with an actionable message:

```
Statistics operation for service123/layer456 exceeded 60s timeout.
Consider optimizing the query, adding indexes, or increasing the timeout in configuration.
```

For streaming queries (SimpleQuery):
```
SimpleQuery operation for service123/layer456 exceeded 15s timeout after returning 1523 records.
Consider adding filters, reducing the result set, or increasing the timeout in configuration.
```

## Example Logging Output

### Successful Query (Debug)
```
[10:15:23 DBG] Starting Count for service123/layer456 with timeout 30s
[10:15:23 DBG] Count for service123/layer456 completed in 245ms
```

### Slow Query (Warning)
```
[10:15:23 DBG] Starting Statistics for service123/layer456 with timeout 60s
[10:15:58 WRN] Slow Statistics for service123/layer456 took 52341ms (87.2% of 60s timeout)
```

### Timeout (Error)
```
[10:15:23 DBG] Starting TileGeneration for service123/layer456 with timeout 10s (zoom=12, x=1024, y=768)
[10:15:33 ERR] Query timeout: TileGeneration for service123/layer456 exceeded 10s timeout after 10002ms (zoom=12, x=1024, y=768)
System.TimeoutException: TileGeneration operation for service123/layer456 exceeded 10s timeout.
```

## Metrics Integration

The implementation integrates with the optional `IQueryMetrics` interface for metrics collection:

```csharp
public interface IQueryMetrics
{
    void RecordQueryDuration(QueryOperationType operationType, string serviceId, string layerId, TimeSpan duration);
    void RecordQueryTimeout(QueryOperationType operationType, string serviceId, string layerId);
}
```

Metrics can be used to:
- Track query performance trends over time
- Identify layers with performance issues
- Monitor timeout frequency by operation type
- Create performance dashboards and alerts

## Best Practices

### 1. Monitoring Slow Queries

Monitor logs for warnings to identify operations approaching timeout:

```bash
# Filter logs for slow queries
grep "Slow" logs/honua-*.log | grep "Statistics"
```

### 2. Tuning Timeouts

Start with conservative timeouts and adjust based on observed performance:

1. Enable detailed logging in development/staging
2. Monitor actual query execution times
3. Set timeouts to 2-3x the p95 query duration
4. Use warning threshold (75%) to catch outliers before timeout

### 3. Optimizing Slow Operations

When timeouts occur frequently:

1. **Add database indexes:**
   - Spatial indexes on geometry columns (GIST for PostgreSQL)
   - Indexes on frequently filtered fields
   - Partial indexes for common filter patterns

2. **Optimize filters:**
   - Use spatial index-friendly predicates
   - Avoid full table scans with unindexed filters
   - Leverage CRS transformation at query time

3. **Reduce result sets:**
   - Apply pagination for large queries
   - Use appropriate LIMIT values
   - Consider pre-aggregated materialized views

4. **Check query plans:**
   ```sql
   -- PostgreSQL
   EXPLAIN ANALYZE SELECT ...;

   -- SQL Server
   SET STATISTICS TIME ON;
   SET STATISTICS IO ON;
   SELECT ...;
   ```

### 4. Environment-Specific Configuration

Use different timeout values per environment:

- **Development:** Longer timeouts for debugging (2-3x production)
- **Staging:** Production-like timeouts for realistic testing
- **Production:** Aggressive timeouts based on SLA requirements

## Migration Notes

### Backward Compatibility

The implementation maintains backward compatibility:

- `IQueryMetrics` is optional (null-safe)
- If `QueryTimeoutOptions` is not configured, defaults are used
- Existing code continues to work without configuration changes

### Registration Required

The `QueryTimeoutOptions` must be registered in the DI container:

```csharp
// In Program.cs or Startup.cs
services.Configure<QueryTimeoutOptions>(
    configuration.GetSection("QueryTimeout"));
```

This is typically done automatically via the configuration binding system when the section exists in `appsettings.json`.

## Performance Impact

### Overhead

The timeout implementation adds minimal overhead:

- **Stopwatch:** ~20-50 nanoseconds per operation
- **CancellationTokenSource:** ~100-200 nanoseconds
- **Logging:** Only when enabled, async logging reduces impact
- **Metrics:** Conditional recording, negligible overhead

### Benefits

- Prevents resource exhaustion from long-running queries
- Improves system responsiveness under load
- Provides visibility into query performance
- Enables data-driven optimization decisions

## Testing

### Unit Testing

Test timeout behavior with mock delays:

```csharp
[Fact]
public async Task CountAsync_ExceedsTimeout_ThrowsTimeoutException()
{
    // Arrange
    var options = new QueryTimeoutOptions { CountTimeout = TimeSpan.FromMilliseconds(100) };
    var repository = new FeatureRepository(contextResolver, logger, Options.Create(options));

    // Act & Assert
    await Assert.ThrowsAsync<TimeoutException>(() =>
        repository.CountAsync("slow-service", "slow-layer", null));
}
```

### Integration Testing

Test with real database operations:

```csharp
[Fact]
public async Task StatisticsAsync_ComplexQuery_CompletesWithinTimeout()
{
    // Arrange
    var options = new QueryTimeoutOptions { StatisticsTimeout = TimeSpan.FromSeconds(30) };
    var repository = new FeatureRepository(contextResolver, logger, Options.Create(options));

    // Act
    var stopwatch = Stopwatch.StartNew();
    var results = await repository.QueryStatisticsAsync(serviceId, layerId, statistics, groupBy, filter);
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
}
```

## Troubleshooting

### Issue: Frequent Timeouts

**Symptoms:** High timeout rate in metrics, error logs

**Solutions:**
1. Check database indexes exist and are being used (EXPLAIN ANALYZE)
2. Review filter complexity and optimize CQL expressions
3. Increase timeout temporarily while investigating root cause
4. Consider query result caching for expensive operations

### Issue: No Warning Logs

**Symptoms:** Timeouts occur without warning logs

**Solutions:**
1. Verify `EnableDetailedLogging: true` in configuration
2. Check log level allows Warning messages
3. Ensure `TimeoutWarningThreshold` is set appropriately (e.g., 0.75)

### Issue: Metrics Not Recorded

**Symptoms:** Query metrics missing from observability platform

**Solutions:**
1. Verify `EnableMetrics: true` in configuration
2. Ensure `IQueryMetrics` is registered in DI container
3. Check metrics backend connectivity
4. Verify metrics namespace/tags configuration

## Future Enhancements

Potential improvements for future versions:

1. **Adaptive Timeouts:** Automatically adjust timeouts based on historical query performance
2. **Per-Layer Timeouts:** Allow timeout configuration in layer metadata
3. **Circuit Breaker:** Fail fast when timeout rate exceeds threshold
4. **Timeout Histograms:** Track timeout distribution for capacity planning
5. **Retry Logic:** Automatic retry with exponential backoff for transient failures

## Related Documentation

- `PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md` - Performance analysis and recommendations
- `src/Honua.Server.Core/Data/README.md` - Data layer architecture
- `src/Honua.Server.Core/Configuration/` - Configuration system overview

## Support

For questions or issues related to query timeouts:

1. Check logs for timeout patterns and slow query warnings
2. Review database query plans with EXPLAIN ANALYZE
3. Consult performance optimization documentation
4. Open an issue with timeout details and query characteristics
