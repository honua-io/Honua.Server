# Honua Seed Data - Reusable Test Instance

**Purpose:** Comprehensive, diverse seed data for testing all Honua API endpoints with real-world scenarios.

## Overview

This seed data provides a complete, realistic geospatial dataset covering:
- Multiple geometry types (Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon)
- Diverse attribute types (string, integer, float, boolean, date, datetime, null)
- Spatial reference systems (EPSG:4326, EPSG:3857)
- Temporal data (timestamps, date ranges)
- Nested properties and complex attributes
- Edge cases and validation scenarios

## Datasets

### 1. **cities** (Point geometries)
- **Geometry:** Point
- **Count:** 50 cities worldwide
- **Attributes:**
  - `name` (string) - City name
  - `country` (string) - Country name
  - `population` (integer) - Population count
  - `capital` (boolean) - Is capital city
  - `elevation` (float) - Elevation in meters
  - `founded` (date) - Date city was founded
  - `timezone` (string) - IANA timezone
  - `metadata` (object) - Nested properties
- **Use Cases:** Point queries, attribute filtering, sorting, paging

### 2. **roads** (LineString geometries)
- **Geometry:** LineString
- **Count:** 100 road segments
- **Attributes:**
  - `name` (string) - Road name
  - `type` (string) - Road type (highway, street, avenue, boulevard)
  - `lanes` (integer) - Number of lanes
  - `speed_limit` (integer) - Speed limit in km/h
  - `surface` (string) - Surface type (asphalt, concrete, gravel)
  - `oneway` (boolean) - One-way street
  - `length_km` (float) - Length in kilometers
  - `condition` (string) - Road condition (excellent, good, fair, poor)
- **Use Cases:** Line queries, spatial filters, CRS transformation

### 3. **parcels** (Polygon geometries)
- **Geometry:** Polygon
- **Count:** 75 land parcels
- **Attributes:**
  - `parcel_id` (string) - Unique parcel identifier
  - `owner` (string) - Property owner name
  - `zoning` (string) - Zoning classification (residential, commercial, industrial, agricultural)
  - `area_sqm` (float) - Area in square meters
  - `assessed_value` (integer) - Property value in USD
  - `year_built` (integer) - Year building was constructed
  - `bedrooms` (integer, nullable) - Number of bedrooms (null for non-residential)
  - `bathrooms` (float, nullable) - Number of bathrooms
  - `last_sale_date` (datetime) - Last sale timestamp
- **Use Cases:** Polygon queries, complex filters, value ranges

### 4. **parks** (MultiPolygon geometries)
- **Geometry:** MultiPolygon (some parks have disconnected areas)
- **Count:** 30 parks and protected areas
- **Attributes:**
  - `name` (string) - Park name
  - `type` (string) - Park type (national, state, city, nature_reserve)
  - `area_hectares` (float) - Total area in hectares
  - `established` (date) - Date park was established
  - `facilities` (array) - Available facilities
  - `visitor_count_annual` (integer) - Annual visitors
  - `protected` (boolean) - Protected area status
- **Use Cases:** MultiPolygon handling, array attributes, spatial joins

### 5. **poi** (Point of Interest - Mixed)
- **Geometry:** Point
- **Count:** 200 diverse points of interest
- **Attributes:**
  - `name` (string) - POI name
  - `category` (string) - Category (restaurant, hotel, museum, school, hospital, etc.)
  - `subcategory` (string) - Subcategory
  - `rating` (float) - Rating 0-5
  - `reviews` (integer) - Number of reviews
  - `price_level` (integer) - Price level 1-4
  - `open_hours` (string) - Operating hours
  - `phone` (string) - Contact phone
  - `website` (string, nullable) - Website URL
  - `wheelchair_accessible` (boolean) - Accessibility
- **Use Cases:** Large result sets, paging, sorting, category filtering

### 6. **transit_routes** (MultiLineString geometries)
- **Geometry:** MultiLineString (routes with multiple segments)
- **Count:** 25 transit routes
- **Attributes:**
  - `route_id` (string) - Route identifier
  - `name` (string) - Route name
  - `type` (string) - Transit type (bus, rail, tram, ferry)
  - `operator` (string) - Operating company
  - `frequency_minutes` (integer) - Service frequency
  - `fare` (float) - Base fare in USD
  - `length_km` (float) - Total route length
  - `stops` (integer) - Number of stops
- **Use Cases:** MultiLineString handling, network analysis

### 7. **administrative_boundaries** (Polygon/MultiPolygon)
- **Geometry:** Polygon, MultiPolygon
- **Count:** 40 administrative regions
- **Attributes:**
  - `name` (string) - Region name
  - `type` (string) - Boundary type (country, state, county, city)
  - `code` (string) - ISO or FIPS code
  - `population` (integer) - Population
  - `area_sqkm` (float) - Area in square kilometers
  - `gdp_usd` (integer, nullable) - GDP in USD
  - `capital` (string) - Capital city
- **Use Cases:** Hierarchical queries, large polygons, spatial relationships

