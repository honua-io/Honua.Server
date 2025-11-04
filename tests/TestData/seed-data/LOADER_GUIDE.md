# Honua Seed Data Loader - Quick Start Guide

## Overview

The `load-all-seed-data.sh` script provides an automated, production-ready solution for loading all seed data into Honua.

## Quick Start

```bash
# Navigate to the seed-data directory
cd tests/TestData/seed-data

# Run the loader
./load-all-seed-data.sh
```

## What It Does

The script performs the following steps automatically:

1. **Health Checks**
   - Verifies Honua CLI is available
   - Checks if Honua server is running (HTTP health check)
   - Validates all seed data files exist

2. **Service Management**
   - Creates "seed-data" service if it doesn't exist
   - Uses In-Memory data store with caching enabled

3. **Data Import**
   - Loads all 9 GeoJSON files in sequence
   - Displays real-time progress (percentage, file count, status)
   - Retries failed imports up to 3 times

4. **Reporting**
   - Shows comprehensive summary statistics
   - Lists success/failure counts
   - Displays total features and data size loaded

## Available Options

### Standard Usage
```bash
./load-all-seed-data.sh
```

### Dry-Run Mode (Preview Without Loading)
```bash
./load-all-seed-data.sh --dry-run
```
Useful to verify what would be loaded without making changes.

### Verbose Mode (Detailed Output)
```bash
./load-all-seed-data.sh --verbose
```
Shows detailed logging for debugging and troubleshooting.

### Custom Retry Count
```bash
./load-all-seed-data.sh --retry-count 5
```
Default is 3 retries. Increase for unreliable networks.

### Custom HTTP Timeout
```bash
./load-all-seed-data.sh --timeout 60
```
Default is 30 seconds. Increase for slow servers.

### Combined Options
```bash
./load-all-seed-data.sh --verbose --dry-run --timeout 60
```

## Loaded Datasets

The script loads the following 9 datasets:

| File | Layer | Geometry | Count | Features |
|------|-------|----------|-------|----------|
| cities.geojson | cities | Point | 50 | Population, elevation, timezone |
| poi.geojson | poi | Point | 200+ | Rating, category, accessibility |
| roads.geojson | roads | LineString | 100 | Type, lanes, speed limit |
| transit_routes.geojson | transit_routes | MultiLineString | 25 | Route type, frequency, fare |
| parcels.geojson | parcels | Polygon | 75 | Zoning, area, assessed value |
| buildings_3d.geojson | buildings_3d | Polygon | 150+ | Height, floors, energy rating |
| parks.geojson | parks | MultiPolygon | 30 | Type, facilities, visitors |
| water_bodies.geojson | water_bodies | Polygon | 35 | Type, depth, salinity |
| administrative_boundaries.geojson | administrative_boundaries | Polygon | 40 | Type, population, GDP |
| weather_stations.geojson | weather_stations | Point | 60 | Temperature, humidity, wind |

## Troubleshooting

### Error: "Honua CLI not found"
Make sure you're running from the project root or that `src/Honua.Cli` exists.

### Error: "Honua server is not responding"
Start the Honua server before running the script:
```bash
dotnet run --project src/Honua.Server.Host
```

### Error: "Missing seed data files"
Verify all .geojson files exist in `tests/TestData/seed-data/`.

### Import Failures After Retries
Run with `--verbose` to see detailed error messages:
```bash
./load-all-seed-data.sh --verbose
```

### Slow Imports
Increase the timeout:
```bash
./load-all-seed-data.sh --timeout 120
```

## Output Example

```
================================================================================
Honua Seed Data Loader
================================================================================
Started at: 2025-02-02 10:30:45

[INFO] Checking Honua CLI...
[SUCCESS] Honua CLI is available

[INFO] Checking Honua server health at: http://localhost:5000
[SUCCESS] Honua server is running and healthy

[INFO] Checking seed data files...
[SUCCESS] Found 9/9 seed data files

[INFO] Checking if service 'seed-data' exists...
[SUCCESS] Service 'seed-data' already exists

================================================================================
Loading Seed Data
================================================================================
[ 11%] [ 1/9] cities.geojson               OK
[ 22%] [ 2/9] poi.geojson                  OK
[ 33%] [ 3/9] roads.geojson                OK
...
[100%] [ 9/9] weather_stations.geojson     OK

================================================================================
Import Summary
================================================================================

Files Statistics:
  Total files to load:     9
  Successfully imported:   9
  Failed imports:          0
  Skipped (not found):     0

Data Statistics:
  Total features:          685
  Total size:              428.4 KiB

Service Information:
  Service name:            seed-data
  Server URL:              http://localhost:5000

[SUCCESS] All seed data loaded successfully!

Completed at: 2025-02-02 10:31:02
```

## Integration with CI/CD

To integrate into CI/CD pipelines:

```bash
#!/bin/bash
set -e

# Start server
dotnet run --project src/Honua.Server.Host &
SERVER_PID=$!

# Wait for server to start
sleep 5

# Load seed data
./tests/TestData/seed-data/load-all-seed-data.sh

# Run tests
dotnet test

# Cleanup
kill $SERVER_PID
```

## Production Considerations

- **Service Name:** Configurable via `SERVICE_NAME` variable
- **Server URL:** Configurable via `HONUA_SERVER_URL` environment variable
- **Data Store:** Uses In-Memory by default (change in script if needed)
- **Caching:** Enabled by default (60-minute expiration)

## Files

- **load-all-seed-data.sh** - Main loader script (this one)
- **cities.geojson** - City data
- **poi.geojson** - Points of interest
- **roads.geojson** - Road network
- **transit_routes.geojson** - Transit routes
- **parcels.geojson** - Land parcels
- **buildings_3d.geojson** - Building footprints
- **parks.geojson** - Parks and protected areas
- **water_bodies.geojson** - Water bodies
- **administrative_boundaries.geojson** - Admin boundaries
- **weather_stations.geojson** - Weather stations
- **README.md** - Detailed seed data documentation

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Run with `--verbose` for detailed diagnostics
3. Review the main README.md for dataset descriptions
4. Check Honua server logs for import errors

## License

Seed data is provided as test data for Honua development and testing.
