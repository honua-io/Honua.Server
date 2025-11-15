# Honua.Server.Host.Tests

Unit tests for the Honua.Server.Host project, focusing on OGC API service classes.

## Test Organization

### OGC Services Tests (`Ogc/Services/`)

#### ConformanceServiceTests
Tests for the `ConformanceService` class that manages OGC API conformance declarations.

**Test Coverage:**
- Returns default conformance classes
- Includes service-specific conformance classes
- Filters out null/empty conformance classes
- Removes duplicate conformance classes
- Async operation completion
- Constructor validation

#### LandingPageServiceTests
Tests for the `LandingPageService` class that generates OGC API landing pages and API definitions.

**Test Coverage:**
- JSON format response
- HTML format response
- Service metadata inclusion
- Correct link generation
- API definition retrieval
- File not found handling
- Constructor validation

#### CollectionServiceTests
Tests for the `CollectionService` class that manages OGC API collections (feature layers).

**Test Coverage:**
- Returns all collections
- Cache hit/miss scenarios
- JSON and HTML format handling
- Disabled services filtering
- Specific collection retrieval
- 404 for unknown collections
- Layer metadata transformation
- Extent calculation
- Constructor validation

#### OgcResponseBuilderTests
Tests for the `OgcResponseBuilder` utility class for building OGC API responses.

**Test Coverage:**
- RFC 7807 validation problem responses (400)
- RFC 7807 not found problem responses (404)
- Collection resolution error mapping
- Collection summary building
- CRS formatting
- Custom response headers
- Content-Crs header
- Style ID ordering

## Test Data Builders (`Builders/`)

### TestDataBuilders.cs
Provides builder classes for creating test data:

- **LayerDefinitionBuilder**: Creates test `LayerDefinition` instances with fluent API
- **ServiceDefinitionBuilder**: Creates test `ServiceDefinition` instances with fluent API
- **MetadataSnapshotBuilder**: Creates test `MetadataSnapshot` instances with fluent API

## Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ConformanceServiceTests"

# Run tests by category
dotnet test --filter "Category=Unit"
```

## Test Patterns

All tests follow these patterns:

1. **Arrange-Act-Assert (AAA)**: Clear separation of test phases
2. **Moq for mocking**: Dependencies are mocked using Moq
3. **FluentAssertions**: Readable assertions with helpful error messages
4. **xUnit**: Test framework with `[Fact]` and `[Theory]` attributes
5. **Trait categorization**: All tests marked with `[Trait("Category", "Unit")]`

## Dependencies

- xUnit 2.9.2
- Moq 4.20.72
- FluentAssertions 7.0.0
- Microsoft.NET.Test.Sdk 17.11.1
- Microsoft.Extensions.Caching.Memory 9.0.10
- Microsoft.Extensions.Options 9.0.10
- NetTopologySuite 2.6.0

## Coverage Goals

- Target: 80%+ code coverage
- Focus on critical paths and edge cases
- Mock external dependencies
- Test both success and failure scenarios
