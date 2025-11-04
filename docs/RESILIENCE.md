# Resilience Patterns

Honua Server implements resilience patterns using Polly to handle transient failures gracefully and maintain system availability.

## Overview

Resilience patterns protect against:
- **Network failures**: Timeouts, connection issues
- **Transient errors**: Temporary service unavailability
- **Resource exhaustion**: Rate limiting, connection pool exhaustion
- **Cascading failures**: One failure triggering others

## Implemented Patterns

### 1. Circuit Breaker

Prevents cascading failures by "opening" the circuit after repeated failures, giving failing services time to recover.

**States:**
- **Closed**: Normal operation, requests pass through
- **Open**: Too many failures, requests fail immediately (fail fast)
- **Half-Open**: Testing if service recovered, allowing limited requests

**Configuration (Cloud Storage Example):**
```csharp
FailureRatio = 0.5          // Open after 50% failures
SamplingDuration = 30s      // Calculate ratio over 30 seconds
MinimumThroughput = 10      // Need at least 10 requests
BreakDuration = 30s         // Stay open for 30 seconds
```

### 2. Retry with Exponential Backoff

Automatically retries failed operations with increasing delays between attempts.

**Cloud Storage Policy:**
- Max 3 retry attempts
- Initial delay: 500ms
- Exponential backoff: 500ms → 1s → 2s
- Retries on: 5xx errors, 408 Request Timeout, 429 Too Many Requests

**Database Policy:**
- Max 3 retry attempts
- Initial delay: 100ms
- Exponential backoff: 100ms → 200ms → 400ms
- Retries on: Timeouts, connection errors, deadlocks

### 3. Timeout

Prevents requests from hanging indefinitely.

| Operation Type | Timeout |
|---------------|---------|
| Cloud Storage | 30 seconds |
| External APIs | 60 seconds |
| Database Queries | 30 seconds |
| Fast Operations | Configurable (typically 5-10s) |

## Resilience Policies

### Cloud Storage Policy

Applied to S3 and Azure Blob storage HTTP requests.

```csharp
var pipeline = ResiliencePolicies.CreateCloudStoragePolicy(loggerFactory);

var response = await pipeline.ExecuteAsync(async ct =>
{
    return await httpClient.GetAsync(url, ct);
}, cancellationToken);
```

**Protection:**
- Timeout: 30s per request
- Retry: 3 attempts with exponential backoff
- Circuit Breaker: Opens after 50% failure rate over 30s window

**Use Cases:**
- Raster tile loading from S3/Azure
- Attachment storage access
- Large file uploads/downloads

### External API Policy

Applied to external service integrations (STAC catalogs, ArcGIS Server migration).

```csharp
var pipeline = ResiliencePolicies.CreateExternalApiPolicy(loggerFactory);

var response = await pipeline.ExecuteAsync(async ct =>
{
    return await httpClient.PostAsync(url, content, ct);
}, cancellationToken);
```

**Protection:**
- Timeout: 60s per request (external APIs may be slower)
- Retry: 2 attempts with exponential backoff
- No circuit breaker (external APIs may have long recovery times)

**Use Cases:**
- Esri service migration
- STAC catalog synchronization
- External metadata harvesting

### Database Policy

Applied to database queries and transactions.

```csharp
var pipeline = ResiliencePolicies.CreateDatabasePolicy(loggerFactory);

var result = await pipeline.ExecuteAsync(async ct =>
{
    return await ExecuteQueryAsync(query, ct);
}, cancellationToken);
```

**Protection:**
- Timeout: 30s per query
- Retry: 3 attempts for transient errors (connection timeouts, deadlocks)
- Exponential backoff: 100ms → 200ms → 400ms

**Detected Transient Errors:**
- Connection timeouts
- Deadlocks
- Temporary connection failures

**Use Cases:**
- Feature queries
- Metadata loading
- Transaction processing

## Monitoring Resilience

### Logs

Resilience events are logged with appropriate levels:

```
[Warning] Retrying cloud storage request (attempt 2/3). Status: 503 ServiceUnavailable
[Error] Cloud storage circuit breaker OPENED. Too many failures.
[Information] Cloud storage circuit breaker CLOSED. Service recovered.
[Warning] Database query timed out after 30s
```

### Metrics

Key metrics to monitor:

