# OGC SensorThings API Implementation Design for Honua

**Version:** 1.0
**Date:** 2025-11-05
**Target OGC Standard:** SensorThings API v1.1 (Part 1: Sensing)
**Primary Use Case:** Mobile field app device integration and sensor data collection

---

## 1. Executive Summary

This design document outlines the implementation of the OGC SensorThings API v1.1 (Sensing profile) within Honua Server. The primary use case focuses on **mobile field app integration** for collecting sensor observations from devices in the field (GPS, environmental sensors, photos, measurements, etc.).

### Key Design Principles

1. **Mobile-First Architecture** - Optimized for intermittent connectivity, batch operations, and efficient sync
2. **Honua Pattern Consistency** - Follow existing metadata-driven, multi-database architecture
3. **Standards Compliance** - Full OGC SensorThings API v1.1 conformance
4. **Performance** - Leverage Dapper, Redis caching, and efficient bulk operations
5. **Extensibility** - Support future tasking capabilities and MQTT streaming

---

## 2. Data Model Design

### 2.1 Core Entity Relationships

```
Thing (Mobile Device/User)
  ├─→ Locations (1:many) ────────────────┐
  │                                       │
  ├─→ HistoricalLocations (1:many)       │
  │     └─→ Location (many:many) ←───────┘
  │
  └─→ Datastreams (1:many)
        ├─→ Sensor (many:1)
        ├─→ ObservedProperty (many:1)
        └─→ Observations (1:many)
              └─→ FeatureOfInterest (many:1)
```

### 2.2 Mobile Field App Mapping

For a mobile field app collecting sensor data:

| SensorThings Entity | Mobile App Context | Example |
|---------------------|-------------------|---------|
| **Thing** | Mobile device or logged-in user | "John's iPhone", "Field Crew A" |
| **Location** | Current GPS position of device | lat: 40.7128, lon: -74.0060 |
| **HistoricalLocation** | GPS track/breadcrumb trail | Movement history |
| **Sensor** | Device sensor or manual measurement | iPhone GPS, Temperature Probe |
| **ObservedProperty** | What is being measured | Temperature, pH, Tree Height |
| **Datastream** | Sensor time-series for a Thing | "iPhone GPS on John's Device" |
| **Observation** | Individual measurement | Temperature: 22.5°C @ 2025-11-05T10:30:00Z |
| **FeatureOfInterest** | What/where was observed | Specific tree, water sample point |

### 2.3 Entity Definitions (Metadata Model)

Following Honua's metadata-driven architecture, define SensorThings entities as configuration records:

#### SensorThingsServiceDefinition

```csharp
public sealed record SensorThingsServiceDefinition
{
    public bool Enabled { get; init; } = false;
    public string Version { get; init; } = "v1.1";
    public string BasePath { get; init; } = "/sta/v1.1";

    // Feature flags
    public bool MqttEnabled { get; init; } = false;
    public bool BatchOperationsEnabled { get; init; } = true;
    public bool DeepInsertEnabled { get; init; } = true;
    public bool DataArrayEnabled { get; init; } = true;  // Efficient bulk observations

    // Mobile optimizations
    public bool OfflineSyncEnabled { get; init; } = true;
    public int MaxBatchSize { get; init; } = 1000;
    public int MaxObservationsPerRequest { get; init; } = 5000;

    // Storage
    public string DataSourceId { get; init; } = default!;
    public SensorThingsStorageDefinition Storage { get; init; } = default!;
}

public sealed record SensorThingsStorageDefinition
{
    // Table names (allow customization per deployment)
    public string ThingsTable { get; init; } = "sta_things";
    public string LocationsTable { get; init; } = "sta_locations";
    public string HistoricalLocationsTable { get; init; } = "sta_historical_locations";
    public string DatastreamsTable { get; init; } = "sta_datastreams";
    public string SensorsTable { get; init; } = "sta_sensors";
    public string ObservedPropertiesTable { get; init; } = "sta_observed_properties";
    public string ObservationsTable { get; init; } = "sta_observations";
    public string FeaturesOfInterestTable { get; init; } = "sta_features_of_interest";
    public string ThingLocationLinkTable { get; init; } = "sta_thing_location";
    public string HistoricalLocationLinkTable { get; init; } = "sta_historical_location_location";

    // Partitioning strategy for observations (critical for mobile scale)
    public bool PartitionObservations { get; init; } = true;
    public string PartitionStrategy { get; init; } = "monthly"; // daily, weekly, monthly, yearly

    // Retention policies
    public int? HistoricalLocationRetentionDays { get; init; } = 365;
    public int? ObservationRetentionDays { get; init; } = null; // null = keep forever
}
```

#### Thing Definition (Mobile Device/User)

```csharp
public sealed record ThingDefinition
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public Dictionary<string, object>? Properties { get; init; }

    // Mobile-specific metadata
    public string? DeviceType { get; init; }  // "ios", "android", "web"
    public string? DeviceModel { get; init; }
    public string? AppVersion { get; init; }
    public string? UserId { get; init; }  // Link to authentication
    public string? OrganizationId { get; init; }
}
```

#### Datastream Definition

```csharp
public sealed record DatastreamDefinition
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string ObservationType { get; init; } = default!; // OM_Measurement, OM_Observation, etc.
    public UnitOfMeasurement UnitOfMeasurement { get; init; } = default!;
    public Dictionary<string, object>? Properties { get; init; }

    public string ThingId { get; init; } = default!;
    public string SensorId { get; init; } = default!;
    public string ObservedPropertyId { get; init; } = default!;

    // Spatial extent (derived from observations)
    public NetTopologySuite.Geometries.Geometry? ObservedArea { get; init; }
    public string? PhenomenonTimeStart { get; init; }
    public string? PhenomenonTimeEnd { get; init; }
}

public sealed record UnitOfMeasurement
{
    public string Name { get; init; } = default!;
    public string Symbol { get; init; } = default!;
    public string Definition { get; init; } = default!;  // UCUM or QUDT URI
}
```

#### Observation Definition

```csharp
public sealed record ObservationDefinition
{
    public string Id { get; init; } = default!;
    public DateTime PhenomenonTime { get; init; }  // When measurement occurred
    public DateTime? ResultTime { get; init; }     // When result was generated
    public object Result { get; init; } = default!; // Can be number, string, boolean, JSON
    public string? ResultQuality { get; init; }
    public DateTimeRange? ValidTime { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }

    public string DatastreamId { get; init; } = default!;
    public string? FeatureOfInterestId { get; init; }

    // Mobile-specific tracking
    public DateTime? ClientTimestamp { get; init; }  // Device time when recorded
    public DateTime ServerTimestamp { get; init; }   // Server ingestion time
    public string? SyncBatchId { get; init; }        // For offline sync tracking
}
```

---

## 3. API Endpoint Design

### 3.1 Core RESTful Endpoints

Following Honua's Minimal Endpoints pattern in `Honua.Server.Host/Ogc/`, implement:

