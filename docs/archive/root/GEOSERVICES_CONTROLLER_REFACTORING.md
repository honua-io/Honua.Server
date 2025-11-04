# GeoservicesRESTFeatureServerController Refactoring Plan

## Problem Statement

The `GeoservicesRESTFeatureServerController.cs` is a critical god class with **3,562 lines** and **113 methods**, violating the Single Responsibility Principle and making it difficult to test and maintain.

## Current State

**File**: `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`
- **Lines**: 3,562
- **Methods**: 113
- **Dependencies**: 10 injected services
- **Responsibilities**: Query, Editing, Attachments, Export, Metadata

## Target Architecture

### Service Distribution (Based on Automated Analysis)

| Service | Methods | Est. Lines | Status |
|---------|---------|------------|--------|
| **GeoservicesMetadataService** | 7 | ~150 | COMPLETE |
| **GeoservicesAttachmentService** | 12 | ~500 | Interfaces Created |
| **GeoservicesEditingService** | 27 | ~1,000 | Interfaces Created |
| **GeoservicesExportService** | 11 | ~350 | Interfaces Created |
| **GeoservicesQueryService** | 55 | ~1,800 | Interfaces Created |
| **Shared Utilities** | 1 | ~15 | TBD |
| **Refactored Controller** | - | ~250 | Pending |

### Total Reduction
- **Before**: 3,562 lines (1 file)
- **After**: ~250 lines (controller) + 5 focused services (~3,815 lines across 6 files)
- **Controller Reduction**: 93% (3,562 → 250 lines)

## Completed Work

### 1. Service Interfaces Created

Located in `src/Honua.Server.Host/GeoservicesREST/Services/`:

- **IGeoservicesMetadataService.cs** - Service/layer metadata operations
- **IGeoservicesQueryService.cs** - Feature query operations
- **IGeoservicesEditingService.cs** - Add/update/delete operations
- **IGeoservicesAttachmentService.cs** - Attachment management
- **IGeoservicesExportService.cs** - Shapefile/KML/CSV export

### 2. Complete Implementation

- **GeoservicesMetadataService.cs** - FULLY IMPLEMENTED (proof of concept)
  - 7 methods extracted
  - ~150 lines
  - Independently testable
  - Follows repository pattern

### 3. Supporting Models

- **GeoservicesEditExecutionResult.cs** - Edit operation result model

## Implementation Guide

### Method Extraction Map

#### Metadata Service (COMPLETED)
```
Lines 87-97: GetService → GetServiceSummary
Lines 100-117: GetLayer → GetLayerDetailAsync
Lines 2693-2713: IsVisibleAtScale
Lines 3460-3479: ResolveDefaultStyleAsync
Lines 3481-3500: ResolveService
Lines 3502-3510: ResolveLayer
Lines 3512-3516: SupportsFeatureServer
```

#### Attachment Service (TODO)
```
Lines 552-624: QueryAttachmentsAsync
Lines 627-630: QueryAttachmentsPostAsync
Lines 635-697: AddAttachmentAsync
Lines 702-778: UpdateAttachmentAsync
Lines 782-865: DeleteAttachmentsAsync
Lines 868-909: DownloadAttachmentAsync
Lines 911-920: CreateMutationSuccess
Lines 922-930: CreateMutationFailure
Lines 932-949: MapError
Lines 976-980: GetUserIdentifier
Lines 982-1007: BuildAttachmentUrl
Lines 1371-1386: ExtractUserRoles
```

#### Editing Service (TODO)
```
Lines 337-393: ApplyEditsAsync
Lines 397-446: AddFeaturesAsync
Lines 450-499: UpdateFeaturesAsync
Lines 503-549: DeleteFeaturesAsync
Lines 1008-1128: ExecuteEditsAsync
Lines 1147-1181: PopulateAddCommands
Lines 1183-1221: PopulateUpdateCommands
Lines 1223-1261: PopulateDeleteCommands
Lines 1277-1289: TryGetDeleteElement
Lines 1291-1308: ParseDeleteIdsFromQuery
Lines 1310-1328: ResolveReturnEditMoment
Lines 1330-1348: ResolveUseGlobalIds
Lines 1388-1411: AppendEditResult
Lines 1413-1445: BuildEditResult
Lines 1494-1530: NormalizeGlobalIdCommandsAsync
Lines 1532-1586: ResolveObjectIdByGlobalIdAsync
Lines 1588-1625: GeoservicesEditExecutionResult (moved to separate file)
Lines 1627-1649: AttachGeometry
Lines 1651-1683: ResolveFeatureIdForUpdate
Lines 1685-1723: ParseDeleteIds (2 overloads)
Lines 1725-1739: NormalizeDeleteIdentifier
Lines 1741-1751: InterpretBoolean
Lines 1753-1766: TryExtractId
Lines 1768-1781: TryExtractGlobalId
Lines 1811-1830: NormalizeGlobalIdValue
```

