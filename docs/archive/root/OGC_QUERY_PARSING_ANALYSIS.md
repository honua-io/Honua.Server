# OGC API Features Query Parameter Parsing Analysis

## Executive Summary

The Honua Server implements comprehensive OGC API Features query parameter parsing with sophisticated validation, error handling, and support for multiple response formats. Query parameter parsing is centralized in the `OgcSharedHandlers.ParseItemsQuery()` method, which validates and transforms URL parameters into a structured `FeatureQuery` object for database operations.

---

## 1. Query Parameter Parsing Architecture

### Main Entry Point: `ParseItemsQuery()`
**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs` (Lines 80-316)

The main parsing function is called from:
- `OgcFeaturesHandlers.ExecuteCollectionItemsAsync()` - For individual collection queries
- `OgcSharedHandlers.ExecuteSearchAsync()` - For multi-collection searches
- Query parameters can be overridden via `queryOverrides` parameter for programmatic requests

### Allowed Query Parameters

The function validates against a whitelist of allowed parameters:

```csharp
var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "limit",           // Pagination: max records to return
    "offset",          // Pagination: skip N records
    "bbox",            // Spatial filter: bounding box
    "bbox-crs",        // CRS for the bounding box
    "datetime",        // Temporal filter
    "resultType",      // "results" or "hits" (count only)
    "properties",      // Property filtering
    "crs",             // Output CRS
    "count",           // Include total count flag
    "f",               // Response format
    "filter",          // CQL filter expression
    "filter-lang",     // Filter language (cql-text, cql2-json)
    "filter-crs",      // CRS for spatial filters
    "ids",             // Feature ID filtering
    "sortby"           // Sorting specification
};
```

**Key Validation:** Any unknown parameter returns a 400 Bad Request error.

```csharp
if (!allowedKeys.Contains(key))
{
    return (default!, string.Empty, false, 
        CreateValidationProblem($"Unknown query parameter '{key}'.", key));
}
```

---

## 2. Limit and Offset Parsing

### Limit Parameter

**Function:** `QueryParsingHelpers.ParsePositiveInt()` (Lines 136-184 in QueryParsingHelpers.cs)

**Validation Rules:**
- Must be a positive integer (> 0)
- Optional parameter (not required)
- No default value - null if not provided
- Clamped to service/layer limits

**Code:**
```csharp
var (limitValue, limitError) = QueryParsingHelpers.ParsePositiveInt(
    queryCollection,
    "limit",
    required: false,
    defaultValue: null,
    allowZero: false,
    errorDetail: "limit must be a positive integer.");
if (limitError is not null)
{
    return (default!, string.Empty, false, limitError);
}
```

**Effective Limit Calculation:**
```csharp
const int DefaultPageSize = 10;
var serviceLimit = service.Ogc.ItemLimit;      // From service config
var layerLimit = layer.Query?.MaxRecordCount;  // From layer config

var maxAllowed = Math.Max(1, serviceLimit ?? 1000);
if (layerLimit.HasValue && layerLimit.Value > 0)
{
    maxAllowed = Math.Min(maxAllowed, layerLimit.Value);
}

var defaultPageSize = Math.Min(DefaultPageSize, maxAllowed);
var effectiveLimit = limitValue.HasValue
    ? Math.Clamp(limitValue.Value, 1, maxAllowed)
    : defaultPageSize;  // 10 or service limit, whichever is smaller
```

**Error Response Format:**
```
Status: 400 Bad Request
Problem Detail Type: http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter
Title: "Invalid Parameter"
Detail: "limit must be a positive integer."
Extensions: { "parameter": "limit" }
```

### Offset Parameter

**Function:** `QueryParsingHelpers.ParsePositiveInt()` (Lines 128-138)

**Validation Rules:**
- Must be zero or greater (>= 0)
- Optional parameter
- Default value: 0
- Allows zero

**Code:**
```csharp
var (offsetValue, offsetError) = QueryParsingHelpers.ParsePositiveInt(
    queryCollection,
    "offset",
    required: false,
    defaultValue: 0,
    allowZero: true,
    errorDetail: "offset must be zero or greater.");
if (offsetError is not null)
{
    return (default!, string.Empty, false, offsetError);
}

var effectiveOffset = offsetValue ?? 0;
```

**Error Response Format:**
```
Status: 400 Bad Request
Title: "Invalid Parameter"
Detail: "offset must be zero or greater."
Extensions: { "parameter": "offset" }
```

---

## 3. Bounding Box (Spatial Filter) Parsing

### BBox Parameter Parsing

**Function:** `ParseBoundingBox()` (Lines 745-764)
**Helper:** `QueryParsingHelpers.ParseBoundingBoxWithCrs()` (Lines 106-134)

**Validation Rules:**
- Must contain 4 or 6 numeric values (with optional altitude)
- Format: `minX,minY,maxX,maxY[,minZ,maxZ]`
- Optional CRS suffix at end (e.g., `minX,minY,maxX,maxY,crs`)
- Values must be numeric (float or double)
- Minimums must be less than maximums

**Code Example:**
```csharp
var bboxParse = ParseBoundingBox(queryCollection["bbox"]);
if (bboxParse.Error is not null)
{
    return (default!, string.Empty, false, bboxParse.Error);
}

// BBox structure after parsing:
// - 4-value: (minX, minY, maxX, maxY)
// - 6-value: (minX, minY, minZ, maxX, maxY, maxZ)
var coordinates = parsed.Value.Coordinates;
var crs = parsed.Value.Crs;

return coordinates.Length == 6
    ? (new BoundingBox(coordinates[0], coordinates[1], coordinates[3], 
        coordinates[4], coordinates[2], coordinates[5], crs), null)
    : (new BoundingBox(coordinates[0], coordinates[1], coordinates[2], 
        coordinates[3], Crs: crs), null);
```

**Validation Details:**
```csharp
// From QueryParsingHelpers.ParseBoundingBox() (Lines 61-104)
var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var expectedLengths = allowAltitude ? new[] { 4, 6 } : new[] { 4 };

if (Array.IndexOf(expectedLengths, parts.Length) < 0)
{
    return (null, Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid bbox parameter",
        detail: "bbox must contain four or six numeric values."));
}

var numbers = new double[parts.Length];
for (var i = 0; i < parts.Length; i++)
{
    if (!double.TryParse(parts[i], NumberStyles.Float | NumberStyles.AllowThousands, 
        CultureInfo.InvariantCulture, out numbers[i]))
    {
        return (null, Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid bbox parameter",
            detail: "bbox values must be numeric."));
    }
}

// Check min < max
if (numbers[0] > numbers[parts.Length == 4 ? 2 : 3] || 
    numbers[1] > numbers[parts.Length == 4 ? 3 : 4])
{
    return (null, Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid bbox parameter",
        detail: "bbox minimums must be less than maximums."));
}
```

### BBox CRS Parameter

**Function:** `QueryParsingHelpers.ResolveCrs()` (Lines 218-258)

**Validation Rules:**
- Optional parameter
- Must be supported by service/layer
- Default: layer storage CRS
- Can be EPSG code or OGC URI format

**Code:**
```csharp
var (bboxCrsCandidate, bboxCrsError) = QueryParsingHelpers.ResolveCrs(
    queryCollection["bbox-crs"].ToString(),
    supportedCrs,
    "bbox-crs",
    DetermineStorageCrs(layer));  // Default: storage CRS
if (bboxCrsError is not null)
{
    return (default!, string.Empty, false, bboxCrsError);
}

var bboxCrs = bboxCrsCandidate ?? DetermineStorageCrs(layer);
```

**Error Response:**
```
Status: 400 Bad Request
Title: "Invalid bbox-crs parameter"
Detail: "Requested bbox-crs 'EPSG:4999' is not supported. 
         Supported CRS values: http://www.opengis.net/def/crs/OGC/1.3/CRS84, ..."
```

---

## 4. CRS (Coordinate Reference System) Resolution

### Content CRS Parameter

**Function:** `ResolveContentCrs()` (Lines 650-669)

**Resolution Priority:**
1. Accept-Crs header (if supported)
2. crs query parameter (if not overridden by Accept-Crs)
3. Service default CRS
4. Layer default CRS
5. OGC CRS84 (fallback)

**Code:**
```csharp
// Parse input crs parameter
var requestedCrsRaw = queryCollection["crs"].ToString();

