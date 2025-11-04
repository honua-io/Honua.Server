# Integration Test Workflows

This directory contains GitHub Actions workflows for running comprehensive integration tests against the Honua server.

## Workflows

### 1. `integration-tests.yml` - Comprehensive Integration Tests

**Purpose**: Full integration testing with QGIS and Python clients

**When it runs**:
- Push to `main`, `master`, or `dev` branches
- Pull requests to these branches
- Manual trigger via workflow_dispatch

**What it tests**:
- QGIS integration (WMS, WFS, WCS, WMTS, OGC API Features/Tiles)
- Python integration (STAC, WFS with OWSLib, WCS with rasterio)
- Real-world client compatibility
- OGC specification compliance

**Duration**: ~30-45 minutes

**Features**:
- Builds Honua server Docker image
- Spins up PostgreSQL with PostGIS
- Runs server in container with health checks
- Executes QGIS tests in `qgis/qgis:3.34` container
- Executes Python tests with Python 3.12
- Uploads test results and coverage reports
- Generates PR comments with test summary

**Manual trigger options**:
```bash
# Trigger via GitHub CLI
gh workflow run integration-tests.yml

# Include slow tests (comprehensive validation)
gh workflow run integration-tests.yml -f run_slow_tests=true
```

### 2. `integration-tests-quick.yml` - Quick Smoke Tests

**Purpose**: Fast subset of tests for quick PR feedback

**When it runs**:
- Pull requests to `main`, `master`, or `dev`
- Manual trigger via workflow_dispatch

**What it tests**:
- Critical path smoke tests only
- Excludes `@pytest.mark.slow` tests
- Basic QGIS functionality
- Basic Python client functionality

**Duration**: ~10-15 minutes

**Features**:
- Same infrastructure as full tests
- Excludes comprehensive/slow tests
- Faster feedback loop
- Lightweight PR checks

**Manual trigger**:
```bash
gh workflow run integration-tests-quick.yml
```

### 3. `integration-tests-storage.yml` - Cloud Storage Emulator Tests

**Purpose**: Tests cloud storage integration (S3, Azure Blob, GCS)

**Note**: This is the previous `integration-tests.yml` file, renamed to avoid conflicts.

## Test Structure

### QGIS Tests (`tests/qgis/`)

Tests run inside the official QGIS Docker container (`qgis/qgis:3.34`):

```
tests/qgis/
├── conftest.py                          # PyQGIS fixtures
├── requirements.txt                     # Python dependencies
├── test_wms_comprehensive.py           # WMS 1.3.0 tests
├── test_wfs_comprehensive.py           # WFS 2.0/3.0 tests
├── test_wcs_comprehensive.py           # WCS 2.0 tests
├── test_wmts_comprehensive.py          # WMTS tests
├── test_ogc_features_comprehensive.py  # OGC API Features
└── test_ogc_tiles_comprehensive.py     # OGC API Tiles
```

**Marking slow tests**:
```python
@pytest.mark.slow
def test_comprehensive_validation():
    # This test will only run when run_slow_tests=true
    pass
```

### Python Tests (`tests/python/`)

Tests run with Python 3.12 and standard geospatial libraries:

```
tests/python/
├── conftest.py              # Test fixtures
├── requirements.txt         # Dependencies (pytest, requests, owslib, rasterio, pystac)
├── test_stac_smoke.py      # STAC API tests
├── test_wfs_owslib.py      # WFS tests with OWSLib
└── test_wcs_rasterio.py    # WCS tests with rasterio
```

**Marking slow tests**:
```python
@pytest.mark.slow
def test_large_dataset_download():
    # This test will only run when run_slow_tests=true
    pass
```

## Environment Variables

Both workflows use these environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `HONUA_API_BASE_URL` | Base URL for Python tests | `http://honua-server:8080` |
| `HONUA_QGIS_BASE_URL` | Base URL for QGIS tests | `http://honua-server:8080` |
| `HONUA_QGIS_WMS_LAYER` | Test WMS layer name | `test-data:features` |
| `HONUA_QGIS_COLLECTION_ID` | Test collection ID | `test-data::features` |
| `POSTGRES_DB` | PostgreSQL database name | `honua_integration_test` |
| `POSTGRES_USER` | PostgreSQL username | `honua_test` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `test_password_123` |

## Test Results

### Artifacts

Both workflows upload test results as artifacts:

- **QGIS test results**: JUnit XML, HTML report
- **Python test results**: JUnit XML, HTML report, JSON report
- **Coverage reports**: OpenCover format (if enabled)
- **Server logs**: Available on test failure

**Retention**: 
- Full tests: 7 days
- Quick tests: 3 days

### PR Comments

Workflows automatically comment on pull requests with:
- Test execution status
- Pass/fail summary
- Links to detailed results
- Next steps for developers

Example PR comment:
```markdown
# Integration Test Results Summary

## Test Execution

| Test Suite | Status |
|------------|--------|
| QGIS Integration Tests | success |
| Python Integration Tests | success |

## Artifacts
- QGIS test results and HTML report
- Python test results and HTML report
- Server logs (if tests failed)
```

## Docker Images

### Honua Server

Built from `Dockerfile` in the project root:
- Base: `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled`
- Health check: `wget http://localhost:8080/healthz/live`
- Exposes port 8080

### PostgreSQL with PostGIS

- Image: `postgis/postgis:16-3.4`
- PostgreSQL 16
- PostGIS 3.4
- Health check: `pg_isready`

### QGIS

- Image: `qgis/qgis:3.34`
- QGIS 3.34 LTR
- PyQGIS libraries
- Headless mode (`QT_QPA_PLATFORM=offscreen`)

## Adding New Tests

### Add a QGIS test:

1. Create test file in `tests/qgis/test_*.py`
2. Import QGIS libraries:
   ```python
   from qgis.core import QgsVectorLayer, QgsProject
   ```
3. Use fixtures from `conftest.py`:
   ```python
   def test_my_feature(qgis_app, honua_base_url):
       pass
   ```
4. Mark slow tests:
   ```python
   @pytest.mark.slow
   def test_comprehensive():
       pass
   ```

### Add a Python test:

1. Create test file in `tests/python/test_*.py`
2. Use fixtures from `conftest.py`:
   ```python
   def test_my_api(api_request, honua_api_base_url):
       response = api_request("GET", "/stac")
       assert response.status_code == 200
   ```
3. Mark slow tests:
   ```python
   @pytest.mark.slow
   def test_large_download():
       pass
   ```

## Troubleshooting

### Server fails to start

Check server logs in the workflow output:
```yaml
- name: Show server logs on failure
  if: failure()
  run: docker logs honua-server
```

### Tests timeout

Increase timeout in the test:
```python
@pytest.mark.timeout(300)  # 5 minutes
def test_long_running():
    pass
```

### QGIS tests fail to load layers

Verify the layer/collection exists:
```python
def test_layer_exists(honua_base_url):
    response = requests.get(f"{honua_base_url}/wms?request=GetCapabilities")
    assert "test-data" in response.text
```

### Python tests can't connect to server

Check network connectivity:
```bash
# In workflow, verify server is responding
curl -f http://localhost:8080/healthz/live
```

## Performance

### Workflow Timing Breakdown

**Comprehensive Tests** (~45 min total):
- Build server: ~10 min
- QGIS tests: ~20 min
- Python tests: ~10 min
- Results upload: ~5 min

**Quick Tests** (~15 min total):
- Build server: ~10 min
- QGIS smoke: ~3 min
- Python smoke: ~2 min

### Optimization Tips

1. **Cache Docker layers**: Both workflows use GitHub Actions cache
2. **Run tests in parallel**: QGIS and Python jobs run concurrently
3. **Skip slow tests**: Use quick workflow for PRs
4. **Optimize test data**: Use minimal datasets for smoke tests

## Best Practices

1. **Mark slow tests**: Always use `@pytest.mark.slow` for tests >30s
2. **Use fixtures**: Reuse setup code via pytest fixtures
3. **Clean up resources**: Tests should clean up temporary data
4. **Test independence**: Each test should be runnable in isolation
5. **Clear assertions**: Use descriptive assertion messages
6. **Document tests**: Add docstrings explaining what's being tested

## Related Workflows

- `ci.yml`: .NET unit tests and code quality
- `docker-tests.yml`: Docker build validation
- `ogc-conformance-nightly.yml`: OGC CITE compliance tests
- `nightly-tests.yml`: Extended test suite

## Support

For issues with these workflows:
1. Check workflow run logs in GitHub Actions
2. Review test output in artifacts
3. Check server logs (uploaded on failure)
4. Consult QGIS/Python test documentation in test files
