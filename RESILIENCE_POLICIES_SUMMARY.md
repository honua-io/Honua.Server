# Resilience Policies Implementation Summary

## Overview

Comprehensive resilience patterns have been implemented across cache, database, and background service operations using Polly v8. This ensures the system can gracefully handle transient failures and prevent cascading failures.

---

## 1. Redis Cache Operations - Circuit Breaker

### Implementation
- **File**: `/src/Honua.Server.Core/Caching/CacheCircuitBreaker.cs`
- **Wrapper**: `/src/Honua.Server.Core/Caching/ResilientCacheWrapper.cs`
- **Pattern**: Circuit Breaker with graceful degradation

### Configuration
```csharp
// Circuit breaker opens after 50% failure rate
FailureRatio = 0.5
MinimumThroughput = 10 operations
SamplingDuration = 30 seconds
BreakDuration = 30 seconds
```

### Behavior
- **Open State**: Cache operations return `null` instead of throwing exceptions
- **Half-Open State**: Tests if cache has recovered by allowing a single request
- **Closed State**: Normal operation resumes
- **Metrics**: Circuit state changes logged with structured logging

### Handles
- `CacheUnavailableException`
- `CacheWriteException`
- `TimeoutException`
- Connection/network errors

### Benefits
- **Fail-fast**: Prevents waiting for unavailable Redis
- **Automatic recovery**: Tests and restores service when Redis recovers
- **No cascading failures**: Cache failures don't bring down the application
- **Observability**: Circuit state changes emit metrics and logs

---

## 2. Database Operations - Retry Policy

### Implementation
- **File**: `/src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs`
- **Executor**: `/src/Honua.Server.Core/Resilience/ResilientDatabaseOperationExecutor.cs`
- **Updated**: `/src/Honua.Server.Core/Data/ResilientDataStoreProvider.cs`
- **Pattern**: Retry with exponential backoff + Bulkhead

### Configuration
```csharp
MaxRetryAttempts = 3
InitialDelay = 100ms
BackoffType = Exponential with Jitter
Timeout = 30 seconds per query
```

### Provider-Specific Implementations
Database retry policies include provider-specific transient error detection:

#### PostgreSQL (Npgsql)
- **Handled errors**:
  - Connection failures (08000, 08003, 08006, 08001)
  - Serialization failures / Deadlocks (40001, 40P01)
  - Resource exhaustion (53000, 53100, 53200, 53300)
  - Admin shutdown (57P01, 57P02, 57P03)
- **File**: Uses `NpgsqlException.IsTransient` + custom SqlState checks

#### MySQL (MySqlConnector)
- **Handled errors**:
  - Lock wait timeout (1205)
  - Deadlocks (1213)
  - Too many connections (1040)
  - Server shutdown (1053)
  - Connection errors (2002, 2003, 2006, 2013)
- **Not retried**: Constraint violations, syntax errors, missing tables

#### SQL Server (SqlClient)
- **Handled errors**:
  - Timeout expired (-2)
  - Connection broken (-1)
  - Deadlocks (1205)
  - Lock timeout (1222)
  - Resource limits (10928, 10929, 40197, 40501, 40613)
- **Azure SQL specific**: Handles service busy, database unavailable errors

#### SQLite (Microsoft.Data.Sqlite)
- **Handled errors**:
  - Database locked (5 - SQLITE_BUSY)
  - Table locked (6 - SQLITE_LOCKED)
  - Out of memory (7 - SQLITE_NOMEM)
  - Disk I/O error (10 - SQLITE_IOERR)
  - Database full (13 - SQLITE_FULL)
- **Single-user optimized**: Retry on lock contention

#### Oracle (Reflection-based)
- **Handled ORA codes**: 00060 (deadlock), 00054 (resource busy), 01555 (snapshot too old), TNS errors
- **Not retried**: Constraint violations (ORA-00001), missing objects (ORA-00942)

### Bulkhead Integration
All database operations also use bulkhead policy:
```csharp
DatabaseMaxParallelization = 50 (configurable)
DatabaseMaxQueuedActions = 100 (configurable)
```

### Benefits
- **Automatic recovery**: Transient errors are retried without manual intervention
- **Connection pool protection**: Bulkhead prevents exhaustion
- **Provider-aware**: Each database has specific transient error handling
- **No permanent error masking**: Only transient errors are retried
- **Metrics**: Retry attempts, successes, and exhaustions are tracked

### Applied To
✅ All database operations in `ResilientDataStoreProvider`:
- QueryAsync
- CountAsync
- GetAsync / CreateAsync / UpdateAsync / DeleteAsync
- SoftDeleteAsync / RestoreAsync / HardDeleteAsync
- BulkInsertAsync / BulkUpdateAsync / BulkDeleteAsync
- GenerateMvtTileAsync
- QueryStatisticsAsync / QueryDistinctAsync / QueryExtentAsync
- BeginTransactionAsync
- TestConnectivityAsync

