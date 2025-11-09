# Parallel Testing Quick Reference

One-page cheat sheet for running HonuaIO tests in parallel.

## Quick Commands

```bash
# 1. Build test cache (one-time, ~2-3 min)
./scripts/build-test-cache.sh

# 2. Run all tests in parallel (~8-12 min)
./scripts/run-tests-parallel.sh

# 3. Run specific suite
./scripts/run-tests-parallel.sh --csharp-only
./scripts/run-tests-parallel.sh --python-only
./scripts/run-tests-parallel.sh --qgis-only

# 4. Run with coverage
./scripts/run-tests-parallel.sh --coverage --html
```

## Individual Test Runners

```bash
# C# tests (xUnit)
./scripts/run-tests-csharp-parallel.sh
./scripts/run-tests-csharp-parallel.sh --filter "Category=Unit"
./scripts/run-tests-csharp-parallel.sh --coverage

# Python tests (pytest)
./scripts/run-tests-python-parallel.sh
./scripts/run-tests-python-parallel.sh --filter "smoke"
./scripts/run-tests-python-parallel.sh -n 8 --html

# QGIS tests (pytest)
./scripts/run-tests-qgis-parallel.sh
./scripts/run-tests-qgis-parallel.sh --filter "wms"
./scripts/run-tests-qgis-parallel.sh -n 8 --html
```

## Configuration

### Adjust Worker Counts

```bash
# Use all 22 cores
./scripts/run-tests-parallel.sh \
  --csharp-threads 10 \
  --python-workers 6 \
  --qgis-workers 6

# Conservative (8 cores)
./scripts/run-tests-parallel.sh \
  --csharp-threads 3 \
  --python-workers 2 \
  --qgis-workers 2
```

### Environment Variables

```bash
# Override defaults
export CSHARP_THREADS=8
export PYTHON_WORKERS=6
export QGIS_WORKERS=6

./scripts/run-tests-parallel.sh
```

## Common Filters

### C# (xUnit)

```bash
--filter "Category=Unit"           # Unit tests only
--filter "Category=Integration"    # Integration tests
--filter "Category!=Slow"          # Exclude slow tests
--filter "FullyQualifiedName~Wms"  # WMS tests only
```

### Python (pytest)

```bash
--filter "smoke"          # Smoke tests
--filter "integration"    # Integration tests
--filter "read_only"      # Parallel-safe tests
--filter "wms"           # WMS protocol tests
--filter "stac"          # STAC tests
```

### QGIS (pytest)

```bash
--filter "wms"           # WMS layer tests
--filter "wfs"           # WFS feature tests
--filter "ogc_features"  # OGC API Features
--filter "comprehensive" # Full test suite
```

## Results Location

```
TestResults/
├── CoverageReport/         # C# coverage (if --coverage)
│   └── index.html
├── python/
│   ├── junit.xml
│   ├── report.html         # If --html
│   └── coverage/           # If --coverage
│       └── index.html
└── qgis/
    ├── junit.xml
    └── report.html         # If --html
```

## Troubleshooting

### Test server not starting

```bash
# Check if port is in use
lsof -i :8080

# Stop existing containers
docker-compose -f docker-compose.test-parallel.yml down

# Rebuild test cache
./scripts/build-test-cache.sh --no-cache
```

### PostgreSQL issues

```bash
# Clean up Docker
docker system prune -a

# Check Docker resources
docker system df
```

### Python environment issues

```bash
# Recreate venv
cd tests/python
rm -rf venv
python3 -m venv venv
source venv/bin/activate
pip install -r requirements.txt
```

### QGIS not found

```bash
# Ubuntu/Debian
sudo apt-get install qgis python3-qgis

# Set PYTHONPATH
export PYTHONPATH=/usr/share/qgis/python:$PYTHONPATH
```

## Performance Benchmarks

| System | Time (Sequential) | Time (Parallel) | Speedup |
|--------|------------------|-----------------|---------|
| 22 cores | 33-42 min | 8-12 min | 4x |
| 16 cores | 33-42 min | 10-15 min | 3x |
| 8 cores | 33-42 min | 15-20 min | 2x |

## Resource Usage

| Suite | CPU | Memory | DB Containers |
|-------|-----|--------|---------------|
| C# | 10-12 cores | 4-6 GB | 6 PostgreSQL |
| Python | 5 cores | 1-2 GB | 0 (SQLite) |
| QGIS | 5 cores | 2-3 GB | 0 (SQLite) |

## Advanced Options

```bash
# Sequential execution (debugging)
./scripts/run-tests-parallel.sh --sequential

# Stop on first failure
./scripts/run-tests-parallel.sh --stop-on-fail

# Skip building test cache
./scripts/run-tests-parallel.sh --no-build

# Verbose output
./scripts/run-tests-csharp-parallel.sh --verbose
```

## Test Server Management

```bash
# Start test server
docker-compose -f docker-compose.test-parallel.yml up -d

# Check health
curl http://localhost:8080/health

# View logs
docker-compose -f docker-compose.test-parallel.yml logs -f

# Stop server
docker-compose -f docker-compose.test-parallel.yml down
```

## CI/CD Integration

```yaml
# GitHub Actions
- name: Run tests
  run: ./scripts/run-tests-parallel.sh --coverage

# GitLab CI
script:
  - ./scripts/run-tests-parallel.sh --coverage
```

## Database Isolation

### C# Tests
- **Pattern**: Shared container + transaction isolation
- **Cleanup**: Automatic rollback
- **Speed**: Very fast (transaction overhead only)

### Python/QGIS Tests
- **Pattern**: Shared SQLite database
- **Cleanup**: Not needed (read-only)
- **Speed**: Very fast (no database overhead)

## File Locations

| File | Purpose |
|------|---------|
| `scripts/run-tests-parallel.sh` | Master orchestrator |
| `scripts/run-tests-csharp-parallel.sh` | C# test runner |
| `scripts/run-tests-python-parallel.sh` | Python test runner |
| `scripts/run-tests-qgis-parallel.sh` | QGIS test runner |
| `scripts/build-test-cache.sh` | Build cached image |
| `tests/xunit.runner.json` | xUnit configuration |
| `tests/python/pytest.ini` | pytest configuration |
| `Dockerfile.test-cached` | Cached image definition |
| `docker-compose.test-parallel.yml` | Test server compose |

## Support

For detailed documentation, see:
- [Parallel Testing Guide](./PARALLEL_TESTING_GUIDE.md)
- [Integration Testing Strategy](./INTEGRATION_TESTING_STRATEGY.md)

For issues: https://github.com/honuaio/honua/issues
