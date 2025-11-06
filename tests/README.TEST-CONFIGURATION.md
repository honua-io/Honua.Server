# Honua Server Test Configuration Guide

## Overview

This guide explains the test configuration architecture for Honua Server, optimized for fast parallel execution and easy maintenance.

## Configuration Architecture

### Single Source of Truth: appsettings.Testing.json

All test configuration is centralized in `/src/Honua.Server.Host/appsettings.Testing.json`. This file:

- Enables ALL services and features for comprehensive testing
- Uses QuickStart authentication (no auth required)
- Configures optimal settings for test performance
- Eliminates configuration sprawl across multiple files

### Environment Selection

The `ASPNETCORE_ENVIRONMENT=Testing` variable loads `appsettings.Testing.json` automatically:

```yaml
# docker-compose.shared-test-env.yml
environment:
  - ASPNETCORE_ENVIRONMENT=Testing
```

### Configuration Override Hierarchy

ASP.NET Core configuration follows this precedence (highest to lowest):

1. **Environment variables** (e.g., `ConnectionStrings__HonuaDb`)
2. **appsettings.{Environment}.json** (e.g., `appsettings.Testing.json`)
3. **appsettings.json** (base configuration)

Use environment variables ONLY for:
- Container-specific paths (`/app/data` vs local paths)
- Service hostnames (`postgres-test-shared` in Docker)
- Connection strings that vary by environment

## Test Metadata Templates

Located in `/tests/TestData/metadata/`:

### minimal-metadata.json
- **Purpose**: Quick smoke tests, CI pipelines
- **Services**: OGC Features only
- **Layers**: 1 simple layer
- **Use case**: Fast validation (<5 seconds startup)

### standard-metadata.json (default)
- **Purpose**: Comprehensive test suite
- **Services**: All OGC APIs (Features, Tiles, Processes, Coverages)
- **Layers**: Multiple layers with different geometries
- **Raster datasets**: Included for tile/coverage testing
- **Use case**: Full integration testing

### enterprise-metadata.json
- **Purpose**: Enterprise feature testing
- **Services**: SensorThings, Geoprocessing, Events
- **Use case**: Enterprise subscription features

**Switching templates:**
```yaml
environment:
  - HONUA__METADATA__PATH=/app/data/metadata/minimal-metadata.json
```

## Python Test Configuration

### pytest.ini

Optimized for parallel execution with pytest-xdist:

```ini
[pytest]
addopts = 
    -n auto              # Auto-detect CPU cores
    --dist loadfile      # Distribute by file for better parallelism
    --durations=10       # Show slowest 10 tests
```

### requirements.txt

Pinned versions for reproducible test environments:
- Core: `pytest==8.3.4`, `pytest-xdist==3.6.1`
- OGC: `OWSLib==0.31.0`, `pyproj==3.7.0`
- Geospatial: `geopandas==1.0.1`, `rasterio==1.4.3`

**Update dependencies:**
```bash
pip install --upgrade <package>
pip freeze > requirements.txt
```

## Docker Test Environment

### Shared Test Environment

Single long-running instance for all tests:

```bash
# Start (builds image if needed)
cd tests && bash start-shared-test-env.sh start

# Stop
bash start-shared-test-env.sh stop

# Rebuild after code changes
bash start-shared-test-env.sh rebuild
```

### Services Available

| Service | Port | Purpose |
|---------|------|---------|
| honua-test | 5100 | Main API server |
| postgres-test | 5433 | PostgreSQL + PostGIS |
| redis-test | 6380 | Redis cache |
| qdrant-test | 6334 | Vector search |

### Health Check

Container uses `/health` endpoint to verify startup:

```bash
curl http://localhost:5100/health
```

## Running Tests

### Quick Start (Recommended)

```bash
cd tests/python
source .venv/bin/activate
pytest -n auto  # Parallel execution with all CPU cores
```

### Selective Test Execution

```bash
# Run specific test file
pytest test_ogc_features.py -n auto

# Run specific test
pytest test_ogc_features.py::test_get_collections -v

# Run by marker
pytest -m ogc -n auto           # Only OGC tests
pytest -m "not slow" -n auto   # Skip slow tests

# Stop on first failure
pytest -x -n auto
```

