# Startup Optimization Tests

This document describes the comprehensive test suite for HonuaIO's startup optimizations, including connection pool warmup and lazy service loading.

## Table of Contents

- [Overview](#overview)
- [Test Categories](#test-categories)
- [Running the Tests](#running-the-tests)
- [Unit Tests](#unit-tests)
- [Integration Tests](#integration-tests)
- [Performance Benchmarks](#performance-benchmarks)
- [E2E Tests](#e2e-tests)
- [Configuration Tests](#configuration-tests)
- [CI/CD Integration](#cicd-integration)
- [Performance Targets](#performance-targets)
- [Troubleshooting](#troubleshooting)

## Overview

The startup optimization test suite validates that:

1. **Connection Pool Warmup** reduces first-request latency in serverless deployments
2. **Lazy Service Loading** reduces cold start time and memory usage
3. **Redis Lazy Initialization** doesn't block application startup
4. **Startup Profiler** accurately tracks performance bottlenecks
5. **Warmup Health Checks** correctly report application readiness

## Test Categories

### Unit Tests (Fast, Isolated)

- **Location**: `tests/Honua.Server.Core.Tests/`
- **Purpose**: Test individual components in isolation
- **Execution Time**: < 5 seconds
- **Dependencies**: None (uses mocks)

### Integration Tests (Medium, Real Dependencies)

- **Location**: `tests/Honua.Server.Integration.Tests/`
- **Purpose**: Test components working together
- **Execution Time**: 5-30 seconds
- **Dependencies**: In-memory services, test containers

### Performance Benchmarks (Slow, Comparative)

- **Location**: `tests/Honua.Server.Benchmarks/`
- **Purpose**: Measure and compare performance
- **Execution Time**: 1-5 minutes
- **Dependencies**: BenchmarkDotNet

### E2E Tests (Very Slow, Full Stack)

- **Location**: `tests/Honua.Server.Deployment.E2ETests/`
- **Purpose**: Test in real deployment environments
- **Execution Time**: 1-10 minutes
- **Dependencies**: Actual deployments (Docker, Cloud Run, etc.)

## Running the Tests

### All Unit Tests

```bash
dotnet test tests/Honua.Server.Core.Tests/ --filter "Category=Unit"
```

### All Integration Tests

```bash
dotnet test tests/Honua.Server.Integration.Tests/ --filter "Category=Integration"
```

### Performance Benchmarks

```bash
cd tests/Honua.Server.Benchmarks
dotnet run -c Release
```

### E2E Tests (Requires Deployment)

```bash
# Set environment variables for deployed services
export SERVICE_URL="https://your-service-url"
dotnet test tests/Honua.Server.Deployment.E2ETests/ --filter "Category=E2E"
```

### Specific Test Suites

```bash
# Connection pool warmup tests only
dotnet test --filter "FullyQualifiedName~ConnectionPoolWarmupServiceTests"

# Lazy service tests only
dotnet test --filter "FullyQualifiedName~LazyServiceExtensionsTests"

# Redis initialization tests only
dotnet test --filter "FullyQualifiedName~LazyRedisInitializerTests"

# Startup profiler tests only
dotnet test --filter "FullyQualifiedName~StartupProfilerTests"

# Health check tests only
dotnet test --filter "FullyQualifiedName~WarmupHealthCheckTests"
```

### Run with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Unit Tests

### ConnectionPoolWarmupServiceTests

**Location**: `tests/Honua.Server.Core.Tests/Data/ConnectionPoolWarmupServiceTests.cs`

**Coverage**:
- Service starts and stops correctly
- Warmup can be disabled via configuration
- Warmup respects environment (dev vs production)
- Startup delay is honored
- Maximum concurrency is enforced
- Connection failures don't crash the app
- Timeout handling works correctly
- Maximum data sources limit is respected
- Metadata initialization is awaited
- Empty data source lists are handled gracefully

**Key Test Cases**:

```csharp
[Fact]
public async Task StartAsync_WhenDisabled_DoesNotWarmUp()
{
    // Verifies warmup can be turned off
}

[Fact]
public async Task StartAsync_RespectsMaxConcurrency()
{
    // Ensures parallel warmup doesn't overwhelm the system
}

[Fact]
public async Task StartAsync_HandlesConnectionFailures_DoesNotThrow()
{
    // Critical: failures shouldn't prevent app startup
}
```

### LazyServiceExtensionsTests

**Location**: `tests/Honua.Server.Core.Tests/DependencyInjection/LazyServiceExtensionsTests.cs`

**Coverage**:
- Services are registered correctly
- Instantiation is deferred until first access
- Singleton behavior is maintained
- Factory-based registration works
- Lazy<T> wrapper is available
- LazyService<T> wrapper works correctly
- Multiple lazy instances share the same underlying service

**Key Test Cases**:

```csharp
[Fact]
public void AddLazySingleton_DefersInstantiation()
{
    // Proves services aren't created at startup
}

[Fact]
public void AddLazySingleton_ReturnsSameInstanceOnMultipleAccesses()
{
    // Ensures singleton semantics are preserved
}
```

### LazyRedisInitializerTests

**Location**: `tests/Honua.Server.Core.Tests/Hosting/LazyRedisInitializerTests.cs`

**Coverage**:
- Missing Redis configuration is handled gracefully
- Different log levels for different environments
- Startup is not blocked by Redis connection
- Background initialization happens after delay
- Connection failures don't crash the app
- Dispose is idempotent
- Null parameter checks

**Key Test Cases**:

```csharp
[Fact]
public async Task StartAsync_DoesNotBlockStartup()
{
    // Critical: Redis connection happens in background
}

[Fact]
public async Task InitializeRedisAsync_HandlesConnectionFailureGracefully()
{
    // App continues even if Redis is unavailable
}
```

### StartupProfilerTests

**Location**: `tests/Honua.Server.Core.Tests/Hosting/StartupProfilerTests.cs`

**Coverage**:
- Checkpoints are recorded correctly
- Timestamps are increasing
- Results are logged properly
- Slowest operations are identified
- Thread safety under concurrent access
- Metrics service logs startup time and memory

**Key Test Cases**:

```csharp
[Fact]
public void Checkpoint_RecordsIncreasingTimestamps()
{
    // Verifies timing accuracy
}

[Fact]
public void Checkpoints_IsThreadSafe()
{
    // Important for multi-threaded startup
}
```

### WarmupHealthCheckTests

**Location**: `tests/Honua.Server.Core.Tests/HealthChecks/WarmupHealthCheckTests.cs`

**Coverage**:
- First health check triggers warmup
- Status transitions from Degraded to Healthy
- Warmup only runs once
- Multiple services are warmed up
- Failures are handled gracefully
- Cancellation tokens are propagated
- Metadata cache warmup works

**Key Test Cases**:

```csharp
[Fact]
public async Task CheckHealthAsync_FirstInvocation_ReturnsDegraded()
{
    // Kubernetes shouldn't route traffic during warmup
}

[Fact]
public async Task CheckHealthAsync_OnlyTriggersWarmupOnce()
{
    // Prevents duplicate warmup on subsequent health checks
}
```

## Integration Tests

### WarmupIntegrationTests

**Location**: `tests/Honua.Server.Integration.Tests/Startup/WarmupIntegrationTests.cs`

**Coverage**:
- Connection pool warmup reduces latency in practice
- Health checks work with real HTTP requests
- Startup profiler integrates correctly
- Lazy Redis doesn't block real startup
- Warmup health check transitions work
- Cold start completes within timeout
- Concurrent requests during warmup all succeed
- App works with warmup disabled

**Key Test Cases**:

```csharp
[Fact]
public async Task ConnectionPoolWarmup_ReducesFirstRequestLatency()
{
    // Proves warmup has measurable benefit
}

[Fact]
public async Task ColdStart_CompletesUnderTimeout()
{
    // Ensures overall startup time is acceptable
}
```

**Running Integration Tests**:

```bash
# Run all integration tests
dotnet test tests/Honua.Server.Integration.Tests/

# Run only warmup tests
dotnet test tests/Honua.Server.Integration.Tests/ --filter "FullyQualifiedName~Warmup"
```

## Performance Benchmarks

### StartupPerformanceBenchmarks

**Location**: `tests/Honua.Server.Benchmarks/StartupPerformanceBenchmarks.cs`

**Benchmarks**:

1. **ServiceRegistration_WithoutLazy** (Baseline)
   - Standard eager service registration

2. **ServiceRegistration_WithLazy**
   - Lazy service registration
   - Expected: Lower startup time

3. **LazyWrapper_AccessTime**
   - Overhead of Lazy<T> wrapper

4. **LazyService_AccessTime**
   - Overhead of LazyService<T> wrapper

5. **StartupProfiler_CheckpointOverhead**
   - Cost of profiling checkpoints

6. **ColdStart_Baseline**
   - Cold start without optimizations

7. **ColdStart_WithOptimizations**
   - Cold start with lazy loading
   - Expected: 20-40% faster

8. **MemoryUsage_EagerLoading** vs **MemoryUsage_LazyLoading**
   - Memory comparison
   - Expected: Lazy uses less memory when only partial services are needed

**Running Benchmarks**:

```bash
cd tests/Honua.Server.Benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release --filter "*ColdStart*"

# Export results
dotnet run -c Release --exporters json markdown
```

**Sample Output**:

```
| Method                              | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated |
|------------------------------------ |----------:|---------:|---------:|------:|-------:|----------:|
| ServiceRegistration_WithoutLazy     | 150.2 ms  | 2.1 ms   | 1.9 ms   | 1.00  | 500.0  | 2048 KB   |
| ServiceRegistration_WithLazy        | 95.3 ms   | 1.4 ms   | 1.2 ms   | 0.63  | 300.0  | 1224 KB   |
```

## E2E Tests

### ColdStartTests

**Location**: `tests/Honua.Server.Deployment.E2ETests/ColdStartTests.cs`

**Test Environments**:
- Docker (local and deployed)
- Google Cloud Run
- AWS Lambda
- Azure Container Instances
- Kubernetes

**Coverage**:
- Docker cold start < 5 seconds
- Cloud Run cold start < 3 seconds
- Lambda cold start < 2 seconds
- First request after warmup < 500ms
- Comparison: warmup vs no warmup
- Concurrent load during cold start
- Kubernetes readiness probe behavior
- Memory usage during cold start

**Running E2E Tests**:

Most E2E tests are skipped by default because they require actual deployments.

```bash
# Skip E2E tests (default)
dotnet test --filter "Category!=E2E"

# Run E2E tests (requires environment variables)
export SERVICE_URL="https://your-cloudrun-service.run.app"
export CLOUDRUN_SERVICE_URL="https://your-cloudrun-service.run.app"
dotnet test --filter "Category=E2E"
```

**Required Environment Variables**:

```bash
# For Cloud Run tests
export CLOUDRUN_SERVICE_URL="https://honua-xyz.run.app"

# For Lambda tests
export LAMBDA_FUNCTION_URL="https://xyz.lambda-url.us-east-1.on.aws"

# For Azure tests
export ACI_INSTANCE_URL="https://honua.azurecontainer.io"

# For comparison tests
export SERVICE_URL_WITH_WARMUP="https://warmup.run.app"
export SERVICE_URL_WITHOUT_WARMUP="https://no-warmup.run.app"

# For Kubernetes tests
export K8S_POD_URL="http://honua-pod.default.svc.cluster.local:8080"
```

## Configuration Tests

### ConnectionPoolWarmupOptionsTests

**Location**: `tests/Honua.Server.Core.Tests/Configuration/ConnectionPoolWarmupOptionsTests.cs`

**Coverage**:
- Default values are correct
- Loading from appsettings.json
- Loading from environment variables
- Partial configuration uses defaults
- Invalid values are handled gracefully
- Environment variables override JSON
- Different deployment scenarios (production, development, serverless)

**Configuration Examples**:

```json
// Production configuration
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

// Serverless configuration (Cloud Run, Lambda)
{
  "ConnectionPoolWarmup": {
    "Enabled": true,
    "StartupDelayMs": 0,
    "MaxConcurrentWarmups": 5,
    "MaxDataSources": 5,
    "TimeoutMs": 3000
  }
}

// Development configuration
{
  "ConnectionPoolWarmup": {
    "Enabled": false
  }
}
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Startup Optimization Tests

on:
  pull_request:
    paths:
      - 'src/Honua.Server.Core/Data/ConnectionPoolWarmupService.cs'
      - 'src/Honua.Server.Core/DependencyInjection/LazyServiceExtensions.cs'
      - 'src/Honua.Server.Core/Hosting/**'
      - 'tests/**/*Startup*.cs'

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Unit Tests
        run: dotnet test tests/Honua.Server.Core.Tests/ --filter "Category=Unit"

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Run Integration Tests
        run: dotnet test tests/Honua.Server.Integration.Tests/ --filter "Category=Integration"

  benchmarks:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Run Benchmarks
        run: |
          cd tests/Honua.Server.Benchmarks
          dotnet run -c Release --exporters json
      - name: Upload Benchmark Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results
          path: tests/Honua.Server.Benchmarks/BenchmarkDotNet.Artifacts/

  e2e-cloudrun:
    runs-on: ubuntu-latest
    needs: [unit-tests, integration-tests]
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v3
      - name: Deploy to Cloud Run
        run: ./scripts/deploy-cloudrun.sh
      - name: Run E2E Tests
        env:
          CLOUDRUN_SERVICE_URL: ${{ secrets.CLOUDRUN_SERVICE_URL }}
        run: dotnet test tests/Honua.Server.Deployment.E2ETests/ --filter "Category=E2E"
```

### Local Pre-commit Hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "Running startup optimization tests..."

# Run fast unit tests
dotnet test tests/Honua.Server.Core.Tests/ \
  --filter "FullyQualifiedName~ConnectionPoolWarmup|LazyService|StartupProfiler|WarmupHealthCheck" \
  --no-build --verbosity quiet

if [ $? -ne 0 ]; then
  echo "Unit tests failed. Commit aborted."
  exit 1
fi

echo "All tests passed!"
```

## Performance Targets

### Cold Start Times

| Environment           | Target    | With Warmup | Without Warmup |
|-----------------------|-----------|-------------|----------------|
| Local Docker          | < 5s      | ~3s         | ~5s            |
| Cloud Run             | < 3s      | ~2s         | ~4s            |
| AWS Lambda            | < 2s      | ~1.5s       | ~3s            |
| Azure Container Inst. | < 4s      | ~3s         | ~5s            |
| Kubernetes            | < 5s      | ~3s         | ~6s            |

### First Request Latency

| Scenario                    | Target    |
|-----------------------------|-----------|
| After warmup                | < 500ms   |
| Without warmup              | < 2000ms  |
| With lazy services          | < 300ms   |

### Memory Usage

| Configuration           | Target    |
|-------------------------|-----------|
| Startup (eager loading) | ~150MB    |
| Startup (lazy loading)  | ~100MB    |
| After first request     | ~200MB    |

## Troubleshooting

### Tests Failing Locally

**Problem**: Integration tests timeout or fail locally

**Solution**:
```bash
# Ensure you have enough resources
docker system prune -a

# Increase test timeout
dotnet test --settings test.runsettings
```

**test.runsettings**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <ResultsDirectory>./TestResults</ResultsDirectory>
    <TestSessionTimeout>300000</TestSessionTimeout>
  </RunConfiguration>
</RunSettings>
```

### Benchmarks Show No Improvement

**Problem**: Benchmarks don't show expected performance improvement

**Possible Causes**:
1. Services are too lightweight (no real initialization cost)
2. Running in Debug mode instead of Release
3. Hardware throttling (thermal, power)

**Solution**:
```bash
# Always run benchmarks in Release mode
dotnet run -c Release --project tests/Honua.Server.Benchmarks

# Use real-world services for more accurate results
# Increase initialization time in test services
```

### E2E Tests Can't Connect

**Problem**: E2E tests fail with connection errors

**Solution**:
```bash
# Verify environment variables
echo $CLOUDRUN_SERVICE_URL

# Test connectivity manually
curl -v $CLOUDRUN_SERVICE_URL/health

# Check service logs
gcloud logging read "resource.type=cloud_run_revision"

# Verify firewall rules allow your IP
```

### Warmup Not Triggering

**Problem**: Connection pool warmup doesn't seem to run

**Solution**:
1. Check configuration:
```json
{
  "ConnectionPoolWarmup": {
    "Enabled": true
  }
}
```

2. Check logs:
```bash
# Should see: "Starting connection pool warmup..."
dotnet run | grep -i warmup
```

3. Verify environment:
```csharp
// Warmup is skipped in Development by default
"EnableInDevelopment": true
```

## Test Maintenance

### Adding New Tests

When adding new startup optimizations:

1. **Unit tests** for the core logic
2. **Integration tests** to verify it works with other components
3. **Benchmarks** to measure performance impact
4. **E2E tests** to validate in real deployments
5. **Configuration tests** for new options

### Test Coverage Goals

- **Unit Tests**: > 90% line coverage
- **Integration Tests**: All major flows
- **Benchmarks**: All optimizations compared to baseline
- **E2E Tests**: All supported deployment platforms

### Running Coverage Reports

```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report

# Open report
open coverage-report/index.html
```

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Testcontainers Documentation](https://www.testcontainers.org/)

## Support

For issues or questions about the test suite:

1. Check test output for detailed error messages
2. Review logs in CI/CD pipelines
3. Consult the troubleshooting section above
4. Open an issue on GitHub with test results attached
