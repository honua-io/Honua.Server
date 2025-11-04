-- ============================================================================
-- Honua Server - Soft Delete Implementation
-- ============================================================================
-- Version: 009
-- Description: Adds soft delete support with audit trails for GDPR compliance
-- Prerequisites: Migrations 001-008
-- Note: This migration defines functions for soft delete but does not add columns
--       to tables. The soft delete columns should be added to specific tables
--       when they are created (stac_collections, stac_items, auth_users, etc.)
-- ============================================================================

-- ============================================================================
-- Soft Delete Audit Log Table
-- ============================================================================
-- Comprehensive audit trail for all deletion operations (soft and hard)
-- Critical for GDPR compliance, SOC2, and data recovery

CREATE TABLE IF NOT EXISTS deletion_audit_log (
    id BIGSERIAL PRIMARY KEY,

    -- Entity identification
    entity_type VARCHAR(100) NOT NULL,
    entity_id VARCHAR(255) NOT NULL,

    -- Deletion metadata
    deletion_type VARCHAR(10) NOT NULL CHECK (deletion_type IN ('soft', 'hard', 'restore')),
    deleted_by VARCHAR(255),
    deleted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Reason and context
    reason TEXT,
    ip_address INET,
    user_agent TEXT,

    -- Entity snapshot for audit and recovery
    entity_metadata_snapshot JSONB,

    -- GDPR compliance fields
    is_data_subject_request BOOLEAN DEFAULT FALSE,
    data_subject_request_id VARCHAR(100),

    -- Additional metadata
    metadata JSONB DEFAULT '{}'::jsonb
);

