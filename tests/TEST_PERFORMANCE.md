# Test Performance Optimization Guide

**Copyright (c) 2025 HonuaIO**
**Licensed under the Elastic License 2.0**

**Last Updated:** 2025-11-11
**Version:** 2.0

---

## Table of Contents

1. [Overview](#overview)
2. [Current Performance Metrics](#current-performance-metrics)
3. [Container Optimization](#container-optimization)
4. [Parallelization Strategy](#parallelization-strategy)
5. [Optimization Techniques](#optimization-techniques)
6. [Performance Monitoring](#performance-monitoring)
7. [CI/CD Optimization](#cicd-optimization)
8. [Troubleshooting Slow Tests](#troubleshooting-slow-tests)

---

## Overview

This guide documents the performance optimization strategies implemented in the Honua.Server test infrastructure and provides guidance for maintaining and improving test performance.

### Performance Goals

- **Local Development:** < 1 minute for full test suite
- **CI/CD:** < 2 minutes for full test suite
- **Individual Test:** < 5 seconds average
- **Container Startup:** < 30 seconds (one-time cost)

### Key Achievements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Container Count** | 39+ | 3 | 87% reduction |
| **Startup Time** | 5-10 min | 30-60 sec | 80-90% faster |
| **Full Suite Time** | 15-20 min | 30-60 sec | 95% faster |
| **Parallel Tests** | Disabled | Enabled | 3-4x speedup |
| **Memory Usage** | 8GB+ | 2-4GB | 50-75% reduction |

---

## Current Performance Metrics

### Test Suite Breakdown

```
Total Tests: 200+
├── Unit Tests: ~150 (75%)
│   ├── Average Time: 10-50ms
│   └── Total Time: ~5 seconds
├── Integration Tests: ~45 (22%)
│   ├── Average Time: 100-500ms
│   └── Total Time: ~20 seconds
└── E2E Tests: ~5 (3%)
    ├── Average Time: 2-5 seconds
    └── Total Time: ~10 seconds
```

### Container Metrics

**PostgreSQL Container:**
- Startup Time: 10-15 seconds
- Memory: 100-200MB
- CPU: < 5% (idle)

**MySQL Container:**
- Startup Time: 8-12 seconds
- Memory: 150-250MB
- CPU: < 5% (idle)

**Redis Container:**
- Startup Time: 2-5 seconds
- Memory: 20-50MB
- CPU: < 2% (idle)

**Total Container Overhead:**
- One-time startup: 20-30 seconds
- Memory: 300-500MB
- Cleanup: 2-5 seconds

---

## Container Optimization

### The Problem: Container Proliferation

**Before Optimization:**

Each test class created its own containers:

```csharp
// Bad - Creates 3 containers per test class!
public class WfsTests : IClassFixture<DatabaseFixture>
{
    public WfsTests()
    {
        // New PostgreSQL container
        // New MySQL container
        // New Redis container
    }
}

public class StacTests : IClassFixture<DatabaseFixture>
{
    public StacTests()
    {
        // Another PostgreSQL container
        // Another MySQL container
        // Another Redis container
    }
}

// With 13 test classes = 39 containers!
```

**Issues:**
- 39+ containers created
- 5-10 minute startup time
- Resource exhaustion
- CI failures
- Parallel execution disabled

### The Solution: Shared Containers

**After Optimization:**

All tests share 3 containers via collection fixtures:

```csharp
// Good - Shares 3 containers across all tests!
[Collection("DatabaseCollection")]
public class WfsTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsTests(DatabaseFixture db)  // Shared fixture
    {
        _db = db;
    }
}

[Collection("DatabaseCollection")]
public class StacTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public StacTests(DatabaseFixture db)  // Same shared fixture
    {
        _db = db;
    }
}

// All test classes share same 3 containers!
```

**Benefits:**
- Only 3 containers created
- 30-60 second startup time
- Parallel execution enabled
- CI stable
- 87% reduction in containers

### Implementation: DatabaseCollection

**Collection Definition:**
```csharp
// tests/Honua.Server.Integration.Tests/Collections/DatabaseCollection.cs

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class is never instantiated
    // It exists only to define the shared fixture collection
}
```

**DatabaseFixture:**
```csharp
public sealed class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private MySqlContainer? _mySqlContainer;
    private RedisContainer? _redisContainer;

    public async Task InitializeAsync()
    {
        // Start all containers in parallel
        var postgresTask = StartPostgresAsync();
        var mysqlTask = StartMySqlAsync();
        var redisTask = StartRedisAsync();

        await Task.WhenAll(postgresTask, mysqlTask, redisTask);
    }

    // Containers shared across all tests in collection
}
```

### Container Lifecycle

```
┌─ Test Run Starts
│
├─ First Test Class in Collection
│  ├─ DatabaseFixture.InitializeAsync()
│  │  ├─ Start PostgreSQL (parallel)
│  │  ├─ Start MySQL (parallel)
│  │  └─ Start Redis (parallel)
│  └─ Wait for all containers ready
│
├─ Run All Tests (parallel)
│  ├─ Test Class 1 → uses shared containers
│  ├─ Test Class 2 → uses shared containers
│  ├─ Test Class 3 → uses shared containers
│  └─ ... (all share same 3 containers)
│
└─ Last Test Completes
   └─ DatabaseFixture.DisposeAsync()
      ├─ Stop PostgreSQL
      ├─ Stop MySQL
      └─ Stop Redis
```

### Container Optimization Best Practices

**1. Always Use Collection Fixtures**

```csharp
// Good
[Collection("DatabaseCollection")]
public class MyTests : IClassFixture<DatabaseFixture>
{
}

// Bad - Creates new containers!
public class MyTests
{
    private readonly DatabaseFixture _db = new DatabaseFixture();
}
```

**2. Parallel Container Startup**

```csharp
// Good - Start in parallel
public async Task InitializeAsync()
{
    var tasks = new[]
    {
        StartPostgresAsync(),
        StartMySqlAsync(),
        StartRedisAsync()
    };

    await Task.WhenAll(tasks);
}

// Bad - Sequential startup
public async Task InitializeAsync()
{
    await StartPostgresAsync();
    await StartMySqlAsync();
    await StartRedisAsync();
}
```

**3. Container Image Caching**

Pull images before running tests (CI):

```bash
# Pre-pull images to avoid timeout during test run
docker pull postgis/postgis:16-3.4
docker pull mysql:8.0
docker pull redis:7-alpine

# Then run tests
dotnet test
```

---

## Parallelization Strategy

### Parallel Execution Overview

xUnit runs tests in parallel at two levels:

1. **Assembly Level:** Multiple test projects run concurrently
2. **Collection Level:** Test classes in different collections run concurrently
3. **Class Level:** Tests within a class run serially by default

### Configuration

**Enable Parallel Execution:**

```xml
<!-- In test project .csproj -->
<PropertyGroup>
  <ParallelizeAssembly>true</ParallelizeAssembly>
  <ParallelizeTestCollections>true</ParallelizeTestCollections>
  <MaxParallelThreads>0</MaxParallelThreads> <!-- 0 = CPU count -->
</PropertyGroup>
```

**Assembly-Level Configuration:**

```csharp
// In any test file (applies to entire assembly)
[assembly: CollectionBehavior(
    DisableTestParallelization = false,
    MaxParallelThreads = 4)]
```

### Parallelization Patterns

**Pattern 1: Independent Tests (Parallel)**

```csharp
[Collection("DatabaseCollection")]
public class WfsTests : IClassFixture<DatabaseFixture>
{
    // All tests can run in parallel with other test classes
}

[Collection("DatabaseCollection")]  // Same collection = shares resources
public class StacTests : IClassFixture<DatabaseFixture>
{
    // Runs in parallel with WfsTests
}
```

**Pattern 2: Serial Tests (Non-Parallel)**

```csharp
[Collection("Serial")]
public class SerialTests
{
    // Tests in this collection run one at a time
}

[CollectionDefinition("Serial", DisableParallelization = true)]
public class SerialCollection { }
```

**Pattern 3: Mixed Parallelization**

```csharp
// Most tests run in parallel
[Collection("DatabaseCollection")]
public class ParallelTest1 { }

[Collection("DatabaseCollection")]
public class ParallelTest2 { }

// Some tests need serial execution
[Collection("Serial")]
public class SerialTest { }
```

### Parallel Safety

**Thread-Safe Patterns:**

```csharp
[Fact]
public async Task ParallelSafeTest()
{
    // Use unique identifiers
    var testId = Guid.NewGuid();
    var tableName = $"test_{testId:N}";

    // Create isolated test data
    await CreateTable(tableName);

    try
    {
        // Test logic - no conflicts with other tests
        var result = await Query(tableName);
    }
    finally
    {
        // Clean up
        await DropTable(tableName);
    }
}
```

**Not Thread-Safe (Avoid):**

```csharp
[Fact]
public async Task NotParallelSafe()
{
    // Bad - Uses fixed table name
    await CreateTable("test_table");

    // If another test runs concurrently, they conflict!
    var result = await Query("test_table");

    await DropTable("test_table");
}
```

### Optimal Thread Count

**Local Development:**
```bash
# Use all cores
dotnet test -- xUnit.MaxParallelThreads=0

# Or specific count
dotnet test -- xUnit.MaxParallelThreads=4
```

**CI/CD:**
```bash
# Limit threads to prevent resource exhaustion
dotnet test -- xUnit.MaxParallelThreads=2
```

**Determine Optimal Count:**

```python
# Rule of thumb
threads = CPU_cores - 1  # Leave one core for OS/Docker

# For containers
threads = min(CPU_cores - 1, 4)  # Cap at 4 for container stability

# For CI
threads = 2  # Conservative for shared runners
```

---

## Optimization Techniques

### 1. Test Data Isolation

**Use Transactions:**

```csharp
[Fact]
public async Task Test_WithTransaction()
{
    using var conn = new NpgsqlConnection(_db.PostgresConnectionString);
    await conn.OpenAsync();

    using var tx = await conn.BeginTransactionAsync();

    try
    {
        // All operations in transaction
        await InsertData(conn);
        var result = await QueryData(conn);

        result.Should().NotBeNull();

        // Automatic rollback on dispose
    }
    finally
    {
        await tx.RollbackAsync();
    }
}
```

**Use Unique Identifiers:**

```csharp
[Fact]
public async Task Test_WithUniqueData()
{
    var testId = Guid.NewGuid();
    var schema = $"test_{testId:N}";

    await ExecuteSql($"CREATE SCHEMA {schema}");

    try
    {
        await ExecuteSql($"CREATE TABLE {schema}.data (...)");
        // Test logic
    }
    finally
    {
        await ExecuteSql($"DROP SCHEMA {schema} CASCADE");
    }
}
```

### 2. Lazy Initialization

**Avoid Unnecessary Setup:**

```csharp
public class OptimizedTests
{
    private HttpClient? _client;

    private HttpClient Client => _client ??= CreateClient();

    private HttpClient CreateClient()
    {
        // Only create if needed
        return new HttpClient();
    }

    [Fact]
    public void Test1()
    {
        // Doesn't use Client - no initialization overhead
    }

    [Fact]
    public async Task Test2()
    {
        // Uses Client - initialized only for this test
        await Client.GetAsync("/api");
    }
}
```

### 3. Test Data Caching

**Share Read-Only Test Data:**

```csharp
public class TestDataFixture
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static bool _dataLoaded = false;

    public async Task EnsureTestDataAsync()
    {
        if (_dataLoaded) return;

        await _lock.WaitAsync();
        try
        {
            if (_dataLoaded) return;

            // Load test data once
            await LoadGeoJsonFiles();
            await LoadShapefiles();

            _dataLoaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

### 4. Avoid Sleep/Delays

**Bad:**
```csharp
[Fact]
public async Task SlowTest()
{
    await service.StartAsync();
    Thread.Sleep(1000);  // Bad!
    var status = await service.GetStatusAsync();
}
```

**Good:**
```csharp
[Fact]
public async Task FastTest()
{
    await service.StartAsync();
    await service.WaitUntilReadyAsync();  // Async wait
    var status = await service.GetStatusAsync();
}
```

### 5. Minimize HTTP Requests

**Bad - Multiple Requests:**
```csharp
[Fact]
public async Task SlowTest()
{
    var response1 = await client.GetAsync("/api/endpoint1");
    var data1 = await response1.Content.ReadAsStringAsync();

    var response2 = await client.GetAsync("/api/endpoint2");
    var data2 = await response2.Content.ReadAsStringAsync();

    // ...
}
```

**Good - Batch Requests:**
```csharp
[Fact]
public async Task FastTest()
{
    var tasks = new[]
    {
        client.GetAsync("/api/endpoint1"),
        client.GetAsync("/api/endpoint2")
    };

    var responses = await Task.WhenAll(tasks);

    // Process responses in parallel
}
```

### 6. Use In-Memory Databases for Unit Tests

**For Unit Tests:**
```csharp
public class UnitTestWithInMemoryDb
{
    [Fact]
    public async Task FastUnitTest()
    {
        // Use SQLite in-memory for unit tests
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Much faster than TestContainers for unit tests
    }
}
```

**For Integration Tests:**
```csharp
[Collection("DatabaseCollection")]
public class IntegrationTestWithRealDb : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task RealisticIntegrationTest()
    {
        // Use real PostgreSQL for integration tests
        // Slower but more realistic
    }
}
```

---

## Performance Monitoring

### Measuring Test Performance

**1. Console Output:**
```bash
dotnet test --logger "console;verbosity=detailed"
```

**2. TRX Reporter:**
```bash
dotnet test --logger "trx;LogFileName=test-results.trx"
```

**3. Custom Timing:**
```csharp
[Fact]
public async Task MeasuredTest()
{
    var sw = Stopwatch.StartNew();

    try
    {
        // Test logic
        await TestOperation();
    }
    finally
    {
        sw.Stop();
        Console.WriteLine($"Test took {sw.ElapsedMilliseconds}ms");
    }
}
```

### Identifying Slow Tests

**Find Slow Tests:**
```bash
# Run with detailed timing
dotnet test --logger "console;verbosity=detailed" | grep "Elapsed"

# Or use trx file
dotnet test --logger "trx"
# Then analyze XML for test durations
```

**Profile Specific Test:**
```bash
dotnet test --filter "FullyQualifiedName~SlowTest" --logger "console;verbosity=detailed"
```

### Performance Benchmarks

**Create Benchmark Tests:**
```csharp
public class PerformanceBenchmarks
{
    [Fact]
    public async Task ContainerStartup_ShouldBeFast()
    {
        var sw = Stopwatch.StartNew();

        var fixture = new DatabaseFixture();
        await fixture.InitializeAsync();

        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(60000); // < 60 seconds
        Console.WriteLine($"Container startup: {sw.ElapsedMilliseconds}ms");

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task FullTestSuite_ShouldBeFast()
    {
        // Run programmatically and measure
        var result = await RunTestSuite();

        result.Duration.Should().BeLessThan(TimeSpan.FromMinutes(2));
    }
}
```

### Monitoring Container Performance

**Check Container Stats:**
```bash
# While tests are running
docker stats

# Output:
CONTAINER ID   NAME            CPU %   MEM USAGE / LIMIT
abc123         postgres-test   2.5%    150MiB / 8GiB
def456         mysql-test      1.8%    200MiB / 8GiB
ghi789         redis-test      0.5%    30MiB / 8GiB
```

**Monitor Container Logs:**
```bash
# Find test containers
docker ps | grep testcontainers

# View logs
docker logs <container-id>

# Follow logs
docker logs -f <container-id>
```

---

## CI/CD Optimization

### GitHub Actions Optimization

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    strategy:
      matrix:
        # Run test projects in parallel
        project:
          - Honua.Server.Core.Tests
          - Honua.Server.Core.Tests.Security
          - Honua.Server.Integration.Tests

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      # Pre-pull images to avoid timeout
      - name: Pull Docker Images
        run: |
          docker pull postgis/postgis:16-3.4
          docker pull mysql:8.0
          docker pull redis:7-alpine

      # Restore once for all projects
      - name: Restore dependencies
        run: dotnet restore

      # Run tests with limited parallelization for CI
      - name: Run Tests
        run: |
          dotnet test tests/${{ matrix.project }}/ \
            --no-restore \
            --verbosity normal \
            --logger "trx;LogFileName=test-results.trx" \
            --collect:"XPlat Code Coverage" \
            -- xUnit.MaxParallelThreads=2

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.project }}
          path: '**/test-results.trx'

      - name: Upload Coverage
        uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
```

### CI Performance Tips

**1. Use Matrix Strategy:**
Run test projects in parallel across different runners.

**2. Cache Dependencies:**
```yaml
- name: Cache NuGet packages
  uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
```

**3. Limit Parallel Threads:**
```bash
# CI runners often have limited resources
dotnet test -- xUnit.MaxParallelThreads=2
```

**4. Pre-pull Images:**
Avoids timeout during test container startup.

**5. Use Self-Hosted Runners:**
For large test suites, self-hosted runners with Docker caching are faster.

### Expected CI Performance

| Test Suite | Local | GitHub Actions | Self-Hosted |
|------------|-------|----------------|-------------|
| Unit Tests | 5s | 10s | 5s |
| Integration Tests | 20s | 40s | 25s |
| E2E Tests | 10s | 20s | 12s |
| **Total** | **35s** | **70s** | **42s** |

---

## Troubleshooting Slow Tests

### Common Performance Issues

#### 1. Tests Taking Minutes Instead of Seconds

**Symptoms:**
- Individual tests take 30s+
- Full suite takes 10+ minutes

**Causes:**
- Not using collection fixtures (creating containers per class)
- Sequential container startup
- No parallel execution

**Solutions:**
```csharp
// Use collection fixtures
[Collection("DatabaseCollection")]
public class MyTests : IClassFixture<DatabaseFixture>
{
}

// Enable parallel execution
[assembly: CollectionBehavior(DisableTestParallelization = false)]
```

#### 2. Container Startup Timeouts

**Symptoms:**
```
TimeoutException: Container did not become healthy within timeout
```

**Solutions:**
```bash
# Pre-pull images
docker pull postgis/postgis:16-3.4

# Increase Docker resources (Docker Desktop)
# Memory: 4GB → 8GB
# CPU: 2 → 4 cores
```

#### 3. Memory Exhaustion

**Symptoms:**
- Tests fail with OutOfMemoryException
- Docker becomes unresponsive
- System swapping

**Solutions:**
- Use collection fixtures (reduces containers)
- Limit parallel threads: `xUnit.MaxParallelThreads=2`
- Increase Docker memory limit
- Clean up containers: `docker system prune`

#### 4. Database Connection Timeouts

**Symptoms:**
```
NpgsqlException: Timeout connecting to database
```

**Solutions:**
```csharp
// Increase pool size
data_source "db" {
  provider = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 2
    max_size = 20  // Increase from default
  }
}
```

#### 5. Slow Test Discovery

**Symptoms:**
- `dotnet test --list-tests` takes long
- Long delay before first test runs

**Causes:**
- Large number of test assemblies
- Reflection overhead

**Solutions:**
```bash
# Test specific project
dotnet test tests/Honua.Server.Core.Tests/

# Use filter
dotnet test --filter "Category=Unit"
```

### Performance Debugging

**1. Profile Individual Tests:**
```bash
dotnet test --filter "FullyQualifiedName~MySlowTest" --logger "console;verbosity=detailed"
```

**2. Measure Container Startup:**
```csharp
[Fact]
public async Task MeasureContainerStartup()
{
    var sw = Stopwatch.StartNew();

    var fixture = new DatabaseFixture();
    await fixture.InitializeAsync();

    Console.WriteLine($"Startup: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"Postgres Ready: {fixture.IsPostgresReady}");
    Console.WriteLine($"MySQL Ready: {fixture.IsMySqlReady}");
    Console.WriteLine($"Redis Ready: {fixture.IsRedisReady}");

    await fixture.DisposeAsync();
}
```

**3. Identify Bottlenecks:**
```csharp
[Fact]
public async Task ProfileTest()
{
    var sw = Stopwatch.StartNew();

    Console.WriteLine($"Setup: {sw.ElapsedMilliseconds}ms");
    await Setup();

    Console.WriteLine($"Operation 1: {sw.ElapsedMilliseconds}ms");
    await Operation1();

    Console.WriteLine($"Operation 2: {sw.ElapsedMilliseconds}ms");
    await Operation2();

    Console.WriteLine($"Total: {sw.ElapsedMilliseconds}ms");
}
```

---

## Quick Reference

### Performance Checklist

Test Infrastructure:
- [ ] Using `[Collection("DatabaseCollection")]`
- [ ] Injecting `DatabaseFixture` via constructor
- [ ] Not creating new `DatabaseFixture()` instances
- [ ] Parallel execution enabled

Test Code:
- [ ] Using unique test data identifiers
- [ ] Cleaning up test data in `finally` blocks
- [ ] Avoiding `Thread.Sleep()` or delays
- [ ] Batching HTTP requests where possible

CI/CD:
- [ ] Pre-pulling Docker images
- [ ] Limiting parallel threads (`MaxParallelThreads=2`)
- [ ] Using test matrix for parallelization
- [ ] Caching dependencies

### Performance Commands

```bash
# Run all tests
dotnet test

# Run with limited parallelization
dotnet test -- xUnit.MaxParallelThreads=2

# Run specific category
dotnet test --filter "Category=Unit"

# Measure performance
dotnet test --logger "console;verbosity=detailed"

# Profile single test
dotnet test --filter "FullyQualifiedName~MyTest" --logger "console;verbosity=detailed"

# Pre-pull images
docker pull postgis/postgis:16-3.4
docker pull mysql:8.0
docker pull redis:7-alpine

# Clean up containers
docker system prune -af
```

### Expected Performance Targets

| Metric | Target | Good | Needs Improvement |
|--------|--------|------|-------------------|
| Container Startup | < 30s | < 60s | > 60s |
| Unit Test | < 50ms | < 100ms | > 100ms |
| Integration Test | < 500ms | < 2s | > 2s |
| E2E Test | < 5s | < 10s | > 10s |
| Full Suite (Local) | < 1min | < 2min | > 2min |
| Full Suite (CI) | < 2min | < 5min | > 5min |

---

## Summary

### Key Performance Improvements

1. **Container Optimization:** 39+ → 3 containers (87% reduction)
2. **Collection Fixtures:** Shared resources across tests
3. **Parallel Execution:** 3-4x speedup
4. **Parallel Container Startup:** 2-3x faster initialization

### Best Practices

- Always use collection fixtures for integration tests
- Enable parallel execution
- Use unique test data identifiers
- Avoid sleep/delays
- Pre-pull Docker images in CI
- Monitor and profile slow tests

### Resources

- [TEST_INFRASTRUCTURE.md](./TEST_INFRASTRUCTURE.md) - Infrastructure details
- [WRITING_TESTS.md](./WRITING_TESTS.md) - Test writing guide
- [CONFIGURATION_V2_MIGRATION_GUIDE.md](./CONFIGURATION_V2_MIGRATION_GUIDE.md) - Migration guide

---

**Last Updated:** 2025-11-11
**Maintained by:** Honua.Server Team
