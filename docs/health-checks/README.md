# Health Checks Documentation

## Overview

HonuaIO implements comprehensive ASP.NET Core health checks to monitor the health of critical infrastructure components including databases, caches, and storage services. These health checks are essential for:

- Kubernetes liveness and readiness probes
- Load balancer health monitoring
- Application monitoring and alerting
- DevOps automation and orchestration

## Health Check Endpoints

### `/health` - Comprehensive Health Check

Returns the health status of all registered health checks including database, cache, and storage.

**Response Format:**
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "totalDuration": 123.45,
  "entries": [
    {
      "name": "database",
      "status": "Healthy",
      "duration": 45.67,
      "description": "All 3 data source(s) are accessible",
      "data": {
        "totalDataSources": 3,
        "healthyDataSources": 3,
        "unhealthyDataSources": 0,
        "healthyDataSourceIds": ["postgres-main", "sqlite-metadata", "mysql-backup"]
      },
      "tags": ["ready", "database"]
    },
    {
      "name": "cache",
      "status": "Healthy",
      "duration": 12.34,
      "description": "Redis cache is operational",
      "data": {
        "cacheType": "Redis",
        "status": "Healthy",
        "endpoints": ["localhost:6379 (Connected: true)"]
      },
      "tags": ["ready", "cache"]
    },
    {
      "name": "storage",
      "status": "Healthy",
      "duration": 56.78,
      "description": "All 2 storage provider(s) are operational",
      "data": {
        "configuredProviders": ["S3 (Healthy)", "FileSystem (Healthy)"],
        "s3Status": "Healthy",
        "s3Details": {
          "bucketName": "honua-attachments",
          "region": "us-west-2"
        },
        "fileSystemStatus": "Healthy",
        "fileSystemDetails": {
          "basePath": "/var/honua/attachments",
          "writable": true
        }
      },
      "tags": ["ready", "storage"]
    }
  ]
}
```

**Status Codes:**
- `200 OK` - All health checks are Healthy
- `200 OK` (Degraded) - Some health checks are Degraded but operational
- `503 Service Unavailable` - One or more health checks are Unhealthy

### `/health/ready` - Readiness Probe

Returns the health status of critical services required for the application to handle requests. Only includes health checks tagged with "ready".

**Use Case:** Kubernetes readiness probe to ensure pods only receive traffic when ready

**Response:** Same format as `/health` but filtered to "ready" tagged checks

**Kubernetes Configuration:**
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 10
  timeoutSeconds: 5
  successThreshold: 1
  failureThreshold: 3
```

### `/health/live` - Liveness Probe

Returns `200 OK` if the application process is running. Does not perform any actual health checks.

**Use Case:** Kubernetes liveness probe to detect deadlocks and restart unhealthy pods

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": 0.0
}
```

**Kubernetes Configuration:**
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 30
  timeoutSeconds: 5
  successThreshold: 1
  failureThreshold: 3
```

### `/healthz/*` - Legacy Kubernetes Endpoints

The application also exposes Kubernetes-style `/healthz` endpoints for compatibility:

- `/healthz/startup` - Startup probe (if configured)
- `/healthz/live` - Liveness probe (same as `/health/live`)
- `/healthz/ready` - Readiness probe (same as `/health/ready`)

## Health Check Components

### Database Health Check

**Class:** `DatabaseHealthCheck`
**Tags:** `ready`, `database`
**Location:** `/src/Honua.Server.Host/HealthChecks/DatabaseHealthCheck.cs`

Tests connectivity to all configured data sources using the `IDataStoreProvider.TestConnectivityAsync` method.

**Health States:**
- **Healthy**: All data sources are accessible
- **Degraded**: Some data sources are accessible, some are not
- **Unhealthy**: All data sources are unavailable OR no data sources configured

**Data Returned:**
- `totalDataSources` - Number of configured data sources
- `healthyDataSources` - Number of accessible data sources
- `unhealthyDataSources` - Number of inaccessible data sources
- `healthyDataSourceIds` - List of accessible data source IDs
- `unhealthyDataSourceIds` - List of inaccessible data source IDs (if any)

### Cache Health Check

**Class:** `CacheHealthCheck`
**Tags:** `ready`, `cache`
**Location:** `/src/Honua.Server.Host/HealthChecks/CacheHealthCheck.cs`

Tests connectivity and basic operations (set/get/delete) for:
- Redis (via `IConnectionMultiplexer`)
- Distributed cache (via `IDistributedCache`)

**Health States:**
- **Healthy**: Cache is operational and read/write operations succeed
- **Degraded**: Cache is connected but operations fail OR no cache configured in development
- **Unhealthy**: Cache is disconnected in production OR operations fail

**Data Returned:**
- `cacheType` - Type of cache (Redis, DistributedCache, or None)
- `status` - Detailed status (Healthy, Disconnected, OperationFailed, NotConfigured)
- `endpoints` - Redis endpoints and connection status (if Redis)
- `error` - Error message (if unhealthy)

### Storage Health Check

**Class:** `StorageHealthCheck`
**Tags:** `ready`, `storage`
**Location:** `/src/Honua.Server.Host/HealthChecks/StorageHealthCheck.cs`

Tests connectivity to configured storage providers:
- AWS S3
- Azure Blob Storage
- Google Cloud Storage
- Filesystem storage

**Health States:**
- **Healthy**: All configured storage providers are accessible
- **Degraded**: Some storage providers are accessible OR no providers configured
- **Unhealthy**: All storage providers are unavailable