#### Export Service (TODO)
```
Lines 2649-2691: WriteKmlAsync
Lines 2750-2771: ExportEmptyShapefileAsync
Lines 2773-2801: ExportEmptyKmlAsync
Lines 2946-2951: BuildKmlFileName
Lines 2953-2957: BuildKmlEntryName
Lines 2958-2981: ExportShapefileAsync
Lines 2983-3029: ExportKmlAsync
Lines 3415-3418: IsKmlFormat
Lines 3443-3458: SanitizeFileSegment
Lines 3518-3539: ExportCsvAsync
Lines 3541-3560: ExportEmptyCsvAsync
```

#### Query Service (TODO - LARGEST)
```
Lines 120-315: QueryAsync (main entry point)
Lines 318-321: QueryPostAsync
Lines 324-327: QueryRelatedRecordsGetAsync
Lines 330-333: QueryRelatedRecordsPostAsync
Lines 951-969: ParseIntList
Lines 971-974: TryParseObjectId
Lines 1130-1145: ParsePayloadAsync
Lines 1263-1275: TryGetArrayProperty
Lines 1350-1369: ResolveRollbackPreference
Lines 1447-1471: CreateErrorPayload
Lines 1783-1797: ConvertJsonElementToString
Lines 1799-1809: ConvertToInvariantString
Lines 1832-1877: FetchFeaturesAsync
Lines 1879-2009: QueryRelatedRecordsInternalAsync
Lines 2011-2061: FetchDistinctAsync
Lines 2063-2121: FetchStatisticsAsync
Lines 2123-2144: ResolveDistinctFields
Lines 2146-2156: BuildFieldLookup
Lines 2158-2174: BuildDistinctAttributes
Lines 2176-2201: BuildDistinctFieldDefinitions
Lines 2203-2225: BuildGroupAttributes
Lines 2227-2230: CloneAccumulators
Lines 2232-2283: BuildStatisticsFieldDefinitions
Lines 2285-2304: ResolveStatisticFieldType
Lines 2306-2324: TryGetAttribute
Lines 2326-2341: EscapeKeyComponent
Lines 2343-2390: TryConvertToDouble
Lines 2392-2428: CompareValues
Lines 2430-2454: ConvertToComparableType
Lines 2456-2567: StatisticsResult (nested class)
Lines 2498-2567: StatisticsAccumulator (nested class)
Lines 2571-2621: WriteGeoJsonAsync
Lines 2623-2647: WriteTopoJsonAsync
Lines 2715-2730: CreateEmptyStatisticsFeatureSet
Lines 2732-2748: CreateEmptyDistinctFeatureSet
Lines 2803-2863: BuildScaleSuppressedResponseAsync
Lines 2865-2880: CreateEmptyFeatureSetResponse
Lines 2882-2891: EmptyFeatureRecords
Lines 2893-2908: CreateEmptyGeoJsonResponse
Lines 2910-2923: CreateEmptyTopoJsonResponse
Lines 2941-2944: BuildCollectionIdentifier
Lines 3031-3039: TryGetAttributeValue
Lines 3042-3105: CalculateExtentAsync
Lines 3107-3127: FetchIdsAsync
Lines 3129-3158: CreateRestFeature
Lines 3160-3190: CreateRelatedQueryRequest
Lines 3192-3213: ParseObjectIdValues
Lines 3215-3251: ConvertObjectIdToken
Lines 3253-3270: BuildObjectIdFilterExpression
Lines 3272-3285: CombineFilters
Lines 3287-3331: BuildRelatedRecordGroups
Lines 3333-3353: FilterFieldsForSelection
Lines 3357-3364: IsRelationalService
Lines 3366-3379: IsRelationalProvider
Lines 3381-3397: ConvertAttributeValue
Lines 3399-3413: ConvertJsonElement
Lines 3420-3423: CreateCollectionIdentifier
```

### Shared Utilities
```
Lines 2925-2939: CreateJsonResult
Lines 3425-3439: WriteJson
```

## Step-by-Step Implementation Plan

### Phase 1: Extract Remaining Services (Priority Order)

1. **GeoservicesAttachmentService** (12 methods, ~500 lines)
   - Medium complexity
   - Well-isolated functionality
   - Recommended next step

2. **GeoservicesExportService** (11 methods, ~350 lines)
   - Low complexity
   - File generation logic
   - Good candidate for early extraction

3. **GeoservicesEditingService** (27 methods, ~1,000 lines)
   - High complexity
   - Many helper methods
   - Complex transaction logic

4. **GeoservicesQueryService** (55 methods, ~1,800 lines)
   - HIGHEST complexity
   - Most methods
   - Nested classes (StatisticsResult, StatisticsAccumulator)
   - Save for last

### Phase 2: Refactor Controller

After all services are extracted, the controller should:

1. **Inject all services** via constructor
2. **Delegate to services** from endpoints
3. **Handle only HTTP concerns**:
   - Route parameter extraction
   - Authorization (already handled by attributes)
   - Service resolution (folder/service/layer lookup)
   - Response formatting

