# Esri Leaflet Test Suite - Setup Guide

Complete setup instructions to run the Esri Leaflet tests against your Honua.Server instance.

## Prerequisites

- Honua.Server running (or ready to start)
- PostgreSQL with PostGIS extension
- Node.js and npm (for browser tests)

## Step-by-Step Setup

### 1. Setup Test Database

**Option A: Using Docker (Recommended)**

```bash
# Start PostgreSQL with PostGIS
docker run -d \
  --name honua-test-db \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=honua \
  -e POSTGRES_DB=honua_test \
  -p 5432:5432 \
  postgis/postgis:16-3.4

# Wait for database to be ready
sleep 5

# Run setup SQL
docker exec -i honua-test-db psql -U honua -d honua_test < setup-test-database.sql
```

**Option B: Using Existing PostgreSQL**

```bash
# Create database
createdb -U postgres honua_test

# Enable PostGIS and load test data
psql -U postgres -d honua_test < setup-test-database.sql
```

### 2. Configure Honua.Server

**Copy the sample metadata to your Honua configuration:**

```bash
# Copy sample metadata to Honua.Server
cp sample-metadata.json /path/to/honua/metadata/test-services.json
```

**Or update your `appsettings.json`:**

```json
{
  "Honua": {
    "Metadata": {
      "Path": "/path/to/tests/esri-leaflet/sample-metadata.json"
    }
  }
}
```

**Update connection string in metadata:**

Edit `sample-metadata.json` and update the connection string:

```json
{
  "dataSources": [
    {
      "id": "test-datasource",
      "provider": "postgres",
      "connectionString": "Host=localhost;Port=5432;Database=honua_test;Username=honua;Password=honua"
    }
  ]
}
```

### 3. Start Honua.Server

```bash
cd /path/to/Honua.Server
dotnet run --project src/Honua.Server.Host
```

Verify it's running:
```bash
curl http://localhost:5100/health
```

### 4. Install Test Dependencies

```bash
cd tests/esri-leaflet
npm install
```

### 5. Run Tests

**Browser Tests (Recommended):**

```bash
npm run serve
```

Then open: http://localhost:8888/test-runner.html

**Command-Line Tests:**

```bash
npm test
```

## Verify Setup

### Test Database Connection

```bash
psql -U honua -d honua_test -c "SELECT COUNT(*) FROM parks;"
```

Should return: `15` (number of test parks)

### Test Honua Server Endpoints

```bash
# Check FeatureServer metadata
curl http://localhost:5100/rest/services/parks/FeatureServer?f=json

# Query features
curl "http://localhost:5100/rest/services/parks/FeatureServer/0/query?where=1=1&f=json"

# Check MapServer
curl http://localhost:5100/rest/services/basemap/MapServer?f=json

# Check Geometry Service
curl http://localhost:5100/rest/services/Geometry/GeometryServer?f=json
```

## Test Data Overview

### Parks Dataset (15 features)
- Location: Portland, Oregon area
- Geometry: Point (EPSG:4326)
- Types: Recreation, Nature, Sports
- Status: active, maintenance, planned

**Sample query:**
```sql
SELECT name, type, ST_AsText(geom) FROM parks WHERE type = 'Recreation';
```

### Basemap Features Dataset (3 features)
- Polygons representing urban areas
- Used for MapServer tests

## Troubleshooting

### "Cannot connect to server"

**Check if Honua.Server is running:**
```bash
curl http://localhost:5100/health
```

**Check logs:**
```bash
cd /path/to/Honua.Server
dotnet run --project src/Honua.Server.Host --verbose
```

### "Service not found" errors

**Verify metadata is loaded:**
```bash
curl http://localhost:5100/rest/services?f=json
```

Should show `parks`, `basemap`, and `imagery` services.

**Check metadata path in appsettings.json:**
```json
{
  "Honua": {
    "Metadata": {
      "Path": "/correct/path/to/sample-metadata.json"
    }
  }
}
```

### Database connection errors

**Test database connection:**
```bash
psql -U honua -d honua_test -c "SELECT 1;"
```

**Check PostGIS extension:**
```bash
psql -U honua -d honua_test -c "SELECT PostGIS_Version();"
```

### CORS errors in browser

**Enable CORS in Honua.Server `appsettings.json`:**
```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:8888"],
    "AllowedMethods": ["GET", "POST", "OPTIONS"],
    "AllowedHeaders": ["*"]
  }
}
```

### Tests are skipped

Some tests gracefully skip if features aren't supported:
- Geometry Service (if not enabled)
- Image Service (if not configured)
- Authentication (if not required)
- Attachments (if layer doesn't support)

This is expected behavior - tests adapt to your Honua configuration.

## Custom Configuration

### Using Your Own Data

1. **Update metadata:**
   - Change service IDs, titles, and layer definitions
   - Point to your existing tables
   - Update field definitions

2. **Update tests:**
   - Modify service URLs in test files
   - Update field names in queries
   - Adjust expected data types

3. **Example custom service:**

```javascript
// In test files, change:
const FEATURE_SERVICE = '/rest/services/your-service/FeatureServer';
const LAYER_ID = 0; // Your layer index
```

### Running Against Remote Server

```bash
# Set environment variable
export HONUA_TEST_BASE_URL=https://your-server.com

# Or update in browser test-runner.html
```

## Performance Expectations

With 15 test features:
- Feature queries: < 100ms
- Map rendering: < 500ms
- Export operations: < 1 second
- Full test suite: 30-60 seconds

With larger datasets (1000+ features):
- Use spatial indexes (already created in setup SQL)
- Enable result pagination
- Consider caching strategies

## Next Steps

1. ✅ Run tests to validate setup
2. 📊 Review test results in browser
3. 🔧 Customize for your specific use case
4. 📈 Add more test data as needed
5. 🚀 Integrate into CI/CD pipeline

## Additional Resources

- [Honua.Server Documentation](../../docs/)
- [Esri Leaflet API](https://esri.github.io/esri-leaflet/)
- [PostGIS Documentation](https://postgis.net/documentation/)
- [Esri REST API Specification](https://developers.arcgis.com/rest/)

## Support

If you encounter issues:
1. Check this troubleshooting guide
2. Review Honua.Server logs
3. Verify database connectivity
4. Test endpoints with curl
5. Check browser console for errors
