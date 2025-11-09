# 3D Coordinate Support in Honua Server

Honua Server provides comprehensive support for 3D geospatial data with Z (elevation/height) and M (measure) coordinates.

## Overview

The platform leverages NetTopologySuite's built-in 3D geometry support to automatically handle Z and M coordinates across all operations:

- **Data Storage**: Native 3D geometry types in PostGIS, SQLite/Spatialite, GeoPackage, and other databases
- **OGC Standards**: WFS, WMS, OGC API Features with CRS84H support
- **Export Formats**: GeoJSON, GeoPackage, Shapefile (PointZ/LineStringZ/PolygonZ), KML, FlatGeobuf
- **Coordinate Transformations**: Preserve Z/M coordinates during CRS transformations

## Configuration

### Layer-Level 3D Configuration

Enable 3D support by setting `hasZ` on your layer definition:

```json
{
  "layers": [{
    "id": "buildings-3d",
    "geometryType": "Polygon",
    "geometryField": "geom",
    "hasZ": true,
    "crs": ["EPSG:4326", "CRS84H"],
    "storage": {
      "table": "buildings",
      "geometryColumn": "geom",
      "srid": 4326,
      "hasZ": true
    }
  }]
}
```

### Storage-Level 3D Configuration

Indicate 3D geometry types at the storage level:

```json
{
  "storage": {
    "table": "flight_paths",
    "geometryColumn": "path",
    "srid": 4326,
    "hasZ": true,
    "hasM": false
  }
}
```

### 3D Bounding Boxes

Define extents with 6 values for 3D bounding boxes (minX, minY, minZ, maxX, maxY, maxZ):

```json
{
  "extent": {
    "bbox": [
      [-122.5, 37.7, 0, -122.3, 37.9, 500]
    ],
    "crs": "CRS84H"
  }
}
```

### Z-Field Mapping

Optionally specify a field containing Z values (useful for attribute-based elevation):

```json
{
  "hasZ": true,
  "zField": "elevation_m"
}
```

## Coordinate Reference Systems (CRS)

### CRS84H - 3D Geographic Coordinates

**CRS84H** is the 3D variant of CRS84 (WGS84 lon/lat):
- **Format**: Longitude, Latitude, Height (ellipsoidal height in meters)
- **URI**: `http://www.opengis.net/def/crs/OGC/0/CRS84h`
- **Use Case**: Global 3D data with heights above the WGS84 ellipsoid

When a layer has `hasZ: true`, CRS84H is automatically added to the supported CRS list in OGC API Features and WFS.

### EPSG Codes with Z

Most 2D EPSG codes (like EPSG:4326) support Z coordinates implicitly. The Z dimension is orthogonal to the horizontal datum.

## Database Support

### PostGIS (Recommended for 3D)

PostGIS natively supports 3D geometry types:

```sql
-- Create table with 3D geometry
CREATE TABLE buildings_3d (
  id SERIAL PRIMARY KEY,
  name VARCHAR(100),
  geom GEOMETRY(PolygonZ, 4326)
);

-- Insert 3D polygon
INSERT INTO buildings_3d (name, geom) VALUES (
  'Building A',
  ST_GeomFromText('POLYGON Z((0 0 0, 1 0 0, 1 1 10, 0 1 10, 0 0 0))', 4326)
);

-- Query with Z coordinates
SELECT
  name,
  ST_AsGeoJSON(geom) as geojson,
  ST_ZMin(geom) as min_elevation,
  ST_ZMax(geom) as max_elevation
FROM buildings_3d;
```

**Geometry Types:**
- `PointZ` - 3D points
- `LineStringZ` - 3D lines
- `PolygonZ` - 3D polygons
- `MultiPointZ`, `MultiLineStringZ`, `MultiPolygonZ`
- `PointZM` - 3D with measure
- `LineStringZM`, `PolygonZM`, etc.

### SQLite/Spatialite

```sql
-- Create 3D point table
CREATE TABLE sensors (
  id INTEGER PRIMARY KEY,
  name TEXT
);

SELECT AddGeometryColumn('sensors', 'location', 4326, 'POINTZ', 'XYZ');

-- Insert 3D point
INSERT INTO sensors (id, name, location) VALUES (
  1,
  'Sensor Alpha',
  GeomFromText('POINT Z(-122.4 37.8 50)', 4326)
);
```

### GeoPackage

GeoPackage supports Z and M dimensions in the gpkg_geometry_columns table:

```sql
-- Z dimension is stored in geometry type
-- GeoPackage automatically handles PointZ, LineStringZ, etc.
```

## OGC Services

### OGC API Features

When `hasZ: true`, the collection advertises CRS84H:

