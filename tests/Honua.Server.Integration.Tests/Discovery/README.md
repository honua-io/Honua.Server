# PostGIS Auto-Discovery Tests

Comprehensive test suite for the PostGIS auto-discovery feature that enables zero-configuration deployment.

## Test Organization

### Unit Tests (`/tests/Honua.Server.Core.Tests/Discovery/`)

#### `PostGisTableDiscoveryServiceTests.cs`
- Constructor validation
- Pattern matching logic
- Configuration option validation
- Basic discovery behavior with mock dependencies

#### `CachedTableDiscoveryServiceTests.cs`
- Cache behavior verification
- Cache invalidation
- Concurrent request handling
- Background refresh timer

#### `DynamicODataModelProviderTests.cs`
- OData EDM model generation
- Service and layer metadata creation
- Friendly name generation
- Geometry type normalization

#### `DynamicOgcCollectionProviderTests.cs`
- OGC API Features collection generation
- Extent and CRS handling
- Link generation
- Queryables schema generation

#### `DiscoveryAdminEndpointsTests.cs`
- DTO structure validation
- Admin endpoint behavior

### Integration Tests (`/tests/Honua.Server.Integration.Tests/Discovery/`)

#### `PostGisDiscoveryIntegrationTests.cs`
**Real PostgreSQL database tests using Testcontainers**

Tests include:
- ✓ Discovering all geometry tables
- ✓ Schema exclusions
- ✓ Table pattern exclusions
- ✓ Spatial index detection
- ✓ Spatial index requirement filtering
- ✓ Column metadata extraction
- ✓ Extent computation
- ✓ Max table limit enforcement
- ✓ Specific table discovery
- ✓ Tables without primary keys (skipped)
- ✓ Estimated row counts
- ✓ Table descriptions

#### `ZeroConfigDemoE2ETests.cs`
**End-to-end "30-second demo" tests**

Demonstrates:
1. Starting fresh PostgreSQL database
2. Creating geometry tables with psql
3. Enabling auto-discovery
4. Automatic table discovery
5. Querying discovered data

This is the canonical example of zero-configuration deployment.

## Running the Tests

### All Discovery Tests
```bash
# Run all discovery tests
dotnet test --filter "FullyQualifiedName~Discovery"

# Run only unit tests
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj --filter "FullyQualifiedName~Discovery"

# Run only integration tests
dotnet test tests/Honua.Server.Integration.Tests/Honua.Server.Integration.Tests.csproj --filter "FullyQualifiedName~Discovery"
```

### Specific Test Classes
```bash
# Run PostGIS discovery integration tests
dotnet test --filter "FullyQualifiedName~PostGisDiscoveryIntegrationTests"

# Run zero-config demo
dotnet test --filter "FullyQualifiedName~ZeroConfigDemoE2ETests"

# Run cache tests
dotnet test --filter "FullyQualifiedName~CachedTableDiscoveryServiceTests"
```

### With Docker Compose
```bash
# Start test database
cd tests/Honua.Server.Integration.Tests/Discovery
docker-compose -f docker-compose.discovery-tests.yml up -d

# Run integration tests
dotnet test --filter "FullyQualifiedName~Discovery"

# Stop test database
docker-compose -f docker-compose.discovery-tests.yml down
```

## Test Infrastructure

### Docker Compose (`docker-compose.discovery-tests.yml`)
Provides:
- **postgis-test**: PostGIS 16 with test data
- **redis-test**: Redis for caching tests

### Test Data (`test-data/01-create-test-tables.sql`)
Pre-creates tables for manual testing:
- `public.cities` - Point geometries with metadata
- `public.roads` - LineString geometries
- `public.parcels` - Polygon geometries
- `geo.buildings` - Multi-schema testing
- `public.unindexed_points` - No spatial index
- `public.temp_data` - Matches exclusion pattern
- `public._internal` - Excluded by pattern
- `public.no_pk_table` - No primary key (skipped)
- `topology.topo_test` - Excluded schema

## Test Coverage