```
Base Path: /sta/v1.1

Service Root:
  GET  /sta/v1.1                          → Service document (JSON-LD)

Entity Collections:
  GET  /sta/v1.1/Things                   → List all Things (paginated)
  POST /sta/v1.1/Things                   → Create new Thing

  GET  /sta/v1.1/Locations
  POST /sta/v1.1/Locations

  GET  /sta/v1.1/HistoricalLocations

  GET  /sta/v1.1/Datastreams
  POST /sta/v1.1/Datastreams

  GET  /sta/v1.1/Sensors
  POST /sta/v1.1/Sensors

  GET  /sta/v1.1/ObservedProperties
  POST /sta/v1.1/ObservedProperties

  GET  /sta/v1.1/Observations             → List observations
  POST /sta/v1.1/Observations             → Create single observation

  GET  /sta/v1.1/FeaturesOfInterest
  POST /sta/v1.1/FeaturesOfInterest

Entity by ID:
  GET    /sta/v1.1/Things(id)             → Get single Thing
  PATCH  /sta/v1.1/Things(id)             → Update Thing (partial)
  DELETE /sta/v1.1/Things(id)             → Delete Thing

  (Same pattern for all entities)

Navigation Properties:
  GET  /sta/v1.1/Things(id)/Locations
  GET  /sta/v1.1/Things(id)/Datastreams
  GET  /sta/v1.1/Datastreams(id)/Observations
  GET  /sta/v1.1/Datastreams(id)/Sensor
  GET  /sta/v1.1/Observations(id)/FeatureOfInterest

Property Access:
  GET  /sta/v1.1/Things(id)/name
  GET  /sta/v1.1/Things(id)/name/$value   → Raw value only

Reference Links:
  GET  /sta/v1.1/Things(id)/Datastreams/$ref
```

### 3.2 Mobile-Optimized Endpoints

**Batch Operations** (Critical for offline sync):
```
POST /sta/v1.1/$batch

Request:
{
  "requests": [
    {
      "id": "1",
      "method": "POST",
      "url": "Things",
      "body": { "name": "Field Device A", ... }
    },
    {
      "id": "2",
      "method": "POST",
      "url": "Observations",
      "body": { "result": 22.5, ... }
    }
  ]
}
```

**Data Array Extension** (Efficient observation upload):
```
POST /sta/v1.1/CreateObservations

Request:
{
  "Datastream": { "@iot.id": "1" },
  "components": ["phenomenonTime", "result", "FeatureOfInterest/id"],
  "dataArray": [
    ["2025-11-05T10:00:00Z", 22.5, "foi1"],
    ["2025-11-05T10:05:00Z", 22.7, "foi1"],
    ["2025-11-05T10:10:00Z", 22.9, "foi1"]
  ]
}

Benefit: Reduces JSON overhead by ~70% for bulk observations
```

**Deep Insert** (Create full hierarchy in one request):
```
POST /sta/v1.1/Things

Request:
{
  "name": "John's iPhone",
  "description": "Field data collection device",
  "properties": {
    "deviceType": "ios",
    "deviceModel": "iPhone 14 Pro",
    "userId": "user123"
  },
  "Locations": [{
    "name": "Current Location",
    "encodingType": "application/geo+json",
    "location": {
      "type": "Point",
      "coordinates": [-74.0060, 40.7128]
    }
  }],
  "Datastreams": [{
    "name": "GPS Track",
    "description": "Device location stream",
    "observationType": "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
    "unitOfMeasurement": {
      "name": "Degree",
      "symbol": "deg",
      "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Degree"
    },
    "Sensor": {
      "name": "iPhone GPS",
      "description": "Built-in GPS sensor",
      "encodingType": "application/pdf",
      "metadata": "https://www.apple.com/iphone-14-pro/specs/"
    },
    "ObservedProperty": {
      "name": "Location",
      "description": "Geographic position",
      "definition": "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#Position"
    }
  }]
}

Response: 201 Created with all generated IDs
```

### 3.3 Query Options (OData-based)

Support standard SensorThings query parameters:

```
$expand    - Include related entities
  GET /sta/v1.1/Things?$expand=Datastreams,Locations

$select    - Choose properties
  GET /sta/v1.1/Things?$select=name,description

$filter    - Filter results
  GET /sta/v1.1/Observations?$filter=result gt 20.0 and phenomenonTime gt 2025-11-01

$orderby   - Sort results
  GET /sta/v1.1/Observations?$orderby=phenomenonTime desc

$top       - Limit results (default: 100, max: 10000)
  GET /sta/v1.1/Observations?$top=1000

$skip      - Pagination offset
  GET /sta/v1.1/Observations?$skip=1000

$count     - Include total count
  GET /sta/v1.1/Observations?$count=true
```

**Mobile-Critical Filters:**
```
# Get observations since last sync
GET /sta/v1.1/Observations?$filter=resultTime gt 2025-11-05T10:00:00Z

# Get observations for specific device
GET /sta/v1.1/Things('device123')/Datastreams?$expand=Observations($orderby=phenomenonTime desc;$top=100)

# Spatial filter for observations in area
GET /sta/v1.1/Observations?$filter=geo.intersects(FeatureOfInterest/feature, geography'POLYGON(...)')
```

---

## 4. Database Schema Design

### 4.1 Multi-Database Strategy

Following Honua's pattern, implement provider-specific schemas for:
- PostgreSQL/PostGIS (primary target)
- MySQL
- SQL Server
- SQLite (for testing)

### 4.2 PostgreSQL Schema (Primary)

