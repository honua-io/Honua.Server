-- ============================================================================
-- Honua GIS Server - Geofence Alert Integration
-- ============================================================================
-- Version: 031
-- Description: Integration tables and functions for geofence-alert correlation
-- Dependencies: Migration 017 (GeoEvent Geofencing), Alert system tables
-- Related: Honua.Server.Enterprise.Events, Honua.Server.AlertReceiver
-- ============================================================================
-- PURPOSE:
-- Enable correlation between geofence events and alert system, allowing
-- geofence events to trigger alerts and alert rules to match geofence criteria.
-- ============================================================================

-- ============================================================================
-- Table: geofence_alert_correlation
-- ============================================================================
-- Tracks correlation between geofence events and generated alerts

CREATE TABLE IF NOT EXISTS geofence_alert_correlation (
    -- Primary key is the geofence event ID
    geofence_event_id UUID PRIMARY KEY REFERENCES geofence_events(id) ON DELETE CASCADE,

    -- Alert fingerprint (from alert deduplication system)
    alert_fingerprint VARCHAR(256) NOT NULL,

    -- Alert history ID (from alert_history table in AlertReceiver)
    -- Note: This is a soft reference as AlertReceiver uses a separate database
    alert_history_id BIGINT,

    -- When the alert was created
    alert_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Alert severity at creation time (denormalized for quick queries)
    alert_severity VARCHAR(20),

    -- Alert status (active, resolved, silenced, acknowledged)
    alert_status VARCHAR(20) DEFAULT 'active',

    -- Notification channels that were triggered (array of channel IDs)
    notification_channel_ids JSONB,

    -- Whether the alert was silenced by any silencing rule
    was_silenced BOOLEAN DEFAULT false,

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- When this correlation was last updated
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT geofence_alert_correlation_severity_check CHECK (
        alert_severity IN ('critical', 'high', 'medium', 'low', 'info')
    ),
    CONSTRAINT geofence_alert_correlation_status_check CHECK (
        alert_status IN ('active', 'resolved', 'silenced', 'acknowledged')
    )
);

-- Index for finding alerts by fingerprint
CREATE INDEX IF NOT EXISTS idx_geofence_alert_correlation_fingerprint
ON geofence_alert_correlation(alert_fingerprint);

-- Index for finding alerts by alert history ID
CREATE INDEX IF NOT EXISTS idx_geofence_alert_correlation_alert_history
ON geofence_alert_correlation(alert_history_id)
WHERE alert_history_id IS NOT NULL;

-- Index for finding active alerts
CREATE INDEX IF NOT EXISTS idx_geofence_alert_correlation_status
ON geofence_alert_correlation(alert_status, alert_created_at DESC)
WHERE alert_status = 'active';

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofence_alert_correlation_tenant
ON geofence_alert_correlation(tenant_id, alert_created_at DESC)
WHERE tenant_id IS NOT NULL;

-- Composite index for common queries
CREATE INDEX IF NOT EXISTS idx_geofence_alert_correlation_tenant_status
ON geofence_alert_correlation(tenant_id, alert_status, alert_severity, alert_created_at DESC)
WHERE tenant_id IS NOT NULL;

COMMENT ON TABLE geofence_alert_correlation IS 'Correlation between geofence events and generated alerts';
COMMENT ON COLUMN geofence_alert_correlation.alert_fingerprint IS 'Unique fingerprint for alert deduplication';
COMMENT ON COLUMN geofence_alert_correlation.alert_history_id IS 'Soft reference to alert_history table';
COMMENT ON COLUMN geofence_alert_correlation.notification_channel_ids IS 'Array of channel IDs that received notifications';

-- ============================================================================
-- Table: geofence_alert_rules
-- ============================================================================
-- Extended alert rules specific to geofence events

