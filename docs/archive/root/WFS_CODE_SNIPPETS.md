# WFS Query Parameter Parsing - Key Code Snippets

## 1. Query Parameter Parsing Core Functions

### Location: `/src/Honua.Server.Host/Utilities/QueryParsingHelpers.cs`

#### Get Query Value
```csharp
public static string? GetQueryValue(IQueryCollection query, string key)
{
    ArgumentNullException.ThrowIfNull(query);

    if (!query.TryGetValue(key, out var values) || values.Count == 0)
    {
        return null;
    }
    return values[^1];  // Get last value if multiple
}
```

#### Parse Bounding Box with CRS
```csharp
public static (BoundingBoxWithCrs? Value, IResult? Error) ParseBoundingBoxWithCrs(
    string? raw, string parameterName = "bbox", bool allowAltitude = false)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return (null, null);
    }

    var tokens = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    
    // Check if last token is CRS (non-numeric)
    string? crs = null;
    if (tokens.Count > 0 && !double.TryParse(tokens[^1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
    {
        crs = tokens[^1];
        tokens.RemoveAt(tokens.Count - 1);
    }

    var numericPortion = string.Join(',', tokens);
    var (bbox, error) = ParseBoundingBox(numericPortion, parameterName, allowAltitude);
    if (bbox is null)
    {
        return (null, error);
    }

    return (new BoundingBoxWithCrs(bbox, crs), null);
}
```

#### Parse Positive Integer
```csharp
public static (int? Value, IResult? Error) ParsePositiveInt(
    IQueryCollection query,
    string key,
    bool required = false,
    int? defaultValue = null,
    bool allowZero = false,
    string? errorDetail = null)
{
    ArgumentNullException.ThrowIfNull(query);

    if (!query.TryGetValue(key, out var values) || values.Count == 0)
    {
        if (defaultValue.HasValue)
        {
            return (defaultValue.Value, null);
        }

        if (!required)
        {
            return (null, null);
        }

        return (null, BuildIntegerProblem(key, allowZero, errorDetail));
    }

    var raw = values[^1];
    if (string.IsNullOrWhiteSpace(raw))
    {
        if (defaultValue.HasValue)
        {
            return (defaultValue.Value, null);
        }

        if (!required)
        {
            return (null, null);
        }

        return (null, BuildIntegerProblem(key, allowZero, errorDetail));
    }

    if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
        parsed < 0 || (!allowZero && parsed == 0))
    {
        return (null, BuildIntegerProblem(key, allowZero, errorDetail));
    }

    return (parsed, null);
}
```

---

## 2. WFS-Specific Parameter Parsing

### Location: `/src/Honua.Server.Host/Wfs/WfsHelpers.cs`

#### Parse Integer (WFS wrapper)
```csharp
public static int ParseInt(IQueryCollection query, string key, int defaultValue, bool allowZero)
{
    var (value, error) = QueryParsingHelpers.ParsePositiveInt(
        query,
        key,
        required: false,
        defaultValue: defaultValue,
        allowZero: allowZero,
        errorDetail: $"Parameter '{key}' must be a positive integer.");

    if (error is not null)
    {
        var message = QueryParsingHelpers.ExtractProblemMessage(error, 
            $"Parameter '{key}' must be a positive integer.");
        throw new InvalidOperationException(message);
    }

    return value ?? defaultValue;
}
```

#### Enforce Count Limit (Multi-level)
```csharp
public static int EnforceCountLimit(int requestedCount, LayerDefinition layer, ServiceDefinition service)
{
    const int SafeMaximum = 5000; // Absolute maximum to prevent DoS

    var effectiveLimit = requestedCount;

    // Apply layer-specific limit if configured
    if (layer.Query?.MaxRecordCount is { } layerMax)
    {
        effectiveLimit = Math.Min(effectiveLimit, layerMax);
    }

    // Apply service-level limit if configured
    if (service.Ogc?.ItemLimit is { } serviceLimit)
    {
        effectiveLimit = Math.Min(effectiveLimit, serviceLimit);
    }

    // Always enforce safe maximum
    effectiveLimit = Math.Min(effectiveLimit, SafeMaximum);

    return effectiveLimit;
}
```

#### Parse Bounding Box (WFS-specific)
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

#### Parse Result Type (hits vs results)
```csharp
public static FeatureResultType ParseResultType(IQueryCollection query)
{
    var value = QueryParsingHelpers.GetQueryValue(query, "resultType");
    return string.Equals(value, "hits", StringComparison.OrdinalIgnoreCase)
        ? FeatureResultType.Hits
        : FeatureResultType.Results;
}
```