**Target Controller Structure** (~250 lines):
```csharp
[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services/{folderId}/{serviceId}/FeatureServer")]
public sealed class GeoservicesRESTFeatureServerController : ControllerBase
{
    private readonly IGeoservicesMetadataService _metadataService;
    private readonly IGeoservicesQueryService _queryService;
    private readonly IGeoservicesEditingService _editingService;
    private readonly IGeoservicesAttachmentService _attachmentService;
    private readonly IGeoservicesExportService _exportService;
    private readonly ILogger<GeoservicesRESTFeatureServerController> _logger;

    // Constructor with all services...

    // Metadata endpoints
    [HttpGet]
    public ActionResult<GeoservicesRESTFeatureServiceSummary> GetService(string folderId, string serviceId)
    {
        var serviceView = _metadataService.ResolveService(folderId, serviceId);
        if (serviceView is null) return NotFound();
        return _metadataService.GetServiceSummary(serviceView);
    }

    [HttpGet("{layerIndex:int}")]
    public async Task<ActionResult<GeoservicesRESTLayerDetailResponse>> GetLayer(...)
    {
        var serviceView = _metadataService.ResolveService(folderId, serviceId);
        if (serviceView is null) return NotFound();
        var layerView = _metadataService.ResolveLayer(serviceView, layerIndex);
        if (layerView is null) return NotFound();
        return await _metadataService.GetLayerDetailAsync(serviceView, layerView, layerIndex, cancellationToken);
    }

    // Query endpoints
    [HttpGet("{layerIndex:int}/query")]
    public async Task<IActionResult> QueryAsync(...)
    {
        var serviceView = _metadataService.ResolveService(folderId, serviceId);
        if (serviceView is null) return NotFound();
        var layerView = _metadataService.ResolveLayer(serviceView, layerIndex);
        if (layerView is null) return NotFound();

        if (!GeoservicesRESTQueryTranslator.TryParse(Request, serviceView, layerView, out var context, out var error))
            return error!;

        return await _queryService.ExecuteQueryAsync(serviceView, layerView, context, cancellationToken);
    }

    // Editing endpoints
    [HttpPost("{layerIndex:int}/applyEdits")]
    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> ApplyEditsAsync(...)
    {
        var serviceView = _metadataService.ResolveService(folderId, serviceId);
        if (serviceView is null) return NotFound();
        var layerView = _metadataService.ResolveLayer(serviceView, layerIndex);
        if (layerView is null) return NotFound();

        using var payload = await ParsePayloadAsync(Request, cancellationToken);
        if (payload is null) return BadRequest(new { error = "Invalid or empty JSON payload." });

        return await _editingService.ApplyEditsAsync(serviceView, layerView, payload.RootElement, Request, cancellationToken);
    }

    // Attachment endpoints...
    // Export endpoints...
}
```

### Phase 3: Register Services

Update `src/Honua.Server.Host/Hosting/HonuaHostConfigurationExtensions.cs`:

```csharp
services.AddScoped<IGeoservicesMetadataService, GeoservicesMetadataService>();
services.AddScoped<IGeoservicesQueryService, GeoservicesQueryService>();
services.AddScoped<IGeoservicesEditingService, GeoservicesEditingService>();
services.AddScoped<IGeoservicesAttachmentService, GeoservicesAttachmentService>();
services.AddScoped<IGeoservicesExportService, GeoservicesExportService>();
```

### Phase 4: Testing

1. **Unit Tests**: Create focused tests for each service
2. **Integration Tests**: Run existing Esri API tests
   - `tests/Honua.Server.Core.Tests/Hosting/GeoservicesRestEditingTests.cs`
   - `tests/Honua.Server.Core.Tests/Hosting/GeoservicesRestLeafletTests.cs`

### Phase 5: Documentation

Update documentation to reflect new architecture:
- Service responsibilities
- Dependency injection setup
- Testing guidelines

## Automation Support

### Python Extraction Script

Located at `/tmp/extract_services.py` - analyzes controller and generates method maps.

Usage:
```bash
python3 /tmp/extract_services.py
```

This script:
- Parses C# file
- Extracts method signatures
- Groups methods by service
- Generates line number references

## Benefits of Refactoring

1. **Single Responsibility**: Each service has one focused purpose
2. **Testability**: Services can be unit tested in isolation
3. **Maintainability**: Smaller files are easier to understand and modify
4. **Reusability**: Services can be used by other controllers/features
5. **Performance**: No impact - same business logic, better organization

## Constraints

- **MUST maintain Esri REST API compatibility**
- **MUST preserve exact endpoint behavior**
- **MUST keep existing authorization logic**
- **MUST pass all existing tests**

## Timeline Estimate

- Attachment Service: 3-4 hours
- Export Service: 2-3 hours
- Editing Service: 5-6 hours
- Query Service: 8-10 hours
- Controller Refactor: 2-3 hours
- Testing & Documentation: 2-3 hours

**Total**: ~22-29 hours of focused development time

## Next Steps

1. Extract GeoservicesAttachmentService (recommended next)
2. Extract GeoservicesExportService
3. Extract GeoservicesEditingService
4. Extract GeoservicesQueryService (largest, save for last)
5. Refactor controller to use all services
6. Run full test suite
7. Update documentation

## References

- Original File: `src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`
- Services Directory: `src/Honua.Server.Host/GeoservicesREST/Services/`
- Tests: `tests/Honua.Server.Core.Tests/Hosting/Geoservices*.cs`
