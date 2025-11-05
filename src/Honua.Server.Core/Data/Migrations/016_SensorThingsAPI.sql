-- ============================================================================
-- Honua GIS Server - OGC SensorThings API v1.1
-- ============================================================================
-- Version: 016
-- Description: Implementation of OGC SensorThings API v1.1 for IoT/sensor
--              observations integration
-- Dependencies: PostGIS 3.0+, Migration 015 (3D geometry)
-- Standard: OGC SensorThings API 1.1
-- Related: HONUA_COMPLETE_SYSTEM_DESIGN.md - Section 2.2
-- ============================================================================
-- PURPOSE:
-- Enable standards-based IoT/sensor data integration. Supports external
-- sensors (GNSS, weather, environmental), cellular sensors, and mobile
-- app sensor plugins.
-- ============================================================================
-- DATA MODEL:
-- Things → IoT devices/sensors
--   ├─ Locations → Geographic locations
--   └─ Datastreams → Time-series of measurements
--        ├─ Sensor → Instrument metadata
--        ├─ ObservedProperty → What's measured
--        └─ Observations → Individual readings
--             └─ FeatureOfInterest → Link to OGC Features
-- ============================================================================

-- ============================================================================
-- Table: sta_things
-- ============================================================================
-- Represents IoT devices, sensors, or any "Thing" that produces observations

CREATE TABLE IF NOT EXISTS sta_things (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    properties JSONB,

    -- Link to OGC Features API (optional)
    feature_id UUID REFERENCES features(id) ON DELETE SET NULL,

    -- Audit fields
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT sta_things_name_check CHECK (char_length(name) > 0)
);

CREATE INDEX IF NOT EXISTS idx_sta_things_feature_id
ON sta_things(feature_id)
WHERE feature_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_sta_things_created_at
ON sta_things(created_at DESC);

COMMENT ON TABLE sta_things IS 'SensorThings API: IoT devices, sensors, or physical objects that produce observations';
COMMENT ON COLUMN sta_things.feature_id IS 'Optional link to OGC Features API feature (e.g., utility pole with sensor)';
COMMENT ON COLUMN sta_things.properties IS 'Arbitrary metadata (manufacturer, model, serial number, etc.)';

-- ============================================================================
-- Table: sta_locations
-- ============================================================================
-- Geographic locations of Things (can have multiple locations over time)

CREATE TABLE IF NOT EXISTS sta_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255),
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/vnd.geo+json',
    location geometry(Point, 4326) NOT NULL,
    properties JSONB,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_sta_locations_location
ON sta_locations USING GIST(location);

COMMENT ON TABLE sta_locations IS 'SensorThings API: Geographic locations of Things';
COMMENT ON COLUMN sta_locations.encoding_type IS 'MIME type of location encoding (application/vnd.geo+json)';

-- ============================================================================
-- Table: sta_things_locations (Many-to-Many)
-- ============================================================================
-- A Thing can have multiple Locations, a Location can belong to multiple Things

CREATE TABLE IF NOT EXISTS sta_things_locations (
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    location_id UUID NOT NULL REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (thing_id, location_id)
);

CREATE INDEX IF NOT EXISTS idx_sta_things_locations_thing
ON sta_things_locations(thing_id);

CREATE INDEX IF NOT EXISTS idx_sta_things_locations_location
ON sta_things_locations(location_id);

-- ============================================================================
-- Table: sta_sensors
-- ============================================================================
-- Sensor instruments that produce observations

CREATE TABLE IF NOT EXISTS sta_sensors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL,
    metadata TEXT,  -- URL or inline metadata (PDF, SensorML, etc.)

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT sta_sensors_name_check CHECK (char_length(name) > 0)
);

COMMENT ON TABLE sta_sensors IS 'SensorThings API: Sensor instruments and their metadata';
COMMENT ON COLUMN sta_sensors.encoding_type IS 'MIME type of metadata (application/pdf, text/html, etc.)';
COMMENT ON COLUMN sta_sensors.metadata IS 'Sensor metadata (URL to datasheet or inline SensorML)';

-- ============================================================================
-- Table: sta_observed_properties
-- ============================================================================
-- What is being observed/measured (temperature, humidity, etc.)

