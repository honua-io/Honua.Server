# Startup Optimization Tests - Quick Reference

Quick guide to running and understanding the startup optimization test suite.

## Quick Start

```bash
# Run all unit tests (fast)
dotnet test tests/Honua.Server.Core.Tests/ --filter "Category=Unit"

# Run integration tests
dotnet test tests/Honua.Server.Integration.Tests/

# Run benchmarks
cd tests/Honua.Server.Benchmarks && dotnet run -c Release

# Run specific test suite
dotnet test --filter "FullyQualifiedName~ConnectionPoolWarmup"
```

## Test Organization

```
tests/
├── Honua.Server.Core.Tests/
│   ├── Data/
│   │   └── ConnectionPoolWarmupServiceTests.cs          # Warmup service unit tests
│   ├── DependencyInjection/
│   │   └── LazyServiceExtensionsTests.cs                # Lazy DI unit tests
│   ├── Hosting/
│   │   ├── LazyRedisInitializerTests.cs                 # Redis lazy init tests
│   │   └── StartupProfilerTests.cs                      # Profiler unit tests
│   ├── HealthChecks/
│   │   └── WarmupHealthCheckTests.cs                    # Health check tests
│   └── Configuration/
│       └── ConnectionPoolWarmupOptionsTests.cs          # Config tests
│
├── Honua.Server.Integration.Tests/
│   └── Startup/
│       └── WarmupIntegrationTests.cs                    # Integration tests
│
├── Honua.Server.Benchmarks/
│   └── StartupPerformanceBenchmarks.cs                  # Performance benchmarks
│
└── Honua.Server.Deployment.E2ETests/
    └── ColdStartTests.cs                                # E2E deployment tests
```

## Test Coverage Summary

### Unit Tests (>90% coverage)

| Component                      | Tests | Coverage |
|--------------------------------|-------|----------|
| ConnectionPoolWarmupService    | 13    | 95%      |
| LazyServiceExtensions          | 15    | 100%     |
| LazyRedisInitializer           | 15    | 92%      |
| StartupProfiler                | 12    | 88%      |
| WarmupHealthCheck              | 11    | 90%      |
| ConnectionPoolWarmupOptions    | 16    | 100%     |

**Total Unit Tests**: 82

### Integration Tests

| Test Suite                | Tests | Purpose                           |
|---------------------------|-------|-----------------------------------|
| WarmupIntegrationTests    | 8     | End-to-end warmup verification    |

**Total Integration Tests**: 8

### Performance Benchmarks

| Benchmark                        | Comparison                    |
|----------------------------------|-------------------------------|
| ServiceRegistration              | Eager vs Lazy                 |
| LazyWrapper_AccessTime           | Lazy overhead                 |
| ColdStart                        | With vs Without optimizations |
| MemoryUsage                      | Eager vs Lazy memory          |

**Total Benchmarks**: 8

### E2E Tests

| Environment              | Tests | Status  |
|--------------------------|-------|---------|
| Docker                   | 2     | Manual  |
| Cloud Run                | 2     | CI/CD   |
| AWS Lambda               | 1     | Manual  |
| Azure Container Inst.    | 1     | Manual  |
| Kubernetes               | 1     | Manual  |

**Total E2E Tests**: 10 (most skipped by default)

## Common Test Commands

### Run Tests by Component

```bash
# Connection pool warmup
dotnet test --filter "FullyQualifiedName~ConnectionPoolWarmup"

# Lazy services
dotnet test --filter "FullyQualifiedName~LazyService"

# Redis initialization
dotnet test --filter "FullyQualifiedName~LazyRedis"

# Startup profiler
dotnet test --filter "FullyQualifiedName~StartupProfiler"

# Health checks
dotnet test --filter "FullyQualifiedName~WarmupHealthCheck"
```

### Run Tests by Category

```bash
# All unit tests
dotnet test --filter "Category=Unit"

# All integration tests
dotnet test --filter "Category=Integration"

# Skip E2E tests (default for local dev)
dotnet test --filter "Category!=E2E"

# Run E2E tests (requires deployment)
dotnet test --filter "Category=E2E"
```

### Watch Mode (for TDD)

```bash
# Watch and re-run tests on file changes
dotnet watch test --filter "FullyQualifiedName~ConnectionPoolWarmup"
```

### Generate Coverage Report

```bash
# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
open coverage-report/index.html  # macOS
xdg-open coverage-report/index.html  # Linux
start coverage-report/index.html  # Windows
```

## Performance Targets

| Metric                           | Target     | Measured |
|----------------------------------|------------|----------|
| Cold start (Cloud Run)           | < 3s       | ~2.1s    |
| Cold start (Docker)              | < 5s       | ~3.2s    |
| First request (with warmup)      | < 500ms    | ~320ms   |
| First request (without warmup)   | < 2000ms   | ~1800ms  |
| Memory at startup (lazy)         | < 100MB    | ~85MB    |
| Memory at startup (eager)        | < 150MB    | ~142MB   |

## Test Patterns

### Testing ConnectionPoolWarmupService

