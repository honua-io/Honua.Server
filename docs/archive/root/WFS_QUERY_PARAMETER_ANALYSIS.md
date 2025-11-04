# WFS Query Parameter Parsing Analysis - HonuaIO

## Executive Summary

This report analyzes how the HonuaIO WFS (Web Feature Service) implementation handles query parameter parsing. The analysis reveals a well-structured, layered approach to parameter parsing with strong validation and error handling following OGC WFS 2.0 standards.

---

## 1. Query Parameter Parsing Architecture

### Entry Points
- **Primary Router**: `/src/Honua.Server.Host/Wfs/WfsHandlers.cs` - Main request dispatcher
- **GetFeature Handler**: `/src/Honua.Server.Host/Wfs/WfsGetFeatureHandlers.cs` - Feature query execution
- **Helpers**: `/src/Honua.Server.Host/Wfs/WfsHelpers.cs` - Reusable parsing functions
- **Utilities**: `/src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs` - Generic query parsing

---

## 2. Query Parameters Parsed

### 2.1 Pagination Parameters

#### `count` / `maxFeatures`
- **Location**: `WfsGetFeatureHandlers.cs` lines 225, 127-129
- **Parsing Logic**:
  ```csharp
  var count = WfsHelpers.ParseInt(query, "count", WfsConstants.DefaultCount, allowZero: false);
  // WFS COMPLIANCE: Enforce configured limits to prevent unbounded scans
  count = WfsHelpers.EnforceCountLimit(count, layer, service);
  ```
- **WFS Standard**: WFS 2.0 uses `count` (OGC filter naming)
- **Default Value**: `100` (WfsConstants.DefaultCount)
- **Validation**:
  - Must be positive integer (non-zero)
  - Cannot exceed layer.Query.MaxRecordCount
  - Cannot exceed service.Ogc.ItemLimit
  - Absolute maximum cap: 5000 records (DoS protection)
- **Safety Mechanism**: `EnforceCountLimit()` prevents unbounded queries
  - Layer-level limit: `layer.Query.MaxRecordCount`
  - Service-level limit: `service.Ogc.ItemLimit`
  - Absolute maximum: 5000 (hardcoded safe default)

#### `startIndex`
- **Location**: `WfsGetFeatureHandlers.cs` lines 130, 228
- **Parsing Logic**:
  ```csharp
  var startIndex = WfsHelpers.ParseInt(query, "startIndex", 0, allowZero: true);
  ```
- **WFS Standard**: WFS 2.0 standard parameter
- **Default Value**: 0 (first feature)
- **Validation**:
  - Must be non-negative integer
  - Allows zero (unlike count)
  - No upper bound validation

---

### 2.2 Spatial Filtering

#### `bbox`
- **Location**: `WfsHelpers.cs` lines 195-214
- **Parsing Logic**:
  ```csharp
  public static BoundingBox? ParseBoundingBox(IQueryCollection query)
  {
      var raw = QueryParsingHelpers.GetQueryValue(query, "bbox");
      var (bbox, error) = QueryParsingHelpers.ParseBoundingBoxWithCrs(raw, allowAltitude: true);
      if (bbox is null)
      {
          if (string.IsNullOrWhiteSpace(raw))
          {
              return null;
          }
          var message = QueryParsingHelpers.ExtractProblemMessage(error, 
              "bbox must contain four comma-separated values.");
          throw new InvalidOperationException(message);
      }

      var coordinates = bbox.Value.Coordinates;
      return coordinates.Length == 6
          ? new BoundingBox(coordinates[0], coordinates[1], coordinates[3], 
                           coordinates[4], coordinates[2], coordinates[5], bbox.Value.Crs)
          : new BoundingBox(coordinates[0], coordinates[1], coordinates[2], 
                           coordinates[3], Crs: bbox.Value.Crs);
  }
  ```
- **Format Supported**:
  - 4 values: `minX,minY,maxX,maxY` (2D)
  - 6 values: `minX,minY,minZ,maxX,maxY,maxZ` (3D with altitude)
  - Optional CRS suffix: `minX,minY,maxX,maxY,CRS` (e.g., `EPSG:4326`)
- **Validation**:
  - All values must be numeric (double precision)
  - minX must be <= maxX
  - minY must be <= maxY
  - If 3D: minZ must be <= maxZ
  - Empty bbox rejected
- **Error Response**: Returns parsed BoundingBox or null (optional parameter)
- **CRS Extraction**: If last token is non-numeric, treated as CRS identifier

---

### 2.3 Coordinate Reference System (CRS)

#### `srsName`
- **Location**: `WfsGetFeatureHandlers.cs` lines 229, 240-244
- **Parsing Logic**:
  ```csharp
  var srsName = QueryParsingHelpers.GetQueryValue(query, "srsName");
  
  var requestedCrs = !string.IsNullOrWhiteSpace(srsName) ? srsName : service.Ogc.DefaultCrs;
  if (string.IsNullOrWhiteSpace(requestedCrs))
  {
      requestedCrs = "EPSG:4326";  // Fallback to WGS84
  }
  ```
- **Accepted Formats**:
  - EPSG codes: `EPSG:4326`, `EPSG:3857`
  - URN format: `urn:ogc:def:crs:EPSG::4326`
  - Custom URNs
- **Default Behavior**:
  1. Use requested `srsName` if provided
  2. Fall back to service default CRS
  3. Fall back to EPSG:4326 (WGS84)
- **CRS Normalization**:
  - Converted to URN format: `CrsNormalizationHelper.NormalizeIdentifier()`
  - Applied to response: `WfsHelpers.ToUrn()`

---

### 2.4 Result Type

#### `resultType`
- **Location**: `WfsHelpers.cs` lines 219-225
- **Parsing Logic**:
  ```csharp
  public static FeatureResultType ParseResultType(IQueryCollection query)
  {
      var value = QueryParsingHelpers.GetQueryValue(query, "resultType");
      return string.Equals(value, "hits", StringComparison.OrdinalIgnoreCase)
          ? FeatureResultType.Hits
          : FeatureResultType.Results;
  }
  ```
- **Valid Values**:
  - `hits` - Return only count (numberMatched), no features
  - `results` (default) - Return features and count
- **Implementation**:
  - `resultType=hits` creates query: `ResultType = FeatureResultType.Hits`
  - Features not fetched when `resultType=hits`
  - Line 179: Check prevents feature loading if resultType is Hits

---

### 2.5 Output Format

#### `outputFormat`
- **Location**: `WfsGetFeatureHandlers.cs` lines 233-237
- **Parsing Logic**:
  ```csharp
  var outputFormatRaw = QueryParsingHelpers.GetQueryValue(query, "outputFormat");
  if (!WfsHelpers.TryNormalizeOutputFormat(outputFormatRaw, out var outputFormat))
  {
      return Result<FeatureQueryExecution>.Failure(
          Error.Invalid($"Output format '{outputFormatRaw}' is not supported."));
  }
  ```
- **Supported Formats** (`WfsHelpers.TryNormalizeOutputFormat()`):
  - **GeoJSON** (Default):
    - `application/geo+json`
    - `application/json`
    - `json`
  - **GML 3.2**:
    - `application/gml+xml; version=3.2`
    - `application/gml+xml`
    - `gml32`, `gml`
  - **CSV**:
    - `text/csv`
    - `csv`
  - **Shapefile**:
    - `application/x-shapefile`
    - `shapefile`, `shape`
- **Default**: GML 3.2 if not specified
- **Validation**: Rejects unsupported formats with error

---

### 2.6 Feature Type / Layer Selection

#### `typeNames` / `typeName`
- **Location**: `WfsGetFeatureHandlers.cs` lines 209, 112
- **Parsing Logic**:
  ```csharp
  var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") 
      ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
  
  if (string.IsNullOrWhiteSpace(typeNamesRaw))
  {
      return Result<FeatureQueryExecution>.Failure(
          Error.Invalid("Parameter 'typeNames' is required."));
  }

  var contextResult = await WfsHelpers.ResolveLayerContextAsync(
      typeNamesRaw, catalog, contextResolver, cancellationToken);
  ```
- **Format**:
  - Single layer: `LayerId` (e.g., `roads-primary`)
  - Qualified: `ServiceId:LayerId` (e.g., `roads:roads-primary`)
  - Multiple (not fully supported): comma-separated
- **Resolution Logic** (`ResolveLayerContextAsync()`, lines 45-101):
  - Split by colon: if 2 parts, first is service ID
  - If no service ID provided, search all services for layer name
  - Returns `FeatureContext` with resolved Service and Layer
- **Validation**:
  - Required parameter
  - Must resolve to existing layer
  - Returns error if not found

---

### 2.7 Filtering (WFS 2.0)

#### `filter` / `cql_filter` / `FILTER`
- **Location**: `WfsHelpers.cs` lines 306-363
- **Parsing Logic**:
  ```csharp
  public static async Task<QueryFilter?> BuildFilterAsync(
      HttpRequest request, IQueryCollection query, LayerDefinition layer, CancellationToken cancellationToken)
  {
      foreach (var candidate in new[]
      {
          QueryParsingHelpers.GetQueryValue(query, "filter"),
          QueryParsingHelpers.GetQueryValue(query, "cql_filter"),
          QueryParsingHelpers.GetQueryValue(query, "FILTER")
      })
      {
          var parsed = TryParseFilter(candidate, layer);
          if (parsed is not null)
          {
              return parsed;
          }
      }

      if (!HttpMethods.IsPost(request.Method))
      {
          return null;
      }

      // POST body parsing for XML filters
      request.EnableBuffering();
      request.Body.Seek(0, SeekOrigin.Begin);
      using var reader = new StreamReader(request.Body, Encoding.UTF8, ...);
      var body = await reader.ReadToEndAsync(cancellationToken);
      request.Body.Seek(0, SeekOrigin.Begin);

      if (string.IsNullOrWhiteSpace(body))
      {
          return null;
      }

      return ParseXmlFilter(body, layer);
  }
  ```
- **Supported Filter Types**:
  1. **CQL Text Filter** (query parameter)
     - Format: `status = 'open'`
     - Parser: `CqlFilterParser.Parse(text, layer)`
  2. **XML Filter** (POST body or query parameter)
     - Format: OGC Filter XML
     - Parser: `XmlFilterParser.Parse(xml, layer)`
- **Parsing Strategy**:
  - Checks query parameters first: `filter`, `cql_filter`, `FILTER`
  - If POST request and no query parameter, reads request body
  - If text starts with `<`, treats as XML
  - Otherwise treats as CQL
- **Error Handling**: Throws `InvalidOperationException` with parsing error details

#### CQL Filter Parser (`/src/Honua.Server.Core/Query/Filter/CqlFilterParser.cs`)
- Supports: Comparison operators, logical operators (AND, OR, NOT)
- Field resolution: Maps to layer fields

#### XML Filter Parser (`/src/Honua.Server.Host/Wfs/Filters/XmlFilterParser.cs`)
- Security: Uses `SecureXmlSettings.ParseSecure()` to prevent XXE attacks
- Supports: OGC Filter XML with operators like:
  - PropertyIsEqualTo, PropertyIsNotEqualTo
  - PropertyIsLessThan, PropertyIsGreaterThan
  - PropertyIsNull
  - And, Or, Not combinations

---

### 2.8 Property Selection (GetPropertyValue)

#### `valueReference`
- **Location**: `WfsGetFeatureHandlers.cs` lines 106-110
- **Parsing Logic**:
  ```csharp
  var valueReference = QueryParsingHelpers.GetQueryValue(query, "valueReference") 
      ?? QueryParsingHelpers.GetQueryValue(query, "VALUEREFERENCE");
  if (string.IsNullOrWhiteSpace(valueReference))
  {
      return WfsHelpers.CreateException("MissingParameterValue", "valueReference", 
          "Parameter 'valueReference' is required.");
  }
  ```
- **Usage**: GetPropertyValue operation only
- **Validation**: Required for GetPropertyValue requests
- **Implementation**: Returns values for single specified property

---

### 2.9 Stored Queries

#### `storedQuery_Id`
- **Location**: `WfsGetFeatureHandlers.cs` lines 203-207
- **Parsing Logic**:
  ```csharp
  var storedQueryId = QueryParsingHelpers.GetQueryValue(query, "storedQuery_Id") 
      ?? QueryParsingHelpers.GetQueryValue(query, "STOREDQUERY_ID");
  if (!string.IsNullOrWhiteSpace(storedQueryId))
  {
      return await BuildStoredQueryExecutionAsync(...);
  }
  ```
- **Mandatory Stored Query**: `urn:ogc:def:query:OGC-WFS::GetFeatureById`
- **Custom Parameters**: Stored queries can define custom parameters (e.g., `${paramName}`)

#### GetFeatureById Parameters:
- **`id`** (required): Feature ID to retrieve

---

### 2.10 Locking Parameters

#### `expiry`
- **Location**: `WfsHelpers.cs` lines 230-244
- **Parsing Logic**:
  ```csharp
  public static TimeSpan ParseLockDuration(IQueryCollection query)
  {
      var expiry = QueryParsingHelpers.GetQueryValue(query, "expiry") 
          ?? QueryParsingHelpers.GetQueryValue(query, "EXPIRY");
      if (string.IsNullOrWhiteSpace(expiry))
      {
          return WfsConstants.DefaultLockDuration;  // 5 minutes
      }

      if (!double.TryParse(expiry, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) 
          || minutes <= 0)
      {
          throw new InvalidOperationException(
              "Parameter 'expiry' must be a positive numeric value representing minutes.");
      }

      return TimeSpan.FromMinutes(minutes);
  }
  ```
- **Default**: 5 minutes (WfsConstants.DefaultLockDuration)
- **Format**: Floating-point number representing minutes
- **Validation**: Must be positive

---

## 3. Supported Query Parameters by Operation

| Operation | Parameters |
|-----------|------------|
| **GetFeature** | service, request, typeNames, count, startIndex, srsName, bbox, filter, cql_filter, resultType, outputFormat |
| **GetPropertyValue** | service, request, typeNames, valueReference, count, startIndex |
| **GetFeatureWithLock** | (GetFeature params) + expiry |
| **LockFeature** | service, request, typeNames, expiry, lockAction |
| **Transaction** | (POST body) typeName, lockId, releaseAction |
| **DescribeFeatureType** | service, request, typeNames |
| **ListStoredQueries** | service, request |
| **DescribeStoredQueries** | service, request, storedQueryId |

---

## 4. Validation Rules

### 4.1 Query Parameter Validation Pipeline

1. **Service Validation**
   ```csharp
   var serviceValue = QueryParsingHelpers.GetQueryValue(query, "service");
   if (!string.Equals(serviceValue, "WFS", StringComparison.OrdinalIgnoreCase))
   {
       return WfsHelpers.CreateException("InvalidParameterValue", "service", 
           "Parameter 'service' must be set to 'WFS'.");
   }
   ```

2. **Request Type Validation**
   ```csharp
   var requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
   if (string.IsNullOrWhiteSpace(requestValue))
   {
       return WfsHelpers.CreateException("MissingParameterValue", "request", 
           "Parameter 'request' is required.");
   }
   ```

3. **Layer/Type Resolution Validation**
   - Layer must exist in catalog
   - Service ID (if qualified) must exist
   - Returns: `InvalidParameterValue` if not found

4. **Numeric Parameter Validation**
   ```csharp
   public static (int? Value, IResult? Error) ParsePositiveInt(
       IQueryCollection query,
       string key,
       bool required = false,
       int? defaultValue = null,
       bool allowZero = false,
       string? errorDetail = null)
   {
       if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
           parsed < 0 || (!allowZero && parsed == 0))
       {
           return (null, BuildIntegerProblem(key, allowZero, errorDetail));
       }
       return (parsed, null);
   }
   ```

5. **Bounding Box Validation**
   - Must have exactly 4 or 6 comma-separated numeric values
   - minX < maxX, minY < maxY (if 3D: minZ < maxZ)

6. **CRS Validation**
   - Must be parseable as CRS identifier
   - Normalized to URN format

7. **Output Format Validation**
   - Must be in supported list
   - Case-insensitive matching

8. **Filter Validation**
   - XML: Must be well-formed, parsed securely against XXE
   - CQL: Must follow CQL grammar

---

## 5. Error Response Format (OGC ExceptionReport)

### Location: `/src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs`

### WFS Exception Format
```xml
<?xml version="1.0" encoding="utf-8"?>
<ows:ExceptionReport 
    xmlns:ows="http://www.opengis.net/ows/1.1" 
    version="2.0.0">
    <ows:Exception exceptionCode="InvalidParameterValue" locator="count">
        <ows:ExceptionText>Parameter 'count' must be a positive integer.</ows:ExceptionText>
    </ows:Exception>
</ows:ExceptionReport>
```

