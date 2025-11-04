# Error Boundary Handling in Honua

This document describes the comprehensive error boundary handling system in Honua, which provides robust error handling, graceful degradation, and automatic recovery from failures.

## Overview

The error boundary system consists of:

1. **Exception Hierarchy** - Structured exceptions with transient markers
2. **Global Exception Handler** - RFC 7807 compliant error responses
3. **Circuit Breakers** - Prevent cascading failures
4. **Fallback Patterns** - Graceful degradation strategies
5. **Resilient Service Wrappers** - Error boundaries for critical services

## Exception Hierarchy

### Base Exceptions

```csharp
// Base exception for all Honua domain exceptions
public abstract class HonuaException : Exception

// Marker interface for transient failures
public interface ITransientException
{
    bool IsTransient { get; }
}
```

### Exception Categories

#### Service Exceptions
- `ServiceException` - Base for service-level errors
- `ServiceUnavailableException` - Service temporarily unavailable (transient)
- `CircuitBreakerOpenException` - Circuit breaker is open (transient)
- `ServiceTimeoutException` - Service timeout (transient)
- `ServiceThrottledException` - Rate limited (transient)

#### Cache Exceptions
- `CacheException` - Base for cache errors
- `CacheUnavailableException` - Cache unavailable (transient)
- `CacheKeyNotFoundException` - Key not found (permanent)
- `CacheWriteException` - Cache write failed (transient)

#### Raster Exceptions
- `RasterException` - Base for raster processing errors
- `RasterProcessingException` - Processing failed (transient/permanent)
- `RasterSourceNotFoundException` - Source not found (permanent)
- `UnsupportedRasterFormatException` - Format not supported (permanent)

## Global Exception Handler

The `GlobalExceptionHandlerMiddleware` provides centralized exception handling with RFC 7807 Problem Details responses.

### Features

- **Structured Error Responses** - RFC 7807 compliant JSON responses
- **Environment-Aware** - Detailed errors in development, sanitized in production
- **Trace Correlation** - Includes trace IDs for debugging
- **Transient Detection** - Marks transient errors for retry logic
- **HTTP Status Mapping** - Correct status codes for each exception type

### Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "The service is temporarily unavailable. Please try again later.",
  "instance": "/api/tiles/layer1/0/0/0.png",
  "traceId": "00-abc123...",
  "timestamp": "2025-10-17T10:30:00Z",
  "isTransient": true
}
```

### HTTP Status Code Mapping

| Exception Type | Status Code | Transient |
|---------------|-------------|-----------|
| `ArgumentException` | 400 Bad Request | No |
| `FeatureNotFoundException` | 404 Not Found | No |
| `ServiceThrottledException` | 429 Too Many Requests | Yes |
| `ServiceUnavailableException` | 503 Service Unavailable | Yes |
| `ServiceTimeoutException` | 504 Gateway Timeout | Yes |

## Circuit Breakers

Circuit breakers prevent cascading failures by failing fast when a service is unhealthy.

### Cache Circuit Breaker

```csharp
var breaker = new CacheCircuitBreaker("RedisCache", logger);

// Execute with circuit breaker protection
var data = await breaker.ExecuteAsync(async ct =>
{
    return await cache.GetAsync(key, ct);
}, cancellationToken);

// Returns null if circuit is open or operation fails
if (data == null)
{
    // Use fallback or default value
}
```

### Configuration

- **Failure Ratio**: 50% (circuit opens after 50% of operations fail)
- **Minimum Throughput**: 10 operations (minimum before circuit can open)
- **Sampling Duration**: 30 seconds (window for failure rate calculation)
- **Break Duration**: 30 seconds (how long circuit stays open)

### Circuit States

1. **Closed** - Normal operation, all requests go through
2. **Open** - Circuit is open, all requests fail fast
3. **Half-Open** - Testing if service has recovered

## Fallback Patterns

### 1. Fallback to Alternative Service

```csharp
var result = await executor.ExecuteWithFallbackAsync(
    primary: async ct => await primaryService.GetDataAsync(ct),
    fallback: async (ex, ct) => await backupService.GetDataAsync(ct),
    operationName: "GetData"
);