### 8. **weather_stations** (Point with temporal data)
- **Geometry:** Point
- **Count:** 60 weather stations
- **Attributes:**
  - `station_id` (string) - Station identifier
  - `name` (string) - Station name
  - `elevation` (float) - Elevation in meters
  - `temperature_c` (float) - Current temperature
  - `humidity_percent` (integer) - Relative humidity
  - `wind_speed_kmh` (float) - Wind speed
  - `wind_direction` (integer) - Wind direction in degrees
  - `last_reading` (datetime) - Last measurement timestamp
  - `operational` (boolean) - Station status
- **Use Cases:** Temporal queries, time-series data, datetime filtering

### 9. **buildings_3d** (Polygon with 3D attributes)
- **Geometry:** Polygon (building footprints)
- **Count:** 150 buildings
- **Attributes:**
  - `building_id` (string) - Unique identifier
  - `name` (string, nullable) - Building name
  - `type` (string) - Building type (residential, commercial, industrial, public)
  - `floors` (integer) - Number of floors
  - `height_m` (float) - Height in meters
  - `construction_year` (integer) - Year built
  - `renovation_year` (integer, nullable) - Last renovation
  - `energy_rating` (string) - Energy efficiency rating (A-G)
  - `occupancy` (integer) - Current occupancy
- **Use Cases:** 3D data, height queries, building analysis

### 10. **water_bodies** (Polygon/MultiPolygon)
- **Geometry:** Polygon, MultiPolygon
- **Count:** 35 lakes, rivers, reservoirs
- **Attributes:**
  - `name` (string) - Water body name
  - `type` (string) - Type (lake, river, reservoir, ocean, sea)
  - `area_sqkm` (float, nullable) - Surface area
  - `max_depth_m` (float, nullable) - Maximum depth
  - `avg_depth_m` (float, nullable) - Average depth
  - `volume_km3` (float, nullable) - Volume
  - `salinity` (string) - Salinity (freshwater, brackish, saltwater)
  - `protected` (boolean) - Protected status
- **Use Cases:** Water resource management, environmental data

## Spatial Coverage

Data covers multiple regions to test global scenarios:
- **North America:** US, Canada, Mexico
- **Europe:** UK, France, Germany, Spain, Italy
- **Asia:** Japan, China, India, Singapore
- **South America:** Brazil, Argentina, Chile
- **Africa:** South Africa, Egypt, Kenya
- **Oceania:** Australia, New Zealand

## Coordinate Reference Systems

All seed data is provided in:
- **EPSG:4326 (WGS84)** - Primary dataset (lat/lon)
- **EPSG:3857 (Web Mercator)** - For tile/web map testing

## Data Characteristics

### Attribute Diversity
- **Strings:** Various lengths, special characters, Unicode
- **Integers:** Positive, negative, zero, large values
- **Floats:** Decimals, scientific notation, null
- **Booleans:** true, false, null
- **Dates:** ISO 8601 format, various ranges
- **Datetimes:** ISO 8601 with timezone, UTC
- **Arrays:** String arrays, number arrays
- **Objects:** Nested JSON structures
- **Nulls:** Testing null handling

### Geometry Complexity
- **Simple:** Basic shapes, few vertices
- **Complex:** Many vertices, holes, multi-parts
- **Edge Cases:** Self-intersecting (invalid), empty geometries, large polygons

### Query Test Scenarios
- **Pagination:** Datasets sized for testing paging (50-200 features)
- **Sorting:** Numeric and string fields for ordering
- **Filtering:** Diverse values for comparison operators
- **Spatial Queries:** Overlapping features, contained features, intersections
- **Temporal Queries:** Historical data, current data, future dates

## File Formats

Each dataset is available in multiple formats:
- **GeoJSON** (`.geojson`) - For OGC API Features, STAC
- **GeoPackage** (`.gpkg`) - For all OGC services
- **Shapefile** (`.zip`) - For legacy compatibility
- **CSV with WKT** (`.csv`) - For simple import
- **GeoParquet** (`.parquet`) - For high-performance testing

## Loading Seed Data

### Option 1: Docker Compose (Recommended)
```bash
docker-compose -f docker-compose.seed.yml up -d
```

This starts:
- PostgreSQL 16 + PostGIS 3.4
- Honua Server
- Automatically loads all seed data

### Option 2: Honua CLI - Automated Loader Script (Recommended)
```bash
# Load all datasets at once with automatic health checks
./tests/TestData/seed-data/load-all-seed-data.sh

# With options:
./tests/TestData/seed-data/load-all-seed-data.sh --verbose
./tests/TestData/seed-data/load-all-seed-data.sh --dry-run
./tests/TestData/seed-data/load-all-seed-data.sh --retry-count 5
./tests/TestData/seed-data/load-all-seed-data.sh --timeout 60
```

#### load-all-seed-data.sh Features
- Automatic health checks (Honua CLI, server connectivity, file validation)
- Automatic service creation if needed
- Progress tracking with percentage and file-by-file status
- Comprehensive summary statistics
- Error handling with retry logic (default 3 attempts)
- Color-coded output for easy reading
- Verbose mode for troubleshooting
- Dry-run mode to preview operations
- Timeout configuration for slow networks

