-- GeoETL Database Optimization Script
-- This script creates recommended indexes and optimizations for production deployments

-- ==================================================
-- INDEXES FOR WORKFLOW RUNS
-- ==================================================

-- Index for querying runs by status and creation date
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_runs_status_created
ON geoetl_workflow_runs(status, created_at DESC)
WHERE is_deleted = false;

-- Index for querying runs by tenant and workflow
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_runs_tenant_workflow
ON geoetl_workflow_runs(tenant_id, workflow_id, created_at DESC)
WHERE is_deleted = false;

-- Index for querying runs by user
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_runs_user
ON geoetl_workflow_runs(triggered_by, created_at DESC)
WHERE is_deleted = false;

-- Index for active runs
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_runs_active
ON geoetl_workflow_runs(status, started_at)
WHERE status IN ('pending', 'running');

-- ==================================================
-- INDEXES FOR WORKFLOW DEFINITIONS
-- ==================================================

-- Index for querying published workflows by tenant
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflows_tenant_published
ON geoetl_workflows(tenant_id, is_published, is_deleted, updated_at DESC);

-- Index for workflow metadata search
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflows_metadata
ON geoetl_workflows USING gin(metadata);

-- Full-text search on workflow names and descriptions
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflows_search
ON geoetl_workflows USING gin(to_tsvector('english',
    COALESCE(metadata->>'name', '') || ' ' ||
    COALESCE(metadata->>'description', '')));

-- ==================================================
-- INDEXES FOR NODE RUNS
-- ==================================================

-- Index for querying node runs by workflow run
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_node_runs_workflow_run
ON geoetl_node_runs(workflow_run_id, started_at);

-- Index for querying node runs by type
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_node_runs_type
ON geoetl_node_runs(node_type, status, duration_ms);

-- Index for failed nodes
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_node_runs_failed
ON geoetl_node_runs(workflow_run_id, node_id)
WHERE status = 'failed';

-- ==================================================
-- INDEXES FOR TEMPLATES
-- ==================================================

-- Index for template queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_templates_category
ON geoetl_templates(category, is_published, created_at DESC);

-- Index for template tags
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_workflow_templates_tags
ON geoetl_templates USING gin(tags);

-- ==================================================
-- MATERIALIZED VIEWS FOR STATISTICS
-- ==================================================

-- Workflow execution statistics
CREATE MATERIALIZED VIEW IF NOT EXISTS geoetl_workflow_stats AS
SELECT
    workflow_id,
    COUNT(*) as total_runs,
    COUNT(*) FILTER (WHERE status = 'completed') as successful_runs,
    COUNT(*) FILTER (WHERE status = 'failed') as failed_runs,
    AVG(EXTRACT(EPOCH FROM (completed_at - started_at))) as avg_duration_seconds,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - started_at))) as p50_duration,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - started_at))) as p95_duration,
    MAX(created_at) as last_run_at
FROM geoetl_workflow_runs
WHERE started_at IS NOT NULL
GROUP BY workflow_id;

CREATE UNIQUE INDEX ON geoetl_workflow_stats(workflow_id);

-- Node performance statistics
CREATE MATERIALIZED VIEW IF NOT EXISTS geoetl_node_stats AS
SELECT
    node_type,
    COUNT(*) as total_executions,
    COUNT(*) FILTER (WHERE status = 'completed') as successful_executions,
    COUNT(*) FILTER (WHERE status = 'failed') as failed_executions,
    AVG(duration_ms) as avg_duration_ms,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY duration_ms) as p50_duration_ms,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ms) as p95_duration_ms,
    SUM(features_processed) as total_features_processed,
    AVG(features_processed::float / NULLIF(duration_ms, 0) * 1000) as avg_throughput_fps
FROM geoetl_node_runs
WHERE duration_ms IS NOT NULL
GROUP BY node_type;

CREATE UNIQUE INDEX ON geoetl_node_stats(node_type);

-- ==================================================
-- FUNCTIONS FOR AUTOMATIC CLEANUP
-- ==================================================

-- Function to archive old workflow runs
CREATE OR REPLACE FUNCTION geoetl_archive_old_runs(days_to_keep INT DEFAULT 90)
RETURNS INTEGER AS $$
DECLARE
    archived_count INTEGER;
BEGIN
    WITH archived AS (
        DELETE FROM geoetl_workflow_runs
        WHERE created_at < NOW() - INTERVAL '1 day' * days_to_keep
            AND status IN ('completed', 'failed', 'cancelled')
        RETURNING *
    )
    SELECT COUNT(*) INTO archived_count FROM archived;

    RETURN archived_count;
END;
$$ LANGUAGE plpgsql;