if (result.IsFromFallback)
{
    logger.LogWarning("Used fallback service due to: {Reason}", result.FallbackReason);
}
```

### 2. Fallback to Default Value

```csharp
var result = await executor.ExecuteWithDefaultAsync(
    primary: async ct => await service.GetConfigAsync(ct),
    defaultValue: new DefaultConfig(),
    operationName: "GetConfig"
);

// Always returns a value, either from service or default
```

### 3. Fallback to Stale Cache

```csharp
var result = await executor.ExecuteWithStaleCacheFallbackAsync(
    primary: async ct => await GenerateFreshTile(ct),
    getStaleCache: async ct => await cache.GetAsync(tileKey, ct),
    defaultValue: EmptyTile,
    operationName: "GetTile"
);

if (result.FallbackReason == FallbackReason.StaleCache)
{
    // Serving stale data, but better than nothing
    response.Headers["X-Cache-Status"] = "STALE";
}
```

### 4. Multiple Fallbacks

```csharp
var result = await executor.ExecuteWithMultipleFallbacksAsync(
    primary: async ct => await primaryService.GetDataAsync(ct),
    fallbacks: new[]
    {
        async (ex, ct) => await secondaryService.GetDataAsync(ct),
        async (ex, ct) => await tertiaryService.GetDataAsync(ct),
        async (ex, ct) => await cache.GetAsync(key, ct)
    },
    defaultValue: EmptyData,
    operationName: "GetDataWithMultipleFallbacks"
);
```

## Resilient Service Wrappers

### Raster Tile Service

```csharp
public class ResilientRasterTileService
{
    public async Task<FallbackResult<byte[]>> GetTileWithFallbackAsync(
        Func<CancellationToken, Task<byte[]>> generateTile,
        string tileKey,
        CancellationToken cancellationToken = default)
    {
        // Attempts to generate tile
        // Falls back to cached tile if generation fails
        // Returns empty tile if cache also unavailable
    }
}
```

### Database Service

```csharp
public class ResilientDatabaseService
{
    public bool IsReadOnlyMode { get; }

    public async Task<bool> ExecuteWriteAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        // Executes write operation
        // Enters read-only mode if permanent write errors occur
        // Returns false if in read-only mode
    }
}
```

### External Service Wrapper

```csharp
public class ResilientExternalServiceWrapper
{
    public async Task<FallbackResult<T>> ExecuteWithAlternativeAsync<T>(
        Func<CancellationToken, Task<T>> primary,
        Func<Exception, CancellationToken, Task<T>> alternative,
        CancellationToken cancellationToken = default)
    {
        // Executes with circuit breaker and retry
        // Falls back to alternative service if primary fails
    }
}
```

## Resilience Policies

### Cloud Storage Policy

```csharp
var policy = ResiliencePolicies.CreateCloudStoragePolicy(loggerFactory);

// Includes:
// - 30 second timeout per request
// - 3 retry attempts with exponential backoff
// - Circuit breaker (50% failure rate, 30s break)
```

### Database Policy

```csharp
var policy = ResiliencePolicies.CreateDatabasePolicy(loggerFactory);

// Includes:
// - 30 second timeout per query
// - 3 retry attempts for transient errors
// - Exponential backoff
```

### External API Policy

```csharp
var policy = ResiliencePolicies.CreateExternalApiPolicy(loggerFactory);

