-- Migration: Create dashboards table for no-code dashboard builder
-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

-- Create dashboards table
CREATE TABLE IF NOT EXISTS dashboards (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    owner_id VARCHAR(255) NOT NULL,
    tags TEXT[] DEFAULT '{}',
    definition JSONB NOT NULL,
    is_public BOOLEAN DEFAULT FALSE,
    is_template BOOLEAN DEFAULT FALSE,
    schema_version VARCHAR(20) DEFAULT '1.0',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    deleted_at TIMESTAMPTZ,

    CONSTRAINT chk_name_not_empty CHECK (LENGTH(TRIM(name)) > 0)
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_dashboards_owner_id ON dashboards(owner_id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_dashboards_is_public ON dashboards(is_public) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_dashboards_is_template ON dashboards(is_template) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_dashboards_tags ON dashboards USING GIN(tags) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_dashboards_definition ON dashboards USING GIN(definition) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_dashboards_updated_at ON dashboards(updated_at DESC) WHERE deleted_at IS NULL;

-- Create full-text search index
CREATE INDEX IF NOT EXISTS idx_dashboards_search ON dashboards USING GIN(
    to_tsvector('english', COALESCE(name, '') || ' ' || COALESCE(description, ''))
) WHERE deleted_at IS NULL;

-- Create trigger to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_dashboards_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_dashboards_updated_at
    BEFORE UPDATE ON dashboards
    FOR EACH ROW
    EXECUTE FUNCTION update_dashboards_updated_at();

-- Grant permissions (adjust as needed for your security model)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON dashboards TO honua_app_user;

-- Add comments for documentation
COMMENT ON TABLE dashboards IS 'Stores dashboard definitions for the no-code dashboard builder';
COMMENT ON COLUMN dashboards.id IS 'Unique dashboard identifier';
COMMENT ON COLUMN dashboards.name IS 'Dashboard display name';
COMMENT ON COLUMN dashboards.description IS 'Dashboard description';
COMMENT ON COLUMN dashboards.owner_id IS 'User ID of the dashboard owner';
COMMENT ON COLUMN dashboards.tags IS 'Tags for categorization and search';
COMMENT ON COLUMN dashboards.definition IS 'Complete dashboard definition in JSON format';
COMMENT ON COLUMN dashboards.is_public IS 'Whether the dashboard is publicly accessible';
COMMENT ON COLUMN dashboards.is_template IS 'Whether this is a template dashboard';
COMMENT ON COLUMN dashboards.schema_version IS 'Dashboard schema version for compatibility';
COMMENT ON COLUMN dashboards.deleted_at IS 'Soft delete timestamp';