---

## 3. Background Service Failures - Retry with Exponential Backoff

### Implementation
- **File**: `/src/Honua.Server.Core/Resilience/BackgroundServiceRetryHelper.cs`
- **Pattern**: Unlimited retry with exponential backoff and jitter

### Configuration
```csharp
MaxRetryAttempts = int.MaxValue (unlimited for background services)
InitialDelay = 1 second
MaxDelay = 5 minutes
BackoffType = Exponential with Jitter
```

### Retry Delays
- Attempt 1: ~1s
- Attempt 2: ~2s
- Attempt 3: ~4s
- Attempt 4: ~8s
- Attempt 5: ~16s
- Attempt 6: ~32s
- Attempt 7: ~64s
- Attempt 8+: ~5 minutes (capped)

### Usage Example
```csharp
public class MyBackgroundService : BackgroundService
{
    private readonly BackgroundServiceRetryHelper _retryHelper;

    public MyBackgroundService(ILogger<MyBackgroundService> logger)
    {
        _retryHelper = new BackgroundServiceRetryHelper(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One-time operation with retry
        await _retryHelper.ExecuteAsync(
            DoWorkAsync,
            "ProcessInitialData",
            stoppingToken);

        // Periodic operation (polling loop)
        await _retryHelper.ExecutePeriodicAsync(
            ProcessQueueAsync,
            TimeSpan.FromMinutes(5),
            "ProcessQueue",
            stoppingToken);
    }
}
```

### Benefits
- **Never gives up**: Background services retry indefinitely until successful or cancelled
- **Exponential backoff**: Reduces load during outages
- **Jitter**: Prevents thundering herd when multiple instances restart
- **Cancellation-aware**: Stops retrying when service is shutting down
- **Metrics**: Retry attempts and delays are tracked

### Applicable To
- Data ingestion services
- Cache warming services
- Synchronization services
- Queue processors
- Scheduled jobs
- Health monitors

---

## 4. Metrics and Observability

### Implementation
- **File**: `/src/Honua.Server.Core/Observability/ResilienceMetrics.cs`
- **Registration**: Auto-registered in `AddHonuaResiliencePolicies()`

### Metrics Exposed

#### Cache Circuit Breaker
```
cache_circuit_breaker_open_total
cache_circuit_breaker_closed_total
cache_circuit_breaker_halfopen_total
cache_operation_success_total
cache_operation_failure_total
cache_operation_duration_seconds (histogram)
```

#### Database Retries
```
database_retry_attempt_total
database_retry_success_total
database_retry_exhausted_total
database_operation_duration_seconds (histogram)
database_transient_error_total
```

#### Background Services
```
background_service_retry_total
background_service_failure_total
background_service_success_total
background_service_retry_delay_seconds (histogram)
```

### Logging
All resilience operations emit structured logs:
- Circuit state changes (Warning/Error/Information)
- Retry attempts (Warning with attempt number and delay)
- Exhausted retries (Error)
- Background service failures (Error with exception details)

---

## 5. Service Registration

### In ServiceCollectionExtensions.cs

```csharp
services.AddHonuaCore(configuration, basePath)
    // ... automatically includes resilience policies
```

The `AddHonuaResiliencePolicies()` method is automatically called and registers:

1. **ResilienceMetrics** - Singleton metrics collector
2. **Database Retry Pipeline** - Uses `ResiliencePolicies.CreateDatabasePolicy()`
3. **ResilientDatabaseOperationExecutor** - Combines retry + bulkhead
4. **ResilientCacheWrapper** - Decorates IDistributedCache with circuit breaker

### Decorator Pattern
The cache wrapper uses the decorator pattern to transparently add resilience:
```csharp
services.Decorate<IDistributedCache>((inner, sp) =>
    new ResilientCacheWrapper(inner, logger, "DistributedCache"));
```

---

## 6. Configuration

### appsettings.json

```json
{
  "Resilience": {
    "CircuitBreaker": {
      "FailureThreshold": 0.5,
      "MinimumThroughput": 10,
      "DurationOfBreak": "00:00:30"
    },
    "Bulkhead": {
      "DatabaseEnabled": true,
      "DatabaseMaxParallelization": 50,
      "DatabaseMaxQueuedActions": 100,
      "ExternalApiEnabled": true,
      "ExternalApiMaxParallelization": 20,
      "ExternalApiMaxQueuedActions": 50
    }
  },
  "CacheInvalidation": {
    "RetryCount": 3,
    "InitialRetryDelay": "00:00:00.500",
    "OperationTimeout": "00:00:05"
  }
}
```

### Sensible Defaults
All resilience policies have sensible defaults and don't require configuration to work:
- Circuit breaker: 50% failure rate, 30s break
- Database retry: 3 attempts, 100ms initial delay
- Background service: Unlimited retries, 1s-5min exponential backoff
- Bulkhead: 50 concurrent database operations

---

## 7. Design Principles

