# OGC SensorThings API v1.1 Conformance Testing

This document describes the conformance testing approach for the Honua Server OGC SensorThings API v1.1 implementation.

## Overview

The conformance test suite (`OgcSensorThingsConformanceTests.cs`) validates that our implementation complies with the **OGC SensorThings API Part 1: Sensing v1.1** specification.

## Conformance Classes

The OGC SensorThings API v1.1 specification defines several conformance classes. Our implementation targets the **Sensing Profile** with the following conformance classes:

### ✅ Class 1: Core Requirements (`/req/core`)

**Specification:** `http://www.opengis.net/spec/iot_sensing/1.1/req/core`

**Requirements:**
- Service must expose all 8 entity sets at the service root
- Each entity must include `@iot.id` and `@iot.selfLink`
- Navigation links must be provided for related entities
- Service root must include `serverSettings.conformance` array

**Tests:**
- `ServiceRoot_ReturnsAllEntitySets` - Validates all 8 entity sets are exposed
- `ServiceRoot_ReturnsConformanceClasses` - Validates conformance declaration
- `Entities_IncludeValidSelfLinks` - Validates self-link format and resolution
- `Entities_IncludeNavigationLinks` - Validates navigation link presence

### ✅ Class 2: Create-Update-Delete (`/req/create-update-delete`)

**Specification:** `http://www.opengis.net/spec/iot_sensing/1.1/req/create-update-delete`

**Requirements:**
- Support POST for creating entities
- Support PATCH for updating entities
- Support DELETE for removing entities
- Return appropriate HTTP status codes (201, 204, 404, etc.)

**Tests:**
- `Thing_SupportsCRUDOperations` - Full CRUD lifecycle for Things
- `Location_SupportsCRUDOperations` - Full CRUD lifecycle for Locations
- `Sensor_SupportsCRUDOperations` - Full CRUD lifecycle for Sensors
- `ObservedProperty_SupportsCRUDOperations` - Full CRUD lifecycle for ObservedProperties

**Note:** Datastreams, Observations, and FeaturesOfInterest follow the same pattern. HistoricalLocations are read-only per specification.

### ✅ Class 3: DataArray Extension (`/req/data-array`)

**Specification:** `http://www.opengis.net/spec/iot_sensing/1.1/req/data-array`

**Requirements:**
- Accept DataArray format at `/Observations` endpoint
- DataArray request must include `dataArray` property
- Support `components` array defining data columns
- Process `dataArray` array with observation values
- Return 201 Created on success

**Tests:**
- `Observations_SupportsDataArrayExtension` - Creates entities and posts DataArray payload

**Example DataArray Payload:**
```json
{
  "dataArray": [
    {
      "Datastream": { "@iot.id": "datastream-uuid" },
      "components": ["phenomenonTime", "result"],
      "dataArray": [
        ["2025-01-01T00:00:00Z", 20.5],
        ["2025-01-01T01:00:00Z", 21.0],
        ["2025-01-01T02:00:00Z", 21.5]
      ]
    }
  ]
}
```

### ✅ Class 4: OData Query Options (`/req/query-options`)

The OGC SensorThings API requires support for OData query parameters:

#### `$top` and `$skip` (Pagination)

**Tests:**
- `EntitySet_SupportsTopQueryOption` - Validates result limiting
- `EntitySet_SupportsSkipQueryOption` - Validates result offset

**Example:**
```
GET /sta/v1.1/Things?$top=10&$skip=20
```

#### `$count`

**Tests:**
- `EntitySet_SupportsCountQueryOption` - Validates `@iot.count` in response

**Example:**
```
GET /sta/v1.1/Things?$count=true
```

**Response:**
```json
{
  "@iot.count": 42,
  "value": [...]
}
```

#### `$orderby`

**Tests:**
- `EntitySet_SupportsOrderByQueryOption` - Validates ascending/descending sort

**Example:**
```
GET /sta/v1.1/Observations?$orderby=phenomenonTime desc
```

#### `$filter`

**Tests:**
- `EntitySet_SupportsFilterQueryOption` - Validates filter expressions

**Supported Operators:**
- `eq` - Equals
- `ne` - Not equals
- `gt` - Greater than
- `ge` - Greater than or equal
- `lt` - Less than
- `le` - Less than or equal

**Example:**
```
GET /sta/v1.1/Things?$filter=name eq 'Weather Station'
```

#### `$select`