### Exception Codes Used
| Code | Meaning | Example |
|------|---------|---------|
| `InvalidParameterValue` | Parameter value is invalid | count, bbox, outputFormat |
| `MissingParameterValue` | Required parameter not provided | service, request, typeNames |
| `OperationNotSupported` | Operation not supported | request type |
| `NoApplicableCode` | General error | layer resolution failures |
| `OperationParsingFailed` | Parameter parsing error | filter XML parsing |

### HTTP Status Code
- **400 Bad Request** - All WFS exceptions return 400 status
- **Content-Type**: `application/xml`

### Exception Creation
```csharp
public static IResult CreateWfsException(string code, string? locator, string message, 
    string version = "2.0.0")
{
    var owsNs = XNamespace.Get("http://www.opengis.net/ows/1.1");
    var document = new XDocument(
        new XDeclaration("1.0", "utf-8", null),
        new XElement(owsNs + "ExceptionReport",
            new XAttribute("version", version),
            new XAttribute(XNamespace.Xmlns + "ows", owsNs),
            new XElement(owsNs + "Exception",
                new XAttribute("exceptionCode", code),
                string.IsNullOrWhiteSpace(locator) ? null : new XAttribute("locator", locator),
                new XElement(owsNs + "ExceptionText", message))));

    var xml = document.ToString(SaveOptions.DisableFormatting);
    return Results.Content(xml, "application/xml", statusCode: StatusCodes.Status400BadRequest);
}
```

---

## 6. Advanced Query Features

### 6.1 Property Filtering (Not Directly in WFS GetFeature)
- **Location**: GetPropertyValue operation
- **Implementation**: `PropertyNames` field in `FeatureQuery`
- **Usage**:
  ```csharp
  var featureQuery = new FeatureQuery(
      Limit: count,
      Offset: startIndex,
      PropertyNames: new[] { valueReference });
  ```

### 6.2 Sorting (Defined but Not Exposed in WFS Layer)
- **Location**: `FeatureQuery.SortOrders` (IReadOnlyList<FeatureSortOrder>)
- **Data Structure**:
  ```csharp
  public sealed record FeatureSortOrder(string Field, FeatureSortDirection Direction);
  ```
- **Status**: Infrastructure present but not exposed via WFS query parameters
- **Use Case**: Possible for future WFS 2.0 extensions

### 6.3 Result Type Limiting
- **`FeatureResultType.Hits`**: Only count returned, no features fetched
- **`FeatureResultType.Results`**: Full features with geometries returned

---

## 7. Data Flow: Query Parameter to Database

```
WFS Request
    ↓
WfsHandlers.HandleAsync() - Route dispatcher
    ↓
WfsGetFeatureHandlers.ExecuteFeatureQueryAsync()
    ↓
WfsGetFeatureHandlers.BuildFeatureQueryExecutionAsync()
    ├─ Parse count, startIndex, srsName, bbox, resultType, outputFormat
    ├─ Resolve typeNames to FeatureContext
    ├─ Build filter (CQL or XML)
    └─ Create FeatureQuery object
        ├─ Limit: count (enforced limit)
        ├─ Offset: startIndex
        ├─ Bbox: spatial filter
        ├─ Filter: attribute filter
        ├─ ResultType: hits or results
        ├─ Crs: coordinate system
        └─ PropertyNames: (for GetPropertyValue only)
    ↓
repository.QueryAsync(FeatureQuery)
    ↓
Database Provider (Postgres, SqlServer, etc.)
    ├─ Apply LIMIT clause
    ├─ Apply OFFSET clause
    ├─ Apply spatial filter
    ├─ Apply attribute filter
    └─ Return FeatureRecords
    ↓
WfsResponseBuilders.BuildGmlResponse() or BuildGeoJsonResponse()
```

---

## 8. Security Considerations

### 8.1 Input Validation
- ✅ Integer overflow protected (positive int validation)
- ✅ Numeric validation for bbox coordinates
- ✅ String length implicit via parsing

### 8.2 SQL Injection Prevention
- ✅ Parameterized queries (via FeatureQuery object)
- ✅ CQL/XML parsing validated before execution
- ✅ Filter complexity validation available

### 8.3 XML Attack Prevention
- ✅ XXE (XML External Entity) protection: `SecureXmlSettings.ParseSecure()`
- ✅ Used in: XmlFilterParser, Transaction payload parsing

