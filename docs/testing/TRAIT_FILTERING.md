# Test Trait Filtering Guide

This document explains how to use xUnit traits to filter tests, and how to mark tests that require specific infrastructure.

## Overview

Tests can be marked with traits (categories/tags) that allow selective execution:

```csharp
[Trait("Category", "STAC")]
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public async Task MyTest()
{
    // Test code
}
```

Then filter when running:
```bash
dotnet test --filter "Category!=STAC"  # Skip STAC tests
dotnet test --filter "Category=Unit"    # Only unit tests
```

## Standard Test Categories

### By Speed/Scope
- **Unit**: Fast, isolated, no external dependencies
- **Integration**: Requires database or external services
- **E2E**: Full end-to-end deployment tests
- **Slow**: Takes >5 seconds

### By Infrastructure Required
- **STAC**: Requires PostgreSQL with STAC schema
- **PostgreSQL**: Requires PostgreSQL database
- **Redis**: Requires Redis instance
- **Docker**: Requires Docker daemon
- **Cloud**: Requires cloud provider credentials

### By Feature Area
- **OGC**: OGC protocol tests (WMS, WFS, etc.)
- **REST**: REST API tests
- **Auth**: Authentication/Authorization tests
- **Data**: Data layer tests

## How to Add Traits to Tests

### Individual Test
```csharp
[Fact]
[Trait("Category", "STAC")]
[Trait("Requires", "PostgreSQL")]
public async Task StacCatalogTest()
{
    // Test code
}
```

### Entire Class
```csharp
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public class PostgresFeatureRepositoryTests : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Test1() { }

    [Fact]
    public async Task Test2() { }
}
```

### Using Test Collections
```csharp
[Collection("PostgreSQL")]  // Defined in collection fixture
public class MyTests
{
    // All tests in this collection inherit traits from the collection definition
}
```

## Filter Expressions

### Basic Filters
```bash
# Single category
dotnet test --filter "Category=Unit"

# Exclude category
dotnet test --filter "Category!=STAC"

# Multiple conditions (AND)
dotnet test --filter "Category=Integration&Requires=PostgreSQL"

# Multiple conditions (OR)
dotnet test --filter "Category=Unit|Category=Integration"
```

### Complex Filters
```bash
# Everything except STAC and E2E
dotnet test --filter "Category!=STAC&Category!=E2E"

# Only fast tests
dotnet test --filter "Category=Unit|Category=Integration"

# Skip slow tests
dotnet test --filter "Category!=Slow"
```

### By Test Name
```bash
# Contains "PostgreSQL"
dotnet test --filter "FullyQualifiedName~PostgreSQL"

# Specific test
dotnet test --filter "FullyQualifiedName=Honua.Server.Core.Tests.MyTest"
```

## Recommended Trait Strategy

### Mark STAC Tests
All tests that require STAC schema should be marked:

```csharp
[Trait("Category", "STAC")]
[Trait("Requires", "PostgreSQL")]
public class StacCatalogStoreTests
{
    // Tests that need stac_collections, stac_items tables
}
```

### Mark Integration Tests
Tests with external dependencies:

```csharp
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public class PostgresDataStoreTests
{
    // Tests that need a real database
}
```

### Mark E2E Tests
Full deployment tests:

```csharp
[Trait("Category", "E2E")]
[Trait("Requires", "Docker")]
[Trait("Category", "Slow")]
public class DeploymentE2ETests
{
    // Tests that deploy full system
}
```

## Running Tests with Filters

### Development Workflow
```bash
# Fast feedback - unit tests only
./scripts/test-all.sh --filter "Category=Unit"

# Skip infrastructure-heavy tests
./scripts/test-all.sh --filter "Category!=STAC&Category!=E2E"
```

### CI/CD Workflow
```bash
# Fast feedback pipeline - unit + integration
./scripts/test-all.sh --filter "Category!=E2E"

# Nightly pipeline - everything
./scripts/test-all.sh
```

### Local Development Without Full Infrastructure
```bash
# Skip STAC (don't have schema)
./scripts/test-all.sh --filter "Category!=STAC"

# Skip Docker tests (Docker not running)
./scripts/test-all.sh --filter "Requires!=Docker"
```

## Finding Tests to Tag

### Find STAC-Related Tests
```bash
grep -r "stac" tests/ --include="*.cs" | grep "public.*Test\|public.*Task"
grep -r "StacCollection\|StacItem" tests/ --include="*.cs"
```

### Find PostgreSQL-Dependent Tests
```bash
grep -r "PostgresFixture\|IClassFixture<.*Postgres" tests/ --include="*.cs"
```

### Find Slow Tests
```bash
# Run all tests and check execution time in output
# Mark any test >5 seconds as [Trait("Category", "Slow")]
```

## Example: Marking Existing Tests

### Before
```csharp
public class StacCatalogStoreTests : IClassFixture<PostgresStacFixture>
{
    [Fact]
    public async Task CreateCollection_ValidCollection_ReturnsCreatedCollection()
    {
        // Test code
    }
}
```

### After
```csharp
[Trait("Category", "Integration")]
[Trait("Category", "STAC")]
[Trait("Requires", "PostgreSQL")]
public class StacCatalogStoreTests : IClassFixture<PostgresStacFixture>
{
    [Fact]
    public async Task CreateCollection_ValidCollection_ReturnsCreatedCollection()
    {
        // Test code
    }
}
```

## Alternative: Use SkippableFact

For tests that should be skipped if infrastructure is unavailable:

```csharp
[SkippableFact]
public async Task MyTest()
{
    Skip.If(!fixture.IsAvailable, "PostgreSQL container not available");

    // Test code
}
```

This is useful when:
- Infrastructure availability is dynamic (Docker not running)
- You want tests to "pass" rather than be filtered out
- CI/CD should show "skipped" status

## Performance Impact

### Without Filtering (All Tests)
- Time: 3-5 minutes
- Docker containers: 4-6
- Memory: ~8GB

### With Filtering (Unit Only)
- Time: 30-60 seconds
- Docker containers: 0
- Memory: ~2GB

### With Filtering (No STAC/E2E)
- Time: 2-3 minutes
- Docker containers: 2-4
- Memory: ~4GB

## Updating Scripts

The test scripts support filtering:

```bash
# test-all.sh
./scripts/test-all.sh --filter "Category!=STAC"

# run-tests-csharp-parallel.sh
./scripts/run-tests-csharp-parallel.sh --filter "Category=Unit"

# dotnet test
dotnet test --filter "Category!=STAC&Category!=E2E"
```

## Best Practices

1. **Mark all tests with at least one category**: Unit, Integration, or E2E
2. **Mark infrastructure requirements**: PostgreSQL, Redis, Docker, etc.
3. **Mark slow tests**: Anything >5 seconds
4. **Use class-level traits** when all tests in a class have the same requirements
5. **Document custom traits**: If you create new traits, document them here

## See Also

- xUnit Trait Documentation: https://xunit.net/docs/running-tests-in-parallel
- Test Filter Expressions: https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