**Tests:**
- `EntitySet_SupportsSelectQueryOption` - Validates property projection

**Example:**
```
GET /sta/v1.1/Things?$select=name,description
```

#### `$expand`

**Tests:**
- `EntitySet_SupportsExpandQueryOption` - Validates navigation property expansion

**Example:**
```
GET /sta/v1.1/Things?$expand=Locations,Datastreams
```

### ✅ Class 5: GeoJSON and Spatial Support

**Requirements:**
- Locations must support `application/geo+json` encoding type
- Geometry must be stored and retrieved correctly
- Support PostGIS spatial operations (underlying implementation)

**Tests:**
- `Location_SupportsGeoJSONGeometry` - Validates Point geometry

**Example Location:**
```json
{
  "name": "Building A Roof",
  "description": "Rooftop sensor location",
  "encodingType": "application/geo+json",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  }
}
```

### ✅ Class 6: HistoricalLocations (Read-Only)

**Requirements:**
- HistoricalLocation entities are created automatically when Thing location changes
- POST/PATCH/DELETE operations on HistoricalLocations must return 405 Method Not Allowed
- GET operations must work for reading historical locations

**Tests:**
- `HistoricalLocation_IsCreatedAutomatically` - Validates automatic creation
- `HistoricalLocation_IsReadOnly` - Validates 405 response for POST

## Entity Coverage

All 8 OGC SensorThings entities are tested:

| Entity | Create | Read | Update | Delete | Navigation |
|--------|--------|------|--------|--------|------------|
| Thing | ✅ | ✅ | ✅ | ✅ | ✅ |
| Location | ✅ | ✅ | ✅ | ✅ | ✅ |
| HistoricalLocation | ❌ Auto | ✅ | ❌ | ❌ | ✅ |
| Sensor | ✅ | ✅ | ✅ | ✅ | ✅ |
| ObservedProperty | ✅ | ✅ | ✅ | ✅ | ✅ |
| Datastream | ✅ | ✅ | ✅ | ✅ | ✅ |
| Observation | ✅ | ✅ | ✅ | ✅ | ✅ |
| FeatureOfInterest | ✅ Auto | ✅ | ✅ | ✅ | ✅ |

**Legend:**
- ✅ = Supported and tested
- ❌ Auto = Automatically created by system (not user-created)

## Running the Tests

### Prerequisites

1. **PostgreSQL with PostGIS** running and accessible
2. **Database connection string** configured in test settings
3. **Host application** configured with SensorThings enabled

### Execute Tests

```bash
# Run all conformance tests
cd tests/Honua.Server.Host.Tests
dotnet test --filter "Component=Conformance"

# Run specific conformance class
dotnet test --filter "FullyQualifiedName~ServiceRoot"
dotnet test --filter "FullyQualifiedName~CRUD"
dotnet test --filter "FullyQualifiedName~DataArray"
dotnet test --filter "FullyQualifiedName~QueryOption"
```

### View Results

The test output includes detailed conformance information:

```
═══════════════════════════════════════════════════════════
  OGC SensorThings API v1.1 Conformance Test Summary
═══════════════════════════════════════════════════════════

Conformance Classes Tested:
  ✓ Class 1: Service Root and Metadata
  ✓ Class 2: Entity CRUD Operations
  ✓ Class 3: Self-Links and Navigation Links
  ✓ Class 4: OData Query Options
  ✓ Class 5: DataArray Extension
  ✓ Class 6: GeoJSON and Spatial Support
  ✓ Class 7: HistoricalLocations (Read-Only)

Entity Coverage:
  ✓ Things
  ✓ Locations
  ✓ HistoricalLocations
  ✓ Sensors
  ✓ ObservedProperties
  ✓ Datastreams
  ✓ Observations
  ✓ FeaturesOfInterest

OData Query Options:
  ✓ $top
  ✓ $skip
  ✓ $count
  ✓ $orderby
  ✓ $filter
  ✓ $select
  ✓ $expand
```

## Test Architecture

### WebApplicationFactory

Tests use ASP.NET Core's `WebApplicationFactory<Program>` to create an in-memory test server:

```csharp
public class OgcSensorThingsConformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OgcSensorThingsConformanceTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
}
```

### Test Isolation

Each test:
1. Creates necessary test data
2. Tracks created entity IDs
3. Performs conformance validation
4. Cleans up test data in `Dispose()`