// Override with Accept-Crs header if present
if (string.IsNullOrWhiteSpace(requestedCrsRaw) && !string.IsNullOrWhiteSpace(acceptCrs))
{
    requestedCrsRaw = acceptCrs;
}

// Resolve against supported CRS
var (servedCrsCandidate, servedCrsError) = QueryParsingHelpers.ResolveCrs(
    requestedCrsRaw,
    supportedCrs,
    "crs",
    defaultCrs);  // Falls back to default CRS

if (servedCrsError is not null)
{
    return (default!, string.Empty, false, servedCrsError);
}

var servedCrs = servedCrsCandidate ?? defaultCrs;
```

### Accept-Crs Header

**Function:** `ResolveAcceptCrs()` (Lines 587-649)

**Validation Rules:**
- HTTP header parsing with quality factors
- Format: `crs-uri[;q=quality]`
- Quality factor (0.0-1.0) determines preference
- Returns first supported CRS by quality order
- Returns 406 Not Acceptable if none supported

**Code:**
```csharp
internal static (string? Value, IResult? Error) ResolveAcceptCrs(
    HttpRequest request, IReadOnlyCollection<string> supported)
{
    if (!request.Headers.TryGetValue("Accept-Crs", out var headerValues) || 
        headerValues.Count == 0)
    {
        return (null, null);
    }

    var candidates = new List<(string Crs, double Quality)>();
    
    foreach (var header in headerValues)
    {
        foreach (var token in header.Split(',', StringSplitOptions.RemoveEmptyEntries | 
            StringSplitOptions.TrimEntries))
        {
            var semicolonIndex = token.IndexOf(';');
            var crsToken = semicolonIndex >= 0 ? token[..semicolonIndex] : token;
            var quality = 1.0;

            if (semicolonIndex >= 0)
            {
                var parameters = token[(semicolonIndex + 1)..]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var parameter in parameters)
                {
                    var parts = parameter.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && 
                        string.Equals(parts[0], "q", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(parts[1], NumberStyles.Float, 
                            CultureInfo.InvariantCulture, out var parsedQ))
                    {
                        quality = parsedQ;
                    }
                }
            }

            candidates.Add((CrsHelper.NormalizeIdentifier(crsToken), quality));
        }
    }

    // Sort by quality (descending) and CRS name
    foreach (var candidate in candidates
        .OrderByDescending(c => c.Quality)
        .ThenBy(item => item.Crs, StringComparer.OrdinalIgnoreCase))
    {
        if (supported.Any(value => 
            string.Equals(value, candidate.Crs, StringComparison.OrdinalIgnoreCase)))
        {
            return (candidate.Crs, null);
        }
    }

    return (null, Results.StatusCode(StatusCodes.Status406NotAcceptable));
}
```

### Supported CRS Resolution

**Function:** `ResolveSupportedCrs()` (Lines 671-708)

**Source Priority:**
1. Layer-specific CRS values
2. Service default CRS
3. Service additional CRS
4. Fallback: CRS84

**Code:**
```csharp
internal static IReadOnlyList<string> ResolveSupportedCrs(
    ServiceDefinition service, LayerDefinition layer)
{
    var supported = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void AddValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        
        var normalized = CrsHelper.NormalizeIdentifier(value);
        if (seen.Add(normalized))
        {
            supported.Add(normalized);
        }
    }

    // Add layer CRS first (highest priority)
    foreach (var crs in layer.Crs)
    {
        AddValue(crs);
    }

    // Add service CRS
    AddValue(service.Ogc.DefaultCrs);
    foreach (var crs in service.Ogc.AdditionalCrs)
    {
        AddValue(crs);
    }

    // Fallback to CRS84
    if (supported.Count == 0)
    {
        AddValue(CrsHelper.DefaultCrsIdentifier);
    }

    return supported;
}
```

**Error Response:**
```
Status: 400 Bad Request
Title: "Invalid Parameter"
Detail: "CRS 'EPSG:9999' is not supported. Supported CRS: 
         http://www.opengis.net/def/crs/OGC/1.3/CRS84, ..."
