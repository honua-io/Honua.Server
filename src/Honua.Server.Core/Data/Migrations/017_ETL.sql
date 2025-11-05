-- Migration 017: ETL Workflow System
-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- WORKFLOW DEFINITIONS
-- ============================================================================

-- Workflow definitions table
CREATE TABLE IF NOT EXISTS geoetl_workflows (
    workflow_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),
    version INTEGER NOT NULL DEFAULT 1,

    -- Metadata
    name TEXT NOT NULL,
    description TEXT,
    author TEXT,
    tags TEXT[],
    category TEXT,
    metadata_custom JSONB,

    -- Workflow definition (JSON)
    definition JSONB NOT NULL,

    -- Parameters
    parameters JSONB,

    -- Status
    is_published BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID NOT NULL,
    updated_by UUID,

    -- Constraints
    CONSTRAINT geoetl_workflows_name_check CHECK (char_length(name) > 0),
    CONSTRAINT geoetl_workflows_version_check CHECK (version > 0)
);

-- Indexes for workflows
CREATE INDEX idx_geoetl_workflows_tenant ON geoetl_workflows(tenant_id) WHERE NOT is_deleted;
CREATE INDEX idx_geoetl_workflows_created_by ON geoetl_workflows(created_by);
CREATE INDEX idx_geoetl_workflows_created_at ON geoetl_workflows(created_at DESC);
CREATE INDEX idx_geoetl_workflows_tags ON geoetl_workflows USING GIN(tags);
CREATE INDEX idx_geoetl_workflows_category ON geoetl_workflows(category) WHERE category IS NOT NULL;
CREATE INDEX idx_geoetl_workflows_published ON geoetl_workflows(tenant_id, is_published) WHERE is_published = TRUE AND NOT is_deleted;

-- Full-text search on workflow name and description
CREATE INDEX idx_geoetl_workflows_search ON geoetl_workflows USING GIN(to_tsvector('english', name || ' ' || COALESCE(description, '')));

-- ============================================================================
-- WORKFLOW RUNS
-- ============================================================================

-- Workflow runs (executions) table
CREATE TABLE IF NOT EXISTS geoetl_workflow_runs (
    run_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workflow_id UUID NOT NULL REFERENCES geoetl_workflows(workflow_id),
    tenant_id UUID NOT NULL REFERENCES tenants(tenant_id),

    -- Status
    status TEXT NOT NULL DEFAULT 'pending',

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,

    -- Trigger information
    triggered_by UUID, -- References users table
    trigger_type TEXT NOT NULL DEFAULT 'manual',

    -- Parameters
    parameter_values JSONB,

    -- Metrics
    features_processed BIGINT,
    bytes_read BIGINT,
    bytes_written BIGINT,
    peak_memory_mb INTEGER,
    cpu_time_ms BIGINT,

    -- Cost tracking
    compute_cost_usd NUMERIC(10,4),
    storage_cost_usd NUMERIC(10,4),

    -- Results
    output_locations JSONB,
    error_message TEXT,
    error_stack TEXT,

    -- Lineage
    input_datasets JSONB,
    output_datasets JSONB,

    -- State (for resume/restart)
    state JSONB,

    -- Constraints
    CONSTRAINT geoetl_workflow_runs_status_check CHECK (status IN ('pending', 'running', 'completed', 'failed', 'cancelled', 'timeout')),
    CONSTRAINT geoetl_workflow_runs_trigger_type_check CHECK (trigger_type IN ('manual', 'scheduled', 'api', 'event', 'workflow'))
);

-- Indexes for workflow runs
CREATE INDEX idx_geoetl_workflow_runs_workflow ON geoetl_workflow_runs(workflow_id);
CREATE INDEX idx_geoetl_workflow_runs_tenant ON geoetl_workflow_runs(tenant_id);
CREATE INDEX idx_geoetl_workflow_runs_status ON geoetl_workflow_runs(status);
CREATE INDEX idx_geoetl_workflow_runs_created_at ON geoetl_workflow_runs(created_at DESC);
CREATE INDEX idx_geoetl_workflow_runs_triggered_by ON geoetl_workflow_runs(triggered_by) WHERE triggered_by IS NOT NULL;
CREATE INDEX idx_geoetl_workflow_runs_completed_at ON geoetl_workflow_runs(completed_at DESC) WHERE completed_at IS NOT NULL;

