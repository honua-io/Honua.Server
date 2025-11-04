-- =====================================================
-- Audit Log Schema (Enterprise Feature)
-- Migration: 011_AuditLog.sql
-- Version: 011
-- Dependencies: 001-010
-- Purpose: Tamper-proof audit trail for compliance
-- =====================================================

-- Main audit events table (append-only, no updates or deletes allowed)
CREATE TABLE IF NOT EXISTS audit_events (
    id UUID PRIMARY KEY,
    customer_id VARCHAR(100),
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    category VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    user_id UUID,
    user_identifier VARCHAR(500),
    success BOOLEAN NOT NULL DEFAULT true,
    resource_type VARCHAR(100),
    resource_id VARCHAR(500),
    description TEXT,
    ip_address VARCHAR(100),
    user_agent TEXT,
    http_method VARCHAR(10),
    request_path TEXT,
    status_code INTEGER,
    duration_ms BIGINT,
    error_message TEXT,
    metadata JSONB,
    changes JSONB,
    session_id VARCHAR(200),
    trace_id VARCHAR(200),
    location VARCHAR(200),
    risk_score INTEGER,
    tags TEXT[],
    archived_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_audit_tenant FOREIGN KEY (tenant_id) REFERENCES customers(customer_id) ON DELETE SET NULL
);

-- Indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_audit_events_customer ON audit_events(customer_id, timestamp DESC) WHERE tenant_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_user ON audit_events(user_id, timestamp DESC) WHERE user_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_timestamp ON audit_events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_events_category ON audit_events(category, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_events_action ON audit_events(action, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_events_resource ON audit_events(resource_type, resource_id, timestamp DESC) WHERE resource_type IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_success ON audit_events(success, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_events_ip ON audit_events(ip_address, timestamp DESC) WHERE ip_address IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_tags ON audit_events USING GIN(tags) WHERE tags IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_risk ON audit_events(risk_score DESC, timestamp DESC) WHERE risk_score >= 80;
CREATE INDEX IF NOT EXISTS idx_audit_events_trace ON audit_events(trace_id) WHERE trace_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_events_session ON audit_events(session_id, timestamp DESC) WHERE session_id IS NOT NULL;

-- Full-text search index on description and metadata
CREATE INDEX IF NOT EXISTS idx_audit_events_search ON audit_events USING GIN(to_tsvector('english', COALESCE(description, '')));

-- Partial index for active (non-archived) events
CREATE INDEX IF NOT EXISTS idx_audit_events_active ON audit_events(timestamp DESC) WHERE archived_at IS NULL;

-- Archive table for long-term retention
CREATE TABLE IF NOT EXISTS audit_events_archive (
    LIKE audit_events INCLUDING ALL
);

-- Partition the archive table by month (for efficient archival and deletion)
-- This would be set up in production with proper partitioning strategy

-- Prevent updates and deletes on audit_events (tamper-proof)
CREATE OR REPLACE FUNCTION prevent_audit_modification()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Deleting audit events is not allowed. Use archival process instead.';
    END IF;

    IF TG_OP = 'UPDATE' AND OLD.archived_at IS NULL AND NEW.archived_at IS NOT NULL THEN
        -- Allow marking as archived
        RETURN NEW;
    END IF;

    IF TG_OP = 'UPDATE' THEN
        RAISE EXCEPTION 'Updating audit events is not allowed. Audit log is tamper-proof.';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_events_tamper_proof
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW
    EXECUTE FUNCTION prevent_audit_modification();

-- Make trigger ALWAYS enabled so even superusers cannot bypass it
ALTER TABLE audit_events ENABLE ALWAYS TRIGGER audit_events_tamper_proof;

-- Function to archive old audit events
CREATE OR REPLACE FUNCTION archive_audit_events(older_than_days INTEGER DEFAULT 90)
RETURNS TABLE(archived_count BIGINT) AS $$
DECLARE
    cutoff_date TIMESTAMPTZ;
    inserted_count BIGINT;
BEGIN
    cutoff_date := NOW() - (older_than_days || ' days')::INTERVAL;

    -- Insert into archive
    WITH archived AS (
        INSERT INTO audit_events_archive
        SELECT * FROM audit_events
        WHERE timestamp < cutoff_date AND archived_at IS NULL
        RETURNING *
    )
    SELECT COUNT(*) INTO inserted_count FROM archived;

    -- Mark as archived in main table (allowed by trigger)
    UPDATE audit_events
    SET archived_at = NOW()
    WHERE timestamp < cutoff_date AND archived_at IS NULL;

    RETURN QUERY SELECT inserted_count;
END;
$$ LANGUAGE plpgsql;

-- Function to purge archived events (for data retention compliance)
CREATE OR REPLACE FUNCTION purge_archived_audit_events(older_than_days INTEGER DEFAULT 365)
RETURNS TABLE(purged_count BIGINT) AS $$
DECLARE
    cutoff_date TIMESTAMPTZ;
    deleted_count BIGINT;
BEGIN
    cutoff_date := NOW() - (older_than_days || ' days')::INTERVAL;

    -- Delete from main table (archived events only)
    WITH deleted AS (
        DELETE FROM audit_events
        WHERE archived_at IS NOT NULL AND archived_at < cutoff_date
        RETURNING *
    )
    SELECT COUNT(*) INTO deleted_count FROM deleted;

    RETURN QUERY SELECT deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Function to get audit log statistics
CREATE OR REPLACE FUNCTION get_audit_statistics(
    p_customer_id VARCHAR(100) DEFAULT NULL,
    p_start_time TIMESTAMPTZ DEFAULT NOW() - INTERVAL '30 days',
    p_end_time TIMESTAMPTZ DEFAULT NOW()
)
RETURNS TABLE(
    total_events BIGINT,
    successful_events BIGINT,
    failed_events BIGINT,
    unique_users BIGINT,
    high_risk_events BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::BIGINT as total_events,
        COUNT(*) FILTER (WHERE success = true)::BIGINT as successful_events,
        COUNT(*) FILTER (WHERE success = false)::BIGINT as failed_events,
        COUNT(DISTINCT user_id)::BIGINT as unique_users,
        COUNT(*) FILTER (WHERE risk_score >= 80)::BIGINT as high_risk_events
    FROM audit_events
    WHERE
        timestamp >= p_start_time
        AND timestamp <= p_end_time
        AND (p_tenant_id IS NULL OR tenant_id = p_tenant_id);
END;
$$ LANGUAGE plpgsql;

-- View for recent high-risk events (security monitoring)
CREATE OR REPLACE VIEW recent_high_risk_events AS
SELECT
    id,
    tenant_id,
    timestamp,
    category,
    action,
    user_identifier,
    resource_type,
    resource_id,
    ip_address,
    risk_score,
    description,
    tags
FROM audit_events
WHERE
    risk_score >= 80
    AND timestamp >= NOW() - INTERVAL '7 days'
    AND archived_at IS NULL
ORDER BY risk_score DESC, timestamp DESC;

-- View for failed authentication attempts (security monitoring)
CREATE OR REPLACE VIEW failed_auth_attempts AS
SELECT
    id,
    tenant_id,
    timestamp,
    user_identifier,
    ip_address,
    error_message,
    risk_score
FROM audit_events
WHERE
    category = 'authentication'
    AND success = false
    AND timestamp >= NOW() - INTERVAL '24 hours'
    AND archived_at IS NULL
ORDER BY timestamp DESC;

-- View for admin actions audit trail
CREATE OR REPLACE VIEW admin_actions_audit AS
SELECT
    id,
    tenant_id,
    timestamp,
    action,
    user_identifier,
    resource_type,
    resource_id,
    description,
    changes,
    success
FROM audit_events
WHERE
    category = 'admin.action'
    AND archived_at IS NULL
ORDER BY timestamp DESC;

-- Comments for documentation
COMMENT ON TABLE audit_events IS 'Tamper-proof audit log for all system events (Enterprise feature)';
COMMENT ON TABLE audit_events_archive IS 'Archived audit events for long-term retention';

COMMENT ON COLUMN audit_events.id IS 'Unique identifier for this audit event';
COMMENT ON COLUMN audit_events.tenant_id IS 'Tenant ID for multi-tenant isolation (NULL for system events)';
COMMENT ON COLUMN audit_events.timestamp IS 'When the event occurred (UTC)';
COMMENT ON COLUMN audit_events.category IS 'Event category (authentication, data.access, admin.action, etc.)';
COMMENT ON COLUMN audit_events.action IS 'Specific action performed (login, create, update, delete, etc.)';
COMMENT ON COLUMN audit_events.user_id IS 'User who performed the action';
COMMENT ON COLUMN audit_events.user_identifier IS 'User email/username for display';
COMMENT ON COLUMN audit_events.success IS 'Whether the action was successful';
COMMENT ON COLUMN audit_events.resource_type IS 'Type of resource affected (collection, user, tenant, etc.)';
COMMENT ON COLUMN audit_events.resource_id IS 'ID of resource affected';
COMMENT ON COLUMN audit_events.ip_address IS 'Client IP address';
COMMENT ON COLUMN audit_events.user_agent IS 'Client user agent (browser/app)';
COMMENT ON COLUMN audit_events.metadata IS 'Additional event metadata (JSON)';
COMMENT ON COLUMN audit_events.changes IS 'Before/after values for updates (JSON)';
COMMENT ON COLUMN audit_events.risk_score IS 'Risk score 0-100 for anomaly detection';
COMMENT ON COLUMN audit_events.tags IS 'Tags for categorization and filtering';
COMMENT ON COLUMN audit_events.archived_at IS 'When event was archived (NULL if active)';

COMMENT ON FUNCTION prevent_audit_modification IS 'Prevents updates and deletes on audit events (tamper-proof)';
COMMENT ON FUNCTION archive_audit_events IS 'Archives audit events older than specified days';
COMMENT ON FUNCTION purge_archived_audit_events IS 'Purges archived events for data retention compliance';
COMMENT ON FUNCTION get_audit_statistics IS 'Gets audit log statistics for a time period';

COMMENT ON VIEW recent_high_risk_events IS 'Recent events with high risk scores for security monitoring';
COMMENT ON VIEW failed_auth_attempts IS 'Failed authentication attempts in last 24 hours';
COMMENT ON VIEW admin_actions_audit IS 'Audit trail of all administrative actions';

-- Grant permissions
-- Note: In production, restrict DELETE and UPDATE permissions to ensure tamper-proof log
GRANT SELECT, INSERT ON audit_events TO honua_app;
GRANT SELECT ON audit_events_archive TO honua_app;
GRANT SELECT ON recent_high_risk_events TO honua_app;
GRANT SELECT ON failed_auth_attempts TO honua_app;
GRANT SELECT ON admin_actions_audit TO honua_app;
