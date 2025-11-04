# Integration Tests Quick Start

## Run Tests Locally

### QGIS Tests (Docker)

```bash
# Start PostgreSQL + PostGIS
docker run -d --name postgres-test \
  -e POSTGRES_DB=honua_test \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=test123 \
  -p 5432:5432 \
  postgis/postgis:16-3.4

# Build and run Honua server
docker build -t honua-server:local .
docker run -d --name honua-server \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=honua_test;Username=honua;Password=test123" \
  -e Honua__DataStore__Provider=PostgreSQL \
  honua-server:local

# Wait for server to be healthy
until curl -f http://localhost:8080/healthz/live; do sleep 3; done

# Run QGIS tests
docker run --rm \
  --network host \
  -v $(pwd)/tests/qgis:/tests \
  -e HONUA_QGIS_BASE_URL=http://localhost:8080 \
  -e QT_QPA_PLATFORM=offscreen \
  -w /tests \
  qgis/qgis:3.34 \
  sh -c "pip install -r requirements.txt && pytest -v -m 'not slow'"

# Cleanup
docker stop honua-server postgres-test
docker rm honua-server postgres-test
```

### Python Tests (Local Python)

```bash
# Install dependencies
cd tests/python
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# Ensure Honua server is running (see above)
export HONUA_API_BASE_URL=http://localhost:8080

# Run tests
pytest -v -m 'not slow'

# Run with slow tests
pytest -v

# Deactivate venv
deactivate
```

## GitHub Actions Workflows

### Trigger Quick Tests (PRs)

Quick tests run automatically on PRs. To run manually:

```bash
gh workflow run integration-tests-quick.yml
```

### Trigger Comprehensive Tests

```bash
# Without slow tests (~30 min)
gh workflow run integration-tests.yml

# With slow tests (~45 min)
gh workflow run integration-tests.yml -f run_slow_tests=true
```

### Monitor Test Runs

```bash
# List recent runs
gh run list --workflow=integration-tests.yml --limit 5

# Watch current run
gh run watch

# View logs
gh run view --log
```

## Test Structure

```
tests/
├── qgis/                           # QGIS integration tests
│   ├── conftest.py                # PyQGIS test fixtures
│   ├── requirements.txt           # pytest, requests, pytest-timeout
│   ├── test_wms_comprehensive.py  # WMS 1.3.0 tests
│   ├── test_wfs_comprehensive.py  # WFS 2.0/3.0 tests
│   ├── test_wcs_comprehensive.py  # WCS 2.0 tests
│   └── ...
└── python/                         # Python integration tests
    ├── conftest.py                # Test fixtures (api_request, etc.)
    ├── requirements.txt           # pytest, requests, owslib, rasterio
    ├── test_stac_smoke.py         # STAC API tests
    ├── test_wfs_owslib.py         # WFS client tests
    └── test_wcs_rasterio.py       # WCS client tests
```

## Writing Tests

### QGIS Test Example

```python
# tests/qgis/test_my_feature.py
import pytest
from qgis.core import QgsVectorLayer

@pytest.mark.requires_qgis
def test_wfs_layer_loads(qgis_app, honua_base_url):
    """Test that WFS layer loads successfully in QGIS."""
    uri = f"url={honua_base_url}/wfs typename=my-layer"
    layer = QgsVectorLayer(uri, "test", "WFS")
    assert layer.isValid()
    assert layer.featureCount() > 0

@pytest.mark.slow
def test_comprehensive_wms_validation(qgis_app, honua_base_url):
    """Comprehensive WMS validation (marked as slow)."""
    # This test only runs when run_slow_tests=true
    pass
```

### Python Test Example

```python
# tests/python/test_my_api.py
import pytest

@pytest.mark.requires_honua
def test_stac_catalog(api_request):
    """Test STAC catalog endpoint."""
    response = api_request("GET", "/stac")
    assert response.status_code == 200
    
    data = response.json()
    assert data["type"] == "Catalog"
    assert "links" in data

@pytest.mark.slow
def test_large_dataset_query(api_request):
    """Test querying large dataset (marked as slow)."""
    # This test only runs when run_slow_tests=true
    pass
```

## Common Issues

### Server Won't Start

```bash
# Check server logs
docker logs honua-server

# Verify database connection
docker exec postgres-test psql -U honua -d honua_test -c "SELECT version();"
```

### Tests Timeout

```python
# Increase timeout for specific test
@pytest.mark.timeout(300)  # 5 minutes
def test_long_operation():
    pass
```

### QGIS Can't Connect

```bash
# Test server connectivity from QGIS container
docker run --rm --network host qgis/qgis:3.34 \
  sh -c "apt-get update && apt-get install -y curl && curl http://localhost:8080/healthz/live"
```

### Missing Python Dependencies

```bash
# Reinstall dependencies
cd tests/python
pip install --upgrade pip
pip install -r requirements.txt --force-reinstall
```

## Performance Tips

1. **Skip slow tests during development**
   ```bash
   pytest -v -m 'not slow'
   ```

2. **Run specific test file**
   ```bash
   pytest -v tests/qgis/test_wms_smoke.py
   ```

3. **Run specific test**
   ```bash
   pytest -v tests/python/test_stac_smoke.py::test_stac_root_exposes_catalog
   ```

4. **Use quick workflow for PRs**
   - Runs automatically on PR
   - ~10-15 minutes vs ~30-45 minutes

5. **Cache dependencies locally**
   ```bash
   # Python venv persists between runs
   cd tests/python
   source .venv/bin/activate
   ```

## Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `HONUA_API_BASE_URL` | Python test base URL | `http://honua-server:8080` |
| `HONUA_QGIS_BASE_URL` | QGIS test base URL | `http://honua-server:8080` |
| `HONUA_QGIS_WMS_LAYER` | Test WMS layer | `test-data:features` |
| `HONUA_QGIS_COLLECTION_ID` | Test collection | `test-data::features` |
| `QT_QPA_PLATFORM` | QGIS headless mode | `offscreen` |

## Artifacts

After workflow runs, download artifacts:

```bash
# List artifacts
gh run view --log

# Download all artifacts
gh run download <run-id>

# Download specific artifact
gh run download <run-id> -n qgis-test-results
```

Artifacts include:
- `qgis-test-results/qgis-junit.xml` - JUnit test results
- `qgis-test-results/qgis-report.html` - HTML test report
- `python-test-results/python-junit.xml` - JUnit test results
- `python-test-results/python-report.html` - HTML test report
- `python-test-results/python-report.json` - JSON test report

## CI/CD Integration

Both workflows integrate with GitHub PR checks:

- ✅ Status checks appear on PRs
- 📊 Test results published as PR comments
- 📁 Artifacts available for download
- 🔍 Detailed logs for debugging

## Related Documentation

- [Full Integration Tests Guide](.github/workflows/README-integration-tests.md)
- [QGIS Test Documentation](../../tests/qgis/README.md)
- [Python Test Documentation](../../tests/python/README.md)
- [OGC Conformance Testing](.github/workflows/ogc-conformance-nightly.yml)