-- Composite index for tenant + status queries
CREATE INDEX idx_geoetl_workflow_runs_tenant_status ON geoetl_workflow_runs(tenant_id, status, created_at DESC);

-- ============================================================================
-- NODE RUNS
-- ============================================================================

-- Node execution details within workflow runs
CREATE TABLE IF NOT EXISTS geoetl_node_runs (
    node_run_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    workflow_run_id UUID NOT NULL REFERENCES geoetl_workflow_runs(run_id) ON DELETE CASCADE,

    -- Node identification
    node_id TEXT NOT NULL,
    node_type TEXT NOT NULL,

    -- Status
    status TEXT NOT NULL DEFAULT 'pending',

    -- Timestamps
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_ms BIGINT,

    -- Metrics
    features_processed BIGINT,

    -- Error information
    error_message TEXT,

    -- Link to geoprocessing run if applicable
    geoprocessing_run_id UUID REFERENCES process_runs(run_id),

    -- Node output (for passing to next nodes)
    output JSONB,

    -- Retry information
    retry_count INTEGER DEFAULT 0,

    -- Constraints
    CONSTRAINT geoetl_node_runs_status_check CHECK (status IN ('pending', 'running', 'completed', 'failed', 'skipped')),
    CONSTRAINT geoetl_node_runs_retry_count_check CHECK (retry_count >= 0)
);

-- Indexes for node runs
CREATE INDEX idx_geoetl_node_runs_workflow_run ON geoetl_node_runs(workflow_run_id);
CREATE INDEX idx_geoetl_node_runs_status ON geoetl_node_runs(status);
CREATE INDEX idx_geoetl_node_runs_node_type ON geoetl_node_runs(node_type);
CREATE INDEX idx_geoetl_node_runs_geoprocessing ON geoetl_node_runs(geoprocessing_run_id) WHERE geoprocessing_run_id IS NOT NULL;

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

-- Function to update workflow updated_at timestamp
CREATE OR REPLACE FUNCTION honua_geoetl_update_workflow_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger to auto-update workflow timestamp
CREATE TRIGGER trigger_geoetl_workflows_updated_at
    BEFORE UPDATE ON geoetl_workflows
    FOR EACH ROW
    EXECUTE FUNCTION honua_geoetl_update_workflow_timestamp();

-- Function to calculate workflow run duration
CREATE OR REPLACE FUNCTION honua_geoetl_workflow_run_duration(p_run_id UUID)
RETURNS INTERVAL AS $$
DECLARE
    v_duration INTERVAL;
BEGIN
    SELECT completed_at - started_at INTO v_duration
    FROM geoetl_workflow_runs
    WHERE run_id = p_run_id;

    RETURN v_duration;
END;
$$ LANGUAGE plpgsql STABLE;

