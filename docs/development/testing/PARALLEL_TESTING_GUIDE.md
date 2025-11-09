# Parallel Testing Guide

Comprehensive guide for running HonuaIO's test suite in parallel, optimized for multi-core systems (22+ cores).

## Overview

The HonuaIO test infrastructure supports massive parallelization across three test suites:

- **C# Tests (xUnit)**: 13 test projects with transaction-based database isolation
- **Python Tests (pytest)**: 13 test files for OGC protocol compliance
- **QGIS Tests (pytest)**: 13 test files for QGIS client integration

**Total Tests**: 500+ tests across all suites

## Architecture

```
Master Test Orchestrator (22 cores)
├── C# Test Runner (10-12 cores)
│   ├── xUnit: maxParallelThreads = 6
│   ├── Database: 1 PostgreSQL container per collection
│   ├── Isolation: Transaction-based (fast rollback)
│   └── Test projects run in parallel
│
├── Python Test Runner (5 cores)
│   ├── pytest-xdist: -n 5 workers
│   ├── Server: Shared Honua instance (SQLite)
│   └── Tests: Read-only (inherently parallel-safe)
│
└── QGIS Test Runner (5 cores)
    ├── pytest-xdist: -n 5 workers
    ├── Server: Shared Honua instance (SQLite)
    └── Tests: Read-only (inherently parallel-safe)
```

## Quick Start

### 1. Build Cached Test Image

Build a Docker image with preloaded test data for fast startup:

```bash
./scripts/build-test-cache.sh
```

This creates the `honua:test-cached` image with:
- Prebuilt Honua binaries (ReadyToRun optimized)
- SQLite databases (`ogc-sample.db`, `stac-catalog.db`)
- Test metadata configuration
- Authentication database

**Build time**: ~2-3 minutes (one-time cost)

### 2. Run All Tests in Parallel

```bash
./scripts/run-tests-parallel.sh
```

This will:
1. Start the cached Honua test server
2. Run C#, Python, and QGIS tests concurrently
3. Collect and aggregate results
4. Generate comprehensive summary

**Expected time**: ~5-10 minutes (vs. 30-45 minutes sequential)

### 3. Run Individual Test Suites

Run only specific test suites:

```bash
# C# tests only
./scripts/run-tests-parallel.sh --csharp-only

# Python tests only
./scripts/run-tests-parallel.sh --python-only

# QGIS tests only
./scripts/run-tests-parallel.sh --qgis-only
```

## Advanced Usage

### Filter Tests

Run only specific test categories:

```bash
# Run only unit tests (C#)
./scripts/run-tests-parallel.sh --filter "Unit"

# Run only smoke tests (Python)
./scripts/run-tests-parallel.sh --python-only --filter "smoke"

# Run only WMS tests (QGIS)
./scripts/run-tests-parallel.sh --qgis-only --filter "wms"
```

### Adjust Parallelization

Tune worker counts for your system:

```bash
# Use all 22 cores aggressively
./scripts/run-tests-parallel.sh \
  --csharp-threads 10 \
  --python-workers 6 \
  --qgis-workers 6

# Conservative (slower but safer)
./scripts/run-tests-parallel.sh \
  --csharp-threads 4 \
  --python-workers 3 \
  --qgis-workers 3
```

### Collect Coverage

Generate code coverage reports:

```bash
# C# coverage
./scripts/run-tests-parallel.sh --csharp-only --coverage

# Python coverage
./scripts/run-tests-parallel.sh --python-only --coverage

# All coverage
./scripts/run-tests-parallel.sh --coverage
```

Coverage reports are generated in `TestResults/`:
- C#: `TestResults/CoverageReport/index.html`
- Python: `TestResults/python/coverage/index.html`

### Generate HTML Reports

Create detailed HTML test reports:

```bash
./scripts/run-tests-parallel.sh --html
```

Reports are generated in `TestResults/`:
- Python: `TestResults/python/report.html`
- QGIS: `TestResults/qgis/report.html`

### Sequential Execution

Run test suites one at a time (useful for debugging):

```bash
./scripts/run-tests-parallel.sh --sequential
```

### Stop on First Failure

Exit immediately when any test suite fails:

```bash
./scripts/run-tests-parallel.sh --stop-on-fail
```

## Individual Test Runners

### C# Tests

```bash
# Run all C# tests (6 parallel threads)
./scripts/run-tests-csharp-parallel.sh

# Run specific projects
./scripts/run-tests-csharp-parallel.sh --projects "Honua.Server.Core.Tests,Honua.Server.Host.Tests"

# Run with filter
./scripts/run-tests-csharp-parallel.sh --filter "Category=Unit"

# Collect coverage
./scripts/run-tests-csharp-parallel.sh --coverage

# Adjust parallelism
./scripts/run-tests-csharp-parallel.sh --max-threads 10

# Verbose output
./scripts/run-tests-csharp-parallel.sh --verbose
```

