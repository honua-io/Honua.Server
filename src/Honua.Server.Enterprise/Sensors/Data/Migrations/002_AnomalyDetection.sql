-- ============================================================================
-- Sensor Anomaly Detection Schema Migration
-- ============================================================================
-- This migration adds support for anomaly detection alert tracking
-- Used for rate limiting and alert deduplication
-- ============================================================================

-- Enable UUID generation if not already enabled
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- ANOMALY ALERTS (Alert tracking for rate limiting)
-- ============================================================================
CREATE TABLE IF NOT EXISTS sta_anomaly_alerts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    datastream_id UUID NOT NULL,
    anomaly_type VARCHAR(50) NOT NULL,
    tenant_id VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_anomaly_alert_datastream
        FOREIGN KEY (datastream_id)
        REFERENCES sta_datastreams(id)
        ON DELETE CASCADE
);

-- Index for efficient rate limit checks per datastream
CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_datastream
    ON sta_anomaly_alerts(datastream_id, anomaly_type, created_at DESC);

-- Index for tenant-scoped queries
CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_tenant
    ON sta_anomaly_alerts(tenant_id, created_at DESC);

-- Index for global rate limit queries
CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_created
    ON sta_anomaly_alerts(created_at DESC);

-- Composite index for efficient rate limit lookups
CREATE INDEX IF NOT EXISTS idx_sta_anomaly_alerts_composite
    ON sta_anomaly_alerts(datastream_id, anomaly_type, tenant_id, created_at DESC);

COMMENT ON TABLE sta_anomaly_alerts IS 'Tracks anomaly alerts for rate limiting and deduplication';
COMMENT ON COLUMN sta_anomaly_alerts.datastream_id IS 'Reference to the datastream where anomaly was detected';
COMMENT ON COLUMN sta_anomaly_alerts.anomaly_type IS 'Type of anomaly: StaleSensor, UnusualReading, SensorOffline, OutOfRange';
COMMENT ON COLUMN sta_anomaly_alerts.tenant_id IS 'Tenant identifier for multi-tenant deployments';
COMMENT ON COLUMN sta_anomaly_alerts.created_at IS 'When the alert was generated';

-- ============================================================================
-- CLEANUP FUNCTION (Optional: Automatically remove old alerts)
-- ============================================================================
-- This function can be called periodically to clean up old alert records
-- that are no longer needed for rate limiting (older than 7 days)

CREATE OR REPLACE FUNCTION cleanup_old_anomaly_alerts()
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM sta_anomaly_alerts
    WHERE created_at < NOW() - INTERVAL '7 days';

    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION cleanup_old_anomaly_alerts() IS 'Removes alert records older than 7 days';

-- ============================================================================
-- PERFORMANCE OPTIMIZATION
-- ============================================================================
-- Create statistics for query planner optimization
ANALYZE sta_anomaly_alerts;

-- ============================================================================
-- VERIFICATION QUERIES (for testing)
-- ============================================================================
-- Verify table was created:
-- SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename = 'sta_anomaly_alerts';

-- Verify indexes:
-- SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'sta_anomaly_alerts';

-- Count alerts:
-- SELECT COUNT(*) FROM sta_anomaly_alerts;

-- Recent alerts by type:
-- SELECT anomaly_type, COUNT(*) as count, MAX(created_at) as latest
-- FROM sta_anomaly_alerts
-- WHERE created_at >= NOW() - INTERVAL '1 hour'
-- GROUP BY anomaly_type;
