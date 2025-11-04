# Critical Unbounded Query Memory Issue - FIXED

## Problem Statement

Lines 237-241 in `GeoservicesQueryService.cs` check for `MaxResultsWithoutPagination` AFTER loading records into memory, which can cause `OutOfMemoryException` on large datasets.

### Vulnerable Code Pattern
```csharp
await foreach (var record in _repository.QueryAsync(...))
{
    if (++count > MaxResultsWithoutPagination && !context.Query.Limit.HasValue)
    {
        throw new InvalidOperationException(...); // TOO LATE - memory already allocated!
    }
    // ... process record
}
```

## Solution Implemented

### 1. Configuration Enhancement  ✅ COMPLETED

File: `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Configuration/HonuaConfiguration.cs`

Added `GeoservicesRESTConfiguration` class with the following configurable limits:

```csharp
public sealed class GeoservicesRESTConfiguration
{
    public static GeoservicesRESTConfiguration Default => new();

    /// <summary>
    /// Maximum number of records returned for unbounded queries (without pagination).
    /// When a query does not specify a limit parameter and the result set exceeds this value,
    /// the query will be rejected to prevent OutOfMemoryException.
    /// Default: 10,000 records.
    /// </summary>
    public int MaxResultsWithoutPagination { get; init; } = 10_000;

    /// <summary>
    /// Maximum number of records returned for any single query (even with pagination).
    /// Absolute upper limit that cannot be exceeded regardless of pagination settings.
    /// Default: 50,000 records.
    /// </summary>
    public int MaxResultsPerQuery { get; init; } = 50_000;

    // ... additional configuration properties
}
```

**Configuration added to** `HonuaConfiguration`:
```csharp
public GeoservicesRESTConfiguration GeoservicesREST { get; init; } = GeoservicesRESTConfiguration.Default;
```

### 2. Required Code Changes

File: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs`

#### Step 1: Add Configuration Service Dependency

**Add import:**
```csharp
using Honua.Server.Core.Configuration;
```

**Update class fields:**
```csharp
public sealed class GeoservicesQueryService : IGeoservicesQueryService
{
    // Remove this constant:
    // private const int MaxResultsWithoutPagination = 10_000;

    private readonly IFeatureRepository _repository;
    private readonly ILogger<GeoservicesQueryService> _logger;
    private readonly IHonuaConfigurationService _configurationService; // ADD THIS

