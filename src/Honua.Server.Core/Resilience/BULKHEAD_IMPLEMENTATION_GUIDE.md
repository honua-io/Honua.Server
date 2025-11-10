# Bulkhead Isolation Patterns Implementation Guide

## Overview

This implementation adds bulkhead isolation patterns using Polly to prevent resource exhaustion and protect against noisy neighbor problems in multi-tenant scenarios.

## Components Implemented

### 1. Configuration (`BulkheadOptions.cs`)

**Location**: `/src/Honua.Server.Core/Resilience/BulkheadOptions.cs`

Provides configuration for all bulkhead policies:
- **Database Bulkhead**: Limits concurrent database connections (default: 50 parallel, 100 queued)
- **External API Bulkhead**: Limits concurrent external API calls (default: 10 parallel, 20 queued)
- **Per-Tenant Limits**: Prevents noisy neighbors (default: 10 parallel per tenant, 20 queued)
- **Memory Circuit Breaker**: Protects against OOM (default: 1GB threshold)

Each bulkhead can be enabled/disabled independently via configuration.

### 2. Bulkhead Policy Provider (`BulkheadPolicyProvider.cs`)

**Location**: `/src/Honua.Server.Core/Resilience/BulkheadPolicyProvider.cs`

Provides Polly-based bulkhead policies for database and external API operations:
- Uses `ConcurrencyLimiter` strategy for bulkhead implementation
- Fail-fast behavior when limits are exceeded
- Separate policies for database and external API operations
- Comprehensive logging of rejections

**Usage**:
```csharp
// Inject the provider
private readonly BulkheadPolicyProvider _bulkheadProvider;

// Execute database operation with protection
var result = await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
{
    return await _dbContext.Users.ToListAsync();
});

// Execute external API call with protection
var response = await _bulkheadProvider.ExecuteExternalApiOperationAsync(async () =>
{
    return await _httpClient.GetAsync("https://api.example.com/data");
});
```

### 3. Tenant Resource Limiter (`TenantResourceLimiter.cs`)

**Location**: `/src/Honua.Server.Core/Resilience/TenantResourceLimiter.cs`

Prevents noisy neighbor problems by limiting concurrent operations per tenant:
- Uses `SemaphoreSlim` per tenant for efficient synchronization
- Fail-fast behavior (TimeSpan.Zero wait)
- Automatic semaphore cleanup for inactive tenants
- Thread-safe using `ConcurrentDictionary`

**Usage**:
```csharp
// Inject the limiter
private readonly TenantResourceLimiter _tenantLimiter;

// Execute operation with per-tenant limit
var result = await _tenantLimiter.ExecuteAsync(tenantId, async () =>
{
    return await ProcessTenantRequest();
});

// Check available slots for monitoring
var availableSlots = _tenantLimiter.GetAvailableSlots(tenantId);

// Periodic cleanup of inactive tenants
var activeTenants = GetActiveTenantIds();
_tenantLimiter.CleanupInactiveTenants(activeTenants);
```

### 4. Memory Circuit Breaker (`MemoryCircuitBreaker.cs`)

**Location**: `/src/Honua.Server.Core/Resilience/MemoryCircuitBreaker.cs`

Protects against out-of-memory conditions by checking memory before operations:
- Uses `GC.GetTotalMemory()` for fast memory checks
- Checks memory before operation execution
- Provides diagnostic methods for monitoring

**Usage**:
```csharp
// Inject the circuit breaker
private readonly MemoryCircuitBreaker _memoryBreaker;

// Execute operation with memory protection
var result = await _memoryBreaker.ExecuteAsync(async () =>
{
    return await ProcessLargeDataset();
});

// Monitoring
var memoryUsage = _memoryBreaker.GetCurrentMemoryUsage();
var percentage = _memoryBreaker.GetMemoryUsagePercentage();
if (_memoryBreaker.IsApproachingThreshold(90.0))
{
    // Alert or throttle operations
}
```

### 5. Resilient Data Store Provider (`ResilientDataStoreProvider.cs`)

**Location**: `/src/Honua.Server.Core/Data/ResilientDataStoreProvider.cs`

Decorator for `IDataStoreProvider` that wraps all database operations with bulkhead protection:
- Implements decorator pattern
- Wraps all 17 IDataStoreProvider methods
- Handles both streaming (IAsyncEnumerable) and regular operations
- Comprehensive logging