// Includes:
// - 60 second timeout (external APIs may be slow)
// - 2 retry attempts
// - Exponential backoff
```

## Usage Examples

### Example 1: Resilient Tile Generation

```csharp
public async Task<IActionResult> GetTile(string layer, int z, int x, int y)
{
    var tileKey = $"{layer}/{z}/{x}/{y}";

    var result = await _resilientTileService.GetTileWithFallbackAsync(
        generateTile: async ct => await _tileGenerator.GenerateAsync(layer, z, x, y, ct),
        tileKey: tileKey,
        cancellationToken: HttpContext.RequestAborted
    );

    if (result.IsFromFallback)
    {
        Response.Headers["X-Tile-Status"] = result.FallbackReason.ToString();
        _logger.LogWarning("Served tile from fallback: {Reason}", result.FallbackReason);
    }

    return File(result.Value, "image/png");
}
```

### Example 2: Database with Read-Only Mode

```csharp
public async Task<IActionResult> UpdateFeature(string id, FeatureUpdate update)
{
    if (_resilientDb.IsReadOnlyMode)
    {
        return StatusCode(503, new ProblemDetails
        {
            Title = "Read-Only Mode",
            Detail = "Database is in read-only mode. Writes are temporarily disabled.",
            Status = 503
        });
    }

    var success = await _resilientDb.ExecuteWriteAsync(
        operation: async ct => await _featureRepo.UpdateAsync(id, update, ct),
        operationName: "UpdateFeature",
        cancellationToken: HttpContext.RequestAborted
    );

    if (!success)
    {
        return StatusCode(503, "Write operation failed");
    }

    return Ok();
}
```

### Example 3: External Service with Alternative

```csharp
public async Task<StacCatalog> GetStacCatalog(string catalogUrl)
{
    var wrapper = new ResilientExternalServiceWrapper("STAC", _logger);

    var result = await wrapper.ExecuteWithAlternativeAsync(
        primary: async ct => await _primaryStacClient.GetCatalogAsync(catalogUrl, ct),
        alternative: async (ex, ct) => await _backupStacClient.GetCatalogAsync(catalogUrl, ct),
        cancellationToken: CancellationToken.None
    );

    if (result.IsFromFallback)
    {
        _logger.LogWarning("Used backup STAC service: {Reason}", result.FallbackReason);
    }

    return result.Value;
}
```

## Chaos Engineering Tests

The system includes comprehensive chaos engineering tests to verify error handling:

- **Random Failures** - Simulates random service failures
- **Intermittent Failures** - Tests recovery from temporary issues
- **Circuit Breaker Behavior** - Verifies circuit opens/closes correctly
- **Fallback Chains** - Tests multiple fallback strategies
- **Stale Cache** - Verifies graceful degradation with stale data

### Running Chaos Tests

```bash
dotnet test --filter "FullyQualifiedName~ChaosEngineering"
```

## Monitoring and Observability

### Metrics

All resilience operations emit metrics:

- `honua_circuit_breaker_state{service="cache"}` - Circuit breaker state
- `honua_fallback_invocations_total{reason="stale_cache"}` - Fallback usage
- `honua_database_readonly_mode{enabled="true"}` - Read-only mode status

### Logging

Error boundaries log at appropriate levels:

- **Transient Errors** - `LogWarning` (expected, will retry)
- **Permanent Errors** - `LogError` (unexpected, needs investigation)
- **Circuit State Changes** - `LogError` (OPENED), `LogInformation` (CLOSED)

### Alerts

Configure alerts for:

- Circuit breaker opened for >5 minutes
- Read-only mode enabled
- High fallback usage (>20% of requests)
- Multiple service timeouts

## Best Practices

1. **Always Use Fallbacks** - Never fail completely if a fallback is possible
2. **Cache Stale Data** - Serving stale data is better than no data
3. **Monitor Circuit Breakers** - Alert when circuits open
4. **Test Failure Scenarios** - Use chaos engineering regularly
5. **Log Fallback Usage** - Track when and why fallbacks are used
6. **Set Appropriate Timeouts** - Balance responsiveness vs. reliability
7. **Document Fallback Behavior** - Users should know when they're getting stale/default data

## Configuration

### appsettings.json

```json
{
  "Resilience": {
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "MinimumThroughput": 10,
      "BreakDuration": "00:00:30",
      "SamplingDuration": "00:00:30"
    },
    "Retry": {
      "MaxAttempts": 3,
      "BaseDelay": "00:00:00.500",
      "BackoffType": "Exponential"
    },
    "Timeout": {
      "Database": "00:00:30",
      "CloudStorage": "00:00:30",
      "ExternalApi": "00:01:00"
    }
  }
}
```

## Related Documentation

- [Observability Guide](./observability/README.md)
- [Testing Strategy](./TESTING.md)
- [API Documentation](./api/README.md)
- [Deployment Guide](./deployment/README.md)
