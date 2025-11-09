# Testing Guide

This document describes how to run tests efficiently in the HonuaIO project.

## Quick Start

### Option 1: Using the unified test script (Recommended)

```bash
# Clean, build, and run all tests in parallel
./scripts/test-all.sh

# Skip clean step for faster iterations
./scripts/test-all.sh --skip-clean

# Run specific tests
./scripts/test-all.sh --filter "Category=Unit"
./scripts/test-all.sh --filter "Category!=STAC"

# With code coverage
./scripts/test-all.sh --coverage

# Use more/fewer parallel threads
./scripts/test-all.sh --max-threads 6  # More aggressive
./scripts/test-all.sh --max-threads 2  # More conservative
```

### Option 2: Using dotnet test directly

```bash
# Clean and build first (IMPORTANT: do this once)
dotnet clean
dotnet build -c Release

# Run all tests (uses .runsettings automatically)
dotnet test --no-build

# Run with filter
dotnet test --no-build --filter "Category!=STAC"
dotnet test --no-build --filter "FullyQualifiedName~PostgreSQL"

# With code coverage
dotnet test --no-build --collect:"XPlat Code Coverage"

# Specific test project
dotnet test tests/Honua.Server.Core.Tests.Data --no-build
```

### Option 3: Using the C# parallel script

```bash
# Build first
dotnet build -c Release

# Run tests in parallel (no rebuild)
./scripts/run-tests-csharp-parallel.sh --no-build --max-threads 4

# With filter
./scripts/run-tests-csharp-parallel.sh --no-build --filter "Category=Unit"
```

## Key Principles

### 1. Build Once, Test Many

**Problem**: Building during parallel test execution causes race conditions and file locking issues.

**Solution**: Always build first, then run tests with `--no-build`:

```bash
# ✅ CORRECT: Build once, then test
dotnet build -c Release
dotnet test --no-build

# ❌ WRONG: Build during test execution
dotnet test  # This rebuilds during testing
```

### 2. Parallel Execution

Tests run in parallel using xUnit test collections:
- **4 parallel threads** by default (conservative to avoid Docker resource issues)
- Each test collection gets its own PostgreSQL container
- Tests use transaction rollback for isolation

Configure parallelization:
- Via script: `--max-threads N`
- Via environment: `export MaxParallelThreads=6`
- Via runsettings: Edit `tests/.runsettings`
- Via xunit.runner.json: Edit `tests/xunit.runner.json`

### 3. Test Filtering

Skip tests that require infrastructure you don't have:

```bash
# Skip STAC tests (require PostgreSQL with STAC schema)
dotnet test --filter "Category!=STAC"

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Skip E2E tests
dotnet test --filter "Category!=E2E"

# Combine filters
dotnet test --filter "Category!=STAC&Category!=E2E"
```

## Common Workflows

### Development Workflow

```bash
# First time: full clean and build
./scripts/test-all.sh

# Fast iterations: skip clean
./scripts/test-all.sh --skip-clean

# Even faster: just unit tests
./scripts/test-all.sh --skip-clean --filter "Category=Unit"
```

### Pre-commit Validation

```bash
# Run unit and integration tests (skip E2E)
./scripts/test-all.sh --filter "Category!=E2E"
```

### Full Test Suite

```bash
# Everything with coverage
./scripts/test-all.sh --coverage
```

### Debugging Individual Tests

```bash
# Build first
dotnet build

# Run specific test
dotnet test tests/Honua.Server.Core.Tests.Data \
  --no-build \
  --filter "FullyQualifiedName~PostgresFunctionRepositoryTests" \
  --logger "console;verbosity=detailed"
```

## Configuration Files

### tests/.runsettings
Primary configuration for `dotnet test`:
- Parallel execution settings
- Environment variables
- Code coverage configuration
- Logger configuration

### tests/xunit.runner.json
xUnit-specific configuration:
- `maxParallelThreads`: 4 (default)
- `parallelizeTestCollections`: true
- `parallelizeAssembly`: true

### tests/appsettings.Test.json
Test environment configuration:
- QuickStart authentication enabled
- Test database connections
- Logging configuration

## Test Categories

Tests are organized using `[Trait("Category", "...")]`:

- **Unit**: Fast, isolated unit tests
- **Integration**: Tests with database/external dependencies
- **E2E**: End-to-end deployment tests
- **STAC**: Tests requiring STAC PostgreSQL schema
- **Slow**: Tests that take >5 seconds

## Troubleshooting

### "PostgreSQL test container is not available"

**Problem**: Too many parallel tests overwhelming Docker.

**Solution**:
```bash
# Reduce parallel threads
./scripts/test-all.sh --max-threads 2

# Or skip PostgreSQL-dependent tests
dotnet test --filter "Category!=Integration"
```

### "QuickStart authentication mode is disabled"

**Problem**: Test environment not configured.

**Solution**:
```bash
export ASPNETCORE_ENVIRONMENT=Test
export HONUA_ALLOW_QUICKSTART=true
dotnet test --no-build
```

### File locking / assembly conflicts

**Problem**: Building during test execution.

**Solution**:
```bash
# Always build first
dotnet build -c Release
dotnet test --no-build
```

### Out of memory

**Problem**: Too many parallel tests.

**Solution**:
```bash
# Reduce parallelism
./scripts/test-all.sh --max-threads 2

# Or run tests sequentially
./scripts/test-all.sh --sequential
```

## Performance Tips

1. **Use Release configuration**: `dotnet build -c Release`
   - ~30% faster test execution
   - Better approximates production behavior

2. **Skip clean when possible**: `./scripts/test-all.sh --skip-clean`
   - Saves 10-20 seconds per run
   - Safe for most changes

3. **Use filters**: `--filter "Category=Unit"`
   - Run only relevant tests
   - Faster feedback

4. **Adjust parallelism**: `--max-threads N`
   - More threads = faster (if you have resources)
   - Fewer threads = more stable (fewer Docker issues)

5. **Use scripts, not direct dotnet test**:
   - Scripts handle build/no-build correctly
   - Better error handling
   - Progress reporting

## CI/CD Integration

### GitHub Actions
```yaml
- name: Run tests
  run: ./scripts/test-all.sh --coverage
```

### GitLab CI
```yaml
test:
  script:
    - ./scripts/test-all.sh --coverage
```

### Azure DevOps
```yaml
- script: ./scripts/test-all.sh --coverage
  displayName: 'Run Tests'
```

## Getting Help

- View script help: `./scripts/test-all.sh --help`
- Check test setup: `./scripts/verify-test-setup.sh`
- Review parallel testing guide: `PARALLEL_TESTING_SUMMARY.md`