### Feature Coverage
- ✅ Table discovery from geometry_columns
- ✅ Schema exclusions
- ✅ Table pattern exclusions (wildcards)
- ✅ Spatial index detection
- ✅ Spatial index requirement
- ✅ Primary key validation
- ✅ Column metadata extraction
- ✅ Extent computation
- ✅ Table descriptions/comments
- ✅ Max table limits
- ✅ Friendly name generation
- ✅ Geometry type normalization
- ✅ Multiple SRID support
- ✅ Caching behavior
- ✅ Cache invalidation
- ✅ Background refresh
- ✅ OData model generation
- ✅ OGC collection generation
- ✅ Admin endpoints

### Database Scenarios
- ✅ PostgreSQL with PostGIS
- ✅ Multiple schemas
- ✅ Various geometry types (Point, LineString, Polygon, Multi*)
- ✅ Different SRIDs
- ✅ With and without spatial indexes
- ✅ With and without primary keys
- ✅ With and without table comments
- ✅ Empty tables
- ✅ Tables with data

## Configuration Options Tested

All `AutoDiscoveryOptions` properties are tested:
- `Enabled`
- `DiscoverPostGISTablesAsODataCollections`
- `DiscoverPostGISTablesAsOgcCollections`
- `DefaultSRID`
- `ExcludeSchemas`
- `ExcludeTablePatterns`
- `RequireSpatialIndex`
- `MaxTables`
- `CacheDuration`
- `UseFriendlyNames`
- `GenerateOpenApiDocs`
- `ComputeExtentOnDiscovery`
- `IncludeNonSpatialTables`
- `DefaultFolderId`
- `DefaultFolderTitle`
- `DataSourceId`
- `BackgroundRefresh`
- `BackgroundRefreshInterval`

## Performance Considerations

Integration tests use Testcontainers which:
- Automatically pull Docker images (first run is slower)
- Start fresh containers for isolation
- Clean up after tests complete

Typical test execution times:
- Unit tests: < 1 second
- Integration tests: 10-30 seconds (includes container startup)
- E2E tests: 15-45 seconds

## Continuous Integration

Tests are designed to run in CI/CD pipelines:
- No manual setup required
- Uses Testcontainers for isolation
- Parallel execution safe (each test has own container)
- Automatic cleanup on failure

## Debugging Tests

### Enable verbose logging
```bash
dotnet test --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Discovery"
```

### Inspect test database
```bash
# Start test database
docker-compose -f docker-compose.discovery-tests.yml up -d

# Connect to database
docker exec -it honua-discovery-test-db psql -U testuser -d testdb

# Run queries
SELECT * FROM geometry_columns;
SELECT tablename FROM pg_tables WHERE schemaname = 'public';
```

### Keep containers running
```bash
# In test code, add a delay before cleanup:
await Task.Delay(TimeSpan.FromMinutes(5)); // Before DisposeAsync

# Then inspect while test is paused
```

## Adding New Tests

### Unit Test Template
```csharp
[Fact]
public async Task NewFeature_ExpectedBehavior()
{
    // Arrange
    var service = CreateTestService();

    // Act
    var result = await service.NewMethodAsync();

    // Assert
    Assert.NotNull(result);
}
```

### Integration Test Template
```csharp
[Fact]
public async Task NewFeature_WithRealDatabase()
{
    // Arrange
    await CreateTestTableAsync("test_table");
    var service = CreateDiscoveryService();

    // Act
    var tables = await service.DiscoverTablesAsync("test-datasource");

    // Assert
    Assert.Single(tables);
}
```

## Common Issues

### Tests fail with "Docker not available"
- Ensure Docker Desktop is running
- Check Docker is accessible: `docker ps`

### Tests time out
- Increase test timeout in xUnit
- Check Docker container health
- Verify network connectivity

### Flaky tests
- Check for shared state between tests
- Ensure proper cleanup in `DisposeAsync`
- Use `IAsyncLifetime` for setup/teardown

## Contributing

When adding auto-discovery features:
1. Add unit tests first (TDD)
2. Add integration tests for database scenarios
3. Update E2E tests if user-facing behavior changes
4. Document new configuration options
5. Update this README

## Questions?

See the main discovery documentation at:
- `/docs/ARCHITECTURE_CLARIFICATION.md`
- `/docs/auto-discovery.md` (if exists)
- `/src/Honua.Server.Core/Discovery/README.md` (if exists)
