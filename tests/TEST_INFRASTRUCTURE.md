# Test Infrastructure Documentation

**Copyright (c) 2025 HonuaIO**
**Licensed under the Elastic License 2.0**

**Last Updated:** 2025-11-11
**Version:** 2.0

---

## Table of Contents

1. [Overview](#overview)
2. [Test Organization](#test-organization)
3. [Test Categories](#test-categories)
4. [Infrastructure Components](#infrastructure-components)
5. [Using DatabaseFixture and Collection Fixtures](#using-databasefixture-and-collection-fixtures)
6. [Test Base Classes](#test-base-classes)
7. [Configuration V2 in Tests](#configuration-v2-in-tests)
8. [Running Tests](#running-tests)
9. [Performance Optimization](#performance-optimization)
10. [Troubleshooting](#troubleshooting)

---

## Overview

The Honua.Server test infrastructure is designed for **comprehensive, fast, and reliable testing** across all project components. The unified test infrastructure leverages modern .NET testing practices with a focus on:

- **Shared Test Containers:** Reduce startup time from 39+ containers to 3 shared containers
- **Parallel Execution:** Tests run in parallel for maximum throughput
- **Configuration V2 Support:** Test declarative HCL-based configuration
- **Real Database Testing:** TestContainers provide real PostgreSQL, MySQL, and Redis instances
- **Isolation:** Tests are isolated while sharing infrastructure

### Key Statistics

- **Test Projects:** 19
- **Test Files:** 77+
- **Container Reduction:** 39+ → 3 (87% reduction)
- **Average Test Run Time:** ~30-60 seconds (full suite)
- **Parallel Execution:** Enabled by default

---

## Test Organization

### Directory Structure

```
tests/
├── Honua.Server.Integration.Tests/      # Integration tests (API, E2E workflows)
├── Honua.Server.Core.Tests/             # Core configuration tests
├── Honua.Server.Core.Tests.Apis/        # API layer unit tests
├── Honua.Server.Core.Tests.Data/        # Data layer unit tests
├── Honua.Server.Core.Tests.DataOperations/  # Database provider tests
├── Honua.Server.Core.Tests.Infrastructure/  # Infrastructure tests
├── Honua.Server.Core.Tests.OgcProtocols/   # OGC protocol tests
├── Honua.Server.Core.Tests.Raster/      # Raster processing tests
├── Honua.Server.Core.Tests.Security/    # Security and auth tests
├── Honua.Server.Core.Tests.Shared/      # Shared utilities tests
├── Honua.Server.Enterprise.Tests/       # Enterprise features tests
├── Honua.Server.Deployment.E2ETests/    # End-to-end deployment tests
├── Honua.Cli.Tests/                     # CLI tool tests
├── Honua.Admin.Blazor.Tests/            # Admin UI tests
└── TEST_INFRASTRUCTURE.md               # This file
```

### Naming Conventions

**Test Projects:**
- `*.Tests` - Unit tests
- `*.Integration.Tests` - Integration tests
- `*.E2ETests` - End-to-end tests

**Test Classes:**
- `{FeatureName}Tests.cs` - Standard test class
- `{FeatureName}ConfigV2Tests.cs` - Configuration V2 test class

**Test Methods:**
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    // Act
    // Assert
}
```

---

## Test Categories

### 1. Unit Tests

**Purpose:** Test individual components in isolation
**Speed:** Fast (< 100ms per test)
**Dependencies:** Mocked
**Container Usage:** None

**Projects:**
- `Honua.Server.Core.Tests.Apis`
- `Honua.Server.Core.Tests.Security`
- `Honua.Server.Core.Tests.Shared`
- `Honua.Server.Core.Tests.Raster`

**Example:**
```csharp
[Fact]
public void ValidatePassword_WithStrongPassword_ReturnsValid()
{
    // Arrange
    var validator = new PasswordComplexityValidator();
    var password = "StrongP@ssw0rd!";

    // Act
    var result = validator.Validate(password);

    // Assert
    result.IsValid.Should().BeTrue();
}
```

**Traits:**
```csharp
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
```

### 2. Integration Tests

**Purpose:** Test component interactions with real dependencies
**Speed:** Medium (100ms - 2s per test)
**Dependencies:** Real databases via TestContainers
**Container Usage:** Shared containers (3 total)

**Projects:**
- `Honua.Server.Integration.Tests`
- `Honua.Server.Core.Tests.DataOperations`

**Example:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class FeatureRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public FeatureRepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetFeature_ReturnsCorrectData()
    {
        // Uses real PostgreSQL container
    }
}
```

### 3. End-to-End Tests

**Purpose:** Test complete workflows through the API
**Speed:** Slow (2s - 10s per test)
**Dependencies:** Full application stack
**Container Usage:** Shared containers + WebApplicationFactory

**Projects:**
- `Honua.Server.Deployment.E2ETests`

**Example:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "E2E")]
public class WmsE2ETests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task GetCapabilities_ReturnsValidXml()
    {
        // Full HTTP request/response cycle
    }
}
```

---

## Infrastructure Components

### 1. DatabaseFixture

**Location:** `tests/Honua.Server.Integration.Tests/Fixtures/DatabaseFixture.cs`

**Purpose:** Provides shared TestContainer instances for PostgreSQL, MySQL, and Redis.

**Lifecycle:**
- Created once per test run
- Shared across all test classes in the `DatabaseCollection`
- Automatically disposed after all tests complete

**Properties:**
```csharp
public string PostgresConnectionString { get; }
public string MySqlConnectionString { get; }
public string RedisConnectionString { get; }
public bool IsPostgresReady { get; }
public bool IsMySqlReady { get; }
public bool IsRedisReady { get; }
```

**Container Details:**
- **PostgreSQL:** `postgis/postgis:16-3.4` with PostGIS extensions
- **MySQL:** `mysql:8.0` with spatial support
- **Redis:** `redis:7-alpine`

**Startup Behavior:**
- All containers start in parallel
- Graceful failure handling (continues if one container fails)
- Automatic cleanup on disposal

### 2. WebApplicationFactoryFixture

**Location:** `tests/Honua.Server.Integration.Tests/Fixtures/WebApplicationFactoryFixture.cs`

**Purpose:** Provides in-memory test server for integration testing.

**Features:**
- Uses real TestContainer databases
- Configurable via `appsettings.Test.json`
- Service overrides via `ConfigureTestServices`
- Test environment isolation

**Usage:**
```csharp
using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
var client = factory.CreateClient();

var response = await client.GetAsync("/api/endpoint");
```

### 3. ConfigurationV2TestFixture

**Location:** `tests/Honua.Server.Integration.Tests/Fixtures/ConfigurationV2TestFixture.cs`

**Purpose:** Test Configuration V2 (HCL/.honua files) with real services.

**Features:**
- Inline HCL configuration
- Builder pattern for configuration
- Automatic connection string interpolation
- Temporary config file management
- Full service registration

**Usage Examples:**

**Builder Pattern:**
```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_databaseFixture, builder =>
{
    builder
        .AddDataSource("gis_db", "postgresql")
        .AddService("wfs", new()
        {
            ["version"] = "2.0.0",
            ["max_features"] = 10000
        })
        .AddLayer("features", "gis_db", "public.features");
});

var client = factory.CreateClient();
```

**Inline HCL:**
```csharp
var hclConfig = @"
honua {
  version = ""1.0""
  environment = ""test""
}

data_source ""main_db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")
}

service ""ogc_api"" {
  enabled = true
  item_limit = 1000
}
";

using var factory = new ConfigurationV2TestFixture<Program>(_db, hclConfig);
```

**Access Loaded Config:**
```csharp
factory.LoadedConfig.Should().NotBeNull();
var wfsService = factory.LoadedConfig!.Services["wfs"];
wfsService.Enabled.Should().BeTrue();
```

### 4. DatabaseCollection

**Location:** `tests/Honua.Server.Integration.Tests/Collections/DatabaseCollection.cs`

**Purpose:** Defines xUnit collection for sharing DatabaseFixture.

**Implementation:**
```csharp
[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class is never instantiated
}
```

**Usage:**
```csharp
[Collection("DatabaseCollection")]
public class MyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyTests(DatabaseFixture db)
    {
        _db = db;
    }
}
```

---

## Using DatabaseFixture and Collection Fixtures

### Collection Fixture Pattern

**Why Use Collection Fixtures?**

1. **Container Reuse:** Start containers once, use for all tests
2. **Performance:** Reduces test run time by 70-90%
3. **Resource Efficiency:** Avoids Docker resource exhaustion
4. **Consistency:** Same database state across test classes

### Step-by-Step Usage

**Step 1: Add Collection Attribute**
```csharp
[Collection("DatabaseCollection")]
public class MyIntegrationTests
{
}
```

**Step 2: Add IClassFixture**
```csharp
[Collection("DatabaseCollection")]
public class MyIntegrationTests : IClassFixture<DatabaseFixture>
{
}
```

**Step 3: Inject DatabaseFixture**
```csharp
[Collection("DatabaseCollection")]
public class MyIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }
}
```

**Step 4: Use Connection Strings**
```csharp
[Fact]
public async Task TestWithPostgres()
{
    // Skip if container failed to start
    if (!_db.IsPostgresReady)
    {
        return; // or throw SkipException
    }

    var connectionString = _db.PostgresConnectionString;
    // Use connection string...
}
```

### Multiple Fixtures

If you need multiple fixtures, combine them:

```csharp
[Collection("DatabaseCollection")]
public class ComplexTests :
    IClassFixture<DatabaseFixture>,
    IClassFixture<TestDataFixture>
{
    private readonly DatabaseFixture _db;
    private readonly TestDataFixture _testData;

    public ComplexTests(DatabaseFixture db, TestDataFixture testData)
    {
        _db = db;
        _testData = testData;
    }
}
```

---

## Test Base Classes

### When to Use Each Pattern

| Pattern | Use Case | Shared Resources | Example |
|---------|----------|------------------|---------|
| No Base Class | Simple unit tests | None | `PasswordHasherTests` |
| IClassFixture | Single shared resource per class | Per test class | Unit tests with setup |
| Collection + IClassFixture | Expensive shared resources | Across all test classes | Integration tests with DB |
| WebApplicationFactory | API/HTTP integration tests | Test server + dependencies | API endpoint tests |

### Unit Test Pattern

**No shared resources needed:**

```csharp
public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var calculator = new Calculator();
        var result = calculator.Add(2, 3);
        result.Should().Be(5);
    }
}
```

### Unit Test with Setup Pattern

**Shared mock setup per test class:**

```csharp
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _service = new UserService(_mockRepo.Object);
    }

    [Fact]
    public async Task GetUser_ReturnsUser()
    {
        // Use _mockRepo and _service
    }
}
```

### Integration Test Pattern

**With real database containers:**

```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class DatabaseIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public DatabaseIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task CanConnectToPostgres()
    {
        var conn = _db.PostgresConnectionString;
        // Test database operations
    }
}
```

### API Integration Test Pattern

**With WebApplicationFactory:**

```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
public class WfsApiTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsApiTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetCapabilities_ReturnsValidXml()
    {
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/wfs?request=GetCapabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## Configuration V2 in Tests

### Testing HCL Configuration

Configuration V2 uses HCL (HashiCorp Configuration Language) for declarative service configuration. The test infrastructure provides specialized fixtures for testing Configuration V2.

### TestConfigurationBuilder Methods

```csharp
public class TestConfigurationBuilder
{
    // Add a data source
    AddDataSource(string id, string provider, string connectionEnvVar = "DATABASE_URL")

    // Add a service
    AddService(string serviceId, Dictionary<string, object>? settings = null)

    // Add a layer
    AddLayer(string id, string dataSourceRef, string table,
             string geometryColumn = "geom", string geometryType = "Polygon", int srid = 4326)

    // Add Redis cache
    AddRedisCache(string id = "redis_test", string connectionEnvVar = "REDIS_URL")

    // Add raw HCL
    AddRaw(string hclConfig)

    // Build final configuration
    string Build()
}
```

### Configuration V2 Test Examples

**Example 1: Simple WFS Service**
```csharp
[Fact]
public async Task WfsService_ConfiguredCorrectly()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder
            .AddDataSource("spatial_db", "postgresql")
            .AddService("wfs", new()
            {
                ["version"] = "2.0.0",
                ["max_features"] = 10000,
                ["default_count"] = 100
            })
            .AddLayer("roads", "spatial_db", "public.roads", "geom", "LineString");
    });

    // Verify configuration loaded
    factory.LoadedConfig.Should().NotBeNull();
    factory.LoadedConfig!.Services.Should().ContainKey("wfs");

    // Test API
    var client = factory.CreateClient();
    var response = await client.GetAsync("/wfs?request=GetCapabilities");
}
```

**Example 2: Multiple Data Sources**
```csharp
[Fact]
public async Task MultipleDatabases_WorkCorrectly()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder
            .AddDataSource("postgres_db", "postgresql", "DATABASE_URL")
            .AddDataSource("mysql_db", "mysql", "MYSQL_URL")
            .AddLayer("pg_layer", "postgres_db", "pg_table")
            .AddLayer("mysql_layer", "mysql_db", "mysql_table");
    });

    // Both data sources configured
    factory.LoadedConfig!.DataSources.Should().HaveCount(2);
}
```

**Example 3: Raw HCL Configuration**
```csharp
[Fact]
public async Task ComplexConfig_ParsesCorrectly()
{
    var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
  log_level = ""debug""
}

data_source ""main"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 2
    max_size = 10
    idle_timeout = 300
  }
}

cache ""redis"" {
  enabled = true
  connection = env(""REDIS_URL"")
  ttl = 3600
}

service ""ogc_api"" {
  enabled = true
  item_limit = 1000
  enable_transactions = true
}
";

    using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);

    factory.LoadedConfig!.DataSources["main"].Pool.MaxSize.Should().Be(10);
    factory.LoadedConfig!.Services["ogc_api"].Enabled.Should().BeTrue();
}
```

### Environment Variable Interpolation

The fixture automatically interpolates these environment variables:

- `env("DATABASE_URL")` → PostgreSQL connection string
- `env("MYSQL_URL")` → MySQL connection string
- `env("REDIS_URL")` → Redis connection string

**Both syntaxes work:**
```hcl
connection = env("DATABASE_URL")
connection = ${env:DATABASE_URL}
```

---

## Running Tests

### Command Reference

**Run all tests:**
```bash
dotnet test
```

**Run specific project:**
```bash
dotnet test tests/Honua.Server.Integration.Tests/
```

**Run with category filter:**
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"

# E2E tests only
dotnet test --filter "Category=E2E"
```

**Run by trait:**
```bash
# All WFS tests
dotnet test --filter "API=WFS"

# All Configuration V2 tests
dotnet test --filter "API=ConfigurationV2"

# Security tests
dotnet test --filter "Component=Security"
```

**Run specific test class:**
```bash
dotnet test --filter "FullyQualifiedName~WfsConfigV2Tests"
```

**Run specific test method:**
```bash
dotnet test --filter "FullyQualifiedName~WfsConfigV2Tests.GetCapabilities_ReturnsValidXml"
```

**Parallel execution control:**
```bash
# Disable parallel execution
dotnet test -- xUnit.ParallelizeAssembly=false

# Control max threads
dotnet test -- xUnit.MaxParallelThreads=4
```

**Verbose output:**
```bash
dotnet test --logger "console;verbosity=detailed"
```

**Code coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**Generate coverage report:**
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage/report
```

### Running in CI/CD

**GitHub Actions example:**
```yaml
- name: Run Tests
  run: |
    dotnet test --no-build --verbosity normal \
      --logger "trx;LogFileName=test-results.trx" \
      --collect:"XPlat Code Coverage"

- name: Publish Test Results
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Test Results
    path: '**/test-results.trx'
    reporter: dotnet-trx
```

### Docker Requirements

Tests require Docker for TestContainers:

**Verify Docker is running:**
```bash
docker info
```

**If tests fail with "Docker not found":**
```bash
# Start Docker daemon
sudo systemctl start docker

# Or on Mac/Windows, start Docker Desktop
```

---

## Performance Optimization

### Container Optimization

**Before Optimization:**
- 39+ containers created (one per test class)
- Average startup time: 5-10 minutes
- Resource exhaustion on CI runners
- Parallel execution disabled

**After Optimization:**
- 3 shared containers (Postgres, MySQL, Redis)
- Average startup time: 30-60 seconds
- Parallel execution enabled
- 87% reduction in container count

**How it Works:**

1. **Collection Fixtures:** All integration tests use `[Collection("DatabaseCollection")]`
2. **Lazy Startup:** Containers start once when first test class runs
3. **Parallel Sharing:** Multiple test classes use same containers concurrently
4. **Automatic Cleanup:** Containers dispose when all tests complete

### Parallel Execution Best Practices

**Enable parallel execution (default):**
```xml
<!-- In *.csproj -->
<PropertyGroup>
  <ParallelizeAssembly>true</ParallelizeAssembly>
  <ParallelizeTestCollections>true</ParallelizeTestCollections>
</PropertyGroup>
```

**Disable for specific tests:**
```csharp
[Collection("Serial")] // Tests in this collection run serially
public class NonParallelTests { }
```

**Control parallelization:**
```csharp
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
```

### Database Isolation

**Each test should clean up its own data:**

```csharp
[Fact]
public async Task Test_CreatesData()
{
    var id = Guid.NewGuid(); // Unique ID per test

    try
    {
        // Create test data with unique ID
        await CreateData(id);

        // Test logic
        var result = await GetData(id);
        result.Should().NotBeNull();
    }
    finally
    {
        // Clean up
        await DeleteData(id);
    }
}
```

**Use transactions for isolation:**
```csharp
[Fact]
public async Task Test_WithTransaction()
{
    using var connection = new NpgsqlConnection(_db.PostgresConnectionString);
    await connection.OpenAsync();

    using var transaction = await connection.BeginTransactionAsync();

    try
    {
        // All operations in transaction
        // Automatic rollback on test end
    }
    finally
    {
        await transaction.RollbackAsync();
    }
}
```

### Test Data Management

**Use unique identifiers:**
```csharp
var testId = $"test_{Guid.NewGuid():N}";
var tableName = $"test_table_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(1000)}";
```

**Schema isolation:**
```csharp
var schema = $"test_{Guid.NewGuid():N}";
await ExecuteSql($"CREATE SCHEMA {schema}");
await ExecuteSql($"CREATE TABLE {schema}.features (...)");
// Cleanup: DROP SCHEMA {schema} CASCADE
```

---

## Troubleshooting

### Common Issues

#### Issue: Tests timeout waiting for containers

**Symptoms:**
```
System.TimeoutException: Container did not become healthy within timeout
```

**Solutions:**
1. Check Docker is running: `docker info`
2. Increase timeout in DatabaseFixture
3. Check Docker resource limits (CPU, memory)
4. Pull images manually: `docker pull postgis/postgis:16-3.4`

**Docker Desktop Settings:**
- Memory: 4GB minimum (8GB recommended)
- CPU: 2 cores minimum (4 cores recommended)

#### Issue: Port conflicts

**Symptoms:**
```
Bind for 0.0.0.0:5432 failed: port is already allocated
```

**Solutions:**
1. TestContainers uses random ports by default
2. Stop conflicting containers: `docker ps` and `docker stop <container>`
3. If using fixed ports, ensure they're available

#### Issue: Permission denied errors

**Symptoms:**
```
Permission denied while trying to connect to Docker daemon
```

**Solutions:**
```bash
# Add user to docker group (Linux)
sudo usermod -aG docker $USER
newgrp docker

# Or run with sudo (not recommended)
sudo dotnet test
```

#### Issue: Tests pass locally but fail in CI

**Common Causes:**
1. **Docker not installed in CI:** Add Docker installation step
2. **Resource constraints:** Increase CI runner resources
3. **Timeout differences:** Increase test timeouts for CI
4. **Environment differences:** Check connection strings, paths

**CI Configuration:**
```yaml
# GitHub Actions
services:
  docker:
    image: docker:dind
    options: --privileged
```

#### Issue: Slow test execution

**Diagnostics:**
```bash
# Measure test execution time
dotnet test --logger "console;verbosity=detailed" | grep "Test run for"
```

**Optimizations:**
1. Ensure parallel execution is enabled
2. Use `[Collection("DatabaseCollection")]` for integration tests
3. Reduce test data size
4. Use in-memory databases for unit tests
5. Profile slow tests: `dotnet test --logger "trx" --diag:logs.txt`

#### Issue: Container cleanup failures

**Symptoms:**
```
Tests hang during cleanup
Orphaned containers remain after tests
```

**Manual Cleanup:**
```bash
# List TestContainers
docker ps -a | grep testcontainers

# Remove TestContainers
docker rm -f $(docker ps -aq -f "label=org.testcontainers=true")

# Clean up volumes
docker volume prune -f
```

**Automatic Cleanup:**
```csharp
// DatabaseFixture already includes cleanup
_postgresContainer = new PostgreSqlBuilder()
    .WithCleanUp(true)  // Auto-cleanup on disposal
    .Build();
```

#### Issue: Connection string interpolation fails

**Symptoms:**
```
Connection string contains literal "env("DATABASE_URL")" instead of actual value
```

**Solutions:**
1. Verify DatabaseFixture is ready: `_db.IsPostgresReady`
2. Use ConfigurationV2TestFixture (handles interpolation automatically)
3. Check environment variable syntax:
   - `env("DATABASE_URL")` ✓
   - `${env:DATABASE_URL}` ✓
   - `$DATABASE_URL` ✗

#### Issue: Test data conflicts between parallel tests

**Symptoms:**
```
Unique constraint violation
Unexpected data in queries
```

**Solutions:**
1. Use unique IDs: `Guid.NewGuid()`
2. Use test-specific schema/table names
3. Use transactions with rollback
4. Mark conflicting tests with `[Collection("Serial")]`

### Debugging Tests

**Run single test with debugging:**
```bash
# In VS Code/Visual Studio, set breakpoint and press F5
# Or use CLI
dotnet test --filter "FullyQualifiedName~MyTest" --logger "console;verbosity=detailed"
```

**View container logs:**
```bash
# Find container
docker ps | grep postgres

# View logs
docker logs <container-id>

# Follow logs
docker logs -f <container-id>
```

**Inspect test database:**
```bash
# Get connection string from test output or fixture
docker ps  # Find postgres container port

# Connect with psql
psql "Host=localhost;Port=<port>;Database=honua_test;Username=postgres;Password=test"

# View tables
\dt

# Query data
SELECT * FROM test_table;
```

**Enable detailed logging:**
```csharp
// In test constructor
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

### Getting Help

**Documentation:**
- Test Infrastructure: `tests/TEST_INFRASTRUCTURE.md`
- Writing Tests: `tests/WRITING_TESTS.md`
- Configuration V2: `tests/CONFIGURATION_V2_MIGRATION_GUIDE.md`
- Performance: `tests/TEST_PERFORMANCE.md`

**Common Commands Quick Reference:**
```bash
# Full test suite
dotnet test

# Integration tests only
dotnet test --filter "Category=Integration"

# Single test class
dotnet test --filter "FullyQualifiedName~MyTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# List tests without running
dotnet test --list-tests
```

---

## Next Steps

1. Read [WRITING_TESTS.md](./WRITING_TESTS.md) for step-by-step guide to writing tests
2. Review [CONFIGURATION_V2_MIGRATION_GUIDE.md](./CONFIGURATION_V2_MIGRATION_GUIDE.md) for migrating tests
3. Check [TEST_PERFORMANCE.md](./TEST_PERFORMANCE.md) for optimization strategies

---

**Last Updated:** 2025-11-11
**Maintained by:** Honua.Server Team