```sql
-- ============================================================================
-- SensorThings API v1.1 Schema for PostgreSQL/PostGIS
-- ============================================================================

-- Extension requirements
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS btree_gist;  -- For temporal indexing

-- ============================================================================
-- THINGS (Mobile devices/users)
-- ============================================================================
CREATE TABLE sta_things (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    properties JSONB,

    -- Mobile-specific
    device_type VARCHAR(50),          -- 'ios', 'android', 'web'
    device_model VARCHAR(255),
    app_version VARCHAR(50),
    user_id VARCHAR(255),             -- Reference to auth system
    organization_id VARCHAR(255),

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- Self-link for OGC navigation
    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/Things(' || id || ')') STORED
);

CREATE INDEX idx_sta_things_user_id ON sta_things(user_id);
CREATE INDEX idx_sta_things_org_id ON sta_things(organization_id);
CREATE INDEX idx_sta_things_properties ON sta_things USING gin(properties);

-- ============================================================================
-- LOCATIONS (Geographic positions)
-- ============================================================================
CREATE TABLE sta_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/geo+json',
    location GEOMETRY(Geometry, 4326) NOT NULL,  -- PostGIS geometry
    properties JSONB,

    -- Derived fields for performance
    geojson JSONB GENERATED ALWAYS AS (
        ST_AsGeoJSON(location)::jsonb
    ) STORED,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/Locations(' || id || ')') STORED
);

CREATE INDEX idx_sta_locations_geom ON sta_locations USING gist(location);
CREATE INDEX idx_sta_locations_properties ON sta_locations USING gin(properties);

-- ============================================================================
-- THING-LOCATION Many-to-Many Relationship
-- ============================================================================
CREATE TABLE sta_thing_location (
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    location_id UUID NOT NULL REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (thing_id, location_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_sta_thing_location_location ON sta_thing_location(location_id);

-- ============================================================================
-- HISTORICAL LOCATIONS (Location history)
-- ============================================================================
CREATE TABLE sta_historical_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    time TIMESTAMPTZ NOT NULL,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/HistoricalLocations(' || id || ')') STORED
);

CREATE INDEX idx_sta_hist_loc_thing_time ON sta_historical_locations(thing_id, time DESC);

-- Link table for HistoricalLocation to Location (many-to-many)
CREATE TABLE sta_historical_location_location (
    historical_location_id UUID NOT NULL REFERENCES sta_historical_locations(id) ON DELETE CASCADE,
    location_id UUID NOT NULL REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (historical_location_id, location_id)
);

-- ============================================================================
-- SENSORS (Measurement procedures/devices)
-- ============================================================================
CREATE TABLE sta_sensors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL,  -- 'application/pdf', 'http://www.opengis.net/doc/IS/SensorML/2.0'
    metadata TEXT NOT NULL,                -- URL or inline SensorML/PDF
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/Sensors(' || id || ')') STORED
);

CREATE INDEX idx_sta_sensors_properties ON sta_sensors USING gin(properties);

-- ============================================================================
-- OBSERVED PROPERTIES (Phenomena being measured)
-- ============================================================================
CREATE TABLE sta_observed_properties (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    definition TEXT NOT NULL,  -- URI to standard definition (QUDT, CF conventions, etc.)
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/ObservedProperties(' || id || ')') STORED
);

CREATE UNIQUE INDEX idx_sta_obs_prop_definition ON sta_observed_properties(definition);
CREATE INDEX idx_sta_obs_prop_properties ON sta_observed_properties USING gin(properties);

-- ============================================================================
-- DATASTREAMS (Time series of observations)
-- ============================================================================
CREATE TABLE sta_datastreams (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    observation_type VARCHAR(255) NOT NULL,  -- OM_Measurement, OM_Observation, etc.
    properties JSONB,

    -- Unit of measurement (embedded JSON)
    unit_of_measurement JSONB NOT NULL,  -- { "name": "Celsius", "symbol": "°C", "definition": "..." }

    -- Foreign keys
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    sensor_id UUID NOT NULL REFERENCES sta_sensors(id) ON DELETE RESTRICT,
    observed_property_id UUID NOT NULL REFERENCES sta_observed_properties(id) ON DELETE RESTRICT,

    -- Derived spatial/temporal extent (updated by trigger)
    observed_area GEOMETRY(Geometry, 4326),
    phenomenon_time_start TIMESTAMPTZ,
    phenomenon_time_end TIMESTAMPTZ,
    result_time_start TIMESTAMPTZ,
    result_time_end TIMESTAMPTZ,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/Datastreams(' || id || ')') STORED
);

CREATE INDEX idx_sta_datastreams_thing ON sta_datastreams(thing_id);
CREATE INDEX idx_sta_datastreams_sensor ON sta_datastreams(sensor_id);
CREATE INDEX idx_sta_datastreams_obs_prop ON sta_datastreams(observed_property_id);
CREATE INDEX idx_sta_datastreams_phen_time ON sta_datastreams USING gist(
    tstzrange(phenomenon_time_start, phenomenon_time_end, '[]')
);
CREATE INDEX idx_sta_datastreams_observed_area ON sta_datastreams USING gist(observed_area);
CREATE INDEX idx_sta_datastreams_properties ON sta_datastreams USING gin(properties);

-- ============================================================================
-- FEATURES OF INTEREST (What is being observed)
-- ============================================================================
CREATE TABLE sta_features_of_interest (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/geo+json',
    feature GEOMETRY(Geometry, 4326) NOT NULL,
    properties JSONB,

    -- Derived GeoJSON
    geojson JSONB GENERATED ALWAYS AS (
        ST_AsGeoJSON(feature)::jsonb
    ) STORED,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/FeaturesOfInterest(' || id || ')') STORED
);

CREATE INDEX idx_sta_foi_geom ON sta_features_of_interest USING gist(feature);
CREATE INDEX idx_sta_foi_properties ON sta_features_of_interest USING gin(properties);

-- ============================================================================
-- OBSERVATIONS (Individual measurements) - PARTITIONED FOR SCALE
-- ============================================================================
CREATE TABLE sta_observations (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    phenomenon_time TIMESTAMPTZ NOT NULL,
    result_time TIMESTAMPTZ,
    result JSONB NOT NULL,  -- Flexible type: number, string, boolean, complex object
    result_quality JSONB,
    valid_time_start TIMESTAMPTZ,
    valid_time_end TIMESTAMPTZ,
    parameters JSONB,

    -- Foreign keys
    datastream_id UUID NOT NULL REFERENCES sta_datastreams(id) ON DELETE CASCADE,
    feature_of_interest_id UUID REFERENCES sta_features_of_interest(id) ON DELETE SET NULL,

    -- Mobile-specific fields
    client_timestamp TIMESTAMPTZ,  -- Device time when recorded
    server_timestamp TIMESTAMPTZ NOT NULL DEFAULT now(),
    sync_batch_id UUID,            -- For tracking offline sync batches

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    self_link TEXT GENERATED ALWAYS AS ('/sta/v1.1/Observations(' || id || ')') STORED,

    PRIMARY KEY (id, phenomenon_time)
) PARTITION BY RANGE (phenomenon_time);

-- Create initial partitions (monthly)
CREATE TABLE sta_observations_2025_11 PARTITION OF sta_observations
    FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');

CREATE TABLE sta_observations_2025_12 PARTITION OF sta_observations
    FOR VALUES FROM ('2025-12-01') TO ('2026-01-01');

-- Indexes on base table (inherited by partitions)
CREATE INDEX idx_sta_obs_datastream ON sta_observations(datastream_id);
CREATE INDEX idx_sta_obs_foi ON sta_observations(feature_of_interest_id);
CREATE INDEX idx_sta_obs_phen_time ON sta_observations(phenomenon_time DESC);
CREATE INDEX idx_sta_obs_result_time ON sta_observations(result_time DESC);
CREATE INDEX idx_sta_obs_server_time ON sta_observations(server_timestamp DESC);
CREATE INDEX idx_sta_obs_sync_batch ON sta_observations(sync_batch_id) WHERE sync_batch_id IS NOT NULL;
CREATE INDEX idx_sta_obs_result ON sta_observations USING gin(result);
CREATE INDEX idx_sta_obs_parameters ON sta_observations USING gin(parameters);

-- Composite index for common mobile queries
CREATE INDEX idx_sta_obs_datastream_time ON sta_observations(datastream_id, phenomenon_time DESC);

-- ============================================================================
-- TRIGGERS FOR AUTOMATIC UPDATES
-- ============================================================================

-- Update Datastream spatial/temporal extent when observations are inserted
CREATE OR REPLACE FUNCTION update_datastream_extent()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE sta_datastreams
    SET
        phenomenon_time_start = LEAST(
            COALESCE(phenomenon_time_start, NEW.phenomenon_time),
            NEW.phenomenon_time
        ),
        phenomenon_time_end = GREATEST(
            COALESCE(phenomenon_time_end, NEW.phenomenon_time),
            NEW.phenomenon_time
        ),
        result_time_start = LEAST(
            COALESCE(result_time_start, NEW.result_time),
            NEW.result_time
        ),
        result_time_end = GREATEST(
            COALESCE(result_time_end, NEW.result_time),
            NEW.result_time
        ),
        updated_at = now()
    WHERE id = NEW.datastream_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_datastream_extent
AFTER INSERT ON sta_observations
FOR EACH ROW
EXECUTE FUNCTION update_datastream_extent();

-- Create HistoricalLocation when Thing location changes
CREATE OR REPLACE FUNCTION create_historical_location()
RETURNS TRIGGER AS $$
BEGIN
    -- Insert new HistoricalLocation
    INSERT INTO sta_historical_locations (thing_id, time)
    VALUES (NEW.thing_id, now())
    RETURNING id INTO NEW.historical_location_id;

    -- Link to Location
    INSERT INTO sta_historical_location_location (historical_location_id, location_id)
    VALUES (NEW.historical_location_id, NEW.location_id);

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_create_historical_location
AFTER INSERT ON sta_thing_location
FOR EACH ROW
EXECUTE FUNCTION create_historical_location();

-- ============================================================================
-- VIEWS FOR CONVENIENCE
-- ============================================================================

-- Flattened view for mobile app queries
CREATE VIEW vw_sta_observations_flat AS
SELECT
    o.id,
    o.phenomenon_time,
    o.result_time,
    o.result,
    o.result_quality,
    o.parameters,
    o.client_timestamp,
    o.server_timestamp,
    o.sync_batch_id,

    -- Datastream info
    d.id AS datastream_id,
    d.name AS datastream_name,
    d.observation_type,
    d.unit_of_measurement,

    -- Thing info
    t.id AS thing_id,
    t.name AS thing_name,
    t.device_type,
    t.user_id,

    -- Sensor info
    s.id AS sensor_id,
    s.name AS sensor_name,

    -- ObservedProperty info
    op.id AS observed_property_id,
    op.name AS observed_property_name,
    op.definition AS observed_property_definition,

    -- FeatureOfInterest info
    foi.id AS feature_of_interest_id,
    foi.name AS feature_of_interest_name,
    ST_AsGeoJSON(foi.feature)::jsonb AS feature_geojson

FROM sta_observations o
JOIN sta_datastreams d ON o.datastream_id = d.id
JOIN sta_things t ON d.thing_id = t.id
JOIN sta_sensors s ON d.sensor_id = s.id
JOIN sta_observed_properties op ON d.observed_property_id = op.id
LEFT JOIN sta_features_of_interest foi ON o.feature_of_interest_id = foi.id;

-- ============================================================================
-- FUNCTIONS FOR COMMON OPERATIONS
-- ============================================================================

-- Get or create FeatureOfInterest from geometry
CREATE OR REPLACE FUNCTION get_or_create_foi(
    p_name VARCHAR,
    p_description TEXT,
    p_geometry GEOMETRY
) RETURNS UUID AS $$
DECLARE
    v_foi_id UUID;
BEGIN
    -- Try to find existing FOI at same location
    SELECT id INTO v_foi_id
    FROM sta_features_of_interest
    WHERE ST_Equals(feature, p_geometry)
    LIMIT 1;

    IF v_foi_id IS NULL THEN
        -- Create new FOI
        INSERT INTO sta_features_of_interest (name, description, encoding_type, feature)
        VALUES (p_name, p_description, 'application/geo+json', p_geometry)
        RETURNING id INTO v_foi_id;
    END IF;

    RETURN v_foi_id;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- GRANTS (adjust based on your security model)
-- ============================================================================

-- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO sta_app_role;
-- GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO sta_app_role;
```

