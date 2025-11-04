-- MySQL Migration Script for Honua License Management
-- This script creates the necessary tables for license management and credential revocation

-- ============================================================================
-- LICENSES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS licenses (
    id CHAR(36) PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL UNIQUE,
    license_key TEXT NOT NULL,
    tier VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    issued_at TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at TIMESTAMP(6) NOT NULL,
    features JSON NOT NULL,
    revoked_at TIMESTAMP(6) NULL,
    email VARCHAR(255) NOT NULL,
    metadata JSON NULL,
    created_at TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT chk_tier CHECK (tier IN ('Free', 'Professional', 'Enterprise')),
    CONSTRAINT chk_status CHECK (status IN ('Active', 'Expired', 'Suspended', 'Revoked', 'Pending'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create indexes for efficient queries
CREATE INDEX idx_licenses_customer_id ON licenses(customer_id);
CREATE INDEX idx_licenses_expires_at ON licenses(expires_at);
CREATE INDEX idx_licenses_status ON licenses(status);
CREATE INDEX idx_licenses_revoked_at ON licenses(revoked_at);
CREATE INDEX idx_licenses_active_expiring ON licenses(status, revoked_at, expires_at);

-- ============================================================================
-- CREDENTIAL_REVOCATIONS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS credential_revocations (
    id INT AUTO_INCREMENT PRIMARY KEY,
    customer_id VARCHAR(100) NOT NULL,
    registry_type VARCHAR(20) NOT NULL,
    revoked_at TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reason TEXT NULL,
    revoked_by VARCHAR(100) NULL,
    CONSTRAINT chk_registry_type CHECK (registry_type IN ('AWS', 'Azure', 'GCP', 'GitHub'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create indexes for efficient queries
CREATE INDEX idx_credential_revocations_customer_id ON credential_revocations(customer_id);
CREATE INDEX idx_credential_revocations_revoked_at ON credential_revocations(revoked_at);
CREATE INDEX idx_credential_revocations_registry_type ON credential_revocations(registry_type);

-- ============================================================================
-- SAMPLE QUERIES
-- ============================================================================

-- Find licenses expiring in the next 7 days
-- SELECT * FROM licenses
-- WHERE status = 'Active'
--   AND revoked_at IS NULL
--   AND expires_at > NOW()
--   AND expires_at <= DATE_ADD(NOW(), INTERVAL 7 DAY)
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
