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

### 3. ⚠️ Missing Entity Handlers (HIGH - TODO)
**Problem:** Service root advertises all 8 entity types, but only Things, Datastreams, and Observations have handler implementations. The other 5 entity sets return 404s.

**Missing Handlers:**
- Locations (GET collection, GET by ID, POST, PATCH, DELETE)
- HistoricalLocations (GET collection, GET by ID)
- Sensors (GET collection, GET by ID, POST, PATCH, DELETE)
- ObservedProperties (GET collection, GET by ID, POST, PATCH, DELETE)
- FeaturesOfInterest (GET collection, GET by ID, POST, PATCH, DELETE)

**Repository Status:** Interfaces exist but marked as `throw new NotImplementedException()`

**Action Required:** Complete repository implementations + create API handlers for Phase 2

### 4. ⚠️ DataArray Non-Standard Endpoint (HIGH - TODO)
**Problem:** DataArray extension exposed at custom `/CreateObservations` path. OGC spec requires POST to standard `/Observations` endpoint with dataArray payload detection, or `/Datastreams(<id>)/Observations`.

**Current (Wrong):**
```
POST /sta/v1.1/CreateObservations
Content-Type: application/json
{ "Datastream": {...}, "dataArray": [...] }
```

**Standards-Compliant (Correct):**
```
POST /sta/v1.1/Observations
Content-Type: application/json
{ "Datastream": {...}, "dataArray": [...] }

OR

POST /sta/v1.1/Datastreams(123)/Observations
Content-Type: application/json
{ "dataArray": [...] }
```

**Action Required:**
- Remove custom `/CreateObservations` endpoint
- Update `Observations` POST handler to detect `dataArray` in request body
- If present, route to `CreateObservationsDataArrayAsync()`
- If not present, route to single `CreateObservationAsync()`

## Implementation Priority

1. **Phase 2A (Next):** Complete remaining repository entity implementations
   - Location CRUD
   - Sensor CRUD
   - ObservedProperty CRUD
   - Datastream CRUD
   - FeatureOfInterest CRUD + GetOrCreate logic

2. **Phase 2B:** Create API handlers for all entities
   - Map all endpoints in endpoint registration
   - Implement standard POST /Observations with dataArray detection
   - Remove custom /CreateObservations endpoint

3. **Phase 2C:** Deep insert support
   - Parse nested entity creation in POST requests
   - Transaction handling for atomic multi-entity creation

## Conformance Impact

| Issue | Severity | Conformance Impact | Status |
|-------|----------|-------------------|--------|
| Hard-coded self_link | HIGH | Breaks deployments with custom BasePath | ✅ FIXED |
| Missing entity handlers | HIGH | 404 errors on mandatory entity sets | ⚠️ TODO |
| Non-standard DataArray | HIGH | Existing clients won't find endpoint | ⚠️ TODO |
| Trigger bug | HIGH | (Implementation correct, no issue) | ✅ OK |

## Testing Recommendations

Once fixes complete:
1. Run OGC SensorThings API conformance test suite
2. Test with existing SensorThings clients (FROST-Server client, SensorUp)
3. Verify all 8 entity types return proper responses
4. Test DataArray at standard /Observations endpoint
5. Test BasePath configurability

---

**Last Updated:** 2025-11-05
**Reviewer:** User feedback incorporated