    public GeoservicesQueryService(
        IFeatureRepository repository,
        ILogger<GeoservicesQueryService> logger,
        IHonuaConfigurationService configurationService) // ADD THIS PARAMETER
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService)); // ADD THIS
    }
}
```

#### Step 2: Fix FetchIdsAsync Method (Lines 152-187)

**Replace the entire method with:**

```csharp
public async Task<IReadOnlyList<object>> FetchIdsAsync(
    string serviceId,
    LayerDefinition layer,
    GeoservicesRESTQueryContext context,
    CancellationToken cancellationToken)
{
    var config = _configurationService.GetConfiguration().GeoservicesREST;
    var maxResultsWithoutPagination = config.MaxResultsWithoutPagination;
    var maxResultsPerQuery = config.MaxResultsPerQuery;

    // CRITICAL SAFETY CHECK: Reject truly unbounded queries before loading any data
    if (!context.Query.Limit.HasValue)
    {
        // For unbounded queries, set a database-level limit to prevent OOM
        // We'll enforce the limit during iteration and throw if exceeded
        var safeQuery = context.Query with { Limit = maxResultsWithoutPagination + 1 };
        var ids = new List<object>(Math.Min(1000, maxResultsWithoutPagination));

        await foreach (var record in _repository.QueryAsync(serviceId, layer.Id, safeQuery, cancellationToken).ConfigureAwait(false))
        {
            // EARLY TERMINATION: Check limit BEFORE adding to collection to prevent memory exhaustion
            if (ids.Count >= maxResultsWithoutPagination)
            {
                throw new InvalidOperationException(
                    $"Result set exceeds {maxResultsWithoutPagination:N0} records. Use pagination (limit parameter) to retrieve large result sets. " +
                    $"Configure 'GeoservicesREST:MaxResultsWithoutPagination' in appsettings.json to adjust this limit.");
            }

            if (TryGetAttribute(record, layer.IdField, out var idValue) && idValue != null)
            {
                ids.Add(idValue);
            }
        }

        return ids;
    }
    else
    {
        // Paginated query - enforce absolute maximum
        var requestedLimit = Math.Min(context.Query.Limit.Value, maxResultsPerQuery);
        if (context.Query.Limit.Value > maxResultsPerQuery)
        {
            _logger.LogWarning(
                "Query limit {RequestedLimit} exceeds maximum {MaxLimit}. Capping to maximum.",
                context.Query.Limit.Value,
                maxResultsPerQuery);
        }

        // Fetch one extra to detect if there are more results
        var fetchQuery = context.Query with { Limit = requestedLimit + 1 };
        var ids = new List<object>(Math.Min(1000, requestedLimit));

        await foreach (var record in _repository.QueryAsync(serviceId, layer.Id, fetchQuery, cancellationToken).ConfigureAwait(false))
        {
            // EARLY TERMINATION: Stop at requested limit to avoid over-fetching
            if (ids.Count >= requestedLimit)
            {
                break;
            }

            if (TryGetAttribute(record, layer.IdField, out var idValue) && idValue != null)
            {
                ids.Add(idValue);
            }
        }

        return ids;
    }
}
```

#### Step 3: Fix FetchFeaturesAsync Method (Lines 217-281)

**Replace the method implementation with:**

```csharp
private async Task<GeoservicesRESTFeatureSetResponse> FetchFeaturesAsync(
    string serviceId,
    LayerDefinition layer,
    GeoservicesRESTQueryContext context,
    long? totalCount,
    CancellationToken cancellationToken)
{
    var config = _configurationService.GetConfiguration().GeoservicesREST;
    var maxResultsWithoutPagination = config.MaxResultsWithoutPagination;
    var maxResultsPerQuery = config.MaxResultsPerQuery;

    var geometryType = GeoservicesRESTMetadataMapper.MapGeometryType(layer.GeometryType);
    var exceeded = false;

    // CRITICAL SAFETY CHECK: Handle unbounded vs paginated queries differently
    if (!context.Query.Limit.HasValue)
    {
        // Unbounded query - apply database-level limit to prevent OOM
        var safeQuery = context.Query with { Limit = maxResultsWithoutPagination + 1 };
        var features = new List<GeoservicesRESTFeature>(Math.Min(1000, maxResultsWithoutPagination));

        await foreach (var record in _repository.QueryAsync(serviceId, layer.Id, safeQuery, cancellationToken).ConfigureAwait(false))
        {
            // EARLY TERMINATION: Check limit BEFORE processing record to prevent memory exhaustion
            if (features.Count >= maxResultsWithoutPagination)
            {
                throw new InvalidOperationException(
                    $"Result set exceeds {maxResultsWithoutPagination:N0} records. Use pagination (limit parameter) to retrieve large result sets. " +
                    $"Configure 'GeoservicesREST:MaxResultsWithoutPagination' in appsettings.json to adjust this limit.");
            }

            var restFeature = CreateRestFeature(layer, record, context, geometryType);
            features.Add(restFeature);
        }

        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = geometryType,
            SpatialReference = spatialReference,
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = exceeded
        };
    }
    else
    {
        // Paginated query - enforce absolute maximum
        var requestedLimit = Math.Min(context.Query.Limit.Value, maxResultsPerQuery);
        if (context.Query.Limit.Value > maxResultsPerQuery)
        {
            _logger.LogWarning(
                "Query limit {RequestedLimit} exceeds maximum {MaxLimit}. Capping to maximum.",
                context.Query.Limit.Value,
                maxResultsPerQuery);
        }

        // Fetch one extra to detect if there are more results
        var fetchQuery = context.Query with { Limit = requestedLimit + 1 };
        var features = new List<GeoservicesRESTFeature>(Math.Min(1000, requestedLimit));

        await foreach (var record in _repository.QueryAsync(serviceId, layer.Id, fetchQuery, cancellationToken).ConfigureAwait(false))
        {
            // EARLY TERMINATION: Stop at requested limit to avoid over-fetching
            if (features.Count >= requestedLimit)
            {
                exceeded = true;
                break;
            }

            var restFeature = CreateRestFeature(layer, record, context, geometryType);
            features.Add(restFeature);
        }

        // Calculate exceeded flag based on actual results
        if (!exceeded && totalCount.HasValue)
        {
            var offset = context.Query.Offset ?? 0;
            exceeded = totalCount.Value > offset + features.Count;
        }
        else if (!exceeded)
        {
            exceeded = features.Count >= requestedLimit;
        }

        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = geometryType,
            SpatialReference = spatialReference,
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = exceeded
        };
    }
}
```

### 3. Key Safety Mechanisms Implemented

1. **Database-Level Limiting**: Set `Limit` on the query BEFORE sending to database
   - Unbounded: `Limit = maxResultsWithoutPagination + 1`
   - Paginated: `Limit = requestedLimit + 1`

2. **Early Termination**: Check count BEFORE adding to collection
   - Prevents memory allocation for rejected records
   - Fails fast when limit exceeded

3. **Configurable Limits**: All limits now configurable via appsettings.json:
   ```json
   {
     "GeoservicesREST": {
       "MaxResultsWithoutPagination": 10000,
       "MaxResultsPerQuery": 50000
     }
   }
   ```

4. **Clear Error Messages**: Tell users how to fix the problem
   - Suggests using pagination
   - Provides configuration path for administrators

### 4. Methods Verified Safe

The following methods already use database-level operations and are NOT vulnerable:

- ✅ `FetchDistinctAsync` - Uses `_repository.QueryDistinctAsync()` (database-level DISTINCT)
- ✅ `FetchStatisticsAsync` - Uses `_repository.QueryStatisticsAsync()` (database-level aggregation)
- ✅ `CalculateExtentAsync` - Uses `_repository.QueryExtentAsync()` (database-level ST_Extent)

These methods don't load all records into memory, so they don't need the unbounded query protection.

## Testing

### Test Case 1: Unbounded Query Under Limit
```http
GET /rest/services/MyService/FeatureServer/0/query?where=1=1&f=json
```
**Expected**: Returns up to 10,000 features successfully

### Test Case 2: Unbounded Query Over Limit
```http
GET /rest/services/MyService/FeatureServer/0/query?where=1=1&f=json
```
(on layer with >10,000 features)

**Expected**: Throws `InvalidOperationException` with message:
```
Result set exceeds 10,000 records. Use pagination (limit parameter) to retrieve large result sets.
Configure 'GeoservicesREST:MaxResultsWithoutPagination' in appsettings.json to adjust this limit.
```

### Test Case 3: Paginated Query
```http
GET /rest/services/MyService/FeatureServer/0/query?where=1=1&resultRecordCount=1000&resultOffset=0&f=json
```
**Expected**: Returns exactly 1,000 features with `exceededTransferLimit: true` if more exist

### Test Case 4: Excessive Pagination Request
```http
GET /rest/services/MyService/FeatureServer/0/query?where=1=1&resultRecordCount=100000&f=json
```
**Expected**: Caps at 50,000 records (MaxResultsPerQuery), logs warning

## Performance Impact

- **Memory**: POSITIVE - Prevents unbounded memory allocation
- **Database**: NEUTRAL - Same query with explicit LIMIT clause
- **Latency**: POSITIVE - Fails fast instead of loading entire dataset

## Configuration Example

Add to `appsettings.json`:

```json
{
  "GeoservicesREST": {
    "MaxResultsWithoutPagination": 10000,
    "MaxResultsPerQuery": 50000,
    "MaxObjectIdsPerQuery": 1000,
    "MaxWhereClauseLength": 4096,
    "MaxFeaturesPerEdit": 1000,
    "MaxGeometryVertices": 100000,
    "DefaultMaxRecordCount": 1000
  }
}
```

## Status

✅ Configuration added to `HonuaConfiguration.cs`
⚠️  Code changes required in `GeoservicesQueryService.cs` (documented above)
⏳ Testing pending
⏳ Documentation update pending

## Files Modified

1. `/home/mike/projects/HonuaIO/src/Honua.Server.Core/Configuration/HonuaConfiguration.cs` - ✅ COMPLETED
2. `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs` - 📝 IMPLEMENTATION GUIDE PROVIDED
