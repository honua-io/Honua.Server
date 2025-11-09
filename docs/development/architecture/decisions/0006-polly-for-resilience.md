# 6. Polly for Resilience Policies

Date: 2025-10-17

Status: Accepted

## Context

Honua integrates with numerous external services that can experience transient failures:
- **Cloud Storage**: AWS S3, Azure Blob Storage, Google Cloud Storage
- **Databases**: PostgreSQL, SQL Server, MySQL (network failures, connection pool exhaustion)
- **HTTP Services**: Raster data sources, external APIs, metadata endpoints
- **Certificate Authorities**: Let's Encrypt ACME protocol
- **DNS Providers**: Cloudflare, Azure DNS, Route53

Without resilience policies, transient failures cause request failures, degraded user experience, and operational incidents. We need systematic handling of:
- **Transient failures**: Network timeouts, temporary service unavailability
- **Rate limiting**: Cloud provider throttling (S3 SlowDown, Azure 429)
- **Cascading failures**: Prevent overloading already-stressed services
- **Partial failures**: Circuit breaking to fail fast when services are down

**Existing Codebase Evidence:**
- Polly v8 package referenced: `<PackageReference Include="Polly" Version="8.5.0" />`
- Circuit breakers: `/src/Honua.Server.Core/Raster/Caching/ExternalServiceResiliencePolicies.cs`
- Database retries: `/src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs`
- Used in S3, Azure Blob, GCS cache providers
- Process framework step retries: `/src/Honua.Cli.AI/Services/Processes/ProcessStepRetryHelper.cs`

## Decision

We will use **Polly** as the standard resilience library for handling transient failures and implementing circuit breakers.

**Resilience Strategies:**
1. **Retry with Exponential Backoff**: Database connections, cloud storage operations
2. **Circuit Breaker**: External service calls (S3, Azure Blob, HTTP)
3. **Timeout**: Prevent hung requests from consuming resources
4. **Bulkhead Isolation**: Limit concurrent operations to critical resources

**Key Policies:**
- **Database Retry**: Exponential backoff (100ms, 500ms, 1s) for transient DB failures
- **Cloud Storage Circuit Breaker**: Open after 50% failure rate, 30-second break duration
- **HTTP Timeout**: 30-second default for external HTTP calls
- **Process Step Retry**: Configurable retries for AI process framework steps

## Consequences

### Positive

- **Improved Reliability**: Automatic recovery from transient failures
- **Better User Experience**: Requests succeed despite temporary issues
- **Reduced Incidents**: Fewer on-call alerts for transient problems
- **Fail Fast**: Circuit breakers prevent wasted resources on failing services
- **Standardized**: Consistent error handling across codebase
- **Observability**: Polly integrates with OpenTelemetry for metrics
- **Testable**: Resilience policies can be tested in isolation
- **Configurable**: Policies adjustable per deployment environment

### Negative

- **Complexity**: More moving parts in error handling
- **Latency**: Retries add latency to failed requests
- **Hidden Failures**: Automatic retries can mask underlying issues
- **Configuration**: Requires tuning for different services
- **Learning Curve**: Team must understand resilience patterns

### Neutral

- Must balance retry attempts with user wait times
- Circuit breaker thresholds require tuning based on production data
- Retry budgets needed to prevent retry storms

## Alternatives Considered

### 1. Manual Retry Logic

Implement retry logic manually with try-catch-sleep loops.

**Pros:**
- Simple, no dependencies
- Full control over logic

**Cons:**
- **Code duplication** across multiple call sites
- Inconsistent retry behavior
- Easy to get wrong (infinite retries, no jitter)
- No circuit breaking
- Hard to test

**Verdict:** Rejected - unmaintainable at scale

### 2. Microsoft.Extensions.Http.Resilience

Use Microsoft's new resilience library (based on Polly v8).

**Pros:**
- Official Microsoft package
- Built on Polly v8
- Good HttpClient integration
- Modern .NET patterns

**Cons:**
- Limited to HttpClient scenarios
- Doesn't cover database, storage SDK calls
- Less mature than Polly
- Would need Polly anyway for non-HTTP cases

**Verdict:** Partial adoption - used for HttpClient, but still need Polly for other cases

### 3. Cloud SDK Built-in Retries

Rely on retry mechanisms in AWS SDK, Azure SDK, etc.

**Pros:**
- No additional code
- SDK-specific optimizations

**Cons:**
- **Inconsistent behavior** across SDKs
- Limited configurability
- No circuit breaking
- No unified observability
- Different for each cloud provider

**Verdict:** Rejected - insufficient and inconsistent

### 4. Service Mesh (Istio, Linkerd)

Implement resilience at infrastructure layer with service mesh.

**Pros:**
- Application-agnostic
- Centralized policy management
- Works for any language

**Cons:**
- **Heavy operational overhead** (requires Kubernetes)
- Doesn't help with in-process calls (database, SDKs)
- Adds network hop latency
- Complex configuration
- Overkill for most deployments