-- Function to refresh materialized views
CREATE OR REPLACE FUNCTION geoetl_refresh_stats()
RETURNS VOID AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY geoetl_workflow_stats;
    REFRESH MATERIALIZED VIEW CONCURRENTLY geoetl_node_stats;
END;
$$ LANGUAGE plpgsql;

-- ==================================================
-- TABLE PARTITIONING (Optional)
-- ==================================================

-- Example: Partition workflow_runs by month
-- Uncomment and adjust dates as needed

/*
-- Convert to partitioned table (requires recreating table)
CREATE TABLE geoetl_workflow_runs_new (
    LIKE geoetl_workflow_runs INCLUDING ALL
) PARTITION BY RANGE (created_at);

-- Create partitions for each month
CREATE TABLE geoetl_workflow_runs_2025_01 PARTITION OF geoetl_workflow_runs_new
    FOR VALUES FROM ('2025-01-01') TO ('2025-02-01');

CREATE TABLE geoetl_workflow_runs_2025_02 PARTITION OF geoetl_workflow_runs_new
    FOR VALUES FROM ('2025-02-01') TO ('2025-03-01');

-- ... create more partitions as needed

-- Migrate data
INSERT INTO geoetl_workflow_runs_new SELECT * FROM geoetl_workflow_runs;

-- Rename tables
ALTER TABLE geoetl_workflow_runs RENAME TO geoetl_workflow_runs_old;
ALTER TABLE geoetl_workflow_runs_new RENAME TO geoetl_workflow_runs;

-- Drop old table after verification
-- DROP TABLE geoetl_workflow_runs_old;
*/

-- ==================================================
-- VACUUM AND ANALYZE
-- ==================================================

-- Analyze tables for query planner
ANALYZE geoetl_workflows;
ANALYZE geoetl_workflow_runs;
ANALYZE geoetl_node_runs;
ANALYZE geoetl_templates;

-- ==================================================
-- SCHEDULED MAINTENANCE (pg_cron)
-- ==================================================

-- If pg_cron extension is available, schedule automatic maintenance
-- Uncomment to enable

/*
-- Install extension (requires superuser)
CREATE EXTENSION IF NOT EXISTS pg_cron;

-- Refresh statistics every hour
SELECT cron.schedule('geoetl-refresh-stats', '0 * * * *', 'SELECT geoetl_refresh_stats()');

-- Archive old runs daily at 2 AM
SELECT cron.schedule('geoetl-archive-runs', '0 2 * * *', 'SELECT geoetl_archive_old_runs(90)');

-- Vacuum tables weekly
SELECT cron.schedule('geoetl-vacuum', '0 3 * * 0', 'VACUUM ANALYZE geoetl_workflow_runs, geoetl_node_runs');
*/

-- ==================================================
-- CONFIGURATION RECOMMENDATIONS
-- ==================================================

-- Adjust these settings in postgresql.conf for optimal performance:

/*
# Memory Settings
shared_buffers = 4GB                    # 25% of system RAM
effective_cache_size = 12GB             # 75% of system RAM
work_mem = 64MB                         # Per operation memory
maintenance_work_mem = 512MB            # For VACUUM, CREATE INDEX

# Connection Pooling
max_connections = 200                   # Adjust based on workload
connection_idle_lifetime = 300          # Match application pool settings

# Query Planning
random_page_cost = 1.1                  # Lower for SSD
effective_io_concurrency = 200          # Higher for SSD

# WAL Settings
wal_buffers = 16MB
checkpoint_completion_target = 0.9
max_wal_size = 4GB
min_wal_size = 1GB

# Parallel Query
max_parallel_workers_per_gather = 4
max_parallel_workers = 8
*/

-- ==================================================
-- MONITORING QUERIES
-- ==================================================

-- View slow queries
/*
SELECT
    query,
    calls,
    total_exec_time / 1000 as total_seconds,
    mean_exec_time / 1000 as mean_seconds,
    max_exec_time / 1000 as max_seconds
FROM pg_stat_statements
WHERE query LIKE '%geoetl%'
ORDER BY total_exec_time DESC
LIMIT 20;
*/

-- View table sizes
/*
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables
WHERE tablename LIKE 'geoetl%'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
*/

-- View index usage
/*
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan as index_scans,
    pg_size_pretty(pg_relation_size(indexrelid)) as index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
    AND tablename LIKE 'geoetl%'
ORDER BY idx_scan ASC;
*/

COMMENT ON FUNCTION geoetl_archive_old_runs IS 'Archives workflow runs older than specified days';
COMMENT ON FUNCTION geoetl_refresh_stats IS 'Refreshes materialized views for workflow statistics';
