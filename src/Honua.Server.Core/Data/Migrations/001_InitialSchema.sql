-- ============================================================================
-- Honua Build Orchestrator - Initial Schema
-- ============================================================================
-- Version: 001
-- Description: Core tables for build cache, licenses, credentials, and customers
-- Prerequisites: PostgreSQL 15+ with pgcrypto and uuid-ossp extensions
-- ============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- Schema Migrations Tracking
-- ============================================================================
-- Tracks which migrations have been applied to the database

CREATE TABLE IF NOT EXISTS schema_migrations (
    version VARCHAR(50) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    applied_by VARCHAR(100) NOT NULL DEFAULT CURRENT_USER,
    checksum VARCHAR(64) NOT NULL,
    execution_time_ms INTEGER NOT NULL
);

COMMENT ON TABLE schema_migrations IS 'Tracks database schema migration history';
COMMENT ON COLUMN schema_migrations.version IS 'Migration version identifier (e.g., 001, 002)';
COMMENT ON COLUMN schema_migrations.checksum IS 'SHA-256 checksum of migration file for integrity verification';

-- ============================================================================
-- Customers Table
-- ============================================================================
-- Core customer/tenant metadata

CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL UNIQUE,

    -- Identity
    organization_name VARCHAR(255) NOT NULL,
    contact_email VARCHAR(255) NOT NULL,
    contact_name VARCHAR(255),

    -- Subscription
    subscription_status VARCHAR(20) NOT NULL DEFAULT 'trial'
        CHECK (subscription_status IN ('trial', 'active', 'suspended', 'cancelled')),
    tier VARCHAR(20) NOT NULL DEFAULT 'core'
        CHECK (tier IN ('core', 'pro', 'enterprise', 'asp')),

    -- Metadata
    metadata JSONB DEFAULT '{}',
    tags VARCHAR(50)[],

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

CREATE INDEX idx_customers_customer_id ON customers(customer_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_customers_tier ON customers(tier) WHERE deleted_at IS NULL;
CREATE INDEX idx_customers_subscription_status ON customers(subscription_status) WHERE deleted_at IS NULL;
CREATE INDEX idx_customers_contact_email ON customers(contact_email) WHERE deleted_at IS NULL;

COMMENT ON TABLE customers IS 'Customer/tenant records for the build orchestrator system';
COMMENT ON COLUMN customers.customer_id IS 'External customer identifier used in API calls and logs';
COMMENT ON COLUMN customers.tier IS 'License tier determining feature access (core/pro/enterprise/asp)';
COMMENT ON COLUMN customers.subscription_status IS 'Current subscription state';
COMMENT ON COLUMN customers.metadata IS 'Flexible JSON metadata for custom customer attributes';
COMMENT ON COLUMN customers.deleted_at IS 'Soft delete timestamp - NULL means active customer';

-- ============================================================================
-- Licenses Table
-- ============================================================================
-- Customer license management with feature flags and expiration

CREATE TABLE licenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL UNIQUE,

    -- License key (encrypted)
    license_key TEXT NOT NULL,
    license_key_hash VARCHAR(64) NOT NULL UNIQUE,

    -- Tier and status
    tier VARCHAR(20) NOT NULL CHECK (tier IN ('core', 'pro', 'enterprise', 'asp')),
    status VARCHAR(20) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'expired', 'revoked', 'trial')),

    -- Validity periods
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    trial_expires_at TIMESTAMPTZ,

    -- Feature flags (JSONB for flexibility)
    features JSONB NOT NULL DEFAULT '{
        "ai_intake": true,
        "custom_modules": true,
        "priority_builds": false,
        "dedicated_cache": false,
        "sla_guarantee": false
    }',

    -- Quota limits
    max_builds_per_month INTEGER DEFAULT 100,
    max_registries INTEGER DEFAULT 3,
    max_concurrent_builds INTEGER DEFAULT 1,

    -- Revocation
    revoked_at TIMESTAMPTZ,
    revoked_by VARCHAR(100),
    revoked_reason TEXT,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign key
    CONSTRAINT fk_licenses_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE
);

CREATE INDEX idx_licenses_customer_id ON licenses(customer_id);
CREATE INDEX idx_licenses_status ON licenses(status);
CREATE INDEX idx_licenses_expires_at ON licenses(expires_at) WHERE status = 'active';
CREATE INDEX idx_licenses_tier ON licenses(tier);
CREATE INDEX idx_licenses_trial_expires ON licenses(trial_expires_at) WHERE status = 'trial';

