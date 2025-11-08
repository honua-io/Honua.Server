# Honua.MapSDK Test Suite

Comprehensive test coverage for the Honua MapSDK - a Blazor component library for interactive web mapping, providing unit tests, integration tests, and documentation for maintaining code quality and preventing regressions.

## Table of Contents

- [Overview](#overview)
- [Test Organization](#test-organization)
- [Running Tests](#running-tests)
- [Writing New Tests](#writing-new-tests)
- [Test Patterns](#test-patterns)
- [Coverage Goals](#coverage-goals)
- [CI/CD Integration](#cicd-integration)
- [Troubleshooting](#troubleshooting)

## Overview

This test suite covers all major components of the Honua.MapSDK:

- **23+ UI Components**: Map, MapLibre, DataGrid, Chart, Legend, FilterPanel, Search, Timeline, Editor, CoordinateDisplay, AttributeTable, and more
- **Core Services**: ComponentBus, MapConfigurationService, FeatureEditService
- **Models**: FilterDefinition, MapConfiguration
- **Integration**: Component communication, provider integration, and synchronization

### Testing Stack

- **xUnit** - Test framework
- **bUnit** - Blazor component testing
- **FluentAssertions** - Expressive assertions
- **Moq** - Mocking framework
- **NSubstitute** - Alternative mocking framework
- **Coverlet** - Code coverage

## Test Organization

```
tests/Honua.MapSDK.Tests/
├── Components/              # Component tests (bUnit)
│   ├── HonuaMapTests.cs
│   ├── HonuaEditorTests.cs
│   ├── HonuaCoordinateDisplayTests.cs
│   ├── HonuaAttributeTableTests.cs
│   ├── HonuaDataGridTests.cs
│   ├── HonuaChartTests.cs
│   ├── HonuaLegendTests.cs
│   ├── HonuaFilterPanelTests.cs
│   ├── HonuaSearchTests.cs
│   └── HonuaTimelineTests.cs
├── Services/                # Service tests
│   ├── ComponentBusTests.cs
│   ├── MapConfigurationServiceTests.cs
│   ├── FeatureEditServiceTests.cs
│   └── ...
├── Integration/             # Integration tests
│   ├── MapProviderIntegrationTests.cs
│   ├── ComponentBusIntegrationTests.cs
│   ├── MapDataGridSyncTests.cs
│   ├── FilteringIntegrationTests.cs
│   └── TimelineIntegrationTests.cs
├── Infrastructure/          # Test infrastructure
│   └── MapTestContext.cs
├── Utilities/               # Test utilities
│   ├── MockJSRuntime.cs
│   ├── MapTestFixture.cs
│   ├── GeometryTestHelpers.cs
│   └── CoordinateConverterTests.cs
└── TestHelpers/             # Shared test utilities
    ├── TestComponentBus.cs
    ├── MockHttpMessageHandler.cs
    ├── TestData.cs
    └── BunitTestContext.cs
```

### Test Categories

#### Component Tests
- Test individual Blazor components in isolation
- Verify rendering, parameters, and events
- Use bUnit for component testing
- Mock dependencies via TestComponentBus

#### Integration Tests
- Test interactions between multiple components
- Verify ComponentBus message flow
- Test data synchronization
- Validate filtering across components
- Test provider integrations (geocoding, routing, basemap)

#### Service Tests
- Test business logic and utilities
- Verify data transformations
- Test validation logic
- Mock external dependencies

## Running Tests

### Quick Start

```bash
# From repository root
dotnet test

# From test project directory
cd tests/Honua.MapSDK.Tests
dotnet test
```

### Run Specific Test Category

```bash
# Component tests only
dotnet test --filter "FullyQualifiedName~ComponentTests"

# Integration tests only
dotnet test --filter "FullyQualifiedName~IntegrationTests"
dotnet test --filter "Category=Integration"

# Service tests only
dotnet test --filter "FullyQualifiedName~ServiceTests"

# Unit tests only
dotnet test --filter "Category=Unit"
```

### Run Single Test Class

```bash
dotnet test --filter "FullyQualifiedName~ComponentBusTests"
```

### Run with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report (requires reportgenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Development Workflow

```bash
# Watch mode - auto-run tests on file changes
dotnet watch test

# Verbose output
dotnet test -v detailed

# Run tests in parallel (default)
dotnet test --parallel

# Run tests sequentially
dotnet test --parallel none
```

## Writing New Tests

### Test Class Template

```csharp
using Bunit;
using FluentAssertions;
using Honua.MapSDK.Tests.TestHelpers;
using Xunit;

namespace Honua.MapSDK.Tests.ComponentTests;

/// <summary>
/// Tests for NewComponent
/// </summary>
public class NewComponentTests : IDisposable
{
    private readonly BunitTestContext _testContext;

    public NewComponentTests()
    {
        _testContext = new BunitTestContext();
    }

    public void Dispose()
    {
        _testContext.Dispose();
    }

    [Fact]
    public void NewComponent_ShouldRenderWithDefaultSettings()
    {
        // Arrange & Act
        var cut = _testContext.RenderComponent<NewComponent>(parameters => parameters
            .Add(p => p.Id, "test-component"));

        // Assert
        cut.Should().NotBeNull();
        cut.Markup.Should().NotBeNullOrEmpty();
    }
}
```

### Test Method Template (AAA Pattern)

```csharp
[Fact]
public void MethodUnderTest_Scenario_ExpectedBehavior()
{
    // Arrange - Setup test data and dependencies
    var testData = TestData.SampleCities;
    var expectedResult = "expected value";

    // Act - Execute the code under test
    var actualResult = MethodUnderTest(testData);

    // Assert - Verify the results
    actualResult.Should().Be(expectedResult);
}
```

### Theory Tests (Data-Driven)

```csharp
[Theory]
[InlineData(0, "zero")]
[InlineData(1, "one")]
[InlineData(5, "five")]
public void ConvertToWord_WithNumber_ReturnsWord(int input, string expected)
{
    // Arrange & Act
    var result = NumberConverter.ToWord(input);

    // Assert
    result.Should().Be(expected);
}
```

### Async Tests

```csharp
[Fact]
public async Task PublishAsync_ShouldInvokeSubscribers()
{
    // Arrange
    var bus = new TestComponentBus();
    var messageReceived = false;

    bus.Subscribe<TestMessage>(args =>
    {
        messageReceived = true;
        return Task.CompletedTask;
    });

    // Act
    await bus.PublishAsync(new TestMessage(), "source");

    // Assert
    messageReceived.Should().BeTrue();
}
```

## Test Patterns

### Component Testing Pattern

```csharp
[Fact]
public void Component_WithParameter_ShouldApplyParameter()
{
    // Arrange & Act
    var cut = _testContext.RenderComponent<HonuaMap>(parameters => parameters
        .Add(p => p.Id, "test-map")
        .Add(p => p.Zoom, 10)
        .Add(p => p.Center, new[] { -122.4194, 37.7749 }));

    // Assert
    cut.Should().NotBeNull();
    // Verify parameter was applied (implementation specific)
}
```

### ComponentBus Testing Pattern

```csharp
[Fact]
public async Task Component_OnAction_ShouldPublishMessage()
{
    // Arrange
    var cut = _testContext.RenderComponent<TestComponent>(parameters => parameters
        .Add(p => p.Id, "test"));

    // Act - Trigger action (implementation specific)

    // Assert
    var messages = _testContext.ComponentBus.GetMessages<TestMessage>();
    messages.Should().HaveCount(1);
    messages[0].Property.Should().Be("expected value");
}
```

### Mocking HTTP Requests

```csharp
[Fact]
public async Task Service_WithHttpCall_ShouldReturnData()
{
    // Arrange
    var handler = MockHttpMessageHandler.CreateJsonHandler(TestData.SampleGeoJson);
    var httpClient = new HttpClient(handler);
    var service = new GeocodingService(httpClient);

    // Act
    var result = await service.SearchAsync("San Francisco");

    // Assert
    result.Should().NotBeNull();
    handler.Requests.Should().HaveCount(1);
}
```

## Coverage Goals

### Overall Targets

- **80%+ overall code coverage**
- **90%+ for critical paths** (ComponentBus, filters, core services)
- **70%+ for UI components**
- **100% for utility functions**

### Checking Coverage

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage percentage
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:Html

# Open coverage/index.html in browser
```

## CI/CD Integration

Tests run automatically on:
- **Pull Requests** - All tests must pass
- **Commits to main** - Full test suite + coverage report
- **Nightly builds** - Extended test suite

### GitHub Actions

See `.github/workflows/test.yml` for the CI configuration.

```yaml
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v3
```

## Troubleshooting

### Common Issues

#### Tests Fail Intermittently

**Problem**: Async tests sometimes fail due to timing issues.

**Solution**: Use `WaitForMessageAsync` or add appropriate delays:
```csharp
await Task.Delay(100); // Give component time to update
var message = await bus.WaitForMessageAsync<TestMessage>();
```

#### ComponentBus Messages Not Received

**Problem**: Subscriber not receiving published messages.

**Solution**: Ensure subscriber is registered before publishing:
```csharp
// Subscribe first
bus.Subscribe<TestMessage>(handler);

// Then publish
await bus.PublishAsync(message, "source");
```

#### bUnit Tests Throw Exceptions

**Problem**: JSInterop not configured properly.

**Solution**: Use BunitTestContext which configures JSInterop:
```csharp
var _testContext = new BunitTestContext();
// JSInterop is configured automatically
```

#### Coverage Not Generating

**Problem**: Coverage reports not created.

**Solution**: Install coverlet.collector package:
```bash
dotnet add package coverlet.collector
```

## Best Practices

1. **One Assertion Per Test** (or closely related assertions)
2. **Descriptive Test Names** - MethodUnderTest_Scenario_ExpectedBehavior
3. **AAA Pattern** - Arrange, Act, Assert
4. **No Test Interdependencies** - Tests should run in any order
5. **Use Test Helpers** - Share common setup via TestData and helpers
6. **Mock External Dependencies** - Use MockHttpMessageHandler for HTTP calls
7. **Clean Up Resources** - Implement IDisposable for test contexts
8. **Test Both Success and Failure Paths**
9. **Keep Tests Fast** - Avoid unnecessary delays
10. **Document Complex Tests** - Add comments explaining non-obvious logic

## Contributing

When adding new features to MapSDK:

1. Write tests first (TDD approach recommended)
2. Ensure all tests pass
3. Maintain or improve code coverage
4. Update test documentation if needed
5. Follow existing test patterns

## License

Copyright (c) Honua.io. All rights reserved.
