-- Migration 018: ETL Workflow Scheduling System
-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0

-- ============================================================================
-- WORKFLOW SCHEDULES
-- ============================================================================

-- Workflow schedules table
CREATE TABLE IF NOT EXISTS geoetl_workflow_schedules (
    schedule_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workflow_id UUID NOT NULL REFERENCES geoetl_workflows(workflow_id) ON DELETE CASCADE,
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),

    -- Schedule metadata
    name TEXT NOT NULL,
    description TEXT,

    -- Cron configuration
    cron_expression TEXT NOT NULL,
    timezone TEXT NOT NULL DEFAULT 'UTC',

    -- Status
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    status TEXT NOT NULL DEFAULT 'active',

    -- Execution tracking
    next_run_at TIMESTAMPTZ,
    last_run_at TIMESTAMPTZ,
    last_run_status TEXT,
    last_run_id UUID REFERENCES geoetl_workflow_runs(run_id),

    -- Parameters
    parameter_values JSONB,

    -- Execution controls
    max_concurrent_executions INTEGER NOT NULL DEFAULT 1,
    retry_attempts INTEGER NOT NULL DEFAULT 0,
    retry_delay_minutes INTEGER NOT NULL DEFAULT 5,

    -- Expiration
    expires_at TIMESTAMPTZ,

    -- Notifications
    notification_config JSONB,

    -- Tags
    tags TEXT[],

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID NOT NULL,
    updated_by UUID,

    -- Constraints
    CONSTRAINT geoetl_workflow_schedules_name_check CHECK (char_length(name) > 0),
    CONSTRAINT geoetl_workflow_schedules_cron_check CHECK (char_length(cron_expression) > 0),
    CONSTRAINT geoetl_workflow_schedules_status_check CHECK (status IN ('active', 'paused', 'expired', 'error')),
    CONSTRAINT geoetl_workflow_schedules_max_concurrent_check CHECK (max_concurrent_executions >= 0),
    CONSTRAINT geoetl_workflow_schedules_retry_attempts_check CHECK (retry_attempts >= 0),
    CONSTRAINT geoetl_workflow_schedules_retry_delay_check CHECK (retry_delay_minutes >= 0)
);

-- Indexes for schedules
CREATE INDEX idx_geoetl_schedules_workflow ON geoetl_workflow_schedules(workflow_id);
CREATE INDEX idx_geoetl_schedules_tenant ON geoetl_workflow_schedules(tenant_id);
CREATE INDEX idx_geoetl_schedules_enabled ON geoetl_workflow_schedules(enabled) WHERE enabled = TRUE;
CREATE INDEX idx_geoetl_schedules_status ON geoetl_workflow_schedules(status);
CREATE INDEX idx_geoetl_schedules_created_at ON geoetl_workflow_schedules(created_at DESC);
CREATE INDEX idx_geoetl_schedules_tags ON geoetl_workflow_schedules USING GIN(tags);

-- Critical index for finding due schedules
CREATE INDEX idx_geoetl_schedules_next_run ON geoetl_workflow_schedules(next_run_at, enabled, status)
    WHERE enabled = TRUE AND status = 'active' AND next_run_at IS NOT NULL;

-- Full-text search on schedule name and description
CREATE INDEX idx_geoetl_schedules_search ON geoetl_workflow_schedules
    USING GIN(to_tsvector('english', name || ' ' || COALESCE(description, '')));

-- ============================================================================
-- SCHEDULE EXECUTIONS
-- ============================================================================

-- Schedule execution history table
CREATE TABLE IF NOT EXISTS geoetl_schedule_executions (
    execution_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    schedule_id UUID NOT NULL REFERENCES geoetl_workflow_schedules(schedule_id) ON DELETE CASCADE,
    workflow_run_id UUID REFERENCES geoetl_workflow_runs(run_id),

    -- Timing
    scheduled_at TIMESTAMPTZ NOT NULL,
    executed_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,

    -- Status
    status TEXT NOT NULL DEFAULT 'pending',

    -- Error tracking
    error_message TEXT,

    -- Retry tracking
    retry_count INTEGER NOT NULL DEFAULT 0,

    -- Skip tracking
    skipped BOOLEAN NOT NULL DEFAULT FALSE,
    skip_reason TEXT,

    -- Constraints
    CONSTRAINT geoetl_schedule_executions_status_check CHECK (status IN ('pending', 'running', 'completed', 'failed', 'skipped')),
    CONSTRAINT geoetl_schedule_executions_retry_count_check CHECK (retry_count >= 0)
);

