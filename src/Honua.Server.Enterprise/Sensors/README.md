# Sensors / IoT Module

Enterprise-tier feature for OGC SensorThings API v1.1 compliance and IoT data management.

## Overview

The Sensors/IoT module implements the OGC SensorThings API v1.1 standard, providing a complete solution for managing IoT sensor data, time series observations, and real-time data streams. It includes mobile-optimized extensions for offline sync, bulk uploads via DataArrays, and efficient time series storage with partitioning.

**Key Capabilities:**
- Full OGC SensorThings API v1.1 compliance
- Thing/Location/Sensor/ObservedProperty/Datastream/Observation entities
- Time series data with PostgreSQL partitioning (by month)
- DataArray extension for efficient bulk uploads (100,000+ observations)
- Mobile offline sync with conflict resolution
- Advanced filtering ($filter, $orderby, $expand, $select)
- Spatial and temporal queries
- Multi-tenant isolation

## Architecture

### OGC SensorThings Data Model

```
┌─────────────┐
│   Thing     │ ◄─── Physical or virtual entity (device, sensor platform)
└──────┬──────┘
       │ 1:N
       ├────────────────────────────────┐
       │                                │
       v                                v
┌─────────────┐                 ┌─────────────┐
│  Location   │                 │ Datastream  │ ◄─── Measurement channel
└─────────────┘                 └──────┬──────┘
       │                               │ N:1
       │                               ├──────────────────┐
       │                               │                  │
       │                               v                  v
       │                        ┌─────────────┐    ┌────────────────┐
       │                        │   Sensor    │    │ObservedProperty│
       │                        └─────────────┘    └────────────────┘
       │                               │
       v                               │ 1:N
┌──────────────────┐                  v
│HistoricalLocation│           ┌─────────────┐
└──────────────────┘           │ Observation │ ◄─── Individual measurement
                               └──────┬──────┘
                                      │ N:1
                                      v
                               ┌─────────────────┐
                               │FeatureOfInterest│ ◄─── Location of observation
                               └─────────────────┘
```