-- Indexes for efficient audit queries
CREATE INDEX IF NOT EXISTS idx_deletion_audit_entity ON deletion_audit_log(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS idx_deletion_audit_deleted_at ON deletion_audit_log(deleted_at DESC);
CREATE INDEX IF NOT EXISTS idx_deletion_audit_deleted_by ON deletion_audit_log(deleted_by) WHERE deleted_by IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_deletion_audit_type ON deletion_audit_log(deletion_type);
CREATE INDEX IF NOT EXISTS idx_deletion_audit_dsr ON deletion_audit_log(data_subject_request_id)
    WHERE is_data_subject_request = TRUE;

COMMENT ON TABLE deletion_audit_log IS 'Audit trail for all deletion operations (soft delete, hard delete, restore)';
COMMENT ON COLUMN deletion_audit_log.entity_type IS 'Type of entity (Feature, StacCollection, StacItem, AuthUser, etc)';
COMMENT ON COLUMN deletion_audit_log.entity_id IS 'Unique identifier of the deleted entity';
COMMENT ON COLUMN deletion_audit_log.deletion_type IS 'Type of operation: soft, hard, or restore';
COMMENT ON COLUMN deletion_audit_log.entity_metadata_snapshot IS 'JSON snapshot of entity metadata for audit and recovery';
COMMENT ON COLUMN deletion_audit_log.is_data_subject_request IS 'GDPR: True if deletion was part of a data subject request';

-- ============================================================================
-- Add Soft Delete Columns to Tables (Template)
-- ============================================================================
-- NOTE: The following table alterations are commented out because the tables
--       (stac_collections, stac_items, auth_users) are not part of the core schema.
--       When you create these tables in your application, add the soft delete columns:
--
--   is_deleted BOOLEAN NOT NULL DEFAULT FALSE
--   deleted_at TIMESTAMPTZ
--   deleted_by VARCHAR(255)
--
-- Then create indexes:
--   CREATE INDEX idx_{table}_is_deleted ON {table}(is_deleted) WHERE is_deleted = FALSE;
--   CREATE INDEX idx_{table}_deleted_at ON {table}(deleted_at) WHERE deleted_at IS NOT NULL;
-- ============================================================================

/*
-- Example for STAC Collections (uncomment when table exists)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'stac_collections' AND column_name = 'is_deleted') THEN
        ALTER TABLE stac_collections ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE stac_collections ADD COLUMN deleted_at TIMESTAMPTZ;
        ALTER TABLE stac_collections ADD COLUMN deleted_by VARCHAR(255);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_stac_collections_is_deleted ON stac_collections(is_deleted) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_stac_collections_deleted_at ON stac_collections(deleted_at) WHERE deleted_at IS NOT NULL;
*/

/*
-- Example for STAC Items (uncomment when table exists)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'stac_items' AND column_name = 'is_deleted') THEN
        ALTER TABLE stac_items ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE stac_items ADD COLUMN deleted_at TIMESTAMPTZ;
        ALTER TABLE stac_items ADD COLUMN deleted_by VARCHAR(255);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_stac_items_is_deleted ON stac_items(is_deleted) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_stac_items_deleted_at ON stac_items(deleted_at) WHERE deleted_at IS NOT NULL;
*/

/*
-- Example for Auth Users (uncomment when table exists)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'auth_users' AND column_name = 'is_deleted') THEN
        ALTER TABLE auth_users ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE auth_users ADD COLUMN deleted_at TIMESTAMPTZ;
        ALTER TABLE auth_users ADD COLUMN deleted_by VARCHAR(255);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_auth_users_is_deleted ON auth_users(is_deleted) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_auth_users_deleted_at ON auth_users(deleted_at) WHERE deleted_at IS NOT NULL;
*/

-- ============================================================================
-- Helper Function: Soft Delete Entity
-- ============================================================================
-- Generic function to perform soft delete with audit logging
-- SECURITY: Whitelist validation prevents SQL injection

CREATE OR REPLACE FUNCTION soft_delete_entity(
    p_table_name TEXT,
    p_entity_id TEXT,
    p_entity_type TEXT,
    p_deleted_by TEXT DEFAULT NULL,
    p_reason TEXT DEFAULT NULL,
    p_metadata_snapshot JSONB DEFAULT NULL
) RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
DECLARE
    v_sql TEXT;
    v_rows_affected INTEGER;
    v_allowed_tables TEXT[] := ARRAY[
        'stac_collections',
        'stac_items',
        'auth_users',
        'customers',
        'build_manifests',
        'conversations'
    ];
BEGIN
    -- SECURITY: Validate table name against whitelist to prevent SQL injection
    IF NOT (p_table_name = ANY(v_allowed_tables)) THEN
        RAISE EXCEPTION 'Table name not allowed for soft delete: %', p_table_name
            USING HINT = 'Allowed tables: ' || array_to_string(v_allowed_tables, ', ');
    END IF;

    -- Construct dynamic SQL for soft delete using %I for identifier quoting
    v_sql := format(
        'UPDATE %I SET is_deleted = TRUE, deleted_at = NOW(), deleted_by = $1 WHERE id = $2 AND is_deleted = FALSE',
        p_table_name
    );

    -- Execute soft delete
    EXECUTE v_sql USING p_deleted_by, p_entity_id;
    GET DIAGNOSTICS v_rows_affected = ROW_COUNT;

    -- If entity was soft-deleted, create audit record
    IF v_rows_affected > 0 THEN
        INSERT INTO deletion_audit_log (
            entity_type,
            entity_id,
            deletion_type,
            deleted_by,
            reason,
            entity_metadata_snapshot
        ) VALUES (
            p_entity_type,
            p_entity_id,
            'soft',
            p_deleted_by,
            p_reason,
            p_metadata_snapshot
        );
        RETURN TRUE;
    END IF;

    RETURN FALSE;
END;
$$;

COMMENT ON FUNCTION soft_delete_entity IS 'Generic soft delete function with automatic audit logging';

-- ============================================================================
-- Helper Function: Restore Soft-Deleted Entity
-- ============================================================================
-- SECURITY: Whitelist validation prevents SQL injection

CREATE OR REPLACE FUNCTION restore_soft_deleted_entity(
    p_table_name TEXT,
    p_entity_id TEXT,
    p_entity_type TEXT,
    p_restored_by TEXT DEFAULT NULL,
    p_reason TEXT DEFAULT NULL
) RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
DECLARE
    v_sql TEXT;
    v_rows_affected INTEGER;
    v_allowed_tables TEXT[] := ARRAY[
        'stac_collections',
        'stac_items',
        'auth_users',
        'customers',
        'build_manifests',
        'conversations'
    ];
BEGIN
    -- SECURITY: Validate table name against whitelist to prevent SQL injection
    IF NOT (p_table_name = ANY(v_allowed_tables)) THEN
        RAISE EXCEPTION 'Table name not allowed for restore: %', p_table_name
            USING HINT = 'Allowed tables: ' || array_to_string(v_allowed_tables, ', ');
    END IF;

    -- Construct dynamic SQL for restoration using %I for identifier quoting
    v_sql := format(
        'UPDATE %I SET is_deleted = FALSE, deleted_at = NULL, deleted_by = NULL WHERE id = $1 AND is_deleted = TRUE',
        p_table_name
    );

    -- Execute restoration
    EXECUTE v_sql USING p_entity_id;
    GET DIAGNOSTICS v_rows_affected = ROW_COUNT;

    -- If entity was restored, create audit record
    IF v_rows_affected > 0 THEN
        INSERT INTO deletion_audit_log (
            entity_type,
            entity_id,
            deletion_type,
            deleted_by,
            reason
        ) VALUES (
            p_entity_type,
            p_entity_id,
            'restore',
            p_restored_by,
            p_reason
        );
        RETURN TRUE;
    END IF;

    RETURN FALSE;
END;
$$;

COMMENT ON FUNCTION restore_soft_deleted_entity IS 'Restores a soft-deleted entity with automatic audit logging';

-- ============================================================================
-- Helper Function: Hard Delete Entity (Admin Only)
-- ============================================================================
-- Permanently removes soft-deleted entities with audit trail
-- SECURITY: Whitelist validation prevents SQL injection

CREATE OR REPLACE FUNCTION hard_delete_entity(
    p_table_name TEXT,
    p_entity_id TEXT,
    p_entity_type TEXT,
    p_deleted_by TEXT DEFAULT NULL,
    p_reason TEXT DEFAULT NULL,
    p_metadata_snapshot JSONB DEFAULT NULL
) RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
DECLARE
    v_sql TEXT;
    v_rows_affected INTEGER;
    v_allowed_tables TEXT[] := ARRAY[
        'stac_collections',
        'stac_items',
        'auth_users',
        'customers',
        'build_manifests',
        'conversations'
    ];
BEGIN
    -- SECURITY: Validate table name against whitelist to prevent SQL injection
    IF NOT (p_table_name = ANY(v_allowed_tables)) THEN
        RAISE EXCEPTION 'Table name not allowed for hard delete: %', p_table_name
            USING HINT = 'Allowed tables: ' || array_to_string(v_allowed_tables, ', ');
    END IF;

    -- First create audit record before permanent deletion
    INSERT INTO deletion_audit_log (
        entity_type,
        entity_id,
        deletion_type,
        deleted_by,
        reason,
        entity_metadata_snapshot
    ) VALUES (
        p_entity_type,
        p_entity_id,
        'hard',
        p_deleted_by,
        p_reason,
        p_metadata_snapshot
    );

    -- Construct dynamic SQL for hard delete using %I for identifier quoting
    v_sql := format('DELETE FROM %I WHERE id = $1', p_table_name);

    -- Execute hard delete
    EXECUTE v_sql USING p_entity_id;
    GET DIAGNOSTICS v_rows_affected = ROW_COUNT;

    RETURN v_rows_affected > 0;
END;
$$;

COMMENT ON FUNCTION hard_delete_entity IS 'Permanently deletes entity with audit trail (admin only)';

-- ============================================================================
-- Migration Complete
-- ============================================================================

-- Record migration in schema_migrations table if it exists
INSERT INTO schema_migrations (version, name, applied_at, checksum, execution_time_ms)
VALUES ('009', 'SoftDelete', NOW(), 'migration_009_soft_delete', 0)
ON CONFLICT (version) DO NOTHING;
