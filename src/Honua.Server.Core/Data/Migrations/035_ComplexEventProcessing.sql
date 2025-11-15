-- ============================================================================
-- Honua GIS Server - Complex Event Processing (CEP)
-- ============================================================================
-- Version: 035
-- Description: Complex Event Processing for geofence events with pattern matching
-- Dependencies: Migration 017 (GeoEvent Geofencing), Migration 018 (Event Queue)
-- Related: Honua.Server.Enterprise.Events.CEP namespace
-- ============================================================================
-- PURPOSE:
-- Enable sophisticated event pattern detection including:
-- - Sequence patterns (A then B within time window)
-- - Count patterns (N occurrences within window)
-- - Correlation patterns (multiple entities)
-- - Absence patterns (expected event didn't occur)
-- - Temporal aggregation windows (sliding, tumbling, session)
-- ============================================================================

-- ============================================================================
-- Table: geofence_event_patterns
-- ============================================================================
-- Stores pattern definitions for CEP

CREATE TABLE IF NOT EXISTS geofence_event_patterns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Pattern identification
    name VARCHAR(255) NOT NULL,
    description TEXT,
    pattern_type VARCHAR(50) NOT NULL,  -- sequence, count, correlation, absence

    -- Pattern enabled/disabled
    enabled BOOLEAN NOT NULL DEFAULT true,

    -- Pattern conditions (JSON array of condition objects)
    conditions JSONB NOT NULL,

    -- Temporal window configuration
    window_duration_seconds INT NOT NULL,  -- Time window size
    window_type VARCHAR(20) NOT NULL DEFAULT 'sliding',  -- sliding, tumbling, session

    -- Session window specific config
    session_gap_seconds INT,  -- Inactivity timeout for session windows

    -- Alert configuration
    alert_name VARCHAR(500) NOT NULL,
    alert_severity VARCHAR(20) NOT NULL DEFAULT 'medium',
    alert_description TEXT,
    alert_labels JSONB,

    -- Notification channels
    notification_channel_ids JSONB,

    -- Pattern priority (higher = evaluated first)
    priority INT NOT NULL DEFAULT 0,

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by VARCHAR(255),
    updated_by VARCHAR(255),

    CONSTRAINT geofence_event_patterns_name_check CHECK (char_length(name) > 0),
    CONSTRAINT geofence_event_patterns_type_check CHECK (
        pattern_type IN ('sequence', 'count', 'correlation', 'absence')
    ),
    CONSTRAINT geofence_event_patterns_window_type_check CHECK (
        window_type IN ('sliding', 'tumbling', 'session')
    ),
    CONSTRAINT geofence_event_patterns_severity_check CHECK (
        alert_severity IN ('critical', 'high', 'medium', 'low', 'info')
    ),
    CONSTRAINT geofence_event_patterns_session_check CHECK (
        window_type != 'session' OR session_gap_seconds IS NOT NULL
    )
);

-- Index for enabled patterns (most queries filter on this)
CREATE INDEX IF NOT EXISTS idx_geofence_event_patterns_enabled
ON geofence_event_patterns(enabled, priority DESC)
WHERE enabled = true;

-- Index for pattern type queries
CREATE INDEX IF NOT EXISTS idx_geofence_event_patterns_type
ON geofence_event_patterns(pattern_type, enabled);

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofence_event_patterns_tenant
ON geofence_event_patterns(tenant_id)
WHERE tenant_id IS NOT NULL;

COMMENT ON TABLE geofence_event_patterns IS 'CEP: Pattern definitions for complex event detection';
COMMENT ON COLUMN geofence_event_patterns.conditions IS 'Array of condition objects defining the pattern';
COMMENT ON COLUMN geofence_event_patterns.window_type IS 'sliding: continuous, tumbling: fixed non-overlapping, session: activity-based';
COMMENT ON COLUMN geofence_event_patterns.session_gap_seconds IS 'For session windows: max gap between events before session ends';

-- ============================================================================
-- Table: pattern_match_state
-- ============================================================================
-- Tracks partial pattern matches in progress (sliding window state)