### System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   Client Applications                        │
│  • Mobile Apps (iOS, Android - offline sync)                │
│  • IoT Devices (ESP32, Raspberry Pi)                        │
│  • Web Dashboards (time series visualization)               │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│              SensorThings API Endpoints                      │
│  GET/POST /Things, /Locations, /Sensors, /Datastreams      │
│  GET/POST /Observations, /ObservedProperties               │
│  POST /Observations (DataArray detection)                   │
│  POST /Sync (mobile offline sync)                          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│           SensorThingsRepository                             │
│  • CRUD operations for all entity types                     │
│  • Query parsing ($filter, $orderby, $expand)              │
│  • Bulk insert optimization (DataArray)                     │
│  • Spatial/temporal filtering                               │
└────────────────────┬────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────┐
│              PostgreSQL + PostGIS                            │
│  • Entity tables (things, sensors, datastreams, etc.)      │
│  • Time series observations (partitioned by month)          │
│  • Spatial indexes (locations, observations)                │
│  • Automatic historical location tracking (triggers)        │
└─────────────────────────────────────────────────────────────┘
```

## OGC SensorThings Entities

### Thing

Represents a physical or virtual entity that is observed or measured.

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Display name
- `description` (string): Detailed description
- `properties` (object): Custom metadata (JSON)

**Navigation Properties:**
- `Locations` - Current locations of the Thing
- `HistoricalLocations` - Past locations
- `Datastreams` - Measurement channels

**Example:**
```json
{
  "id": "thing-001",
  "name": "Weather Station #42",
  "description": "Rooftop weather station at Building A",
  "properties": {
    "owner": "Facilities Department",
    "installation_date": "2024-01-15",
    "model": "WS-2000"
  }
}
```

### Location

Represents the last known location of a Thing (with geometry).

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Location name
- `description` (string): Description
- `encodingType` (string): "application/geo+json"
- `location` (object): GeoJSON geometry (Point, Polygon, etc.)

**Example:**
```json
{
  "id": "loc-001",
  "name": "Building A Rooftop",
  "description": "North corner of Building A roof",
  "encodingType": "application/geo+json",
  "location": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  }
}
```

### Sensor

Describes the instrument or procedure used to make observations.

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Sensor name
- `description` (string): Description
- `encodingType` (string): "application/pdf", "text/html", etc.
- `metadata` (string): Calibration data, datasheets, etc.

**Example:**
```json
{
  "id": "sensor-001",
  "name": "DHT22 Temperature/Humidity Sensor",
  "description": "Digital temperature and humidity sensor",
  "encodingType": "application/pdf",
  "metadata": "https://example.com/datasheets/dht22.pdf"
}
```

### ObservedProperty

Defines what physical property is being measured.

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Property name
- `definition` (string): URI to vocabulary definition
- `description` (string): Description

**Example:**
```json
{
  "id": "obs-prop-001",
  "name": "Air Temperature",
  "definition": "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature",
  "description": "Temperature of the ambient air"
}
```

### Datastream

Represents a collection of observations measuring a single ObservedProperty.

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Datastream name
- `description` (string): Description
- `observationType` (string): OM_Observation, OM_Measurement, etc.
- `unitOfMeasurement` (object): Unit details (name, symbol, definition)
- `observedArea` (object): Spatial extent (GeoJSON Polygon)
- `phenomenonTime` (string): Temporal extent (ISO 8601 interval)
- `resultTime` (string): Time range of results

**Navigation Properties:**
- `Thing` - Parent Thing
- `Sensor` - Sensor used
- `ObservedProperty` - Property measured
- `Observations` - Collection of observations

**Example:**
```json
{
  "id": "ds-001",
  "name": "Temperature Readings - WS-42",
  "description": "Hourly air temperature measurements",
  "observationType": "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
  "unitOfMeasurement": {
    "name": "Degree Celsius",
    "symbol": "°C",
    "definition": "http://unitsofmeasure.org/ucum.html#para-30"
  },
  "Thing@iot.navigationLink": "/Things('thing-001')",
  "Sensor@iot.navigationLink": "/Sensors('sensor-001')",
  "ObservedProperty@iot.navigationLink": "/ObservedProperties('obs-prop-001')"
}
```

### Observation

Individual measurement or estimate of an ObservedProperty.

**Properties:**
- `id` (string): Unique identifier
- `phenomenonTime` (datetime): When observation occurred
- `resultTime` (datetime): When result was generated
- `result` (any): Measured value (number, string, JSON object)
- `resultQuality` (string): Quality indicator (optional)
- `validTime` (interval): Validity period (optional)
- `parameters` (object): Environmental conditions (optional)

**Navigation Properties:**
- `Datastream` - Parent datastream
- `FeatureOfInterest` - Location observed

**Example:**
```json
{
  "id": "obs-001",
  "phenomenonTime": "2025-11-05T10:30:00Z",
  "resultTime": "2025-11-05T10:30:05Z",
  "result": 22.5,
  "resultQuality": "good",
  "parameters": {
    "humidity": 65,
    "pressure": 1013.25
  },
  "Datastream@iot.navigationLink": "/Datastreams('ds-001')"
}
```

### FeatureOfInterest

The location or feature where the observation was made.

**Properties:**
- `id` (string): Unique identifier
- `name` (string): Feature name
- `description` (string): Description
- `encodingType` (string): "application/geo+json"
- `feature` (object): GeoJSON geometry

**Example:**
```json
{
  "id": "foi-001",
  "name": "Sensor Location",
  "description": "Location where observation was recorded",
  "encodingType": "application/geo+json",
  "feature": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  }
}
```

## API Endpoints

### Service Root

**Endpoint:** `GET /sensorthings/v1.1`

**Response:**
```json
{
  "value": [
    {
      "name": "Things",
      "url": "https://api.honua.io/sensorthings/v1.1/Things"
    },
    {
      "name": "Locations",
      "url": "https://api.honua.io/sensorthings/v1.1/Locations"
    },
    {
      "name": "Sensors",
      "url": "https://api.honua.io/sensorthings/v1.1/Sensors"
    },
    {
      "name": "ObservedProperties",
      "url": "https://api.honua.io/sensorthings/v1.1/ObservedProperties"
    },
    {
      "name": "Datastreams",
      "url": "https://api.honua.io/sensorthings/v1.1/Datastreams"
    },
    {
      "name": "Observations",
      "url": "https://api.honua.io/sensorthings/v1.1/Observations"
    },
    {
      "name": "FeaturesOfInterest",
      "url": "https://api.honua.io/sensorthings/v1.1/FeaturesOfInterest"
    },
    {
      "name": "HistoricalLocations",
      "url": "https://api.honua.io/sensorthings/v1.1/HistoricalLocations"
    }
  ]
}
```

### CRUD Operations (All Entities)

**Get Collection:**
```
GET /sensorthings/v1.1/Things
GET /sensorthings/v1.1/Locations
GET /sensorthings/v1.1/Datastreams
GET /sensorthings/v1.1/Observations
```

**Get Entity:**
```
GET /sensorthings/v1.1/Things('thing-001')
GET /sensorthings/v1.1/Datastreams('ds-001')
```

**Create Entity:**
```
POST /sensorthings/v1.1/Things
POST /sensorthings/v1.1/Observations
```

**Update Entity:**
```
PATCH /sensorthings/v1.1/Things('thing-001')
PATCH /sensorthings/v1.1/Datastreams('ds-001')
```

**Delete Entity:**
```
DELETE /sensorthings/v1.1/Things('thing-001')
DELETE /sensorthings/v1.1/Observations('obs-001')
```

### Navigation Properties

**Get Related Entities:**
```
GET /sensorthings/v1.1/Things('thing-001')/Locations
GET /sensorthings/v1.1/Things('thing-001')/Datastreams
GET /sensorthings/v1.1/Datastreams('ds-001')/Observations
GET /sensorthings/v1.1/Datastreams('ds-001')/Thing
```

### Query Options

#### $filter (Filtering)

**Comparison Operators:**
```
GET /Observations?$filter=result gt 20
GET /Observations?$filter=phenomenonTime ge 2025-11-01T00:00:00Z
GET /Things?$filter=name eq 'Weather Station #42'
```

**Logical Operators:**
```
GET /Observations?$filter=result gt 20 and result lt 30
GET /Observations?$filter=result lt 0 or result gt 100
```

**String Functions:**
```
GET /Things?$filter=startswith(name, 'Weather')
GET /Things?$filter=contains(description, 'rooftop')
GET /Things?$filter=endswith(name, 'Station')
```

**Spatial Functions:**
```
GET /Observations?$filter=st_within(FeatureOfInterest/feature, geography'POLYGON((...))' )
GET /Locations?$filter=st_distance(location, geography'POINT(-122.4194 37.7749)') lt 1000
```

**Temporal Functions:**
```
GET /Observations?$filter=year(phenomenonTime) eq 2025
GET /Observations?$filter=month(phenomenonTime) eq 11
GET /Observations?$filter=hour(phenomenonTime) eq 10
```

#### $orderby (Sorting)

```
GET /Observations?$orderby=phenomenonTime desc
GET /Observations?$orderby=result asc
GET /Things?$orderby=name
GET /Observations?$orderby=phenomenonTime desc,result asc
```

#### $top and $skip (Pagination)

```
GET /Observations?$top=100
GET /Observations?$top=100&$skip=100
GET /Observations?$top=1000&$orderby=phenomenonTime desc
```

#### $expand (Include Related Entities)

```
GET /Things?$expand=Locations
GET /Things?$expand=Datastreams,Locations
GET /Datastreams?$expand=Thing,Sensor,ObservedProperty
GET /Observations?$expand=Datastream($expand=Thing)
```

#### $select (Choose Properties)

```
GET /Things?$select=name,description
GET /Observations?$select=phenomenonTime,result
GET /Datastreams?$select=name,unitOfMeasurement
```

#### Combined Queries

```
GET /Observations
  ?$filter=phenomenonTime ge 2025-11-01T00:00:00Z and result gt 20
  &$orderby=phenomenonTime desc
  &$top=100
  &$expand=Datastream($select=name)
  &$select=phenomenonTime,result