### Don't Mask Permanent Failures
- Only transient errors are retried
- Permanent errors (constraint violations, syntax errors) fail immediately
- Circuit breaker fails fast when service is down

### Proper Timeout Values
- Cache operations: 5 seconds
- Database operations: 30 seconds per query
- Background services: No timeout (retry indefinitely)

### Metrics Over Silence
- All resilience events emit metrics
- Structured logging for troubleshooting
- Circuit state changes are observable

### Graceful Degradation
- Cache failures return null instead of throwing
- Database retries are transparent to callers
- Background services never crash the application

---

## 8. Testing Resilience

### Manual Testing

#### Test Cache Circuit Breaker
1. Stop Redis: `docker stop redis`
2. Make 10+ requests (triggers circuit breaker)
3. Check logs for "Circuit breaker OPENED"
4. Verify application still works (degraded)
5. Restart Redis: `docker start redis`
6. Wait 30 seconds, check logs for "Circuit breaker CLOSED"

#### Test Database Retry
1. Cause transient error (e.g., kill database connection mid-query)
2. Check logs for "Retrying database operation (attempt X)"
3. Verify operation eventually succeeds

#### Test Background Service Retry
1. Inject failure in background service
2. Check logs for exponential backoff: 1s, 2s, 4s, 8s...
3. Verify service keeps retrying

### Metrics Monitoring
Use Prometheus/Grafana to monitor:
- `cache_circuit_breaker_open_total` - Alert if circuit opens
- `database_retry_exhausted_total` - Alert if retries exhausted
- `database_operation_duration_seconds` - Track query performance
- `background_service_failure_total` - Monitor background job health

---

## 9. Migration Notes

### Existing Code
Existing code benefits automatically:
- All `IDataStoreProvider` usage now has retry + bulkhead
- All `IDistributedCache` usage now has circuit breaker
- No code changes required

### New Background Services
Use `BackgroundServiceRetryHelper`:
```csharp
services.AddHostedService<MyNewBackgroundService>();

public class MyNewBackgroundService : BackgroundService
{
    private readonly BackgroundServiceRetryHelper _retryHelper;

    public MyNewBackgroundService(ILogger<MyNewBackgroundService> logger)
    {
        _retryHelper = new BackgroundServiceRetryHelper(
            logger,
            maxRetries: int.MaxValue,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromMinutes(5));
    }

    protected override Task ExecuteAsync(CancellationToken ct) =>
        _retryHelper.ExecutePeriodicAsync(
            DoWorkAsync,
            interval: TimeSpan.FromMinutes(5),
            "MyOperation",
            ct);
}
```

---

## 10. Summary

| Component | Pattern | Retry Count | Timeout | Fail Behavior |
|-----------|---------|-------------|---------|---------------|
| **Redis Cache** | Circuit Breaker | N/A | 5s | Return null |
| **PostgreSQL** | Retry + Bulkhead | 3 | 30s | Throw after exhaustion |
| **MySQL** | Retry + Bulkhead | 3 | 30s | Throw after exhaustion |
| **SQL Server** | Retry + Bulkhead | 3 | 30s | Throw after exhaustion |
| **SQLite** | Retry + Bulkhead | 3 | 30s | Throw after exhaustion |
| **Background Services** | Exponential Backoff | Unlimited | None | Keep retrying |

### Key Files Created/Modified

**Created:**
- `/src/Honua.Server.Core/Resilience/ResilientDatabaseOperationExecutor.cs`
- `/src/Honua.Server.Core/Resilience/BackgroundServiceRetryHelper.cs`
- `/src/Honua.Server.Core/Observability/ResilienceMetrics.cs`

**Modified:**
- `/src/Honua.Server.Core/Data/ResilientDataStoreProvider.cs` - Now uses retry + bulkhead
- `/src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs` - Added `AddHonuaResiliencePolicies()`

**Existing (Already Present):**
- `/src/Honua.Server.Core/Caching/CacheCircuitBreaker.cs`
- `/src/Honua.Server.Core/Caching/ResilientCacheWrapper.cs`
- `/src/Honua.Server.Core/Caching/Resilience/CacheInvalidationRetryPolicy.cs`
- `/src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs`
- `/src/Honua.Server.Core/Resilience/ResiliencePolicies.cs`

### Total Resilience Coverage

✅ **HTTP Clients**: Hedging policy (already existed)
✅ **Redis Cache**: Circuit breaker with graceful degradation
✅ **Database Operations**: Retry with exponential backoff + bulkhead
✅ **Background Services**: Unlimited retry with exponential backoff
✅ **Metrics**: Comprehensive observability for all resilience patterns

---

## 11. References

- **Polly Documentation**: https://www.pollydocs.org/
- **Circuit Breaker Pattern**: https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker
- **Retry Pattern**: https://learn.microsoft.com/en-us/azure/architecture/patterns/retry
- **Bulkhead Pattern**: https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead
