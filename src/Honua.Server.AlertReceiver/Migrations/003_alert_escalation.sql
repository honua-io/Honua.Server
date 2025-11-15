-- PostgreSQL Migration Script for Alert Escalation System
-- This script creates the necessary tables for multi-level alert escalation with time-based policies

-- ============================================================================
-- ALERT_ESCALATION_POLICIES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS alert_escalation_policies (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT NULL,
    applies_to_patterns JSONB NULL,  -- Array of alert name patterns (e.g., ["geoprocessing.*", "critical.*"])
    applies_to_severities TEXT[] COMMENT 'Array of severities this policy applies to',
    escalation_levels JSONB NOT NULL,  -- Array of escalation level configurations
    requires_acknowledgment BOOLEAN NOT NULL DEFAULT true,
    is_active BOOLEAN NOT NULL DEFAULT true,
    tenant_id TEXT NULL,  -- For multi-tenant support
    created_by TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_alert_escalation_policies_active ON alert_escalation_policies(is_active);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_policies_tenant ON alert_escalation_policies(tenant_id, is_active);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_policies_name ON alert_escalation_policies(name);

-- ============================================================================
-- ALERT_ESCALATION_STATE TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS alert_escalation_state (
    id BIGSERIAL PRIMARY KEY,
    alert_id BIGINT NOT NULL,  -- References alert_history.id
    alert_fingerprint TEXT NOT NULL,
    policy_id BIGINT NOT NULL,  -- References alert_escalation_policies.id
    current_level INTEGER NOT NULL DEFAULT 0,
    is_acknowledged BOOLEAN NOT NULL DEFAULT false,
    acknowledged_by TEXT NULL,
    acknowledged_at TIMESTAMPTZ NULL,
    acknowledgment_notes TEXT NULL,
    next_escalation_time TIMESTAMPTZ NULL,
    escalation_started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    escalation_completed_at TIMESTAMPTZ NULL,
    status TEXT NOT NULL DEFAULT 'active',  -- active, acknowledged, completed, cancelled
    cancellation_reason TEXT NULL,
    row_version INTEGER NOT NULL DEFAULT 1,  -- For optimistic locking
    CONSTRAINT chk_escalation_status CHECK (status IN ('active', 'acknowledged', 'completed', 'cancelled')),
    CONSTRAINT fk_alert_escalation_policy FOREIGN KEY (policy_id) REFERENCES alert_escalation_policies(id)
);

-- Create indexes for efficient queries
CREATE UNIQUE INDEX IF NOT EXISTS idx_alert_escalation_state_alert_unique ON alert_escalation_state(alert_id)
    WHERE status = 'active';  -- Only one active escalation per alert
CREATE INDEX IF NOT EXISTS idx_alert_escalation_state_fingerprint ON alert_escalation_state(alert_fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_state_status ON alert_escalation_state(status);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_state_next_escalation ON alert_escalation_state(next_escalation_time, status)
    WHERE status = 'active' AND next_escalation_time IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_alert_escalation_state_policy ON alert_escalation_state(policy_id);

-- ============================================================================
-- ALERT_ESCALATION_EVENTS TABLE (Audit Trail)
-- ============================================================================
CREATE TABLE IF NOT EXISTS alert_escalation_events (
    id BIGSERIAL PRIMARY KEY,
    escalation_state_id BIGINT NOT NULL,
    event_type TEXT NOT NULL,  -- started, escalated, acknowledged, cancelled, completed
    escalation_level INTEGER NULL,
    notification_channels TEXT[] NULL,
    severity_override TEXT NULL,
    event_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    event_details JSONB NULL,  -- Additional context (e.g., delivery results, user info)
    CONSTRAINT fk_alert_escalation_state FOREIGN KEY (escalation_state_id) REFERENCES alert_escalation_state(id) ON DELETE CASCADE,
    CONSTRAINT chk_event_type CHECK (event_type IN ('started', 'escalated', 'acknowledged', 'cancelled', 'completed', 'suppressed'))
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_alert_escalation_events_state ON alert_escalation_events(escalation_state_id);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_events_timestamp ON alert_escalation_events(event_timestamp);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_events_type ON alert_escalation_events(event_type);

-- ============================================================================
-- ALERT_ESCALATION_SUPPRESSIONS TABLE (Maintenance Windows)
-- ============================================================================
CREATE TABLE IF NOT EXISTS alert_escalation_suppressions (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    reason TEXT NOT NULL,
    applies_to_patterns JSONB NULL,  -- Alert name patterns to suppress
    applies_to_severities TEXT[] NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_by TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_suppression_dates CHECK (ends_at > starts_at)
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_alert_escalation_suppressions_active ON alert_escalation_suppressions(is_active);
CREATE INDEX IF NOT EXISTS idx_alert_escalation_suppressions_range ON alert_escalation_suppressions(is_active, starts_at, ends_at);

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE alert_escalation_policies IS 'Defines multi-level escalation policies for alerts with time-based delays and channel progression';
COMMENT ON COLUMN alert_escalation_policies.applies_to_patterns IS 'JSON array of alert name patterns (glob-style) this policy applies to';
COMMENT ON COLUMN alert_escalation_policies.applies_to_severities IS 'Array of severity levels this policy applies to (e.g., {critical, high})';
COMMENT ON COLUMN alert_escalation_policies.escalation_levels IS 'JSON array of escalation level configurations with delays, channels, and overrides';
COMMENT ON COLUMN alert_escalation_policies.requires_acknowledgment IS 'Whether alerts must be acknowledged to stop escalation';

COMMENT ON TABLE alert_escalation_state IS 'Tracks the current escalation state for each alert';
COMMENT ON COLUMN alert_escalation_state.current_level IS 'Current escalation level (0-based index into policy levels)';
COMMENT ON COLUMN alert_escalation_state.next_escalation_time IS 'When to escalate to next level (NULL if at final level)';
COMMENT ON COLUMN alert_escalation_state.status IS 'Current escalation status (active, acknowledged, completed, cancelled)';
COMMENT ON COLUMN alert_escalation_state.row_version IS 'Version for optimistic locking to prevent concurrent modifications';

COMMENT ON TABLE alert_escalation_events IS 'Audit trail of all escalation events for compliance and debugging';
COMMENT ON COLUMN alert_escalation_events.event_type IS 'Type of escalation event (started, escalated, acknowledged, cancelled, completed)';
COMMENT ON COLUMN alert_escalation_events.severity_override IS 'Severity override applied at this level (if any)';

COMMENT ON TABLE alert_escalation_suppressions IS 'Defines maintenance windows where escalations should be suppressed';
COMMENT ON COLUMN alert_escalation_suppressions.applies_to_patterns IS 'JSON array of alert patterns to suppress during this window';

-- ============================================================================
-- SAMPLE QUERIES
-- ============================================================================

-- Find alerts ready for escalation
-- SELECT es.*, ah.name, ah.severity
-- FROM alert_escalation_state es
-- JOIN alert_history ah ON es.alert_id = ah.id
-- WHERE es.status = 'active'
--   AND es.next_escalation_time IS NOT NULL
--   AND es.next_escalation_time <= NOW()
-- ORDER BY es.next_escalation_time ASC
-- LIMIT 100;

-- Get escalation history for an alert
-- SELECT aee.*, aes.current_level
-- FROM alert_escalation_events aee
-- JOIN alert_escalation_state aes ON aee.escalation_state_id = aes.id
-- WHERE aes.alert_fingerprint = 'abc123'
-- ORDER BY aee.event_timestamp DESC;

-- Get active escalation policies
-- SELECT * FROM alert_escalation_policies
-- WHERE is_active = true
-- ORDER BY name;

-- Check if escalation is suppressed
-- SELECT * FROM alert_escalation_suppressions
-- WHERE is_active = true
--   AND starts_at <= NOW()
--   AND ends_at > NOW();
