# OGC API Features Query Parsing - Code Examples

## 1. Query Parameter Validation Examples

### Example 1: Validating All Parameters
```csharp
var (query, contentCrs, includeCount, error) = OgcSharedHandlers.ParseItemsQuery(
    request: httpRequest,
    service: serviceDefinition,
    layer: layerDefinition,
    overrideQuery: null  // Use request.Query instead
);

if (error is not null)
{
    // error is an IResult with 400/406/etc status code
    return error;  // Return OGC Problem Details
}

// All parameters validated and normalized
var features = await repository.QueryAsync(
    service.Id, 
    layer.Id, 
    query,  // FeatureQuery with all parsed parameters
    cancellationToken);
```

### Example 2: Limit Parameter Validation

**Valid Requests:**
```
GET /ogc/collections/roads/items?limit=25     # Clamped to service/layer max
GET /ogc/collections/roads/items?limit=1      # Minimum valid
GET /ogc/collections/roads/items               # Uses default (10)
```

**Invalid Requests:**
```
GET /ogc/collections/roads/items?limit=0
Response: 400 Bad Request
{
  "type": "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "limit must be a positive integer.",
  "parameter": "limit"
}

GET /ogc/collections/roads/items?limit=abc
Response: Same as above
```

### Example 3: Offset Parameter Validation

**Valid Requests:**
```
GET /ogc/collections/roads/items?offset=0      # Start from beginning
GET /ogc/collections/roads/items?offset=100    # Skip first 100
GET /ogc/collections/roads/items                # Defaults to 0
```

**Invalid Requests:**
```
GET /ogc/collections/roads/items?offset=-1
Response: 400 Bad Request
{
  "detail": "offset must be zero or greater."
}

GET /ogc/collections/roads/items?offset=xyz
Response: Same error as above
```

### Example 4: Bounding Box Validation

**Valid Requests:**
```
# 2D bounding box
GET /ogc/collections/roads/items?bbox=-180,-90,180,90

# 3D bounding box with altitude
GET /ogc/collections/roads/items?bbox=-180,-90,0,180,90,1000

# With CRS suffix
GET /ogc/collections/roads/items?bbox=-100,40,-95,45,EPSG:4326

# Different coordinate formats
GET /ogc/collections/roads/items?bbox=0.0,0.0,180.0,90.0
GET /ogc/collections/roads/items?bbox=1e2,1e1,1.8e2,9e1
```

**Invalid Requests:**
```
# Wrong number of values
GET /ogc/collections/roads/items?bbox=-180,-90,180
Response: 400
{ "detail": "bbox must contain four or six numeric values." }

# Non-numeric values
GET /ogc/collections/roads/items?bbox=-180,abc,180,90
Response: 400
{ "detail": "bbox values must be numeric." }

# Min > Max
GET /ogc/collections/roads/items?bbox=180,-90,-180,90
Response: 400
{ "detail": "bbox minimums must be less than maximums." }

# Bad CRS suffix
GET /ogc/collections/roads/items?bbox=-180,-90,180,90,EPSG:9999
Response: 400
{ "detail": "Requested bbox-crs 'EPSG:9999' is not supported. 
             Supported CRS values: ..." }
```

## 2. CRS Resolution Examples

### Example 1: CRS Resolution Priority

**Request 1: Via Query Parameter**
```
GET /ogc/collections/roads/items?crs=EPSG:3857
Response: CRS resolved to EPSG:3857 (Web Mercator)
Header: Content-Crs: <EPSG:3857>
```

**Request 2: Via Accept-Crs Header**
```
GET /ogc/collections/roads/items
Accept-Crs: EPSG:4269; q=0.9, EPSG:4326; q=0.8

Response: CRS resolved to EPSG:4269 (higher quality)
Header: Content-Crs: <EPSG:4269>
```

**Request 3: Via Query Parameter (overrides Accept-Crs)**
```
GET /ogc/collections/roads/items?crs=EPSG:3857
Accept-Crs: EPSG:4269; q=0.9

Response: CRS resolved to EPSG:3857 (query parameter wins)
Header: Content-Crs: <EPSG:3857>
```

**Request 4: Default CRS**
```
GET /ogc/collections/roads/items
# No Accept-Crs header, no crs parameter

Response: CRS resolved to service default (usually CRS84)
Header: Content-Crs: <http://www.opengis.net/def/crs/OGC/1.3/CRS84>
```

### Example 2: CRS Validation Code

```csharp
// From QueryParsingHelpers.ResolveCrs()
var (resolvedCrs, crsError) = QueryParsingHelpers.ResolveCrs(
    raw: "EPSG:4326",
    supported: new[] { 
        "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        "EPSG:3857",
        "EPSG:4269" 
    },
    parameterName: "crs",
    defaultValue: "http://www.opengis.net/def/crs/OGC/1.3/CRS84");

if (crsError is not null)
{
    return (string.Empty, crsError);  // Return error result
}

var servedCrs = resolvedCrs ?? defaultValue;  // Always has a value
```

