# Test Filtering Guide

## Overview

Honua uses xUnit trait-based test categorization to enable selective test execution based on speed, dependencies, and scope. This allows for efficient CI/CD pipelines while maintaining comprehensive test coverage.

## Quick Reference

### Common Test Filters

```bash
# Run only fast unit tests (PR builds)
dotnet test --filter "Category=Unit"

# Run unit and integration tests (main branch)
dotnet test --filter "Category=Unit|Category=Integration"

# Run all tests except slow ones
dotnet test --filter "Speed!=Slow"

# Run only E2E tests
dotnet test --filter "Category=E2E"

# Run slow/performance tests only
dotnet test --filter "Speed=Slow|Category=Performance"

# Run Docker-specific tests
dotnet test --filter "Category=Docker"

# Run integration tests excluding slow ones
dotnet test --filter "Category=Integration&Speed!=Slow"
```

## Test Categories

### Category=Unit
- **Speed**: < 1 second per test
- **Dependencies**: None (in-memory only)
- **Examples**: Logic tests, parsers, DTOs, utilities
- **When**: Every PR build, every commit

### Category=Integration
- **Speed**: 1-10 seconds per test
- **Dependencies**: External services (databases, caches, containers)
- **Examples**: Database tests, HTTP client tests, cache providers
- **When**: Main branch builds, on-demand

### Category=E2E
- **Speed**: 10+ seconds per test
- **Dependencies**: Full system deployment
- **Examples**: Complete workflow tests, multi-agent orchestration
- **When**: Nightly builds only

### Speed=Slow
- **Speed**: > 30 seconds per test
- **Use Cases**: Performance benchmarks, stress tests, large datasets
- **When**: Nightly builds only

## CI/CD Test Strategy

### Pull Request Builds
**Goal**: Fast feedback (< 5 minutes)

```bash
dotnet test --filter "Category=Unit"
```

**Tests Run**:
- All unit tests
- Fast, isolated tests
- No external dependencies

**Expected Duration**: 2-5 minutes

### Main Branch Builds
**Goal**: Comprehensive validation (10-15 minutes)

```bash
# Unit tests with coverage
dotnet test --filter "Category=Unit" --collect:"XPlat Code Coverage"

# Integration tests (excluding slow)
dotnet test --filter "Category=Integration&Speed!=Slow"
```

**Tests Run**:
- All unit tests (with coverage)
- Fast integration tests
- Database integration tests
- Cache provider tests

**Expected Duration**: 10-15 minutes

### Nightly Builds
**Goal**: Complete validation (30-60 minutes)

```bash
# All categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=E2E"
dotnet test --filter "Category=Performance|Speed=Slow"
dotnet test --filter "Category=Docker"
```

**Tests Run**:
- All unit tests
- All integration tests (including slow)
- All E2E tests
- Performance/benchmark tests
- Docker tests
- Protocol conformance suites

**Expected Duration**: 30-60 minutes

## Advanced Filtering

### Combine Multiple Filters

```bash
# Unit OR Integration tests
dotnet test --filter "Category=Unit|Category=Integration"

# Integration tests AND not slow
dotnet test --filter "Category=Integration&Speed!=Slow"

# Complex: (Unit OR Integration) AND not slow
dotnet test --filter "(Category=Unit|Category=Integration)&Speed!=Slow"
```

### Filter by Test Name

```bash
# Run specific test class
dotnet test --filter "FullyQualifiedName~CrsTransformTests"

# Run tests matching pattern
dotnet test --filter "Name~Transform"

# Combine with category
dotnet test --filter "Category=Unit&Name~Transform"
```

### Filter by Project

```bash
# Run tests in specific project only
dotnet test tests/Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
    --filter "Category=Unit"

# Run unit tests in all projects except one
dotnet test --filter "Category=Unit&FullyQualifiedName!~Honua.Cli.AI"
```

## Test Execution Tips

### Parallel Execution

```bash
# Run tests in parallel (default)
dotnet test --filter "Category=Unit"

# Disable parallel execution (for debugging)
dotnet test --filter "Category=Unit" -- xUnit.ParallelizeTestCollections=false
```

### Verbose Output

```bash
# Normal verbosity
dotnet test --filter "Category=Unit" --logger "console;verbosity=normal"

# Detailed output (for debugging failures)
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"

# Minimal output (for CI)
dotnet test --filter "Category=Unit" --logger "console;verbosity=minimal"
```

### Results and Coverage

```bash
# Generate TRX results
dotnet test --filter "Category=Unit" \
    --logger "trx;LogFileName=test-results.trx" \
    --results-directory ./TestResults

# Collect code coverage
dotnet test --filter "Category=Unit" \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults
```