Extensions: { "parameter": "crs" }
```

---

## 5. Result Type Determination

### ResultType Parameter

**Function:** `ParseResultType()` (Lines 779-792)

**Allowed Values:**
- `"results"` (default) - Return feature data
- `"hits"` - Return only count, no features

**Code:**
```csharp
private static (FeatureResultType Value, IResult? Error) ParseResultType(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return (FeatureResultType.Results, null);
    }

    return raw.ToLowerInvariant() switch
    {
        "results" => (FeatureResultType.Results, null),
        "hits" => (FeatureResultType.Hits, null),
        _ => (FeatureResultType.Results, CreateValidationProblem(
            "resultType must be either 'results' or 'hits'.", "resultType"))
    };
}
```

**Related: Count Parameter**

When `resultType=hits`, the count is always included:
```csharp
var includeCount = QueryParsingHelpers.ParseBoolean(
    queryCollection, "count", 
    defaultValue: resultType == FeatureResultType.Hits);
if (resultType == FeatureResultType.Hits)
{
    includeCount = true;  // Always count for hits
}
```

**Error Response:**
```
Status: 400 Bad Request
Title: "Invalid Parameter"
Detail: "resultType must be either 'results' or 'hits'."
Extensions: { "parameter": "resultType" }
```

---

## 6. Property Filtering

### Properties Parameter

**Function:** `ParseList()` (Lines 794-805) / `OgcHelpers.ParseList()` (Lines 46-54)

**Format:**
- Comma-separated list of property names
- Case-insensitive matching
- Optional parameter
- If omitted, all properties returned

**Code:**
```csharp
var rawProperties = ParseList(queryCollection["properties"]);
IReadOnlyList<string>? propertyNames = rawProperties.Count == 0 ? null : rawProperties;

// In FeatureQuery:
var query = new FeatureQuery(
    // ... other parameters
    PropertyNames: propertyNames,  // null = all properties
    // ...
);

private static IReadOnlyList<string> ParseList(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return Array.Empty<string>();
    }

    var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | 
        StringSplitOptions.TrimEntries)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToArray();
    return values.Length == 0 ? Array.Empty<string>() : values;
}
```

**Example:**
```
GET /ogc/collections/roads/items?properties=name,speed_limit,geometry
```

---

## 7. Sorting/Order By Handling

### SortBy Parameter

**Function:** `ParseSortOrders()` (Lines 379-467)

**Format:**
- Comma-separated field list
- Optional direction prefix: `+` (asc) or `-` (desc)
- Optional direction suffix: `:a`, `:asc`, `:ascending`, `:+`, `:d`, `:desc`, `:descending`, `:-`

**Validation Rules:**
- Fields must exist in schema
- Geometry fields cannot be sorted
- Empty field names rejected
- Empty direction segments rejected
- Default direction: ascending

**Code:**
```csharp
private static (IReadOnlyList<FeatureSortOrder>? SortOrders, IResult? Error) 
    ParseSortOrders(string? raw, LayerDefinition layer)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return (null, null);
    }

    var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | 
        StringSplitOptions.TrimEntries);
    var orders = new List<FeatureSortOrder>();

    foreach (var token in tokens)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0) continue;

        var direction = FeatureSortDirection.Ascending;
        
        // Check prefix direction
        if (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            direction = FeatureSortDirection.Descending;
            trimmed = trimmed[1..].Trim();
        }
        else if (trimmed.StartsWith("+", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].Trim();
        }

        var fieldToken = trimmed;
        var suffixIndex = trimmed.IndexOf(':');
        
        // Check suffix direction
        if (suffixIndex >= 0)
        {
            fieldToken = trimmed[..suffixIndex].Trim();
            var suffix = trimmed[(suffixIndex + 1)..].Trim();
            
            if (suffix.Length == 0)
            {
                return (null, CreateValidationProblem(
                    "sortby direction segment cannot be empty.", "sortby"));
            }

            direction = suffix.ToLowerInvariant() switch
            {
                "a" or "asc" or "ascending" or "+" => FeatureSortDirection.Ascending,
                "d" or "desc" or "descending" or "-" => FeatureSortDirection.Descending,
                _ => direction
            };

            // Validate known directions only
            if (!IsValidDirection(suffix))
            {
                return (null, CreateValidationProblem(
                    $"Unsupported sort direction '{suffix}'.", "sortby"));
            }
        }

        if (string.IsNullOrWhiteSpace(fieldToken))
        {
            return (null, CreateValidationProblem(
                "sortby field name cannot be empty.", "sortby"));
        }

        // Validate field exists and isn't geometry
        try
        {
            var (resolvedField, fieldType) = 
                CqlFilterParserUtils.ResolveField(layer, fieldToken);
            
            if (string.Equals(resolvedField, layer.GeometryField, 
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fieldType, "geometry", StringComparison.OrdinalIgnoreCase))
            {
                return (null, CreateValidationProblem(
                    "Geometry fields cannot be used with sortby.", "sortby"));
            }

            orders.Add(new FeatureSortOrder(resolvedField, direction));
        }
        catch (InvalidOperationException ex)
        {
            return (null, CreateValidationProblem(ex.Message, "sortby"));
        }
    }

    if (orders.Count == 0)
    {
        return (null, CreateValidationProblem(
            "sortby parameter must specify at least one field.", "sortby"));
    }

    return (orders, null);
}
```

**Default Behavior:**
When no sortby is specified, defaults to sorting by ID field (if available):
```csharp
IReadOnlyList<FeatureSortOrder>? sortOrders = sortOrdersExplicit;
if (sortOrders is null && !string.IsNullOrWhiteSpace(layer.IdField))
{
    sortOrders = new[] { 
        new FeatureSortOrder(layer.IdField, FeatureSortDirection.Ascending) 
    };
}
```

**Error Examples:**
```
Status: 400 Bad Request
Title: "Invalid Parameter"
Detail: "Geometry fields cannot be used with sortby."
Extensions: { "parameter": "sortby" }

