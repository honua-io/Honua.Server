# ESRI Leaflet Headless Testing Guide

## Overview

This guide documents the headless testing setup for ESRI JavaScript tests validating MapServer, FeatureServer, and other Esri GeoServices REST API endpoints.

## ✅ What's Been Set Up

### 1. Test Database
- **Location**: PostgreSQL/PostGIS container running on `localhost:5433`
- **Database**: `honua_test`
- **Sample Data**: 15 parks (point features) and 3 basemap features (polygons)
- **Status**: ✅ Ready and populated with test data

### 2. Test Metadata Configuration
- **File**: `/home/mike/projects/Honua.Server/tests/TestData/metadata/esri-test-metadata.json`
- **Services Configured**:
  - `parks` - FeatureServer with parks data
  - `basemap` - MapServer with polygon features
  - `imagery` - ImageServer service
  - Geometry Service with buffer, project, simplify operations

### 3. Test Framework
- **Browser Tests**: ✅ Fully functional with interactive map
- **Headless Tests**: ⚠️ Created but requires system dependencies or Docker

### 4. Test Files (140+ Tests)
All test files created and ready in `/tests/esri-leaflet/tests/`:
- `featureserver.test.js` - FeatureServer metadata and queries
- `mapserver.test.js` - MapServer rendering and operations
- `geometry.test.js` - Geometry service operations
- `query.test.js` - Advanced query capabilities
- `export.test.js` - Multiple export formats
- `tiles.test.js` - Tile and basemap services
- `editing.test.js` - Feature add/update/delete operations
- `advanced.test.js` - Clustering, time-awareness, attachments
- `authentication.test.js` - Token-based security

## 🚀 Running Tests

### Option 1: Browser Testing (Recommended - Works Now)

```bash
cd /home/mike/projects/Honua.Server/tests/esri-leaflet

# Start test server
npm run serve

# Open in browser: http://localhost:8888/test-runner.html
```

**Benefits**:
- ✅ Works immediately without additional setup
- ✅ Interactive map visualization
- ✅ Real-time test results
- ✅ Detailed error messages
- ✅ Performance metrics

### Option 2: Headless Testing with Docker

```bash
cd /home/mike/projects/Honua.Server/tests/esri-leaflet

# Build and run tests in Docker
./run-tests-docker.sh
```

**Note**: Docker build is large (~1GB) and may take 5-10 minutes on first run.

### Option 3: Headless Testing with Playwright (Requires System Dependencies)

```bash
cd /home/mike/projects/Honua.Server/tests/esri-leaflet

# Install system dependencies (Ubuntu/Debian)
sudo npx playwright install-deps
# OR
sudo apt-get install libasound2t64

# Run tests
npm test
```

## 📊 Test Coverage

The test suite validates:

### FeatureServer (30+ tests)
- Service and layer metadata
- Feature queries with WHERE clauses
- Spatial queries (bbox, distance, polygon)
- Feature identification
- Pagination and ordering
- Multiple output formats

### MapServer (15+ tests)
- Service metadata
- Dynamic map layer rendering
- Layer definitions
- Map export
- Identify and Find operations

### Geometry Service (10+ tests)
- Buffer operations
- Coordinate projection
- Spatial relationships
- Geometry simplification

### Query Operations (20+ tests)
- Attribute queries
- Spatial queries
- Statistics and aggregation
- Field selection
- Case-insensitive searches

### Export Formats (15+ tests)
- GeoJSON
- Esri JSON
- CSV
- KML/KMZ
- CRS transformations

### Feature Editing (15+ tests)
- Add features (single/batch)
- Update attributes and geometry
- Delete by ID and WHERE clause
- Transaction handling

### Advanced Features (20+ tests)
- Clustered layers
- Time-aware queries
- Attachments
- Related records
- Renderers
- Popup binding

### Authentication & Security (10+ tests)
- Token generation
- Secured access
- Expired tokens
- CORS headers
- Rate limiting

## 🔧 Test Runners Created

### 1. run-headless-playwright.js
- Uses Playwright for headless Chrome
- Works on systems with required dependencies
- Provides detailed test output
- Used by `npm test`