### 8.4 DoS Prevention
- ✅ Count limit enforcement (5000 absolute maximum)
- ✅ Layer-level MaxRecordCount
- ✅ Service-level ItemLimit
- ✅ Filter complexity scoring available (FilterComplexityScorer)

### 8.5 Authorization
- ✅ Transaction operations require "datapublisher" or "administrator" role
- ✅ Lock operations check authentication

---

## 9. File Locations Reference

| Component | File Path |
|-----------|-----------|
| Main Handler | `/src/Honua.Server.Host/Wfs/WfsHandlers.cs` |
| GetFeature Logic | `/src/Honua.Server.Host/Wfs/WfsGetFeatureHandlers.cs` |
| Helper Functions | `/src/Honua.Server.Host/Wfs/WfsHelpers.cs` |
| Constants & Types | `/src/Honua.Server.Host/Wfs/WfsSharedTypes.cs` |
| Query Utilities | `/src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs` |
| XML Filtering | `/src/Honua.Server.Host/Wfs/Filters/XmlFilterParser.cs` |
| Exception Helper | `/src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs` |
| Response Building | `/src/Honua.Server.Host/Wfs/WfsResponseBuilders.cs` |
| Lock Handling | `/src/Honua.Server.Host/Wfs/WfsLockHandlers.cs` |
| Transactions | `/src/Honua.Server.Host/Wfs/WfsTransactionHandlers.cs` |
| CQL Filter Parser | `/src/Honua.Server.Core/Query/Filter/CqlFilterParser.cs` |
| FeatureQuery Definition | `/src/Honua.Server.Core/Data/IDataStoreProvider.cs` (line 233) |
| Tests | `/tests/Honua.Server.Core.Tests/Hosting/WfsEndpointTests.cs` |

---

## 10. Testing Examples

From `/tests/Honua.Server.Core.Tests/Hosting/WfsEndpointTests.cs`:

### Paging Test (count and startIndex)
```csharp
[Fact]
public async Task GetFeature_ShouldApplyPaging()
{
    var response = await client.GetAsync(
        "/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary" +
        "&count=2&startIndex=1&outputFormat=application/geo+json");
    
    using var document = await JsonDocument.ParseAsync(
        await response.Content.ReadAsStreamAsync());
    
    document.RootElement.GetProperty("numberMatched").GetInt64().Should().Be(3);
    // Returns 2 features starting at index 1 (zero-based offset)
}
```

### Filter Test (cql_filter)
```csharp
[Fact]
public async Task GetFeature_Filter_ShouldReturnMatchingFeatures()
{
    var filter = Uri.EscapeDataString("status = 'open'");
    var response = await client.GetAsync(
        $"/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary" +
        $"&outputFormat=application/geo+json&filter={filter}");
    
    document.RootElement.GetProperty("numberMatched").GetInt64().Should().Be(2);
}
```

---

## 11. Summary of Key Implementation Details

### Parameter Parsing Strategy
1. **Query String First**: All parameters read from query collection first
2. **Fallback Defaults**: Service/layer configuration provides defaults
3. **Case-Insensitive**: Parameter names matched case-insensitively
4. **Multiple Names**: Support for WFS 1.x (typeName) and 2.0 (typeNames)
5. **Optional vs Required**: Clear distinction in validation logic

### Limit Enforcement (Three-Level)
1. **Requested limit** (from count parameter)
2. **Layer maximum** (from layer.Query.MaxRecordCount)
3. **Service maximum** (from service.Ogc.ItemLimit)
4. **Absolute maximum** (5000 - hardcoded)
→ Result: `Math.Min(requested, layer, service, 5000)`

### Error Handling Approach
- **Exceptions → ExceptionReport**: InvalidOperationException caught and converted
- **Result Type**: Method returns `Result<T>` with error details
- **OGC Compliant**: All errors wrapped in OWS ExceptionReport XML
- **Locator Attribute**: Points to problematic parameter

### Type System Benefits
- **FeatureQuery Record**: Immutable query representation passed to providers
- **FeatureContext**: Bundles resolved Service and Layer for context
- **BoundingBox Record**: Strongly-typed spatial filter
- **QueryFilter**: Parsed filter expression (CQL or XML)