## 3. Temporal (DateTime) Filtering Examples

### Example 1: Valid DateTime Requests

```
# Both start and end
GET /ogc/collections/observations/items?datetime=2023-01-01T00:00:00Z/2023-12-31T23:59:59Z

# Only end (open start)
GET /ogc/collections/observations/items?datetime=.../2023-12-31T23:59:59Z

# Only start (open end)
GET /ogc/collections/observations/items?datetime=2023-01-01T00:00:00Z/...

# Single instant
GET /ogc/collections/observations/items?datetime=2023-06-15T12:00:00Z/2023-06-15T12:00:00Z

# Date only (normalized to start of day)
GET /ogc/collections/observations/items?datetime=2023-01-01/2023-12-31
```

### Example 2: Invalid DateTime Requests

```
# Invalid ISO 8601
GET /ogc/collections/observations/items?datetime=01/01/2023
Response: 400
{ "detail": "Unable to parse '01/01/2023'." }

# Invalid month
GET /ogc/collections/observations/items?datetime=2023-13-01
Response: 400
{ "detail": "Unable to parse '2023-13-01'." }

# Missing separator
GET /ogc/collections/observations/items?datetime=2023-01-01T00:00:00Z2023-12-31T23:59:59Z
Response: 400
{ "detail": "datetime must be start/end." }
```

### Example 3: DateTime Parsing Code

```csharp
var (temporalRange, temporalError) = QueryParsingHelpers.ParseTemporalRange(
    raw: "2023-01-01T00:00:00Z/2023-12-31T23:59:59Z");

if (temporalError is not null)
{
    return error;
}

// temporalRange is null if input was empty/whitespace
if (temporalRange is not null)
{
    var start = temporalRange.Value.Start;  // DateTimeOffset?
    var end = temporalRange.Value.End;      // DateTimeOffset?
    
    var temporal = new TemporalInterval(start, end);
    // Pass to repository for filtering
}
```

## 4. Result Type Examples

### Example 1: Hits vs Results

**Request 1: Get actual features (default)**
```
GET /ogc/collections/roads/items
Response:
{
  "type": "FeatureCollection",
  "features": [
    { "type": "Feature", "properties": {...}, "geometry": {...} },
    ...
  ],
  "numberMatched": 12543,
  "numberReturned": 10,
  "links": [...]
}
```

**Request 2: Get count only**
```
GET /ogc/collections/roads/items?resultType=hits
Response:
{
  "type": "FeatureCollection",
  "features": [],  # Empty array
  "numberMatched": 12543,
  "numberReturned": 0,  # Count-only response
  "links": [...]
}
```

**Request 3: Explicit count with results**
```
GET /ogc/collections/roads/items?resultType=results&count=true
Response:
{
  "numberMatched": 12543,  # Total count included
  "numberReturned": 10,
  "features": [...]
}
```

## 5. Format (f) Parameter Examples

### Example 1: Format Negotiation

**Request 1: Explicit format**
```
GET /ogc/collections/roads/items?f=json
Response: application/geo+json
{
  "type": "FeatureCollection",
  "features": [...]
}
```

**Request 2: Content negotiation via Accept header**
```
GET /ogc/collections/roads/items
Accept: text/html; q=0.9, application/geo+json; q=0.8

Response: text/html; charset=utf-8
<!DOCTYPE html>
<html>
  <body>
    <table>...</table>
  </body>
</html>
```

**Request 3: Query parameter overrides Accept header**
```
GET /ogc/collections/roads/items?f=kml
Accept: text/html

Response: application/vnd.google-earth.kml+xml
<?xml version="1.0" encoding="UTF-8"?>
<kml xmlns="http://www.opengis.net/kml/2.2">
  <Document>
    <Placemark>...</Placemark>
  </Document>
</kml>
```

### Example 2: Export Formats (with data)

```
# GeoPackage (SQLite database)
GET /ogc/collections/roads/items?f=geopkg
Response-Type: application/geopackage+sqlite3
[binary GeoPackage file]

# Shapefile (zipped)
GET /ogc/collections/roads/items?f=shapefile
Response-Type: application/zip
[zipped shapefile components]

# FlatGeobuf (binary columnar)
GET /ogc/collections/roads/items?f=flatgeobuf
Response-Type: application/vnd.flatgeobuf
[binary FlatGeobuf data]

# CSV with WKT geometry
GET /ogc/collections/roads/items?f=csv
Response-Type: text/csv
name,speed_limit,geometry
Main St,55,"LINESTRING(-122.4 37.8, -122.3 37.9)"
```

## 6. Property Filtering Examples