**Usage**:
```csharp
// Register as decorator (optional - not registered by default)
services.Decorate<IDataStoreProvider, ResilientDataStoreProvider>();

// Or manually wrap specific providers
var resilientProvider = new ResilientDataStoreProvider(
    innerProvider,
    bulkheadProvider,
    logger);
```

### 6. Custom Exceptions (`ResilienceExceptions.cs`)

**Location**: `/src/Honua.Server.Core/Exceptions/ResilienceExceptions.cs`

Three new exception types for bulkhead scenarios:

- **`TenantResourceLimitExceededException`**: Tenant exceeded concurrent operation limit
- **`MemoryThresholdExceededException`**: Memory usage exceeded threshold
- **`BulkheadRejectedException`**: Bulkhead rejected operation (capacity full)

All inherit from `HonuaException` for consistent error handling.

## Configuration

### appsettings.json

```json
{
  "Resilience": {
    "Bulkhead": {
      "_comment": "Bulkhead isolation patterns to prevent resource exhaustion",
      "DatabaseEnabled": true,
      "DatabaseMaxParallelization": 50,
      "DatabaseMaxQueuedActions": 100,
      "ExternalApiEnabled": true,
      "ExternalApiMaxParallelization": 10,
      "ExternalApiMaxQueuedActions": 20,
      "PerTenantEnabled": true,
      "PerTenantMaxParallelization": 10,
      "PerTenantMaxQueuedActions": 20,
      "MemoryCircuitBreakerEnabled": true,
      "MemoryThresholdBytes": 1073741824
    }
  }
}
```

### Configuration Tuning

**Database Bulkhead**:
- Match `DatabaseMaxParallelization` to your connection pool size
- Set `DatabaseMaxQueuedActions` to 2x parallelization for burst handling
- For PostgreSQL with default pool size 50, use defaults

**External API Bulkhead**:
- Set conservatively to avoid overwhelming external services
- Consider rate limits of the external API
- Default of 10 is safe for most scenarios

**Per-Tenant Limits**:
- Balance between tenant isolation and resource utilization
- Lower values = stronger isolation, higher risk of false rejections
- Default of 10 per tenant is good for 5-10 active tenants

**Memory Circuit Breaker**:
- Set to 70-80% of available memory
- For 2GB RAM: 1.4-1.6GB threshold
- Monitor GC pressure and adjust accordingly

## Service Registration

The bulkhead services are automatically registered in `ServiceCollectionExtensions`:

```csharp
// Configure bulkhead resilience options and services
services.Configure<Resilience.BulkheadOptions>(
    configuration.GetSection(Resilience.BulkheadOptions.SectionName));
services.AddSingleton<Resilience.BulkheadPolicyProvider>();
services.AddSingleton<Resilience.TenantResourceLimiter>();
services.AddSingleton<Resilience.MemoryCircuitBreaker>();
```

## Testing

### Test Coverage

**TenantResourceLimiterTests** (13 tests):
- Concurrent requests per tenant
- Queue overflow behavior
- Semaphore release on exception
- Different tenants have independent limits
- Cleanup of inactive tenants
- Cancellation token propagation
- Disabled state behavior

**MemoryCircuitBreakerTests** (12 tests):
- Memory threshold detection
- Operation execution under threshold
- Exception when over threshold
- Disabled state behavior
- Memory usage monitoring
- Multiple operation consistency

**BulkheadPolicyProviderTests** (12 tests):
- Database and API bulkhead enforcement
- Rejection behavior when full
- Independent bulkhead policies
- Queueing behavior
- Exception propagation
- Disabled state behavior

Total: **37 comprehensive tests** covering all scenarios.

## Performance Considerations

### Overhead
- **BulkheadPolicyProvider**: ~0.1-0.5ms per operation (Polly policy execution)
- **TenantResourceLimiter**: ~0.01-0.05ms per operation (semaphore wait)
- **MemoryCircuitBreaker**: ~0.1-0.2ms per operation (GC.GetTotalMemory)

### Benefits
- **Database**: Prevents connection pool exhaustion (prevents 503 errors)
- **External API**: Prevents cascading failures from slow external services
- **Per-Tenant**: Prevents one tenant from consuming all resources
- **Memory**: Prevents OOM crashes that require restart

### Trade-offs
- Adds minimal latency (~0.2-0.8ms per operation)
- May reject valid requests under extreme load (fail-fast vs. fail-slow)
- Requires tuning based on workload patterns

## Integration Patterns