**xUnit Configuration**: `tests/xunit.runner.json`
- `maxParallelThreads`: 6 (configurable)
- `parallelizeAssembly`: true
- `parallelizeTestCollections`: true

### Python Tests

```bash
# Run all Python tests (5 parallel workers)
./scripts/run-tests-python-parallel.sh

# Run specific markers
./scripts/run-tests-python-parallel.sh --filter "smoke"

# Adjust workers
./scripts/run-tests-python-parallel.sh -n 8

# Generate HTML report
./scripts/run-tests-python-parallel.sh --html

# Collect coverage
./scripts/run-tests-python-parallel.sh --coverage

# Use existing server (don't start new one)
./scripts/run-tests-python-parallel.sh --no-server
```

**Pytest Configuration**: `tests/python/pytest.ini`

Available markers:
- `smoke`: Quick smoke tests (<30s)
- `integration`: Integration tests (2-5min)
- `read_only`: Safe for parallel execution
- `write`: Requires isolation
- `wms`, `wfs`, `wmts`, `wcs`, `csw`, `stac`, `ogc_features`, `ogc_tiles`, `ogc_processes`, `geoservices`

### QGIS Tests

```bash
# Run all QGIS tests (5 parallel workers)
./scripts/run-tests-qgis-parallel.sh

# Run specific markers
./scripts/run-tests-qgis-parallel.sh --filter "wms"

# Adjust workers
./scripts/run-tests-qgis-parallel.sh -n 8

# Generate HTML report
./scripts/run-tests-qgis-parallel.sh --html

# Use existing server
./scripts/run-tests-qgis-parallel.sh --no-server
```

**Requirements**:
- QGIS installed with Python bindings (PyQGIS)
- `QT_QPA_PLATFORM=offscreen` for headless execution

## Database Isolation Strategy

### C# Tests

**Pattern**: Shared PostgreSQL container with transaction-based isolation

```csharp
[Collection("SharedPostgres")]
public class MyTests : IClassFixture<SharedPostgresFixture>
{
    public async Task TestMethod()
    {
        var (connection, transaction) = await fixture.CreateTransactionScopeAsync();
        try
        {
            // Test code here - all changes are isolated
        }
        finally
        {
            await transaction.RollbackAsync();  // Automatic cleanup
        }
    }
}
```

**Benefits**:
- **Fast**: Single container startup, transaction rollback is cheap
- **Isolated**: Each test gets a clean transaction
- **Parallel-safe**: Multiple collections run in parallel with separate containers

**Configuration**:
- Container image: `postgis/postgis:16-3.4`
- Isolation level: `ReadCommitted`
- Cleanup: Automatic rollback on test completion

### Python/QGIS Tests

**Pattern**: Shared SQLite database (read-only)

- No external database required
- Fast startup (<5 seconds)
- Inherently parallel-safe (read-only operations)
- Single Docker container shared across all workers

## Performance Benchmarks

### Sequential vs. Parallel Execution

| Test Suite | Sequential | Parallel (22 cores) | Speedup |
|------------|-----------|---------------------|---------|
| C# Tests | ~15-20 min | ~5-7 min | **3x** |
| Python Tests | ~8-10 min | ~2-3 min | **4x** |
| QGIS Tests | ~10-12 min | ~3-4 min | **3x** |
| **Total** | **33-42 min** | **8-12 min** | **~4x** |

### Resource Usage

| Test Suite | CPU Cores | Memory (peak) | PostgreSQL Containers |
|------------|-----------|---------------|----------------------|
| C# Tests | 10-12 | ~4-6 GB | 6 (one per collection) |
| Python Tests | 5 | ~1-2 GB | 0 (SQLite) |
| QGIS Tests | 5 | ~2-3 GB | 0 (SQLite) |
| **Total** | **20-22** | **~8 GB** | **6** |

## Troubleshooting

### PostgreSQL Container Issues

**Problem**: C# tests fail to start PostgreSQL containers

**Solution**:
```bash
# Check Docker resources
docker system df

# Clean up old containers
docker system prune -a

# Increase Docker memory limit (Docker Desktop)
# Settings → Resources → Memory: 8 GB minimum
```

### Port Conflicts

**Problem**: Port 8080 already in use

**Solution**:
```bash
# Find and stop conflicting process
lsof -i :8080
docker-compose -f docker-compose.test-parallel.yml down

# Or use a different port
docker-compose -f docker-compose.test-parallel.yml up -d \
  -e ASPNETCORE_URLS=http://+:8081
```

### Python Virtual Environment Issues

**Problem**: pytest-xdist not found

**Solution**:
```bash
# Recreate Python environment
cd tests/python
rm -rf venv
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

### QGIS Not Found

**Problem**: QGIS Python bindings not available

**Solution**:
```bash
# Ubuntu/Debian
sudo apt-get install qgis python3-qgis

# Arch
sudo pacman -S qgis python-qgis

# macOS
brew install qgis

