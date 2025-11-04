-- =====================================================
-- Cloud-Native Geoprocessing Schema (Enterprise Feature)
-- Migration: 012_Geoprocessing.sql
-- Version: 012
-- Dependencies: 001-011
-- Purpose: ProcessRun - single source of truth for geoprocessing job tracking
--          Supports three-tier execution (NTS, PostGIS, Cloud Batch)
--          Used for scheduling, billing, provenance, and audit
-- =====================================================

-- ProcessRun table - single source of truth
CREATE TABLE IF NOT EXISTS process_runs (
    -- Identification
    job_id VARCHAR(100) PRIMARY KEY,
    process_id VARCHAR(100) NOT NULL,
    tenant_id VARCHAR(100) NOT NULL,
    user_id UUID NOT NULL,
    user_email VARCHAR(500),

    -- Status & Timing
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_ms BIGINT,
    queue_wait_ms BIGINT,

    -- Execution
    executed_tier VARCHAR(20), -- 'NTS', 'PostGIS', 'CloudBatch'
    worker_id VARCHAR(200),
    cloud_batch_job_id VARCHAR(500),
    priority INTEGER NOT NULL DEFAULT 5,
    progress INTEGER NOT NULL DEFAULT 0,
    progress_message TEXT,

    -- Inputs & Outputs
    inputs JSONB NOT NULL,
    output JSONB,
    response_format VARCHAR(50) NOT NULL DEFAULT 'geojson',
    output_url TEXT,
    output_size_bytes BIGINT,

    -- Error Handling
    error_message TEXT,
    error_details TEXT,
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    cancellation_reason TEXT,

    -- Resource Usage & Billing
    peak_memory_mb BIGINT,
    cpu_time_ms BIGINT,
    features_processed BIGINT,
    input_size_mb DECIMAL(10,2),
    compute_cost DECIMAL(12,4),
    storage_cost DECIMAL(12,4),
    total_cost DECIMAL(12,4),

    -- Provenance & Audit
    ip_address VARCHAR(50),
    user_agent VARCHAR(500),
    api_surface VARCHAR(50) NOT NULL DEFAULT 'OGC',
    client_id VARCHAR(100),
    tags TEXT[],
    metadata JSONB,

    -- Notifications
    webhook_url TEXT,
    notify_email BOOLEAN NOT NULL DEFAULT false,
    webhook_sent_at TIMESTAMPTZ,
    webhook_response_status INTEGER,

    -- Constraints
    CONSTRAINT fk_process_runs_tenant FOREIGN KEY (tenant_id) REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT chk_progress CHECK (progress >= 0 AND progress <= 100),
    CONSTRAINT chk_priority CHECK (priority >= 1 AND priority <= 10),
    CONSTRAINT chk_status CHECK (status IN ('pending', 'running', 'completed', 'failed', 'cancelled', 'timeout'))
);

-- Indexes for efficient querying