CREATE TABLE IF NOT EXISTS geofence_alert_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Rule name
    name VARCHAR(255) NOT NULL,

    -- Rule description
    description TEXT,

    -- Whether this rule is enabled
    enabled BOOLEAN NOT NULL DEFAULT true,

    -- Geofence criteria (optional - if NULL, applies to all geofences)
    geofence_id UUID REFERENCES geofences(id) ON DELETE CASCADE,
    geofence_name_pattern VARCHAR(255),  -- Regex pattern to match geofence names

    -- Event type filter (enter, exit, dwell, approach)
    event_types VARCHAR(20)[],

    -- Entity criteria (optional)
    entity_id_pattern VARCHAR(255),  -- Regex pattern to match entity IDs
    entity_type VARCHAR(100),

    -- Dwell time threshold (in seconds) - only for exit/dwell events
    min_dwell_time_seconds INT,
    max_dwell_time_seconds INT,

    -- Alert configuration
    alert_severity VARCHAR(20) NOT NULL DEFAULT 'medium',
    alert_name_template VARCHAR(500) NOT NULL,  -- Template with placeholders
    alert_description_template TEXT,
    alert_labels JSONB,  -- Additional labels to attach to alert

    -- Notification channels to use (references notification_channels table)
    notification_channel_ids JSONB,  -- Array of channel IDs

    -- Silencing configuration
    silence_duration_minutes INT,  -- Auto-silence for this many minutes after first alert

    -- Deduplication window (prevent multiple alerts for same entity/geofence)
    deduplication_window_minutes INT DEFAULT 60,

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by VARCHAR(255),
    updated_by VARCHAR(255),

    CONSTRAINT geofence_alert_rules_name_check CHECK (char_length(name) > 0),
    CONSTRAINT geofence_alert_rules_severity_check CHECK (
        alert_severity IN ('critical', 'high', 'medium', 'low', 'info')
    ),
    CONSTRAINT geofence_alert_rules_event_types_check CHECK (
        event_types IS NULL OR
        event_types <@ ARRAY['enter', 'exit', 'dwell', 'approach']::VARCHAR[]
    ),
    CONSTRAINT geofence_alert_rules_dwell_time_check CHECK (
        min_dwell_time_seconds IS NULL OR
        max_dwell_time_seconds IS NULL OR
        min_dwell_time_seconds <= max_dwell_time_seconds
    )
);

-- Index for enabled rules
CREATE INDEX IF NOT EXISTS idx_geofence_alert_rules_enabled
ON geofence_alert_rules(enabled)
WHERE enabled = true;

-- Index for geofence-specific rules
CREATE INDEX IF NOT EXISTS idx_geofence_alert_rules_geofence
ON geofence_alert_rules(geofence_id)
WHERE geofence_id IS NOT NULL;

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofence_alert_rules_tenant
ON geofence_alert_rules(tenant_id)
WHERE tenant_id IS NOT NULL;

-- Index for matching by severity
CREATE INDEX IF NOT EXISTS idx_geofence_alert_rules_severity
ON geofence_alert_rules(alert_severity, enabled);

COMMENT ON TABLE geofence_alert_rules IS 'Alert rules for geofence events with advanced matching criteria';
COMMENT ON COLUMN geofence_alert_rules.geofence_name_pattern IS 'Regex pattern to match geofence names (e.g., "Restricted.*")';
COMMENT ON COLUMN geofence_alert_rules.entity_id_pattern IS 'Regex pattern to match entity IDs (e.g., "vehicle-[0-9]+")';
COMMENT ON COLUMN geofence_alert_rules.alert_name_template IS 'Template with placeholders: {entity_id}, {geofence_name}, {event_type}, {dwell_time}';
COMMENT ON COLUMN geofence_alert_rules.deduplication_window_minutes IS 'Prevent duplicate alerts within this window';

-- ============================================================================
-- Table: geofence_alert_silencing
-- ============================================================================
-- Silencing rules for geofence alerts

CREATE TABLE IF NOT EXISTS geofence_alert_silencing (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Silencing rule name
    name VARCHAR(255) NOT NULL,

    -- Whether this rule is enabled
    enabled BOOLEAN NOT NULL DEFAULT true,

    -- Silencing criteria
    geofence_id UUID REFERENCES geofences(id) ON DELETE CASCADE,
    geofence_name_pattern VARCHAR(255),
    entity_id_pattern VARCHAR(255),
    event_types VARCHAR(20)[],

    -- Time-based silencing (optional)
    start_time TIMESTAMPTZ,
    end_time TIMESTAMPTZ,

    -- Recurring silencing (cron-like expressions for maintenance windows)
    -- Example: silence every day from 9am-5pm
    recurring_schedule JSONB,  -- {days: [1,2,3,4,5], start_hour: 9, end_hour: 17}

    -- Multi-tenancy
    tenant_id VARCHAR(100),

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by VARCHAR(255),
    updated_by VARCHAR(255),

    CONSTRAINT geofence_alert_silencing_name_check CHECK (char_length(name) > 0),
    CONSTRAINT geofence_alert_silencing_time_check CHECK (
        start_time IS NULL OR end_time IS NULL OR start_time < end_time
    ),
    CONSTRAINT geofence_alert_silencing_event_types_check CHECK (
        event_types IS NULL OR
        event_types <@ ARRAY['enter', 'exit', 'dwell', 'approach']::VARCHAR[]
    )
);

-- Index for enabled silencing rules
CREATE INDEX IF NOT EXISTS idx_geofence_alert_silencing_enabled
ON geofence_alert_silencing(enabled)
WHERE enabled = true;

