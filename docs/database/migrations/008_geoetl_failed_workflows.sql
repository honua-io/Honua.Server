-- Migration: GeoETL Failed Workflows and Dead Letter Queue
-- Description: Add tables for tracking failed workflow runs and retry logic
-- Date: 2025-01-07

-- Failed workflows table (Dead Letter Queue)
CREATE TABLE IF NOT EXISTS geoetl_failed_workflows (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_run_id UUID NOT NULL,
    workflow_id UUID NOT NULL,
    workflow_name TEXT,
    tenant_id UUID NOT NULL,
    failed_node_id TEXT,
    failed_node_type TEXT,
    failed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    error_category TEXT NOT NULL, -- Transient, Data, Resource, Configuration, External, Logic, Unknown
    error_message TEXT NOT NULL,
    error_details_json JSONB,
    retry_count INTEGER NOT NULL DEFAULT 0,
    last_retry_at TIMESTAMPTZ,
    status TEXT NOT NULL DEFAULT 'Pending', -- Pending, Retrying, Investigating, Resolved, Abandoned
    assigned_to UUID,
    resolution_notes TEXT,
    resolved_at TIMESTAMPTZ,
    related_failures JSONB, -- Array of related failed workflow IDs
    tags JSONB, -- Array of tags
    priority TEXT NOT NULL DEFAULT 'Medium', -- Low, Medium, High, Critical
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Indexes for common queries
    INDEX idx_geoetl_failed_workflows_tenant (tenant_id),
    INDEX idx_geoetl_failed_workflows_workflow (workflow_id),
    INDEX idx_geoetl_failed_workflows_status (status),
    INDEX idx_geoetl_failed_workflows_category (error_category),
    INDEX idx_geoetl_failed_workflows_failed_at (failed_at DESC),
    INDEX idx_geoetl_failed_workflows_assigned (assigned_to),
    INDEX idx_geoetl_failed_workflows_node_type (failed_node_type)
);

-- Circuit breaker state table (for persistence across restarts)
CREATE TABLE IF NOT EXISTS geoetl_circuit_breakers (
    node_type TEXT PRIMARY KEY,
    state TEXT NOT NULL, -- Closed, Open, HalfOpen
    consecutive_failures INTEGER NOT NULL DEFAULT 0,
    total_failures BIGINT NOT NULL DEFAULT 0,
    total_successes BIGINT NOT NULL DEFAULT 0,
    last_failure_at TIMESTAMPTZ,
    opened_at TIMESTAMPTZ,
    half_open_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Error patterns table (for identifying common issues)
CREATE TABLE IF NOT EXISTS geoetl_error_patterns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    error_signature TEXT NOT NULL, -- Hash of error message pattern
    error_category TEXT NOT NULL,
    node_type TEXT,
    occurrence_count INTEGER NOT NULL DEFAULT 1,
    first_occurred TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_occurred TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    example_error_message TEXT,
    suggested_resolution TEXT,

    UNIQUE (error_signature, node_type)
);

-- Retry history table (for audit trail)
CREATE TABLE IF NOT EXISTS geoetl_retry_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    failed_workflow_id UUID NOT NULL REFERENCES geoetl_failed_workflows(id) ON DELETE CASCADE,
    retry_attempt INTEGER NOT NULL,
    retried_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    retried_by UUID,
    retry_from_point TEXT, -- Beginning, FailedNode
    parameter_overrides JSONB,
    new_run_id UUID,
    retry_result TEXT, -- Success, Failed, Cancelled
    retry_error TEXT,

    INDEX idx_geoetl_retry_history_failed_workflow (failed_workflow_id),
    INDEX idx_geoetl_retry_history_retried_at (retried_at DESC)
);

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers for updating timestamps
DROP TRIGGER IF EXISTS update_geoetl_failed_workflows_updated_at ON geoetl_failed_workflows;
CREATE TRIGGER update_geoetl_failed_workflows_updated_at
    BEFORE UPDATE ON geoetl_failed_workflows
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_geoetl_circuit_breakers_updated_at ON geoetl_circuit_breakers;
CREATE TRIGGER update_geoetl_circuit_breakers_updated_at
    BEFORE UPDATE ON geoetl_circuit_breakers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Create view for error statistics
CREATE OR REPLACE VIEW geoetl_error_stats AS
SELECT
    DATE_TRUNC('day', failed_at) AS failure_date,
    error_category,
    failed_node_type,
    workflow_id,
    status,
    COUNT(*) AS failure_count,
    COUNT(CASE WHEN status = 'Resolved' THEN 1 END) AS resolved_count,
    COUNT(CASE WHEN status = 'Abandoned' THEN 1 END) AS abandoned_count,
    AVG(retry_count) AS avg_retry_count,
    MAX(failed_at) AS last_failure
FROM geoetl_failed_workflows
GROUP BY DATE_TRUNC('day', failed_at), error_category, failed_node_type, workflow_id, status;

-- Comments
COMMENT ON TABLE geoetl_failed_workflows IS 'Dead letter queue for failed workflow runs requiring manual intervention or retry';
COMMENT ON TABLE geoetl_circuit_breakers IS 'Circuit breaker state for preventing cascading failures by node type';
COMMENT ON TABLE geoetl_error_patterns IS 'Common error patterns for automated diagnosis and resolution suggestions';
COMMENT ON TABLE geoetl_retry_history IS 'Audit trail of retry attempts for failed workflows';
COMMENT ON VIEW geoetl_error_stats IS 'Aggregated error statistics for monitoring and analytics';