-- Tenant + time range queries (most common)
CREATE INDEX IF NOT EXISTS idx_process_runs_customer_created ON process_runs(tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_process_runs_customer_completed ON process_runs(tenant_id, completed_at DESC)
    WHERE completed_at IS NOT NULL;

-- User queries
CREATE INDEX IF NOT EXISTS idx_process_runs_user ON process_runs(user_id, created_at DESC);

-- Status queries
CREATE INDEX IF NOT EXISTS idx_process_runs_status ON process_runs(status, created_at DESC);

-- Process type queries
CREATE INDEX IF NOT EXISTS idx_process_runs_process ON process_runs(process_id, created_at DESC);

-- Job queue (pending jobs ordered by priority)
CREATE INDEX IF NOT EXISTS idx_process_runs_queue ON process_runs(priority DESC, created_at ASC)
    WHERE status = 'pending';

-- Running jobs (for monitoring)
CREATE INDEX IF NOT EXISTS idx_process_runs_running ON process_runs(started_at DESC)
    WHERE status = 'running';

-- Cloud batch jobs (for event completion matching)
CREATE INDEX IF NOT EXISTS idx_process_runs_cloud_batch ON process_runs(cloud_batch_job_id)
    WHERE cloud_batch_job_id IS NOT NULL;

-- Tags (GIN index for array containment queries)
CREATE INDEX IF NOT EXISTS idx_process_runs_tags ON process_runs USING GIN(tags)
    WHERE tags IS NOT NULL;

-- API surface tracking
CREATE INDEX IF NOT EXISTS idx_process_runs_api_surface ON process_runs(api_surface, created_at DESC);

-- Tier tracking (for statistics)
CREATE INDEX IF NOT EXISTS idx_process_runs_tier ON process_runs(executed_tier, created_at DESC)
    WHERE executed_tier IS NOT NULL;

-- =====================================================
-- Process Catalog - Declarative process definitions
-- =====================================================

CREATE TABLE IF NOT EXISTS process_catalog (
    process_id VARCHAR(100) PRIMARY KEY,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    version VARCHAR(20) NOT NULL DEFAULT '1.0.0',
    category VARCHAR(50) NOT NULL DEFAULT 'vector',
    keywords TEXT[],

    -- Configuration (stored as JSONB for flexibility)
    inputs_schema JSONB NOT NULL, -- ProcessParameter[] serialized
    output_schema JSONB,
    output_formats TEXT[] NOT NULL DEFAULT ARRAY['geojson'],
    execution_config JSONB NOT NULL, -- ProcessExecutionConfig serialized

    -- Links and metadata
    links JSONB, -- ProcessLink[] serialized
    metadata JSONB,

    -- Status
    enabled BOOLEAN NOT NULL DEFAULT true,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Implementation
    implementation_class VARCHAR(500) -- Fully qualified class name if code-based
);

-- Index for listing enabled processes
CREATE INDEX IF NOT EXISTS idx_process_catalog_enabled ON process_catalog(category, process_id)
    WHERE enabled = true;

-- Index for keyword search
CREATE INDEX IF NOT EXISTS idx_process_catalog_keywords ON process_catalog USING GIN(keywords)
    WHERE keywords IS NOT NULL;

-- =====================================================
-- Functions for queue management
-- =====================================================

-- Atomically dequeue next job from queue
CREATE OR REPLACE FUNCTION dequeue_process_run()
RETURNS TABLE(
    job_id VARCHAR,
    process_id VARCHAR,
    tenant_id VARCHAR(100),
    inputs JSONB
) AS $$
DECLARE
    selected_job RECORD;
BEGIN
    -- Select and lock next job atomically
    SELECT * INTO selected_job
    FROM process_runs
    WHERE status = 'pending'
    ORDER BY priority DESC, created_at ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    IF selected_job IS NULL THEN
        RETURN;
    END IF;

    -- Mark as running
    UPDATE process_runs
    SET
        status = 'running',
        started_at = NOW(),
        progress = 0,
        progress_message = 'Job started',
        queue_wait_ms = EXTRACT(EPOCH FROM (NOW() - created_at)) * 1000
    WHERE process_runs.job_id = selected_job.job_id;

    -- Return job info
    RETURN QUERY
    SELECT
        selected_job.job_id,
        selected_job.process_id,
        selected_job.tenant_id,
        selected_job.inputs;
END;
$$ LANGUAGE plpgsql;

-- Get queue depth
CREATE OR REPLACE FUNCTION get_process_queue_depth()
RETURNS INTEGER AS $$
BEGIN
    RETURN (SELECT COUNT(*)::INTEGER FROM process_runs WHERE status = 'pending');
END;
$$ LANGUAGE plpgsql;

-- Get job statistics
CREATE OR REPLACE FUNCTION get_process_statistics(
    p_tenant_id VARCHAR(100) DEFAULT NULL,
    p_start_time TIMESTAMPTZ DEFAULT NULL,
    p_end_time TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE(
    total_runs BIGINT,
    successful_runs BIGINT,
    failed_runs BIGINT,
    cancelled_runs BIGINT,
    pending_runs BIGINT,
    running_runs BIGINT,
    average_duration_seconds DOUBLE PRECISION,
    median_duration_seconds DOUBLE PRECISION,
    total_compute_cost DECIMAL,
    runs_by_tier_nts BIGINT,
    runs_by_tier_postgis BIGINT,
    runs_by_tier_cloud_batch BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::BIGINT as total_runs,
        COUNT(*) FILTER (WHERE status = 'completed')::BIGINT as successful_runs,
        COUNT(*) FILTER (WHERE status = 'failed')::BIGINT as failed_runs,
        COUNT(*) FILTER (WHERE status = 'cancelled')::BIGINT as cancelled_runs,
        COUNT(*) FILTER (WHERE status = 'pending')::BIGINT as pending_runs,
        COUNT(*) FILTER (WHERE status = 'running')::BIGINT as running_runs,
        AVG(duration_ms / 1000.0) FILTER (WHERE duration_ms IS NOT NULL) as average_duration_seconds,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY duration_ms / 1000.0) FILTER (WHERE duration_ms IS NOT NULL) as median_duration_seconds,
        SUM(COALESCE(compute_cost, 0)) as total_compute_cost,
        COUNT(*) FILTER (WHERE executed_tier = 'NTS')::BIGINT as runs_by_tier_nts,
        COUNT(*) FILTER (WHERE executed_tier = 'PostGIS')::BIGINT as runs_by_tier_postgis,
        COUNT(*) FILTER (WHERE executed_tier = 'CloudBatch')::BIGINT as runs_by_tier_cloud_batch
    FROM process_runs
    WHERE
        (p_tenant_id IS NULL OR tenant_id = p_tenant_id)
        AND (p_start_time IS NULL OR created_at >= p_start_time)
        AND (p_end_time IS NULL OR created_at <= p_end_time);
END;
$$ LANGUAGE plpgsql;

-- Cleanup old completed jobs
CREATE OR REPLACE FUNCTION cleanup_old_process_runs(older_than_days INTEGER DEFAULT 90)
RETURNS BIGINT AS $$
DECLARE
    deleted_count BIGINT;
BEGIN
    WITH deleted AS (
        DELETE FROM process_runs
        WHERE
            completed_at IS NOT NULL
            AND completed_at < NOW() - (older_than_days || ' days')::INTERVAL
            AND status IN ('completed', 'failed', 'cancelled', 'timeout')
        RETURNING *
    )
    SELECT COUNT(*) INTO deleted_count FROM deleted;

    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Find stale jobs (running for too long)
CREATE OR REPLACE FUNCTION find_stale_process_runs(timeout_minutes INTEGER DEFAULT 60)
RETURNS TABLE(
    job_id VARCHAR,
    process_id VARCHAR,
    tenant_id VARCHAR(100),
    started_at TIMESTAMPTZ,
    minutes_running INTEGER
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        pr.job_id,
        pr.process_id,
        pr.tenant_id,
        pr.started_at,
        EXTRACT(EPOCH FROM (NOW() - pr.started_at)) / 60 as minutes_running
    FROM process_runs pr
    WHERE
        pr.status = 'running'
        AND pr.started_at < NOW() - (timeout_minutes || ' minutes')::INTERVAL
    ORDER BY pr.started_at ASC;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Views for common queries
-- =====================================================

-- Active jobs (pending + running)
CREATE OR REPLACE VIEW active_process_runs AS
SELECT
    job_id,
    process_id,
    tenant_id,
    user_id,
    user_email,
    status,
    priority,
    progress,
    progress_message,
    created_at,
    started_at,
    executed_tier,
    worker_id,
    api_surface
FROM process_runs
WHERE status IN ('pending', 'running')
ORDER BY priority DESC, created_at ASC;

-- Recent completions (last 7 days)
CREATE OR REPLACE VIEW recent_process_completions AS
SELECT
    job_id,
    process_id,
    tenant_id,
    user_id,
    status,
    created_at,
    started_at,
    completed_at,
    duration_ms,
    executed_tier,
    features_processed,
    total_cost,
    error_message,
    api_surface
FROM process_runs
WHERE
    completed_at IS NOT NULL
    AND completed_at >= NOW() - INTERVAL '7 days'
ORDER BY completed_at DESC;

-- Failed jobs requiring attention
CREATE OR REPLACE VIEW failed_process_runs AS
SELECT
    job_id,
    process_id,
    tenant_id,
    user_id,
    user_email,
    error_message,
    error_details,
    retry_count,
    max_retries,
    created_at,
    started_at,
    completed_at,
    executed_tier
FROM process_runs
WHERE status = 'failed'
ORDER BY completed_at DESC;

-- Tier usage summary (for capacity planning)
CREATE OR REPLACE VIEW tier_usage_summary AS
SELECT
    executed_tier as tier,
    COUNT(*) as total_runs,
    COUNT(*) FILTER (WHERE status = 'completed') as successful_runs,
    COUNT(*) FILTER (WHERE status = 'failed') as failed_runs,
    AVG(duration_ms / 1000.0) as avg_duration_seconds,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ms / 1000.0) as p95_duration_seconds,
    SUM(COALESCE(compute_cost, 0)) as total_cost
FROM process_runs
WHERE
    executed_tier IS NOT NULL
    AND completed_at >= NOW() - INTERVAL '30 days'
GROUP BY executed_tier;

-- =====================================================
-- Comments for documentation
-- =====================================================

COMMENT ON TABLE process_runs IS 'ProcessRun - Single source of truth for geoprocessing job tracking (Enterprise feature)';
COMMENT ON COLUMN process_runs.job_id IS 'Unique job identifier';
COMMENT ON COLUMN process_runs.process_id IS 'Process type (buffer, intersection, etc.)';
COMMENT ON COLUMN process_runs.status IS 'Job status: pending, running, completed, failed, cancelled, timeout';
COMMENT ON COLUMN process_runs.executed_tier IS 'Execution tier used: NTS (in-process), PostGIS (database), CloudBatch (AWS/Azure/GCP)';
COMMENT ON COLUMN process_runs.cloud_batch_job_id IS 'Cloud provider batch job ID (for Tier 3)';
COMMENT ON COLUMN process_runs.inputs IS 'Input parameters (JSON)';
COMMENT ON COLUMN process_runs.output IS 'Output result (GeoJSON, URLs, etc.)';
COMMENT ON COLUMN process_runs.compute_cost IS 'Compute cost in units (for billing)';
COMMENT ON COLUMN process_runs.api_surface IS 'API used: OGC (OGC API - Processes), GeoservicesREST (Esri compatible), Internal';

COMMENT ON TABLE process_catalog IS 'Declarative process definitions catalog';
COMMENT ON COLUMN process_catalog.inputs_schema IS 'Input parameters schema (ProcessParameter[] as JSON)';
COMMENT ON COLUMN process_catalog.execution_config IS 'Execution configuration including tier thresholds (ProcessExecutionConfig as JSON)';

COMMENT ON FUNCTION dequeue_process_run IS 'Atomically gets next job from queue (with locking)';
COMMENT ON FUNCTION get_process_queue_depth IS 'Returns number of pending jobs';
COMMENT ON FUNCTION get_process_statistics IS 'Returns geoprocessing execution statistics';
COMMENT ON FUNCTION cleanup_old_process_runs IS 'Deletes completed jobs older than specified days';
COMMENT ON FUNCTION find_stale_process_runs IS 'Finds jobs that have been running longer than timeout';

COMMENT ON VIEW active_process_runs IS 'Currently pending and running jobs';
COMMENT ON VIEW recent_process_completions IS 'Jobs completed in last 7 days';
COMMENT ON VIEW failed_process_runs IS 'Failed jobs requiring attention';
COMMENT ON VIEW tier_usage_summary IS 'Tier usage statistics for capacity planning (last 30 days)';

-- =====================================================
-- Grant permissions
-- =====================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON process_runs TO honua_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON process_catalog TO honua_app;
GRANT SELECT ON active_process_runs TO honua_app;
GRANT SELECT ON recent_process_completions TO honua_app;
GRANT SELECT ON failed_process_runs TO honua_app;
GRANT SELECT ON tier_usage_summary TO honua_app;