### 4.3 Partitioning Strategy

For mobile field apps collecting frequent observations:

**Automatic Partition Management:**
```sql
-- Function to create future partitions
CREATE OR REPLACE FUNCTION create_observation_partitions(months_ahead INT)
RETURNS void AS $$
DECLARE
    start_date DATE;
    end_date DATE;
    partition_name TEXT;
BEGIN
    FOR i IN 0..months_ahead LOOP
        start_date := date_trunc('month', now() + (i || ' months')::interval);
        end_date := start_date + interval '1 month';
        partition_name := 'sta_observations_' || to_char(start_date, 'YYYY_MM');

        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF sta_observations FOR VALUES FROM (%L) TO (%L)',
            partition_name, start_date, end_date
        );
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- Schedule via pg_cron or application startup
SELECT create_observation_partitions(6);  -- Create 6 months ahead
```

### 4.4 Indexes for Mobile Performance

**Critical indexes for mobile sync patterns:**

```sql
-- Fast "get latest observations since timestamp" queries
CREATE INDEX idx_obs_sync_query ON sta_observations(
    datastream_id,
    server_timestamp DESC
) INCLUDE (result, phenomenon_time);

-- Spatial queries for observations in area
CREATE INDEX idx_obs_spatial ON sta_observations(feature_of_interest_id)
WHERE feature_of_interest_id IS NOT NULL;

-- User-specific queries (via Thing)
CREATE INDEX idx_datastream_user ON sta_datastreams(thing_id)
INCLUDE (name, observation_type);
```

---

## 5. Service Layer Architecture

### 5.1 Core Interfaces

Following Honua's repository pattern:

```csharp
// Core/SensorThings/ISensorThingsRepository.cs
public interface ISensorThingsRepository
{
    // Thing operations
    Task<Thing> GetThingAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<Thing>> GetThingsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default);
    Task<Thing> UpdateThingAsync(string id, Thing thing, CancellationToken ct = default);
    Task DeleteThingAsync(string id, CancellationToken ct = default);

    // Datastream operations
    Task<Datastream> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default);
    Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default);

    // Observation operations (critical for mobile)
    Task<Observation> GetObservationAsync(string id, CancellationToken ct = default);
    Task<PagedResult<Observation>> GetObservationsAsync(QueryOptions options, CancellationToken ct = default);
    Task<Observation> CreateObservationAsync(Observation observation, CancellationToken ct = default);
    Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(IReadOnlyList<Observation> observations, CancellationToken ct = default);
    Task<IReadOnlyList<Observation>> CreateObservationsDataArrayAsync(DataArrayRequest request, CancellationToken ct = default);

    // Navigation property helpers
    Task<PagedResult<Datastream>> GetThingDatastreamsAsync(string thingId, QueryOptions options, CancellationToken ct = default);
    Task<PagedResult<Observation>> GetDatastreamObservationsAsync(string datastreamId, QueryOptions options, CancellationToken ct = default);

    // Mobile-specific operations
    Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default);
}

// Core/SensorThings/Models/QueryOptions.cs
public sealed record QueryOptions
{
    public FilterExpression? Filter { get; init; }
    public IReadOnlyList<OrderBy>? OrderBy { get; init; }
    public int? Top { get; init; } = 100;
    public int? Skip { get; init; } = 0;
    public bool Count { get; init; } = false;
    public ExpandOptions? Expand { get; init; }
    public IReadOnlyList<string>? Select { get; init; }
}

public sealed record ExpandOptions
{
    public bool Locations { get; init; }
    public bool HistoricalLocations { get; init; }
    public bool Datastreams { get; init; }
    public bool Observations { get; init; }
    public ExpandOptions? Nested { get; init; }
    public int? MaxDepth { get; init; } = 2;
}

// Mobile sync models
public sealed record SyncRequest
{
    public string ThingId { get; init; } = default!;
    public DateTime? SinceTimestamp { get; init; }
    public string? SyncBatchId { get; init; }
    public IReadOnlyList<Observation> Observations { get; init; } = [];
}

public sealed record SyncResponse
{
    public DateTime ServerTimestamp { get; init; }
    public int ObservationsCreated { get; init; }
    public int ObservationsUpdated { get; init; }
    public IReadOnlyList<SyncError> Errors { get; init; } = [];
}
```

### 5.2 Provider Implementation

```csharp
// Core/SensorThings/Postgres/PostgresSensorThingsRepository.cs
public sealed class PostgresSensorThingsRepository : ISensorThingsRepository
{
    private readonly IDbConnection _connection;
    private readonly IMetadataRegistry _metadata;
    private readonly ILogger<PostgresSensorThingsRepository> _logger;

    public PostgresSensorThingsRepository(
        IDataStoreProvider dataStoreProvider,
        IMetadataRegistry metadata,
        ILogger<PostgresSensorThingsRepository> logger)
    {
        _connection = dataStoreProvider.GetConnection();
        _metadata = metadata;
        _logger = logger;
    }

    public async Task<Observation> CreateObservationAsync(
        Observation observation,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_observations (
                phenomenon_time,
                result_time,
                result,
                datastream_id,
                feature_of_interest_id,
                client_timestamp,
                sync_batch_id,
                parameters
            )
            VALUES (
                @PhenomenonTime,
                @ResultTime,
                @Result::jsonb,
                @DatastreamId,
                @FeatureOfInterestId,
                @ClientTimestamp,
                @SyncBatchId,
                @Parameters::jsonb
            )
            RETURNING id, server_timestamp, self_link;
            """;

        var result = await _connection.QuerySingleAsync<dynamic>(sql, new
        {
            observation.PhenomenonTime,
            observation.ResultTime,
            Result = JsonSerializer.Serialize(observation.Result),
            observation.DatastreamId,
            observation.FeatureOfInterestId,
            observation.ClientTimestamp,
            observation.SyncBatchId,
            Parameters = observation.Parameters != null
                ? JsonSerializer.Serialize(observation.Parameters)
                : null
        }, cancellationToken: ct);

        return observation with
        {
            Id = result.id,
            ServerTimestamp = result.server_timestamp,
            SelfLink = result.self_link
        };
    }

    // Optimized bulk insert for mobile sync
    public async Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct = default)
    {
        if (observations.Count == 0)
            return [];

        // Use PostgreSQL COPY for maximum performance
        const string copyCommand = """
            COPY sta_observations (
                phenomenon_time, result_time, result, datastream_id,
                feature_of_interest_id, client_timestamp, sync_batch_id, parameters
            )
            FROM STDIN (FORMAT BINARY)
            """;

        using var writer = await _connection.BeginBinaryImportAsync(copyCommand, ct);

        foreach (var obs in observations)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(obs.PhenomenonTime, ct);
            await writer.WriteAsync(obs.ResultTime, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(JsonSerializer.Serialize(obs.Result), NpgsqlDbType.Jsonb, ct);
            await writer.WriteAsync(obs.DatastreamId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(obs.FeatureOfInterestId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(obs.ClientTimestamp, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(obs.SyncBatchId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(
                obs.Parameters != null ? JsonSerializer.Serialize(obs.Parameters) : DBNull.Value,
                NpgsqlDbType.Jsonb,
                ct);
        }

        await writer.CompleteAsync(ct);

        _logger.LogInformation("Bulk inserted {Count} observations", observations.Count);

        return observations;
    }
}
```

