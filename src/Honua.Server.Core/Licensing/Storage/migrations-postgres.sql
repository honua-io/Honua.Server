-- PostgreSQL Migration Script for Honua License Management
-- This script creates the necessary tables for license management and credential revocation

-- ============================================================================
-- LICENSES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS licenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL UNIQUE,
    license_key TEXT NOT NULL,
    tier VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    features JSONB NOT NULL DEFAULT '{}'::jsonb,
    revoked_at TIMESTAMPTZ,
    email VARCHAR(255) NOT NULL,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_tier CHECK (tier IN ('Free', 'Professional', 'Enterprise')),
    CONSTRAINT chk_status CHECK (status IN ('Active', 'Expired', 'Suspended', 'Revoked', 'Pending'))
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_licenses_customer_id ON licenses(customer_id);
CREATE INDEX IF NOT EXISTS idx_licenses_expires_at ON licenses(expires_at);
CREATE INDEX IF NOT EXISTS idx_licenses_status ON licenses(status);
CREATE INDEX IF NOT EXISTS idx_licenses_revoked_at ON licenses(revoked_at) WHERE revoked_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_licenses_active_expiring ON licenses(expires_at)
    WHERE status = 'Active' AND revoked_at IS NULL;

-- Add GIN index for JSONB features column for efficient feature lookups
CREATE INDEX IF NOT EXISTS idx_licenses_features ON licenses USING GIN(features);

-- ============================================================================
-- CREDENTIAL_REVOCATIONS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS credential_revocations (
    id SERIAL PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL,
    registry_type VARCHAR(20) NOT NULL,
    revoked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason TEXT,
    revoked_by VARCHAR(100),
    CONSTRAINT chk_registry_type CHECK (registry_type IN ('AWS', 'Azure', 'GCP', 'GitHub'))
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_credential_revocations_customer_id ON credential_revocations(customer_id);
CREATE INDEX IF NOT EXISTS idx_credential_revocations_revoked_at ON credential_revocations(revoked_at);
CREATE INDEX IF NOT EXISTS idx_credential_revocations_registry_type ON credential_revocations(registry_type);

-- ============================================================================
-- TRIGGERS
-- ============================================================================

-- Trigger to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_licenses_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_licenses_updated_at
    BEFORE UPDATE ON licenses
    FOR EACH ROW
    EXECUTE FUNCTION update_licenses_updated_at();

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE licenses IS 'Stores customer license information with JWT keys and expiration tracking';
COMMENT ON COLUMN licenses.id IS 'Unique license identifier';
COMMENT ON COLUMN licenses.customer_id IS 'Unique customer identifier';
COMMENT ON COLUMN licenses.license_key IS 'JWT-based license key';
COMMENT ON COLUMN licenses.tier IS 'License tier (Free, Professional, Enterprise)';
COMMENT ON COLUMN licenses.status IS 'Current license status';
COMMENT ON COLUMN licenses.issued_at IS 'When the license was originally issued';
COMMENT ON COLUMN licenses.expires_at IS 'When the license expires';
COMMENT ON COLUMN licenses.features IS 'JSON object containing enabled features and quotas';
COMMENT ON COLUMN licenses.revoked_at IS 'When the license was revoked (NULL if not revoked)';
COMMENT ON COLUMN licenses.email IS 'Customer email address for notifications';
COMMENT ON COLUMN licenses.metadata IS 'Additional metadata (optional)';

COMMENT ON TABLE credential_revocations IS 'Audit log of credential revocations';
COMMENT ON COLUMN credential_revocations.customer_id IS 'Customer whose credentials were revoked';
COMMENT ON COLUMN credential_revocations.registry_type IS 'Type of registry (AWS, Azure, GCP, GitHub)';
COMMENT ON COLUMN credential_revocations.revoked_at IS 'When the credentials were revoked';
COMMENT ON COLUMN credential_revocations.reason IS 'Reason for revocation';
COMMENT ON COLUMN credential_revocations.revoked_by IS 'Who initiated the revocation (user or System)';

-- ============================================================================
-- SAMPLE QUERIES
-- ============================================================================

-- Find licenses expiring in the next 7 days
-- SELECT * FROM licenses
-- WHERE status = 'Active'
--   AND revoked_at IS NULL
--   AND expires_at > NOW()
--   AND expires_at <= NOW() + INTERVAL '7 days'
-- ORDER BY expires_at ASC;

-- Find all expired licenses that haven't been revoked
-- SELECT * FROM licenses
-- WHERE expires_at <= NOW()
--   AND revoked_at IS NULL
--   AND status != 'Revoked'
-- ORDER BY expires_at ASC;

-- Get revocation history for a customer
-- SELECT * FROM credential_revocations
-- WHERE customer_id = 'customer-123'
-- ORDER BY revoked_at DESC;

-- Count licenses by tier
-- SELECT tier, COUNT(*) as count
-- FROM licenses
-- WHERE status = 'Active'
-- GROUP BY tier;
