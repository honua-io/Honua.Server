# Filter-CRS Quick Reference Guide

## Overview

The `filter-crs` parameter allows clients to specify the Coordinate Reference System (CRS) of geometries in CQL2 filter expressions. The server automatically transforms these geometries to the storage CRS before querying.

## Basic Usage

### GET Request

```http
GET /ogc/collections/buildings/items?
  filter={"op":"s_intersects","args":[{"property":"geom"},{"type":"Point","coordinates":[-13627640.0,4544450.0]}]}&
  filter-lang=cql2-json&
  filter-crs=EPSG:3857
```

### POST Request

```http
POST /ogc/search
Content-Type: application/json

{
  "collections": ["buildings"],
  "filter": {
    "op": "s_intersects",
    "args": [
      {"property": "geom"},
      {"type": "Point", "coordinates": [-13627640.0, 4544450.0]}
    ]
  },
  "filter-crs": "EPSG:3857"
}
```

## Supported CRS Formats

| Format | Example |
|--------|---------|
| EPSG code | `EPSG:3857` |
| Numeric SRID | `3857` |
| OGC URN | `http://www.opengis.net/def/crs/EPSG/0/3857` |
| CRS84 | `http://www.opengis.net/def/crs/OGC/1.3/CRS84` |

## Common CRS Values

- **EPSG:4326** or **CRS84** - WGS84 (longitude, latitude)
- **EPSG:3857** - Web Mercator (used by Google Maps, OpenStreetMap)
- **EPSG:2154** - Lambert 93 (France)
- **EPSG:32633** - UTM Zone 33N (Central Europe)

## Spatial Predicates Supported

All CQL2 spatial operators work with Filter-CRS:

- `s_intersects` - Geometries intersect
- `s_contains` - Geometry contains another
- `s_within` - Geometry is within another
- `s_crosses` - Geometries cross
- `s_overlaps` - Geometries overlap
- `s_touches` - Geometries touch
- `s_disjoint` - Geometries are disjoint
- `s_equals` - Geometries are equal

## Examples

### Web Mercator Point Intersection

```json
{
  "filter": {
    "op": "s_intersects",
    "args": [
      {"property": "geom"},
      {
        "type": "Point",
        "coordinates": [-13627640.0, 4544450.0]
      }
    ]
  },
  "filter-crs": "EPSG:3857"
}
```

### WGS84 Polygon Within

```json
{
  "filter": {
    "op": "s_within",
    "args": [
      {"property": "geom"},
      {
        "type": "Polygon",
        "coordinates": [[
          [-122.5, 37.7],
          [-122.3, 37.7],
          [-122.3, 37.9],
          [-122.5, 37.9],
          [-122.5, 37.7]
        ]]
      }
    ]
  },
  "filter-crs": "EPSG:4326"
}
```

### Multiple Spatial Predicates

```json
{
  "filter": {
    "op": "and",
    "args": [
      {
        "op": "s_intersects",
        "args": [
          {"property": "geom"},
          {"type": "Point", "coordinates": [-13627640.0, 4544450.0]}
        ]
      },
      {
        "op": "s_within",
        "args": [
          {"property": "geom"},
          {
            "type": "Polygon",
            "coordinates": [[...]]
          }
        ]
      }
    ]
  },
  "filter-crs": "EPSG:3857"
}
```

## Important Notes

1. **Filter-CRS is optional** - If not specified, geometries are assumed to be in the layer's storage CRS

2. **Embedded CRS takes precedence** - If a GeoJSON geometry has an embedded CRS, it overrides filter-crs

3. **Transformation is automatic** - The server handles CRS transformation transparently

4. **Performance** - Transformations are cached and optimized with spatial indexes

5. **Validation** - Invalid CRS identifiers return a 400 Bad Request error

## Troubleshooting

### Error: "CRS 'X' is not supported"

**Cause:** The specified CRS is not in the layer's supported CRS list

**Solution:** Check the layer's metadata at `/ogc/collections/{collectionId}` for supported CRS values

### Error: "Invalid filter expression"

**Cause:** Malformed GeoJSON geometry or CQL2 syntax error

**Solution:** Validate your GeoJSON geometry and CQL2 expression structure

### Filter-CRS ignored

**Cause:** Geometry has embedded CRS that takes precedence

**Solution:** Remove embedded CRS from geometry, or ensure it matches filter-crs

## Related Parameters

- **`bbox-crs`** - Specifies CRS for bbox parameter
- **`crs`** - Specifies CRS for response geometries
- **`filter-lang`** - Specifies filter language (use `cql2-json` with filter-crs)

## See Also

- [OGC API Features Part 3: Filtering](https://docs.ogc.org/DRAFTS/19-079r1.html)
- [CQL2 Specification](https://docs.ogc.org/DRAFTS/21-065.html)
- [EPSG Code Database](https://epsg.io/)
- Full documentation: `FILTER_CRS_TRANSFORMATION_FIX_COMPLETE.md`
