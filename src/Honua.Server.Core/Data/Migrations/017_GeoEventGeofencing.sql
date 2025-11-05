-- ============================================================================
-- Honua GIS Server - GeoEvent Geofencing
-- ============================================================================
-- Version: 017
-- Description: GeoEvent geofencing tables for real-time spatial event processing
-- Dependencies: PostGIS 3.0+, Migration 015 (3D geometry), Migration 016 (SensorThings)
-- Related: Honua.Server.Enterprise.Events namespace
-- ============================================================================
-- PURPOSE:
-- Enable real-time geofencing with enter/exit event detection for fleet
-- tracking, asset monitoring, and location-based workflows.
-- ============================================================================

-- ============================================================================
-- Table: geofences
-- ============================================================================
-- Stores geofence boundary polygons

CREATE TABLE IF NOT EXISTS geofences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,

    -- Polygon geometry (2D for MVP, 3D in Phase 3)
    geometry geometry(Polygon, 4326) NOT NULL,

    -- Additional metadata as JSON
    properties JSONB,

    -- Event types enabled for this geofence (bitmask)
    -- 1 = Enter, 2 = Exit, 4 = Dwell (Phase 2), 8 = Approach (Phase 2)
    enabled_event_types INT NOT NULL DEFAULT 3,  -- Enter | Exit

    -- Whether this geofence is active
    is_active BOOLEAN NOT NULL DEFAULT true,

    -- Multi-tenancy support
    tenant_id VARCHAR(100),

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by VARCHAR(255),
    updated_by VARCHAR(255),

    CONSTRAINT geofences_name_check CHECK (char_length(name) > 0)
);

-- Spatial index for geofence lookups (GIST for polygon containment queries)
CREATE INDEX IF NOT EXISTS idx_geofences_geometry
ON geofences USING GIST(geometry);

-- Index for active geofences (most queries filter on this)
CREATE INDEX IF NOT EXISTS idx_geofences_active
ON geofences(is_active)
WHERE is_active = true;

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofences_tenant
ON geofences(tenant_id)
WHERE tenant_id IS NOT NULL;

-- Composite index for common query pattern (active geofences by tenant)
CREATE INDEX IF NOT EXISTS idx_geofences_tenant_active
ON geofences(tenant_id, is_active)
WHERE tenant_id IS NOT NULL AND is_active = true;

COMMENT ON TABLE geofences IS 'GeoEvent: Geofence boundary definitions for spatial event detection';
COMMENT ON COLUMN geofences.geometry IS '2D polygon boundary (WGS84). 3D support in Phase 3';
COMMENT ON COLUMN geofences.enabled_event_types IS 'Bitmask: 1=Enter, 2=Exit, 4=Dwell, 8=Approach';
COMMENT ON COLUMN geofences.properties IS 'Additional metadata (speed_limit, zone_type, etc.)';

-- ============================================================================
-- Table: geofence_events
-- ============================================================================
-- Stores generated geofence events (enter, exit, etc.)

CREATE TABLE IF NOT EXISTS geofence_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Event type: enter, exit, dwell, approach
    event_type VARCHAR(20) NOT NULL,

    -- When the event occurred (from source event)
    event_time TIMESTAMPTZ NOT NULL,

    -- Geofence that triggered the event
    geofence_id UUID NOT NULL REFERENCES geofences(id) ON DELETE CASCADE,
    geofence_name VARCHAR(255) NOT NULL,  -- Denormalized for convenience

    -- Entity that triggered the event (vehicle ID, asset ID, etc.)
    entity_id VARCHAR(255) NOT NULL,
    entity_type VARCHAR(100),

    -- Location where event occurred (Point)
    location geometry(Point, 4326) NOT NULL,

    -- Entry/exit point on geofence boundary (optional)
    boundary_point geometry(Point, 4326),

    -- Additional properties from source event
    properties JSONB,

    -- Dwell time in seconds (for exit events)
    dwell_time_seconds INT,

    -- Optional link to SensorThings observation
    sensorthings_observation_id UUID,

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- When this event was processed by Honua
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT geofence_events_type_check CHECK (
        event_type IN ('enter', 'exit', 'dwell', 'approach')
    )
);