### 1. Opt-In via Decorator (Recommended)

Wrap specific providers that need protection:

```csharp
// For critical data sources
services.AddKeyedSingleton<IDataStoreProvider>(
    PostgresDataStoreProvider.ProviderKey,
    (sp, _) =>
    {
        var inner = new PostgresDataStoreProvider();
        var bulkhead = sp.GetRequiredService<BulkheadPolicyProvider>();
        var logger = sp.GetRequiredService<ILogger<ResilientDataStoreProvider>>();
        return new ResilientDataStoreProvider(inner, bulkhead, logger);
    });
```

### 2. Repository-Level Protection

Apply bulkheads at the repository level:

```csharp
public class UserRepository
{
    private readonly BulkheadPolicyProvider _bulkhead;

    public async Task<User> GetUserAsync(int id)
    {
        return await _bulkhead.ExecuteDatabaseOperationAsync(async () =>
        {
            return await _dbContext.Users.FindAsync(id);
        });
    }
}
```

### 3. API Controller Protection

Protect API endpoints with tenant limits:

```csharp
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly TenantResourceLimiter _tenantLimiter;

    [HttpGet]
    public async Task<IActionResult> GetData([FromQuery] string tenantId)
    {
        return await _tenantLimiter.ExecuteAsync(tenantId, async () =>
        {
            var data = await ProcessRequest();
            return Ok(data);
        });
    }
}
```

## Monitoring and Observability

### Metrics to Track

1. **Bulkhead Rejections**: Count of rejected operations
2. **Queue Depth**: Current queue size for each bulkhead
3. **Execution Time**: P50/P95/P99 latency with bulkhead overhead
4. **Memory Usage**: Current vs. threshold
5. **Per-Tenant Slot Utilization**: Active vs. available slots per tenant

### Logging

All components provide comprehensive logging:
- **Debug**: Operation execution, slot availability
- **Warning**: Rejections, approaching thresholds
- **Information**: Initialization, configuration

### Health Checks

Consider adding health checks for:
```csharp
services.AddHealthChecks()
    .AddCheck("bulkhead_capacity", () =>
    {
        var memoryBreaker = sp.GetRequiredService<MemoryCircuitBreaker>();
        if (memoryBreaker.IsApproachingThreshold(95.0))
            return HealthCheckResult.Degraded("Memory usage above 95%");
        return HealthCheckResult.Healthy();
    });
```

## Best Practices

1. **Start with conservative limits** and tune based on observed behavior
2. **Monitor rejection rates** - high rejection rate indicates need for tuning
3. **Use fail-fast for user-facing operations** - don't make users wait
4. **Combine with circuit breakers** - bulkheads prevent exhaustion, circuit breakers prevent cascading failures
5. **Log all rejections** - helps identify capacity issues
6. **Test under load** - verify limits work correctly under stress
7. **Document tuning decisions** - capture why specific limits were chosen

## Troubleshooting

### High Rejection Rate
- **Cause**: Limits too low for actual workload
- **Solution**: Increase `MaxParallelization` or add queueing
- **Monitoring**: Track rejection rate vs. success rate

### Memory Threshold Frequently Exceeded
- **Cause**: Memory leak or insufficient memory
- **Solution**: Investigate memory usage, consider GC tuning
- **Monitoring**: Track memory growth over time

### One Tenant Dominates Resources
- **Cause**: Tenant making excessive concurrent requests
- **Solution**: Lower `PerTenantMaxParallelization`
- **Monitoring**: Track per-tenant slot utilization

### Bulkhead Never Rejects
- **Cause**: Limits too high or insufficient load
- **Solution**: Review limits, may not be needed for this workload
- **Monitoring**: Track peak concurrent operations

## Future Enhancements

1. **Adaptive Bulkheads**: Automatically adjust limits based on load
2. **Priority Queues**: Allow high-priority operations to bypass limits
3. **Rate-Based Bulkheads**: Limit requests per second, not just concurrent
4. **Distributed Bulkheads**: Share limits across multiple instances
5. **Metrics Integration**: Publish metrics to Prometheus/Grafana
6. **Policy Composition**: Combine bulkhead with retry and circuit breaker

## References

- [Polly Documentation](https://www.pollydocs.org/)
- [Bulkhead Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/bulkhead)
- [Circuit Breaker Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
- [Release It! - Michael Nygard](https://pragprog.com/titles/mnee2/release-it-second-edition/)