# Set PYTHONPATH
export PYTHONPATH=/usr/share/qgis/python:$PYTHONPATH
```

### Slow Test Execution

**Problem**: Tests running slower than expected

**Diagnostics**:
```bash
# Check system resources
top
htop

# Check Docker stats
docker stats

# Check PostgreSQL container count
docker ps | grep postgres

# Check disk I/O
iostat -x 1
```

**Solutions**:
- Reduce parallel worker counts
- Close unnecessary applications
- Use SSD instead of HDD
- Increase Docker memory allocation

### Test Failures

**Problem**: Intermittent test failures

**Debugging**:
```bash
# Run with verbose output
./scripts/run-tests-parallel.sh --verbose

# Run sequentially to isolate issues
./scripts/run-tests-parallel.sh --sequential

# Run specific suite
./scripts/run-tests-csharp-parallel.sh --filter "FailingTest"

# Check logs
docker-compose -f docker-compose.test-parallel.yml logs
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Parallel Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Build test cache
        run: ./scripts/build-test-cache.sh

      - name: Run parallel tests
        run: ./scripts/run-tests-parallel.sh --coverage --html

      - name: Upload results
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults/
```

### GitLab CI

```yaml
test:parallel:
  stage: test
  image: mcr.microsoft.com/dotnet/sdk:9.0
  services:
    - docker:dind
  script:
    - ./scripts/build-test-cache.sh
    - ./scripts/run-tests-parallel.sh --coverage --html
  artifacts:
    reports:
      junit:
        - TestResults/**/junit.xml
        - TestResults/**/*.trx
    paths:
      - TestResults/
```

## Best Practices

### 1. Always Build Test Cache First

The cached image significantly speeds up Python/QGIS tests:

```bash
# Build once per day or when test data changes
./scripts/build-test-cache.sh
```

### 2. Use Filters for Fast Feedback

During development, run only relevant tests:

```bash
# Unit tests only (fast)
./scripts/run-tests-parallel.sh --filter "Unit"

# Smoke tests only
./scripts/run-tests-python-parallel.sh --filter "smoke"
```

### 3. Run Full Suite Before Commits

Before pushing changes, run the full test suite:

```bash
./scripts/run-tests-parallel.sh --coverage
```

### 4. Monitor Resource Usage

Keep an eye on system resources:

```bash
# Terminal 1: Run tests
./scripts/run-tests-parallel.sh

# Terminal 2: Monitor resources
watch -n 1 'docker stats --no-stream && echo && free -h'
```

### 5. Clean Up Regularly

Clean up Docker resources periodically:

```bash
# Stop test containers
docker-compose -f docker-compose.test-parallel.yml down

# Clean up unused resources
docker system prune -a --volumes
```

## Configuration Files

### xUnit Runner Configuration

**File**: `tests/xunit.runner.json`

```json
{
  "maxParallelThreads": 6,
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "longRunningTestSeconds": 60
}
```

### Pytest Configuration

**File**: `tests/python/pytest.ini`

```ini
[pytest]
markers =
    smoke: Quick smoke tests
    integration: Integration tests
    read_only: Safe for parallel execution
    write: Requires isolation
```

### Docker Compose Configuration

**File**: `docker-compose.test-parallel.yml`

- Image: `honua:test-cached`
- Port: 8080
- Health check: `/health` endpoint
- Environment: SQLite, no auth, in-memory cache

## Performance Tuning

### For 22-Core Systems (Recommended)

```bash
./scripts/run-tests-parallel.sh \
  --csharp-threads 6 \
  --python-workers 5 \
  --qgis-workers 5
```

### For 16-Core Systems

```bash
./scripts/run-tests-parallel.sh \
  --csharp-threads 5 \
  --python-workers 4 \
  --qgis-workers 4
```

### For 8-Core Systems

```bash
./scripts/run-tests-parallel.sh \
  --csharp-threads 3 \
  --python-workers 2 \
  --qgis-workers 2
```

### For 4-Core Systems (Sequential Recommended)

```bash
./scripts/run-tests-parallel.sh \
  --sequential \
  --csharp-threads 2 \
  --python-workers 2 \
  --qgis-workers 2
```

## Summary

The HonuaIO parallel testing infrastructure provides:

✅ **4x faster** test execution on 22-core systems
✅ **Database isolation** via transactions (C#) and read-only SQLite (Python/QGIS)
✅ **Cached test image** for instant startup
✅ **Flexible configuration** for different system sizes
✅ **Comprehensive coverage** across OGC protocols
✅ **Easy CI/CD integration**

**Total test time**: ~8-12 minutes (vs. 33-42 minutes sequential)

For questions or issues, see:
- [Test Infrastructure Report](../tests/TEST_INFRASTRUCTURE_COMPLETE_SUMMARY.md)
- [Integration Testing Strategy](./INTEGRATION_TESTING_STRATEGY.md)
- [GitHub Issues](https://github.com/honuaio/honua/issues)