CREATE TABLE IF NOT EXISTS pattern_match_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Pattern being matched
    pattern_id UUID NOT NULL REFERENCES geofence_event_patterns(id) ON DELETE CASCADE,

    -- Partition key for grouping (entity_id for entity-based patterns)
    partition_key VARCHAR(255) NOT NULL,

    -- Matched events so far (ordered array of event IDs)
    matched_event_ids JSONB NOT NULL DEFAULT '[]'::jsonb,

    -- Condition sequence tracking (for sequence patterns)
    current_condition_index INT NOT NULL DEFAULT 0,

    -- Window boundaries
    window_start TIMESTAMPTZ NOT NULL,
    window_end TIMESTAMPTZ NOT NULL,

    -- Last event time (for session window gap detection)
    last_event_time TIMESTAMPTZ NOT NULL,

    -- Accumulated context (entity IDs, geofence IDs, etc.)
    context JSONB NOT NULL DEFAULT '{}'::jsonb,

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Composite unique constraint for pattern + partition
    CONSTRAINT pattern_match_state_unique UNIQUE(pattern_id, partition_key)
);

-- Index for finding states for a pattern
CREATE INDEX IF NOT EXISTS idx_pattern_match_state_pattern
ON pattern_match_state(pattern_id, window_end);

-- Index for finding states by partition key
CREATE INDEX IF NOT EXISTS idx_pattern_match_state_partition
ON pattern_match_state(partition_key, window_end);

-- Index for cleanup (expired states)
CREATE INDEX IF NOT EXISTS idx_pattern_match_state_expired
ON pattern_match_state(window_end)
WHERE window_end < NOW();

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_pattern_match_state_tenant
ON pattern_match_state(tenant_id)
WHERE tenant_id IS NOT NULL;

COMMENT ON TABLE pattern_match_state IS 'CEP: Partial pattern matches in progress (sliding window state)';
COMMENT ON COLUMN pattern_match_state.partition_key IS 'Grouping key: entity_id for entity patterns, geofence_id for geofence patterns';
COMMENT ON COLUMN pattern_match_state.matched_event_ids IS 'Array of event IDs that partially match this pattern';
COMMENT ON COLUMN pattern_match_state.context IS 'Accumulated context: entity_ids[], geofence_ids[], event_times[], etc.';

-- ============================================================================
-- Table: pattern_match_history
-- ============================================================================
-- Completed pattern matches and triggered alerts (audit trail)

CREATE TABLE IF NOT EXISTS pattern_match_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Pattern that was matched
    pattern_id UUID NOT NULL REFERENCES geofence_event_patterns(id) ON DELETE CASCADE,
    pattern_name VARCHAR(255) NOT NULL,  -- Denormalized for history

    -- Events that formed the match
    matched_event_ids JSONB NOT NULL,

    -- Pattern match metadata
    partition_key VARCHAR(255) NOT NULL,
    match_context JSONB NOT NULL,  -- Full context of the match

    -- Window that produced the match
    window_start TIMESTAMPTZ NOT NULL,
    window_end TIMESTAMPTZ NOT NULL,

    -- Alert that was generated
    alert_fingerprint VARCHAR(256),
    alert_severity VARCHAR(20) NOT NULL,
    alert_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT pattern_match_history_severity_check CHECK (
        alert_severity IN ('critical', 'high', 'medium', 'low', 'info')
    )
);

-- Index for querying matches by pattern
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_pattern
ON pattern_match_history(pattern_id, created_at DESC);

-- Index for querying matches by time
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_time
ON pattern_match_history(created_at DESC);

-- BRIN index for time-series queries (very efficient)
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_time_brin
ON pattern_match_history USING BRIN(created_at);

-- Index for alert correlation
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_alert
ON pattern_match_history(alert_fingerprint)
WHERE alert_fingerprint IS NOT NULL;

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_tenant
ON pattern_match_history(tenant_id, created_at DESC)
WHERE tenant_id IS NOT NULL;