### Performance Tuning

For high-performance laptops (8+ cores, 16+ GB RAM):

```bash
# Use all cores (default with -n auto)
pytest -n auto

# Limit workers (if hitting resource limits)
pytest -n 4

# Show test timing
pytest -n auto --durations=20
```

## Development Containers

### VS Code Dev Container

Open project in VS Code and select "Reopen in Container":

- **Auto-configured**: Python, .NET 9, Docker-in-Docker
- **Extensions**: Python testing, C# DevKit, Docker
- **Services**: All test services pre-started
- **Performance**: Uses volume caching for fast rebuilds

### Manual Setup

```bash
# Install dependencies
cd tests/python
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# Start test environment
cd ../
bash start-shared-test-env.sh start

# Run tests
cd python
pytest -n auto
```

## Troubleshooting

### Tests Failing with 404 Errors

**Symptom**: `/ogc/collections` returns 404

**Fix**: Legacy API redirect middleware not enabled. Rebuild Docker image:
```bash
cd tests && bash start-shared-test-env.sh rebuild
```

### Tests Failing with 401 Unauthorized

**Symptom**: Authentication errors despite QuickStart mode

**Fix**: Verify Testing environment is active:
```bash
docker logs honua-test-shared | grep "ASPNETCORE_ENVIRONMENT"
# Should show: Testing
```

### Docker Build Slow

**Symptom**: `docker build` takes 7+ minutes

**Expected**: Normal for clean builds. Subsequent builds use layer caching (~30s).

**Speed up**:
- Use shared test environment (build once, reuse)
- Avoid unnecessary rebuilds (only rebuild after code changes)

### Tests Not Running in Parallel

**Symptom**: Tests run sequentially

**Fix**: Ensure pytest-xdist is installed:
```bash
pip install pytest-xdist==3.6.1
pytest -n auto  # Should show "gw0 ... gw7" for 8 workers
```

### Database Lock Errors (SQLite)

**Symptom**: `database is locked` errors

**Fix**: SQLite doesn't handle high concurrency well. Use fewer workers:
```bash
pytest -n 4  # Instead of -n auto
```

Or switch to PostgreSQL for integration tests (edit `ConnectionStrings__HonuaDb` in docker-compose).

## Configuration Best Practices

### DO:
✅ Use `appsettings.Testing.json` for test-wide configuration  
✅ Use environment variables for container-specific paths  
✅ Use metadata templates for different test scenarios  
✅ Pin exact dependency versions in `requirements.txt`  
✅ Use pytest markers for selective test execution  

### DON'T:
❌ Scatter configuration across multiple files  
❌ Override Testing config with individual env vars  
❌ Mix test data between test runs (use fixtures)  
❌ Skip parallel execution (wastes time and resources)  
❌ Run full suite on every code change (use markers)  

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Python Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Start test environment
        run: cd tests && bash start-shared-test-env.sh start
      
      - name: Run tests
        run: |
          cd tests/python
          python -m venv .venv
          source .venv/bin/activate
          pip install -r requirements.txt
          pytest -n auto --junitxml=results.xml
      
      - name: Publish test results
        uses: EnricoMi/publish-unit-test-result-action@v2
        if: always()
        with:
          files: tests/python/results.xml
```

## Performance Benchmarks

### Test Execution Times (Reference Hardware: 8-core, 16GB RAM)

| Test Suite | Sequential | Parallel (-n auto) | Speedup |
|------------|------------|-------------------|---------|
| OGC Features (50 tests) | 45s | 8s | 5.6x |
| OGC Tiles (30 tests) | 60s | 12s | 5.0x |
| Full Suite (350 tests) | ~15 min | ~3 min | 5.0x |

### Docker Build Times

| Scenario | Time | Notes |
|----------|------|-------|
| Clean build | ~7 min | First build with no cache |
| Incremental build | ~30s | Code changes only |
| Config-only change | ~10s | No rebuild needed (restart container) |

## Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [pytest-xdist Documentation](https://pytest-xdist.readthedocs.io/)
- [OWS Lib Documentation](https://owslib.readthedocs.io/)
- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
