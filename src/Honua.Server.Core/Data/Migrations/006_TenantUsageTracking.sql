-- ============================================================================
-- Tenant Usage Tracking and Metering
-- ============================================================================
-- Version: 006
-- Description: Tables for tracking tenant resource usage and enforcing quotas
-- ============================================================================

-- ============================================================================
-- Tenant Usage Aggregates (Monthly)
-- ============================================================================
-- Stores aggregated usage metrics per tenant per month

CREATE TABLE IF NOT EXISTS tenant_usage (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(100) NOT NULL,

    -- Period
    period_start TIMESTAMPTZ NOT NULL,
    period_end TIMESTAMPTZ NOT NULL,

    -- API Usage
    api_requests BIGINT NOT NULL DEFAULT 0,
    api_errors INT NOT NULL DEFAULT 0,

    -- Storage
    storage_bytes BIGINT NOT NULL DEFAULT 0,
    dataset_count INT NOT NULL DEFAULT 0,

    -- Processing
    raster_processing_minutes INT NOT NULL DEFAULT 0,
    vector_processing_requests BIGINT NOT NULL DEFAULT 0,

    -- Builds
    builds INT NOT NULL DEFAULT 0,

    -- Exports
    export_requests INT NOT NULL DEFAULT 0,
    export_size_bytes BIGINT NOT NULL DEFAULT 0,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    CONSTRAINT fk_tenant_usage_customer FOREIGN KEY (tenant_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT unique_tenant_period UNIQUE (tenant_id, period_start)
);

CREATE INDEX idx_tenant_usage_tenant_period ON tenant_usage(tenant_id, period_start DESC);
CREATE INDEX idx_tenant_usage_period ON tenant_usage(period_start, period_end);
CREATE INDEX idx_tenant_usage_tenant_fk ON tenant_usage(tenant_id);

COMMENT ON TABLE tenant_usage IS 'Monthly aggregated usage metrics per tenant';
COMMENT ON COLUMN tenant_usage.api_requests IS 'Total API requests in this period';
COMMENT ON COLUMN tenant_usage.storage_bytes IS 'Current storage usage in bytes';
COMMENT ON COLUMN tenant_usage.raster_processing_minutes IS 'Total raster processing time in minutes';

-- ============================================================================
-- Tenant Usage Events (Detailed)
-- ============================================================================
-- Stores detailed usage events for auditing and analytics

CREATE TABLE IF NOT EXISTS tenant_usage_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(100) NOT NULL,

    -- Event details
    event_type VARCHAR(50) NOT NULL,
    event_data JSONB DEFAULT '{}',

    -- Metadata
    ip_address INET,
    user_agent TEXT,
    request_id VARCHAR(100),

    -- Timestamp
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    CONSTRAINT fk_tenant_events_customer FOREIGN KEY (tenant_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE
);

-- Partitioning by month for better performance
CREATE INDEX idx_tenant_events_tenant_time ON tenant_usage_events(tenant_id, created_at DESC);
CREATE INDEX idx_tenant_events_type ON tenant_usage_events(event_type);
CREATE INDEX idx_tenant_events_created ON tenant_usage_events(created_at DESC);
CREATE INDEX idx_tenant_events_tenant_fk ON tenant_usage_events(tenant_id);

COMMENT ON TABLE tenant_usage_events IS 'Detailed usage event log for analytics and auditing';
COMMENT ON COLUMN tenant_usage_events.event_type IS 'Type of event: api_request, raster_processing, vector_processing, build, export';
COMMENT ON COLUMN tenant_usage_events.event_data IS 'JSON metadata about the event';

-- ============================================================================
-- Tenant Quota Overrides
-- ============================================================================
-- Allows custom quota overrides per tenant

CREATE TABLE IF NOT EXISTS tenant_quota_overrides (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(100) NOT NULL UNIQUE,

    -- Storage Quotas
    max_storage_bytes BIGINT,
    max_datasets INT,

    -- API Quotas
    max_api_requests_per_month BIGINT,
    max_concurrent_requests INT,
    rate_limit_per_minute INT,

    -- Processing Quotas
    max_raster_processing_minutes_per_month INT,
    max_vector_processing_requests_per_month BIGINT,

    -- Build Quotas
    max_builds_per_month INT,

    -- Export Quotas
    max_export_size_bytes BIGINT,

    -- Metadata
    reason TEXT,
    approved_by VARCHAR(100),

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    CONSTRAINT fk_quota_overrides_customer FOREIGN KEY (tenant_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE
);

CREATE INDEX idx_quota_overrides_tenant ON tenant_quota_overrides(tenant_id);

COMMENT ON TABLE tenant_quota_overrides IS 'Custom quota overrides for specific tenants';
COMMENT ON COLUMN tenant_quota_overrides.max_storage_bytes IS 'NULL means use tier default';

-- ============================================================================
-- Functions for Usage Management
-- ============================================================================

-- Function to get current month usage for a tenant
CREATE OR REPLACE FUNCTION get_tenant_usage_current_month(p_tenant_id VARCHAR(100))
RETURNS TABLE (
    api_requests BIGINT,
    storage_bytes BIGINT,
    raster_processing_minutes INT,
    vector_processing_requests BIGINT,
    builds INT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        COALESCE(tu.api_requests, 0)::BIGINT,
        COALESCE(tu.storage_bytes, 0)::BIGINT,
        COALESCE(tu.raster_processing_minutes, 0)::INT,
        COALESCE(tu.vector_processing_requests, 0)::BIGINT,
        COALESCE(tu.builds, 0)::INT
    FROM tenant_usage tu
    WHERE tu.tenant_id = p_tenant_id
      AND tu.period_start = DATE_TRUNC('month', NOW())
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;

-- Function to check if tenant exceeded quota
CREATE OR REPLACE FUNCTION is_quota_exceeded(
    p_tenant_id VARCHAR(100),
    p_quota_type VARCHAR(50)
) RETURNS BOOLEAN AS $$
DECLARE
    v_usage BIGINT;
    v_quota BIGINT;
    v_tier VARCHAR(20);
BEGIN
    -- Get tenant tier
    SELECT tier INTO v_tier
    FROM customers
    WHERE customer_id = p_tenant_id AND deleted_at IS NULL;

    IF v_tier IS NULL THEN
        RETURN TRUE; -- Tenant not found, block request
    END IF;

    -- Get current usage
    IF p_quota_type = 'api_requests' THEN
        SELECT api_requests INTO v_usage
        FROM tenant_usage
        WHERE tenant_id = p_tenant_id
          AND period_start = DATE_TRUNC('month', NOW());

        -- Get quota for tier
        v_quota := CASE v_tier
            WHEN 'trial' THEN 10000
            WHEN 'core' THEN 100000
            WHEN 'pro' THEN 1000000
            ELSE NULL -- Unlimited for enterprise
        END;
    END IF;

    -- Check if quota is exceeded
    IF v_quota IS NULL THEN
        RETURN FALSE; -- Unlimited
    END IF;

    RETURN COALESCE(v_usage, 0) >= v_quota;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Triggers for Usage Tracking
-- ============================================================================

-- Trigger to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_tenant_usage_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tenant_usage_updated
    BEFORE UPDATE ON tenant_usage
    FOR EACH ROW
    EXECUTE FUNCTION update_tenant_usage_timestamp();

-- ============================================================================
-- Sample Data (for testing)
-- ============================================================================

-- Initialize usage for existing tenants (optional)
-- INSERT INTO tenant_usage (tenant_id, period_start, period_end)
-- SELECT
--     customer_id,
--     DATE_TRUNC('month', NOW()),
--     DATE_TRUNC('month', NOW()) + INTERVAL '1 month'
-- FROM customers
-- WHERE deleted_at IS NULL
-- ON CONFLICT (tenant_id, period_start) DO NOTHING;

-- Record migration
INSERT INTO schema_migrations (version, name, checksum, execution_time_ms)
VALUES ('006', 'TenantUsageTracking', 'PLACEHOLDER_CHECKSUM', 0)
ON CONFLICT (version) DO NOTHING;