### 5.3 Filter Translation

Implement OData filter translation following Honua's existing `CqlFilterTranslator`:

```csharp
// Core/SensorThings/Query/SensorThingsFilterTranslator.cs
public sealed class SensorThingsFilterTranslator
{
    public (string Sql, DynamicParameters Parameters) Translate(
        FilterExpression filter,
        string entityType)
    {
        var parameters = new DynamicParameters();
        var sql = TranslateExpression(filter, parameters, entityType);
        return (sql, parameters);
    }

    private string TranslateExpression(
        FilterExpression expr,
        DynamicParameters parameters,
        string entityType)
    {
        return expr switch
        {
            ComparisonExpression comp => TranslateComparison(comp, parameters),
            LogicalExpression logical => TranslateLogical(logical, parameters, entityType),
            FunctionExpression func => TranslateFunction(func, parameters),
            _ => throw new NotSupportedException($"Expression type {expr.GetType()} not supported")
        };
    }

    private string TranslateComparison(ComparisonExpression expr, DynamicParameters parameters)
    {
        var paramName = $"p{parameters.ParameterNames.Count()}";
        parameters.Add(paramName, expr.Value);

        var column = MapPropertyToColumn(expr.Property);

        return expr.Operator switch
        {
            "eq" => $"{column} = @{paramName}",
            "ne" => $"{column} != @{paramName}",
            "gt" => $"{column} > @{paramName}",
            "ge" => $"{column} >= @{paramName}",
            "lt" => $"{column} < @{paramName}",
            "le" => $"{column} <= @{paramName}",
            _ => throw new NotSupportedException($"Operator {expr.Operator} not supported")
        };
    }

    private string TranslateFunction(FunctionExpression expr, DynamicParameters parameters)
    {
        return expr.Name switch
        {
            "geo.intersects" => TranslateGeoIntersects(expr, parameters),
            "geo.distance" => TranslateGeoDistance(expr, parameters),
            "substringof" => TranslateSubstringOf(expr, parameters),
            "startswith" => $"{MapPropertyToColumn(expr.Arguments[0])} LIKE @{AddParameter(parameters, expr.Arguments[1] + "%")}",
            "endswith" => $"{MapPropertyToColumn(expr.Arguments[0])} LIKE @{AddParameter(parameters, "%" + expr.Arguments[1])}",
            _ => throw new NotSupportedException($"Function {expr.Name} not supported")
        };
    }

    private string TranslateGeoIntersects(FunctionExpression expr, DynamicParameters parameters)
    {
        var property = MapPropertyToColumn(expr.Arguments[0]);
        var geometry = expr.Arguments[1]; // GeoJSON or WKT
        var paramName = $"geom{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, geometry);

        return $"ST_Intersects({property}, ST_GeomFromGeoJSON(@{paramName}))";
    }
}
```

---

## 6. API Handlers (Minimal Endpoints)

### 6.1 Handler Registration

```csharp
// Host/Ogc/SensorThings/SensorThingsEndpoints.cs
public static class SensorThingsEndpoints
{
    public static IEndpointRouteBuilder MapSensorThingsEndpoints(
        this IEndpointRouteBuilder endpoints,
        SensorThingsServiceDefinition config)
    {
        if (!config.Enabled)
            return endpoints;

        var basePath = config.BasePath; // "/sta/v1.1"

        // Service root
        endpoints.MapGet(basePath, SensorThingsHandlers.GetServiceRoot)
            .WithName("SensorThings_ServiceRoot")
            .WithTags("SensorThings");

        // Things
        endpoints.MapGet($"{basePath}/Things", SensorThingsHandlers.GetThings)
            .WithName("SensorThings_GetThings");
        endpoints.MapGet($"{basePath}/Things({{id}})", SensorThingsHandlers.GetThing)
            .WithName("SensorThings_GetThing");
        endpoints.MapPost($"{basePath}/Things", SensorThingsHandlers.CreateThing)
            .WithName("SensorThings_CreateThing");
        endpoints.MapPatch($"{basePath}/Things({{id}})", SensorThingsHandlers.UpdateThing)
            .WithName("SensorThings_UpdateThing");
        endpoints.MapDelete($"{basePath}/Things({{id}})", SensorThingsHandlers.DeleteThing)
            .WithName("SensorThings_DeleteThing");

        // Datastreams
        endpoints.MapGet($"{basePath}/Datastreams", SensorThingsHandlers.GetDatastreams);
        endpoints.MapGet($"{basePath}/Datastreams({{id}})", SensorThingsHandlers.GetDatastream);
        endpoints.MapPost($"{basePath}/Datastreams", SensorThingsHandlers.CreateDatastream);

        // Observations (critical for mobile)
        endpoints.MapGet($"{basePath}/Observations", SensorThingsHandlers.GetObservations);
        endpoints.MapGet($"{basePath}/Observations({{id}})", SensorThingsHandlers.GetObservation);
        endpoints.MapPost($"{basePath}/Observations", SensorThingsHandlers.CreateObservation);

        // Navigation properties
        endpoints.MapGet($"{basePath}/Things({{id}})/Datastreams", SensorThingsHandlers.GetThingDatastreams);
        endpoints.MapGet($"{basePath}/Things({{id}})/Locations", SensorThingsHandlers.GetThingLocations);
        endpoints.MapGet($"{basePath}/Datastreams({{id}})/Observations", SensorThingsHandlers.GetDatastreamObservations);

        // Mobile-optimized endpoints
        if (config.BatchOperationsEnabled)
        {
            endpoints.MapPost($"{basePath}/$batch", SensorThingsHandlers.ProcessBatch);
        }

        if (config.DataArrayEnabled)
        {
            endpoints.MapPost($"{basePath}/CreateObservations", SensorThingsHandlers.CreateObservationsDataArray);
        }

        // Custom mobile sync endpoint
        if (config.OfflineSyncEnabled)
        {
            endpoints.MapPost($"{basePath}/Sync", SensorThingsHandlers.SyncObservations)
                .RequireAuthorization(); // Require authenticated user
        }

        return endpoints;
    }
}
```

### 6.2 Handler Implementation