-- Index for querying events by geofence
CREATE INDEX IF NOT EXISTS idx_geofence_events_geofence
ON geofence_events(geofence_id, event_time DESC);

-- Index for querying events by entity
CREATE INDEX IF NOT EXISTS idx_geofence_events_entity
ON geofence_events(entity_id, event_time DESC);

-- Index for time-range queries
CREATE INDEX IF NOT EXISTS idx_geofence_events_time
ON geofence_events(event_time DESC);

-- BRIN index for time-series queries (very efficient for append-only data)
CREATE INDEX IF NOT EXISTS idx_geofence_events_time_brin
ON geofence_events USING BRIN(event_time);

-- Composite index for common query: entity events in geofence
CREATE INDEX IF NOT EXISTS idx_geofence_events_entity_geofence_time
ON geofence_events(entity_id, geofence_id, event_time DESC);

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofence_events_tenant
ON geofence_events(tenant_id, event_time DESC)
WHERE tenant_id IS NOT NULL;

-- Spatial index for location-based queries
CREATE INDEX IF NOT EXISTS idx_geofence_events_location
ON geofence_events USING GIST(location);

COMMENT ON TABLE geofence_events IS 'GeoEvent: Generated geofence events (enter, exit, dwell, approach)';
COMMENT ON COLUMN geofence_events.event_time IS 'When the event actually occurred (from source data)';
COMMENT ON COLUMN geofence_events.processed_at IS 'When Honua processed the event (system time)';
COMMENT ON COLUMN geofence_events.dwell_time_seconds IS 'Duration inside geofence (for exit events)';

-- ============================================================================
-- Table: entity_geofence_state
-- ============================================================================
-- Tracks current state of entities relative to geofences (for enter/exit detection)

CREATE TABLE IF NOT EXISTS entity_geofence_state (
    entity_id VARCHAR(255) NOT NULL,
    geofence_id UUID NOT NULL REFERENCES geofences(id) ON DELETE CASCADE,

    -- Whether entity is currently inside the geofence
    is_inside BOOLEAN NOT NULL,

    -- When entity entered (NULL if not inside)
    entered_at TIMESTAMPTZ,

    -- Last update timestamp
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    PRIMARY KEY (entity_id, geofence_id)
);

-- Index for querying all geofences an entity is inside
CREATE INDEX IF NOT EXISTS idx_entity_geofence_state_entity
ON entity_geofence_state(entity_id)
WHERE is_inside = true;

-- Index for querying all entities inside a geofence
CREATE INDEX IF NOT EXISTS idx_entity_geofence_state_geofence
ON entity_geofence_state(geofence_id)
WHERE is_inside = true;

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_entity_geofence_state_tenant
ON entity_geofence_state(tenant_id)
WHERE tenant_id IS NOT NULL;

-- Index for cleanup queries (find stale states)
CREATE INDEX IF NOT EXISTS idx_entity_geofence_state_last_updated
ON entity_geofence_state(last_updated)
WHERE is_inside = true;

COMMENT ON TABLE entity_geofence_state IS 'GeoEvent: Tracks which entities are currently inside which geofences';
COMMENT ON COLUMN entity_geofence_state.is_inside IS 'True if entity is currently inside geofence';
COMMENT ON COLUMN entity_geofence_state.entered_at IS 'Timestamp when entity entered geofence';
COMMENT ON COLUMN entity_geofence_state.last_updated IS 'Last location update for this entity/geofence pair';

-- ============================================================================
-- Function: Cleanup Stale Entity States
-- ============================================================================
-- Remove entity states that haven't been updated in 24 hours
-- Run this periodically via cron or scheduled job

