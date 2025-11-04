-- ============================================================================
-- Data Versioning and Temporal Tables (Enterprise Feature)
-- ============================================================================
-- Version: 008
-- Description: Git-like versioning for data with temporal tables, change tracking,
--              conflict detection, and merge capabilities
-- Dependencies: 001-007
-- ============================================================================

-- ============================================================================
-- Versioned Collections (Example - applies to all versionable entities)
-- ============================================================================

-- Create versioned collections table
CREATE TABLE IF NOT EXISTS collections_versioned (
    -- Entity identification (stable across versions)
    id UUID NOT NULL,

    -- Version tracking
    version BIGINT NOT NULL,
    content_hash VARCHAR(64) NOT NULL,

    -- Temporal tracking
    version_created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version_created_by VARCHAR(255),
    version_valid_from TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version_valid_to TIMESTAMPTZ,

    -- Branching and merging
    parent_version BIGINT,
    branch VARCHAR(100) DEFAULT 'main',
    commit_message TEXT,

    -- Soft delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by VARCHAR(255),

    -- Original entity data (all columns from collections table)
    name VARCHAR(255) NOT NULL,
    description TEXT,
    license VARCHAR(255),
    spatial_extent GEOMETRY(Geometry, 4326),
    temporal_extent TSTZRANGE,
    properties JSONB DEFAULT '{}',
    metadata JSONB DEFAULT '{}',

    -- Tenant isolation
    tenant_id VARCHAR(100),

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Primary key is combination of id and version
    PRIMARY KEY (id, version),

    -- Foreign key to original table (optional - for current version sync)
    CONSTRAINT fk_collections_original FOREIGN KEY (tenant_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE
);

-- Create indexes for efficient querying
CREATE INDEX idx_collections_versioned_id ON collections_versioned(id);
CREATE INDEX idx_collections_versioned_version ON collections_versioned(id, version DESC);
CREATE INDEX idx_collections_versioned_timestamp ON collections_versioned(id, version_created_at DESC);
CREATE INDEX idx_collections_versioned_valid_range ON collections_versioned(id, version_valid_from, version_valid_to);
CREATE INDEX idx_collections_versioned_branch ON collections_versioned(id, branch);
CREATE INDEX idx_collections_versioned_parent ON collections_versioned(id, parent_version);
CREATE INDEX idx_collections_versioned_tenant ON collections_versioned(tenant_id);
CREATE INDEX idx_collections_versioned_current ON collections_versioned(id, version) WHERE version_valid_to IS NULL AND is_deleted = FALSE;

COMMENT ON TABLE collections_versioned IS 'Temporal version history for collections with git-like versioning';
COMMENT ON COLUMN collections_versioned.id IS 'Stable entity ID across all versions';
COMMENT ON COLUMN collections_versioned.version IS 'Monotonically increasing version number';
COMMENT ON COLUMN collections_versioned.content_hash IS 'SHA256 hash of entity content for change detection';
COMMENT ON COLUMN collections_versioned.version_valid_from IS 'When this version became active';
COMMENT ON COLUMN collections_versioned.version_valid_to IS 'When this version was superseded (NULL = current)';
COMMENT ON COLUMN collections_versioned.parent_version IS 'Parent version for tracking lineage and branching';
COMMENT ON COLUMN collections_versioned.branch IS 'Branch name (main by default, supports feature branches)';

-- ============================================================================
-- Change Tracking Table
-- ============================================================================

CREATE TABLE IF NOT EXISTS version_changes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID NOT NULL,
    from_version BIGINT NOT NULL,
    to_version BIGINT NOT NULL,

    -- Change details
    field_name VARCHAR(255) NOT NULL,
    field_path VARCHAR(500), -- JSON path for nested fields
    old_value JSONB,
    new_value JSONB,
    change_type VARCHAR(20) NOT NULL, -- 'added', 'removed', 'modified'

    -- Metadata
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    changed_by VARCHAR(255),

    -- Index for efficient lookups
    CONSTRAINT check_change_type CHECK (change_type IN ('added', 'removed', 'modified'))
);

CREATE INDEX idx_version_changes_entity ON version_changes(entity_type, entity_id, to_version DESC);
CREATE INDEX idx_version_changes_version_range ON version_changes(entity_id, from_version, to_version);
CREATE INDEX idx_version_changes_field ON version_changes(entity_id, field_name);
CREATE INDEX idx_version_changes_timestamp ON version_changes(changed_at DESC);

COMMENT ON TABLE version_changes IS 'Detailed field-level change tracking between versions';