```

### DataArray Extension (Bulk Upload)

The DataArray extension allows efficient bulk upload of observations with minimal overhead.

**Endpoint:** `POST /sensorthings/v1.1/Observations`

**Standard Approach (Inefficient for bulk):**
```json
[
  {
    "phenomenonTime": "2025-11-05T10:00:00Z",
    "result": 22.5,
    "Datastream": {"@iot.id": "ds-001"}
  },
  {
    "phenomenonTime": "2025-11-05T10:01:00Z",
    "result": 22.6,
    "Datastream": {"@iot.id": "ds-001"}
  }
  // ... 1000 more
]
```

**DataArray Approach (Efficient):**
```json
{
  "dataArray": [
    {
      "Datastream": {"@iot.id": "ds-001"},
      "components": [
        "phenomenonTime",
        "result"
      ],
      "dataArray": [
        ["2025-11-05T10:00:00Z", 22.5],
        ["2025-11-05T10:01:00Z", 22.6],
        ["2025-11-05T10:02:00Z", 22.7],
        ["2025-11-05T10:03:00Z", 22.8]
        // ... 10,000 more rows
      ]
    }
  ]
}
```

**Performance:**
- Standard: ~50 observations/second (individual INSERTs)
- DataArray: ~10,000 observations/second (bulk INSERT)

**Database Implementation:**
```sql
-- Bulk insert via COPY or multi-row INSERT
INSERT INTO observations (id, phenomenon_time, result, datastream_id, created_at)
SELECT
    gen_random_uuid(),
    (data->>0)::timestamptz,
    (data->>1)::double precision,
    @datastream_id,
    NOW()
