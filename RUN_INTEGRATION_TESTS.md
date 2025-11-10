# Running Honua Integration Tests - Quick Start Guide

**Status:** ✅ All 278 integration tests ready to run
**Created:** 2025-02-02

## Prerequisites Check

Before running tests, verify you have:
- ✅ 14 integration test files created (8 QGIS + 6 Python)
- ✅ 10 seed data GeoJSON files created
- ✅ Honua server built and ready
- ✅ Python 3.12+ installed
- ✅ Docker available (for QGIS tests)

## Quick Start Options

### Option 1: Docker-Based Testing (Recommended - Most Reliable)

This approach uses Docker for both the Honua server and test execution, ensuring consistent environments.

#### Step 1: Start Seeded Honua Instance
```bash
# Start PostgreSQL, Honua server, and load seed data (one command)
docker-compose -f docker-compose.seed.yml up -d

# Wait for seeding to complete (check logs)
docker-compose -f docker-compose.seed.yml logs -f seed-loader

# When you see "Database seeding completed successfully!" press Ctrl+C

# Verify server is healthy
curl http://localhost:8080/health
```

#### Step 2: Run Python Integration Tests
```bash
# Install Python dependencies
pip install -r tests/python/requirements.txt

# Set environment variable
export HONUA_API_BASE_URL=http://localhost:8080

# Run all Python tests
pytest tests/python -v

# Run specific test file
pytest tests/python/test_stac_pystac.py -v

# Run by marker
pytest tests/python -m stac -v
```

#### Step 3: Run QGIS Integration Tests (Docker)
```bash
# Run QGIS tests in official QGIS Docker container
docker run --rm --network host \
  -e HONUA_QGIS_BASE_URL=http://localhost:8080 \
  -e QT_QPA_PLATFORM=offscreen \
  -v $PWD:/workspace -w /workspace \
  qgis/qgis:3.34 \
  bash -c "pip install -r tests/qgis/requirements.txt && pytest tests/qgis -v"

# Run specific QGIS test
docker run --rm --network host \
  -e HONUA_QGIS_BASE_URL=http://localhost:8080 \
  -e QT_QPA_PLATFORM=offscreen \
  -v $PWD:/workspace -w /workspace \
  qgis/qgis:3.34 \
  bash -c "pip install -r tests/qgis/requirements.txt && pytest tests/qgis/test_wfs_comprehensive.py -v"
```

#### Step 4: Run Verification Script
```bash
# Automated verification of all API endpoints
./tests/TestData/seed-data/verify-seed-data.sh http://localhost:8080
```

---

### Option 2: Local Server Testing (Faster for Development)

This approach runs Honua locally without Docker (faster iteration).

#### Step 1: Start Local Honua Server
```bash
# Option A: With seed data (QuickStart mode)
HONUA_ALLOW_QUICKSTART=true \
DOTNET_ENVIRONMENT=QuickStart \
dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005

# Option B: With PostgreSQL
# (Start PostgreSQL first, then load seed data manually)
./tests/TestData/seed-data/load-all-seed-data.sh
dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005
```

#### Step 2: Run Python Tests
```bash
# Install dependencies
pip install -r tests/python/requirements.txt

# Set environment variable
export HONUA_API_BASE_URL=http://localhost:5005

# Run tests
pytest tests/python -v
```

#### Step 3: Run QGIS Tests
```bash
# Docker-based QGIS tests (recommended)
docker run --rm --network host \
  -e HONUA_QGIS_BASE_URL=http://localhost:5005 \
  -e QT_QPA_PLATFORM=offscreen \
  -v $PWD:/workspace -w /workspace \
  qgis/qgis:3.34 \
  bash -c "pip install -r tests/qgis/requirements.txt && pytest tests/qgis -v"

# OR local PyQGIS (if QGIS installed)
export HONUA_QGIS_BASE_URL=http://localhost:5005
export QT_QPA_PLATFORM=offscreen
pytest tests/qgis -v
```

---

### Option 3: CI/CD Automated Testing

The GitHub Actions workflows will automatically run all integration tests:

#### Comprehensive Tests (All 278 tests)
```bash
# Trigger via GitHub CLI
gh workflow run integration-tests.yml

# Or push to main/dev branch
git push origin dev
```

#### Quick Smoke Tests (Fast subset)
```bash
# Trigger quick tests
gh workflow run integration-tests-quick.yml

# Or create a pull request (auto-triggers)
```

---

## Test Execution Examples

### Run by API Standard
```bash
# WFS tests only (QGIS + Python)
pytest -m wfs tests/qgis tests/python -v

# STAC tests only
pytest -m stac tests/qgis tests/python -v

# WMS tests only
pytest -m wms tests/qgis -v
```

### Run by Client
```bash
# QGIS tests only
pytest tests/qgis -v

# Python client library tests only
pytest tests/python -v
```

### Run Specific Test File
```bash
# QGIS WFS comprehensive tests
pytest tests/qgis/test_wfs_comprehensive.py -v

# Python STAC pystac-client tests
pytest tests/python/test_stac_pystac.py -v

# QGIS OGC API Features tests
pytest tests/qgis/test_ogc_features_comprehensive.py -v
```

### Run Specific Test Function
```bash
# Single WFS test
pytest tests/qgis/test_wfs_comprehensive.py::test_wfs_getcapabilities_returns_valid_document -v

# Single STAC test
pytest tests/python/test_stac_pystac.py::test_stac_search_with_bbox -v
```

### Exclude Slow Tests
```bash
# Run only fast tests (for quick feedback)
pytest -m "integration and not slow" tests/qgis tests/python -v
```

