# Location Services Test Coverage

This document provides an overview of the comprehensive test coverage for the Honua Location Services functionality.

## Test Organization

### Unit Tests (`Honua.Server.Core.Tests.LocationServices/`)

Unit tests verify individual components in isolation using mocks and test doubles.

#### Provider Tests

**Geocoding Providers:**
- `AzureMapsGeocodingProviderTests.cs` - Azure Maps geocoding (14 tests)
  - Constructor validation
  - Geocoding with various parameters (MaxResults, CountryCodes, BoundingBox, Language)
  - Reverse geocoding
  - Error handling (HTTP errors, invalid responses)
  - Connectivity testing
  - Empty results handling

- `NominatimGeocodingProviderTests.cs` - Nominatim (OSM) geocoding (15 tests)
  - Constructor validation with defaults
  - Geocoding with filtering options
  - Reverse geocoding with localization
  - Parameterized tests for multiple cities
  - Rate limiting consideration
  - Error scenarios

**Routing Providers:**
- `AzureMapsRoutingProviderTests.cs` - Azure Maps routing (14 tests)
  - Route calculation with traffic
  - Multiple travel modes (car, truck, bicycle, pedestrian)
  - Vehicle specifications for commercial routing
  - Avoid options (tolls, highways, ferries)
  - Departure time scheduling
  - Multi-waypoint routing
  - Error handling

- `OsrmRoutingProviderTests.cs` - OSRM routing (10 tests)
  - Travel mode mapping
  - Multi-leg route calculation
  - OSRM-specific error handling
  - Waypoint validation

**Basemap Tile Providers:**
- `AzureMapsBasemapTileProviderTests.cs` - Azure Maps tiles (14 tests)
  - Available tilesets enumeration
  - Tile fetching (raster and vector)
  - High-DPI support (@2x)
  - Language-specific tiles
  - URL template generation
  - Cache header handling
  - Invalid tileset handling

#### Configuration Tests

- `LocationServiceConfigurationTests.cs` (8 tests)
  - Default configuration values
  - Configuration binding from appsettings
  - Provider-specific configurations (Azure Maps, Nominatim, OSRM, OSM Tiles)
  - Configuration combinations

- `LocationServiceExtensionsTests.cs` (9 tests)
  - Service registration (AddLocationServices)
  - Provider resolution by key
  - Case-insensitive provider lookup
  - Default and keyed service registration
  - Multiple provider support
  - Provider switching

#### Test Utilities

- `MockGeocodingProvider.cs` - Configurable mock for testing
- `MockRoutingProvider.cs` - Routing test double
- `MockBasemapTileProvider.cs` - Tile provider mock
- `LocationServiceTestFixture.cs` - Shared test infrastructure with xUnit collection fixture

**Total Unit Tests: 84 tests**

### Integration Tests (`Honua.Server.Integration.Tests/LocationServices/`)

Integration tests verify behavior against real services (skipped by default, require API keys/internet).

- `NominatimIntegrationTests.cs` (4 tests)
  - Real address geocoding
  - Reverse geocoding with actual coordinates
  - Public endpoint connectivity
  - Country filtering

- `OsrmIntegrationTests.cs` (4 tests)
  - Real route calculation
  - Multiple travel modes
  - Public OSRM connectivity

- `AzureMapsIntegrationTests.cs` (4 tests)
  - Azure Maps geocoding with API key
  - Azure Maps routing
  - Tile fetching
  - Service health checks

**Total Integration Tests: 12 tests**

### API Endpoint Tests (`Honua.Server.Host.Tests/LocationServices/`)

- `LocationServicesEndpointsTests.cs` (13 tests)
  - Geocoding endpoint behavior
  - Reverse geocoding endpoint
  - Routing endpoint
  - Tileset enumeration endpoint
  - Tile retrieval endpoint
  - Error handling and validation
  - Cache header propagation
  - Provider switching
  - Health check endpoints

**Total Endpoint Tests: 13 tests**

## Test Coverage Summary

