# Cold Start Optimization Guide

This document describes the cold start optimization strategies implemented in Honua Server to improve startup performance for serverless deployments (Cloud Run, AWS Lambda, Azure Container Apps).

## Table of Contents

- [Overview](#overview)
- [Problem Statement](#problem-statement)
- [Optimization Strategies](#optimization-strategies)
- [Configuration](#configuration)
- [Usage](#usage)
- [Monitoring and Profiling](#monitoring-and-profiling)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)

## Overview

Cold start performance is critical for serverless deployments. Without optimization, the first request after deployment can take 5-10 seconds due to:

- Connection pool initialization (1-2s)
- Redis connection establishment (500-2000ms)
- Metadata loading (500-1000ms)
- Service initialization (1-2s)
- JIT compilation (500-1500ms)

With the optimizations described in this document, cold start time is reduced to **2-4 seconds** with first request latency under **500ms**.

## Problem Statement

### Before Optimization

```
Timeline of cold start WITHOUT optimization:

T+0ms    : Container starts
T+500ms  : .NET runtime loaded
T+1500ms : Services registered (blocking)
T+3000ms : Redis connection established (blocking)
T+4000ms : Connection pools initialized (blocking)
T+5000ms : Metadata loaded (blocking)
T+6000ms : App starts accepting requests
T+8000ms : First request completes (2s request processing)

Total: 8 seconds from container start to first successful response
```

### After Optimization

```
Timeline of cold start WITH optimization:

T+0ms    : Container starts
T+500ms  : .NET runtime loaded (ReadyToRun)
T+1000ms : Lightweight services registered
T+1500ms : App starts accepting requests ✅
T+2000ms : (Background) Redis connection initiated
T+2500ms : (Background) Connection pool warmup started
T+3000ms : First request arrives
T+3500ms : First request completes (500ms processing)
T+4000ms : (Background) All warmup completed

Total: 3.5 seconds from container start to first successful response
       (1.5s startup + 2s background warmup in parallel with requests)
```

## Optimization Strategies

### 1. Connection Pool Warmup

The `ConnectionPoolWarmupService` pre-establishes database connections in the background without blocking startup.

**Key Features:**
- Runs in background after app starts
- Configurable delay before starting
- Parallel warmup with concurrency limits
- Never fails startup on errors
- Configurable timeout per connection

**Implementation:**
```csharp
// In ServiceCollectionExtensions.cs
services.Configure<ConnectionPoolWarmupOptions>(
    configuration.GetSection(ConnectionPoolWarmupOptions.SectionName));
services.AddHostedService<ConnectionPoolWarmupService>();
```

**Configuration:**
```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true,
    "EnableInDevelopment": false,
    "StartupDelayMs": 1000,
    "MaxConcurrentWarmups": 3,
    "MaxDataSources": 10,
    "TimeoutMs": 5000
  }
}
```

### 2. Lazy Service Loading

Heavy services are loaded only when first accessed using `LazyServiceExtensions`.

**Example Usage:**
```csharp
// Instead of eager loading:
services.AddSingleton<IHeavyService, HeavyService>(); // ❌ Loaded at startup

// Use lazy loading:
services.AddLazySingleton<IHeavyService, HeavyService>(); // ✅ Loaded on first use

// Or use explicit Lazy<T>:
services.AddLazyWrapper<IHeavyService>();

// In consuming code:
public class MyController
{
    private readonly Lazy<IHeavyService> _service;

    public MyController(Lazy<IHeavyService> service)
    {
        _service = service;
    }

    public void DoWork()
    {
        // Service initialized only when needed
        _service.Value.ProcessData();
    }
}
```

### 3. Lazy Redis Initialization

Redis connections are established in the background using `LazyRedisInitializer`.

**Key Features:**
- Non-blocking connection establishment
- Automatic retry on failure
- Graceful degradation to in-memory fallbacks
- Production warnings if Redis unavailable

**Implementation:**
```csharp
services.AddSingleton<LazyRedisInitializer>();
services.AddHostedService(sp => sp.GetRequiredService<LazyRedisInitializer>());
```

### 4. Startup Profiling

The `StartupProfiler` class provides detailed timing information for identifying bottlenecks.

**Example Usage in Program.cs:**
```csharp
var profiler = new StartupProfiler();
profiler.Checkpoint("Builder created");

var builder = WebApplication.CreateBuilder(args);
profiler.Checkpoint("Builder initialized");

builder.ConfigureHonuaServices();
profiler.Checkpoint("Services configured");

var app = builder.Build();
profiler.Checkpoint("App built");

app.ConfigureHonuaRequestPipeline();
profiler.Checkpoint("Pipeline configured");

StartupMetricsService.RecordStartupComplete(app.Logger);
profiler.LogResults(app.Logger);

app.Run();
```

**Output:**
```
=== Startup Performance Profile ===
Total startup time: 1847ms
Process runtime: 1952ms
Checkpoint timings:
  [   134ms] (+  134ms) Builder created
  [   289ms] (+  155ms) Builder initialized
  [  1234ms] (+  945ms) Services configured
  [  1653ms] (+  419ms) App built
  [  1847ms] (+  194ms) Pipeline configured
Slowest operations:
  Services configured: 945ms
  App built: 419ms
  Builder initialized: 155ms
====================================
```

### 5. Warmup Health Check

The `WarmupHealthCheck` triggers lazy service initialization on first health check.

**Implementation:**
```csharp
services.AddSingleton<IWarmupService, MetadataCacheWarmupService>();
services.AddHealthChecks()
    .AddCheck<WarmupHealthCheck>("warmup", tags: new[] { "ready" });
```

**Kubernetes Readiness Probe:**
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
  timeoutSeconds: 3
  failureThreshold: 3
```

### 6. Dockerfile Optimizations

The `Dockerfile.lite` includes several .NET runtime optimizations:

```dockerfile
ENV DOTNET_ReadyToRun=1                   # Pre-compiled for fast startup
ENV DOTNET_TieredPGO=1                    # Faster JIT with profile-guided optimization
ENV DOTNET_TC_QuickJitForLoops=1          # Fast loop compilation
ENV DOTNET_TieredCompilation_QuickJit=1   # Quick initial compilation
ENV DOTNET_gcServer=0                     # Workstation GC (faster startup)
ENV DOTNET_GCHeapCount=2                  # Limit heap count
ENV DOTNET_ThreadPool_UnfairSemaphoreSpinLimit=0  # Faster thread pool startup
```

## Configuration

### Connection Pool Warmup

Configure in `appsettings.json`:

```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true,
    "EnableInDevelopment": false,
    "StartupDelayMs": 1000,
    "MaxConcurrentWarmups": 3,
    "MaxDataSources": 10,
    "TimeoutMs": 5000
  }
}
```

**Options:**

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `true` | Enable/disable connection pool warmup |
| `EnableInDevelopment` | `false` | Run warmup in development environment |
| `StartupDelayMs` | `1000` | Delay before starting warmup (allows app to start accepting requests) |
| `MaxConcurrentWarmups` | `3` | Maximum number of concurrent warmup operations |
| `MaxDataSources` | `10` | Maximum number of data sources to warm up |
| `TimeoutMs` | `5000` | Timeout for each warmup operation |

### Environment Variables (Docker/Kubernetes)

```yaml
env:
  # Connection pool warmup
  - name: ConnectionPoolWarmup__Enabled
    value: "true"
  - name: ConnectionPoolWarmup__StartupDelayMs
    value: "500"

  # .NET runtime optimizations
  - name: DOTNET_ReadyToRun
    value: "1"
  - name: DOTNET_TieredPGO
    value: "1"
  - name: DOTNET_gcServer
    value: "0"
```

## Usage

### Measuring Cold Start Time

Use the `StartupProfiler` in your `Program.cs`:

```csharp
var profiler = new StartupProfiler();

// ... your startup code with checkpoints ...

profiler.LogResults(app.Logger);
```

### Custom Warmup Services

Implement `IWarmupService` for custom warmup logic:

```csharp
public class MyCustomWarmupService : IWarmupService
{
    private readonly IMyHeavyService _heavyService;
    private readonly ILogger<MyCustomWarmupService> _logger;

    public MyCustomWarmupService(
        IMyHeavyService heavyService,
        ILogger<MyCustomWarmupService> logger)
    {
        _heavyService = heavyService;
        _logger = logger;
    }

    public async Task WarmupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Warming up custom service");

        // Pre-load data, establish connections, etc.
        await _heavyService.InitializeAsync(cancellationToken);

        _logger.LogInformation("Custom service warmup complete");
    }
}

// Register in DI:
services.AddSingleton<IWarmupService, MyCustomWarmupService>();
```

## Monitoring and Profiling

### Startup Metrics

The `StartupMetricsService` logs key metrics:

```
Application startup completed in 1847ms
Memory usage at startup: 78.45 MB
GC collections during startup: Gen0=12, Gen1=3, Gen2=1
```

### Connection Pool Warmup Metrics

Warmup service logs detailed timing:

```
Starting connection pool warmup...
Warming up 5 connection pools
Warmed up connection pool for data source 'postgres-main' (postgis) in 234ms
Warmed up connection pool for data source 'mysql-aux' (mysql) in 189ms
Connection pool warmup completed in 456ms
```

### Profiling Output

The `StartupProfiler` identifies bottlenecks:

```
Slowest operations:
  Services configured: 945ms
  App built: 419ms
  Builder initialized: 155ms
```

## Troubleshooting

### Cold Start Still Slow

**Symptom:** Cold start takes longer than expected (>5s)

**Diagnosis:**
1. Enable startup profiling to identify bottlenecks
2. Check logs for warmup failures
3. Verify ReadyToRun compilation is enabled
4. Check database connectivity (slow network?)

**Solutions:**
```bash
# 1. Enable detailed startup profiling
export ASPNETCORE_ENVIRONMENT=Development

# 2. Check warmup status
curl http://localhost:8080/health/ready

# 3. Review startup logs
docker logs <container-id> | grep "startup\|warmup"

# 4. Test database connectivity
docker exec <container-id> curl http://localhost:8080/health/ready?tag=database
```

### Warmup Failures

**Symptom:** Logs show warmup errors

**Diagnosis:**
- Check connection strings
- Verify network connectivity to databases
- Check timeout settings

**Solutions:**
```json
{
  "ConnectionPoolWarmup": {
    "TimeoutMs": 10000,  // Increase timeout
    "MaxConcurrentWarmups": 1  // Reduce concurrency
  }
}
```

### High Memory Usage

**Symptom:** Memory usage higher than expected

**Diagnosis:**
- Check GC mode (Server vs Workstation)
- Review lazy service configuration
- Check for memory leaks in warmup services

**Solutions:**
```dockerfile
# Use Workstation GC for lower memory usage
ENV DOTNET_gcServer=0
ENV DOTNET_GCHeapCount=2
```

### First Request Slow

**Symptom:** First request takes several seconds

**Diagnosis:**
- Check if warmup health check is configured
- Verify lazy services are being warmed up
- Check for JIT compilation delays

**Solutions:**
```csharp
// Ensure warmup health check is registered
services.AddHealthChecks()
    .AddCheck<WarmupHealthCheck>("warmup", tags: new[] { "ready" });

// Configure Kubernetes to wait for warmup
readinessProbe:
  httpGet:
    path: /health/ready
  initialDelaySeconds: 5
```

## Best Practices

### 1. Use Readiness Probes

Always configure readiness probes to wait for warmup:

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

### 2. Profile in Production-like Environment

Test cold start performance in an environment that matches production:

```bash
# Build production image
docker build -f Dockerfile.lite -t honua:test .

# Run with production settings
docker run --rm \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionPoolWarmup__Enabled=true \
  -p 8080:8080 \
  honua:test
```

### 3. Monitor Warmup Completion

Track warmup status via health checks:

```bash
# Check warmup status
curl http://localhost:8080/health/ready | jq '.checks[] | select(.name == "warmup")'
```

### 4. Lazy Load Heavy Services

Identify and lazy-load heavy services:

```csharp
// Heavy services that should be lazy-loaded:
// - Cloud SDK clients (AWS, Azure, Google)
// - OData EDM model builders
// - Large cache pre-loading
// - External API clients

services.AddLazySingleton<ICloudStorageClient, CloudStorageClient>();
services.AddLazySingleton<IODataModelBuilder, ODataModelBuilder>();
```

### 5. Disable Warmup in Development

Avoid warmup overhead during development:

```json
{
  "ConnectionPoolWarmup": {
    "EnableInDevelopment": false
  }
}
```

### 6. Set Appropriate Timeouts

Balance between thorough warmup and startup time:

```json
{
  "ConnectionPoolWarmup": {
    "TimeoutMs": 5000,  // 5s per connection
    "MaxDataSources": 10,  // Limit total warmup time
    "StartupDelayMs": 1000  // Let app start first
  }
}
```

## Performance Targets

| Metric | Before Optimization | After Optimization | Target |
|--------|---------------------|-------------------|--------|
| Cold start time | 5-10s | 2-4s | <3s |
| First request latency | 2-3s | <500ms | <500ms |
| Memory at startup | 150MB | 80MB | <100MB |
| Warmup completion | N/A | 3-5s | <5s |

## Summary

Cold start optimization is achieved through:

1. **Non-blocking warmup** - Connection pools and Redis initialized in background
2. **Lazy loading** - Heavy services loaded only when needed
3. **ReadyToRun compilation** - Pre-compiled code for faster startup
4. **Workstation GC** - Lower memory footprint and faster startup
5. **Startup profiling** - Identify and eliminate bottlenecks
6. **Health check integration** - Trigger warmup before receiving traffic

These optimizations reduce cold start time from 5-10 seconds to 2-4 seconds, making Honua Server suitable for serverless deployments.