CREATE OR REPLACE FUNCTION honua_cleanup_stale_entity_states(
    p_hours_old INT DEFAULT 24
)
RETURNS INT AS $$
DECLARE
    v_deleted_count INT;
BEGIN
    DELETE FROM entity_geofence_state
    WHERE last_updated < NOW() - (p_hours_old || ' hours')::INTERVAL;

    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;

    RETURN v_deleted_count;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION honua_cleanup_stale_entity_states IS
    'Cleanup entity states that haven''t been updated recently (default: 24 hours)';

-- ============================================================================
-- Triggers: Updated_at Timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_geofence_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_geofences_updated ON geofences;
CREATE TRIGGER trg_geofences_updated
    BEFORE UPDATE ON geofences
    FOR EACH ROW
    EXECUTE FUNCTION honua_geofence_update_timestamp();

-- ============================================================================
-- Helper Functions: Geofence Queries
-- ============================================================================

-- Function to find all active geofences containing a point
CREATE OR REPLACE FUNCTION honua_find_geofences_at_point(
    p_longitude DOUBLE PRECISION,
    p_latitude DOUBLE PRECISION,
    p_tenant_id VARCHAR(100) DEFAULT NULL
)
RETURNS TABLE(
    geofence_id UUID,
    geofence_name VARCHAR,
    properties JSONB
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        g.id,
        g.name,
        g.properties
    FROM geofences g
    WHERE g.is_active = true
      AND (p_tenant_id IS NULL OR g.tenant_id = p_tenant_id)
      AND ST_Contains(g.geometry, ST_SetSRID(ST_MakePoint(p_longitude, p_latitude), 4326));
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;

COMMENT ON FUNCTION honua_find_geofences_at_point IS
    'Find all active geofences containing a given point (optimized with spatial index)';

-- Function to get geofence statistics
CREATE OR REPLACE FUNCTION honua_get_geofence_stats(
    p_start_time TIMESTAMPTZ DEFAULT NOW() - INTERVAL '24 hours',
    p_end_time TIMESTAMPTZ DEFAULT NOW()
)
RETURNS TABLE(
    total_events BIGINT,
    enter_events BIGINT,
    exit_events BIGINT,
    unique_entities BIGINT,
    avg_dwell_time_seconds NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*) AS total_events,
        SUM(CASE WHEN event_type = 'enter' THEN 1 ELSE 0 END) AS enter_events,
        SUM(CASE WHEN event_type = 'exit' THEN 1 ELSE 0 END) AS exit_events,
        COUNT(DISTINCT entity_id) AS unique_entities,
        AVG(dwell_time_seconds) AS avg_dwell_time_seconds
    FROM geofence_events
    WHERE event_time BETWEEN p_start_time AND p_end_time;
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_get_geofence_stats IS
    'Get aggregate statistics for geofence events in a time range';

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. GIST spatial index enables fast point-in-polygon queries (< 10ms for 1k geofences)
-- 2. BRIN index on event_time provides excellent compression for time-series queries
-- 3. Partial indexes reduce index size and improve performance
-- 4. entity_geofence_state table enables O(1) enter/exit detection
-- 5. Consider partitioning geofence_events by time range for very large datasets (Phase 2)
-- ============================================================================

-- ============================================================================
-- Future Enhancements (Phase 2+)
-- ============================================================================
-- 1. Table partitioning for geofence_events (by month)
-- 2. Materialized views for common aggregations
-- 3. PostgreSQL LISTEN/NOTIFY for real-time event streaming
-- 4. Time-series compression (TimescaleDB extension)
-- 5. 3D geofencing (replace Polygon with PolyhedralSurface)
-- 6. Dwell detection (requires time-series state tracking)
-- ============================================================================

-- ============================================================================
-- Migration Complete
-- ============================================================================

-- Version tracking (insert into existing schema_version table)
INSERT INTO schema_version (version, description)
VALUES (17, 'GeoEvent geofencing tables for real-time spatial event processing')
ON CONFLICT (version) DO NOTHING;
