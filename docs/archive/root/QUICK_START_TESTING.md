# Quick Start Guide - HonuaIO Test Suite

After the massive refactoring, here's how to work with the improved test suite.

---

## Running Tests

### AI Emulator Suite (Docker Required)

The AI deployment workflows use Dockerized cloud emulators. Before running these tests:

- Ensure **Docker Desktop** (or equivalent) is running and has at least 6 GB RAM available.
- Required images: `localstack/localstack`, `mcr.microsoft.com/azure-storage/azurite`, `fsouza/fake-gcs-server:1.52.2`, `postgis/postgis:16-3.4`.
- No manual container setup is needed; the xUnit fixtures start/stop containers automatically.  
  If Docker is unavailable, tests marked with `[SkippableFact]` will skip with a helpful message.

Run the full emulator pipeline suite:

```bash
./scripts/start-emulators.sh up
dotnet test tests/Honua.Cli.AI.E2ETests/Honua.Cli.AI.E2ETests.csproj \
  --no-build \
  --filter ProcessFrameworkEmulatorE2ETests
```

Stop the stack when finished:

```bash
./scripts/start-emulators.sh down
```

Useful environment overrides:

| Variable | Purpose | Default |
|----------|---------|---------|
| `HONUA_DOCKER_NETWORK` | Custom Docker network name for emulators | *(auto)* |
| `LOCALSTACK_HOST` | Override LocalStack endpoint | *(fixture generated)* |
| `STORAGE_EMULATOR_HOST` | Fake GCS endpoint used by tests | Set/unset per test |

If an emulator fails readiness checks, inspect the Docker logs (e.g. `docker logs <container>`) or rerun the suite after restarting Docker.

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Categories

**Fast Unit Tests Only** (for local development):
```bash
dotnet test --filter "Category=Unit&Speed=Fast"
```

**Integration Tests Only**:
```bash
dotnet test --filter "Category=Integration"
```

**Slow Tests Only** (for CI):
```bash
dotnet test --filter "Speed=Slow"
```

### Run Tests by Feature Area

**OGC API Features Tests**:
```bash
dotnet test --filter "Feature=OGC"
```

**STAC API Tests**:
```bash
dotnet test --filter "Feature=STAC"
```

**WFS Tests**:
```bash
dotnet test --filter "Feature=WFS"
```

**GeoservicesREST Tests**:
```bash
dotnet test --filter "Feature=GeoservicesREST"
```

**Security Tests**:
```bash
dotnet test --filter "Feature=Security"
```

**Data Provider Tests**:
```bash
dotnet test --filter "Feature=Data"
```

### Run Database-Specific Tests

**PostgreSQL Tests Only**:
```bash
dotnet test --filter "Database=Postgres"
```

**MySQL Tests Only**:
```bash
dotnet test --filter "Database=MySQL"
```

**SQL Server Tests Only**:
```bash
dotnet test --filter "Database=SQLServer"
```

**SQLite Tests Only**:
```bash
dotnet test --filter "Database=SQLite"
```

### Run GeoservicesREST Server-Specific Tests

**MapServer Tests Only**:
```bash
dotnet test --filter "Server=MapServer"
```

**FeatureServer Tests Only**:
```bash
dotnet test --filter "Server=FeatureServer"
```

**ImageServer Tests Only**:
```bash
dotnet test --filter "Server=ImageServer"
```

---

## Writing New Tests

### Using the Test Infrastructure

#### For OGC Handler Tests

Use the `OgcHandlerTestFixture`:

```csharp
using Honua.Server.Core.Tests.Ogc;
using Xunit;

[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class MyOgcTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public MyOgcTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/test/items", "limit=10");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "test",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }
}
```

#### For Integration Tests

Use the `HonuaTestWebApplicationFactory`:

```csharp
using Honua.Server.Core.Tests.TestInfrastructure;
using Xunit;

[Trait("Category", "Integration")]
[Trait("Feature", "MyFeature")]
[Trait("Speed", "Slow")]
public class MyIntegrationTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HonuaTestWebApplicationFactory _factory;

    public MyIntegrationTests(HonuaTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MyTest()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/my/endpoint");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

#### For Database Provider Tests

Inherit from `DataStoreProviderTestsBase`:

```csharp
using Honua.Server.Core.Tests.Data;

public class MyDatabaseProviderTests : DataStoreProviderTestsBase<MyDatabaseProviderTests.Fixture>
{
    public MyDatabaseProviderTests(Fixture fixture) : base(fixture) { }

    protected override IDataStoreProvider Provider => _fixture.Provider;
    protected override string ProviderName => "MyDatabase";

    // Common CRUD tests are inherited automatically!
    // Just implement fixture-specific setup

    public class Fixture : IAsyncLifetime
    {
        public IDataStoreProvider Provider { get; private set; }

        public async Task InitializeAsync()
        {
            // Setup database container and provider
        }

