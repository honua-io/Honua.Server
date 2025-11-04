-- ============================================================================
-- Honua Build Orchestrator - Database Functions and Triggers
-- ============================================================================
-- Version: 005
-- Description: Stored procedures, functions, and triggers for automation
-- Dependencies: 001_InitialSchema.sql, 002_BuildQueue.sql, 003_IntakeSystem.sql
-- ============================================================================

-- ============================================================================
-- Utility Functions
-- ============================================================================

-- Update timestamp trigger function (reusable)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION update_updated_at_column() IS 'Automatically updates updated_at timestamp on row modification';

-- ============================================================================
-- Apply updated_at Triggers
-- ============================================================================

-- Customers table
DROP TRIGGER IF EXISTS trigger_customers_updated_at ON customers;
CREATE TRIGGER trigger_customers_updated_at
    BEFORE UPDATE ON customers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Licenses table
DROP TRIGGER IF EXISTS trigger_licenses_updated_at ON licenses;
CREATE TRIGGER trigger_licenses_updated_at
    BEFORE UPDATE ON licenses
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Registry credentials table
DROP TRIGGER IF EXISTS trigger_registry_credentials_updated_at ON registry_credentials;
CREATE TRIGGER trigger_registry_credentials_updated_at
    BEFORE UPDATE ON registry_credentials
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Build queue table
DROP TRIGGER IF EXISTS trigger_build_queue_updated_at ON build_queue;
CREATE TRIGGER trigger_build_queue_updated_at
    BEFORE UPDATE ON build_queue
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Build manifests table
DROP TRIGGER IF EXISTS trigger_build_manifests_updated_at ON build_manifests;
CREATE TRIGGER trigger_build_manifests_updated_at
    BEFORE UPDATE ON build_manifests
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Conversations table
DROP TRIGGER IF EXISTS trigger_conversations_updated_at ON conversations;
CREATE TRIGGER trigger_conversations_updated_at
    BEFORE UPDATE ON conversations
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- Cache Statistics Functions
-- ============================================================================

-- Function to update cache statistics when accessed
CREATE OR REPLACE FUNCTION update_cache_stats()
RETURNS TRIGGER AS $$
BEGIN
    -- Only update for cache hits
    IF NEW.access_type = 'cache_hit' THEN
        UPDATE build_cache_registry
        SET
            cache_hit_count = cache_hit_count + 1,
            last_accessed_at = NEW.accessed_at,
            total_customers_served = (
                SELECT COUNT(DISTINCT customer_id)
                FROM build_access_log
                WHERE build_cache_id = NEW.build_cache_id
            )
        WHERE id = NEW.build_cache_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION update_cache_stats() IS 'Updates cache hit statistics when builds are accessed';

-- Apply trigger for cache statistics
DROP TRIGGER IF EXISTS trigger_update_cache_stats ON build_access_log;
CREATE TRIGGER trigger_update_cache_stats
    AFTER INSERT ON build_access_log
    FOR EACH ROW
    EXECUTE FUNCTION update_cache_stats();

-- ============================================================================
-- Build Access Logging Function
-- ============================================================================

-- Function to record cache access (callable from application)
CREATE OR REPLACE FUNCTION record_cache_access(
    p_build_cache_id UUID,
    p_customer_id VARCHAR(100),
    p_access_type VARCHAR(20),
    p_deployment_id VARCHAR(100) DEFAULT NULL,
    p_deployment_region VARCHAR(100) DEFAULT NULL,
    p_pull_duration_seconds NUMERIC(10,2) DEFAULT NULL,
    p_download_size_bytes BIGINT DEFAULT NULL,
    p_user_agent TEXT DEFAULT NULL,
    p_ip_address INET DEFAULT NULL
)
RETURNS BIGINT AS $$
DECLARE
    v_access_log_id BIGINT;
BEGIN
    -- Insert access log entry
    INSERT INTO build_access_log (
        build_cache_id,
        customer_id,
        access_type,
        deployment_id,
        deployment_region,
        pull_duration_seconds,
        download_size_bytes,
        user_agent,
        ip_address
    ) VALUES (
        p_build_cache_id,
        p_customer_id,
        p_access_type,
        p_deployment_id,
        p_deployment_region,
        p_pull_duration_seconds,
        p_download_size_bytes,
        p_user_agent,
        p_ip_address
    )
    RETURNING id INTO v_access_log_id;

    RETURN v_access_log_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION record_cache_access IS 'Records build cache access with automatic statistics update';