COMMENT ON TABLE licenses IS 'Customer licenses with tier, features, and expiration tracking';
COMMENT ON COLUMN licenses.license_key IS 'Encrypted license key for customer verification';
COMMENT ON COLUMN licenses.license_key_hash IS 'SHA-256 hash of license key for quick lookups';
COMMENT ON COLUMN licenses.features IS 'Feature flags as JSON - controls what customer can access';
COMMENT ON COLUMN licenses.max_builds_per_month IS 'Monthly build quota - NULL means unlimited';
COMMENT ON COLUMN licenses.max_registries IS 'Maximum number of container registries customer can configure';
COMMENT ON COLUMN licenses.trial_expires_at IS 'Trial expiration - only set when status is trial';

-- ============================================================================
-- Registry Credentials Table
-- ============================================================================
-- Encrypted customer container registry credentials

CREATE TABLE registry_credentials (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(100) NOT NULL,

    -- Registry identification
    registry_name VARCHAR(100) NOT NULL,
    registry_url TEXT NOT NULL,
    registry_provider VARCHAR(50) NOT NULL
        CHECK (registry_provider IN ('dockerhub', 'ecr', 'gcr', 'acr', 'harbor', 'quay', 'custom')),

    -- Encrypted credentials
    username_encrypted BYTEA NOT NULL,
    password_encrypted BYTEA NOT NULL,
    encryption_key_id VARCHAR(100) NOT NULL,

    -- Credential metadata
    is_default BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,

    -- Validation
    last_validated_at TIMESTAMPTZ,
    validation_status VARCHAR(20) CHECK (validation_status IN ('valid', 'invalid', 'unknown')),
    validation_error TEXT,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMPTZ,

    -- Foreign key
    CONSTRAINT fk_registry_credentials_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE,

    -- Unique constraint: one default registry per customer
    CONSTRAINT uq_default_registry_per_customer UNIQUE (customer_id, is_default)
        WHERE is_default = TRUE
);

CREATE INDEX idx_registry_credentials_customer_id ON registry_credentials(customer_id) WHERE revoked_at IS NULL;
CREATE INDEX idx_registry_credentials_is_active ON registry_credentials(is_active) WHERE revoked_at IS NULL;
CREATE INDEX idx_registry_credentials_provider ON registry_credentials(registry_provider);
CREATE INDEX idx_registry_credentials_validation_status ON registry_credentials(validation_status);

COMMENT ON TABLE registry_credentials IS 'Encrypted container registry credentials for customer image pushes';
COMMENT ON COLUMN registry_credentials.registry_name IS 'Human-readable registry identifier (e.g., "Production ECR")';
COMMENT ON COLUMN registry_credentials.username_encrypted IS 'AES-256 encrypted username/access key';
COMMENT ON COLUMN registry_credentials.password_encrypted IS 'AES-256 encrypted password/secret key';
COMMENT ON COLUMN registry_credentials.encryption_key_id IS 'Reference to encryption key in KMS/vault';
COMMENT ON COLUMN registry_credentials.is_default IS 'Default registry for customer builds';
COMMENT ON COLUMN registry_credentials.last_validated_at IS 'Last successful credential validation timestamp';

-- ============================================================================
-- Build Cache Registry Table
-- ============================================================================
-- Shared build cache across customers - deduplication based on manifest hash