```json
{
  "collections": [{
    "id": "buildings-3d",
    "title": "3D Buildings",
    "crs": [
      "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
      "http://www.opengis.net/def/crs/OGC/0/CRS84h",
      "http://www.opengis.net/def/crs/EPSG/0/4326"
    ],
    "storageCrs": "http://www.opengis.net/def/crs/EPSG/0/4326"
  }]
}
```

**Query with 3D Bbox:**

```
GET /collections/buildings-3d/items?bbox=-122.5,37.7,0,-122.3,37.9,100&bbox-crs=CRS84H
```

The 6-value bbox filters by (minLon, minLat, minHeight, maxLon, maxLat, maxHeight).

### WFS 2.0

WFS GetCapabilities advertises 3D bounding boxes:

```xml
<WFS_Capabilities>
  <FeatureTypeList>
    <FeatureType>
      <Name>buildings-3d</Name>
      <DefaultCRS>urn:ogc:def:crs:EPSG::4326</DefaultCRS>
      <OtherCRS>urn:ogc:def:crs:OGC:0:CRS84h</OtherCRS>
      <WGS84BoundingBox dimensions="3">
        <ows:LowerCorner>-122.5 37.7 0</ows:LowerCorner>
        <ows:UpperCorner>-122.3 37.9 100</ows:UpperCorner>
      </WGS84BoundingBox>
    </FeatureType>
  </FeatureTypeList>
</WFS_Capabilities>
```

## Export Formats

### GeoJSON (3D)

GeoJSON natively supports 3D coordinates:

```json
{
  "type": "Feature",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4, 37.8, 50]
  },
  "properties": {
    "name": "Sensor Alpha",
    "elevation_m": 50
  }
}
```

### Shapefile

NetTopologySuite automatically exports 3D geometries using Shapefile Z types:
- **PointZ** (type 11)
- **PolyLineZ** (type 13)
- **PolygonZ** (type 15)
- **MultiPointZ** (type 18)

### KML

KML exports Z coordinates as altitude:

```xml
<Placemark>
  <Point>
    <coordinates>-122.4,37.8,50</coordinates>
  </Point>
</Placemark>
```

### GeoPackage

GeoPackage preserves Z dimensions in the geometry encoding.

## Code Examples

### Using GeometryTypeHelper

```csharp
using Honua.Server.Core.Data;
using NetTopologySuite.Geometries;

// Check if geometry has Z coordinate
var hasZ = GeometryTypeHelper.HasZCoordinate(geometry);

// Get OGC type name with Z suffix
var typeName = GeometryTypeHelper.GetOgcGeometryTypeName(geometry);
// Returns: "PointZ", "LineStringZ", "PolygonZ", etc.

// Get coordinate dimension
var dimension = GeometryTypeHelper.GetCoordinateDimension(geometry);
// Returns: 2 (XY), 3 (XYZ or XYM), or 4 (XYZM)
```

### Reading 3D Geometries

```csharp
using Honua.Server.Core.Data;
using NetTopologySuite.IO;

// Read from WKB (automatically detects 3D)
var geometry = GeometryReader.ReadWkbGeometry(wkbBytes, storageSrid: 4326);

// Z coordinate is preserved
var point = (Point)geometry;
var elevation = point.Z; // Z value
```

## Best Practices

1. **Always set `hasZ: true`** when your database geometries include Z coordinates
2. **Use CRS84H** for global 3D data with ellipsoidal heights
3. **Define 6-value bboxes** for 3D extent filtering
4. **Use PostGIS** for optimal 3D support (ST_3DDistance, ST_3DIntersects, etc.)
5. **Test exports** to ensure Z coordinates are preserved in all formats
6. **Document Z semantics**: Specify whether Z represents:
   - Height above ellipsoid (CRS84H)
   - Height above mean sea level
   - Height above ground
   - Depth below surface (negative Z)

## Limitations

### Vector Tiles (MVT)

The Mapbox Vector Tile (MVT) format is inherently 2D. Z coordinates are not included in vector tile exports. For 3D visualization, use:
- **OGC API Features** with GeoJSON output
- **3D Tiles** (future support)
- **KML** for Google Earth

### Web Mercator (EPSG:3857)

Web Mercator is a 2D projection. When transforming 3D data to EPSG:3857, Z coordinates are preserved but represent heights in the original vertical datum.

## See Also

- [3D Layers Example](./3d-layers-example.json) - Complete metadata configuration
- [OGC API Features Specification](https://docs.ogc.org/is/17-069r3/17-069r3.html)
- [WFS 2.0 Specification](http://docs.opengeospatial.org/is/09-025r2/09-025r2.html)
- [PostGIS 3D Functions](https://postgis.net/docs/reference.html#PostGIS_3D_Functions)