## Development Workflows

### Local Development

```bash
# Quick validation before commit (1-2 minutes)
dotnet test --filter "Category=Unit"

# Pre-push validation (5-10 minutes)
dotnet test --filter "Category=Unit|Category=Integration&Speed!=Slow"

# Full local validation (20-30 minutes)
dotnet test --filter "Category!=E2E"
```

### Debugging Slow Tests

```bash
# Find all slow tests
dotnet test --filter "Speed=Slow" --logger "console;verbosity=detailed"

# Run integration tests to identify slow ones
dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed" \
    | grep -A5 "Test run for"
```

### Working on Specific Features

```bash
# Only raster-related tests
dotnet test --filter "FullyQualifiedName~Raster"

# Only OGC-related tests
dotnet test --filter "FullyQualifiedName~Ogc|FullyQualifiedName~OData"

# Security tests
dotnet test --filter "FullyQualifiedName~Security|FullyQualifiedName~Auth"
```

## Test Categories Breakdown

### Current Distribution

Based on analysis of 290 test files:

- **Unit Tests**: 88 files (~30%)
  - Fast, isolated tests
  - No external dependencies
  - Average execution: < 100ms per test

- **Integration Tests**: 191 files (~66%)
  - Require external services
  - Use TestContainers where applicable
  - Average execution: 1-5s per test

- **E2E Tests**: 5 files (~2%)
  - Complete workflow validation
  - Full system deployment
  - Average execution: 10-30s per test

- **Slow Tests**: 6 files (~2%)
  - Performance benchmarks
  - Stress tests
  - Average execution: 30s-2m per test

## Troubleshooting

### Tests Not Running

```bash
# Verify trait syntax
dotnet test --filter "Category=Unit" --logger "console;verbosity=detailed"

# List all available traits
dotnet test --list-tests | grep -i trait

# Check specific test file
grep -n "Trait" tests/path/to/YourTests.cs
```

### Filter Not Matching

```bash
# Test filter syntax
dotnet test --filter "help"

# Use parentheses for complex filters
dotnet test --filter "(Category=Unit|Category=Integration)&Speed!=Slow"

# Escape special characters in shell
dotnet test --filter 'Category=Unit'
```

### CI/CD Issues

```bash
# Verify filter in CI logs
echo "Running: dotnet test --filter \"Category=Unit\""
dotnet test --filter "Category=Unit" --logger "console;verbosity=normal"

# Check test count
dotnet test --filter "Category=Unit" --list-tests | wc -l
```

## Best Practices

1. **Always categorize new tests** - Add `[Trait("Category", "...")]` to all test classes

2. **Use appropriate categories** - Follow the decision tree in `TEST_CATEGORIZATION_GUIDE.md`

3. **Mark slow tests** - Add `[Trait("Speed", "Slow")]` for tests > 30 seconds

4. **Run locally before pushing**:
   ```bash
   dotnet test --filter "Category=Unit"
   ```

5. **Don't commit failing tests** - Fix or skip with `[Fact(Skip = "Reason")]`

6. **Use descriptive test names** - Makes filtering by name easier

7. **Keep unit tests fast** - < 1 second per test

8. **Use TestContainers for integration** - Ensures consistent environment

## CI Workflow Triggers

### Automatic Triggers

| Event | Tests Run | Filter |
|-------|-----------|--------|
| PR to main | Unit only | `Category=Unit` |
| Push to main | Unit + Integration | `Category=Unit\|Category=Integration&Speed!=Slow` |
| Nightly (2 AM UTC) | All tests | No filter |
| Manual trigger | Configurable | Specified in workflow input |

### Manual Triggers

Trigger integration tests on PR:
1. Add label `run-integration-tests` to PR
2. Tests will run on next push

Trigger nightly tests manually:
1. Go to Actions → Nightly Comprehensive Tests
2. Click "Run workflow"
3. Select branch and options

## Performance Baselines

Expected test execution times by category:

| Category | Count | Average Time | Total Time |
|----------|-------|-------------|------------|
| Unit | 88 | < 100ms | ~9s |
| Integration (fast) | ~150 | 1-3s | ~5m |
| Integration (slow) | ~40 | 10-30s | ~10m |
| E2E | 5 | 10-30s | ~2m |
| Performance | 6 | 30s-2m | ~6m |

**Total comprehensive suite**: ~30-35 minutes

## References

- [Test Categorization Guide](../tests/TEST_CATEGORIZATION_GUIDE.md)
- [CI/CD Workflows](../.github/workflows/)
- [xUnit Trait Documentation](https://xunit.net/docs/running-tests-in-parallel#traits)
- [.NET Test Filter Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests)