| Component | Unit Tests | Integration Tests | Endpoint Tests | Total |
|-----------|-----------|-------------------|----------------|-------|
| Geocoding | 29 | 4 | 3 | 36 |
| Routing | 24 | 4 | 2 | 30 |
| Basemap Tiles | 14 | 1 | 3 | 18 |
| Configuration | 17 | 0 | 0 | 17 |
| Endpoints | 0 | 0 | 13 | 13 |
| **Total** | **84** | **12** | **13** | **109** |

## Code Coverage Targets

- **Target Coverage: >80%**
- Provider implementations: ~90%
- Configuration: ~95%
- Extensions: ~85%
- Models: 100% (DTOs)

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test --filter "Category=Unit"
```

### Run Integration Tests (requires API keys)
```bash
# Set environment variables or user secrets
export AZURE_MAPS_SUBSCRIPTION_KEY="your-key"

dotnet test --filter "Category=Integration"
```

### Run Tests for Specific Provider
```bash
dotnet test --filter "Service=Nominatim"
dotnet test --filter "Service=AzureMaps"
dotnet test --filter "Service=OSRM"
```

### Generate Coverage Report
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Patterns

### Given-When-Then Naming
Tests follow the Given-When-Then pattern:
```csharp
[Fact]
public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
{
    // Arrange (Given)
    // Act (When)
    // Assert (Then)
}
```

### Parameterized Tests
Use `[Theory]` for testing multiple scenarios:
```csharp
[Theory]
[InlineData("car")]
[InlineData("bicycle")]
[InlineData("pedestrian")]
public async Task CalculateRouteAsync_WithDifferentTravelModes_SupportsAllModes(string travelMode)
```

### Mock HTTP Responses
Use Moq to simulate HTTP responses:
```csharp
private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object responseContent)
{
    var mockHandler = new Mock<HttpMessageHandler>();
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ...)
        .ReturnsAsync(new HttpResponseMessage { ... });
    return new HttpClient(mockHandler.Object);
}
```

## Test Fixtures

### LocationServiceTestFixture
Shared test infrastructure providing:
- Pre-configured IConfiguration
- Mock logger factory
- HttpClient factory
- Common test data

Used via xUnit collection fixture:
```csharp
[Collection("LocationService")]
public class MyTests
{
    private readonly LocationServiceTestFixture _fixture;

    public MyTests(LocationServiceTestFixture fixture)
    {
        _fixture = fixture;
    }
}
```

## CI/CD Integration

### Test Categories
- `Unit` - Fast, no external dependencies
- `Integration` - Requires external services (skipped in CI without secrets)
- `E2E` - Full end-to-end tests

### GitHub Actions Example
```yaml
- name: Run Unit Tests
  run: dotnet test --filter "Category=Unit" --logger "trx"

- name: Run Integration Tests
  if: env.AZURE_MAPS_KEY != ''
  run: dotnet test --filter "Category=Integration"
  env:
    AZURE_MAPS_SUBSCRIPTION_KEY: ${{ secrets.AZURE_MAPS_KEY }}
```

## Future Enhancements

- [ ] Add tests for AWS Location Service provider
- [ ] Add tests for Google Maps provider
- [ ] Add caching behavior tests (CachedProvidersTests.cs)
- [ ] Add metrics/monitoring tests (MonitoredProvidersTests.cs)
- [ ] Add load/performance tests
- [ ] Add chaos engineering tests (network failures, timeouts)
- [ ] Add contract tests for external APIs

## Contributing

When adding new location service features:

1. Write unit tests first (TDD)
2. Add integration tests (with Skip attribute)
3. Update endpoint tests if API changes
4. Ensure >80% code coverage
5. Follow existing naming patterns
6. Use test utilities/fixtures to reduce duplication
7. Document any special test setup requirements

## Dependencies

Test projects use:
- **xUnit** - Test framework
- **FluentAssertions** - Assertion library
- **Moq** - Mocking framework
- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing
- **Coverlet** - Code coverage

## Related Documentation

- [Location Services README](../../../src/Honua.Server.Core/LocationServices/README.md)
- [Azure Maps API Docs](https://learn.microsoft.com/en-us/rest/api/maps/)
- [Nominatim API Docs](https://nominatim.org/release-docs/latest/api/Overview/)
- [OSRM API Docs](http://project-osrm.org/docs/v5.24.0/api/)