Detail: "Unsupported sort direction 'xyz'."
Detail: "sortby field name cannot be empty."
Detail: "sortby parameter must specify at least one field."
```

---

## 8. Temporal (DateTime) Filtering

### DateTime Parameter

**Function:** `ParseTemporal()` (Lines 766-777) / `QueryParsingHelpers.ParseTemporalRange()` (Lines 303-335)

**Format:**
- `start/end` (both dates required)
- `.../end` (only end bound)
- `start/...` (only start bound)
- `start/start` (single instant in time)

**Date Format:**
- ISO 8601 (RFC 3339)
- Timezone aware
- Normalized to UTC

**Code:**
```csharp
private static (TemporalInterval? Value, IResult? Error) ParseTemporal(string? raw)
{
    var (range, error) = QueryParsingHelpers.ParseTemporalRange(raw);
    if (error is not null)
    {
        return (null, error);
    }

    return range is null
        ? (null, null)
        : (new TemporalInterval(range.Value.Start, range.Value.End), null);
}

public static (QueryTemporalRange? Value, IResult? Error) 
    ParseTemporalRange(string? raw, string parameterName = "datetime")
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return (null, null);
    }

    var trimmed = raw!.Trim();
    if (trimmed.Length == 0)
    {
        return (null, null);
    }

    var parts = trimmed.Split('/', StringSplitOptions.TrimEntries);
    if (parts.Length == 0 || parts.Length > 2)
    {
        return (null, Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid datetime parameter",
            detail: "datetime must be start/end."));
    }

    var start = ParseTemporalBoundary(parts[0], parameterName);
    if (start.Error is not null)
    {
        return (null, start.Error);
    }

    var end = parts.Length == 2 
        ? ParseTemporalBoundary(parts[1], parameterName) 
        : start;
    if (end.Error is not null)
    {
        return (null, end.Error);
    }

    return (new QueryTemporalRange(start.Value, end.Value), null);
}