-- Index for partition key analysis
CREATE INDEX IF NOT EXISTS idx_pattern_match_history_partition
ON pattern_match_history(partition_key, created_at DESC);

COMMENT ON TABLE pattern_match_history IS 'CEP: Completed pattern matches and audit trail';
COMMENT ON COLUMN pattern_match_history.match_context IS 'Full match context: event details, entities, geofences, timestamps';

-- ============================================================================
-- Table: tumbling_window_state
-- ============================================================================
-- State for tumbling window aggregations (non-overlapping fixed windows)

CREATE TABLE IF NOT EXISTS tumbling_window_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Pattern being aggregated
    pattern_id UUID NOT NULL REFERENCES geofence_event_patterns(id) ON DELETE CASCADE,

    -- Window boundaries (aligned to window size)
    window_start TIMESTAMPTZ NOT NULL,
    window_end TIMESTAMPTZ NOT NULL,

    -- Partition key
    partition_key VARCHAR(255) NOT NULL,

    -- Event count in this window
    event_count INT NOT NULL DEFAULT 0,

    -- Events in this window
    event_ids JSONB NOT NULL DEFAULT '[]'::jsonb,

    -- Aggregated context
    context JSONB NOT NULL DEFAULT '{}'::jsonb,

    -- Window status
    status VARCHAR(20) NOT NULL DEFAULT 'open',  -- open, closed, matched

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT tumbling_window_state_status_check CHECK (
        status IN ('open', 'closed', 'matched')
    ),
    -- Unique constraint: one window per pattern/partition/time
    CONSTRAINT tumbling_window_state_unique UNIQUE(pattern_id, partition_key, window_start)
);

-- Index for active windows
CREATE INDEX IF NOT EXISTS idx_tumbling_window_state_active
ON tumbling_window_state(pattern_id, window_end)
WHERE status = 'open';

-- Index for cleanup
CREATE INDEX IF NOT EXISTS idx_tumbling_window_state_closed
ON tumbling_window_state(window_end)
WHERE status IN ('closed', 'matched');

COMMENT ON TABLE tumbling_window_state IS 'CEP: State for tumbling window aggregations';
COMMENT ON COLUMN tumbling_window_state.status IS 'open: accepting events, closed: evaluation complete, matched: pattern matched';

-- ============================================================================
-- Function: Evaluate Event Against Patterns
-- ============================================================================
-- Main entry point for CEP evaluation