### Example 1: Select Specific Properties

```
# Get only name and id
GET /ogc/collections/roads/items?properties=name,id
Response:
{
  "features": [
    {
      "id": "road-1",
      "properties": {
        "name": "Main Street",
        "id": "road-1"
      },
      "geometry": null
    }
  ]
}

# Get name, geometry, and surface_type
GET /ogc/collections/roads/items?properties=name,geometry,surface_type
Response:
{
  "features": [
    {
      "properties": {
        "name": "Main Street",
        "surface_type": "asphalt"
      },
      "geometry": { "type": "LineString", "coordinates": [...] }
    }
  ]
}
```

## 7. SortBy Examples

### Example 1: Sorting by Single Field

```
# Ascending (default)
GET /ogc/collections/roads/items?sortby=name
Results ordered by name A-Z

# Descending with prefix
GET /ogc/collections/roads/items?sortby=-name
Results ordered by name Z-A

# Explicit direction syntax
GET /ogc/collections/roads/items?sortby=name:desc
Results ordered by name Z-A

# Alternative syntax
GET /ogc/collections/roads/items?sortby=name:d
Results ordered by name Z-A (using 'd' for descending)
```

### Example 2: Multi-Field Sorting

```
# Primary: state ascending, Secondary: county descending
GET /ogc/collections/roads/items?sortby=state:asc,-county
Results:
  AL items first (sorted by county desc within AL)
  AK items next (sorted by county desc within AK)
  ...

# All descending
GET /ogc/collections/roads/items?sortby=-state,-county,-name
```

### Example 3: Invalid Sort Requests

```
# Try to sort by geometry
GET /ogc/collections/roads/items?sortby=geometry
Response: 400
{ "detail": "Geometry fields cannot be used with sortby." }

# Non-existent field
GET /ogc/collections/roads/items?sortby=invalid_field
Response: 400
{ "detail": "[field resolution error]" }

# Empty field name
GET /ogc/collections/roads/items?sortby=:asc
Response: 400
{ "detail": "sortby field name cannot be empty." }
```

## 8. Filter (CQL) Examples

### Example 1: Text-Based CQL

```
# Simple comparison
GET /ogc/collections/roads/items?filter=name%20=%20%27Main%20St%27&filter-lang=cql-text
URL-decoded: filter=name = 'Main St'

# Spatial filter
GET /ogc/collections/roads/items?filter=INTERSECTS(geometry,%20POINT(-122.4%2037.8))
URL-decoded: filter=INTERSECTS(geometry, POINT(-122.4 37.8))

# Compound with AND
GET /ogc/collections/roads/items?filter=speed_limit%20%3E%2045%20AND%20surface_type%20=%20%27asphalt%27
URL-decoded: filter=speed_limit > 45 AND surface_type = 'asphalt'
```

### Example 2: JSON-Based CQL2

```
POST /ogc/collections/roads/items HTTP/1.1
Content-Type: application/json

{
  "collections": ["roads"],
  "filter": {
    "op": "and",
    "args": [
      {
        "op": ">",
        "args": [
          {"property": "speed_limit"},
          45
        ]
      },
      {
        "op": "=",
        "args": [
          {"property": "surface_type"},
          "asphalt"
        ]
      }
    ]
  },
  "filter-lang": "cql2-json"
}
```

## 9. Multi-Collection Search Example

### Example 1: GET Search

```
GET /ogc/search?collections=roads,buildings,parks&limit=10&bbox=-122.5,37.7,-122.3,37.9
```

### Example 2: POST Search (More Powerful)

```
POST /ogc/search HTTP/1.1
Content-Type: application/json

{
  "collections": ["roads", "buildings", "parks"],
  "bbox": [-122.5, 37.7, -122.3, 37.9],
  "limit": 10,
  "offset": 0,
  "resultType": "results",
  "properties": ["name", "geometry"],
  "filter": {
    "op": ">",
    "args": [
      {"property": "area"},
      1000
    ]
  },
  "filter-lang": "cql2-json"
}
```

## 10. Error Handling Pattern

### Example: Complete Error Handling

```csharp
var (query, contentCrs, includeCount, error) = OgcSharedHandlers.ParseItemsQuery(
    request,
    service,
    layer);

if (error is not null)
{
    // error is an IResult (possibly redirecting to an error endpoint)
    return error;
}

// Proceed with database query
try
{
    var features = await repository.QueryAsync(
        service.Id,
        layer.Id,
        query,
        cancellationToken);
    
    // Build response with features
    var response = new
    {
        type = "FeatureCollection",
        features = features,
        numberReturned = features.Count,
        links = BuildLinks(request, query)
    };
    
    return Results.Json(response);
}
catch (Exception ex)
{
    return OgcProblemDetails.CreateServerErrorProblem(
        $"Query execution failed: {ex.Message}");
}
```