**Verdict:** Rejected - too heavyweight, doesn't solve all cases

## Implementation Details

### Database Retry Policy
```csharp
// /src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs
public static class DatabaseRetryPolicy
{
    public static ResiliencePipeline Create(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<NpgsqlException>(IsTransientException)
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning("Database retry {Attempt} after {Delay}ms",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .Build();
    }
}
```

### Circuit Breaker for Cloud Storage
```csharp
// /src/Honua.Server.Core/Raster/Caching/ExternalServiceResiliencePolicies.cs
public static ResiliencePipeline CreateCircuitBreakerPipeline(
    string serviceName, ILogger logger)
{
    return new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,              // 50% failure rate
            MinimumThroughput = 10,          // Min 10 actions
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>(IsTransientException),
            OnOpened = args =>
            {
                logger.LogWarning(
                    "Circuit breaker OPENED for {ServiceName}",
                    serviceName);
                return default;
            }
        })
        .Build();
}
```

### Usage Example (S3 Cache Provider)
```csharp
// /src/Honua.Server.Core/Raster/Caching/S3RasterTileCacheProvider.cs
private readonly ResiliencePipeline _circuitBreaker;

public async Task<byte[]?> GetTileAsync(string key)
{
    return await _circuitBreaker.ExecuteAsync(async ct =>
    {
        var response = await _s3Client.GetObjectAsync(
            _bucketName, key, ct);
        return await ReadStreamAsync(response.ResponseStream);
    }, cancellationToken);
}
```

### Process Framework Retry
```csharp
// /src/Honua.Cli.AI/Services/Processes/ProcessStepRetryHelper.cs
public static async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    TimeSpan? initialDelay = null)
{
    var pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = maxRetries,
            Delay = initialDelay ?? TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential
        })
        .Build();

    return await pipeline.ExecuteAsync(async ct => await operation());
}
```

## Resilience Policy Matrix

| Service Type | Retry | Circuit Breaker | Timeout | Bulkhead |
|-------------|-------|-----------------|---------|----------|
| **PostgreSQL** | ✅ 3 retries, exponential | ❌ No | ⚠️ Connection timeout | ❌ No |
| **SQLite** | ✅ 3 retries | ❌ No (local) | ❌ No | ❌ No |
| **S3** | ❌ SDK handles | ✅ 50% threshold | ✅ 30s | ⚠️ Future |
| **Azure Blob** | ❌ SDK handles | ✅ 50% threshold | ✅ 30s | ⚠️ Future |
| **HTTP Raster** | ✅ 2 retries | ✅ 50% threshold | ✅ 30s | ❌ No |
| **Process Steps** | ✅ Configurable | ❌ No | ✅ Step timeout | ❌ No |

## Configuration

Resilience policies can be configured:
```json
{
  "Resilience": {
    "Database": {
      "MaxRetries": 3,
      "InitialDelayMs": 100
    },
    "ExternalServices": {
      "CircuitBreakerThreshold": 0.5,
      "CircuitBreakerDurationSeconds": 30,
      "TimeoutSeconds": 30
    }
  }
}
```

## Observability Integration

Polly integrates with OpenTelemetry:
- Retry attempts tracked as events on activity spans
- Circuit breaker state changes logged
- Metrics: `polly.retry.attempts`, `polly.circuit_breaker.state`

## Testing

Resilience policies are testable:
```csharp
[Fact]
public async Task RetryPolicy_RetriesTransientFailures()
{
    var attempts = 0;
    var pipeline = DatabaseRetryPolicy.Create(logger);

    await pipeline.ExecuteAsync(async ct =>
    {
        attempts++;
        if (attempts < 3)
            throw new TimeoutException("Transient");
        return Task.CompletedTask;
    });

    Assert.Equal(3, attempts);
}
```

## Code References

- Circuit Breakers: `/src/Honua.Server.Core/Raster/Caching/ExternalServiceResiliencePolicies.cs`
- Database Retry: `/src/Honua.Server.Core/Data/DatabaseRetryPolicy.cs`
- S3 Provider: `/src/Honua.Server.Core/Raster/Caching/S3RasterTileCacheProvider.cs`
- Azure Provider: `/src/Honua.Server.Core/Raster/Caching/AzureBlobRasterTileCacheProvider.cs`

## References

- [Polly Documentation](https://www.pollydocs.org/)
- [Polly v8 Migration Guide](https://www.pollydocs.org/migration-v8.html)
- [Resilience Patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/category/resiliency)
- [Circuit Breaker Pattern](https://martinfowler.com/bliki/CircuitBreaker.html)

## Notes

Polly has proven essential for production reliability. The investment in configuring policies pays off through reduced incidents and better user experience.

The move from Polly v7 to v8 required migration to the new resilience pipeline API, but provided better composition and cleaner syntax.

Future work: Implement bulkhead isolation for high-concurrency scenarios and rate limiting for outbound requests.