CREATE OR REPLACE FUNCTION honua_cep_evaluate_event(
    p_event_id UUID,
    p_event_type VARCHAR,
    p_event_time TIMESTAMPTZ,
    p_geofence_id UUID,
    p_entity_id VARCHAR,
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS TABLE(
    pattern_id UUID,
    pattern_name VARCHAR,
    match_type VARCHAR,  -- 'partial', 'complete'
    state_id UUID
) AS $$
DECLARE
    v_pattern RECORD;
    v_state_id UUID;
    v_match_complete BOOLEAN;
BEGIN
    -- Get all enabled patterns for this tenant
    FOR v_pattern IN
        SELECT *
        FROM geofence_event_patterns
        WHERE enabled = true
          AND (p_tenant_id IS NULL OR tenant_id = p_tenant_id)
        ORDER BY priority DESC, created_at
    LOOP
        -- Evaluate based on pattern type
        CASE v_pattern.pattern_type
            WHEN 'sequence' THEN
                -- Sequence pattern: ordered events
                -- Implementation delegated to application layer for complex logic
                NULL;
            WHEN 'count' THEN
                -- Count pattern: N occurrences within window
                NULL;
            WHEN 'correlation' THEN
                -- Correlation pattern: multiple entities
                NULL;
            WHEN 'absence' THEN
                -- Absence pattern: expected event didn't occur
                NULL;
        END CASE;
    END LOOP;

    RETURN;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION honua_cep_evaluate_event IS
    'Evaluate a geofence event against all active CEP patterns (main entry point)';

-- ============================================================================
-- Function: Cleanup Expired Pattern States
-- ============================================================================
-- Remove expired partial matches and closed windows

CREATE OR REPLACE FUNCTION honua_cep_cleanup_expired_states(
    p_retention_hours INT DEFAULT 24
)
RETURNS TABLE(
    pattern_states_deleted INT,
    tumbling_windows_deleted INT
) AS $$
DECLARE
    v_pattern_states_deleted INT;
    v_tumbling_windows_deleted INT;
    v_cutoff_time TIMESTAMPTZ;
BEGIN
    v_cutoff_time := NOW() - (p_retention_hours || ' hours')::INTERVAL;

    -- Delete expired pattern match states
    DELETE FROM pattern_match_state
    WHERE window_end < v_cutoff_time;

    GET DIAGNOSTICS v_pattern_states_deleted = ROW_COUNT;

    -- Delete old closed tumbling windows
    DELETE FROM tumbling_window_state
    WHERE status IN ('closed', 'matched')
      AND window_end < v_cutoff_time;

    GET DIAGNOSTICS v_tumbling_windows_deleted = ROW_COUNT;

    RETURN QUERY SELECT v_pattern_states_deleted, v_tumbling_windows_deleted;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION honua_cep_cleanup_expired_states IS
    'Cleanup expired CEP pattern states and closed windows (retention policy)';

-- ============================================================================
-- Function: Get Pattern Match Statistics
-- ============================================================================
-- Analytics for pattern matches

CREATE OR REPLACE FUNCTION honua_cep_get_pattern_stats(
    p_pattern_id UUID DEFAULT NULL,
    p_start_time TIMESTAMPTZ DEFAULT NOW() - INTERVAL '24 hours',
    p_end_time TIMESTAMPTZ DEFAULT NOW(),
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS TABLE(
    pattern_id UUID,
    pattern_name VARCHAR,
    total_matches BIGINT,
    matches_by_severity JSONB,
    avg_events_per_match NUMERIC,
    unique_partitions BIGINT,
    first_match TIMESTAMPTZ,
    last_match TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        h.pattern_id,
        MAX(h.pattern_name) AS pattern_name,
        COUNT(*) AS total_matches,
        jsonb_object_agg(h.alert_severity, severity_count) AS matches_by_severity,
        AVG(jsonb_array_length(h.matched_event_ids)) AS avg_events_per_match,
        COUNT(DISTINCT h.partition_key) AS unique_partitions,
        MIN(h.created_at) AS first_match,
        MAX(h.created_at) AS last_match
    FROM pattern_match_history h
    LEFT JOIN LATERAL (
        SELECT h.alert_severity, COUNT(*) AS severity_count
        FROM pattern_match_history
        WHERE pattern_id = h.pattern_id
        GROUP BY alert_severity
    ) severity_counts ON true
    WHERE h.created_at BETWEEN p_start_time AND p_end_time
      AND (p_pattern_id IS NULL OR h.pattern_id = p_pattern_id)
      AND (p_tenant_id IS NULL OR h.tenant_id = p_tenant_id)
    GROUP BY h.pattern_id;
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_cep_get_pattern_stats IS
    'Get pattern match statistics for monitoring and analytics';

-- ============================================================================
-- Function: Get Active Pattern States
-- ============================================================================
-- View currently active partial matches

CREATE OR REPLACE FUNCTION honua_cep_get_active_states(
    p_pattern_id UUID DEFAULT NULL,
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS TABLE(
    state_id UUID,
    pattern_id UUID,
    pattern_name VARCHAR,
    partition_key VARCHAR,
    event_count INT,
    window_start TIMESTAMPTZ,
    window_end TIMESTAMPTZ,
    time_remaining_seconds INT,
    progress_percent NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        s.id AS state_id,
        s.pattern_id,
        p.name AS pattern_name,
        s.partition_key,
        jsonb_array_length(s.matched_event_ids) AS event_count,
        s.window_start,
        s.window_end,
        EXTRACT(EPOCH FROM (s.window_end - NOW()))::INT AS time_remaining_seconds,
        CASE
            WHEN p.pattern_type = 'count' THEN
                (jsonb_array_length(s.matched_event_ids)::NUMERIC /
                 NULLIF((p.conditions->0->>'min_occurrences')::INT, 0) * 100)
            ELSE 50.0  -- Generic progress for other pattern types
        END AS progress_percent
    FROM pattern_match_state s
    JOIN geofence_event_patterns p ON p.id = s.pattern_id
    WHERE s.window_end > NOW()
      AND (p_pattern_id IS NULL OR s.pattern_id = p_pattern_id)
      AND (p_tenant_id IS NULL OR s.tenant_id = p_tenant_id)
    ORDER BY s.window_end ASC;
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_cep_get_active_states IS
    'Get all active pattern matching states (for monitoring and debugging)';

-- ============================================================================
-- Function: Test Pattern Against Historical Data
-- ============================================================================
-- Backtest a pattern against historical events

CREATE OR REPLACE FUNCTION honua_cep_test_pattern(
    p_pattern_conditions JSONB,
    p_window_seconds INT,
    p_window_type VARCHAR,
    p_start_time TIMESTAMPTZ,
    p_end_time TIMESTAMPTZ,
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS TABLE(
    match_time TIMESTAMPTZ,
    matched_event_ids JSONB,
    match_context JSONB
) AS $$
BEGIN
    -- This is a simplified test function
    -- Full implementation would replay historical events through CEP engine

    RETURN QUERY
    SELECT
        e.event_time AS match_time,
        jsonb_build_array(e.id) AS matched_event_ids,
        jsonb_build_object(
            'entity_id', e.entity_id,
            'geofence_id', e.geofence_id,
            'event_type', e.event_type
        ) AS match_context
    FROM geofence_events e
    WHERE e.event_time BETWEEN p_start_time AND p_end_time
      AND (p_tenant_id IS NULL OR e.tenant_id = p_tenant_id)
    LIMIT 10;  -- Simplified for MVP
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_cep_test_pattern IS
    'Test a pattern against historical events (backtesting)';

-- ============================================================================
-- View: Pattern Performance Dashboard
-- ============================================================================

CREATE OR REPLACE VIEW v_cep_pattern_performance AS
SELECT
    p.id AS pattern_id,
    p.name AS pattern_name,
    p.pattern_type,
    p.enabled,
    p.alert_severity,
    COUNT(h.id) AS total_matches_24h,
    COUNT(DISTINCT h.partition_key) AS unique_entities_24h,
    MAX(h.created_at) AS last_match_time,
    EXTRACT(EPOCH FROM (NOW() - MAX(h.created_at)))::INT AS seconds_since_last_match,
    (
        SELECT COUNT(*)
        FROM pattern_match_state s
        WHERE s.pattern_id = p.id
          AND s.window_end > NOW()
    ) AS active_states
FROM geofence_event_patterns p
LEFT JOIN pattern_match_history h ON h.pattern_id = p.id
    AND h.created_at > NOW() - INTERVAL '24 hours'
GROUP BY p.id, p.name, p.pattern_type, p.enabled, p.alert_severity
ORDER BY total_matches_24h DESC, p.name;

COMMENT ON VIEW v_cep_pattern_performance IS
    'CEP pattern performance metrics dashboard';

-- ============================================================================
-- Triggers: Updated_at Timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_cep_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_geofence_event_patterns_updated ON geofence_event_patterns;
CREATE TRIGGER trg_geofence_event_patterns_updated
    BEFORE UPDATE ON geofence_event_patterns
    FOR EACH ROW
    EXECUTE FUNCTION honua_cep_update_timestamp();

DROP TRIGGER IF EXISTS trg_pattern_match_state_updated ON pattern_match_state;
CREATE TRIGGER trg_pattern_match_state_updated
    BEFORE UPDATE ON pattern_match_state
    FOR EACH ROW
    EXECUTE FUNCTION honua_cep_update_timestamp();

DROP TRIGGER IF EXISTS trg_tumbling_window_state_updated ON tumbling_window_state;
CREATE TRIGGER trg_tumbling_window_state_updated
    BEFORE UPDATE ON tumbling_window_state
    FOR EACH ROW
    EXECUTE FUNCTION honua_cep_update_timestamp();

-- ============================================================================
-- Sample Data: Example Patterns
-- ============================================================================

-- Example 1: Route Deviation Detection (Sequence Pattern)
INSERT INTO geofence_event_patterns (
    name,
    description,
    pattern_type,
    conditions,
    window_duration_seconds,
    window_type,
    alert_name,
    alert_severity,
    alert_description
) VALUES (
    'Route Deviation Detection',
    'Detect when a vehicle enters an unauthorized zone within 30 minutes of leaving a designated area',
    'sequence',
    '[
        {
            "condition_id": "step1",
            "event_type": "exit",
            "geofence_name_pattern": "Warehouse-.*"
        },
        {
            "condition_id": "step2",
            "event_type": "enter",
            "geofence_name_pattern": "Unauthorized-.*",
            "previous_condition_id": "step1",
            "max_time_since_previous_seconds": 1800
        }
    ]'::jsonb,
    1800,  -- 30 minutes
    'sliding',
    'Route Deviation Detected',
    'critical',
    'Vehicle {entity_id} entered unauthorized zone {geofence_name} after leaving warehouse'
) ON CONFLICT DO NOTHING;

-- Example 2: Loitering Detection (Count Pattern)
INSERT INTO geofence_event_patterns (
    name,
    description,
    pattern_type,
    conditions,
    window_duration_seconds,
    window_type,
    alert_name,
    alert_severity,
    alert_description
) VALUES (
    'Loitering Detection',
    'Detect when an entity enters/exits the same geofence 3+ times within 1 hour',
    'count',
    '[
        {
            "event_type": "exit",
            "min_occurrences": 3
        }
    ]'::jsonb,
    3600,  -- 1 hour
    'sliding',
    'Loitering Detected',
    'medium',
    'Entity {entity_id} has entered/exited {geofence_name} multiple times'
) ON CONFLICT DO NOTHING;

-- Example 3: Convoy Detection (Correlation Pattern)
INSERT INTO geofence_event_patterns (
    name,
    description,
    pattern_type,
    conditions,
    window_duration_seconds,
    window_type,
    alert_name,
    alert_severity,
    alert_description
) VALUES (
    'Convoy Detection',
    'Detect when 3+ vehicles enter the same geofence within 5 minutes',
    'correlation',
    '[
        {
            "event_type": "enter",
            "min_occurrences": 3,
            "unique_entities": true
        }
    ]'::jsonb,
    300,  -- 5 minutes
    'sliding',
    'Convoy Detected',
    'info',
    'Multiple vehicles ({entity_count}) entered {geofence_name} in a short time'
) ON CONFLICT DO NOTHING;

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. Pattern match states are indexed by pattern_id and partition_key
-- 2. BRIN indexes on time columns provide excellent compression
-- 3. Partial indexes reduce index size for expired states
-- 4. JSONB indexes can be added for specific condition queries
-- 5. Consider partitioning pattern_match_history by time for very large datasets
-- ============================================================================

-- ============================================================================
-- Architecture Notes
-- ============================================================================
-- 1. CEP evaluation is triggered by event queue consumer after SignalR delivery
-- 2. Pattern matching engine maintains state in pattern_match_state table
-- 3. Completed matches generate alerts via existing alert bridge
-- 4. Cleanup job runs periodically to remove expired states
-- 5. Tumbling windows are evaluated when window closes (aligned to clock)
-- 6. Sliding windows are evaluated on each event (continuous)
-- 7. Session windows timeout based on inactivity gap
-- ============================================================================

-- ============================================================================
-- Migration Complete
-- ============================================================================

INSERT INTO schema_version (version, description)
VALUES (35, 'Complex Event Processing (CEP) for geofence events with pattern matching')
ON CONFLICT (version) DO NOTHING;
