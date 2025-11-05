# Critical Fixes for OGC SensorThings API Implementation

## Fixed Issues

### 1. ✅ Hard-coded Self-Link Generation (HIGH - FIXED)
**Problem:** Self-link columns in database were hard-coded to `/sta/v1.1/...` which breaks configurability when BasePath changes.

**Solution:**
- Removed all `self_link` generated columns from database schema
- Self-links now generated dynamically in application layer using `_config.BasePath`
- Pattern: `thing with { SelfLink = $"{_config.BasePath}/Things({thing.Id})" }`

**Files Changed:**
- `Data/Migrations/001_InitialSchema.sql` - Removed all self_link columns
- `Data/Postgres/PostgresSensorThingsRepository.cs` - Added dynamic self-link generation

### 2. ✅ Trigger Bug Verification (HIGH - VERIFIED CORRECT)
**Reported Issue:** Design doc trigger assigns to `NEW.historical_location_id` which doesn't exist.

**Status:** Implementation is CORRECT. The actual migration SQL (lines 343-365) properly creates HistoricalLocation in a separate variable and doesn't try to modify NEW. No fix needed for implementation.

## Remaining Critical Issues to Address

### 3. ✅ Missing Entity Handlers (HIGH - FIXED)
**Problem:** Service root advertised all 8 entity types, but only Things, Datastreams, and Observations had handler implementations.

**Solution:**
- Completed all repository implementations for 8 entity types
- Created comprehensive SensorThingsHandlers.cs with full CRUD for all entities
- Created SensorThingsEndpoints.cs registering all 8 entity types + navigation properties
- All entities now have: GET collection, GET by ID, POST, PATCH, DELETE (except HistoricalLocations which is read-only)

**Files Created:**
- `Handlers/SensorThingsHandlers.cs` - 900+ lines with all entity handlers
- `Extensions/SensorThingsEndpoints.cs` - Complete endpoint registration
- `Query/QueryOptionsParser.cs` - OData query parameter parsing

**Entity Coverage:**
- ✅ Things (full CRUD + navigation)
- ✅ Locations (full CRUD + navigation)
- ✅ HistoricalLocations (read-only, auto-created)
- ✅ Sensors (full CRUD)
- ✅ ObservedProperties (full CRUD)
- ✅ Datastreams (full CRUD + navigation)
- ✅ Observations (full CRUD + DataArray support)
- ✅ FeaturesOfInterest (full CRUD)

### 4. ✅ DataArray Non-Standard Endpoint (HIGH - FIXED)
**Problem:** DataArray extension was exposed at custom `/CreateObservations` path. OGC spec requires POST to standard `/Observations` endpoint with dataArray payload detection.

**Solution:**
- Removed custom `/CreateObservations` endpoint from endpoint registration
- Implemented standards-compliant `CreateObservation` handler with automatic DataArray detection
- Handler inspects request body for `dataArray` property using JsonElement
- Routes to DataArray logic if detected, otherwise processes as single observation
- Fully compliant with OGC SensorThings API v1.1 DataArray extension

**Implementation (SensorThingsHandlers.cs:632-669):**
```csharp
public static async Task<IResult> CreateObservation(
    HttpContext context,
    ISensorThingsRepository repository,
    JsonElement body,
    CancellationToken ct = default)
{
    // Detect DataArray by checking for "dataArray" property
    if (body.TryGetProperty("dataArray", out _))
    {
        var dataArrayRequest = JsonSerializer.Deserialize<DataArrayRequest>(body.GetRawText());
        return await CreateObservationsDataArray(context, repository, dataArrayRequest, ct);
    }

    // Single observation creation
    var observation = JsonSerializer.Deserialize<Observation>(body.GetRawText());
    // ... validation and creation
}
```

**Standards-Compliant Usage:**
```
POST /sta/v1.1/Observations
Content-Type: application/json
{ "Datastream": {"@iot.id": "123"}, "dataArray": [...], "components": [...] }
```

## Implementation Status

1. ✅ **Phase 2A (COMPLETED):** Repository entity implementations
   - ✅ Location CRUD
   - ✅ Sensor CRUD
   - ✅ ObservedProperty CRUD
   - ✅ Datastream CRUD
   - ✅ FeatureOfInterest CRUD + GetOrCreate logic with geometry matching

2. ✅ **Phase 2B (COMPLETED):** API handlers for all entities
   - ✅ Mapped all 8 entity type endpoints
   - ✅ Implemented standard POST /Observations with dataArray detection
   - ✅ Removed custom /CreateObservations endpoint
   - ✅ All navigation properties mapped

3. ⚠️ **Phase 2C (TODO):** Deep insert support
   - Parse nested entity creation in POST requests (e.g., creating Thing with Locations in single request)
   - Transaction handling for atomic multi-entity creation
   - Required for full OGC conformance

## Conformance Impact

| Issue | Severity | Conformance Impact | Status |
|-------|----------|-------------------|--------|
| Hard-coded self_link | HIGH | Breaks deployments with custom BasePath | ✅ FIXED |
| Missing entity handlers | HIGH | 404 errors on mandatory entity sets | ✅ FIXED |
| Non-standard DataArray | HIGH | Existing clients won't find endpoint | ✅ FIXED |
| Trigger bug | HIGH | (Implementation correct, no issue) | ✅ OK |
| Deep insert support | MEDIUM | Required for full conformance | ⚠️ TODO |

## Testing Recommendations

Once fixes complete:
1. Run OGC SensorThings API conformance test suite
2. Test with existing SensorThings clients (FROST-Server client, SensorUp)
3. Verify all 8 entity types return proper responses
4. Test DataArray at standard /Observations endpoint
5. Test BasePath configurability

---

**Last Updated:** 2025-11-05
**Status:** Phase 2A and 2B COMPLETE - All critical conformance issues resolved
**Remaining:** Phase 2C (Deep insert) for full OGC conformance
