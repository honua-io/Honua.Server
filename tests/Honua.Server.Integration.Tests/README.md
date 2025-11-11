# Honua Server Integration Tests

This project contains comprehensive integration tests for the Honua Server API endpoints and data providers.

## Test Coverage

### API Endpoint Tests

#### GeoservicesREST (4 test suites, ~64 tests)
- **FeatureServerTests** - Feature layer operations (query, create, update, delete)
- **MapServerTests** - Map service operations (export, identify, find)
- **ImageServerTests** - Raster operations (imagery export, catalog queries)
- **GeometryServerTests** - Geometry operations (projection, buffer, spatial analysis)

#### STAC (3 test suites, ~37 tests)
- **StacSearchTests** - Search functionality with spatial, temporal, and attribute filtering
- **StacCollectionsTests** - Collections API and item retrieval
- **StacCatalogTests** - Root catalog and conformance endpoints

#### OGC (3 test suites, ~47 tests)
- **WfsTests** - WFS 2.0/3.0 operations (GetCapabilities, GetFeature, DescribeFeatureType)
- **WmsTests** - WMS 1.3.0 operations (GetCapabilities, GetMap, GetFeatureInfo)
- **WmtsTests** - WMTS tile operations (GetCapabilities, GetTile)

### Data Provider Tests (4 test suites, ~60 tests)

- **PostgreSqlProviderTests** - PostgreSQL + PostGIS provider with TestContainers
- **MySqlProviderTests** - MySQL provider with spatial extensions
- **SQLiteProviderTests** - SQLite provider with SpatiaLite
- **DuckDbProviderTests** - DuckDB provider for analytical queries

## Prerequisites

### Required Software
- .NET 9.0 SDK
- Docker Desktop (for TestContainers)
- Visual Studio 2022 or VS Code

### Docker Images (Downloaded Automatically)
- `postgis/postgis:16-3.4` - PostgreSQL with PostGIS
- `mysql:8.0` - MySQL with spatial extensions
- `redis:7-alpine` - Redis for caching tests

## Running Tests

### Run All Integration Tests
```bash
# From project root
./scripts/run-tests.sh integration
```

### Run Specific Test Categories
```bash
# Unit tests only (no Docker required)
./scripts/run-tests.sh unit

# All tests
./scripts/run-tests.sh all
```

### Run Tests by API Surface
```bash
# GeoservicesREST tests
dotnet test --filter "API=GeoservicesREST"

# STAC tests
dotnet test --filter "API=STAC"

# OGC tests
dotnet test --filter "API=OGC"

# Data provider tests
dotnet test --filter "Provider=PostgreSQL"
```

### Run Tests from IDE
In Visual Studio or VS Code:
1. Ensure Docker Desktop is running
2. Open Test Explorer
3. Run tests individually or by category
4. Use filters: `Category=Integration`, `API=STAC`, etc.

## Test Infrastructure

### Fixtures
- **DatabaseFixture** - TestContainers setup for PostgreSQL, MySQL, and Redis
- **WebApplicationFactoryFixture** - In-memory API test server
- **TestDataFixture** - Sample geospatial test data

### Helpers
- **GeoJsonHelper** - Create and manipulate GeoJSON test data
- **GeometryHelper** - Create test geometries (points, polygons, etc.)
- **HttpClientHelper** - API request helpers and content serialization

### Test Data
- Sample GeoJSON features and collections
- Test geometries (points, polygons, lines)
- Bounding boxes and temporal ranges
- STAC collection and item IDs

## Test Organization

Tests are organized using xUnit traits:

```csharp
[Trait("Category", "Integration")]  // Integration vs Unit
[Trait("API", "GeoservicesREST")]   // API surface area
[Trait("Endpoint", "FeatureServer")] // Specific endpoint
[Trait("Provider", "PostgreSQL")]    // Data provider
```

## Coverage Goals

- **Current Coverage**: ~15%
- **Target Coverage**: 40%+ (with these integration tests)
- **API Endpoint Tests**: 30+ tests per major API surface
- **Data Provider Tests**: 20+ tests per provider
- **Total New Tests**: ~200 tests

## Key Testing Patterns

### 1. TestContainers for Real Databases
```csharp
// Don't mock databases - use real ones!
_container = new PostgreSqlBuilder()
    .WithImage("postgis/postgis:16-3.4")
    .Build();
```

### 2. WebApplicationFactory for API Testing
```csharp
using var factory = new WebApplicationFactoryFixture<Program>(_databaseFixture);
var client = factory.CreateClient();
var response = await client.GetAsync("/v1/stac/search");
```

### 3. FluentAssertions for Readable Tests
```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
content.Should().Contain("FeatureCollection");
```

### 4. Idempotent and Parallel-Safe Tests
- Tests clean up after themselves
- Tests don't depend on shared state
- Tests can run in parallel

## Troubleshooting

### Docker Not Running
```
Error: Cannot connect to Docker daemon
Solution: Start Docker Desktop and try again
```

### Port Conflicts
```
Error: Port already in use
Solution: TestContainers automatically assigns random ports
```

### Slow Test Startup
- First run downloads Docker images (~2-3 minutes)
- Subsequent runs are much faster (~10-20 seconds)
- Images are cached by Docker

### Test Failures
- Check Docker has sufficient resources (4GB+ RAM recommended)
- Ensure no port conflicts (PostgreSQL 5432, MySQL 3306)
- Review test output for specific error messages

## Contributing

When adding new integration tests:
1. Follow existing test patterns and naming conventions
2. Use appropriate xUnit traits for categorization
3. Ensure tests are idempotent and can run in parallel
4. Add documentation for complex test scenarios
5. Use TestContainers for database testing (don't mock)

## Performance

- Unit tests: ~5-10 seconds
- Integration tests (first run): ~2-3 minutes (Docker image download)
- Integration tests (subsequent): ~20-40 seconds
- Full test suite: ~1-2 minutes

## References

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [TestContainers](https://dotnet.testcontainers.org/)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