-- ============================================================================
-- Merge Conflicts Table
-- ============================================================================

CREATE TABLE IF NOT EXISTS merge_conflicts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID NOT NULL,

    -- Version tracking
    base_version BIGINT NOT NULL,
    current_version BIGINT NOT NULL,
    incoming_version BIGINT NOT NULL,

    -- Conflict details
    field_name VARCHAR(255) NOT NULL,
    field_path VARCHAR(500),
    base_value JSONB,
    current_value JSONB,
    incoming_value JSONB,
    conflict_type VARCHAR(50) NOT NULL, -- 'both_modified', 'modified_and_deleted', etc.

    -- Resolution
    is_resolved BOOLEAN NOT NULL DEFAULT FALSE,
    resolved_value JSONB,
    resolution_strategy VARCHAR(50), -- 'use_ours', 'use_theirs', 'custom', etc.
    resolved_at TIMESTAMPTZ,
    resolved_by VARCHAR(255),

    -- Metadata
    detected_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT check_conflict_type CHECK (conflict_type IN (
        'both_modified', 'modified_and_deleted', 'deleted_and_modified',
        'both_added', 'type_mismatch'
    )),
    CONSTRAINT check_resolution_strategy CHECK (resolution_strategy IN (
        'use_ours', 'use_theirs', 'use_base', 'custom', 'keep_both', 'manual'
    ) OR resolution_strategy IS NULL)
);

CREATE INDEX idx_merge_conflicts_entity ON merge_conflicts(entity_type, entity_id);
CREATE INDEX idx_merge_conflicts_unresolved ON merge_conflicts(entity_id, is_resolved) WHERE is_resolved = FALSE;
CREATE INDEX idx_merge_conflicts_versions ON merge_conflicts(entity_id, base_version, current_version, incoming_version);

COMMENT ON TABLE merge_conflicts IS 'Detected merge conflicts requiring resolution';

-- ============================================================================
-- Merge Operations Audit Log
-- ============================================================================

CREATE TABLE IF NOT EXISTS merge_operations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type VARCHAR(100) NOT NULL,
    entity_id UUID NOT NULL,

    -- Versions involved
    base_version BIGINT NOT NULL,
    current_version BIGINT NOT NULL,
    incoming_version BIGINT NOT NULL,
    result_version BIGINT,

    -- Merge details
    merge_strategy VARCHAR(50) NOT NULL,
    auto_merged_fields INT NOT NULL DEFAULT 0,
    conflicted_fields INT NOT NULL DEFAULT 0,
    manually_resolved_fields INT NOT NULL DEFAULT 0,

    -- Status
    status VARCHAR(50) NOT NULL, -- 'success', 'failed', 'conflicts_remaining'
    error_message TEXT,

    -- Audit
    merged_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    merged_by VARCHAR(255) NOT NULL,
    commit_message TEXT,

    CONSTRAINT check_merge_status CHECK (status IN ('success', 'failed', 'conflicts_remaining'))
);

CREATE INDEX idx_merge_operations_entity ON merge_operations(entity_type, entity_id, merged_at DESC);
CREATE INDEX idx_merge_operations_status ON merge_operations(status, merged_at DESC);

COMMENT ON TABLE merge_operations IS 'Audit log of all merge operations';

-- ============================================================================
-- Functions for Version Management
-- ============================================================================

-- Function to get current version of an entity
CREATE OR REPLACE FUNCTION get_current_version(
    p_entity_type VARCHAR(100),
    p_entity_id UUID
) RETURNS BIGINT AS $$
BEGIN
    RETURN (
        SELECT MAX(version)
        FROM collections_versioned
        WHERE id = p_entity_id
          AND is_deleted = FALSE
          AND version_valid_to IS NULL
    );
END;
$$ LANGUAGE plpgsql;

-- Function to get entity at specific timestamp
CREATE OR REPLACE FUNCTION get_version_at_timestamp(
    p_entity_id UUID,
    p_timestamp TIMESTAMPTZ
) RETURNS BIGINT AS $$
BEGIN
    RETURN (
        SELECT version
        FROM collections_versioned
        WHERE id = p_entity_id
          AND version_valid_from <= p_timestamp
          AND (version_valid_to IS NULL OR version_valid_to > p_timestamp)
        ORDER BY version DESC
        LIMIT 1
    );
END;
$$ LANGUAGE plpgsql;

