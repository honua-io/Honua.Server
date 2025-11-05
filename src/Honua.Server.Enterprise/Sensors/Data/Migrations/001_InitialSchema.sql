-- ============================================================================
-- OGC SensorThings API v1.1 Schema for PostgreSQL/PostGIS
-- ============================================================================
-- This migration creates the complete schema for the SensorThings API
-- including tables, indexes, triggers, and functions.
-- ============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS btree_gist;  -- For temporal indexing

-- ============================================================================
-- THINGS (Mobile devices/users)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_things (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    properties JSONB,

    -- Mobile-specific metadata
    device_type VARCHAR(50),          -- 'ios', 'android', 'web'
    device_model VARCHAR(255),
    app_version VARCHAR(50),
    user_id VARCHAR(255),             -- Reference to auth system
    organization_id VARCHAR(255),

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_things_user_id ON sta_things(user_id);
CREATE INDEX IF NOT EXISTS idx_sta_things_org_id ON sta_things(organization_id);
CREATE INDEX IF NOT EXISTS idx_sta_things_properties ON sta_things USING gin(properties);

COMMENT ON TABLE sta_things IS 'Things represent physical or virtual entities capable of network integration (OGC SensorThings API v1.1)';

-- ============================================================================
-- LOCATIONS (Geographic positions)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/geo+json',
    location GEOMETRY(Geometry, 4326) NOT NULL,  -- PostGIS geometry
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_locations_geom ON sta_locations USING gist(location);
CREATE INDEX IF NOT EXISTS idx_sta_locations_properties ON sta_locations USING gin(properties);

COMMENT ON TABLE sta_locations IS 'Locations describe the geographic position of Things (OGC SensorThings API v1.1)';

-- ============================================================================
-- THING-LOCATION Many-to-Many Relationship
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_thing_location (
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    location_id UUID NOT NULL REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (thing_id, location_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_thing_location_location ON sta_thing_location(location_id);

-- ============================================================================
-- HISTORICAL LOCATIONS (Location history)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_historical_locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    thing_id UUID NOT NULL REFERENCES sta_things(id) ON DELETE CASCADE,
    time TIMESTAMPTZ NOT NULL,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_hist_loc_thing_time ON sta_historical_locations(thing_id, time DESC);

COMMENT ON TABLE sta_historical_locations IS 'Historical locations track the movement history of Things (OGC SensorThings API v1.1)';

-- Link table for HistoricalLocation to Location (many-to-many)
CREATE TABLE IF NOT EXISTS sta_historical_location_location (
    historical_location_id UUID NOT NULL REFERENCES sta_historical_locations(id) ON DELETE CASCADE,
    location_id UUID NOT NULL REFERENCES sta_locations(id) ON DELETE CASCADE,
    PRIMARY KEY (historical_location_id, location_id)
);

-- ============================================================================
-- SENSORS (Measurement procedures/devices)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_sensors (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL,  -- 'application/pdf', 'http://www.opengis.net/doc/IS/SensorML/2.0'
    metadata TEXT NOT NULL,                -- URL or inline SensorML/PDF
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_sensors_properties ON sta_sensors USING gin(properties);

COMMENT ON TABLE sta_sensors IS 'Sensors are instruments that observe properties (OGC SensorThings API v1.1)';

-- ============================================================================
-- OBSERVED PROPERTIES (Phenomena being measured)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_observed_properties (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    definition TEXT NOT NULL,  -- URI to standard definition (QUDT, CF conventions, etc.)
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_sta_obs_prop_definition ON sta_observed_properties(definition);
CREATE INDEX IF NOT EXISTS idx_sta_obs_prop_properties ON sta_observed_properties USING gin(properties);

COMMENT ON TABLE sta_observed_properties IS 'Observed properties specify what phenomenon is being measured (OGC SensorThings API v1.1)';

-- ============================================================================
-- DATASTREAMS (Time series of observations)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_datastreams (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
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

    -- Derived spatial/temporal extent (updated by trigger or background job)
    observed_area GEOMETRY(Geometry, 4326),
    phenomenon_time_start TIMESTAMPTZ,
    phenomenon_time_end TIMESTAMPTZ,
    result_time_start TIMESTAMPTZ,
    result_time_end TIMESTAMPTZ,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_datastreams_thing ON sta_datastreams(thing_id);
CREATE INDEX IF NOT EXISTS idx_sta_datastreams_sensor ON sta_datastreams(sensor_id);
CREATE INDEX IF NOT EXISTS idx_sta_datastreams_obs_prop ON sta_datastreams(observed_property_id);
CREATE INDEX IF NOT EXISTS idx_sta_datastreams_phen_time ON sta_datastreams USING gist(
    tstzrange(phenomenon_time_start, phenomenon_time_end, '[]')
) WHERE phenomenon_time_start IS NOT NULL AND phenomenon_time_end IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_sta_datastreams_observed_area ON sta_datastreams USING gist(observed_area) WHERE observed_area IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_sta_datastreams_properties ON sta_datastreams USING gin(properties);

COMMENT ON TABLE sta_datastreams IS 'Datastreams group observations by Thing, Sensor, and ObservedProperty (OGC SensorThings API v1.1)';

-- ============================================================================
-- FEATURES OF INTEREST (What is being observed)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_features_of_interest (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    encoding_type VARCHAR(100) NOT NULL DEFAULT 'application/geo+json',
    feature GEOMETRY(Geometry, 4326) NOT NULL,
    properties JSONB,

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_sta_foi_geom ON sta_features_of_interest USING gist(feature);
CREATE INDEX IF NOT EXISTS idx_sta_foi_properties ON sta_features_of_interest USING gin(properties);

COMMENT ON TABLE sta_features_of_interest IS 'Features of Interest are the targets of observations (OGC SensorThings API v1.1)';

-- ============================================================================
-- OBSERVATIONS (Individual measurements) - PARTITIONED FOR SCALE
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_observations (
    id UUID NOT NULL DEFAULT uuid_generate_v4(),
    phenomenon_time TIMESTAMPTZ NOT NULL,
    result_time TIMESTAMPTZ,
    result JSONB NOT NULL,  -- Flexible type: number, string, boolean, complex object
    result_quality TEXT,
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

    PRIMARY KEY (id, phenomenon_time)
) PARTITION BY RANGE (phenomenon_time);

COMMENT ON TABLE sta_observations IS 'Observations are measurements of observed properties (OGC SensorThings API v1.1)';

-- Create initial partitions (for current and next month)
DO $$
DECLARE
    start_date DATE;
    end_date DATE;
    partition_name TEXT;
BEGIN
    -- Current month
    start_date := date_trunc('month', now());
    end_date := start_date + interval '1 month';
    partition_name := 'sta_observations_' || to_char(start_date, 'YYYY_MM');

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF sta_observations FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );

    -- Next month
    start_date := end_date;
    end_date := start_date + interval '1 month';
    partition_name := 'sta_observations_' || to_char(start_date, 'YYYY_MM');

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF sta_observations FOR VALUES FROM (%L) TO (%L)',
        partition_name, start_date, end_date
    );
END $$;

-- Indexes on base table (inherited by partitions)
CREATE INDEX IF NOT EXISTS idx_sta_obs_datastream ON sta_observations(datastream_id);
CREATE INDEX IF NOT EXISTS idx_sta_obs_foi ON sta_observations(feature_of_interest_id);
CREATE INDEX IF NOT EXISTS idx_sta_obs_phen_time ON sta_observations(phenomenon_time DESC);
CREATE INDEX IF NOT EXISTS idx_sta_obs_result_time ON sta_observations(result_time DESC) WHERE result_time IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_sta_obs_server_time ON sta_observations(server_timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_sta_obs_sync_batch ON sta_observations(sync_batch_id) WHERE sync_batch_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_sta_obs_result ON sta_observations USING gin(result);
CREATE INDEX IF NOT EXISTS idx_sta_obs_parameters ON sta_observations USING gin(parameters) WHERE parameters IS NOT NULL;

-- Composite index for common mobile sync queries
CREATE INDEX IF NOT EXISTS idx_sta_obs_datastream_time ON sta_observations(datastream_id, phenomenon_time DESC);

-- ============================================================================
-- FUNCTIONS FOR PARTITION MANAGEMENT
-- ============================================================================

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

        -- Create partition if it doesn't exist
        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF sta_observations FOR VALUES FROM (%L) TO (%L)',
            partition_name, start_date, end_date
        );

        RAISE NOTICE 'Created/verified partition: %', partition_name;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION create_observation_partitions IS 'Creates monthly partitions for observations table (call periodically to create future partitions)';

-- ============================================================================
-- FUNCTION: Get or Create FeatureOfInterest
-- ============================================================================

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

COMMENT ON FUNCTION get_or_create_foi IS 'Gets an existing FeatureOfInterest by geometry or creates a new one';

-- ============================================================================
-- TRIGGER: Create HistoricalLocation when Thing location changes
-- ============================================================================

CREATE OR REPLACE FUNCTION trg_create_historical_location()
RETURNS TRIGGER AS $$
DECLARE
    v_hist_loc_id UUID;
BEGIN
    -- Insert new HistoricalLocation
    INSERT INTO sta_historical_locations (thing_id, time)
    VALUES (NEW.thing_id, now())
    RETURNING id INTO v_hist_loc_id;

    -- Link to Location
    INSERT INTO sta_historical_location_location (historical_location_id, location_id)
    VALUES (v_hist_loc_id, NEW.location_id);

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_thing_location_insert ON sta_thing_location;
CREATE TRIGGER trg_thing_location_insert
AFTER INSERT ON sta_thing_location
FOR EACH ROW
EXECUTE FUNCTION trg_create_historical_location();

-- ============================================================================
-- TRIGGER: Update timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION trg_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply to all tables with updated_at column
DO $$
DECLARE
    tbl TEXT;
BEGIN
    FOR tbl IN
        SELECT table_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
        AND column_name = 'updated_at'
        AND table_name LIKE 'sta_%'
    LOOP
        EXECUTE format('
            DROP TRIGGER IF EXISTS trg_update_timestamp ON %I;
            CREATE TRIGGER trg_update_timestamp
            BEFORE UPDATE ON %I
            FOR EACH ROW
            EXECUTE FUNCTION trg_update_timestamp();
        ', tbl, tbl);
    END LOOP;
END $$;

-- ============================================================================
-- VIEWS FOR CONVENIENCE
-- ============================================================================

CREATE OR REPLACE VIEW vw_sta_observations_flat AS
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

COMMENT ON VIEW vw_sta_observations_flat IS 'Flattened view of observations with all related entities for easy querying';

-- ============================================================================
-- GRANTS (adjust based on your security model)
-- ============================================================================
-- Example:
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO sta_app_role;
-- GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO sta_app_role;
-- GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO sta_app_role;

-- ============================================================================
-- Migration complete
-- ============================================================================
