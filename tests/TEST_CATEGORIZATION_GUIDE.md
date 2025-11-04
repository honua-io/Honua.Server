# Test Categorization Guide

## Overview

All tests in the Honua project are categorized using xUnit traits to enable selective test execution in different CI/CD scenarios. This improves build times and provides flexibility in test execution strategies.

## Trait Categories

### Category Trait

Used to classify tests by their scope and dependencies:

#### `[Trait("Category", "Unit")]`
- **Execution Time**: < 1 second per test
- **Dependencies**: None (or minimal in-memory dependencies)
- **Scope**: Tests single classes/methods in isolation
- **Examples**:
  - Pure logic tests
  - DTO/Model tests
  - Utility/helper method tests
  - Parser tests
  - Validation logic tests
- **CI Usage**: Always run on every PR

#### `[Trait("Category", "Integration")]`
- **Execution Time**: 1-10 seconds per test
- **Dependencies**: Requires external services or infrastructure
- **Scope**: Tests interaction between components
- **Examples**:
  - Database tests (with TestContainers)
  - HTTP client tests
  - Cache provider tests
  - Message queue tests
  - Third-party service integration tests
- **CI Usage**: Run on main branch merges and when explicitly requested

#### `[Trait("Category", "E2E")]`
- **Execution Time**: 10+ seconds per test
- **Dependencies**: Full system deployment
- **Scope**: Tests complete workflows from end to end
- **Examples**:
  - Deployment workflow tests
  - Multi-agent orchestration tests
  - Complete API workflow tests
  - Cross-service integration tests
- **CI Usage**: Run on nightly builds only

### Speed Trait

Used to mark tests that are exceptionally slow:

#### `[Trait("Speed", "Slow")]`
- **Execution Time**: > 30 seconds per test
- **Use Cases**:
  - Performance benchmarks
  - Large data set processing
  - Stress tests
  - Long-running integration scenarios
- **CI Usage**: Run on nightly builds only

### Specialized Category Traits

Additional categorical traits for specific test types:

#### `[Trait("Category", "Docker")]`
- Tests that require Docker containers
- May be slow depending on container startup time
- Typically also marked as Integration or E2E

#### `[Trait("Category", "Performance")]`
- Performance regression tests
- Benchmarks and load tests
- Usually also marked with `Speed=Slow`

#### `[Trait("Category", "Security")]`
- Security-specific tests
- Penetration tests
- Authorization/authentication tests

#### `[Trait("Category", "BugHunting")]`
- Edge case tests
- Regression tests for specific bugs
- Data consistency tests

#### `[Trait("Category", "ProcessFramework")]`
- Tests for the AI Process Framework
- Typically integration or E2E level

## Categorization Decision Tree

```
START
  ↓
Does the test require external services? (DB, Redis, S3, etc.)
  ├─ NO → Does it test a single class/method?
  │         ├─ YES → [Trait("Category", "Unit")]
  │         └─ NO → Reconsider, probably needs external service
  │
  └─ YES → Does it test a complete end-to-end workflow?
            ├─ YES → [Trait("Category", "E2E")]
            │         └─ Takes > 30s? → Also add [Trait("Speed", "Slow")]
            │
            └─ NO → [Trait("Category", "Integration")]
                    └─ Takes > 30s? → Also add [Trait("Speed", "Slow")]
```

## Running Tests by Category

### Run only unit tests (fast feedback)
```bash
dotnet test --filter "Category=Unit"
```

### Run unit and integration tests
```bash
dotnet test --filter "Category=Unit|Category=Integration"
```

### Run all tests except slow ones
```bash
dotnet test --filter "Speed!=Slow"
```

### Run only E2E tests
```bash
dotnet test --filter "Category=E2E"
```

### Run specific categories together
```bash
dotnet test --filter "Category=Docker|Category=Security"
```

### Exclude specific categories
```bash
dotnet test --filter "Category!=E2E"
```

## CI/CD Strategy

### Pull Request Builds
- **Tests Run**: Unit tests only
- **Filter**: `Category=Unit`
- **Expected Time**: < 5 minutes
- **Goal**: Fast feedback on code quality

### Main Branch Builds
- **Tests Run**: Unit + Integration tests
- **Filter**: `Category=Unit|Category=Integration&Speed!=Slow`
- **Expected Time**: 10-15 minutes
- **Goal**: Comprehensive validation before merge

### Nightly Builds
- **Tests Run**: All tests including E2E and slow tests
- **Filter**: None (run all)
- **Expected Time**: 30-60 minutes
- **Goal**: Full system validation

## Test Naming Conventions

To make categorization clearer, follow these naming patterns:

- **Unit Tests**: `{ClassName}Tests.cs`
- **Integration Tests**: `{Feature}IntegrationTests.cs`
- **E2E Tests**: `{Workflow}E2ETests.cs`
- **Performance Tests**: `{Feature}PerformanceTests.cs` or `{Feature}Benchmarks.cs`

## Examples

### Unit Test Example
```csharp
[Trait("Category", "Unit")]
public class CrsTransformTests
{
    [Fact]
    public void Transformations_cache_entries_can_be_cleared()
    {
        // Fast, no external dependencies
    }
}
```

### Integration Test Example
```csharp
[Trait("Category", "Integration")]
[Collection("StorageContainers")]
public class S3RasterTileCacheProviderIntegrationTests
{
    [Fact]
    public async Task Should_cache_and_retrieve_tiles()
    {
        // Requires MinIO container
    }
}
```

### E2E Test Example
```csharp
[Trait("Category", "E2E")]
[Trait("Speed", "Slow")]
public class DeploymentWorkflowTests
{
    [Fact]
    public async Task CompleteDeploymentWorkflow_QuickStartMode_ShouldSucceed()
    {
        // Tests complete system deployment
    }
}
```

### Docker Integration Test Example
```csharp
[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public class DockerIntegrationTests
{
    [Fact]
    public async Task Container_starts_and_responds_to_health_checks()
    {
        // Requires Docker to be running
    }
}
```

## Applying Traits to Existing Tests

When categorizing existing tests:

1. Read the test class and understand what it tests
2. Identify external dependencies (databases, containers, APIs)
3. Consider typical execution time
4. Apply the most specific category that fits
5. Add Speed=Slow if the test takes > 30 seconds
6. Add specialized traits (Docker, Performance, etc.) as needed

## Maintenance

- **Adding New Tests**: Always add appropriate trait(s) when creating tests
- **Reviewing PRs**: Ensure new tests have proper categorization
- **CI Failures**: If CI is slow, review test categories and adjust filters
- **Quarterly Review**: Review and recategorize tests as patterns emerge