private static (DateTimeOffset? Value, IResult? Error) 
    ParseTemporalBoundary(string value, string parameterName)
{
    if (string.IsNullOrWhiteSpace(value) || value == "..")
    {
        return (null, null);  // Open-ended boundary
    }

    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, 
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, 
        out var parsed))
    {
        return (parsed, null);
    }

    return (null, Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid datetime parameter",
        detail: $"Unable to parse '{value}'."));
}
```

**Valid Examples:**
```
datetime=2023-01-01T00:00:00Z/2023-12-31T23:59:59Z
datetime=2023-01-01/2023-12-31
datetime=.../2023-12-31T23:59:59Z
datetime=2023-01-01T00:00:00Z/...
datetime=2023-06-15T12:00:00Z/2023-06-15T12:00:00Z
```

**Error Response:**
```
Status: 400 Bad Request
Title: "Invalid datetime parameter"
Detail: "Unable to parse '2023-13-01'."
```

---

## 9. Response Format Parsing

### Format (f) Parameter

**Function:** `ResolveResponseFormat()` (Lines 513-563) / `ParseFormat()` (Lines 469-495)

**Supported Formats:**
| Format | Values | MIME Type |
|--------|--------|-----------|
| GeoJSON | json, geojson | application/geo+json |
| HTML | html, text/html | text/html; charset=utf-8 |
| KML | kml, application/vnd.google-earth.kml+xml | application/vnd.google-earth.kml+xml |
| KMZ | kmz, application/vnd.google-earth.kmz | application/vnd.google-earth.kmz |
| TopoJSON | topojson, application/topo+json | application/topo+json |
| GeoPackage | geopkg, geopackage | application/geopackage+sqlite3 |
| Shapefile | shp, shapefile | application/x-esri-shapefile |
| FlatGeobuf | flatgeobuf | application/vnd.flatgeobuf |
| GeoArrow | geoarrow | application/vnd.apache.arrow.stream |
| CSV | csv | text/csv |
| JSON-LD | application/ld+json | application/ld+json |
| GeoJSON-T | application/geo+json-t | application/geo+json-t |

**Resolution Priority:**
1. `f` query parameter
2. Accept header (with quality factors)
3. Default: GeoJSON

**Code:**
```csharp
internal static (OgcResponseFormat Format, string ContentType, IResult? Error) 
    ResolveResponseFormat(HttpRequest request, IQueryCollection? queryOverrides = null)
{
    // Priority 1: f parameter
    var formatParameter = queryOverrides?["f"].ToString();
    if (string.IsNullOrWhiteSpace(formatParameter))
    {
        formatParameter = request.Query["f"].ToString();
    }
    
    if (!string.IsNullOrWhiteSpace(formatParameter))
    {
        var (format, error) = ParseFormat(formatParameter);
        if (error is not null)
        {
            return (default, string.Empty, error);
        }
        return (format, GetMimeType(format), null);
    }

    // Priority 2: Accept header
    if (request.Headers.TryGetValue(HeaderNames.Accept, out var acceptValues) && 
        acceptValues.Count > 0)
    {
        if (MediaTypeHeaderValue.TryParseList(acceptValues, out var parsedAccepts))
        {
            var ordered = parsedAccepts
                .OrderByDescending(value => value.Quality ?? 1.0)
                .ToList();

            foreach (var media in ordered)
            {
                var mediaType = media.MediaType.ToString();
                if (string.IsNullOrWhiteSpace(mediaType)) continue;

                if (TryMapMediaType(mediaType!, out var mappedFormat))
                {
                    return (mappedFormat, GetMimeType(mappedFormat), null);
                }

                if (string.Equals(mediaType, "*/*", StringComparison.Ordinal))
                {
                    return (OgcResponseFormat.GeoJson, 
                        GetMimeType(OgcResponseFormat.GeoJson), null);
                }
            }

            return (default, string.Empty, 
                Results.StatusCode(StatusCodes.Status406NotAcceptable));
        }
    }

    // Priority 3: Default
    return (OgcResponseFormat.GeoJson, GetMimeType(OgcResponseFormat.GeoJson), null);
}
```

**Error Response:**
```
Status: 400 Bad Request
Title: "Invalid Parameter"
Detail: "Unsupported format 'xyz'."
Extensions: { "parameter": "f" }

Status: 406 Not Acceptable
Title: "Not Acceptable"
Detail: "None of the requested media types are supported."
```

---

## 10. Validation Rules and Error Responses

### OGC Problem Details Format

**Base Implementation:** `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcProblemDetails.cs`

**Standard Error Structure:**
```json
{
  "type": "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0/invalid-parameter",
  "title": "Invalid Parameter",
  "status": 400,
  "detail": "limit must be a positive integer.",
  "parameter": "limit"
}
```

### Exception Type URIs

```csharp
public static class ExceptionTypes
{
    private const string OgcApiFeaturesBase = 
        "http://www.opengis.net/def/exceptions/ogcapi-features-1/1.0";
    private const string OgcCommonBase = 
        "http://www.opengis.net/def/exceptions/ogcapi-common/1.0";