        public async Task DisposeAsync()
        {
            // Cleanup
        }
    }
}
```

#### For STAC Catalog Store Tests

Inherit from `StacCatalogStoreTestsBase`:

```csharp
using Honua.Server.Core.Tests.Stac;

public class MyStacStoreTests : StacCatalogStoreTestsBase
{
    protected override IStacCatalogStore CatalogStore => _store;
    protected override string ConnectionString => _connectionString;

    // 13 common tests are inherited automatically!
}
```

---

## Using Test Utilities

### Mocking Dependencies

Use `MockBuilders`:

```csharp
using Honua.Server.Core.Tests.TestInfrastructure;

var mockRegistry = MockBuilders.CreateMetadataRegistry(service, layer);
var mockProvider = MockBuilders.CreateDataStoreProviderWithFeatures(features);
```

### Database Seeding

Use `TestDatabaseSeeder`:

```csharp
using Honua.Server.Core.Tests.TestInfrastructure;

await TestDatabaseSeeder.SeedPostGisTableAsync(connection, service, layer, featureCount: 100);
await TestDatabaseSeeder.SeedMySqlTableAsync(connection, service, layer, featureCount: 100);
```

### Test Data

Use existing test data classes:

```csharp
using Honua.Server.Core.Tests.TestInfrastructure;

var geometries = GeometryTestData.AllGeometryTypes;
var realistic = RealisticGisTestData.SanFranciscoLocations;
```

---

## Trait Guidelines

Always add these traits to your test classes:

**Required**:
- `[Trait("Category", "Unit|Integration|E2E")]`
- `[Trait("Feature", "OGC|STAC|WFS|GeoservicesREST|Data|Security|...")]`

**Recommended**:
- `[Trait("Speed", "Fast|Slow")]`

**When Applicable**:
- `[Trait("Database", "Postgres|MySQL|SQLServer|SQLite|Redis")]`
- `[Trait("Server", "MapServer|FeatureServer|ImageServer")]`

---

## Best Practices

1. **Use FluentAssertions** for all assertions
2. **Validate data, not just status codes**
3. **Test error cases** as well as success cases
4. **Test edge cases** (boundaries, nulls, extremes)
5. **Use shared fixtures** to reduce duplication
6. **Add XML documentation** to test classes
7. **Follow naming convention**: `MethodName_StateUnderTest_ExpectedBehavior`

---

## Code Coverage

Generate code coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

View coverage in HTML:
```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open in browser
open coveragereport/index.html  # macOS
xdg-open coveragereport/index.html  # Linux
```

---

## CI/CD Recommended Setup

### Stage 1: Fast Tests (on every commit)
```bash
dotnet test --filter "Category=Unit&Speed=Fast"
```
Expected: < 30 seconds

### Stage 2: Integration Tests (on PR)
```bash
dotnet test --filter "Category=Integration&Speed=Fast"
```
Expected: < 2 minutes

### Stage 3: Full Suite (on merge to main)
```bash
dotnet test --collect:"XPlat Code Coverage"
```
Expected: < 10 minutes

### Stage 4: Database Tests (nightly)
```bash
dotnet test --filter "Database=Postgres"
dotnet test --filter "Database=MySQL"
dotnet test --filter "Database=SQLServer"
```

---

## Troubleshooting

### Tests Not Found
Make sure traits are properly formatted:
```csharp
[Trait("Category", "Unit")]  // ✅ Correct
[Trait("category", "unit")]  // ❌ Wrong (case-sensitive)
```

### Fixtures Not Working
Ensure you implement `IClassFixture<T>`:
```csharp
public class MyTests : IClassFixture<MyFixture>  // ✅ Correct
{
    private readonly MyFixture _fixture;
    public MyTests(MyFixture fixture) => _fixture = fixture;
}
```

### Database Tests Failing
Check Docker is running:
```bash
docker ps  # Should show testcontainers
```

---

## Key Files

**Test Infrastructure**:
- `tests/Honua.Server.Core.Tests/TestInfrastructure/HonuaTestWebApplicationFactory.cs`
- `tests/Honua.Server.Core.Tests/TestInfrastructure/TestDatabaseSeeder.cs`
- `tests/Honua.Server.Core.Tests/TestInfrastructure/MockBuilders.cs`

**Test Fixtures**:
- `tests/Honua.Server.Core.Tests/Ogc/OgcHandlerTestFixture.cs`
- `tests/Honua.Server.Core.Tests/Hosting/GeoservicesTestFixture.cs`

**Base Classes**:
- `tests/Honua.Server.Core.Tests/Stac/StacCatalogStoreTestsBase.cs`
- `tests/Honua.Server.Core.Tests/Data/DataStoreProviderTestsBase.cs`

---

## Summary

The test suite now provides:
- ✅ Reusable infrastructure (eliminate duplication)
- ✅ Strong assertions (validate data, not just status)
- ✅ Comprehensive coverage (error cases, edge cases, security)
- ✅ Selective execution (run only what you need)
- ✅ Clear organization (SRP, traits, documentation)

**Happy Testing!** 🎉