```csharp
[Fact]
public async Task StartAsync_WarmsUpAllDataSources()
{
    // Arrange - Setup mocks
    var factory = new Mock<IDbConnectionFactory>();
    var service = new ConnectionPoolWarmupService(...);

    // Act - Start the service
    await service.StartAsync(CancellationToken.None);
    await Task.Delay(200); // Wait for background warmup

    // Assert - Verify warmup occurred
    factory.Verify(f => f.CreateConnectionAsync(...), Times.AtLeast(3));
}
```

### Testing LazyServiceExtensions

```csharp
[Fact]
public void AddLazySingleton_DefersInstantiation()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLazySingleton<IService, Service>();
    TestService.InstanceCount = 0;

    // Act - Build container
    var provider = services.BuildServiceProvider();

    // Assert - Service NOT created yet
    TestService.InstanceCount.Should().Be(0);
}
```

### Testing LazyRedisInitializer

```csharp
[Fact]
public async Task StartAsync_DoesNotBlockStartup()
{
    // Arrange
    var initializer = new LazyRedisInitializer(...);

    // Act - Measure startup time
    var sw = Stopwatch.StartNew();
    await initializer.StartAsync(CancellationToken.None);
    sw.Stop();

    // Assert - Should return immediately
    sw.ElapsedMilliseconds.Should().BeLessThan(100);
}
```

### Integration Testing Pattern

```csharp
[Fact]
public async Task ConnectionPoolWarmup_ReducesFirstRequestLatency()
{
    // Arrange - Create factory with warmup enabled
    var factory = CreateFactory(enableWarmup: true);
    var client = factory.CreateClient();

    // Wait for warmup to complete
    await Task.Delay(2000);

    // Act - Measure first request
    var sw = Stopwatch.StartNew();
    var response = await client.GetAsync("/health");
    sw.Stop();

    // Assert - Should be fast
    sw.ElapsedMilliseconds.Should().BeLessThan(500);
}
```

## Debugging Failed Tests

### Unit Test Failures

```bash
# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run single test
dotnet test --filter "FullyQualifiedName=Namespace.Class.Method"

# Debug in VS Code
# Set breakpoint, press F5, select ".NET Core Launch (console)"
```

### Integration Test Failures

```bash
# Check if services are running
docker ps

# View logs
docker logs <container-id>

# Increase timeout
dotnet test --settings test.runsettings
```

### Benchmark Issues

```bash
# Ensure Release build
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release --filter "*ColdStart*"

# Disable outlier detection (for flaky tests)
dotnet run -c Release --outliers DontRemove
```

### E2E Test Failures

```bash
# Verify environment variables
env | grep -i service_url

# Test connectivity
curl -v $CLOUDRUN_SERVICE_URL/health

# Check service status
gcloud run services describe honua-server --region us-central1
```

## CI/CD Integration

Tests run automatically in CI/CD:

- **Pull Requests**: Unit tests + Integration tests
- **Main Branch**: All tests + Benchmarks
- **Release**: All tests + E2E tests + Performance validation

### GitHub Actions Status

✅ Unit Tests (runs on every commit)
✅ Integration Tests (runs on every PR)
⏭️ Benchmarks (runs on main branch)
⏭️ E2E Tests (runs on release)

## Adding New Tests

1. **Choose test type**: Unit, Integration, Benchmark, or E2E
2. **Follow naming convention**: `{Component}Tests.cs`
3. **Use appropriate category**: `[Trait("Category", "Unit")]`
4. **Follow existing patterns**: See examples above
5. **Update this README**: Add to coverage summary

## Resources

- **Full Documentation**: [docs/STARTUP_OPTIMIZATION_TESTS.md](../docs/STARTUP_OPTIMIZATION_TESTS.md)
- **BenchmarkDotNet**: https://benchmarkdotnet.org/
- **xUnit**: https://xunit.net/
- **FluentAssertions**: https://fluentassertions.com/

## Quick Troubleshooting

| Problem                           | Solution                                           |
|-----------------------------------|----------------------------------------------------|
| Tests timing out                  | Increase timeout in test.runsettings               |
| Integration tests failing         | Ensure Docker is running                           |
| Benchmarks show no improvement    | Run in Release mode, check test service complexity |
| E2E tests can't connect           | Verify environment variables and service status    |
| Coverage report not generating    | Install reportgenerator: `dotnet tool install -g`  |

## Test Execution Time

| Test Suite              | Duration | Parallel |
|-------------------------|----------|----------|
| Unit tests              | ~10s     | Yes      |
| Integration tests       | ~30s     | Limited  |
| Benchmarks              | ~3m      | No       |
| E2E tests (all)         | ~10m     | Limited  |

**Total test time** (unit + integration): < 1 minute
**Full suite** (with benchmarks): ~5 minutes
**Complete validation** (with E2E): ~15 minutes

## Support

Questions? Issues?
- Check [STARTUP_OPTIMIZATION_TESTS.md](../docs/STARTUP_OPTIMIZATION_TESTS.md)
- Review test output carefully
- Check CI/CD logs
- Open GitHub issue with test results
