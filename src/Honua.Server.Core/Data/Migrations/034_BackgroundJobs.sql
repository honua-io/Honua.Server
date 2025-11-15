-- Migration: 034_BackgroundJobs
-- Description: Creates background_jobs table for PostgreSQL-based job queue (Tier 1-2)
-- Date: 2025-01-14

-- ============================================================================
-- Background Jobs Queue Table
-- ============================================================================
-- Purpose: Provides PostgreSQL-based job queue for background processing
-- Features:
--   - Atomic dequeue using FOR UPDATE SKIP LOCKED
--   - Priority-based ordering
--   - Visibility timeout for in-flight messages
--   - Automatic retry with exponential backoff
--   - Dead-letter queue via failed status
--   - FIFO ordering support via message_group_id
-- ============================================================================

CREATE TABLE IF NOT EXISTS background_jobs (
    -- Identity
    message_id TEXT PRIMARY KEY,
    job_type TEXT NOT NULL,

    -- Payload
    payload JSONB NOT NULL,

    -- Status and lifecycle
    status TEXT NOT NULL CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
    priority INTEGER NOT NULL DEFAULT 5 CHECK (priority BETWEEN 1 AND 10),

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_received_at TIMESTAMPTZ,
    visible_after TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,

    -- Retry tracking
    delivery_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,

    -- Receipt handle for deduplication
    receipt_handle TEXT,

    -- FIFO support
    deduplication_id TEXT,
    message_group_id TEXT,

    -- Metadata
    attributes JSONB,
    error_message TEXT,
    error_details TEXT,

    -- Indexes hint
    -- Index on (status, visible_after, priority, created_at) for efficient dequeue
    -- Index on (message_group_id, created_at) for FIFO ordering
    -- Index on (deduplication_id) for duplicate detection
    CONSTRAINT unique_deduplication_id UNIQUE (deduplication_id) DEFERRABLE INITIALLY DEFERRED
);

-- ============================================================================
-- Indexes for efficient job queue operations
-- ============================================================================

-- Primary dequeue index: find pending jobs ordered by priority and age
-- Supports: WHERE status = 'pending' AND visible_after <= NOW() ORDER BY priority DESC, created_at
CREATE INDEX IF NOT EXISTS idx_background_jobs_dequeue
ON background_jobs (status, visible_after, priority DESC, created_at)
WHERE status = 'pending';

-- FIFO ordering index: ensures ordered processing within message groups
CREATE INDEX IF NOT EXISTS idx_background_jobs_message_group
ON background_jobs (message_group_id, created_at)
WHERE message_group_id IS NOT NULL;

-- Job type index: filter by job type during receive
CREATE INDEX IF NOT EXISTS idx_background_jobs_job_type
ON background_jobs (job_type, status, created_at);

-- Deduplication index: prevent duplicate job submission
CREATE INDEX IF NOT EXISTS idx_background_jobs_deduplication
ON background_jobs (deduplication_id)
WHERE deduplication_id IS NOT NULL;

-- Receipt handle index: quick lookup during Complete/Abandon
CREATE INDEX IF NOT EXISTS idx_background_jobs_receipt_handle
ON background_jobs (receipt_handle)
WHERE receipt_handle IS NOT NULL;

-- Cleanup index: find old completed/failed jobs for archival
CREATE INDEX IF NOT EXISTS idx_background_jobs_cleanup
ON background_jobs (status, completed_at)
WHERE completed_at IS NOT NULL;

-- ============================================================================
-- Functions for job queue operations
-- ============================================================================

-- Function: Cleanup old completed/failed jobs
-- Purpose: Archive or delete jobs older than retention period (default 7 days)
CREATE OR REPLACE FUNCTION cleanup_background_jobs(retention_days INTEGER DEFAULT 7)
RETURNS TABLE(deleted_count BIGINT) AS $$
BEGIN
    WITH deleted AS (
        DELETE FROM background_jobs
        WHERE status IN ('completed', 'failed')
          AND completed_at < NOW() - (retention_days || ' days')::INTERVAL
        RETURNING *
    )
    SELECT COUNT(*)::BIGINT INTO deleted_count FROM deleted;

    RETURN QUERY SELECT deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Function: Get queue statistics
-- Purpose: Monitoring and observability
CREATE OR REPLACE FUNCTION get_background_jobs_stats()
RETURNS TABLE(
    status TEXT,
    job_type TEXT,
    count BIGINT,
    avg_delivery_count NUMERIC,
    oldest_created_at TIMESTAMPTZ,
    newest_created_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        bj.status,
        bj.job_type,
        COUNT(*)::BIGINT as count,
        AVG(bj.delivery_count)::NUMERIC as avg_delivery_count,
        MIN(bj.created_at) as oldest_created_at,
        MAX(bj.created_at) as newest_created_at
    FROM background_jobs bj
    GROUP BY bj.status, bj.job_type
    ORDER BY bj.status, bj.job_type;
END;
$$ LANGUAGE plpgsql;

-- Function: Move stuck jobs back to pending
-- Purpose: Recover from worker crashes (jobs stuck in 'processing' state)
CREATE OR REPLACE FUNCTION recover_stuck_background_jobs(stuck_threshold_minutes INTEGER DEFAULT 60)
RETURNS TABLE(recovered_count BIGINT) AS $$
BEGIN
    WITH recovered AS (
        UPDATE background_jobs
        SET
            status = 'pending',
            receipt_handle = NULL,
            visible_after = NOW()
        WHERE status = 'processing'
          AND last_received_at < NOW() - (stuck_threshold_minutes || ' minutes')::INTERVAL
        RETURNING *
    )
    SELECT COUNT(*)::BIGINT INTO recovered_count FROM recovered;

    RETURN QUERY SELECT recovered_count;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Comments for documentation
-- ============================================================================

COMMENT ON TABLE background_jobs IS 'Background job queue for async processing (Tier 1-2 PostgreSQL polling)';
COMMENT ON COLUMN background_jobs.message_id IS 'Unique message identifier (UUID)';
COMMENT ON COLUMN background_jobs.job_type IS 'Job type name for routing and filtering';
COMMENT ON COLUMN background_jobs.payload IS 'Job payload as JSON';
COMMENT ON COLUMN background_jobs.status IS 'Job status: pending, processing, completed, failed';
COMMENT ON COLUMN background_jobs.priority IS 'Job priority 1-10 (higher = more urgent)';
COMMENT ON COLUMN background_jobs.visible_after IS 'Message becomes visible after this timestamp (visibility timeout)';
COMMENT ON COLUMN background_jobs.delivery_count IS 'Number of times message has been received';
COMMENT ON COLUMN background_jobs.receipt_handle IS 'Unique handle for current processing attempt';
COMMENT ON COLUMN background_jobs.deduplication_id IS 'Deduplication ID for exactly-once delivery';
COMMENT ON COLUMN background_jobs.message_group_id IS 'Message group for FIFO ordering';

-- ============================================================================
-- Grants (adjust based on your security model)
-- ============================================================================

-- Example: Grant permissions to application role
-- GRANT SELECT, INSERT, UPDATE, DELETE ON background_jobs TO honua_app;
-- GRANT EXECUTE ON FUNCTION cleanup_background_jobs(INTEGER) TO honua_app;
-- GRANT EXECUTE ON FUNCTION get_background_jobs_stats() TO honua_app;
-- GRANT EXECUTE ON FUNCTION recover_stuck_background_jobs(INTEGER) TO honua_app;