#### Parse Lock Duration
```csharp
public static TimeSpan ParseLockDuration(IQueryCollection query)
{
    var expiry = QueryParsingHelpers.GetQueryValue(query, "expiry") 
        ?? QueryParsingHelpers.GetQueryValue(query, "EXPIRY");
    if (string.IsNullOrWhiteSpace(expiry))
    {
        return WfsConstants.DefaultLockDuration;
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

#### Normalize Output Format
```csharp
public static bool TryNormalizeOutputFormat(string? format, out string normalized)
{
    if (string.IsNullOrWhiteSpace(format))
    {
        normalized = WfsConstants.GmlFormat;
        return true;
    }

    var candidate = format.Trim();
    candidate = candidate.Replace(' ', '+');
    if (candidate.StartsWith(WfsConstants.GeoJsonFormat, StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("json", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("application/json", StringComparison.OrdinalIgnoreCase))
    {
        normalized = WfsConstants.GeoJsonFormat;
        return true;
    }

    if (candidate.StartsWith("application/gml+xml", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("gml32", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("gml", StringComparison.OrdinalIgnoreCase))
    {
        normalized = WfsConstants.GmlFormat;
        return true;
    }

    if (candidate.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("csv", StringComparison.OrdinalIgnoreCase))
    {
        normalized = WfsConstants.CsvFormat;
        return true;
    }

    if (candidate.StartsWith("application/x-shapefile", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("shapefile", StringComparison.OrdinalIgnoreCase) ||
        candidate.Equals("shape", StringComparison.OrdinalIgnoreCase))
    {
        normalized = WfsConstants.ShapefileFormat;
        return true;
    }

    normalized = string.Empty;
    return false;
}
```

#### Build Filter from Request
```csharp
public static async Task<QueryFilter?> BuildFilterAsync(
    HttpRequest request, IQueryCollection query, LayerDefinition layer, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(query);
    ArgumentNullException.ThrowIfNull(layer);

    // Try query parameter filters
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

    // Try POST body for XML filters
    if (!HttpMethods.IsPost(request.Method))
    {
        return null;
    }

    request.EnableBuffering();
    request.Body.Seek(0, SeekOrigin.Begin);
    using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    request.Body.Seek(0, SeekOrigin.Begin);

    if (string.IsNullOrWhiteSpace(body))
    {
        return null;
    }

    return ParseXmlFilter(body, layer);
}

private static QueryFilter? TryParseFilter(string? candidate, LayerDefinition layer)
{
    if (string.IsNullOrWhiteSpace(candidate))
    {
        return null;
    }

    var trimmed = candidate.Trim();
    return trimmed.StartsWith("<", StringComparison.Ordinal)
        ? ParseXmlFilter(trimmed, layer)
        : ParseCqlFilter(trimmed, layer);
}

private static QueryFilter ParseCqlFilter(string text, LayerDefinition layer)
    => CqlFilterParser.Parse(text, layer);

private static QueryFilter ParseXmlFilter(string text, LayerDefinition layer)
    => XmlFilterParser.Parse(text, layer);
```

#### Resolve Layer Context
```csharp
public static async Task<Result<FeatureContext>> ResolveLayerContextAsync(
    string typeNamesRaw,
    ICatalogProjectionService catalog,
    IFeatureContextResolver resolver,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(typeNamesRaw))
    {
        return Result<FeatureContext>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
    }

    var typeName = typeNamesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    if (string.IsNullOrWhiteSpace(typeName))
    {
        return Result<FeatureContext>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
    }

    string? serviceId = null;
    string layerId;

    var parts = typeName.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 2)
    {
        serviceId = parts[0];
        layerId = parts[1];
    }
    else
    {
        layerId = parts[0];
        var projection = catalog.GetSnapshot();
        foreach (var service in projection.ServiceIndex.Values)
        {
            if (service.Layers.Any(l => string.Equals(l.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase)))
            {
                serviceId = service.Service.Id;
                break;
            }
        }
    }

    if (string.IsNullOrWhiteSpace(serviceId))
    {
        return Result<FeatureContext>.Failure(Error.NotFound($"Layer '{typeName}' was not found."));
    }

    try
    {
        var context = await resolver.ResolveAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        return Result<FeatureContext>.Success(context);
    }
    catch (KeyNotFoundException ex)
    {
        return Result<FeatureContext>.Failure(Error.NotFound(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Result<FeatureContext>.Failure(Error.Invalid(ex.Message));
    }
    catch (NotSupportedException ex)
    {
        return Result<FeatureContext>.Failure(Error.Invalid(ex.Message));
    }
}
```

#### CRS to URN Conversion
```csharp
public static string ToUrn(string crs)
{
    if (string.IsNullOrWhiteSpace(crs))
    {
        return "urn:ogc:def:crs:EPSG::4326";
    }

    if (crs.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
    {
        return crs;
    }

    if (crs.StartsWith("EPSG", StringComparison.OrdinalIgnoreCase))
    {
        var code = crs.Split(':').LastOrDefault() ?? "4326";
        return $"urn:ogc:def:crs:EPSG::{code}";
    }

    return crs;
}
```

---

## 3. GetFeature Query Building

### Location: `/src/Honua.Server.Host/Wfs/WfsGetFeatureHandlers.cs`

#### Build Feature Query Execution
```csharp
private static async Task<Result<FeatureQueryExecution>> BuildFeatureQueryExecutionAsync(
    HttpRequest request,
    IQueryCollection query,
    ICatalogProjectionService catalog,
    IFeatureContextResolver contextResolver,
    IMetadataRegistry registry,
    CancellationToken cancellationToken)
{
    // Check for stored query execution
    var storedQueryId = QueryParsingHelpers.GetQueryValue(query, "storedQuery_Id") 
        ?? QueryParsingHelpers.GetQueryValue(query, "STOREDQUERY_ID");
    if (!string.IsNullOrWhiteSpace(storedQueryId))
    {
        return await BuildStoredQueryExecutionAsync(request, query, storedQueryId, catalog, contextResolver, registry, cancellationToken);
    }

    // Parse standard GetFeature parameters
    var typeNamesRaw = QueryParsingHelpers.GetQueryValue(query, "typeNames") 
        ?? QueryParsingHelpers.GetQueryValue(query, "typeName");
    if (string.IsNullOrWhiteSpace(typeNamesRaw))
    {
        return Result<FeatureQueryExecution>.Failure(Error.Invalid("Parameter 'typeNames' is required."));
    }

    var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeNamesRaw, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
    if (contextResult.IsFailure)
    {
        return Result<FeatureQueryExecution>.Failure(contextResult.Error!);
    }

    var context = contextResult.Value;
    var service = context.Service;
    var layer = context.Layer;

    // Parse pagination
    var count = WfsHelpers.ParseInt(query, "count", WfsConstants.DefaultCount, allowZero: false);
    // WFS COMPLIANCE: Enforce configured limits to prevent unbounded scans
    count = WfsHelpers.EnforceCountLimit(count, layer, service);
    var startIndex = WfsHelpers.ParseInt(query, "startIndex", 0, allowZero: true);

    // Parse CRS
    var srsName = QueryParsingHelpers.GetQueryValue(query, "srsName");
    
    // Parse spatial filter
    var bbox = WfsHelpers.ParseBoundingBox(query);
    
    // Parse attribute filter
    var filter = await WfsHelpers.BuildFilterAsync(request, query, layer, cancellationToken).ConfigureAwait(false);
    
    // Parse result type
    var resultType = WfsHelpers.ParseResultType(query);
    
    // Parse output format
    var outputFormatRaw = QueryParsingHelpers.GetQueryValue(query, "outputFormat");
    if (!WfsHelpers.TryNormalizeOutputFormat(outputFormatRaw, out var outputFormat))
    {
        return Result<FeatureQueryExecution>.Failure(
            Error.Invalid($"Output format '{outputFormatRaw}' is not supported."));
    }

    var requestedCrs = !string.IsNullOrWhiteSpace(srsName) ? srsName : service.Ogc.DefaultCrs;
    if (string.IsNullOrWhiteSpace(requestedCrs))
    {
        requestedCrs = "EPSG:4326";
    }

    var srid = CrsHelper.ParseCrs(requestedCrs);
    var urnCrs = WfsHelpers.ToUrn(requestedCrs);

    // Build immutable query object
    var resultQuery = new FeatureQuery(
        Limit: count,
        Offset: startIndex,
        Bbox: bbox,
        Filter: filter,
        ResultType: resultType,
        Crs: requestedCrs);

    var countQuery = resultQuery with { Limit = null, Offset = null, ResultType = FeatureResultType.Hits };

    return Result<FeatureQueryExecution>.Success(
        new FeatureQueryExecution(context, resultQuery, countQuery, resultType, outputFormat, urnCrs, srid, requestedCrs));
}
```

#### Execute Feature Query
```csharp
public static async Task<Result<FeatureQueryExecutionResult>> ExecuteFeatureQueryAsync(
    HttpRequest request,
    IQueryCollection query,
    ICatalogProjectionService catalog,
    IFeatureContextResolver contextResolver,
    IFeatureRepository repository,
    IMetadataRegistry registry,
    CancellationToken cancellationToken)
{
    var buildResult = await BuildFeatureQueryExecutionAsync(request, query, catalog, contextResolver, registry, cancellationToken).ConfigureAwait(false);
    if (buildResult.IsFailure)
    {
        return Result<FeatureQueryExecutionResult>.Failure(buildResult.Error!);
    }

    var execution = buildResult.Value;
    var context = execution.Context;
    var numberMatched = await repository.CountAsync(context.Service.Id, context.Layer.Id, execution.CountQuery, cancellationToken).ConfigureAwait(false);

    var features = new List<WfsFeature>();
    if (execution.ResultQuery.ResultType == FeatureResultType.Results)
    {
        await foreach (var record in repository.QueryAsync(context.Service.Id, context.Layer.Id, execution.ResultQuery, cancellationToken).ConfigureAwait(false))
        {
            var geometry = WfsHelpers.TryReadGeometry(context.Layer, record, execution.Srid);
            features.Add(new WfsFeature(record, geometry));
        }
    }

    return Result<FeatureQueryExecutionResult>.Success(
        new FeatureQueryExecutionResult(execution, numberMatched, features));
}
```

---

## 4. Exception Handling

### Location: `/src/Honua.Server.Host/Ogc/OgcExceptionHelper.cs`

#### Create WFS Exception Report
```csharp
public static IResult CreateWfsException(
    string code, 
    string? locator, 
    string message, 
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

#### Map Execution Errors
```csharp
public static IResult MapExecutionError(Error error, IQueryCollection query)
{
    if (error is null)
    {
        return CreateException("NoApplicableCode", "request", "Request could not be processed.");
    }

    return error.Code switch
    {
        "not_found" => CreateException("InvalidParameterValue", "typeNames", 
            error.Message ?? "Requested layer was not found."),
        "invalid" => CreateException("InvalidParameterValue", "request", 
            error.Message ?? "Request is not valid."),
        _ => CreateException("NoApplicableCode", "request", 
            error.Message ?? "Request could not be processed.")
    };
}
```

---

## 5. XML Filter Parsing (Security)

### Location: `/src/Honua.Server.Host/Wfs/Filters/XmlFilterParser.cs`

#### Parse XML Filter with XXE Protection
```csharp
public static QueryFilter Parse(string xml, LayerDefinition layer)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(xml);
    ArgumentNullException.ThrowIfNull(layer);

    try
    {
        // Use secure XML parsing to prevent XXE attacks
        var document = SecureXmlSettings.ParseSecure(xml, LoadOptions.PreserveWhitespace);
        var filterElement = document.Root?.Name.LocalName == "Filter"
            ? document.Root
            : document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Filter");

        if (filterElement is null)
        {
            throw new InvalidOperationException("XML filter is missing the required <Filter> element.");
        }

        var firstChild = filterElement.Elements().FirstOrDefault();
        if (firstChild is null)
        {
            throw new InvalidOperationException("XML filter does not contain any filter expressions.");
        }

        var expression = ParseNode(firstChild, layer);
        return new QueryFilter(expression);
    }
    catch (XmlException ex)
    {
        throw new InvalidOperationException("XML filter is not well-formed.", ex);
    }
}
```

---

## 6. FeatureQuery Data Structure

### Location: `/src/Honua.Server.Core/Data/IDataStoreProvider.cs`

```csharp
public sealed record FeatureQuery(
    int? Limit = null,
    int? Offset = null,
    BoundingBox? Bbox = null,
    TemporalInterval? Temporal = null,
    FeatureResultType ResultType = FeatureResultType.Results,
    IReadOnlyList<string>? PropertyNames = null,
    IReadOnlyList<FeatureSortOrder>? SortOrders = null,
    QueryFilter? Filter = null,
    QueryEntityDefinition? EntityDefinition = null,
    string? Crs = null,
    TimeSpan? CommandTimeout = null);

public sealed record BoundingBox(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double? MinZ = null,
    double? MaxZ = null,
    string? Crs = null);

public sealed record FeatureSortOrder(string Field, FeatureSortDirection Direction);
```

---

## 7. Constants

### Location: `/src/Honua.Server.Host/Wfs/WfsSharedTypes.cs`

```csharp
internal static class WfsConstants
{
    // XML Namespaces
    public static readonly XNamespace Wfs = "http://www.opengis.net/wfs/2.0";
    public static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";
    public static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
    public static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
    public static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    public static readonly XNamespace Fes = "http://www.opengis.net/fes/2.0";

    // Format constants
    public const string GeoJsonFormat = "application/geo+json";
    public const string GmlFormat = "application/gml+xml; version=3.2";
    public const string CsvFormat = "text/csv";
    public const string ShapefileFormat = "application/x-shapefile";

    // Default values
    public const int DefaultCount = 100;
    public static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);

    // Shared GeoJSON reader
    public static readonly GeoJsonReader GeoJsonReader = new();
}
```