-- Indexes for schedule executions
CREATE INDEX idx_geoetl_schedule_executions_schedule ON geoetl_schedule_executions(schedule_id);
CREATE INDEX idx_geoetl_schedule_executions_workflow_run ON geoetl_schedule_executions(workflow_run_id) WHERE workflow_run_id IS NOT NULL;
CREATE INDEX idx_geoetl_schedule_executions_status ON geoetl_schedule_executions(status);
CREATE INDEX idx_geoetl_schedule_executions_scheduled_at ON geoetl_schedule_executions(scheduled_at DESC);

-- Index for finding running executions
CREATE INDEX idx_geoetl_schedule_executions_running ON geoetl_schedule_executions(schedule_id, status)
    WHERE status = 'running';

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

-- Function to update schedule updated_at timestamp
CREATE OR REPLACE FUNCTION honua_geoetl_update_schedule_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger to auto-update schedule timestamp
CREATE TRIGGER trigger_geoetl_schedules_updated_at
    BEFORE UPDATE ON geoetl_workflow_schedules
    FOR EACH ROW
    EXECUTE FUNCTION honua_geoetl_update_schedule_timestamp();

-- Function to get schedule statistics
CREATE OR REPLACE FUNCTION honua_geoetl_get_schedule_stats(p_schedule_id UUID, p_days INTEGER DEFAULT 30)
RETURNS TABLE (
    schedule_id UUID,
    schedule_name TEXT,
    total_executions INTEGER,
    successful_executions INTEGER,
    failed_executions INTEGER,
    skipped_executions INTEGER,
    avg_duration_seconds INTEGER,
    last_execution TIMESTAMPTZ,
    next_execution TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        s.schedule_id,
        s.name AS schedule_name,
        COUNT(e.execution_id)::INTEGER AS total_executions,
        COUNT(e.execution_id) FILTER (WHERE e.status = 'completed')::INTEGER AS successful_executions,
        COUNT(e.execution_id) FILTER (WHERE e.status = 'failed')::INTEGER AS failed_executions,
        COUNT(e.execution_id) FILTER (WHERE e.status = 'skipped')::INTEGER AS skipped_executions,
        AVG(EXTRACT(EPOCH FROM (e.completed_at - e.executed_at)))::INTEGER AS avg_duration_seconds,
        s.last_run_at AS last_execution,
        s.next_run_at AS next_execution
    FROM geoetl_workflow_schedules s
    LEFT JOIN geoetl_schedule_executions e ON e.schedule_id = s.schedule_id
        AND e.scheduled_at >= NOW() - (p_days || ' days')::INTERVAL
    WHERE s.schedule_id = p_schedule_id
    GROUP BY s.schedule_id, s.name, s.last_run_at, s.next_run_at;
END;
$$ LANGUAGE plpgsql STABLE;

-- Function to get tenant schedule statistics
CREATE OR REPLACE FUNCTION honua_geoetl_get_tenant_schedule_stats(p_tenant_id UUID, p_days INTEGER DEFAULT 30)
RETURNS TABLE (
    total_schedules INTEGER,
    active_schedules INTEGER,
    paused_schedules INTEGER,
    total_executions INTEGER,
    successful_executions INTEGER,
    failed_executions INTEGER,
    avg_success_rate NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(DISTINCT s.schedule_id)::INTEGER AS total_schedules,
        COUNT(DISTINCT s.schedule_id) FILTER (WHERE s.status = 'active' AND s.enabled = TRUE)::INTEGER AS active_schedules,
        COUNT(DISTINCT s.schedule_id) FILTER (WHERE s.status = 'paused' OR s.enabled = FALSE)::INTEGER AS paused_schedules,
        COUNT(e.execution_id)::INTEGER AS total_executions,
        COUNT(e.execution_id) FILTER (WHERE e.status = 'completed')::INTEGER AS successful_executions,
        COUNT(e.execution_id) FILTER (WHERE e.status = 'failed')::INTEGER AS failed_executions,
        CASE
            WHEN COUNT(e.execution_id) > 0 THEN
                ROUND(100.0 * COUNT(e.execution_id) FILTER (WHERE e.status = 'completed') / COUNT(e.execution_id), 2)
            ELSE 0
        END AS avg_success_rate
    FROM geoetl_workflow_schedules s
    LEFT JOIN geoetl_schedule_executions e ON e.schedule_id = s.schedule_id
        AND e.scheduled_at >= NOW() - (p_days || ' days')::INTERVAL
    WHERE s.tenant_id = p_tenant_id;
END;
$$ LANGUAGE plpgsql STABLE;

-- Function to clean up old execution history
CREATE OR REPLACE FUNCTION honua_geoetl_cleanup_old_executions(p_retention_days INTEGER DEFAULT 90)
RETURNS INTEGER AS $$
DECLARE
    v_deleted_count INTEGER;
BEGIN
    DELETE FROM geoetl_schedule_executions
    WHERE scheduled_at < NOW() - (p_retention_days || ' days')::INTERVAL;

    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;

    RETURN v_deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Function to auto-expire schedules
CREATE OR REPLACE FUNCTION honua_geoetl_expire_schedules()
RETURNS INTEGER AS $$
DECLARE
    v_updated_count INTEGER;
BEGIN
    UPDATE geoetl_workflow_schedules
    SET status = 'expired', updated_at = NOW()
    WHERE enabled = TRUE
        AND status = 'active'
        AND expires_at IS NOT NULL
        AND expires_at <= NOW();

    GET DIAGNOSTICS v_updated_count = ROW_COUNT;

    RETURN v_updated_count;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- ROW LEVEL SECURITY (if RLS is enabled)
-- ============================================================================

-- Enable RLS on schedule tables
ALTER TABLE geoetl_workflow_schedules ENABLE ROW LEVEL SECURITY;
ALTER TABLE geoetl_schedule_executions ENABLE ROW LEVEL SECURITY;

-- Policy for schedules: users can only see schedules in their tenant
CREATE POLICY geoetl_schedules_tenant_isolation ON geoetl_workflow_schedules
    USING (tenant_id = current_setting('app.current_tenant_id', true)::UUID);

-- Policy for schedule executions: users can only see executions for schedules in their tenant
CREATE POLICY geoetl_schedule_executions_tenant_isolation ON geoetl_schedule_executions
    USING (
        EXISTS (
            SELECT 1 FROM geoetl_workflow_schedules s
            WHERE s.schedule_id = geoetl_schedule_executions.schedule_id
            AND s.tenant_id = current_setting('app.current_tenant_id', true)::UUID
        )
    );

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE geoetl_workflow_schedules IS 'Scheduled execution plans for ETL workflows';
COMMENT ON TABLE geoetl_schedule_executions IS 'Execution history for scheduled workflows';

COMMENT ON COLUMN geoetl_workflow_schedules.cron_expression IS 'Standard cron expression (e.g., "0 0 * * *" for daily at midnight)';
COMMENT ON COLUMN geoetl_workflow_schedules.timezone IS 'IANA timezone identifier (e.g., "America/New_York", "UTC")';
COMMENT ON COLUMN geoetl_workflow_schedules.next_run_at IS 'Next scheduled execution time (calculated from cron expression)';
COMMENT ON COLUMN geoetl_workflow_schedules.max_concurrent_executions IS 'Maximum number of simultaneous executions allowed (0 = unlimited)';
COMMENT ON COLUMN geoetl_workflow_schedules.notification_config IS 'JSON configuration for email, webhook, Slack, and Teams notifications';
COMMENT ON COLUMN geoetl_schedule_executions.skipped IS 'Whether execution was skipped due to concurrent execution limit';

-- ============================================================================
-- GRANTS (adjust based on your role structure)
-- ============================================================================

-- Grant SELECT to read-only role (if exists)
-- GRANT SELECT ON geoetl_workflow_schedules, geoetl_schedule_executions TO honua_readonly;

-- Grant full access to application role
-- GRANT SELECT, INSERT, UPDATE, DELETE ON geoetl_workflow_schedules, geoetl_schedule_executions TO honua_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua_app;