```
# Circuit breaker state (Prometheus format)
polly_circuit_breaker_state{policy="CloudStorage"} 0  # 0=Closed, 1=Open, 2=HalfOpen

# Retry attempts
polly_retry_attempts_total{policy="CloudStorage",result="success"} 245
polly_retry_attempts_total{policy="CloudStorage",result="failure"} 12

# Timeouts
polly_timeout_total{policy="Database"} 5
```

### Observability Integration

Resilience events create OpenTelemetry spans:

```
WMS.GetMap (2.5s)
├── Database.QueryFeatures (1.2s)
│   └── Polly.Retry (attempt 2) (0.3s)
├── CloudStorage.LoadTile (0.8s)
│   └── Polly.Retry (attempt 1) (0.4s)
```

## Best Practices

### Do's ✓

- **Use appropriate policies** - Cloud storage needs circuit breakers, databases need retries
- **Configure realistic timeouts** - Too short causes unnecessary failures, too long blocks threads
- **Log resilience events** - Helps diagnose issues and tune policies
- **Monitor circuit breaker state** - Alerts when services degrade
- **Test failure scenarios** - Verify policies work under actual failures

### Don'ts ✗

- **Don't retry non-idempotent operations** - Can cause data corruption
- **Don't retry on 4xx client errors** - They won't succeed on retry
- **Don't set infinite retries** - Causes cascading failures
- **Don't ignore circuit breaker open state** - Implement fallback behavior
- **Don't use same policy for all scenarios** - Different operations need different protection

## Graceful Degradation

When dependencies fail, Honua implements fallback behaviors:

### Raster Tile Cache Unavailable
```
Fallback: Generate tiles on-the-fly (slower but functional)
User Impact: Increased latency, no data loss
```

### Metadata Temporarily Unavailable
```
Fallback: Use last known good metadata snapshot
User Impact: Stale metadata, services continue operating
```

### Database Connection Pool Exhausted
```
Fallback: Queue requests with timeout
User Impact: Increased latency, eventual timeout if pool doesn't recover
```

### S3/Azure Storage Unavailable
```
Fallback: Return ServiceUnavailable (503) with Retry-After header
User Impact: Temporary service interruption for affected resources
```

## Configuration

Resilience policies can be customized in code. Future enhancement: configuration file support.

```json
{
  "Resilience": {
    "CloudStorage": {
      "Timeout": "00:00:30",
      "RetryCount": 3,
      "CircuitBreakerThreshold": 0.5,
      "CircuitBreakerDuration": "00:00:30"
    },
    "Database": {
      "Timeout": "00:00:30",
      "RetryCount": 3
    }
  }
}
```

## Testing Resilience

### Chaos Engineering

Test failure scenarios with tools like Chaos Mesh or manual injection:

```bash
# Simulate S3 unavailability
iptables -A OUTPUT -p tcp --dport 443 -d s3.amazonaws.com -j DROP

# Verify circuit breaker opens
curl http://localhost:5000/wmts/layer/WebMercatorQuad/10/163/395.png

# Check logs for circuit breaker state
docker logs honua-server | grep "circuit breaker"
```

### Load Testing with Failures

```bash
# Run load test while introducing failures
k6 run --env FAILURE_RATE=0.1 wmts-load-test.js
```

### Database Resilience Testing

```bash
# Kill database connections mid-query
docker exec -it postgres psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name = 'Honua';"

# Verify automatic recovery
```

## Troubleshooting

### Circuit Breaker Stuck Open

**Symptoms:** All requests to a service fail immediately
**Diagnosis:** Check logs for "circuit breaker OPENED" messages
**Solutions:**
1. Wait for break duration to expire (circuit will test recovery)
2. Fix underlying service issue
3. Restart application to reset circuit breaker state

### Excessive Retries

**Symptoms:** High latency, increased load on failing service
**Diagnosis:** Log shows many retry attempts
**Solutions:**
1. Reduce retry count
2. Increase backoff delay
3. Add circuit breaker to fail fast

### Timeouts Too Aggressive

**Symptoms:** Operations fail that should succeed
**Diagnosis:** Timeout logs for operations that need more time
**Solutions:**
1. Increase timeout duration
2. Optimize slow operations
3. Use different policy for slow operations

## Next Steps

- [Performance Testing](../tests/load/README.md)
- [Observability](./TRACING.md)
- [Deployment](./DEPLOYMENT.md)