```csharp
// Host/Ogc/SensorThings/SensorThingsHandlers.cs
public static class SensorThingsHandlers
{
    public static async Task<IResult> GetServiceRoot(
        HttpContext context,
        ISensorThingsRepository repository)
    {
        var serviceRoot = new
        {
            value = new[]
            {
                new { name = "Things", url = "Things" },
                new { name = "Locations", url = "Locations" },
                new { name = "HistoricalLocations", url = "HistoricalLocations" },
                new { name = "Datastreams", url = "Datastreams" },
                new { name = "Sensors", url = "Sensors" },
                new { name = "ObservedProperties", url = "ObservedProperties" },
                new { name = "Observations", url = "Observations" },
                new { name = "FeaturesOfInterest", url = "FeaturesOfInterest" }
            },
            serverSettings = new
            {
                conformance = new[]
                {
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/core",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/dataArray",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/create-update-delete",
                    "http://www.opengis.net/spec/iot_sensing/1.1/req/batch-request"
                }
            }
        };

        return Results.Json(serviceRoot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public static async Task<IResult> GetThings(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromQuery(Name = "$filter")] string? filter,
        [FromQuery(Name = "$expand")] string? expand,
        [FromQuery(Name = "$select")] string? select,
        [FromQuery(Name = "$orderby")] string? orderby,
        [FromQuery(Name = "$top")] int? top,
        [FromQuery(Name = "$skip")] int? skip,
        [FromQuery(Name = "$count")] bool count = false)
    {
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);
        var result = await repository.GetThingsAsync(options);

        return Results.Json(new
        {
            @odata.context = $"{context.Request.Scheme}://{context.Request.Host}/sta/v1.1/$metadata#Things",
            @odata.count = count ? result.TotalCount : null,
            @odata.nextLink = result.NextLink,
            value = result.Items
        });
    }

    public static async Task<IResult> CreateThing(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromBody] Thing thing)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(thing.Name))
            return Results.BadRequest(new { error = "Name is required" });

        // Extract user context from JWT
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            thing = thing with { Properties = thing.Properties ?? new Dictionary<string, object>() };
            thing.Properties["userId"] = userId;
        }

        var created = await repository.CreateThingAsync(thing);

        return Results.Created(created.SelfLink, created);
    }

    public static async Task<IResult> CreateObservation(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromBody] Observation observation)
    {
        // Validate
        if (observation.DatastreamId == null)
            return Results.BadRequest(new { error = "Datastream is required" });

        if (observation.Result == null)
            return Results.BadRequest(new { error = "Result is required" });

        var created = await repository.CreateObservationAsync(observation);

        return Results.Created(created.SelfLink, created);
    }

    // Mobile-optimized batch creation
    public static async Task<IResult> CreateObservationsDataArray(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromBody] DataArrayRequest request)
    {
        if (request.Datastream?.Id == null)
            return Results.BadRequest(new { error = "Datastream reference required" });

        if (request.Components == null || request.DataArray == null)
            return Results.BadRequest(new { error = "Components and dataArray required" });

        // Convert data array to observations
        var observations = request.ToObservations();

        var created = await repository.CreateObservationsDataArrayAsync(request);

        return Results.Created($"/sta/v1.1/Datastreams({request.Datastream.Id})/Observations", new
        {
            @odata.context = $"{context.Request.Scheme}://{context.Request.Host}/sta/v1.1/$metadata#Observations",
            value = created
        });
    }

    // Custom mobile sync endpoint
    public static async Task<IResult> SyncObservations(
        HttpContext context,
        ISensorThingsRepository repository,
        [FromBody] SyncRequest request)
    {
        // Validate user owns the Thing
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var thing = await repository.GetThingAsync(request.ThingId);

        if (thing?.Properties?.GetValueOrDefault("userId")?.ToString() != userId)
            return Results.Forbid();

        // Process sync
        var response = await repository.SyncObservationsAsync(request);

        return Results.Ok(response);
    }
}

// Core/SensorThings/Models/DataArrayRequest.cs
public sealed record DataArrayRequest
{
    [JsonPropertyName("Datastream")]
    public EntityReference? Datastream { get; init; }

    [JsonPropertyName("components")]
    public IReadOnlyList<string>? Components { get; init; }

    [JsonPropertyName("dataArray")]
    public IReadOnlyList<IReadOnlyList<object>>? DataArray { get; init; }

    public IReadOnlyList<Observation> ToObservations()
    {
        if (Components == null || DataArray == null || Datastream?.Id == null)
            return [];

        var observations = new List<Observation>();

        foreach (var row in DataArray)
        {
            var obs = new Observation
            {
                DatastreamId = Datastream.Id
            };

            for (int i = 0; i < Components.Count; i++)
            {
                var component = Components[i];
                var value = row[i];

                obs = component switch
                {
                    "phenomenonTime" => obs with { PhenomenonTime = DateTime.Parse(value.ToString()!) },
                    "result" => obs with { Result = value },
                    "resultTime" => obs with { ResultTime = DateTime.Parse(value.ToString()!) },
                    "FeatureOfInterest/id" => obs with { FeatureOfInterestId = value.ToString() },
                    _ => obs
                };
            }

            observations.Add(obs);
        }

        return observations;
    }
}
```

---

## 7. Mobile Integration Patterns

### 7.1 Mobile App Workflow

**Initial Setup (First Launch):**
```
1. User logs into mobile app (JWT obtained)
2. App creates Thing entity:
   POST /sta/v1.1/Things
   {
     "name": "John's iPhone",
     "properties": {
       "deviceType": "ios",
       "deviceModel": "iPhone 14 Pro",
       "appVersion": "1.2.0"
     },
     "Locations": [{ ... }],
     "Datastreams": [
       {
         "name": "GPS Track",
         "Sensor": { "name": "iPhone GPS", ... },
         "ObservedProperty": { "name": "Location", ... }
       },
       {
         "name": "Temperature",
         "Sensor": { "name": "Manual Entry", ... },
         "ObservedProperty": { "name": "Air Temperature", ... }
       }
     ]
   }
3. Store Thing ID locally (thingId: "abc-123")
```

**Recording Observations (Online):**
```
User takes measurement →
  POST /sta/v1.1/Observations
  {
    "phenomenonTime": "2025-11-05T10:30:00Z",
    "result": 22.5,
    "Datastream": { "@iot.id": "datastream-123" },
    "FeatureOfInterest": {
      "name": "Sample Point A",
      "encodingType": "application/geo+json",
      "feature": {
        "type": "Point",
        "coordinates": [-74.006, 40.7128]
      }
    }
  }
```

**Recording Observations (Offline):**
```
User takes measurements while offline →
  Store in local SQLite database

When connection restored →
  POST /sta/v1.1/CreateObservations (DataArray format)
  {
    "Datastream": { "@iot.id": "datastream-123" },
    "components": ["phenomenonTime", "result", "FeatureOfInterest/id"],
    "dataArray": [
      ["2025-11-05T10:00:00Z", 22.5, "foi-1"],
      ["2025-11-05T10:05:00Z", 22.7, "foi-1"],
      ["2025-11-05T10:10:00Z", 22.9, "foi-1"]
    ]
  }

Benefits:
- 70% smaller payload size
- Single transaction
- Atomic commit
```

### 7.2 Authentication Integration

Link SensorThings entities to Honua's existing auth system:

```csharp
// Host/Extensions/SensorThingsAuthorizationExtensions.cs
public static class SensorThingsAuthorizationExtensions
{
    public static IServiceCollection AddSensorThingsAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // User can only access their own Things
            options.AddPolicy("OwnThingAccess", policy =>
                policy.RequireAssertion(context =>
                {
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var thingUserId = context.Resource as string; // From Thing.Properties.userId
                    return userId == thingUserId;
                }));

            // Organization-level access for shared devices
            options.AddPolicy("OrganizationAccess", policy =>
                policy.RequireClaim("organization_id"));
        });

        return services;
    }
}

// Apply in handlers
public static async Task<IResult> GetThing(
    HttpContext context,
    ISensorThingsRepository repository,
    string id)
{
    var thing = await repository.GetThingAsync(id);
    if (thing == null)
        return Results.NotFound();

    // Check authorization
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var thingUserId = thing.Properties?.GetValueOrDefault("userId")?.ToString();

    if (thingUserId != userId)
        return Results.Forbid();

    return Results.Ok(thing);
}
```

