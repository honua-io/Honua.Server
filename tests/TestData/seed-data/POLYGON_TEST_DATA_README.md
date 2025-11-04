# GeoJSON Polygon Test Data

Two comprehensive GeoJSON FeatureCollections created for polygon testing in EPSG:4326 projection.

## Files Overview

### 1. parcels.geojson (82 KB)
**75 parcels with realistic property attributes**

#### Geometry Characteristics
- Type: Polygon geometries
- Vertices per polygon: 4-9 vertices (with closing coordinate)
- Polygon ring distribution: 15-19 polygons per vertex count
- Global coverage: 12 major cities across 6 continents
- Coordinates: Valid EPSG:4326 (lon: -180/180, lat: -90/90)

#### Properties
- `parcel_id`: Unique identifier (PARC-001 to PARC-075)
- `owner`: Random person names
- `zoning`: Categorical (residential: 22, commercial: 20, agricultural: 19, industrial: 14)
- `area_sqm`: Integer (1,441 - 49,492 sqm, avg: 24,422)
- `assessed_value`: Currency (53,681 - 4,846,646)
- `year_built`: Integer (1851 - 2024)
- `bedrooms`: Nullable integer (1-5, null count: 36/75)
- `bathrooms`: Nullable decimal (1.0-4.5, null count: varies)
- `last_sale_date`: ISO 8601 datetime string

#### Data Diversity
- Wide range of property types (residential homes, commercial buildings, industrial lots, farms)
- Realistic assessed values correlating with zoning type
- Historical year_built range spanning 170+ years
- Null values for non-residential bedrooms/bathrooms
- Varies geographic locations ensure coordinate diversity

---

### 2. buildings_3d.geojson (127 KB)
**100 buildings with 3D attributes**

#### Geometry Characteristics
- Type: Polygon geometries (building footprints)
- Vertices per polygon: 4-13 vertices (with closing coordinate)
- Polygon ring distribution: 5-17 polygons per vertex count
- Global coverage: Distributed across same 12 major cities
- Coordinates: Valid EPSG:4326

#### Properties
- `building_id`: Unique identifier (BLDG-001 to BLDG-100)
- `name`: Text field (nullable, ~34% null values)
- `type`: Categorical (residential: 32, public: 26, industrial: 25, commercial: 17)
- `floors`: Integer (1 - 100, avg: 50)
- `height_m`: Integer (17 - 497 meters, avg: 264)
- `construction_year`: Integer (1900 - 2023)
- `renovation_year`: Nullable integer (~38% null values)
- `energy_rating`: Categorical A-G (roughly even distribution, 10-17 per grade)
- `occupancy`: Integer (0 - 10,000)

#### Data Diversity
- Small homes (1 floor, 17m) to skyscrapers (100 floors, 497m)
- Historical construction dates spanning 120+ years
- Realistic renovation patterns (older buildings more likely renovated)
- Complete null handling for optional fields (name, renovation_year)
- Energy ratings distributed across all grades A-G

---

## Technical Specifications

### Format & Validation
- Valid GeoJSON FeatureCollection format (RFC 7946 compliant)
- EPSG:4326 Coordinate Reference System (WGS 84)
- Coordinates: [longitude, latitude] (standard GeoJSON order)
- File sizes: Both under 1MB limit (parcels: 82KB, buildings: 127KB)

### Geometry Validation
- All geometries are simple polygons (no multi-polygons)
- All polygons properly closed (first and last coordinate identical)
- No self-intersecting polygons
- Valid coordinate ranges within Earth bounds

### Attribute Handling
- Proper null/empty value representation in JSON
- Numeric types: integers and decimals as appropriate
- String dates in ISO 8601 format with timezone (Z = UTC)
- Mixed nullable and required fields for realistic scenarios

---

## Geographic Distribution

Both files use data sampled from 12 global cities:
- **North America**: New York, Los Angeles, Chicago
- **Europe**: London, Paris, Berlin
- **Asia**: Tokyo, Dubai
- **Pacific**: Sydney, Melbourne
- **South America**: Rio de Janeiro
- **Scandinavia**: Oslo

Polygons distributed around these cities with randomized offsets to ensure geographic diversity while maintaining global coverage.

---

## Use Cases

### For Testing
- Polygon geometry parsing and validation
- Null/nullable field handling
- Large batch feature processing
- Geographic coordinate validation
- Attribute filtering and querying
- Data type coercion and mapping

### For Performance
- Large FeatureCollection handling (75 + 100 features)
- JSON serialization/deserialization benchmarks
- Polygon complexity performance testing
- Streaming vs. loading entire feature collections

---

## File Structure Example

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "properties": {
        "parcel_id": "PARC-001",
        "owner": "David Coleman",
        "zoning": "agricultural",
        "area_sqm": 14621,
        "assessed_value": 915544,
        "year_built": 1993,
        "bedrooms": null,
        "bathrooms": null,
        "last_sale_date": "2021-08-21T00:00:00Z"
      },
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-74.001, 40.708],
          [-74.002, 40.708],
          [-74.002, 40.709],
          [-74.001, 40.709],
          [-74.001, 40.708]
        ]]
      }
    }
  ]
}
```

---

## Statistics

### Parcels.geojson
- Total features: 75
- Zoning breakdown: residential (29%), commercial (27%), agricultural (25%), industrial (19%)
- Avg area: 24,422 sqm
- Year built span: 1851-2024
- Nullable fields: bedrooms (48%), bathrooms (varies)

### Buildings_3d.geojson
- Total features: 100
- Type breakdown: residential (32%), public (26%), industrial (25%), commercial (17%)
- Avg floors: 50
- Height range: 17-497m
- Nullable fields: name (34%), renovation_year (38%)
- Energy ratings: All grades A-G represented