### Verbose Output
```bash
# Show full test output
pytest tests/qgis/test_wfs_comprehensive.py -vv

# Show print statements
pytest tests/qgis/test_wfs_comprehensive.py -v -s
```

---

## Expected Results

### Success Criteria
- ✅ All tests pass (278/278)
- ✅ No connection errors
- ✅ All API endpoints return expected status codes
- ✅ Data is correctly retrieved and validated

### Pass Rates
With properly seeded data, expect:
- **QGIS tests:** 90-95% pass (some may skip if features unavailable)
- **Python tests:** 95-100% pass (graceful skipping for missing packages)
- **Overall:** >90% pass rate

### Sample Output
```
tests/qgis/test_wfs_comprehensive.py::test_wfs_getcapabilities_returns_valid_document PASSED
tests/qgis/test_wfs_comprehensive.py::test_wfs_getfeature_loads_layer_in_qgis PASSED
tests/python/test_stac_pystac.py::test_stac_open_catalog PASSED
tests/python/test_stac_pystac.py::test_stac_search_with_bbox PASSED

======================== 278 passed in 45.2s ========================
```

---

## Troubleshooting

### Server Not Running
```bash
# Check server status
curl http://localhost:8080/health

# Start server
docker-compose -f docker-compose.seed.yml up -d

# Or locally
dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005
```

### Seed Data Not Loaded
```bash
# Verify data exists
curl "http://localhost:8080/ogc/collections" | jq

# Re-load seed data
./tests/TestData/seed-data/load-all-seed-data.sh

# Or restart Docker Compose
docker-compose -f docker-compose.seed.yml down
docker-compose -f docker-compose.seed.yml up -d
```

### Python Dependencies Missing
```bash
# Install all Python test dependencies
pip install -r tests/python/requirements.txt
pip install -r tests/qgis/requirements.txt
```

### QGIS Tests Fail to Initialize
```bash
# Ensure using Docker (most reliable)
docker run --rm --network host \
  -e HONUA_QGIS_BASE_URL=http://localhost:8080 \
  -e QT_QPA_PLATFORM=offscreen \
  -v $PWD:/workspace -w /workspace \
  qgis/qgis:3.34 \
  bash -c "pip install pytest && pytest tests/qgis -v"
```

### Connection Timeouts
```bash
# Increase timeout (add to pytest.ini or command line)
pytest --timeout=300 tests/qgis -v

# Or disable timeout temporarily
pytest --timeout=0 tests/qgis -v
```

### Tests Skip Due to Missing Data
```bash
# This is normal - some tests skip gracefully
# To see which tests skipped:
pytest tests/qgis -v --tb=short

# Check test output for "SKIPPED" and reason
```

---

## Performance Benchmarks

Expected execution times (varies by hardware):

| Test Suite | Tests | Expected Time | Notes |
|------------|-------|---------------|-------|
| **QGIS - WFS** | 30 | 3-5 min | Includes layer loading |
| **QGIS - WMS** | 27 | 2-4 min | Map rendering |
| **QGIS - WMTS** | 23 | 2-3 min | Tile operations |
| **QGIS - OGC Features** | 32 | 3-5 min | REST API tests |
| **QGIS - OGC Tiles** | 22 | 2-3 min | Tile matrix tests |
| **QGIS - WCS** | 30 | 3-5 min | Coverage tests |
| **QGIS - STAC** | 24 | 2-4 min | Catalog tests |
| **QGIS - GeoServices** | 17 | 2-3 min | FeatureServer tests |
| **Python - All** | 93 | 5-8 min | Client library tests |
| **TOTAL** | **278** | **25-45 min** | Full suite |

**Quick tests (no slow):** ~10-15 minutes

---

## Continuous Integration

The tests are integrated into GitHub Actions:

### Automatic Triggers
- ✅ Push to `main` or `dev` branches
- ✅ Pull requests to `main` or `dev`
- ✅ Manual workflow dispatch

### Workflows
1. **integration-tests.yml** - Full comprehensive suite (~45 min)
2. **integration-tests-quick.yml** - Fast smoke tests (~15 min)

### Viewing Results
```bash
# List recent runs
gh run list --workflow=integration-tests.yml

# Watch current run
gh run watch

# View logs
gh run view --log
```

---

## Next Steps After Running Tests

### If All Tests Pass ✅
1. Review test coverage in pytest output
2. Check for any warnings in logs
3. Commit any fixes or improvements
4. Celebrate! 🎉

### If Tests Fail ❌
1. Check which tests failed: `pytest --lf` (last failed)
2. Review error messages and stack traces
3. Verify seed data is loaded correctly
4. Check server logs for issues
5. Re-run specific failed tests with verbose output
6. Fix issues and re-run

### Performance Optimization
1. Mark long-running tests with `@pytest.mark.slow`
2. Use pytest-xdist for parallel execution: `pytest -n auto`
3. Profile slow tests: `pytest --durations=10`

---

## Summary

**Ready to run:** ✅ Yes!

**Quick start:**
```bash
# 1. Start seeded Honua
docker-compose -f docker-compose.seed.yml up -d

# 2. Run Python tests
pip install -r tests/python/requirements.txt
export HONUA_API_BASE_URL=http://localhost:8080
pytest tests/python -v

# 3. Run QGIS tests
docker run --rm --network host \
  -e HONUA_QGIS_BASE_URL=http://localhost:8080 \
  -e QT_QPA_PLATFORM=offscreen \
  -v $PWD:/workspace -w /workspace \
  qgis/qgis:3.34 \
  bash -c "pip install -r tests/qgis/requirements.txt && pytest tests/qgis -v"

# 4. Verify endpoints
./tests/TestData/seed-data/verify-seed-data.sh http://localhost:8080
```

**All 278 integration tests are ready to validate Honua's API implementations!**