FROM json_array_elements(@data_array) AS data;
```

### Mobile Offline Sync

**Endpoint:** `POST /sensorthings/v1.1/Sync`

**Request:**
```json
{
  "device_id": "mobile-123",
  "sync_batch_id": "sync-456",
  "last_sync_timestamp": "2025-11-05T08:00:00Z",
  "observations": [
    {
      "client_id": "local-001",
      "client_timestamp": "2025-11-05T09:15:23Z",
      "datastream_id": "ds-001",
      "phenomenon_time": "2025-11-05T09:15:20Z",
      "result": 22.5,
      "location": {
        "type": "Point",
        "coordinates": [-122.4194, 37.7749]
      }
    }
    // ... 500 more offline observations
  ]
}
```

**Response:**
```json
{
  "sync_batch_id": "sync-456",
  "server_timestamp": "2025-11-05T10:30:00Z",
  "observations_created": 501,
  "observations_failed": 0,
  "conflicts": [],
  "mappings": [
    {
      "client_id": "local-001",
      "server_id": "obs-12345"
    }
    // ... mappings for all observations
  ]
}
```

## Database Schema

### Entity Tables

```sql
-- Things
CREATE TABLE st_things (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    properties JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Locations
CREATE TABLE st_locations (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    encoding_type TEXT NOT NULL DEFAULT 'application/geo+json',
    location GEOMETRY(Geometry, 4326) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_st_locations_geometry ON st_locations USING GIST(location);

-- Thing-Location association (many-to-many)
CREATE TABLE st_thing_locations (
    thing_id TEXT NOT NULL REFERENCES st_things(id) ON DELETE CASCADE,
    location_id TEXT NOT NULL REFERENCES st_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (thing_id, location_id)
);

-- Sensors
CREATE TABLE st_sensors (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    encoding_type TEXT NOT NULL,
    metadata TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Observed Properties
CREATE TABLE st_observed_properties (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    definition TEXT NOT NULL,
    description TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Datastreams
CREATE TABLE st_datastreams (
    id TEXT PRIMARY KEY,
    thing_id TEXT NOT NULL REFERENCES st_things(id) ON DELETE CASCADE,
    sensor_id TEXT NOT NULL REFERENCES st_sensors(id),
    observed_property_id TEXT NOT NULL REFERENCES st_observed_properties(id),
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    observation_type TEXT NOT NULL,
    unit_of_measurement JSONB NOT NULL,
    observed_area GEOMETRY(Polygon, 4326),
    phenomenon_time_start TIMESTAMPTZ,
    phenomenon_time_end TIMESTAMPTZ,
    result_time_start TIMESTAMPTZ,
    result_time_end TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_st_datastreams_thing ON st_datastreams(thing_id);
CREATE INDEX idx_st_datastreams_sensor ON st_datastreams(sensor_id);
CREATE INDEX idx_st_datastreams_observed_property ON st_datastreams(observed_property_id);

-- Features of Interest
CREATE TABLE st_features_of_interest (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    encoding_type TEXT NOT NULL DEFAULT 'application/geo+json',
    feature GEOMETRY(Geometry, 4326) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_st_foi_feature ON st_features_of_interest USING GIST(feature);
```

### Time Series Observations (Partitioned)

```sql
-- Observations table (partitioned by month)
CREATE TABLE st_observations (
    id TEXT NOT NULL,
    datastream_id TEXT NOT NULL REFERENCES st_datastreams(id) ON DELETE CASCADE,
    foi_id TEXT REFERENCES st_features_of_interest(id),
    phenomenon_time TIMESTAMPTZ NOT NULL,
    result_time TIMESTAMPTZ,
    result JSONB NOT NULL,
    result_quality TEXT,
    valid_time_start TIMESTAMPTZ,
    valid_time_end TIMESTAMPTZ,
    parameters JSONB,
    client_timestamp TIMESTAMPTZ,
    server_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    sync_batch_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, phenomenon_time)
) PARTITION BY RANGE (phenomenon_time);

-- Create partitions (monthly)
CREATE TABLE st_observations_2025_11 PARTITION OF st_observations
    FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');

CREATE TABLE st_observations_2025_12 PARTITION OF st_observations
    FOR VALUES FROM ('2025-12-01') TO ('2026-01-01');

-- Indexes on partitions
CREATE INDEX idx_st_obs_2025_11_datastream ON st_observations_2025_11(datastream_id, phenomenon_time DESC);
CREATE INDEX idx_st_obs_2025_11_phenomenon_time ON st_observations_2025_11(phenomenon_time DESC);
CREATE INDEX idx_st_obs_2025_11_foi ON st_observations_2025_11(foi_id);

-- Historical Locations (automatic tracking)
CREATE TABLE st_historical_locations (
    id TEXT PRIMARY KEY,
    thing_id TEXT NOT NULL REFERENCES st_things(id) ON DELETE CASCADE,
    time TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE st_historical_location_locations (
    historical_location_id TEXT NOT NULL REFERENCES st_historical_locations(id) ON DELETE CASCADE,
    location_id TEXT NOT NULL REFERENCES st_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (historical_location_id, location_id)
);
```

### Automatic Historical Location Tracking

```sql
-- Trigger to create historical location when thing location changes
CREATE OR REPLACE FUNCTION track_thing_location_change()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO st_historical_locations (id, thing_id, time)
    VALUES (gen_random_uuid()::text, NEW.thing_id, NOW())
    RETURNING id INTO @hl_id;

    INSERT INTO st_historical_location_locations (historical_location_id, location_id)
    VALUES (@hl_id, NEW.location_id);

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_thing_location_change
    AFTER INSERT ON st_thing_locations
    FOR EACH ROW
    EXECUTE FUNCTION track_thing_location_change();
```

## Time Series Partitioning

### Partitioning Strategy

**Monthly Partitions:**
- Each month gets dedicated partition
- Queries hitting single month stay on single partition
- Old partitions can be archived/dropped
- Reduces index size and improves query performance

**Automatic Partition Creation:**

```sql
-- Scheduled job (runs monthly)
CREATE OR REPLACE FUNCTION create_next_month_partition()
RETURNS void AS $$
DECLARE
    next_month date := date_trunc('month', NOW() + INTERVAL '1 month');
    partition_name text := 'st_observations_' || to_char(next_month, 'YYYY_MM');
    start_date text := to_char(next_month, 'YYYY-MM-DD');
    end_date text := to_char(next_month + INTERVAL '1 month', 'YYYY-MM-DD');
BEGIN
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF st_observations FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );

    EXECUTE format('CREATE INDEX idx_%I_datastream ON %I(datastream_id, phenomenon_time DESC)', partition_name, partition_name);
    EXECUTE format('CREATE INDEX idx_%I_phenomenon_time ON %I(phenomenon_time DESC)', partition_name, partition_name);
END;
$$ LANGUAGE plpgsql;

-- Schedule via pg_cron or external scheduler
SELECT cron.schedule('create-partitions', '0 0 1 * *', 'SELECT create_next_month_partition()');
```

### Partition Management

**Drop Old Partitions:**
```sql
-- Archive observations > 2 years old
DROP TABLE st_observations_2023_01;
DROP TABLE st_observations_2023_02;
```

**Attach/Detach for Maintenance:**
```sql
-- Detach partition for bulk load
ALTER TABLE st_observations DETACH PARTITION st_observations_2025_11;

-- Bulk load data
COPY st_observations_2025_11 FROM '/data/observations_2025_11.csv' CSV;

-- Re-attach
ALTER TABLE st_observations ATTACH PARTITION st_observations_2025_11
    FOR VALUES FROM ('2025-11-01') TO ('2025-12-01');
```

## Query Performance Optimization

### Partition Pruning

```sql
-- Query hits single partition (fast)
SELECT *
FROM st_observations
WHERE phenomenon_time >= '2025-11-01'
  AND phenomenon_time < '2025-11-02'
  AND datastream_id = 'ds-001'
ORDER BY phenomenon_time DESC;

-- Query plan shows partition pruning:
-- Seq Scan on st_observations_2025_11
```

### Covering Indexes

```sql
-- Index includes all queried columns (index-only scan)
CREATE INDEX idx_st_obs_covering ON st_observations_2025_11
    (datastream_id, phenomenon_time DESC)
    INCLUDE (result, result_quality);
```

### Materialized Views (Aggregations)

```sql
-- Hourly aggregations
CREATE MATERIALIZED VIEW st_observations_hourly AS
SELECT
    datastream_id,
    date_trunc('hour', phenomenon_time) as hour,
    COUNT(*) as observation_count,
    AVG((result->>'value')::double precision) as avg_value,
    MIN((result->>'value')::double precision) as min_value,
    MAX((result->>'value')::double precision) as max_value,
    STDDEV((result->>'value')::double precision) as stddev_value
FROM st_observations
WHERE phenomenon_time > NOW() - INTERVAL '7 days'
GROUP BY datastream_id, date_trunc('hour', phenomenon_time);

CREATE INDEX idx_st_obs_hourly ON st_observations_hourly(datastream_id, hour DESC);

-- Refresh hourly
REFRESH MATERIALIZED VIEW CONCURRENTLY st_observations_hourly;
```

## Usage Examples

### Create Weather Station Setup

```bash
# 1. Create Thing (weather station)
curl -X POST https://api.honua.io/sensorthings/v1.1/Things \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Rooftop Weather Station",
    "description": "Wireless weather station on Building A roof",
    "properties": {
      "model": "WS-2000",
      "serial": "WS2000-12345"
    }
  }'

# Response: {"id": "thing-001", ...}

# 2. Create Location
curl -X POST https://api.honua.io/sensorthings/v1.1/Locations \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Building A Rooftop",
    "description": "North corner",
    "encodingType": "application/geo+json",
    "location": {
      "type": "Point",
      "coordinates": [-122.4194, 37.7749]
    },
    "Things": [{"@iot.id": "thing-001"}]
  }'

# 3. Create Sensor
curl -X POST https://api.honua.io/sensorthings/v1.1/Sensors \
  -H "Content-Type: application/json" \
  -d '{
    "name": "DHT22 Temperature Sensor",
    "description": "Digital temp/humidity sensor",
    "encodingType": "application/pdf",
    "metadata": "https://example.com/dht22-datasheet.pdf"
  }'

# Response: {"id": "sensor-001", ...}

# 4. Create ObservedProperty
curl -X POST https://api.honua.io/sensorthings/v1.1/ObservedProperties \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Air Temperature",
    "definition": "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature",
    "description": "Ambient air temperature"
  }'

# Response: {"id": "obs-prop-001", ...}

# 5. Create Datastream
curl -X POST https://api.honua.io/sensorthings/v1.1/Datastreams \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Temperature - WS Rooftop",
    "description": "Hourly temperature readings",
    "observationType": "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
    "unitOfMeasurement": {
      "name": "Degree Celsius",
      "symbol": "°C",
      "definition": "http://unitsofmeasure.org/ucum.html#para-30"
    },
    "Thing": {"@iot.id": "thing-001"},
    "Sensor": {"@iot.id": "sensor-001"},
    "ObservedProperty": {"@iot.id": "obs-prop-001"}
  }'