### 7.3 Offline Sync Strategy

**Sync Protocol:**

```typescript
// Mobile app pseudocode
interface SyncRequest {
  thingId: string;
  sinceTimestamp?: string;  // Last successful sync
  syncBatchId: string;      // UUID for this sync
  observations: Observation[];
}

interface SyncResponse {
  serverTimestamp: string;
  observationsCreated: number;
  observationsUpdated: number;
  errors: SyncError[];
}

async function syncWithServer() {
  const localDb = await getLocalDatabase();
  const lastSync = await localDb.getLastSyncTime();

  // Get unsent observations
  const pendingObs = await localDb.getPendingObservations();

  const syncBatchId = uuid();

  const request: SyncRequest = {
    thingId: await getStoredThingId(),
    sinceTimestamp: lastSync,
    syncBatchId: syncBatchId,
    observations: pendingObs
  };

  try {
    const response = await fetch('/sta/v1.1/Sync', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${getJwtToken()}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    });

    const result: SyncResponse = await response.json();

    // Mark observations as synced
    await localDb.markObservationsSynced(syncBatchId);

    // Update last sync timestamp
    await localDb.setLastSyncTime(result.serverTimestamp);

    console.log(`Synced ${result.observationsCreated} observations`);

  } catch (error) {
    console.error('Sync failed:', error);
    // Observations remain in local DB for retry
  }
}
```

**Conflict Resolution:**
- Client timestamp < Server timestamp → Server wins
- Duplicate detection by (DatastreamId, PhenomenonTime, Result)
- Use `sync_batch_id` to track which batch an observation came from

---

## 8. Performance Optimizations

### 8.1 Caching Strategy

```csharp
// Core/SensorThings/SensorThingsCacheService.cs
public sealed class SensorThingsCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ISensorThingsRepository _repository;

    private const int ThingCacheDurationMinutes = 60;
    private const int DatastreamCacheDurationMinutes = 30;
    private const int ObservationCacheDurationMinutes = 5;

    public async Task<Thing?> GetThingCachedAsync(string id, CancellationToken ct = default)
    {
        var cacheKey = $"sta:thing:{id}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);

        if (cached != null)
            return JsonSerializer.Deserialize<Thing>(cached);

        var thing = await _repository.GetThingAsync(id, ct: ct);
        if (thing != null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(thing),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ThingCacheDurationMinutes)
                },
                ct);
        }

        return thing;
    }

    // Invalidate cache on updates
    public async Task InvalidateThingCacheAsync(string id, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"sta:thing:{id}", ct);
    }
}
```

### 8.2 Database Connection Pooling

Leverage Honua's existing `ConnectionPoolWarmupService`:

```csharp
// Extensions/SensorThingsServiceExtensions.cs
public static IServiceCollection AddSensorThings(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton<ISensorThingsRepository, PostgresSensorThingsRepository>();
    services.AddSingleton<SensorThingsCacheService>();

    // Warm up connection pool on startup
    services.AddHostedService<SensorThingsConnectionWarmupService>();

    return services;
}

public sealed class SensorThingsConnectionWarmupService : BackgroundService
{
    private readonly ISensorThingsRepository _repository;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Execute simple query to warm up connections
        await _repository.GetThingsAsync(new QueryOptions { Top = 1 }, stoppingToken);
    }
}
```

### 8.3 Bulk Insert Performance

**Benchmark Target:** 10,000 observations/second

Strategy:
1. Use PostgreSQL COPY for bulk inserts
2. Batch observations into partitions
3. Defer Datastream extent updates (async background job)
4. Use connection pooling

```csharp
// Async background job to update Datastream extents
public sealed class DatastreamExtentUpdateService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            // Update extents for all Datastreams modified in last 5 minutes
            await UpdateDatastreamExtents(stoppingToken);
        }
    }

    private async Task UpdateDatastreamExtents(CancellationToken ct)
    {
        const string sql = """
            UPDATE sta_datastreams d
            SET
                phenomenon_time_start = o.min_phen_time,
                phenomenon_time_end = o.max_phen_time,
                updated_at = now()
            FROM (
                SELECT
                    datastream_id,
                    MIN(phenomenon_time) as min_phen_time,
                    MAX(phenomenon_time) as max_phen_time
                FROM sta_observations
                WHERE server_timestamp > now() - interval '5 minutes'
                GROUP BY datastream_id
            ) o
            WHERE d.id = o.datastream_id;
            """;

        await _connection.ExecuteAsync(sql, cancellationToken: ct);
    }
}
```

---

## 9. Testing Strategy

### 9.1 Test Project Structure

```
Honua.Server.Core.Tests.SensorThings/
├── Unit/
│   ├── FilterTranslatorTests.cs
│   ├── DataArrayParserTests.cs
│   └── EntityValidationTests.cs
├── Integration/
│   ├── ThingRepositoryTests.cs
│   ├── ObservationRepositoryTests.cs
│   └── BatchOperationTests.cs
├── Api/
│   ├── SensorThingsEndpointTests.cs
│   ├── MobileWorkflowTests.cs
│   └── AuthorizationTests.cs
└── Performance/
    ├── BulkInsertBenchmarks.cs
    └── QueryPerformanceBenchmarks.cs
```

### 9.2 Sample Tests

```csharp
// Integration/ObservationRepositoryTests.cs
public sealed class ObservationRepositoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ObservationRepositoryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateObservation_ShouldPersistToDatabase()
    {
        // Arrange
        var repository = _factory.Services.GetRequiredService<ISensorThingsRepository>();

        var thing = await repository.CreateThingAsync(new Thing
        {
            Name = "Test Device",
            Description = "Test"
        });

        var datastream = await repository.CreateDatastreamAsync(new Datastream
        {
            Name = "Test Stream",
            ThingId = thing.Id,
            // ... other required fields
        });

        var observation = new Observation
        {
            PhenomenonTime = DateTime.UtcNow,
            Result = 22.5,
            DatastreamId = datastream.Id
        };

        // Act
        var created = await repository.CreateObservationAsync(observation);

        // Assert
        Assert.NotNull(created.Id);
        Assert.NotEqual(default, created.ServerTimestamp);

        var retrieved = await repository.GetObservationAsync(created.Id);
        Assert.Equal(22.5, retrieved.Result);
    }

    [Fact]
    public async Task CreateObservationsDataArray_ShouldHandleBulkInsert()
    {
        // Arrange
        var repository = _factory.Services.GetRequiredService<ISensorThingsRepository>();

        var request = new DataArrayRequest
        {
            Datastream = new EntityReference { Id = "datastream-123" },
            Components = ["phenomenonTime", "result"],
            DataArray = Enumerable.Range(0, 1000)
                .Select(i => new object[]
                {
                    DateTime.UtcNow.AddMinutes(i),
                    20.0 + i * 0.1
                })
                .ToList()
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var created = await repository.CreateObservationsDataArrayAsync(request);
        stopwatch.Stop();

        // Assert
        Assert.Equal(1000, created.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, "Should insert 1000 obs in <1s");
    }
}

// Api/MobileWorkflowTests.cs
public sealed class MobileWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task MobileApp_EndToEndWorkflow_ShouldSucceed()
    {
        // Simulate complete mobile app lifecycle
        var client = _factory.CreateClient();

        // 1. Create Thing (device registration)
        var thingResponse = await client.PostAsJsonAsync("/sta/v1.1/Things", new
        {
            name = "Test Mobile Device",
            properties = new { deviceType = "ios" },
            Locations = new[]
            {
                new
                {
                    name = "Initial Location",
                    encodingType = "application/geo+json",
                    location = new
                    {
                        type = "Point",
                        coordinates = new[] { -74.006, 40.7128 }
                    }
                }
            },
            Datastreams = new[]
            {
                new
                {
                    name = "Temperature",
                    observationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
                    unitOfMeasurement = new
                    {
                        name = "Celsius",
                        symbol = "°C",
                        definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
                    },
                    Sensor = new { name = "Thermometer" },
                    ObservedProperty = new { name = "Temperature" }
                }
            }
        });

        thingResponse.EnsureSuccessStatusCode();
        var thing = await thingResponse.Content.ReadFromJsonAsync<Thing>();
        Assert.NotNull(thing.Id);

        // 2. Record observations (simulate offline collection)
        var datastreamId = thing.Datastreams.First().Id;

        var observationsResponse = await client.PostAsJsonAsync("/sta/v1.1/CreateObservations", new
        {
            Datastream = new { @iot.id = datastreamId },
            components = new[] { "phenomenonTime", "result" },
            dataArray = new[]
            {
                new object[] { "2025-11-05T10:00:00Z", 22.5 },
                new object[] { "2025-11-05T10:05:00Z", 22.7 },
                new object[] { "2025-11-05T10:10:00Z", 22.9 }
            }
        });

        observationsResponse.EnsureSuccessStatusCode();

        // 3. Query observations back
        var queryResponse = await client.GetAsync(
            $"/sta/v1.1/Datastreams({datastreamId})/Observations?$orderby=phenomenonTime desc");

        queryResponse.EnsureSuccessStatusCode();
        var result = await queryResponse.Content.ReadFromJsonAsync<ODataResult<Observation>>();

        Assert.Equal(3, result.Value.Count);
        Assert.Equal(22.9, result.Value[0].Result);
    }
}
```