-- Function to get workflow run summary
CREATE OR REPLACE FUNCTION honua_geoetl_get_run_summary(p_run_id UUID)
RETURNS TABLE (
    run_id UUID,
    workflow_id UUID,
    workflow_name TEXT,
    status TEXT,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_seconds INTEGER,
    total_nodes INTEGER,
    completed_nodes INTEGER,
    failed_nodes INTEGER,
    features_processed BIGINT,
    total_cost_usd NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        wr.run_id,
        wr.workflow_id,
        w.name AS workflow_name,
        wr.status,
        wr.started_at,
        wr.completed_at,
        EXTRACT(EPOCH FROM (wr.completed_at - wr.started_at))::INTEGER AS duration_seconds,
        COUNT(nr.node_run_id)::INTEGER AS total_nodes,
        COUNT(nr.node_run_id) FILTER (WHERE nr.status = 'completed')::INTEGER AS completed_nodes,
        COUNT(nr.node_run_id) FILTER (WHERE nr.status = 'failed')::INTEGER AS failed_nodes,
        wr.features_processed,
        (COALESCE(wr.compute_cost_usd, 0) + COALESCE(wr.storage_cost_usd, 0)) AS total_cost_usd
    FROM geoetl_workflow_runs wr
    JOIN geoetl_workflows w ON w.workflow_id = wr.workflow_id
    LEFT JOIN geoetl_node_runs nr ON nr.workflow_run_id = wr.run_id
    WHERE wr.run_id = p_run_id
    GROUP BY wr.run_id, wr.workflow_id, w.name, wr.status, wr.started_at, wr.completed_at, wr.features_processed, wr.compute_cost_usd, wr.storage_cost_usd;
END;
$$ LANGUAGE plpgsql STABLE;

-- Function to get tenant workflow statistics
CREATE OR REPLACE FUNCTION honua_geoetl_get_tenant_stats(p_tenant_id UUID, p_days INTEGER DEFAULT 30)
RETURNS TABLE (
    total_workflows INTEGER,
    total_runs INTEGER,
    successful_runs INTEGER,
    failed_runs INTEGER,
    avg_duration_seconds INTEGER,
    total_features_processed BIGINT,
    total_cost_usd NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(DISTINCT w.workflow_id)::INTEGER AS total_workflows,
        COUNT(wr.run_id)::INTEGER AS total_runs,
        COUNT(wr.run_id) FILTER (WHERE wr.status = 'completed')::INTEGER AS successful_runs,
        COUNT(wr.run_id) FILTER (WHERE wr.status = 'failed')::INTEGER AS failed_runs,
        AVG(EXTRACT(EPOCH FROM (wr.completed_at - wr.started_at)))::INTEGER AS avg_duration_seconds,
        SUM(wr.features_processed) AS total_features_processed,
        SUM(COALESCE(wr.compute_cost_usd, 0) + COALESCE(wr.storage_cost_usd, 0)) AS total_cost_usd
    FROM geoetl_workflows w
    LEFT JOIN geoetl_workflow_runs wr ON wr.workflow_id = w.workflow_id
        AND wr.created_at >= NOW() - (p_days || ' days')::INTERVAL
    WHERE w.tenant_id = p_tenant_id
        AND NOT w.is_deleted;
END;
$$ LANGUAGE plpgsql STABLE;

-- ============================================================================
-- ROW LEVEL SECURITY (if RLS is enabled)
-- ============================================================================

-- Enable RLS on workflow tables
ALTER TABLE geoetl_workflows ENABLE ROW LEVEL SECURITY;
ALTER TABLE geoetl_workflow_runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE geoetl_node_runs ENABLE ROW LEVEL SECURITY;

-- Policy for workflows: users can only see workflows in their tenant
CREATE POLICY geoetl_workflows_tenant_isolation ON geoetl_workflows
    USING (tenant_id = current_setting('app.current_tenant_id', true)::UUID);

-- Policy for workflow runs: users can only see runs in their tenant
CREATE POLICY geoetl_workflow_runs_tenant_isolation ON geoetl_workflow_runs
    USING (tenant_id = current_setting('app.current_tenant_id', true)::UUID);

-- Policy for node runs: users can only see node runs for runs in their tenant
CREATE POLICY geoetl_node_runs_tenant_isolation ON geoetl_node_runs
    USING (
        EXISTS (
            SELECT 1 FROM geoetl_workflow_runs wr
            WHERE wr.run_id = geoetl_node_runs.workflow_run_id
            AND wr.tenant_id = current_setting('app.current_tenant_id', true)::UUID
        )
    );

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE geoetl_workflows IS 'ETL workflow definitions with JSON-based node and edge configuration';
COMMENT ON TABLE geoetl_workflow_runs IS 'Execution history of workflows with metrics and cost tracking';
COMMENT ON TABLE geoetl_node_runs IS 'Individual node execution details within workflow runs';

COMMENT ON COLUMN geoetl_workflows.definition IS 'Complete workflow definition in JSON format (nodes, edges, etc.)';
COMMENT ON COLUMN geoetl_workflows.parameters IS 'Workflow parameter definitions (type, required, default, etc.)';
COMMENT ON COLUMN geoetl_workflow_runs.parameter_values IS 'Actual parameter values provided for this run';
COMMENT ON COLUMN geoetl_workflow_runs.state IS 'Intermediate state for resume/restart capability';
COMMENT ON COLUMN geoetl_node_runs.output IS 'Node output data to pass to downstream nodes';

-- ============================================================================
-- GRANTS (adjust based on your role structure)
-- ============================================================================

-- Grant SELECT to read-only role (if exists)
-- GRANT SELECT ON geoetl_workflows, geoetl_workflow_runs, geoetl_node_runs TO honua_readonly;

-- Grant full access to application role
-- GRANT SELECT, INSERT, UPDATE, DELETE ON geoetl_workflows, geoetl_workflow_runs, geoetl_node_runs TO honua_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua_app;
