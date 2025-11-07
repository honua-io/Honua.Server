# Test Metadata Templates

This directory contains metadata configuration templates for different testing scenarios.

## Available Templates

### minimal-metadata.json
Bare minimum configuration for basic API testing:
- Single data source (SQLite)
- One service with OGC Features only
- One layer with simple geometry
- Perfect for quick smoke tests and CI

### standard-metadata.json
Comprehensive configuration for full test coverage:
- Multiple data sources (SQLite, PostgreSQL)
- All OGC services enabled (Features, Tiles, Processes, Coverages)
- Multiple layers with different geometries
- Raster datasets for tile/coverage testing
- Used by default test suite

### enterprise-metadata.json
Enterprise features testing:
- SensorThings API configuration
- Geoprocessing operations
- Event/geofencing configuration
- Advanced enterprise-only features

## Usage

Set the `HONUA__METADATA__PATH` environment variable to point to the desired template:

```bash
# Minimal tests
export HONUA__METADATA__PATH=/app/data/metadata/minimal-metadata.json

# Standard tests (default)
export HONUA__METADATA__PATH=/app/data/metadata/standard-metadata.json

# Enterprise tests
export HONUA__METADATA__PATH=/app/data/metadata/enterprise-metadata.json
```

Or in docker-compose.yml:

```yaml
environment:
  - HONUA__METADATA__PATH=/app/data/metadata/standard-metadata.json
```