    public const string InvalidParameter = 
        $"{OgcApiFeaturesBase}/invalid-parameter";         // 400
    public const string NotFound = 
        $"{OgcApiFeaturesBase}/not-found";                  // 404
    public const string Conflict = 
        $"{OgcApiFeaturesBase}/conflict";                   // 409
    public const string OperationNotSupported = 
        $"{OgcApiFeaturesBase}/operation-not-supported";   // 501
    public const string InvalidValue = 
        $"{OgcApiFeaturesBase}/invalid-value";             // 400
    public const string ServerError = 
        $"{OgcCommonBase}/server-error";                    // 500
    public const string NotAcceptable = 
        $"{OgcApiFeaturesBase}/not-acceptable";            // 406
    public const string InvalidCrs = 
        $"{OgcApiFeaturesBase}/invalid-crs";               // 400
    public const string InvalidBbox = 
        $"{OgcApiFeaturesBase}/invalid-bbox";              // 400
    public const string InvalidDatetime = 
        $"{OgcApiFeaturesBase}/invalid-datetime";          // 400
    public const string LimitOutOfRange = 
        $"{OgcApiFeaturesBase}/limit-out-of-range";        // 400
}
```

### Error Creation Helper

```csharp
internal static IResult CreateValidationProblem(string detail, string parameter)
{
    var problemDetails = new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid request parameter",
        Detail = detail,
        Extensions = { ["parameter"] = parameter }
    };

    return Results.Problem(
        problemDetails.Detail,
        statusCode: problemDetails.Status,
        title: problemDetails.Title,
        extensions: problemDetails.Extensions);
}
```

### Common Validation Errors

| Parameter | Error Condition | Detail Message |
|-----------|-----------------|-----------------|
| limit | Non-integer value | "limit must be a positive integer." |
| limit | Zero or negative | "limit must be a positive integer." |
| offset | Non-integer value | "offset must be zero or greater." |
| offset | Negative | "offset must be zero or greater." |
| bbox | Wrong # of values | "bbox must contain four or six numeric values." |
| bbox | Non-numeric values | "bbox values must be numeric." |
| bbox | min > max | "bbox minimums must be less than maximums." |
| crs | Unsupported CRS | "CRS 'EPSG:9999' is not supported. Supported CRS: ..." |
| datetime | Invalid format | "Unable to parse '2023-13-01'." |
| resultType | Invalid value | "resultType must be either 'results' or 'hits'." |
| sortby | Geometry field | "Geometry fields cannot be used with sortby." |
| filter | Invalid expression | "Invalid filter expression. [parser error details]" |
| Unknown | Any unknown key | "Unknown query parameter 'xyz'." |

---

## 11. Advanced Features

### Filter (CQL) Parsing

**Function:** `ParseItemsQuery()` (Lines 174-224)

**Supported Filter Languages:**
- `cql-text` - OGC CQL text format (legacy subset)
- `cql2-json` - CQL2 JSON format

**Code:**
```csharp
var filterLangRaw = queryCollection["filter-lang"].ToString();
string? filterLangNormalized = null;

if (!string.IsNullOrWhiteSpace(filterLangRaw))
{
    filterLangNormalized = filterLangRaw.Trim().ToLowerInvariant();
    if (filterLangNormalized != "cql-text" &&
        filterLangNormalized != "cql2-json")
    {
        return (default!, string.Empty, false, CreateValidationProblem(
            $"filter-lang '{filterLangRaw}' is not supported. " +
            "Supported values: cql-text, cql2-json.", 
            "filter-lang"));
    }
}

QueryFilter? combinedFilter = null;
var rawFilter = queryCollection["filter"].ToString();