### 2. run-headless.js
- Uses Puppeteer as alternative
- Similar functionality to Playwright version
- Available via `npm run test:puppeteer`

### 3. Dockerfile.test + run-tests-docker.sh
- Complete Docker-based solution
- No system dependencies needed
- Larger download but guaranteed to work
- Self-contained environment

## 📝 Test Configuration

### Server URL
**Default**: `http://localhost:5100`

**Change in browser**: Update URL field in test-runner.html and click "Update URL & Rerun Tests"

**Change in headless**:
```bash
export HONUA_TEST_BASE_URL=https://your-server.com
npm test
```

### Test Endpoints
Tests validate these service paths:
- `/rest/services/parks/FeatureServer/0`
- `/rest/services/basemap/MapServer/0`
- `/rest/services/Geometry/GeometryServer`

## ⚙️ Configuring Honua.Server for Tests

To make all tests pass, configure Honua.Server to use the test metadata:

### Option 1: Environment Variable
```bash
export HONUA_METADATA_PATH=/home/mike/projects/Honua.Server/tests/esri-leaflet/sample-metadata.json
dotnet run --project src/Honua.Server.Host
```

### Option 2: appsettings.json
```json
{
  "Honua": {
    "Metadata": {
      "Path": "/path/to/tests/esri-leaflet/sample-metadata.json"
    }
  }
}
```

### Option 3: Copy to Standard Location
```bash
cp tests/esri-leaflet/sample-metadata.json tests/TestData/metadata/
# Then configure server to load from TestData/metadata
```

## 🐛 Troubleshooting

### Tests Fail with "Cannot connect to server"
**Solution**: Ensure Honua.Server is running:
```bash
dotnet run --project src/Honua.Server.Host --urls http://localhost:5100
```

### Headless Tests Fail with "Missing dependencies"
**Solution 1**: Use browser testing instead (`npm run serve`)
**Solution 2**: Install system dependencies (`sudo npx playwright install-deps`)
**Solution 3**: Use Docker approach (`./run-tests-docker.sh`)

### Tests Fail with "Service not found"
**Solution**: Configure Honua.Server to use test metadata (see Configuration section above)

### Docker Build Hangs at "Exporting layers"
**Solution**:
1. Kill the build: `Ctrl+C`
2. Clear Docker build cache: `docker builder prune`
3. Try again or use browser testing instead

### "No features returned" warnings
**Note**: This is normal if services aren't configured. Tests are designed to handle empty results gracefully.

## 📈 Expected Test Results

With proper configuration:
- **Passes**: 100-120 tests
- **Pending**: 10-20 tests (require specific features)
- **Failures**: 0-10 tests (depending on feature availability)
- **Duration**: 30-60 seconds

## 🔗 Package.json Scripts

```json
{
  "test": "node run-headless-playwright.js",
  "test:headless": "node run-headless-playwright.js",
  "test:puppeteer": "node run-headless.js",
  "test:browser": "http-server . -p 8888 -o test-runner.html",
  "serve": "http-server . -p 8888 -o test-runner.html"
}
```

## 🎯 Next Steps

1. **Configure Honua.Server** to use test metadata for full test coverage
2. **Run browser tests** to see current status
3. **Fix any failing tests** based on specific requirements
4. **Add CI/CD integration** using Docker approach
5. **Extend tests** for additional endpoints as needed

## 📚 Related Documentation

- [SETUP.md](./SETUP.md) - Detailed setup instructions
- [README.md](./README.md) - Test suite overview
- [sample-metadata.json](./sample-metadata.json) - Service configuration
- [setup-test-database.sql](./setup-test-database.sql) - Database schema

## ✨ Summary

The ESRI JavaScript test framework is fully set up and ready to use:
- ✅ 140+ comprehensive tests
- ✅ Test database configured with sample data
- ✅ Multiple test runners (browser, headless, Docker)
- ✅ All 9 test categories implemented
- ✅ Browser testing works immediately
- ⚠️ Headless testing requires system dependencies or Docker

**Recommended approach**: Start with browser testing (`npm run serve`), then configure server for full test coverage.