# Response: {"id": "ds-001", ...}
```

### Post Single Observation

```bash
curl -X POST https://api.honua.io/sensorthings/v1.1/Observations \
  -H "Content-Type: application/json" \
  -d '{
    "phenomenonTime": "2025-11-05T10:30:00Z",
    "result": 22.5,
    "Datastream": {"@iot.id": "ds-001"}
  }'
```

### Post Bulk Observations (DataArray)

```bash
curl -X POST https://api.honua.io/sensorthings/v1.1/Observations \
  -H "Content-Type: application/json" \
  -d '{
    "dataArray": [
      {
        "Datastream": {"@iot.id": "ds-001"},
        "components": ["phenomenonTime", "result"],
        "dataArray": [
          ["2025-11-05T00:00:00Z", 18.2],
          ["2025-11-05T01:00:00Z", 17.8],
          ["2025-11-05T02:00:00Z", 17.5],
          ["2025-11-05T03:00:00Z", 17.2]
        ]
      }
    ]
  }'
```

### Query Latest Observations

```bash
# Get last 100 observations for datastream
curl "https://api.honua.io/sensorthings/v1.1/Datastreams('ds-001')/Observations?\$orderby=phenomenonTime%20desc&\$top=100"

# Get observations from last 24 hours
curl "https://api.honua.io/sensorthings/v1.1/Observations?\$filter=phenomenonTime%20ge%20$(date -u -d '24 hours ago' '+%Y-%m-%dT%H:%M:%SZ')&\$orderby=phenomenonTime%20desc"

