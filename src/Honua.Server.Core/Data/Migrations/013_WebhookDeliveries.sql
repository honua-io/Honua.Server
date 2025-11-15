-- =====================================================
-- Webhook Delivery Queue Schema
-- Migration: 013_WebhookDeliveries.sql
-- Version: 013
-- Dependencies: 012_Geoprocessing.sql
-- Purpose: Reliable webhook delivery with retry mechanism
--          for geoprocessing job completion notifications
-- =====================================================

-- Webhook deliveries table - queued webhooks with retry tracking
CREATE TABLE IF NOT EXISTS webhook_deliveries (
    -- Identification
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id VARCHAR(100) NOT NULL,
    webhook_url TEXT NOT NULL,

    -- Payload
    payload JSONB NOT NULL,
    headers JSONB, -- Custom headers to include in request

    -- Status & Timing
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    next_retry_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_attempt_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,

    -- Retry tracking
    attempt_count INTEGER NOT NULL DEFAULT 0,
    max_attempts INTEGER NOT NULL DEFAULT 5,

    -- Response tracking
    last_response_status INTEGER,
    last_response_body TEXT,
    last_error_message TEXT,

    -- Metadata
    tenant_id VARCHAR(100) NOT NULL,
    process_id VARCHAR(100) NOT NULL,

    -- Constraints
    CONSTRAINT fk_webhook_deliveries_job FOREIGN KEY (job_id)
        REFERENCES process_runs(job_id) ON DELETE CASCADE,
    CONSTRAINT fk_webhook_deliveries_tenant FOREIGN KEY (tenant_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT chk_status CHECK (status IN ('pending', 'processing', 'delivered', 'failed', 'abandoned')),
    CONSTRAINT chk_attempt_count CHECK (attempt_count >= 0 AND attempt_count <= max_attempts)
);

-- Indexes for efficient querying

-- Queue processing - get next pending webhooks ordered by retry time
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_queue ON webhook_deliveries(next_retry_at ASC)
    WHERE status = 'pending' AND attempt_count < max_attempts;

-- Job lookup - find webhooks for a specific job
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_job ON webhook_deliveries(job_id, created_at DESC);

-- Tenant queries
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_tenant ON webhook_deliveries(tenant_id, created_at DESC);

-- Status tracking
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_status ON webhook_deliveries(status, created_at DESC);

-- Failed deliveries for monitoring
CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_failed ON webhook_deliveries(last_attempt_at DESC)
    WHERE status = 'failed' OR status = 'abandoned';

-- =====================================================
-- Functions for webhook queue management
-- =====================================================

-- Atomically dequeue next webhook for delivery
CREATE OR REPLACE FUNCTION dequeue_webhook_delivery()
RETURNS TABLE(
    id UUID,
    job_id VARCHAR(100),
    webhook_url TEXT,
    payload JSONB,
    headers JSONB,
    attempt_count INTEGER,
    max_attempts INTEGER
) AS $$
DECLARE
    selected_webhook RECORD;
BEGIN
    -- Select and lock next webhook atomically
    SELECT * INTO selected_webhook
    FROM webhook_deliveries
    WHERE status = 'pending'
      AND attempt_count < max_attempts
      AND next_retry_at <= NOW()
    ORDER BY next_retry_at ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED;

    IF selected_webhook IS NULL THEN
        RETURN;
    END IF;

    -- Mark as processing
    UPDATE webhook_deliveries
    SET
        status = 'processing',
        last_attempt_at = NOW(),
        attempt_count = attempt_count + 1
    WHERE webhook_deliveries.id = selected_webhook.id;

    -- Return webhook info
    RETURN QUERY
    SELECT
        selected_webhook.id,
        selected_webhook.job_id,
        selected_webhook.webhook_url,
        selected_webhook.payload,
        selected_webhook.headers,
        selected_webhook.attempt_count + 1, -- Return incremented count
        selected_webhook.max_attempts;
END;
$$ LANGUAGE plpgsql;

-- Record successful webhook delivery
CREATE OR REPLACE FUNCTION record_webhook_delivery_success(
    p_id UUID,
    p_response_status INTEGER,
    p_response_body TEXT DEFAULT NULL
)
RETURNS VOID AS $$
BEGIN
    UPDATE webhook_deliveries
    SET
        status = 'delivered',
        completed_at = NOW(),
        last_response_status = p_response_status,
        last_response_body = p_response_body,
        last_error_message = NULL
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Record failed webhook delivery attempt (with exponential backoff for retry)
CREATE OR REPLACE FUNCTION record_webhook_delivery_failure(
    p_id UUID,
    p_response_status INTEGER DEFAULT NULL,
    p_error_message TEXT DEFAULT NULL
)
RETURNS VOID AS $$
DECLARE
    v_attempt_count INTEGER;
    v_max_attempts INTEGER;
    v_next_retry_delay INTERVAL;
    v_new_status VARCHAR(50);
BEGIN
    -- Get current attempt count
    SELECT attempt_count, max_attempts
    INTO v_attempt_count, v_max_attempts
    FROM webhook_deliveries
    WHERE id = p_id;

    -- Determine if we should retry or abandon
    IF v_attempt_count >= v_max_attempts THEN
        v_new_status := 'abandoned';
        v_next_retry_delay := NULL;
    ELSE
        v_new_status := 'pending';
        -- Exponential backoff: 1min, 2min, 4min, 8min, 16min
        v_next_retry_delay := (POWER(2, v_attempt_count) || ' minutes')::INTERVAL;
    END IF;

    -- Update delivery record
    UPDATE webhook_deliveries
    SET
        status = v_new_status,
        last_response_status = p_response_status,
        last_error_message = p_error_message,
        next_retry_at = CASE
            WHEN v_next_retry_delay IS NOT NULL THEN NOW() + v_next_retry_delay
            ELSE next_retry_at
        END,
        completed_at = CASE
            WHEN v_new_status = 'abandoned' THEN NOW()
            ELSE completed_at
        END
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Get webhook delivery statistics
CREATE OR REPLACE FUNCTION get_webhook_delivery_statistics(
    p_tenant_id VARCHAR(100) DEFAULT NULL,
    p_start_time TIMESTAMPTZ DEFAULT NULL,
    p_end_time TIMESTAMPTZ DEFAULT NULL
)
RETURNS TABLE(
    total_deliveries BIGINT,
    delivered_count BIGINT,
    pending_count BIGINT,
    failed_count BIGINT,
    abandoned_count BIGINT,
    average_attempts DOUBLE PRECISION,
    delivery_success_rate DOUBLE PRECISION
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COUNT(*)::BIGINT as total_deliveries,
        COUNT(*) FILTER (WHERE status = 'delivered')::BIGINT as delivered_count,
        COUNT(*) FILTER (WHERE status = 'pending')::BIGINT as pending_count,
        COUNT(*) FILTER (WHERE status = 'failed')::BIGINT as failed_count,
        COUNT(*) FILTER (WHERE status = 'abandoned')::BIGINT as abandoned_count,
        AVG(attempt_count) FILTER (WHERE status IN ('delivered', 'abandoned')) as average_attempts,
        CASE
            WHEN COUNT(*) FILTER (WHERE status IN ('delivered', 'abandoned')) > 0
            THEN (COUNT(*) FILTER (WHERE status = 'delivered')::DOUBLE PRECISION /
                  COUNT(*) FILTER (WHERE status IN ('delivered', 'abandoned'))::DOUBLE PRECISION) * 100.0
            ELSE 0.0
        END as delivery_success_rate
    FROM webhook_deliveries
    WHERE
        (p_tenant_id IS NULL OR tenant_id = p_tenant_id)
        AND (p_start_time IS NULL OR created_at >= p_start_time)
        AND (p_end_time IS NULL OR created_at <= p_end_time);
END;
$$ LANGUAGE plpgsql;

-- Cleanup old delivered webhooks
CREATE OR REPLACE FUNCTION cleanup_old_webhook_deliveries(older_than_days INTEGER DEFAULT 30)
RETURNS BIGINT AS $$
DECLARE
    deleted_count BIGINT;
BEGIN
    WITH deleted AS (
        DELETE FROM webhook_deliveries
        WHERE
            completed_at IS NOT NULL
            AND completed_at < NOW() - (older_than_days || ' days')::INTERVAL
            AND status IN ('delivered', 'abandoned')
        RETURNING *
    )
    SELECT COUNT(*) INTO deleted_count FROM deleted;

    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Views for monitoring
-- =====================================================

-- Pending webhook deliveries
CREATE OR REPLACE VIEW pending_webhook_deliveries AS
SELECT
    id,
    job_id,
    webhook_url,
    created_at,
    next_retry_at,
    attempt_count,
    max_attempts,
    tenant_id,
    process_id,
    EXTRACT(EPOCH FROM (next_retry_at - NOW())) as seconds_until_retry
FROM webhook_deliveries
WHERE status = 'pending'
ORDER BY next_retry_at ASC;

-- Failed webhook deliveries requiring attention
CREATE OR REPLACE VIEW failed_webhook_deliveries AS
SELECT
    id,
    job_id,
    webhook_url,
    created_at,
    last_attempt_at,
    attempt_count,
    max_attempts,
    last_response_status,
    last_error_message,
    tenant_id,
    process_id,
    status
FROM webhook_deliveries
WHERE status IN ('failed', 'abandoned')
ORDER BY last_attempt_at DESC;

-- Webhook delivery metrics (last 24 hours)
CREATE OR REPLACE VIEW webhook_delivery_metrics_24h AS
SELECT
    tenant_id,
    COUNT(*) as total_webhooks,
    COUNT(*) FILTER (WHERE status = 'delivered') as delivered,
    COUNT(*) FILTER (WHERE status = 'abandoned') as abandoned,
    COUNT(*) FILTER (WHERE status = 'pending') as pending,
    AVG(attempt_count) FILTER (WHERE status IN ('delivered', 'abandoned')) as avg_attempts,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY attempt_count)
        FILTER (WHERE status IN ('delivered', 'abandoned')) as p95_attempts,
    AVG(EXTRACT(EPOCH FROM (completed_at - created_at)))
        FILTER (WHERE completed_at IS NOT NULL) as avg_delivery_time_seconds
FROM webhook_deliveries
WHERE created_at >= NOW() - INTERVAL '24 hours'
GROUP BY tenant_id;

-- =====================================================
-- Comments for documentation
-- =====================================================

COMMENT ON TABLE webhook_deliveries IS 'Webhook delivery queue with retry mechanism for geoprocessing job notifications';
COMMENT ON COLUMN webhook_deliveries.id IS 'Unique delivery identifier';
COMMENT ON COLUMN webhook_deliveries.job_id IS 'Associated geoprocessing job ID';
COMMENT ON COLUMN webhook_deliveries.webhook_url IS 'Target webhook URL';
COMMENT ON COLUMN webhook_deliveries.payload IS 'Webhook payload (JSON)';
COMMENT ON COLUMN webhook_deliveries.status IS 'Delivery status: pending, processing, delivered, failed, abandoned';
COMMENT ON COLUMN webhook_deliveries.attempt_count IS 'Number of delivery attempts made';
COMMENT ON COLUMN webhook_deliveries.next_retry_at IS 'When to retry delivery (exponential backoff)';

COMMENT ON FUNCTION dequeue_webhook_delivery IS 'Atomically gets next webhook from queue (with locking)';
COMMENT ON FUNCTION record_webhook_delivery_success IS 'Records successful webhook delivery';
COMMENT ON FUNCTION record_webhook_delivery_failure IS 'Records failed delivery attempt with exponential backoff';
COMMENT ON FUNCTION get_webhook_delivery_statistics IS 'Returns webhook delivery statistics';
COMMENT ON FUNCTION cleanup_old_webhook_deliveries IS 'Deletes old delivered/abandoned webhooks';

COMMENT ON VIEW pending_webhook_deliveries IS 'Webhooks waiting to be delivered';
COMMENT ON VIEW failed_webhook_deliveries IS 'Failed webhook deliveries requiring attention';
COMMENT ON VIEW webhook_delivery_metrics_24h IS 'Webhook delivery metrics for last 24 hours';

-- =====================================================
-- Grant permissions
-- =====================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON webhook_deliveries TO honua_app;
GRANT SELECT ON pending_webhook_deliveries TO honua_app;
GRANT SELECT ON failed_webhook_deliveries TO honua_app;
GRANT SELECT ON webhook_delivery_metrics_24h TO honua_app;