**Data Returned:**
- `configuredProviders` - List of configured providers with status
- `{provider}Status` - Status for each provider (Healthy, Degraded, Unhealthy, NotConfigured)
- `{provider}Details` - Provider-specific details (bucket name, region, etc.)

## Configuration

Health checks are configured in `appsettings.json`:

```json
{
  "HealthChecks": {
    "Timeout": "00:00:30",
    "Period": "00:00:10",
    "EnableDetailedErrors": true,
    "EnableDatabaseCheck": true,
    "EnableCacheCheck": true,
    "EnableStorageCheck": true,
    "EnableUI": false,
    "UIPath": "/healthchecks-ui"
  }
}
```

**Configuration Options:**

- `Timeout` - Maximum time for each health check to execute (default: 30 seconds)
- `Period` - Frequency of health check execution for UI (default: 10 seconds)
- `EnableDetailedErrors` - Include detailed diagnostic data in responses (default: true)
- `EnableDatabaseCheck` - Enable database health check (default: true)
- `EnableCacheCheck` - Enable cache health check (default: true)
- `EnableStorageCheck` - Enable storage health check (default: true)
- `EnableUI` - Enable Health Checks UI dashboard (default: false)
- `UIPath` - Path for Health Checks UI (default: /healthchecks-ui)

## Health Checks UI (Optional)

The Health Checks UI provides a web-based dashboard for monitoring health check status over time.

**Enable UI:**
```json
{
  "HealthChecks": {
    "EnableUI": true,
    "UIPath": "/healthchecks-ui"
  }
}
```

**Access Dashboard:**
Navigate to `/healthchecks-ui` in your browser

**Recommendation:** Only enable in development/staging environments. Use external monitoring solutions (Prometheus, Grafana, Application Insights) in production.

## Kubernetes Deployment Example

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua-server
        image: honua/server:latest
        ports:
        - containerPort: 8080

        # Startup probe - check if app started successfully
        startupProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          successThreshold: 1
          failureThreshold: 30  # Allow up to 150 seconds for startup

        # Liveness probe - restart if app is unresponsive
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
          timeoutSeconds: 5
          successThreshold: 1
          failureThreshold: 3  # Restart after 90 seconds of failures

        # Readiness probe - remove from load balancer if not ready
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          successThreshold: 1
          failureThreshold: 3  # Mark unready after 30 seconds of failures

        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
        - name: HealthChecks__EnableDetailedErrors
          value: "false"  # Disable detailed errors in production
```

## Best Practices

1. **Liveness vs Readiness**
   - Use `/health/live` for liveness probes (quick, no external dependencies)
   - Use `/health/ready` for readiness probes (checks external dependencies)

2. **Timeouts**
   - Set appropriate timeouts based on your infrastructure (default: 30 seconds)
   - Ensure health check timeout < probe timeout

3. **Probe Intervals**
   - Liveness: 30-60 seconds (avoid too frequent restarts)
   - Readiness: 5-10 seconds (faster traffic rerouting)

4. **Failure Thresholds**
   - Liveness: 3 failures (avoid premature restarts)
   - Readiness: 2-3 failures (faster traffic removal)

5. **Production Configuration**
   - Disable detailed errors (`EnableDetailedErrors: false`)
   - Disable Health Checks UI (`EnableUI: false`)
   - Use external monitoring (Prometheus, Application Insights)

6. **Development Configuration**
   - Enable detailed errors for debugging
   - Enable Health Checks UI for local monitoring
   - Shorter probe intervals for faster feedback

## Monitoring and Alerting

Health check endpoints can be integrated with:

- **Prometheus**: Scrape `/metrics` endpoint (requires health check metrics exporter)
- **Application Insights**: Automatic health check logging
- **Datadog**: APM health check monitoring
- **PagerDuty**: Alert on health check failures
- **Kubernetes**: Built-in probe failure events

## Troubleshooting

### Health Check Always Returns Unhealthy

1. Check logs for health check execution errors
2. Verify database/cache/storage connectivity from pod
3. Increase health check timeout
4. Verify configuration in appsettings.json

### Pods Restarting Frequently

1. Check if liveness probe is too aggressive
2. Increase `failureThreshold` or `periodSeconds`
3. Verify `/health/live` is not performing expensive checks

### Pods Not Receiving Traffic

1. Check readiness probe status: `kubectl describe pod <pod-name>`
2. Verify `/health/ready` endpoint returns 200 OK
3. Check if critical services (database, cache) are accessible

### Database Health Check Failing

1. Verify data source connection strings
2. Check network connectivity from pod to database
3. Verify database credentials and permissions
4. Check `IDataStoreProvider.TestConnectivityAsync` implementation

## API Reference

### IHealthCheck Interface

All health checks implement `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck`:

```csharp
public interface IHealthCheck
{
    Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default);
}
```

### HealthCheckResult

```csharp
public class HealthCheckResult
{
    public HealthStatus Status { get; }
    public string? Description { get; }
    public Exception? Exception { get; }
    public IReadOnlyDictionary<string, object>? Data { get; }
}
```

### HealthStatus Enum

```csharp
public enum HealthStatus
{
    Unhealthy = 0,  // Service unavailable
    Degraded = 1,   // Service operational but degraded
    Healthy = 2     // Service fully operational
}
```

## Additional Resources

- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Liveness, Readiness, and Startup Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
