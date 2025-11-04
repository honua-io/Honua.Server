# OGC API Features Query Parsing - Quick Reference

## Core Entry Point
- **Main Function:** `OgcSharedHandlers.ParseItemsQuery()`
- **Location:** `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs` (Lines 80-316)
- **Called From:** Collection item queries and multi-collection searches

## Query Parameters Whitelist

```
limit          - Pagination limit (positive integer, > 0)
offset         - Pagination offset (integer, >= 0)
bbox           - Spatial filter (4 or 6 numeric values)
bbox-crs       - CRS for bounding box
datetime       - Temporal filter (ISO 8601 format)
resultType     - "results" or "hits"
properties     - Comma-separated property names
crs            - Output coordinate reference system
count          - Include total count (boolean)
f              - Response format
filter         - CQL filter expression
filter-lang    - Filter language (cql-text, cql2-json)
filter-crs     - CRS for filter expressions
ids            - Comma-separated feature IDs
sortby         - Comma-separated sort specification
```

## Parameter Parsing Summary

### Limit & Offset
| Parameter | Min | Max | Default | Required |
|-----------|-----|-----|---------|----------|
| limit | 1 | service/layer limit | 10 | No |
| offset | 0 | unbounded | 0 | No |

**Validation:**
- Limit must be positive integer
- Offset must be non-negative integer
- Both are clamped to configured service/layer limits

### Bounding Box
- **Format:** `minX,minY,maxX,maxY[,minZ,maxZ]`
- **Optional CRS suffix:** `minX,minY,maxX,maxY,EPSG:4326`
- **Validation:**
  - Exactly 4 or 6 values
  - All numeric (float/double)
  - min < max for each dimension

### CRS Resolution
**Priority:**
1. Accept-Crs header (with quality factors)
2. crs query parameter
3. Service default CRS
4. Layer CRS
5. OGC CRS84 (fallback)

**Normalization:** All CRS values normalized by `CrsHelper.NormalizeIdentifier()`

### DateTime
- **Format:** `start/end` (ISO 8601)
- **Examples:** `2023-01-01/2023-12-31`, `.../2023-12-31`, `2023-06-15/...`
- **Validation:** Valid ISO 8601 dates, timezone-aware, normalized to UTC

### ResultType
- **Values:** `"results"` (default), `"hits"` (count only)
- **Count Behavior:** When resultType=hits, count is always included

### Properties
- **Format:** Comma-separated column names
- **Behavior:** Omitted = all properties returned
- **Validation:** Case-insensitive matching against schema

### SortBy
- **Format:** `[+-]fieldname[:direction][,[+-]field2[:direction2]]`
- **Directions:** `a|asc|ascending|+` (ascending), `d|desc|descending|-` (descending)
- **Default:** Ascending
- **Validation:** Field must exist, geometry fields prohibited

### Format (f parameter)
| Alias | MIME Type | Notes |
|-------|-----------|-------|
| json/geojson | application/geo+json | Default |
| html | text/html | Browser-friendly |
| kml | application/vnd.google-earth.kml+xml | KML |
| kmz | application/vnd.google-earth.kmz | Compressed KML |
| topojson | application/topo+json | Topology JSON |
| geopkg/geopackage | application/geopackage+sqlite3 | SQLite-based |
| shp/shapefile | application/x-esri-shapefile | Shapefile |
| flatgeobuf | application/vnd.flatgeobuf | Binary format |
| geoarrow | application/vnd.apache.arrow.stream | Arrow IPC |
| csv | text/csv | CSV with geometry WKT |
| application/ld+json | application/ld+json | JSON-LD |
| application/geo+json-t | application/geo+json-t | GeoJSON with temporal |

## Error Responses

All errors return HTTP 4xx/5xx with OGC Problem Details:

```json
{
  "type": "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "Detailed error message",
  "parameter": "param_name"
}
```

### Exception Type URIs
- `invalid-parameter` - 400
- `not-found` - 404
- `conflict` - 409
- `invalid-value` - 400
- `invalid-crs` - 400
- `invalid-bbox` - 400
- `invalid-datetime` - 400
- `limit-out-of-range` - 400
- `not-acceptable` - 406
- `operation-not-supported` - 501

## Validation Helper Functions

| Function | Location | Purpose |
|----------|----------|---------|
| `ParsePositiveInt()` | QueryParsingHelpers.cs:136-184 | Limit/offset parsing |
| `ParseBoundingBox()` | QueryParsingHelpers.cs:49-104 | BBox validation |
| `ParseBoundingBoxWithCrs()` | QueryParsingHelpers.cs:106-134 | BBox with optional CRS |
| `ParseTemporalRange()` | QueryParsingHelpers.cs:303-335 | DateTime parsing |
| `ResolveCrs()` | QueryParsingHelpers.cs:218-258 | CRS normalization & validation |
| `ParseBoolean()` | QueryParsingHelpers.cs:260-291 | Boolean parameter parsing |
| `ParseList()` | OgcSharedHandlers.cs:794-805 | CSV list parsing |
| `ParseSortOrders()` | OgcSharedHandlers.cs:379-467 | SortBy parsing & validation |

## Output Data Model

All parsed parameters assembled into `FeatureQuery`:

```csharp
var query = new FeatureQuery(
    Limit: int,                                  // Effective limit
    Offset: int,                                 // Effective offset
    Bbox: BoundingBox?,                          // Spatial filter
    Temporal: TemporalInterval?,                 // Temporal filter
    ResultType: FeatureResultType,               // "results" or "hits"
    PropertyNames: IReadOnlyList<string>?,       // null = all properties
    SortOrders: IReadOnlyList<FeatureSortOrder>?,// Null = sort by ID
    Filter: QueryFilter?,                        // CQL filter
    Crs: string                                  // Output CRS
);
```

## Integration with Handlers

1. **Collection Items Query:**
   - `OgcFeaturesHandlers.ExecuteCollectionItemsAsync()`
   - Calls `ParseItemsQuery()` → `repository.QueryAsync()` → `repository.CountAsync()`

2. **Feature Retrieval:**
   - Single feature with `featureId`
   - Uses subset of query params (format, crs only)

3. **Multi-Collection Search:**
   - `OgcSharedHandlers.ExecuteSearchAsync()`
   - Collects parameters once, applies to multiple layers
   - Aggregates results if format supports it

## Key Implementation Details

### Unknown Parameter Handling
Any parameter not in the whitelist returns 400 error:
```
Unknown query parameter 'xyz'
```

### Limit Clamping Logic
```
Service Limit: 1000 (default)
Layer Limit: 500
Default: 10
User requests: 100

Effective: min(100, min(1000, 500)) = 100
User requests: 5000
Effective: min(5000, min(1000, 500)) = 500
No limit specified
Effective: min(10, min(1000, 500)) = 10
```

### CRS Normalization
- All CRS identifiers normalized for comparison
- Supports both EPSG codes and OGC URIs
- Case-insensitive matching
- Falls back to supported list if not found

### Response Headers
```
Content-Crs: <EPSG:4326>  (format: <identifier>)
Content-Type: application/geo+json (based on format)
```

## File References
- Main parsing: `src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs`
- Validation helpers: `src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs`
- Error definitions: `src/Honua.Server.Host/Ogc/OgcProblemDetails.cs`
- Features handlers: `src/Honua.Server.Host/Ogc/OgcFeaturesHandlers.cs`
- Shared types: `src/Honua.Server.Host/Ogc/OgcTypes.cs`
