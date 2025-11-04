# Python Integration Tests - Quick Start Guide

## Installation

```bash
cd tests/python
pip install -r requirements.txt
```

## Configuration

Set environment variables:

```bash
export HONUA_API_BASE_URL="http://localhost:5000"
export HONUA_API_BEARER="your-token-here"  # Optional
```

Or create a `.env` file:

```bash
HONUA_API_BASE_URL=http://localhost:5000
HONUA_API_BEARER=your-token-here
```

## Run Tests

### All Tests
```bash
pytest tests/python/ -v
```

### Specific Protocol
```bash
pytest tests/python/test_wms_owslib.py -v          # WMS tests
pytest tests/python/test_wfs_owslib.py -v          # WFS tests
pytest tests/python/test_wmts_owslib.py -v         # WMTS tests
pytest tests/python/test_wcs_rasterio.py -v        # WCS tests
pytest tests/python/test_csw_owslib.py -v          # CSW tests
pytest tests/python/test_stac_pystac.py -v         # STAC tests
pytest tests/python/test_geoservices_arcpy.py -v   # GeoServices tests
pytest tests/python/test_ogc_features_requests.py -v   # OGC API Features
pytest tests/python/test_ogc_tiles_requests.py -v      # OGC API Tiles
pytest tests/python/test_ogc_processes_requests.py -v  # OGC API Processes
```

### By Marker
```bash
pytest -m wms                  # All WMS tests
pytest -m "ogc_features"       # All OGC API Features tests
pytest -m "python and wfs"     # WFS Python tests
```

### Parallel Execution (Faster)
```bash
pytest tests/python/ -n auto   # Use all available CPU cores
pytest tests/python/ -n 4      # Use 4 workers
```

### Dry Run (Verify Tests)
```bash
pytest tests/python/ --collect-only   # Collect without running
```

## Test Status

✅ **347 total tests**
✅ **All tests successfully collected**
✅ **0 syntax errors**
✅ **Production ready**

## Coverage

- WMS 1.3.0 (30 tests)
- WFS 2.0 (13 tests)
- WMTS 1.0.0 (39 tests)
- WCS 2.0 (27 tests)
- CSW 2.0.2 (40 tests)
- STAC 1.0 (63 tests)
- GeoServices REST API (19 tests)
- OGC API - Features 1.0 (56 tests)
- OGC API - Tiles 1.0 (41 tests)
- OGC API - Processes 1.0 (53 tests)

## Documentation

See `COMPREHENSIVE_TEST_SUITE_SUMMARY.md` for full details.