CREATE TABLE IF NOT EXISTS sta_observed_properties (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    definition TEXT,  -- URI to standard definition (QUDT, CF conventions, etc.)
    description TEXT,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT sta_observed_properties_name_check CHECK (char_length(name) > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_sta_observed_properties_definition
ON sta_observed_properties(definition)
WHERE definition IS NOT NULL;

COMMENT ON TABLE sta_observed_properties IS 'SensorThings API: Observable properties (temperature, humidity, etc.)';
COMMENT ON COLUMN sta_observed_properties.definition IS 'URI to standard definition (e.g., http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#Temperature)';

-- ============================================================================
-- Table: sta_datastreams
-- ============================================================================
-- Time-series of observations from a sensor on a thing

CREATE TABLE IF NOT EXISTS sta_datastreams (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Relationships
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    sensor_id UUID NOT NULL REFERENCES sta_sensors(id),
    observed_property_id UUID NOT NULL REFERENCES sta_observed_properties(id),

    -- Core fields
    name VARCHAR(255) NOT NULL,
    description TEXT,
    observation_type VARCHAR(100) NOT NULL,
    unit_of_measurement JSONB NOT NULL,

    -- Spatial and temporal extent (auto-updated by observations)
    observed_area geometry(Polygon, 4326),
    phenomenon_time_start TIMESTAMPTZ,
    phenomenon_time_end TIMESTAMPTZ,
    result_time_start TIMESTAMPTZ,
    result_time_end TIMESTAMPTZ,

    -- Metadata
    properties JSONB,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT sta_datastreams_name_check CHECK (char_length(name) > 0),
    CONSTRAINT sta_datastreams_observation_type_check CHECK (
        observation_type IN (
            'OM_CategoryObservation',
            'OM_CountObservation',
            'OM_Measurement',
            'OM_Observation',
            'OM_TruthObservation'
        )
    )
);

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_thing
ON sta_datastreams(thing_id);

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_sensor
ON sta_datastreams(sensor_id);

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_observed_property
ON sta_datastreams(observed_property_id);

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_phenomenon_time
ON sta_datastreams(phenomenon_time_start, phenomenon_time_end)
WHERE phenomenon_time_start IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_observed_area
ON sta_datastreams USING GIST(observed_area)
WHERE observed_area IS NOT NULL;

COMMENT ON TABLE sta_datastreams IS 'SensorThings API: Time-series of observations from a sensor';
COMMENT ON COLUMN sta_datastreams.observation_type IS 'Type of observation result (OM_Measurement for numeric, OM_CategoryObservation for categorical, etc.)';
COMMENT ON COLUMN sta_datastreams.unit_of_measurement IS 'JSON object: {"name": "Celsius", "symbol": "°C", "definition": "..."}';
COMMENT ON COLUMN sta_datastreams.observed_area IS 'Spatial extent of all observations in this datastream (auto-updated)';
COMMENT ON COLUMN sta_datastreams.phenomenon_time_start IS 'Earliest phenomenon time of observations (auto-updated)';
COMMENT ON COLUMN sta_datastreams.phenomenon_time_end IS 'Latest phenomenon time of observations (auto-updated)';

-- ============================================================================
-- Table: sta_features_of_interest
-- ============================================================================
-- What feature is being observed (often same as Thing location, but can differ)

CREATE TABLE IF NOT EXISTS sta_features_of_interest (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255),
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/vnd.geo+json',
    feature JSONB NOT NULL,  -- GeoJSON feature

    -- Optional link to OGC Features API
    feature_id UUID REFERENCES features(id) ON DELETE SET NULL,

    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_sta_foi_feature_id
ON sta_features_of_interest(feature_id)
WHERE feature_id IS NOT NULL;

COMMENT ON TABLE sta_features_of_interest IS 'SensorThings API: Features being observed (what is observed, not where sensor is)';
COMMENT ON COLUMN sta_features_of_interest.feature IS 'GeoJSON feature object';
COMMENT ON COLUMN sta_features_of_interest.feature_id IS 'Optional link to OGC Features API feature';

-- ============================================================================
-- Table: sta_observations
-- ============================================================================
-- Individual sensor readings/measurements

CREATE TABLE IF NOT EXISTS sta_observations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Relationships
    datastream_id UUID NOT NULL REFERENCES sta_datastreams(id) ON DELETE CASCADE,
    feature_of_interest_id UUID REFERENCES sta_features_of_interest(id) ON DELETE SET NULL,

    -- Temporal fields
    phenomenon_time TIMESTAMPTZ NOT NULL,  -- When the observation was made
    result_time TIMESTAMPTZ DEFAULT NOW(), -- When the result was produced

    -- Result value (one of these will be populated based on observation_type)
    result_value NUMERIC,       -- For OM_Measurement, OM_CountObservation
    result_string TEXT,          -- For OM_CategoryObservation
    result_json JSONB,           -- For complex observations
    result_boolean BOOLEAN,      -- For OM_TruthObservation

    -- Quality metadata
    result_quality JSONB,        -- Quality indicators, accuracy, etc.
    valid_time TSTZRANGE,        -- Time period for which result is valid
    parameters JSONB,            -- Additional parameters

    created_at TIMESTAMPTZ DEFAULT NOW(),

    CONSTRAINT sta_observations_result_check CHECK (
        result_value IS NOT NULL OR
        result_string IS NOT NULL OR
        result_json IS NOT NULL OR
        result_boolean IS NOT NULL
    )
);

-- Time-series optimized indexes
CREATE INDEX IF NOT EXISTS idx_sta_observations_datastream_time
ON sta_observations(datastream_id, phenomenon_time DESC);

-- BRIN index for time-series queries (very efficient for time-ordered data)
CREATE INDEX IF NOT EXISTS idx_sta_observations_phenomenon_time_brin
ON sta_observations USING BRIN(phenomenon_time);

CREATE INDEX IF NOT EXISTS idx_sta_observations_result_time
ON sta_observations(result_time DESC);

CREATE INDEX IF NOT EXISTS idx_sta_observations_foi
ON sta_observations(feature_of_interest_id)
WHERE feature_of_interest_id IS NOT NULL;

-- Composite index for common query patterns
CREATE INDEX IF NOT EXISTS idx_sta_observations_datastream_foi_time
ON sta_observations(datastream_id, feature_of_interest_id, phenomenon_time DESC)
WHERE feature_of_interest_id IS NOT NULL;

COMMENT ON TABLE sta_observations IS 'SensorThings API: Individual sensor observations/measurements';
COMMENT ON COLUMN sta_observations.phenomenon_time IS 'When the observation was made (event time)';
COMMENT ON COLUMN sta_observations.result_time IS 'When the result was produced (processing time)';
COMMENT ON COLUMN sta_observations.result_quality IS 'Quality metadata: {"confidence": 0.95, "method": "GPS", ...}';
COMMENT ON COLUMN sta_observations.parameters IS 'Additional parameters: {"temperature_unit": "celsius", ...}';

-- ============================================================================
-- Triggers: Auto-update Datastream Extents
-- ============================================================================

-- Function to update datastream temporal and spatial extents
CREATE OR REPLACE FUNCTION honua_sta_update_datastream_extents()
RETURNS TRIGGER AS $$
BEGIN
    -- Update temporal extents
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
        updated_at = NOW()
    WHERE id = NEW.datastream_id;

    -- Update observed_area if feature_of_interest has geometry
    -- (Future enhancement: aggregate all FOI geometries)

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sta_update_datastream_extents ON sta_observations;
CREATE TRIGGER trg_sta_update_datastream_extents
    AFTER INSERT ON sta_observations
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_datastream_extents();

-- ============================================================================
-- Triggers: Updated_at Timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_sta_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply to all tables with updated_at
DROP TRIGGER IF EXISTS trg_sta_things_updated ON sta_things;
CREATE TRIGGER trg_sta_things_updated
    BEFORE UPDATE ON sta_things
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

DROP TRIGGER IF EXISTS trg_sta_locations_updated ON sta_locations;
CREATE TRIGGER trg_sta_locations_updated
    BEFORE UPDATE ON sta_locations
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

DROP TRIGGER IF EXISTS trg_sta_sensors_updated ON sta_sensors;
CREATE TRIGGER trg_sta_sensors_updated
    BEFORE UPDATE ON sta_sensors
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

DROP TRIGGER IF EXISTS trg_sta_observed_properties_updated ON sta_observed_properties;
CREATE TRIGGER trg_sta_observed_properties_updated
    BEFORE UPDATE ON sta_observed_properties
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

DROP TRIGGER IF EXISTS trg_sta_datastreams_updated ON sta_datastreams;
CREATE TRIGGER trg_sta_datastreams_updated
    BEFORE UPDATE ON sta_datastreams
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

DROP TRIGGER IF EXISTS trg_sta_foi_updated ON sta_features_of_interest;
CREATE TRIGGER trg_sta_foi_updated
    BEFORE UPDATE ON sta_features_of_interest
    FOR EACH ROW
    EXECUTE FUNCTION honua_sta_update_timestamp();

-- ============================================================================
-- Helper Functions: Query Optimization
-- ============================================================================

-- Function to get latest observation for a datastream
CREATE OR REPLACE FUNCTION honua_sta_get_latest_observation(
    p_datastream_id UUID
)
RETURNS TABLE(
    id UUID,
    phenomenon_time TIMESTAMPTZ,
    result_value NUMERIC,
    result_string TEXT,
    result_json JSONB,
    result_boolean BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.id,
        o.phenomenon_time,
        o.result_value,
        o.result_string,
        o.result_json,
        o.result_boolean
    FROM sta_observations o
    WHERE o.datastream_id = p_datastream_id
    ORDER BY o.phenomenon_time DESC
    LIMIT 1;
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;

COMMENT ON FUNCTION honua_sta_get_latest_observation IS 'Get most recent observation for a datastream (optimized with index)';

-- Function to get observation statistics for a datastream
CREATE OR REPLACE FUNCTION honua_sta_get_observation_stats(
    p_datastream_id UUID,
    p_start_time TIMESTAMPTZ DEFAULT NULL,
    p_end_time TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE(
    count BIGINT,
    min_value NUMERIC,
    max_value NUMERIC,
    avg_value NUMERIC,
    stddev_value NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::BIGINT,
        MIN(o.result_value),
        MAX(o.result_value),
        AVG(o.result_value),
        STDDEV(o.result_value)
    FROM sta_observations o
    WHERE o.datastream_id = p_datastream_id
      AND (p_start_time IS NULL OR o.phenomenon_time >= p_start_time)
      AND (p_end_time IS NULL OR o.phenomenon_time <= p_end_time)
      AND o.result_value IS NOT NULL;
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;

COMMENT ON FUNCTION honua_sta_get_observation_stats IS 'Get statistical summary of observations for a datastream';

-- ============================================================================
-- Sample Data for Testing (Optional - Remove in Production)
-- ============================================================================

-- Example: Weather station with temperature sensor
/*
-- Create Thing
INSERT INTO sta_things (name, description, properties)
VALUES (
    'Weather Station 001',
    'Field weather monitoring station',
    '{"manufacturer": "Davis", "model": "Vantage Pro2", "serial": "WS001"}'::jsonb
);

-- Create Location
INSERT INTO sta_locations (name, location)
VALUES (
    'Field Site A',
    ST_GeomFromText('POINT(-122.4194 37.7749)', 4326)
);

-- Link Thing to Location
INSERT INTO sta_things_locations (thing_id, location_id)
SELECT t.id, l.id
FROM sta_things t, sta_locations l
WHERE t.name = 'Weather Station 001' AND l.name = 'Field Site A';

-- Create Sensor
INSERT INTO sta_sensors (name, description, encoding_type, metadata)
VALUES (
    'DS18B20 Temperature Sensor',
    'Digital temperature sensor',
    'application/pdf',
    'http://example.com/datasheets/ds18b20.pdf'
);

-- Create ObservedProperty
INSERT INTO sta_observed_properties (name, definition, description)
VALUES (
    'Temperature',
    'http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#Temperature',
    'Air temperature'
);

-- Create Datastream
INSERT INTO sta_datastreams (thing_id, sensor_id, observed_property_id, name, observation_type, unit_of_measurement)
SELECT
    t.id,
    s.id,
    op.id,
    'Air Temperature Stream',
    'OM_Measurement',
    '{"name": "Celsius", "symbol": "°C", "definition": "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"}'::jsonb
FROM sta_things t, sta_sensors s, sta_observed_properties op
WHERE t.name = 'Weather Station 001'
  AND s.name = 'DS18B20 Temperature Sensor'
  AND op.name = 'Temperature';

-- Create Observations
INSERT INTO sta_observations (datastream_id, phenomenon_time, result_value)
SELECT
    ds.id,
    NOW() - INTERVAL '1 hour',
    23.5
FROM sta_datastreams ds
WHERE ds.name = 'Air Temperature Stream';
*/

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. BRIN index on phenomenon_time provides excellent compression and performance
--    for time-series data (especially for high-frequency sensors)
-- 2. Partial indexes reduce index size and improve performance
-- 3. Datastream extents auto-update via trigger (trade-off: write vs read perf)
-- 4. Consider partitioning sta_observations by time for very large datasets
-- 5. For high-volume sensors, batch inserts are recommended
-- ============================================================================

-- ============================================================================
-- Future Enhancements
-- ============================================================================
-- 1. Table partitioning for sta_observations (by time range)
-- 2. Materialized views for common aggregations
-- 3. MQTT support via PostgreSQL LISTEN/NOTIFY
-- 4. Time-series compression (TimescaleDB extension)
-- 5. Spatial aggregation of observed_area
-- ============================================================================

-- ============================================================================
-- Migration Complete
-- ============================================================================

-- Version tracking
INSERT INTO schema_version (version, description)
VALUES (16, 'OGC SensorThings API v1.1 implementation')
ON CONFLICT (version) DO NOTHING;