CREATE TABLE build_cache_registry (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Cache key (manifest hash)
    manifest_hash VARCHAR(16) NOT NULL UNIQUE,
    tier VARCHAR(20) NOT NULL CHECK (tier IN ('core', 'pro', 'enterprise', 'asp')),

    -- Image location
    registry_url TEXT NOT NULL,
    image_tag TEXT NOT NULL,
    full_image_url TEXT NOT NULL,

    -- Build composition (what's in this build)
    modules TEXT[] NOT NULL,
    architecture VARCHAR(20) NOT NULL CHECK (architecture IN ('amd64', 'arm64')),
    cloud_provider VARCHAR(20) NOT NULL CHECK (cloud_provider IN ('aws', 'azure', 'gcp', 'kubernetes')),
    cloud_compute VARCHAR(50) NOT NULL,
    deployment_mode VARCHAR(20) NOT NULL CHECK (deployment_mode IN ('standalone', 'distributed', 'serverless')),

    -- Build metadata
    first_built_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    first_built_by_customer VARCHAR(100), -- NULLABLE to allow ON DELETE SET NULL
    build_duration_seconds NUMERIC(10,2) NOT NULL,
    image_size_bytes BIGINT,
    image_layers INTEGER,

    -- Cache statistics
    cache_hit_count INTEGER DEFAULT 0,
    last_accessed_at TIMESTAMPTZ,
    total_customers_served INTEGER DEFAULT 1,

    -- Build manifest (full manifest for reconstruction)
    manifest_json JSONB NOT NULL,

    -- Lifecycle management
    expires_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,
    deletion_reason VARCHAR(255),

    -- Foreign key
    CONSTRAINT fk_build_cache_first_customer FOREIGN KEY (first_built_by_customer)
        REFERENCES customers(customer_id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX idx_build_cache_manifest_hash ON build_cache_registry(manifest_hash) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_cache_tier ON build_cache_registry(tier) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_cache_last_accessed ON build_cache_registry(last_accessed_at DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_cache_first_built ON build_cache_registry(first_built_at DESC);
CREATE INDEX idx_build_cache_cloud_provider ON build_cache_registry(cloud_provider) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_cache_architecture ON build_cache_registry(architecture) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_cache_first_customer ON build_cache_registry(first_built_by_customer) WHERE first_built_by_customer IS NOT NULL;

-- GIN index for array and JSONB columns
CREATE INDEX idx_build_cache_modules_gin ON build_cache_registry USING GIN(modules);
CREATE INDEX idx_build_cache_manifest_gin ON build_cache_registry USING GIN(manifest_json);

COMMENT ON TABLE build_cache_registry IS 'Shared build cache registry - deduplicates builds across customers';
COMMENT ON COLUMN build_cache_registry.manifest_hash IS 'MD5 hash of normalized build manifest - cache lookup key';
COMMENT ON COLUMN build_cache_registry.tier IS 'Minimum tier required to use this cached build';
COMMENT ON COLUMN build_cache_registry.modules IS 'Array of Honua modules included in build';
COMMENT ON COLUMN build_cache_registry.cache_hit_count IS 'Number of times this build was reused';
COMMENT ON COLUMN build_cache_registry.total_customers_served IS 'Unique customer count who used this build';
COMMENT ON COLUMN build_cache_registry.manifest_json IS 'Complete build manifest for audit and recreation';
COMMENT ON COLUMN build_cache_registry.expires_at IS 'Cache expiration - NULL means never expires';

-- ============================================================================
-- Build Access Log Table
-- ============================================================================
-- Tracks which customers accessed which cached builds

CREATE TABLE build_access_log (
    id BIGSERIAL PRIMARY KEY,

    -- References
    build_cache_id UUID NOT NULL,
    customer_id VARCHAR(100) NOT NULL,

    -- Access details
    accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    access_type VARCHAR(20) NOT NULL
        CHECK (access_type IN ('cache_hit', 'cache_miss', 'build_created')),

    -- Deployment context
    deployment_id VARCHAR(100),
    deployment_region VARCHAR(100),

    -- Performance metrics
    pull_duration_seconds NUMERIC(10,2),
    download_size_bytes BIGINT,

    -- Audit
    user_agent TEXT,
    ip_address INET,

    -- Foreign keys
    CONSTRAINT fk_build_access_build FOREIGN KEY (build_cache_id)
        REFERENCES build_cache_registry(id) ON DELETE CASCADE,
    CONSTRAINT fk_build_access_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE
);

CREATE INDEX idx_build_access_log_build_cache_id ON build_access_log(build_cache_id);
CREATE INDEX idx_build_access_log_customer_id ON build_access_log(customer_id);
CREATE INDEX idx_build_access_log_accessed_at ON build_access_log(accessed_at DESC);
CREATE INDEX idx_build_access_log_access_type ON build_access_log(access_type);
CREATE INDEX idx_build_access_log_deployment_id ON build_access_log(deployment_id);

-- Partitioning hint: Consider partitioning by accessed_at (monthly) for high-volume systems
COMMENT ON TABLE build_access_log IS 'Access log for build cache - tracks cache hits and customer usage patterns';
COMMENT ON COLUMN build_access_log.access_type IS 'Type of access: cache_hit (reused), cache_miss (new build), build_created (first build)';
COMMENT ON COLUMN build_access_log.deployment_id IS 'Associated deployment ID if this access was part of a deployment';

-- ============================================================================
-- Initial Data / Reference Values
-- ============================================================================

-- Insert default system customer for internal builds
INSERT INTO customers (customer_id, organization_name, contact_email, subscription_status, tier)
VALUES ('system', 'Honua System', 'system@honua.io', 'active', 'enterprise')
ON CONFLICT (customer_id) DO NOTHING;

-- ============================================================================
-- Grants (adjust based on your application user)
-- ============================================================================
-- Example grants - uncomment and adjust for your database user
-- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO honua_app_user;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO honua_app_user;