-- ============================================================================
-- Conversation Management Functions
-- ============================================================================

-- Function to update conversation message count
CREATE OR REPLACE FUNCTION update_conversation_stats()
RETURNS TRIGGER AS $$
BEGIN
    UPDATE conversations
    SET
        total_messages = total_messages + 1,
        last_message_at = NEW.created_at,
        total_tokens_used = total_tokens_used + COALESCE(NEW.total_tokens, 0)
    WHERE id = NEW.conversation_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION update_conversation_stats() IS 'Updates conversation statistics when messages are added';

-- Apply trigger for conversation statistics
DROP TRIGGER IF EXISTS trigger_update_conversation_stats ON conversation_messages;
CREATE TRIGGER trigger_update_conversation_stats
    AFTER INSERT ON conversation_messages
    FOR EACH ROW
    EXECUTE FUNCTION update_conversation_stats();

-- ============================================================================
-- Cleanup Functions
-- ============================================================================

-- Function to expire old conversations
CREATE OR REPLACE FUNCTION expire_old_conversations(
    p_inactive_hours INTEGER DEFAULT 24
)
RETURNS TABLE(
    conversation_id UUID,
    customer_email VARCHAR(255),
    abandoned_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    UPDATE conversations
    SET
        status = 'abandoned',
        abandoned_at = NOW()
    WHERE
        status = 'active'
        AND last_message_at < NOW() - (p_inactive_hours || ' hours')::INTERVAL
    RETURNING id, conversations.customer_email, conversations.abandoned_at;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION expire_old_conversations IS 'Marks inactive conversations as abandoned (default: 24 hours)';

-- Function to revoke expired credentials
CREATE OR REPLACE FUNCTION revoke_expired_credentials()
RETURNS TABLE(
    credential_id UUID,
    customer_id VARCHAR(100),
    registry_name VARCHAR(100),
    revoked_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    UPDATE registry_credentials
    SET
        is_active = FALSE,
        revoked_at = NOW()
    WHERE
        is_active = TRUE
        AND revoked_at IS NULL
        AND validation_status = 'invalid'
        AND last_validated_at < NOW() - INTERVAL '7 days'
    RETURNING id, registry_credentials.customer_id, registry_credentials.registry_name, registry_credentials.revoked_at;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION revoke_expired_credentials IS 'Revokes credentials that have been invalid for 7+ days';

-- Function to expire licenses
CREATE OR REPLACE FUNCTION expire_licenses()
RETURNS TABLE(
    license_id UUID,
    customer_id VARCHAR(100),
    expired_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    UPDATE licenses
    SET status = 'expired'
    WHERE
        status IN ('active', 'trial')
        AND (
            (status = 'active' AND expires_at < NOW())
            OR (status = 'trial' AND trial_expires_at < NOW())
        )
    RETURNING id, licenses.customer_id, NOW();
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION expire_licenses IS 'Marks expired licenses based on expiration dates';

-- Function to clean up old build logs (for data retention)
CREATE OR REPLACE FUNCTION cleanup_old_build_logs(
    p_retention_days INTEGER DEFAULT 90
)
RETURNS TABLE(
    deleted_count BIGINT
) AS $$
DECLARE
    v_deleted_count BIGINT;
BEGIN
    -- Truncate logs in build_results older than retention period
    UPDATE build_results
    SET
        stdout_log = '[Truncated - Retention period exceeded]',
        stderr_log = '[Truncated - Retention period exceeded]'
    WHERE
        created_at < NOW() - (p_retention_days || ' days')::INTERVAL
        AND (stdout_log IS NOT NULL OR stderr_log IS NOT NULL)
        AND LENGTH(COALESCE(stdout_log, '')) + LENGTH(COALESCE(stderr_log, '')) > 1000;

    GET DIAGNOSTICS v_deleted_count = ROW_COUNT;

    RETURN QUERY SELECT v_deleted_count;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION cleanup_old_build_logs IS 'Truncates old build logs beyond retention period (default: 90 days)';

-- Function to purge soft-deleted records
CREATE OR REPLACE FUNCTION purge_soft_deleted_records(
    p_purge_after_days INTEGER DEFAULT 30
)
RETURNS TABLE(
    table_name TEXT,
    records_purged BIGINT
) AS $$
DECLARE
    v_customers_purged BIGINT;
    v_cache_purged BIGINT;
    v_manifests_purged BIGINT;
BEGIN
    -- Purge customers
    DELETE FROM customers
    WHERE deleted_at < NOW() - (p_purge_after_days || ' days')::INTERVAL;
    GET DIAGNOSTICS v_customers_purged = ROW_COUNT;

    -- Purge build cache
    DELETE FROM build_cache_registry
    WHERE deleted_at < NOW() - (p_purge_after_days || ' days')::INTERVAL;
    GET DIAGNOSTICS v_cache_purged = ROW_COUNT;

    -- Purge manifests
    DELETE FROM build_manifests
    WHERE deleted_at < NOW() - (p_purge_after_days || ' days')::INTERVAL;
    GET DIAGNOSTICS v_manifests_purged = ROW_COUNT;

    RETURN QUERY
    SELECT 'customers'::TEXT, v_customers_purged
    UNION ALL
    SELECT 'build_cache_registry'::TEXT, v_cache_purged
    UNION ALL
    SELECT 'build_manifests'::TEXT, v_manifests_purged;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION purge_soft_deleted_records IS 'Permanently deletes soft-deleted records (default: after 30 days)';

-- ============================================================================
-- Build Queue Management Functions
-- ============================================================================

-- Function to get next build from queue
CREATE OR REPLACE FUNCTION get_next_build_for_worker(
    p_worker_id VARCHAR(100),
    p_worker_hostname VARCHAR(255),
    p_architecture VARCHAR(20) DEFAULT NULL
)
RETURNS TABLE(
    build_id UUID,
    manifest_json JSONB,
    customer_id VARCHAR(100),
    priority INTEGER,
    retry_count INTEGER
) AS $$
DECLARE
    v_build_id UUID;
BEGIN
    -- Lock and assign next build to worker
    SELECT id INTO v_build_id
    FROM build_queue
    WHERE
        status = 'queued'
        AND (p_architecture IS NULL OR architecture = p_architecture)
        AND retry_count < max_retries
    ORDER BY priority DESC, queued_at ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    -- Update build status and worker assignment
    IF v_build_id IS NOT NULL THEN
        UPDATE build_queue
        SET
            status = 'building',
            worker_id = p_worker_id,
            worker_hostname = p_worker_hostname,
            worker_assigned_at = NOW(),
            started_at = NOW(),
            timeout_at = NOW() + (max_duration_seconds || ' seconds')::INTERVAL
        WHERE id = v_build_id;

        -- Return build details
        RETURN QUERY
        SELECT
            bq.id,
            bq.manifest_json,
            bq.customer_id,
            bq.priority,
            bq.retry_count
        FROM build_queue bq
        WHERE bq.id = v_build_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_next_build_for_worker IS 'Atomically assigns next build to worker (SKIP LOCKED for concurrency)';

-- Function to update build progress
CREATE OR REPLACE FUNCTION update_build_progress(
    p_build_id UUID,
    p_progress_percent INTEGER,
    p_current_step VARCHAR(255),
    p_steps_completed INTEGER DEFAULT NULL
)
RETURNS BOOLEAN AS $$
BEGIN
    UPDATE build_queue
    SET
        progress_percent = p_progress_percent,
        current_step = p_current_step,
        steps_completed = COALESCE(p_steps_completed, steps_completed)
    WHERE
        id = p_build_id
        AND status = 'building';

    RETURN FOUND;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION update_build_progress IS 'Updates build progress for real-time monitoring';

-- Function to handle build timeout
CREATE OR REPLACE FUNCTION handle_build_timeouts()
RETURNS TABLE(
    build_id UUID,
    customer_id VARCHAR(100),
    worker_id VARCHAR(100)
) AS $$
BEGIN
    RETURN QUERY
    UPDATE build_queue
    SET
        status = 'timeout',
        completed_at = NOW()
    WHERE
        status = 'building'
        AND timeout_at < NOW()
    RETURNING id, build_queue.customer_id, build_queue.worker_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION handle_build_timeouts IS 'Marks timed-out builds as failed (run as background job)';

-- Function to retry failed build
CREATE OR REPLACE FUNCTION retry_failed_build(
    p_build_id UUID
)
RETURNS BOOLEAN AS $$
BEGIN
    UPDATE build_queue
    SET
        status = 'queued',
        retry_count = retry_count + 1,
        last_retry_at = NOW(),
        worker_id = NULL,
        worker_hostname = NULL,
        worker_assigned_at = NULL,
        started_at = NULL,
        completed_at = NULL,
        timeout_at = NULL,
        progress_percent = 0,
        current_step = NULL,
        steps_completed = 0
    WHERE
        id = p_build_id
        AND status IN ('failed', 'timeout')
        AND retry_count < max_retries;

    RETURN FOUND;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION retry_failed_build IS 'Requeues a failed build for retry';

-- ============================================================================
-- Analytics Functions
-- ============================================================================

-- Function to calculate cache hit rate
CREATE OR REPLACE FUNCTION get_cache_hit_rate(
    p_customer_id VARCHAR(100) DEFAULT NULL,
    p_days INTEGER DEFAULT 30
)
RETURNS TABLE(
    customer_id VARCHAR(100),
    total_accesses BIGINT,
    cache_hits BIGINT,
    cache_misses BIGINT,
    hit_rate_percent NUMERIC(5,2)
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        bal.customer_id,
        COUNT(*)::BIGINT AS total_accesses,
        COUNT(*) FILTER (WHERE bal.access_type = 'cache_hit')::BIGINT AS cache_hits,
        COUNT(*) FILTER (WHERE bal.access_type = 'cache_miss')::BIGINT AS cache_misses,
        ROUND(
            (COUNT(*) FILTER (WHERE bal.access_type = 'cache_hit')::NUMERIC / NULLIF(COUNT(*), 0) * 100),
            2
        ) AS hit_rate_percent
    FROM build_access_log bal
    WHERE
        bal.accessed_at >= NOW() - (p_days || ' days')::INTERVAL
        AND (p_customer_id IS NULL OR bal.customer_id = p_customer_id)
    GROUP BY bal.customer_id
    ORDER BY hit_rate_percent DESC;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_cache_hit_rate IS 'Calculates cache hit rate by customer';

-- Function to get license quota usage
CREATE OR REPLACE FUNCTION get_license_quota_usage(
    p_customer_id VARCHAR(100)
)
RETURNS TABLE(
    max_builds_per_month INTEGER,
    builds_this_month BIGINT,
    remaining_builds INTEGER,
    quota_utilization_percent NUMERIC(5,2),
    max_registries INTEGER,
    active_registries BIGINT,
    max_concurrent_builds INTEGER,
    current_concurrent_builds BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        l.max_builds_per_month,
        COUNT(bq.id) FILTER (WHERE DATE_TRUNC('month', bq.queued_at) = DATE_TRUNC('month', NOW()))::BIGINT AS builds_this_month,
        GREATEST(0, l.max_builds_per_month - COUNT(bq.id) FILTER (WHERE DATE_TRUNC('month', bq.queued_at) = DATE_TRUNC('month', NOW()))::INTEGER) AS remaining_builds,
        ROUND(
            (COUNT(bq.id) FILTER (WHERE DATE_TRUNC('month', bq.queued_at) = DATE_TRUNC('month', NOW()))::NUMERIC / NULLIF(l.max_builds_per_month, 0) * 100),
            2
        ) AS quota_utilization_percent,
        l.max_registries,
        COUNT(DISTINCT rc.id) FILTER (WHERE rc.is_active = TRUE AND rc.revoked_at IS NULL)::BIGINT AS active_registries,
        l.max_concurrent_builds,
        COUNT(bq.id) FILTER (WHERE bq.status = 'building')::BIGINT AS current_concurrent_builds
    FROM licenses l
    LEFT JOIN build_queue bq ON l.customer_id = bq.customer_id
    LEFT JOIN registry_credentials rc ON l.customer_id = rc.customer_id
    WHERE l.customer_id = p_customer_id
    GROUP BY l.id, l.max_builds_per_month, l.max_registries, l.max_concurrent_builds;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_license_quota_usage IS 'Returns current quota usage for a customer license';

-- ============================================================================
-- Validation Functions
-- ============================================================================

-- Function to validate build queue before insert
CREATE OR REPLACE FUNCTION validate_build_queue_insert()
RETURNS TRIGGER AS $$
DECLARE
    v_license RECORD;
    v_current_quota RECORD;
BEGIN
    -- Get license and quota information
    SELECT * INTO v_license
    FROM licenses
    WHERE customer_id = NEW.customer_id
      AND status IN ('active', 'trial');

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No active license found for customer: %', NEW.customer_id;
    END IF;

    -- Check quota
    SELECT * INTO v_current_quota
    FROM get_license_quota_usage(NEW.customer_id);

    -- Check monthly build quota
    IF v_license.max_builds_per_month IS NOT NULL AND
       v_current_quota.builds_this_month >= v_license.max_builds_per_month THEN
        RAISE EXCEPTION 'Monthly build quota exceeded for customer: % (limit: %, used: %)',
            NEW.customer_id, v_license.max_builds_per_month, v_current_quota.builds_this_month;
    END IF;

    -- Check concurrent build quota
    IF v_license.max_concurrent_builds IS NOT NULL AND
       v_current_quota.current_concurrent_builds >= v_license.max_concurrent_builds THEN
        RAISE EXCEPTION 'Concurrent build quota exceeded for customer: % (limit: %, current: %)',
            NEW.customer_id, v_license.max_concurrent_builds, v_current_quota.current_concurrent_builds;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION validate_build_queue_insert() IS 'Validates license quotas before queueing builds';

-- Apply validation trigger (OPTIONAL - uncomment to enable quota enforcement)
-- DROP TRIGGER IF EXISTS trigger_validate_build_queue_insert ON build_queue;
-- CREATE TRIGGER trigger_validate_build_queue_insert
--     BEFORE INSERT ON build_queue
--     FOR EACH ROW
--     EXECUTE FUNCTION validate_build_queue_insert();

-- ============================================================================
-- Maintenance Functions
-- ============================================================================

-- Function to vacuum and analyze tables
CREATE OR REPLACE FUNCTION maintenance_vacuum_analyze()
RETURNS TABLE(
    table_name TEXT,
    completed_at TIMESTAMPTZ
) AS $$
BEGIN
    -- Note: VACUUM cannot be run inside a transaction block
    -- This function serves as documentation for maintenance tasks
    -- Execute these commands directly via psql or pg_cron

    RETURN QUERY
    SELECT 'Run these commands directly via psql or pg_cron:'::TEXT, NOW()
    UNION ALL
    SELECT 'VACUUM ANALYZE build_access_log;'::TEXT, NOW()
    UNION ALL
    SELECT 'VACUUM ANALYZE build_events;'::TEXT, NOW()
    UNION ALL
    SELECT 'VACUUM ANALYZE conversation_messages;'::TEXT, NOW()
    UNION ALL
    SELECT 'VACUUM ANALYZE build_metrics;'::TEXT, NOW();
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION maintenance_vacuum_analyze IS 'Lists maintenance VACUUM commands (run via psql or pg_cron)';

-- ============================================================================
-- Example: Schedule cleanup functions with pg_cron (if installed)
-- ============================================================================
-- SELECT cron.schedule('expire-conversations', '0 */6 * * *', 'SELECT expire_old_conversations(24);');
-- SELECT cron.schedule('revoke-credentials', '0 2 * * *', 'SELECT revoke_expired_credentials();');
-- SELECT cron.schedule('expire-licenses', '0 1 * * *', 'SELECT expire_licenses();');
-- SELECT cron.schedule('handle-timeouts', '* * * * *', 'SELECT handle_build_timeouts();');
-- SELECT cron.schedule('cleanup-logs', '0 3 * * 0', 'SELECT cleanup_old_build_logs(90);');
-- SELECT cron.schedule('purge-deleted', '0 4 * * 0', 'SELECT purge_soft_deleted_records(30);');