#### load-all-seed-data.sh Usage
```bash
# Standard load
./tests/TestData/seed-data/load-all-seed-data.sh

# Preview what would be loaded without actually loading
./tests/TestData/seed-data/load-all-seed-data.sh --dry-run

# Show detailed output for debugging
./tests/TestData/seed-data/load-all-seed-data.sh --verbose

# Custom retry attempts (default 3)
./tests/TestData/seed-data/load-all-seed-data.sh --retry-count 5

# Custom HTTP timeout in seconds (default 30)
./tests/TestData/seed-data/load-all-seed-data.sh --timeout 60

# Combine options
./tests/TestData/seed-data/load-all-seed-data.sh --verbose --dry-run --timeout 60
```

### Option 3: Honua CLI - Manual Import
```bash
# Import individual datasets
dotnet run --project src/Honua.Cli -- \
  ingest --service seed-data --layer cities \
  --file tests/TestData/seed-data/cities.geojson
```

### Option 4: QuickStart Mode (In-Memory)
```bash
HONUA_ALLOW_QUICKSTART=true \
DOTNET_ENVIRONMENT=QuickStart \
dotnet run --project src/Honua.Server.Host --urls http://127.0.0.1:5005
```

QuickStart mode automatically loads a subset of seed data into SQLite in-memory.

### Option 5: SQL Scripts (PostgreSQL)
```bash
psql -h localhost -U postgres -d honua \
  -f tests/TestData/seed-data/seed-data.sql
```

## Testing API Endpoints

### WFS 2.0/3.0
```bash
# GetCapabilities
curl "http://localhost:5005/wfs?service=WFS&request=GetCapabilities&version=2.0.0"

# GetFeature - cities
curl "http://localhost:5005/wfs?service=WFS&version=2.0.0&request=GetFeature&typeName=cities&count=10&outputFormat=application/json"

# GetFeature with filter - large cities
curl "http://localhost:5005/wfs?service=WFS&version=2.0.0&request=GetFeature&typeName=cities&cql_filter=population>1000000"
```

### WMS 1.3.0
```bash
# GetCapabilities
curl "http://localhost:5005/wms?service=WMS&request=GetCapabilities&version=1.3.0"

# GetMap - cities
curl "http://localhost:5005/wms?service=WMS&version=1.3.0&request=GetMap&layers=cities&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&format=image/png" > cities.png
```

### OGC API - Features
```bash
# Landing page
curl "http://localhost:5005/ogc/"

# Collections
curl "http://localhost:5005/ogc/collections"

# Collection items - cities
curl "http://localhost:5005/ogc/collections/seed-data::cities/items?limit=10"

# Spatial filter
curl "http://localhost:5005/ogc/collections/seed-data::cities/items?bbox=-180,-90,180,90&limit=5"
```

### STAC 1.0
```bash
# Root catalog
curl "http://localhost:5005/stac/"

# Collections
curl "http://localhost:5005/stac/collections"

# Search
curl "http://localhost:5005/stac/search?collections=seed-data::cities&limit=10"
```

### GeoServices REST
```bash
# FeatureServer metadata
curl "http://localhost:5005/rest/services/seed-data/FeatureServer"

# Query features
curl "http://localhost:5005/rest/services/seed-data/FeatureServer/0/query?where=population>500000&f=json"
```

## Verification Queries

### High Population Cities
```sql
SELECT name, country, population
FROM cities
WHERE population > 1000000
ORDER BY population DESC
LIMIT 10;
```

### Roads by Type
```sql
SELECT type, COUNT(*) as count, AVG(length_km) as avg_length
FROM roads
GROUP BY type
ORDER BY count DESC;
```

### Parcels by Zoning
```sql
SELECT zoning, COUNT(*) as count, AVG(area_sqm) as avg_area
FROM parcels
GROUP BY zoning;
```

### Temporal Query - Recent Weather Readings
```sql
SELECT station_id, name, temperature_c, last_reading
FROM weather_stations
WHERE last_reading > NOW() - INTERVAL '1 hour'
ORDER BY last_reading DESC;
```

## Extending Seed Data

To add new datasets:

1. Create GeoJSON file in `tests/TestData/seed-data/`
2. Follow naming convention: `{dataset_name}.geojson`
3. Include diverse attributes and geometries
4. Update `load-all-seed-data.sh` script
5. Update this README with dataset description
6. Add validation queries

## Performance Benchmarks

Expected performance with seed data:

| Operation | Expected Time | Features Returned |
|-----------|---------------|-------------------|
| GetCapabilities | < 100ms | N/A |
| GetFeature (all cities) | < 200ms | 50 |
| GetFeature (filtered) | < 150ms | 5-20 |
| Spatial query (bbox) | < 300ms | 10-50 |
| Complex join | < 500ms | 10-100 |
| Tile generation | < 100ms | 1 tile |

## Maintenance

Seed data is versioned and should be updated when:
- New geometry types are supported
- New attribute types are added
- API specifications change
- Additional test scenarios are needed

**Version:** 1.0.0
**Last Updated:** 2025-02-02
**Maintainer:** HonuaIO Team