# Get observations with temperature > 25°C
curl "https://api.honua.io/sensorthings/v1.1/Observations?\$filter=result%20gt%2025&\$orderby=phenomenonTime%20desc"
```

### Expand Related Entities

```bash
# Get Things with their Locations and Datastreams
curl "https://api.honua.io/sensorthings/v1.1/Things?\$expand=Locations,Datastreams"

# Get Observations with nested expansion
curl "https://api.honua.io/sensorthings/v1.1/Observations?\$expand=Datastream(\$expand=Thing,Sensor,ObservedProperty)"
```

## Mobile Offline Sync Implementation

### Client-Side (Mobile App)

```javascript
// Collect observations while offline
const offlineObservations = [];

function recordObservation(datastreamId, value, location) {
  const obs = {
    client_id: generateUUID(),
    client_timestamp: new Date().toISOString(),
    datastream_id: datastreamId,
    phenomenon_time: new Date().toISOString(),
    result: value,
    location: location
  };

  offlineObservations.push(obs);
  saveToLocalStorage(obs);
}

// Sync when network available
async function syncObservations() {
  const lastSyncTimestamp = getLastSyncTimestamp();

  const response = await fetch('https://api.honua.io/sensorthings/v1.1/Sync', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({
      device_id: getDeviceId(),
      sync_batch_id: generateUUID(),
      last_sync_timestamp: lastSyncTimestamp,
      observations: offlineObservations
    })
  });

  const result = await response.json();

  // Update local IDs with server IDs
  result.mappings.forEach(mapping => {
    updateLocalObservation(mapping.client_id, mapping.server_id);
  });

  // Clear synced observations
  clearOfflineObservations();
  setLastSyncTimestamp(result.server_timestamp);

  return result;
}
```

### Server-Side Conflict Resolution

```csharp
public async Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct)
{
    var mappings = new List<ObservationMapping>();
    var conflicts = new List<SyncConflict>();

    foreach (var obs in request.Observations)
    {
        // Check for duplicate based on client_id + device_id
        var existing = await GetObservationByClientIdAsync(
            request.DeviceId,
            obs.ClientId,
            ct);

        if (existing != null)
        {
            // Conflict: already synced
            conflicts.Add(new SyncConflict
            {
                ClientId = obs.ClientId,
                Reason = "Already synced",
                ServerObservationId = existing.Id
            });
            continue;
        }

        // Create new observation
        var observation = new Observation
        {
            Id = Guid.NewGuid().ToString(),
            DatastreamId = obs.DatastreamId,
            PhenomenonTime = obs.PhenomenonTime,
            Result = obs.Result,
            ClientTimestamp = obs.ClientTimestamp,
            ServerTimestamp = DateTime.UtcNow,
            SyncBatchId = request.SyncBatchId
        };

        // Create FeatureOfInterest from location
        if (obs.Location != null)
        {
            var foi = await GetOrCreateFeatureOfInterestAsync(
                "Mobile observation location",
                "Location recorded by mobile device",
                obs.Location,
                ct);
            observation.FeatureOfInterestId = foi.Id;
        }

        await CreateObservationAsync(observation, ct);

        mappings.Add(new ObservationMapping
        {
            ClientId = obs.ClientId,
            ServerId = observation.Id
        });
    }

    return new SyncResponse
    {
        SyncBatchId = request.SyncBatchId,
        ServerTimestamp = DateTime.UtcNow,
        ObservationsCreated = mappings.Count,
        ObservationsFailed = conflicts.Count,
        Conflicts = conflicts,
        Mappings = mappings
    };
}
```

## Configuration

### appsettings.json

```json
{
  "SensorThings": {
    "BasePath": "/sensorthings/v1.1",
    "Enabled": true,
    "OfflineSyncEnabled": true,
    "MaxPageSize": 1000,
    "DefaultPageSize": 100,
    "EnableDataArrayExtension": true,
    "ConnectionString": "Host=postgres;Database=honua;...",
    "Partitioning": {
      "Enabled": true,
      "PartitionBy": "month",
      "RetentionMonths": 24
    }
  }
}
```

### Dependency Injection

```csharp
// In Program.cs
services.AddSensorThingsApi(configuration.GetSection("SensorThings"));

