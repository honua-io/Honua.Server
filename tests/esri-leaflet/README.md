# Honua Server - Esri Leaflet Test Suite

Comprehensive end-to-end tests for Honua.Server's Esri REST API implementation using Esri Leaflet.

## 🎯 Purpose

This test suite validates that Honua.Server correctly implements the Esri GeoServices REST API specification, ensuring compatibility with Esri Leaflet and other Esri-compatible clients.

## 📦 What's Tested

### FeatureServer Tests
- Service and layer metadata retrieval
- Feature layer loading and visualization
- Spatial and attribute queries
- Feature identification
- Error handling
- Performance benchmarks

### MapServer Tests
- Service metadata
- Dynamic map layer rendering
- Layer definitions and filtering
- Map export operations
- Identify operations
- Find operations

### Geometry Service Tests
- Buffer operations
- Coordinate projection
- Spatial relationship testing
- Geometry simplification
- Service availability

### Query Tests
- Attribute queries (SQL WHERE clauses)
- Spatial queries (bbox, point+distance, polygon)
- Statistics and aggregation
- Pagination and ordering
- Multiple output formats (JSON, GeoJSON, CSV, KML)
- Performance testing

### Export Tests
- GeoJSON export
- Esri JSON format
- CSV export
- KML/KMZ export
- Coordinate reference system transformations
- Geometry customization
- Large dataset handling

## 🚀 Quick Start

### Option 1: Browser-Based Testing (Recommended)

1. **Install dependencies:**
   ```bash
   cd tests/esri-leaflet
   npm install
   ```

2. **Start a local server:**
   ```bash
   npm run serve
   ```

3. **Open in browser:**
   - Navigate to `http://localhost:8888/test-runner.html`
   - The tests will run automatically with interactive map visualization

### Option 2: Command-Line Testing

```bash
cd tests/esri-leaflet
npm test
```

## ⚙️ Configuration

### Server URL

The default server URL is `http://localhost:5100`. To change it:

**Browser:**
1. Update the URL in the configuration section of test-runner.html
2. Click "Update URL & Rerun Tests"

**Command-line:**
```bash
export HONUA_TEST_BASE_URL=https://your-server.com
npm test
```

### Test Endpoints

The tests use these default service endpoints:
- FeatureServer: `/rest/services/parks/FeatureServer/0`
- MapServer: `/rest/services/basemap/MapServer/0`
- GeometryServer: `/rest/services/Geometry/GeometryServer`

## 📊 Test Results

### Browser Output
- Interactive map showing tested features
- Real-time test results with pass/fail indicators
- Detailed error messages for failures
- Performance metrics

### Command-Line Output
```
FeatureServer Tests
  Service Metadata
    ✓ should fetch FeatureServer metadata
    ✓ should fetch layer metadata
  Feature Layer Loading
    ✓ should load FeatureLayer using Esri Leaflet
    ✓ should query features with where clause
...

95 passing (12s)
5 pending
```

## 🏗️ Test Structure

```
tests/esri-leaflet/
├── package.json              # Dependencies and scripts
├── test-runner.html          # Browser test harness with map
├── README.md                 # This file
└── tests/
    ├── featureserver.test.js # FeatureServer endpoint tests
    ├── mapserver.test.js     # MapServer endpoint tests
    ├── geometry.test.js      # Geometry service tests
    ├── query.test.js         # Query capability tests
    └── export.test.js        # Export format tests
```

## 🔧 Writing Custom Tests

### Add a New Test

```javascript
describe('My Custom Tests', function() {
    this.timeout(10000);

    it('should do something specific', async function() {
        const response = await fetch(`${BASE_URL}/rest/services/myservice/FeatureServer/0?f=json`);
        expect(response.ok).to.be.true;

        const data = await response.json();
        expect(data).to.have.property('name');
    });
});
```

### Use Esri Leaflet

```javascript
it('should load my custom layer', function(done) {
    const layer = L.esri.featureLayer({
        url: `${BASE_URL}/rest/services/myservice/FeatureServer/0`
    });

    layer.once('load', function() {
        expect(layer).to.exist;
        done();
    });

    layer.addTo(testMap);
});
```

## 📈 Performance Benchmarks

The suite includes performance tests that verify:
- Feature queries complete within 3 seconds
- Large exports (1000 features) complete within 10 seconds
- Export operations complete within 5 seconds
- Map rendering completes within reasonable time

## 🐛 Troubleshooting

### Tests Failing with "Cannot connect to server"

**Solution:** Ensure Honua.Server is running:
```bash
cd /path/to/Honua.Server
dotnet run --project src/Honua.Server.Host
```

### "No features returned" warnings

**Solution:** This is normal if the test database is empty. The tests are designed to handle empty results gracefully.

### CORS errors in browser

**Solution:** Ensure Honua.Server has CORS enabled for `http://localhost:8888`:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:8888"]
  }
}
```

### Map not displaying

**Solution:**
1. Check browser console for errors
2. Verify internet connection (OpenStreetMap tiles need to load)
3. Clear browser cache and reload

## 🔗 Dependencies

- **Esri Leaflet** (3.0.12): Official Esri plugin for Leaflet
- **Leaflet** (1.9.4): Interactive map library
- **Mocha** (10.2.0): Test framework
- **Chai** (4.3.10): Assertion library
- **Axios** (1.6.2): HTTP client

## 📚 Resources

- [Esri Leaflet Documentation](https://esri.github.io/esri-leaflet/)
- [Esri GeoServices REST API Specification](https://developers.arcgis.com/rest/)
- [Leaflet Documentation](https://leafletjs.com/)
- [Mocha Documentation](https://mochajs.org/)

## 🤝 Contributing

To add new test cases:

1. Create a new test file in `tests/` directory
2. Follow the existing test patterns
3. Include the script in `test-runner.html`
4. Update this README with test coverage

## 📄 License

MIT License - See LICENSE file in project root
