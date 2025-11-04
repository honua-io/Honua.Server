# Error Handling Quick Reference

Quick reference guide for common error handling patterns in Honua.

## Exception Types

### Transient Exceptions (Can Retry)
```csharp
ServiceUnavailableException     // Service temporarily down
ServiceTimeoutException         // Request timed out
CircuitBreakerOpenException     // Circuit breaker protecting service
ServiceThrottledException       // Rate limited
CacheUnavailableException       // Cache temporarily unavailable
CacheWriteException             // Cache write failed
RasterProcessingException       // Raster processing failed (if isTransient=true)
```

### Permanent Exceptions (Don't Retry)
```csharp
FeatureNotFoundException        // Feature doesn't exist
LayerNotFoundException          // Layer doesn't exist
RasterSourceNotFoundException   // Raster file not found
UnsupportedRasterFormatException // Format not supported
ArgumentException               // Invalid input
UnauthorizedAccessException     // Permission denied
```

## Common Patterns

### Pattern 1: Simple Fallback
```csharp
var result = await executor.ExecuteWithDefaultAsync(
    primary: async ct => await GetFromService(ct),
    defaultValue: new EmptyResult(),
    operationName: "GetData"
);
```

### Pattern 2: Stale Cache Fallback
```csharp
var result = await executor.ExecuteWithStaleCacheFallbackAsync(
    primary: async ct => await GenerateFresh(ct),
    getStaleCache: async ct => await cache.GetAsync(key, ct),
    defaultValue: EmptyData,
    operationName: "GetData"
);
```

### Pattern 3: Alternative Service
```csharp
var result = await executor.ExecuteWithFallbackAsync(
    primary: async ct => await primaryService.GetAsync(ct),
    fallback: async (ex, ct) => await backupService.GetAsync(ct),
    operationName: "GetData"
);
```

### Pattern 4: Circuit Breaker
```csharp
var breaker = new CacheCircuitBreaker("Redis", logger);
var data = await breaker.ExecuteAsync(
    async ct => await cache.GetAsync(key, ct),
    cancellationToken
);
```

### Pattern 5: Database Read-Only Mode
```csharp
if (resilientDb.IsReadOnlyMode)
{
    return StatusCode(503, "Database in read-only mode");
}

await resilientDb.ExecuteWriteAsync(
    async ct => await repo.SaveAsync(data, ct),
    "SaveData"
);
```

## HTTP Status Codes

| Code | Exception | Meaning |
|------|-----------|---------|
| 400 | ArgumentException | Bad request |
| 401 | UnauthorizedAccessException | Not authenticated |
| 404 | *NotFoundException | Resource not found |
| 429 | ServiceThrottledException | Rate limited |
| 500 | Unexpected errors | Internal error |
| 503 | ServiceUnavailableException | Service down |
| 504 | ServiceTimeoutException | Timeout |

## Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Cache service temporarily unavailable",
  "instance": "/api/features/123",
  "traceId": "00-abc123...",
  "timestamp": "2025-10-17T10:30:00Z",
  "isTransient": true
}
```

## Circuit Breaker States

| State | Behavior | Next State |
|-------|----------|------------|
| **Closed** | Normal operation | Open (on failures) |
| **Open** | Fail fast, return null | Half-Open (after break duration) |
| **Half-Open** | Test one request | Closed (success) or Open (failure) |

## Fallback Reasons

```csharp
FallbackReason.ServiceUnavailable   // Primary service down
FallbackReason.Timeout              // Request timed out
FallbackReason.CircuitBreakerOpen   // Circuit protecting service
FallbackReason.Throttled            // Rate limited
FallbackReason.StaleCache           // Using stale cached data
FallbackReason.NoFallbackAvailable  // No fallback, using default
FallbackReason.TransientError       // Other transient error
```

## Testing Errors

### Unit Test
```csharp
[Fact]
public async Task ShouldFallbackOnError()
{
    var result = await executor.ExecuteWithDefaultAsync(
        async ct => throw new ServiceUnavailableException("Test", "Error"),
        defaultValue: -1,
        operationName: "Test"
    );

    Assert.True(result.IsFromFallback);
    Assert.Equal(-1, result.Value);
}
```

### Chaos Test
```csharp
[Fact]
public async Task ShouldHandleRandomFailures()
{
    for (int i = 0; i < 100; i++)
    {
        var result = await executor.ExecuteWithDefaultAsync(
            async ct =>
            {
                if (Random.Shared.Next(100) < 50)
                    throw new ServiceTimeoutException("Test", TimeSpan.FromSeconds(30));
                return i;
            },
            defaultValue: -1,
            operationName: "RandomTest"
        );

        // Should never throw - always returns a value
    }
}
```

## Monitoring Queries

### Prometheus

```promql
# Circuit breaker state
honua_circuit_breaker_state{service="cache"}

# Fallback usage
rate(honua_fallback_invocations_total[5m])

# Error rate
rate(honua_errors_total[5m]) / rate(honua_requests_total[5m])

# Database read-only mode
honua_database_readonly_mode
```

### Logs

```bash
# Circuit breaker events
grep "Circuit breaker" logs/*.log

# Fallback usage
grep "Using fallback\|IsFromFallback" logs/*.log

# High error rate
grep "ERROR" logs/*.log | wc -l
```

## Configuration

```json
{
  "Resilience": {
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "MinimumThroughput": 10,
      "BreakDuration": "00:00:30"
    },
    "Retry": {
      "MaxAttempts": 3,
      "BaseDelay": "00:00:00.500"
    }
  }
}
```

## Common Mistakes

❌ **DON'T**
```csharp
// Don't let exceptions propagate without handling
var data = await service.GetAsync(); // Can throw

// Don't retry permanent errors
catch (FeatureNotFoundException ex) { retry(); } // Bad!

// Don't ignore transient markers
catch (Exception ex) { /* ignore */ }
```

✅ **DO**
```csharp
// Use fallback patterns
var result = await executor.ExecuteWithDefaultAsync(...);

// Check transient markers
if (ex is ITransientException transient && transient.IsTransient)
    await RetryAsync();

// Log and handle appropriately
catch (Exception ex)
{
    logger.LogError(ex, "Operation failed");
    return DefaultValue;
}
```

## See Also

- [Full Error Boundary Documentation](./ERROR_BOUNDARY_HANDLING.md)
- [Error Recovery Runbook](./ERROR_RECOVERY_RUNBOOK.md)
- [Testing Guide](./TESTING.md)