-- Index for active time-based rules
CREATE INDEX IF NOT EXISTS idx_geofence_alert_silencing_time
ON geofence_alert_silencing(start_time, end_time)
WHERE enabled = true AND start_time IS NOT NULL;

-- Index for tenant isolation
CREATE INDEX IF NOT EXISTS idx_geofence_alert_silencing_tenant
ON geofence_alert_silencing(tenant_id)
WHERE tenant_id IS NOT NULL;

COMMENT ON TABLE geofence_alert_silencing IS 'Silencing rules for geofence alerts (maintenance windows, etc.)';
COMMENT ON COLUMN geofence_alert_silencing.recurring_schedule IS 'JSON config for recurring silencing windows';

-- ============================================================================
-- Function: Check if alert should be silenced
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_should_silence_geofence_alert(
    p_geofence_id UUID,
    p_geofence_name VARCHAR,
    p_entity_id VARCHAR,
    p_event_type VARCHAR,
    p_event_time TIMESTAMPTZ,
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS BOOLEAN AS $$
DECLARE
    v_silencing_rule RECORD;
    v_current_hour INT;
    v_current_day INT;
BEGIN
    -- Get current hour and day for recurring schedule checks
    v_current_hour := EXTRACT(HOUR FROM p_event_time);
    v_current_day := EXTRACT(DOW FROM p_event_time);  -- 0=Sunday, 6=Saturday

    -- Check all enabled silencing rules
    FOR v_silencing_rule IN
        SELECT *
        FROM geofence_alert_silencing
        WHERE enabled = true
          AND (p_tenant_id IS NULL OR tenant_id = p_tenant_id OR tenant_id IS NULL)
    LOOP
        -- Check geofence match
        IF v_silencing_rule.geofence_id IS NOT NULL AND v_silencing_rule.geofence_id != p_geofence_id THEN
            CONTINUE;
        END IF;

        -- Check geofence name pattern
        IF v_silencing_rule.geofence_name_pattern IS NOT NULL AND
           p_geofence_name !~ v_silencing_rule.geofence_name_pattern THEN
            CONTINUE;
        END IF;

        -- Check entity pattern
        IF v_silencing_rule.entity_id_pattern IS NOT NULL AND
           p_entity_id !~ v_silencing_rule.entity_id_pattern THEN
            CONTINUE;
        END IF;

        -- Check event type
        IF v_silencing_rule.event_types IS NOT NULL AND
           NOT (p_event_type = ANY(v_silencing_rule.event_types)) THEN
            CONTINUE;
        END IF;

        -- Check time-based silencing
        IF v_silencing_rule.start_time IS NOT NULL AND v_silencing_rule.end_time IS NOT NULL THEN
            IF p_event_time BETWEEN v_silencing_rule.start_time AND v_silencing_rule.end_time THEN
                RETURN true;
            END IF;
        END IF;

        -- Check recurring schedule
        IF v_silencing_rule.recurring_schedule IS NOT NULL THEN
            -- Check if current day is in the schedule
            IF v_silencing_rule.recurring_schedule->>'days' IS NOT NULL THEN
                IF (v_silencing_rule.recurring_schedule->'days')::jsonb ? v_current_day::text THEN
                    -- Check if current hour is in the range
                    IF v_current_hour >= (v_silencing_rule.recurring_schedule->>'start_hour')::int AND
                       v_current_hour < (v_silencing_rule.recurring_schedule->>'end_hour')::int THEN
                        RETURN true;
                    END IF;
                END IF;
            END IF;
        END IF;

        -- If we get here and all criteria matched (or were NULL), silence the alert
        IF v_silencing_rule.geofence_id IS NULL AND
           v_silencing_rule.geofence_name_pattern IS NULL AND
           v_silencing_rule.entity_id_pattern IS NULL AND
           v_silencing_rule.event_types IS NULL AND
           v_silencing_rule.start_time IS NULL AND
           v_silencing_rule.recurring_schedule IS NULL THEN
            -- This is a catch-all rule
            RETURN true;
        END IF;
    END LOOP;

    RETURN false;
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_should_silence_geofence_alert IS
    'Check if a geofence alert should be silenced based on silencing rules';

-- ============================================================================
-- Function: Find matching alert rules for geofence event
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_find_matching_geofence_alert_rules(
    p_geofence_id UUID,
    p_geofence_name VARCHAR,
    p_entity_id VARCHAR,
    p_entity_type VARCHAR,
    p_event_type VARCHAR,
    p_dwell_time_seconds INT,
    p_tenant_id VARCHAR DEFAULT NULL
)
RETURNS TABLE(
    rule_id UUID,
    rule_name VARCHAR,
    alert_severity VARCHAR,
    alert_name_template VARCHAR,
    alert_description_template TEXT,
    alert_labels JSONB,
    notification_channel_ids JSONB,
    deduplication_window_minutes INT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        r.id,
        r.name,
        r.alert_severity,
        r.alert_name_template,
        r.alert_description_template,
        r.alert_labels,
        r.notification_channel_ids,
        r.deduplication_window_minutes
    FROM geofence_alert_rules r
    WHERE r.enabled = true
      AND (p_tenant_id IS NULL OR r.tenant_id = p_tenant_id OR r.tenant_id IS NULL)
      -- Geofence matching
      AND (r.geofence_id IS NULL OR r.geofence_id = p_geofence_id)
      AND (r.geofence_name_pattern IS NULL OR p_geofence_name ~ r.geofence_name_pattern)
      -- Event type matching
      AND (r.event_types IS NULL OR p_event_type = ANY(r.event_types))
      -- Entity matching
      AND (r.entity_id_pattern IS NULL OR p_entity_id ~ r.entity_id_pattern)
      AND (r.entity_type IS NULL OR r.entity_type = p_entity_type)
      -- Dwell time matching
      AND (r.min_dwell_time_seconds IS NULL OR p_dwell_time_seconds >= r.min_dwell_time_seconds)
      AND (r.max_dwell_time_seconds IS NULL OR p_dwell_time_seconds <= r.max_dwell_time_seconds)
    ORDER BY r.alert_severity DESC, r.created_at;  -- Process critical alerts first
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION honua_find_matching_geofence_alert_rules IS
    'Find all alert rules that match a given geofence event';

-- ============================================================================
-- Function: Update alert correlation status
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_update_alert_correlation_status(
    p_geofence_event_id UUID,
    p_new_status VARCHAR,
    p_alert_history_id BIGINT DEFAULT NULL
)
RETURNS BOOLEAN AS $$
BEGIN
    UPDATE geofence_alert_correlation
    SET
        alert_status = p_new_status,
        alert_history_id = COALESCE(p_alert_history_id, alert_history_id),
        updated_at = NOW()
    WHERE geofence_event_id = p_geofence_event_id;

    RETURN FOUND;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION honua_update_alert_correlation_status IS
    'Update the status of an alert correlation (e.g., acknowledged, resolved)';

-- ============================================================================
-- View: Active geofence alerts
-- ============================================================================

CREATE OR REPLACE VIEW v_active_geofence_alerts AS
SELECT
    c.geofence_event_id,
    e.event_type,
    e.event_time,
    e.geofence_id,
    e.geofence_name,
    e.entity_id,
    e.entity_type,
    e.dwell_time_seconds,
    e.location,
    e.properties AS event_properties,
    c.alert_fingerprint,
    c.alert_severity,
    c.alert_status,
    c.alert_created_at,
    c.notification_channel_ids,
    c.was_silenced,
    c.tenant_id
FROM geofence_alert_correlation c
JOIN geofence_events e ON e.id = c.geofence_event_id
WHERE c.alert_status = 'active'
ORDER BY c.alert_created_at DESC;

COMMENT ON VIEW v_active_geofence_alerts IS
    'All currently active alerts triggered by geofence events';

-- ============================================================================
-- Triggers: Updated_at Timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION honua_geofence_alert_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_geofence_alert_rules_updated ON geofence_alert_rules;
CREATE TRIGGER trg_geofence_alert_rules_updated
    BEFORE UPDATE ON geofence_alert_rules
    FOR EACH ROW
    EXECUTE FUNCTION honua_geofence_alert_update_timestamp();

DROP TRIGGER IF EXISTS trg_geofence_alert_silencing_updated ON geofence_alert_silencing;
CREATE TRIGGER trg_geofence_alert_silencing_updated
    BEFORE UPDATE ON geofence_alert_silencing
    FOR EACH ROW
    EXECUTE FUNCTION honua_geofence_alert_update_timestamp();

DROP TRIGGER IF EXISTS trg_geofence_alert_correlation_updated ON geofence_alert_correlation;
CREATE TRIGGER trg_geofence_alert_correlation_updated
    BEFORE UPDATE ON geofence_alert_correlation
    FOR EACH ROW
    EXECUTE FUNCTION honua_geofence_alert_update_timestamp();

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. All matching functions use index-friendly conditions
-- 2. Regex patterns should be used sparingly for best performance
-- 3. Consider materialized view for alert statistics if needed
-- 4. Deduplication window prevents alert storms
-- ============================================================================

-- ============================================================================
-- Migration Complete
-- ============================================================================

-- Version tracking
INSERT INTO schema_version (version, description)
VALUES (31, 'Geofence alert integration: correlation tracking, rules, and silencing')
ON CONFLICT (version) DO NOTHING;
