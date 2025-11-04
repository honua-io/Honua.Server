# Resilience Features Quick Reference

**Version**: 1.0 | **Date**: 2025-10-23

---

## Circuit Breaker Pattern (HttpZarrReader)

### Default Configuration
```csharp
// File: src/Honua.Server.Core/Raster/Readers/HttpZarrReader.cs

HttpClient Timeout:         60 seconds
Operation Timeout:          30 seconds
Max Retry Attempts:         3
Initial Retry Delay:        100ms
Retry Backoff:              Exponential (100ms → 200ms → 400ms)
Circuit Breaker Threshold:  50% failure ratio
Sampling Duration:          30 seconds
Minimum Throughput:         10 requests
Break Duration:             30 seconds
```

### Circuit Breaker States

| State | Description | Duration | Next State |
|-------|-------------|----------|------------|
| **CLOSED** | Normal operation | Until 50% failures | OPEN |
| **OPEN** | Rejecting requests | 30 seconds | HALF-OPEN |
| **HALF-OPEN** | Testing recovery | First request | CLOSED or OPEN |

### Log Messages

```bash
# Circuit breaker opened
"Circuit breaker OPENED for Zarr storage. Remote storage is unavailable. Breaking for 30 seconds."

# Circuit breaker testing recovery
"Circuit breaker HALF-OPEN for Zarr storage. Testing if service recovered."

# Circuit breaker recovered
"Circuit breaker CLOSED for Zarr storage. Service recovered."

# Retry attempt
"Zarr chunk read failed (attempt 2 of 3), retrying after 200ms: Connection timeout"
```

### Exceptions Thrown

```csharp
// Circuit breaker is open
InvalidOperationException: "Zarr storage temporarily unavailable: {uri}. The service will retry automatically when storage recovers."

// Operation timeout
InvalidOperationException: "Zarr chunk read timeout after 30 seconds: {chunkUri}"

// HTTP request failed
InvalidOperationException: "Failed to fetch Zarr chunk: {message}"
```

---

## Health Check Endpoints

### Endpoint Overview

| Endpoint | Purpose | Kubernetes Use | Timeout |
|----------|---------|----------------|---------|
| `/healthz/startup` | Initial readiness | startupProbe | Fast |
| `/healthz/live` | Application liveness | livenessProbe | Fast |
| `/healthz/ready` | Full dependency check | readinessProbe | 30s |

### Health Check Tags

```
startup  - Critical initialization checks
live     - Basic application health
ready    - All dependencies operational
database - Database connectivity
storage  - Cloud storage (S3, Azure, GCS)
oidc     - OIDC authentication service
distributed - Redis/distributed services
```

### Response Format

```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "totalDuration": "00:00:00.123",
  "entries": {
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.012",
      "data": {
        "redis.configured": true,
        "redis.connected": true,
        "redis.latency_ms": 12.5
      }
    }
  }
}
```

### Health Status Meanings

| Status | Meaning | Action |
|--------|---------|--------|
| **Healthy** | All checks passed | None |
| **Degraded** | Non-critical issue | Monitor |
| **Unhealthy** | Critical failure | Alert |

---

## Quick Diagnostics

### Check Circuit Breaker State
```bash
# View circuit breaker logs
grep "Circuit breaker" /var/log/honua.log | tail -20

# Count retry attempts (last hour)
grep "Zarr chunk read failed" /var/log/honua.log | grep "$(date -d '1 hour ago' '+%Y-%m-%d %H')" | wc -l
```

### Check Health Status
```bash
# All health checks
curl -s http://localhost:8080/healthz/ready | jq

# Specific health check
curl -s http://localhost:8080/healthz/ready | jq '.entries.redis'

# Only failed checks
curl -s http://localhost:8080/healthz/ready | jq '.entries | to_entries[] | select(.value.status != "Healthy")'
```

### Check Redis Connectivity
```bash
# Health check
curl -s http://localhost:8080/healthz/ready | jq '.entries.redisStores'

# Response times
curl -s http://localhost:8080/healthz/ready | jq '.entries.redisStores.data."redis.latency_ms"'
```

### Check Database Connectivity
```bash
# All databases
curl -s http://localhost:8080/healthz/ready | jq '.entries.database_connectivity'

# Failed connections
curl -s http://localhost:8080/healthz/ready | jq '.entries.database_connectivity.data.failures'
```

---

## Tuning Guide

### Circuit Breaker Tuning

#### Increase Timeout (Slow Networks)
```csharp
// Increase operation timeout to 60s
_httpClient.Timeout = TimeSpan.FromSeconds(90);
.AddTimeout(TimeSpan.FromSeconds(60))
```

#### Reduce False Positives
```csharp
// Increase minimum throughput and sampling duration
MinimumThroughput = 20,  // Require 20 requests
SamplingDuration = TimeSpan.FromSeconds(60),  // Over 60 seconds
```

#### Longer Recovery Time
```csharp
// Increase break duration for slow recovery
BreakDuration = TimeSpan.FromSeconds(60),
```

#### More Aggressive Retries
```csharp
// More retry attempts with slower backoff
MaxRetryAttempts = 5,
Delay = TimeSpan.FromMilliseconds(200),
```

### Health Check Tuning

#### Adjust Timeout
```csharp
// Increase timeout for slow external services
.AddCheck<OidcDiscoveryHealthCheck>("oidc",
    failureStatus: HealthStatus.Degraded,
    tags: new[] { "ready", "oidc" },
    timeout: TimeSpan.FromSeconds(10))  // Increased from 5s
```

#### Change Failure Status
```csharp
// Treat as Degraded instead of Unhealthy
failureStatus: HealthStatus.Degraded,
```