if (!string.IsNullOrWhiteSpace(rawFilter))
{
    var treatAsJsonFilter = 
        string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal) ||
        (filterLangNormalized is null && LooksLikeJson(rawFilter));

    try
    {
        if (treatAsJsonFilter)
        {
            combinedFilter = Cql2JsonParser.Parse(rawFilter, layer, normalizedFilterCrs);
            filterLangNormalized ??= "cql2-json";
        }
        else
        {
            combinedFilter = CqlFilterParser.Parse(rawFilter, layer);
        }
    }
    catch (Exception ex)
    {
        return (default!, string.Empty, false, CreateValidationProblem(
            $"Invalid filter expression. {ex.Message}", "filter"));
    }
}
else if (string.Equals(filterLangNormalized, "cql2-json", StringComparison.Ordinal))
{
    return (default!, string.Empty, false, CreateValidationProblem(
        "filter parameter is required when filter-lang=cql2-json.", "filter"));
}
```

### IDs (Feature ID) Filtering

**Function:** `BuildIdsFilter()` (Lines 334-377)

**Format:**
- Comma-separated list of feature IDs
- Supports multiple ID values
- Combined with OR logic

**Code:**
```csharp
var rawIds = ParseList(queryCollection["ids"]);
if (rawIds.Count > 0)
{
    var (idsFilter, idsError) = BuildIdsFilter(layer, rawIds);
    if (idsError is not null)
    {
        return (default!, string.Empty, false, idsError);
    }

    combinedFilter = CombineFilters(combinedFilter, idsFilter);
}
```

### Multi-Collection Search

**Function:** `OgcFeaturesHandlers.GetSearch()` / `OgcFeaturesHandlers.PostSearch()`

**Collections Parameter:**
- GET: Query parameter `collections=col1,col2,col3`
- POST: JSON body property `"collections": ["col1", "col2", "col3"]`
- Required for search endpoint
- Distinct collection IDs only

---

## 12. Integration Points

### Data Model

The parsed parameters are assembled into a `FeatureQuery` object:

```csharp
var query = new FeatureQuery(
    Limit: effectiveLimit,              // Pagination
    Offset: effectiveOffset,            // Pagination
    Bbox: bbox,                         // Spatial filter
    Temporal: timeParse.Value,          // Temporal filter
    ResultType: resultType,             // "results" or "hits"
    PropertyNames: propertyNames,       // Property filtering (null = all)
    SortOrders: sortOrders,             // Order by specification
    Filter: combinedFilter,             // CQL filter expression
    Crs: servedCrs);                    // Output CRS
```

This is then passed to the repository:
```csharp
await repository.QueryAsync(service.Id, layer.Id, query, cancellationToken)
await repository.CountAsync(service.Id, layer.Id, query, cancellationToken)
```

### Response Headers

When parsing succeeds, the response includes CRS headers:
```csharp
response = OgcSharedHandlers.WithResponseHeader(
    response, "Content-Crs", 
    OgcSharedHandlers.FormatContentCrs(contentCrs));  // <EPSG:4326>
```

---

## 13. Code File Locations Summary

| Component | File | Lines |
|-----------|------|-------|
| Main query parsing | OgcSharedHandlers.cs | 80-316 |
| Format resolution | OgcSharedHandlers.cs | 513-585 |
| CRS resolution | OgcSharedHandlers.cs | 587-728 |
| Bbox parsing | OgcSharedHandlers.cs | 745-764 |
| Temporal parsing | OgcSharedHandlers.cs | 766-777 |
| ResultType parsing | OgcSharedHandlers.cs | 779-792 |
| Sort order parsing | OgcSharedHandlers.cs | 379-467 |
| Helper validators | QueryParsingHelpers.cs | 1-413 |
| Problem details | OgcProblemDetails.cs | 1-315 |
| Features handlers | OgcFeaturesHandlers.cs | 1-1446 |

---

## 14. Key Takeaways

1. **Robust Validation**: All query parameters are validated against strict rules with detailed error messages
2. **OGC Compliance**: Error responses follow OGC API Features specification with proper exception type URIs
3. **CRS Handling**: Sophisticated CRS resolution with support for Accept-Crs headers and query parameters
4. **Format Flexibility**: Supports 12+ output formats with quality-factor based content negotiation
5. **Advanced Filtering**: Supports CQL filters, spatial/temporal filtering, and ID-based queries
6. **Pagination**: Configurable per-service and per-layer limits with defaults
7. **Property Filtering**: Optional column selection for efficient data transfer
8. **Sorting**: Multi-field sorting with ascending/descending control
