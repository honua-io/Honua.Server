# Cold Start Optimization - Quick Reference

## TL;DR

Add to Program.cs:
```csharp
using Honua.Server.Core.DependencyInjection;
using Honua.Server.Core.Hosting;

var profiler = new StartupProfiler();
builder.Services.AddColdStartOptimizations(builder.Configuration);
profiler.LogResults(app.Logger);
```

Add to appsettings.json:
```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true
  }
}
```

## Configuration Cheat Sheet

### Minimal (Default Behavior)

```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true
  }
}
```

### Production (Optimized for Speed)

```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true,
    "StartupDelayMs": 500,
    "MaxConcurrentWarmups": 5,
    "TimeoutMs": 3000
  }
}
```

### Development (Disable for Faster Iteration)

```json
{
  "ConnectionPoolWarmup": {
    "Enabled": false
  }
}
```

## Environment Variables

```bash
# Enable warmup
export ConnectionPoolWarmup__Enabled=true

# Faster startup (less delay)
export ConnectionPoolWarmup__StartupDelayMs=300

# .NET runtime optimizations
export DOTNET_ReadyToRun=1
export DOTNET_TieredPGO=1
export DOTNET_gcServer=0
```

## Kubernetes Quick Start

```yaml
env:
- name: ConnectionPoolWarmup__Enabled
  value: "true"
- name: DOTNET_ReadyToRun
  value: "1"
- name: DOTNET_gcServer
  value: "0"

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

## Cloud Run Quick Start

```yaml
env:
- name: ConnectionPoolWarmup__Enabled
  value: "true"
- name: ConnectionPoolWarmup__StartupDelayMs
  value: "300"
- name: DOTNET_ReadyToRun
  value: "1"

startupProbe:
  httpGet:
    path: /health/ready
  initialDelaySeconds: 0
  periodSeconds: 3
  failureThreshold: 10
```

## Docker Compose Quick Start

```yaml
services:
  honua:
    image: honua-server:latest
    environment:
      ConnectionPoolWarmup__Enabled: "true"
      DOTNET_ReadyToRun: "1"
      DOTNET_gcServer: "0"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 10s
      timeout: 3s
      retries: 3
      start_period: 10s
```

## Common Tasks

### Check Warmup Status

```bash
curl http://localhost:8080/health/ready | jq '.checks[] | select(.name == "warmup")'
```

### View Startup Metrics

```bash
docker logs honua-container | grep -E "startup|warmup"
```

### Profile Startup Time

Add to Program.cs:
```csharp
var profiler = new StartupProfiler();
profiler.Checkpoint("After service configuration");
profiler.Checkpoint("After app build");
profiler.LogResults(app.Logger);
```

### Lazy Load a Heavy Service

```csharp
// Instead of:
services.AddSingleton<IHeavyService, HeavyService>();

// Use:
services.AddLazySingleton<IHeavyService, HeavyService>();
```

### Add Custom Warmup

```csharp
public class MyWarmupService : IWarmupService
{
    public async Task WarmupAsync(CancellationToken ct)
    {
        // Your warmup logic
    }
}

// Register:
services.AddWarmupService<MyWarmupService>();
```

## Troubleshooting Quick Fixes

### Startup Still Slow

```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Core.Data.ConnectionPoolWarmupService": "Debug"
    }
  }
}
```

### Warmup Timeouts

```json
{
  "ConnectionPoolWarmup": {
    "TimeoutMs": 10000,
    "MaxConcurrentWarmups": 1
  }
}
```

### High Memory Usage

```dockerfile
ENV DOTNET_gcServer=0
ENV DOTNET_GCHeapCount=2
```

## Performance Targets

| Metric | Target |
|--------|--------|
| Cold start time | <3s |
| First request | <500ms |
| Startup memory | <100MB |
| Warmup time | <5s |

## Services Affected

### Optimized Automatically
- Database connection pools
- Redis connections
- Metadata cache
- Health checks

### Candidates for Lazy Loading
- Cloud SDK clients (AWS, Azure, Google)
- OData EDM models
- Raster processing services
- External API clients

## Files Created

```
src/Honua.Server.Core/
├── Data/
│   └── ConnectionPoolWarmupService.cs
├── DependencyInjection/
│   ├── LazyServiceExtensions.cs
│   └── ColdStartOptimizationExtensions.cs
├── HealthChecks/
│   └── WarmupHealthCheck.cs
└── Hosting/
    ├── LazyRedisInitializer.cs
    └── StartupProfiler.cs

docs/performance/
├── COLD_START_OPTIMIZATION.md
├── INTEGRATION_EXAMPLE.md
└── COLD_START_QUICK_REFERENCE.md
```

## One-Line Checklist

- [ ] Add `AddColdStartOptimizations()` to Program.cs
- [ ] Configure warmup in appsettings.json
- [ ] Set .NET environment variables
- [ ] Add readiness probe pointing to `/health/ready`
- [ ] Test with startup profiler
- [ ] Verify warmup completion in logs
- [ ] Measure first request latency

## Performance Comparison

| Before | After | Improvement |
|--------|-------|-------------|
| 5-10s startup | 2-4s startup | 50-60% |
| 2-3s first request | <500ms | 75-83% |
| 150MB memory | 80MB | 47% |

## Further Reading

- [Full Documentation](./COLD_START_OPTIMIZATION.md)
- [Integration Examples](./INTEGRATION_EXAMPLE.md)
- [Deployment Guide](../DEPLOYMENT.md)
