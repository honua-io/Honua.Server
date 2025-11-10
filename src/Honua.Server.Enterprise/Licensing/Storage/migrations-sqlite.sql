-- SQLite Migration Script for Honua License Management
-- This script creates the necessary tables for license management and credential revocation

-- ============================================================================
-- LICENSES TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS licenses (
    id TEXT PRIMARY KEY,
    customer_id TEXT NOT NULL UNIQUE,
    license_key TEXT NOT NULL,
    tier TEXT NOT NULL CHECK(tier IN ('Free', 'Professional', 'Enterprise')),
    status TEXT NOT NULL CHECK(status IN ('Active', 'Expired', 'Suspended', 'Revoked', 'Pending')),
    issued_at TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    features TEXT NOT NULL,
    revoked_at TEXT,
    email TEXT NOT NULL,
    metadata TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_licenses_customer_id ON licenses(customer_id);
CREATE INDEX IF NOT EXISTS idx_licenses_expires_at ON licenses(expires_at);
CREATE INDEX IF NOT EXISTS idx_licenses_status ON licenses(status);
CREATE INDEX IF NOT EXISTS idx_licenses_revoked_at ON licenses(revoked_at) WHERE revoked_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_licenses_active_expiring ON licenses(expires_at)
    WHERE status = 'Active' AND revoked_at IS NULL;

-- ============================================================================
-- CREDENTIAL_REVOCATIONS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS credential_revocations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    customer_id TEXT NOT NULL,
    registry_type TEXT NOT NULL CHECK(registry_type IN ('AWS', 'Azure', 'GCP', 'GitHub')),
    revoked_at TEXT NOT NULL DEFAULT (datetime('now')),
    reason TEXT,
    revoked_by TEXT
);

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_credential_revocations_customer_id ON credential_revocations(customer_id);
CREATE INDEX IF NOT EXISTS idx_credential_revocations_revoked_at ON credential_revocations(revoked_at);
CREATE INDEX IF NOT EXISTS idx_credential_revocations_registry_type ON credential_revocations(registry_type);

-- ============================================================================
-- TRIGGERS
-- ============================================================================

-- Trigger to automatically update updated_at timestamp
CREATE TRIGGER IF NOT EXISTS trigger_licenses_updated_at
    AFTER UPDATE ON licenses
    FOR EACH ROW
BEGIN
    UPDATE licenses SET updated_at = datetime('now') WHERE id = NEW.id;
END;

-- ============================================================================
-- SAMPLE QUERIES
-- ============================================================================

-- Find licenses expiring in the next 7 days
-- SELECT * FROM licenses
-- WHERE status = 'Active'
--   AND revoked_at IS NULL
--   AND expires_at > datetime('now')
--   AND expires_at <= datetime('now', '+7 days')
-- ORDER BY expires_at ASC;

-- Find all expired licenses that haven't been revoked
-- SELECT * FROM licenses
-- WHERE expires_at <= datetime('now')
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
