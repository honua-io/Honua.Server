# Honua.MapSDK Test Suite

Comprehensive test coverage for the Honua MapSDK - a Blazor component library for interactive web mapping.

## Test Organization

### Components/ - Component Tests
- **HonuaMapTests.cs** - Tests for the main HonuaMap component (MapLibre integration)
  - Component initialization and configuration
  - Event handling (OnMapReady, OnExtentChanged, OnFeatureClicked)
  - Message bus integration
  - Public API methods (FlyToAsync, FitBoundsAsync)
  - Accessibility and responsive behavior
  - Disposal and cleanup

### Services/ - Service Tests
- **ComponentBusTests.cs** - Tests for the message bus (pub/sub pattern)
  - Basic publish/subscribe functionality
  - Multiple subscribers and handlers
  - Message args (source, timestamp, correlation ID)
  - Different message types
  - Unsubscribe and clear operations
  - Error handling
  - Complex workflows

- **MapConfigurationServiceTests.cs** - Tests for configuration management
  - Export as JSON (formatted and compact)
  - Export as YAML
  - Export as HTML embed code
  - Export as Blazor component
  - Import from JSON and YAML
  - Configuration validation (required fields, value ranges, duplicates)

### Integration/ - Integration Tests
- **MapProviderIntegrationTests.cs** - Integration with external providers
  - Geocoding provider integration (search, autocomplete, reverse geocoding)
  - Routing provider integration (route calculation, alternatives, waypoints)
  - Basemap tile provider integration (tilesets, URL templates, switching)
  - End-to-end workflows (search + navigate, route + visualize, click + geocode)

### Infrastructure/ - Test Infrastructure
- **MapTestContext.cs** - Base test context for bUnit component tests
  - Registers MapSDK services
  - Configures JSInterop for testing
  - Provides fresh context per test

### Utilities/ - Test Utilities
- **MockJSRuntime.cs** - Mock JavaScript interop for testing
  - Tracks JS invocations
  - Configurable return values
  - Mock JSObjectReference for module imports

- **MapTestFixture.cs** - Shared test data and fixtures
  - Map configuration builders
  - Test geometry (points, polygons, routes)
  - Test feature properties
  - Test bounds and coordinates

- **GeometryTestHelpers.cs** - Geometry validation and utilities
  - GeoJSON validation (points, polygons)
  - Coordinate validation (lon/lat, bounds, zoom, bearing, pitch)
  - Distance calculation (Haversine formula)
  - Bounds calculations
  - Polyline encoding/decoding

## Running Tests

### Run All Tests
```bash
dotnet test /home/user/Honua.Server/tests/Honua.MapSDK.Tests
```

### Run Unit Tests Only
```bash
dotnet test /home/user/Honua.Server/tests/Honua.MapSDK.Tests --filter "Category=Unit"
```

### Run Integration Tests Only
```bash
dotnet test /home/user/Honua.Server/tests/Honua.MapSDK.Tests --filter "Category=Integration"
```

### Run with Coverage
```bash
dotnet test /home/user/Honua.Server/tests/Honua.MapSDK.Tests --collect:"XPlat Code Coverage"
```

## Test Patterns

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern:
```csharp
[Fact]
public void Component_ShouldRenderWithDefaultParameters()
{
    // Arrange
    var cut = Context.RenderComponent<HonuaMap>();

    // Act
    // (rendering happens in Arrange for component tests)

    // Assert
    cut.Should().NotBeNull();
    cut.Markup.Should().Contain("class=\"honua-map");
}
```

### Given-When-Then Naming
Test names follow the Given-When-Then pattern:
- **Given**: Initial state or context (e.g., "Component", "ValidConfiguration")
- **When**: Action being tested (e.g., "ShouldRender", "ShouldValidate")
- **Then**: Expected outcome (e.g., "WithDefaultParameters", "Successfully")

### Parameterized Tests
Use `[Theory]` and `[InlineData]` for testing multiple scenarios:
```csharp
[Theory]
[InlineData(0, true)]
[InlineData(10, true)]
[InlineData(22, true)]
[InlineData(-1, false)]
[InlineData(23, false)]
public void IsValidZoom_ShouldValidateCorrectly(double zoom, bool expected)
{
    // Act
    var result = GeometryTestHelpers.IsValidZoom(zoom);

    // Assert
    result.Should().Be(expected);
}
```

## Test Coverage Goals