-- Function to create a new version
CREATE OR REPLACE FUNCTION create_new_version(
    p_entity_id UUID,
    p_created_by VARCHAR(255),
    p_commit_message TEXT,
    p_branch VARCHAR(100) DEFAULT 'main'
) RETURNS BIGINT AS $$
DECLARE
    v_new_version BIGINT;
    v_current_version BIGINT;
BEGIN
    -- Get current max version
    SELECT COALESCE(MAX(version), 0) + 1
    INTO v_new_version
    FROM collections_versioned
    WHERE id = p_entity_id;

    -- Get current active version
    SELECT MAX(version)
    INTO v_current_version
    FROM collections_versioned
    WHERE id = p_entity_id
      AND version_valid_to IS NULL;

    -- Mark current version as superseded
    IF v_current_version IS NOT NULL THEN
        UPDATE collections_versioned
        SET version_valid_to = NOW()
        WHERE id = p_entity_id
          AND version = v_current_version;
    END IF;

    RETURN v_new_version;
END;
$$ LANGUAGE plpgsql;

-- Function to calculate content hash
CREATE OR REPLACE FUNCTION calculate_content_hash(
    p_content JSONB
) RETURNS VARCHAR(64) AS $$
BEGIN
    RETURN encode(digest(p_content::TEXT, 'sha256'), 'hex');
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- Triggers for Automatic Change Tracking
-- ============================================================================

CREATE OR REPLACE FUNCTION track_version_changes()
RETURNS TRIGGER AS $$
DECLARE
    v_old_json JSONB;
    v_new_json JSONB;
    v_key TEXT;
    v_old_value JSONB;
    v_new_value JSONB;
BEGIN
    -- Convert old and new records to JSONB for comparison
    v_old_json := to_jsonb(OLD);
    v_new_json := to_jsonb(NEW);

    -- Compare each field
    FOR v_key IN SELECT jsonb_object_keys(v_new_json) LOOP
        -- Skip version tracking fields
        IF v_key IN ('version', 'version_created_at', 'version_valid_from', 'version_valid_to', 'updated_at') THEN
            CONTINUE;
        END IF;

        v_old_value := v_old_json->v_key;
        v_new_value := v_new_json->v_key;

        -- Record change if values differ
        IF v_old_value IS DISTINCT FROM v_new_value THEN
            INSERT INTO version_changes (
                entity_type,
                entity_id,
                from_version,
                to_version,
                field_name,
                old_value,
                new_value,
                change_type,
                changed_by
            ) VALUES (
                'collection',
                NEW.id,
                OLD.version,
                NEW.version,
                v_key,
                v_old_value,
                v_new_value,
                CASE
                    WHEN v_old_value IS NULL THEN 'added'
                    WHEN v_new_value IS NULL THEN 'removed'
                    ELSE 'modified'
                END,
                NEW.version_created_by
            );
        END IF;
    END LOOP;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger
CREATE TRIGGER trigger_track_changes
    AFTER INSERT OR UPDATE ON collections_versioned
    FOR EACH ROW
    WHEN (NEW.version > COALESCE(OLD.version, 0))
    EXECUTE FUNCTION track_version_changes();

-- ============================================================================
-- Views for Convenient Querying
-- ============================================================================

-- View for current versions only
CREATE OR REPLACE VIEW collections_current AS
SELECT *
FROM collections_versioned
WHERE version_valid_to IS NULL
  AND is_deleted = FALSE;

COMMENT ON VIEW collections_current IS 'Current (latest) versions of all collections';

-- View for complete version history with change counts
CREATE OR REPLACE VIEW version_history_summary AS
SELECT
    cv.id,
    cv.version,
    cv.branch,
    cv.version_created_at,
    cv.version_created_by,
    cv.commit_message,
    cv.parent_version,
    COUNT(vc.id) as changes_count,
    ARRAY_AGG(DISTINCT vc.field_name) FILTER (WHERE vc.field_name IS NOT NULL) as changed_fields
FROM collections_versioned cv
LEFT JOIN version_changes vc ON cv.id = vc.entity_id AND cv.version = vc.to_version
GROUP BY cv.id, cv.version, cv.branch, cv.version_created_at,
         cv.version_created_by, cv.commit_message, cv.parent_version
ORDER BY cv.id, cv.version DESC;

COMMENT ON VIEW version_history_summary IS 'Summary of all versions with change counts';

-- Record migration
INSERT INTO schema_migrations (version, name, checksum, execution_time_ms)
VALUES ('008', 'DataVersioning', 'PLACEHOLDER_CHECKSUM', 0)
ON CONFLICT (version) DO NOTHING;