---

## 10. Deployment Considerations

### 10.1 Configuration

Add to `appsettings.json`:

```json
{
  "SensorThings": {
    "Enabled": true,
    "Version": "v1.1",
    "BasePath": "/sta/v1.1",
    "MqttEnabled": false,
    "BatchOperationsEnabled": true,
    "DeepInsertEnabled": true,
    "DataArrayEnabled": true,
    "OfflineSyncEnabled": true,
    "MaxBatchSize": 1000,
    "MaxObservationsPerRequest": 5000,
    "DataSourceId": "primary-postgres",
    "Storage": {
      "PartitionObservations": true,
      "PartitionStrategy": "monthly",
      "HistoricalLocationRetentionDays": 365,
      "ObservationRetentionDays": null
    }
  }
}
```

### 10.2 Migration Scripts

Create Honua CLI command for schema setup:

```bash
# Initialize SensorThings schema
honua sensorthings init --datasource primary-postgres

# Create partitions
honua sensorthings create-partitions --months-ahead 12

# Migrate existing data (if applicable)
honua sensorthings migrate --from legacy-db
```

### 10.3 Monitoring

Leverage Honua's OpenTelemetry integration:

```csharp
// Add SensorThings-specific metrics
public sealed class SensorThingsMetrics
{
    private readonly Counter<long> _observationsCreated;
    private readonly Histogram<double> _batchInsertDuration;
    private readonly Counter<long> _syncRequests;

    public SensorThingsMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.SensorThings");

        _observationsCreated = meter.CreateCounter<long>(
            "sta.observations.created",
            description: "Number of observations created");

        _batchInsertDuration = meter.CreateHistogram<double>(
            "sta.batch_insert.duration",
            unit: "ms",
            description: "Duration of batch insert operations");

        _syncRequests = meter.CreateCounter<long>(
            "sta.sync.requests",
            description: "Number of mobile sync requests");
    }

    public void RecordObservationCreated() => _observationsCreated.Add(1);
    public void RecordBatchInsert(double durationMs) => _batchInsertDuration.Record(durationMs);
    public void RecordSyncRequest() => _syncRequests.Add(1);
}
```

**Health Check:**
```csharp
public sealed class SensorThingsHealthCheck : IHealthCheck
{
    private readonly ISensorThingsRepository _repository;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            // Simple query to verify connectivity
            await _repository.GetThingsAsync(new QueryOptions { Top = 1 }, ct);
            return HealthCheckResult.Healthy("SensorThings API is operational");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SensorThings API is not operational", ex);
        }
    }
}
```

---

## 11. Future Enhancements

### 11.1 Phase 2: MQTT Support

Add real-time streaming for live sensor data:

```
MQTT Topics:
- v1.1/Datastreams(id)/Observations (subscribe for updates)
- v1.1/Things(id)/properties (subscribe for property changes)

Use MQTTnet library:
- Install-Package MQTTnet.AspNetCore
```

### 11.2 Phase 3: Tasking Extension

Implement OGC SensorThings API Part 2 (Tasking):
- Task entity
- TaskingCapability entity
- Actuator entity

Use cases:
- Send commands to field devices
- Trigger sensors remotely
- Configure sampling rates

### 11.3 Phase 4: STAplus Extension

Add STAplus 1.0 features:
- Campaign entity (field project organization)
- Platform entity (mobile platform metadata)
- Deployment entity (sensor deployment tracking)
- Relation entity (complex relationships)

---

## 12. Implementation Checklist

### Phase 1: Foundation (Week 1-2)
- [ ] Create database schema (PostgreSQL)
- [ ] Define metadata models (SensorThingsServiceDefinition, etc.)
- [ ] Implement core repository interface (ISensorThingsRepository)
- [ ] Add PostgreSQL provider implementation
- [ ] Create basic entity models (Thing, Datastream, Observation, etc.)
- [ ] Add service registration (dependency injection)
- [ ] Write unit tests for core functionality

### Phase 2: API Endpoints (Week 3-4)
- [ ] Implement minimal endpoints for all entities
- [ ] Add query parameter parsing ($filter, $expand, $select, etc.)
- [ ] Implement filter translation (OData to SQL)
- [ ] Add navigation property handlers
- [ ] Implement deep insert support
- [ ] Write API integration tests
- [ ] Add Swagger/OpenAPI documentation

### Phase 3: Mobile Optimizations (Week 5-6)
- [ ] Implement batch operations endpoint
- [ ] Add DataArray extension for bulk observations
- [ ] Create custom mobile sync endpoint
- [ ] Add authentication/authorization integration
- [ ] Implement caching layer
- [ ] Optimize bulk insert performance (target: 10k obs/sec)
- [ ] Write mobile workflow tests

### Phase 4: Polish & Deploy (Week 7-8)
- [ ] Add health checks
- [ ] Implement metrics/telemetry
- [ ] Create migration scripts
- [ ] Add Honua CLI commands
- [ ] Write deployment documentation
- [ ] Performance testing and optimization
- [ ] Security audit
- [ ] User acceptance testing with mobile app team

---

## 13. Success Metrics

- **API Conformance:** Pass OGC SensorThings API 1.1 conformance tests
- **Performance:**
  - Single observation insert: <50ms (p95)
  - Bulk insert (1000 obs): <1s
  - Query with filter: <200ms (p95)
- **Scalability:** Support 1M+ observations per device over time
- **Mobile UX:** Offline sync completes in <5s for 100 observations
- **Reliability:** 99.9% API uptime

---

## 14. References

- [OGC SensorThings API 1.1 Specification](https://docs.ogc.org/is/18-088/18-088.html)
- [OGC SensorThings API GitHub](https://github.com/opengeospatial/sensorthings)
- [SensorUp Developer Docs](https://developers.sensorup.com/docs/)
- [Honua Server Architecture](../architecture.md)
- [ISO 19156:2011 - Observations and Measurements](https://www.iso.org/standard/32574.html)

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Author:** Claude (Anthropic AI)
**Reviewed By:** [Pending]
