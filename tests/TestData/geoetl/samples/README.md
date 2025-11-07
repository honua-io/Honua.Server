# GeoETL Test Data

This directory contains sample geospatial data files used for GeoETL integration tests.

## Sample Files

### GeoJSON Files

- **points_10.geojson** - 10 point features
- **points_100.geojson** - 100 point features
- **points_1000.geojson** - 1000 point features
- **polygons_10.geojson** - 10 polygon features
- **linestrings_10.geojson** - 10 linestring features

### GeoPackage Files

- **sample.gpkg** - Multi-layer GeoPackage with points, lines, and polygons
  - Layer: `points` - 50 point features
  - Layer: `lines` - 20 linestring features
  - Layer: `polygons` - 15 polygon features

### Shapefile

- **parcels.shp** (+ .shx, .dbf, .prj) - Sample parcel polygons
- **roads.shp** (+ .shx, .dbf, .prj) - Sample road linestrings

### CSV with Geometry

- **points_wkt.csv** - Points with WKT geometry column
- **points_latlon.csv** - Points with latitude/longitude columns
- **polygons_wkb.csv** - Polygons with WKB (hex) geometry column

### GPX Files

- **waypoints.gpx** - Sample GPS waypoints
- **track.gpx** - Sample GPS track
- **route.gpx** - Sample GPS route

### GML Files

- **features.gml** - GML 3.2 format features

## Generating Test Data

Test data is generated programmatically using the `FeatureGenerator` utility class during test execution. The static files in this directory serve as reference samples for format validation.

## Coordinate System

All test data uses WGS84 (EPSG:4326) coordinate reference system unless otherwise noted.

## Data Attribution

Sample test data is synthetically generated for testing purposes and does not represent real-world features.
