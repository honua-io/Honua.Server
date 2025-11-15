-- PostgreSQL Migration Script for Alert Delivery Dead Letter Queue
-- This script creates the necessary table for tracking failed alert deliveries

-- ============================================================================
-- ALERT_DELIVERY_FAILURES TABLE (Dead Letter Queue)
-- ============================================================================
CREATE TABLE IF NOT EXISTS alert_delivery_failures (
    id BIGSERIAL PRIMARY KEY,
    alert_fingerprint TEXT NOT NULL,
    alert_payload JSONB NOT NULL,
    target_channel TEXT NOT NULL,
    error_message TEXT NOT NULL,
    error_details JSONB NULL,
    retry_count INTEGER NOT NULL DEFAULT 0,
    failed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_retry_at TIMESTAMPTZ NULL,
    next_retry_at TIMESTAMPTZ NULL,
    resolved_at TIMESTAMPTZ NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    severity TEXT NOT NULL,
    CONSTRAINT chk_status CHECK (status IN ('pending', 'retrying', 'resolved', 'abandoned'))
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_fingerprint ON alert_delivery_failures(alert_fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_status ON alert_delivery_failures(status);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_next_retry ON alert_delivery_failures(next_retry_at)
    WHERE status = 'pending' AND next_retry_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_failed_at ON alert_delivery_failures(failed_at);
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_channel_status ON alert_delivery_failures(target_channel, status);

-- Add GIN index for JSONB alert_payload column for efficient querying
CREATE INDEX IF NOT EXISTS idx_alert_delivery_failures_payload ON alert_delivery_failures USING GIN(alert_payload);

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE alert_delivery_failures IS 'Dead letter queue for alert deliveries that failed after retry attempts';
COMMENT ON COLUMN alert_delivery_failures.id IS 'Unique identifier for the failed delivery';
COMMENT ON COLUMN alert_delivery_failures.alert_fingerprint IS 'Unique fingerprint of the alert for correlation';
COMMENT ON COLUMN alert_delivery_failures.alert_payload IS 'Full AlertManagerWebhook payload as JSON';
COMMENT ON COLUMN alert_delivery_failures.target_channel IS 'Destination channel that failed (e.g., Slack, PagerDuty, Opsgenie)';
COMMENT ON COLUMN alert_delivery_failures.error_message IS 'Error message from the failed delivery attempt';
COMMENT ON COLUMN alert_delivery_failures.error_details IS 'Additional error context and stack traces';
COMMENT ON COLUMN alert_delivery_failures.retry_count IS 'Number of retry attempts made';
COMMENT ON COLUMN alert_delivery_failures.failed_at IS 'When the delivery first failed';
COMMENT ON COLUMN alert_delivery_failures.last_retry_at IS 'When the last retry attempt was made';
COMMENT ON COLUMN alert_delivery_failures.next_retry_at IS 'Scheduled time for next retry attempt';
COMMENT ON COLUMN alert_delivery_failures.resolved_at IS 'When the delivery was successfully retried (NULL if not resolved)';
COMMENT ON COLUMN alert_delivery_failures.status IS 'Current status (pending, retrying, resolved, abandoned)';
COMMENT ON COLUMN alert_delivery_failures.severity IS 'Alert severity for prioritization';

-- ============================================================================
-- SAMPLE QUERIES
-- ============================================================================

-- Find alerts ready for retry
-- SELECT * FROM alert_delivery_failures
-- WHERE status = 'pending'
--   AND next_retry_at IS NOT NULL
--   AND next_retry_at <= NOW()
--   AND retry_count < 5
-- ORDER BY severity DESC, next_retry_at ASC
-- LIMIT 50;

-- Get failure statistics by channel
-- SELECT target_channel, status, COUNT(*) as count
-- FROM alert_delivery_failures
-- GROUP BY target_channel, status
-- ORDER BY target_channel, status;

-- Find recurring failures for the same alert
-- SELECT alert_fingerprint, target_channel, COUNT(*) as failure_count
-- FROM alert_delivery_failures
-- WHERE failed_at > NOW() - INTERVAL '24 hours'
-- GROUP BY alert_fingerprint, target_channel
-- HAVING COUNT(*) > 3
-- ORDER BY failure_count DESC;

-- Clean up old resolved/abandoned failures (retention policy)
-- DELETE FROM alert_delivery_failures
-- WHERE status IN ('resolved', 'abandoned')
--   AND (resolved_at < NOW() - INTERVAL '30 days' OR failed_at < NOW() - INTERVAL '30 days');