- **Component Tests**: >80% code coverage
  - All public properties and parameters
  - All event callbacks
  - All message subscriptions
  - Lifecycle methods (initialization, disposal)

- **Service Tests**: >80% code coverage
  - All public methods
  - All validation rules
  - Error handling paths
  - Edge cases

- **Integration Tests**: Key workflows
  - Provider connectivity
  - End-to-end scenarios
  - Cross-component communication

## Dependencies

### Testing Frameworks
- **xUnit** - Test framework
- **bUnit** - Blazor component testing
- **FluentAssertions** - Fluent assertion library
- **Moq** - Mocking framework
- **NSubstitute** - Alternative mocking framework

### UI Libraries
- **MudBlazor** - UI component library (used by tested components)

### Test Utilities
- **Microsoft.AspNetCore.TestHost** - ASP.NET Core testing
- **RichardSzalay.MockHttp** - HTTP mocking
- **coverlet.collector** - Code coverage

## Writing New Tests

### Component Tests
1. Inherit from `MapComponentTestBase`
2. Use `Context.RenderComponent<T>()` to render components
3. Test markup with `cut.Markup.Should().Contain()`
4. Test instance properties with `cut.Instance.PropertyName`
5. Dispose context automatically (handled by base class)

Example:
```csharp
public class MyComponentTests : MapComponentTestBase
{
    [Fact]
    public void MyComponent_ShouldRender()
    {
        var cut = Context.RenderComponent<MyComponent>();
        cut.Markup.Should().Contain("expected-class");
    }
}
```

### Service Tests
1. Create instance of service in constructor or test method
2. Test all public methods
3. Test validation and error cases
4. Use FluentAssertions for readable assertions

Example:
```csharp
public class MyServiceTests
{
    private readonly MyService _service = new();

    [Fact]
    public void MyMethod_WithValidInput_ShouldSucceed()
    {
        var result = _service.MyMethod(validInput);
        result.Should().NotBeNull();
    }
}
```

### Integration Tests
1. Mock external dependencies (providers)
2. Test cross-component communication via ComponentBus
3. Test end-to-end workflows
4. Mark with `[Trait("Category", "Integration")]`

## Code Coverage Report

Generate HTML coverage report:
```bash
cd /home/user/Honua.Server/tests/Honua.MapSDK.Tests
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:TestResults/*/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
```

View report: `CoverageReport/index.html`

## Continuous Integration

Tests are run automatically on:
- Pull requests
- Main branch commits
- Release builds

CI configuration ensures:
- All tests pass
- Code coverage meets threshold (>80%)
- No test warnings or errors

## Troubleshooting

### JS Interop Errors
If you see JS interop errors, ensure:
- `Context.JSInterop.Mode = JSRuntimeMode.Loose` is set
- Mock JS runtime is configured for expected calls

### Component Rendering Errors
If components fail to render:
- Check that required services are registered in `MapTestContext`
- Verify all required parameters are provided
- Check for null reference exceptions in component code

### Test Isolation Issues
If tests interfere with each other:
- Ensure each test gets fresh `Context` (provided by `MapComponentTestBase`)
- Clear ComponentBus subscriptions if needed
- Avoid static state in components

## Performance

- Unit tests should complete in milliseconds
- Integration tests may take longer (seconds)
- Total test suite should complete in <30 seconds

## Best Practices

1. **Test Behavior, Not Implementation** - Test what components do, not how they do it
2. **One Assert Per Test** - Each test should verify one thing (with FluentAssertions, multiple assertions are OK if they relate to the same behavior)
3. **Independent Tests** - Tests should not depend on execution order
4. **Clear Test Names** - Test names should describe what is being tested
5. **Use Test Fixtures** - Reuse common test data from `MapTestFixture`
6. **Mock External Dependencies** - Don't call real services in unit tests
7. **Test Error Cases** - Don't just test the happy path
8. **Keep Tests Fast** - Avoid unnecessary delays or waits

## Future Test Additions

Components to add when implemented:
- **HonuaLeafletTests** - Leaflet component tests
- **LayerControlTests** - Layer switcher tests
- **DrawingToolbarTests** - Drawing tools tests
- **GeocodingSearchTests** - Search control tests
- **RoutePanelTests** - Routing panel tests
- **LayerManagerTests** - Layer service tests
- **DrawingManagerTests** - Drawing service tests
- **MeasurementManagerTests** - Measurement tests
- **RouteVisualizationServiceTests** - Route rendering tests

## Contact

For questions about tests, contact the Honua MapSDK team.