---

## Monitoring Queries

### Prometheus
```promql
# Circuit breaker state
honua_circuit_breaker_state{service="HttpZarrReader"}

# Retry rate (last 5 minutes)
rate(honua_retry_attempts_total{service="HttpZarrReader"}[5m])

# Health check duration
histogram_quantile(0.95, honua_health_check_duration_seconds{check="database_connectivity"})

# Health check failures
rate(honua_health_check_failures_total[5m])
```

### Log Queries (grep)
```bash
# Circuit breaker events today
grep "Circuit breaker" /var/log/honua.log | grep "$(date '+%Y-%m-%d')"

# Retry attempts (last hour)
grep "Zarr chunk read failed" /var/log/honua.log | grep "$(date -d '1 hour ago' '+%Y-%m-%d %H')"

# Health check failures
grep "health check failed" /var/log/honua.log | tail -50

# Timeout errors
grep "Timeout reading Zarr chunk" /var/log/honua.log
```

---

## Alerting Rules

### Recommended Alerts

#### Critical Alerts
```yaml
# Circuit breaker open > 5 minutes
alert: CircuitBreakerOpen
expr: honua_circuit_breaker_state{service="HttpZarrReader"} == 1
for: 5m
labels:
  severity: critical
annotations:
  summary: "Circuit breaker is OPEN for HttpZarrReader"

# Database connectivity down
alert: DatabaseUnhealthy
expr: honua_health_check_status{check="database_connectivity"} == 0
for: 1m
labels:
  severity: critical
annotations:
  summary: "Database connectivity health check failed"

# Redis latency high
alert: RedisHighLatency
expr: honua_health_check_data{check="redisStores",metric="latency_ms"} > 100
for: 5m
labels:
  severity: warning
annotations:
  summary: "Redis latency > 100ms"
```

#### Warning Alerts
```yaml
# High retry rate
alert: HighRetryRate
expr: rate(honua_retry_attempts_total[5m]) > 0.1
for: 10m
labels:
  severity: warning
annotations:
  summary: "High retry rate detected (>0.1/s)"

# Health check degraded
alert: HealthCheckDegraded
expr: honua_health_check_status{check!="live"} == 1
for: 5m
labels:
  severity: warning
annotations:
  summary: "Health check {{ $labels.check }} is degraded"
```

---

## Troubleshooting Playbook

### Problem: Circuit breaker constantly opening

**Symptoms**:
- Repeated "Circuit breaker OPENED" logs
- `InvalidOperationException` with "temporarily unavailable"

**Diagnosis**:
```bash
# Check failure pattern
grep "Zarr chunk read failed" /var/log/honua.log | tail -50

# Check remote storage logs
curl -I https://zarr-storage.example.com/health
```

**Solutions**:
1. Verify remote storage is operational
2. Check network connectivity
3. Increase `MinimumThroughput` if too sensitive
4. Increase `SamplingDuration` for longer window
5. Check if timeout is too aggressive

### Problem: Readiness probe failing

**Symptoms**:
- Kubernetes not routing traffic
- `/healthz/ready` returns `Unhealthy`

**Diagnosis**:
```bash
# Identify failing check
curl -s http://localhost:8080/healthz/ready | jq '.entries | to_entries[] | select(.value.status != "Healthy")'

# Check specific service
curl -s http://localhost:8080/healthz/ready | jq '.entries.database_connectivity'
```

**Solutions**:
1. Review failing check logs
2. Verify external service connectivity
3. Increase check timeout if needed
4. Consider changing to `Degraded` if non-critical

### Problem: High retry rate

**Symptoms**:
- Many "retrying after" log messages
- Slow response times

**Diagnosis**:
```bash
# Count retries
grep "Zarr chunk read failed" /var/log/honua.log | wc -l

# Analyze failure types
grep "Zarr chunk read failed" /var/log/honua.log | cut -d':' -f4 | sort | uniq -c
```

**Solutions**:
1. Identify root cause (timeout, 500 errors, network)
2. Increase timeout if slow storage
3. Check remote storage performance
4. Review network latency

### Problem: Redis health check degraded

**Symptoms**:
- Health check shows `Degraded`
- "Redis not connected" logs

**Diagnosis**:
```bash
# Check Redis status
curl -s http://localhost:8080/healthz/ready | jq '.entries.redisStores'

# Test Redis directly
redis-cli -h localhost ping
```

**Solutions**:
1. Verify Redis is running
2. Check connection string
3. Review Redis logs
4. Verify network connectivity
5. Application falls back to in-memory (no data loss)

---

## Configuration Examples

### appsettings.json
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false"
  },
  "HealthChecks": {
    "Enabled": true,
    "DetailedErrors": true
  },
  "Logging": {
    "LogLevel": {
      "Honua.Server.Core.Raster.Readers": "Debug",
      "Honua.Server.Host.Health": "Information"
    }
  }
}
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: honua
        image: honua:latest
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /healthz/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /healthz/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 30
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /healthz/startup
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 30
```

---

## References

- **Implementation Status**: [RESILIENCE_IMPLEMENTATION_STATUS.md](RESILIENCE_IMPLEMENTATION_STATUS.md)
- **Verification Report**: [RESILIENCE_VERIFICATION_REPORT.md](RESILIENCE_VERIFICATION_REPORT.md)
- **Polly Documentation**: https://www.pollydocs.org/
- **ASP.NET Core Health Checks**: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
- **Circuit Breaker Pattern**: https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker

---

**Last Updated**: 2025-10-23
**Maintained By**: Honua Platform Team