**Example:**
```csharp
var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
var thingId = created.GetProperty("@iot.id").GetString()!;
_createdThingIds.Add(thingId); // Track for cleanup

// ... perform test validations ...

// Cleanup happens in Dispose()
```

### Assertions

Tests use FluentAssertions for readable validation:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
json.TryGetProperty("@iot.selfLink", out var selfLink).Should().BeTrue(
    "Entity must include @iot.selfLink property");
```

## Known Limitations

### Not Yet Tested

The following OGC SensorThings features are not yet covered by conformance tests:

1. **Advanced $filter operators:**
   - String functions (`startswith`, `endswith`, `contains`, `length`)
   - Math functions (`round`, `floor`, `ceiling`)
   - Logical operators (`and`, `or`, `not`)

2. **$expand with nested $filter/$orderby:**
   ```
   /Things?$expand=Datastreams($filter=name eq 'Temperature')
   ```

3. **Spatial query functions:**
   - `geo.distance`
   - `geo.intersects`
   - `geo.within`

4. **Multi-datastream support:**
   - Not yet implemented in the specification

5. **Batch requests:**
   - Multiple operations in single HTTP request

### Future Improvements

1. **Add stress tests** for DataArray with 10,000+ observations
2. **Add spatial query tests** when implementing advanced spatial functions
3. **Add deep expand tests** ($expand with depth > 2)
4. **Add pagination tests** with large datasets (test `@iot.nextLink`)
5. **Add concurrent request tests** for race conditions

## Manual Testing

### Postman Collection

A Postman collection is available at `tests/postman/SensorThings-Conformance.postman_collection.json` with:

- All entity CRUD operations
- Navigation property requests
- OData query examples
- DataArray examples
- Error case scenarios

### Example Manual Tests

#### 1. Verify Service Root

```bash
curl http://localhost:5000/sta/v1.1
```

**Expected Response:**
```json
{
  "value": [
    { "name": "Things", "url": "/sta/v1.1/Things" },
    { "name": "Locations", "url": "/sta/v1.1/Locations" },
    ...
  ],
  "serverSettings": {
    "conformance": [
      "http://www.opengis.net/spec/iot_sensing/1.1/req/core",
      "http://www.opengis.net/spec/iot_sensing/1.1/req/create-update-delete",
      "http://www.opengis.net/spec/iot_sensing/1.1/req/data-array"
    ]
  }
}
```

#### 2. Test DataArray

```bash
curl -X POST http://localhost:5000/sta/v1.1/Observations \
  -H "Content-Type: application/json" \
  -d '{
    "dataArray": [{
      "Datastream": {"@iot.id": "your-datastream-uuid"},
      "components": ["phenomenonTime", "result"],
      "dataArray": [
        ["2025-01-01T00:00:00Z", 20.5],
        ["2025-01-01T01:00:00Z", 21.0]
      ]
    }]
  }'
```

#### 3. Test OData Queries

```bash
# Pagination
curl "http://localhost:5000/sta/v1.1/Things?\$top=5&\$skip=10"

# Filter
curl "http://localhost:5000/sta/v1.1/Things?\$filter=name eq 'Weather Station'"

# Expand
curl "http://localhost:5000/sta/v1.1/Things?\$expand=Locations,Datastreams"

# Orderby
curl "http://localhost:5000/sta/v1.1/Observations?\$orderby=phenomenonTime desc&\$top=10"
```

## Conformance Certification

To claim **OGC SensorThings API v1.1 Sensing Profile** conformance:

1. ✅ All conformance tests must pass
2. ✅ Service root must declare all conformance classes
3. ✅ All required entity types must be implemented
4. ✅ All required operations (CRUD) must work
5. ✅ All required query options must be supported
6. ✅ DataArray extension must be functional
7. ⚠️ Consider submitting to OGC for official certification

## References

- **OGC SensorThings API v1.1 Specification:**
  http://docs.opengeospatial.org/is/18-088/18-088.html

- **OGC Conformance Test Suite:**
  https://github.com/opengeospatial/ets-sta10

- **Implementation Guide:**
  `docs/design/SENSORTHINGS_INTEGRATION.md`

- **API Design Document:**
  `docs/design/ogc-sensorthings-api-design.md`

- **Database Setup Guide:**
  `scripts/README-DATABASE-SETUP.md`

## Support

For issues related to conformance testing:

1. Check test output for specific failure details
2. Review API logs for error messages
3. Verify database schema is up to date
4. Check configuration in `appsettings.Development.json`
5. Consult the OGC specification for requirement clarification