// Registers:
// - ISensorThingsRepository
// - SensorThingsEndpoints
// - Query parsers ($filter, $orderby, etc.)
```

## Monitoring and Analytics

### Key Metrics

```sql
-- Observation ingestion rate (observations/second)
SELECT
    COUNT(*) / EXTRACT(EPOCH FROM (MAX(created_at) - MIN(created_at))) as obs_per_second
FROM st_observations
WHERE created_at > NOW() - INTERVAL '1 hour';

-- Top datastreams by observation count
SELECT
    d.name,
    COUNT(o.*) as observation_count
FROM st_datastreams d
JOIN st_observations o ON o.datastream_id = d.id
WHERE o.created_at > NOW() - INTERVAL '24 hours'
GROUP BY d.id, d.name
ORDER BY observation_count DESC
LIMIT 10;

-- Storage per partition
SELECT
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE tablename LIKE 'st_observations_%'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

## Best Practices

1. **Use DataArray for Bulk:** Always use DataArray extension for > 100 observations
2. **Index Efficiently:** Create covering indexes for common queries
3. **Partition Time Series:** Enable monthly partitioning for observations
4. **Cache Metadata:** Cache Thing/Sensor/ObservedProperty lookups
5. **Batch Reads:** Use $expand to avoid N+1 queries
6. **Offline First:** Design mobile apps for offline-first data collection
7. **Validate Units:** Ensure consistent unitOfMeasurement across datastreams

## Troubleshooting

**Slow Observation Queries:**
- Verify partition pruning in query plan: `EXPLAIN SELECT ...`
- Check indexes exist on partition: `\d st_observations_2025_11`
- Use $filter on phenomenonTime to limit partition scan

**DataArray Not Working:**
- Ensure `EnableDataArrayExtension: true` in config
- Verify JSON structure matches spec
- Check server logs for parsing errors

**Sync Conflicts:**
- Review conflict resolution logic
- Check client_id + device_id uniqueness
- Verify timestamp formats (ISO 8601)

## Related Documentation

- [OGC SensorThings API v1.1 Specification](https://docs.ogc.org/is/18-088/18-088.html)
- [ENTERPRISE_FEATURES.md](/home/user/Honua.Server/src/Honua.Server.Enterprise/ENTERPRISE_FEATURES.md) - Enterprise features overview
- [Multitenancy Module](../Multitenancy/README.md) - Multi-tenant architecture
- [GeoEvent Module](../Events/README.md) - Geofencing and spatial events
