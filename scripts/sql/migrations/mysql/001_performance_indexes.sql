-- MySQL Performance Indexes Migration
-- Purpose: Add missing indexes to improve query performance
-- Target Tables: Feature tables, STAC tables, Auth tables
-- Performance Impact: Expected 50-80% improvement on spatial and filtered queries

-- ========================================
-- STAC Performance Indexes
-- ========================================

-- Index on collection foreign key for faster item lookups
CREATE INDEX IF NOT EXISTS idx_stac_items_collection_id
    ON stac_items(collection_id);

-- Composite index for temporal queries
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal
    ON stac_items(collection_id, datetime DESC, start_datetime, end_datetime);

-- Spatial index on geometry (requires SPATIAL index type)
-- Note: Spatial indexes in MySQL require the column to be NOT NULL
-- If geometry_json is TEXT/JSON, you'll need a generated column:
-- ALTER TABLE stac_items ADD COLUMN geometry_point POINT GENERATED ALWAYS AS (ST_GeomFromGeoJSON(geometry_json)) STORED;

-- Attempt to create spatial index if geometry column exists and is spatial type
SET @geometry_exists = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'stac_items'
      AND column_name = 'geometry_json'
      AND data_type IN ('geometry', 'point', 'linestring', 'polygon', 'multipoint', 'multilinestring', 'multipolygon')
);

SET @create_spatial_idx = IF(@geometry_exists > 0,
    'CREATE SPATIAL INDEX sidx_stac_items_geometry ON stac_items(geometry_json)',
    'SELECT "Skipping spatial index - geometry_json is not a spatial type" AS Warning'
);

PREPARE stmt FROM @create_spatial_idx;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Raster dataset lookups
CREATE INDEX IF NOT EXISTS idx_stac_items_raster_dataset
    ON stac_items(raster_dataset_id)
    WHERE raster_dataset_id IS NOT NULL;

-- Service/Layer composite index
CREATE INDEX IF NOT EXISTS idx_stac_collections_service_layer
    ON stac_collections(service_id, layer_id);

-- Data source lookups
CREATE INDEX IF NOT EXISTS idx_stac_collections_data_source
    ON stac_collections(data_source_id);

-- Updated_at indexes for cache invalidation
CREATE INDEX IF NOT EXISTS idx_stac_collections_updated
    ON stac_collections(updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_stac_items_updated
    ON stac_items(collection_id, updated_at DESC);

-- ========================================
-- Authentication & RBAC Indexes
-- ========================================

-- Note: MySQL 8.0+ supports functional indexes for JSON columns
-- Earlier versions may need generated columns

-- User lookup by subject
CREATE INDEX IF NOT EXISTS idx_auth_users_subject
    ON auth.users(subject);

-- User lookup by email
CREATE INDEX IF NOT EXISTS idx_auth_users_email
    ON auth.users(email);

-- User lookup by username
CREATE INDEX IF NOT EXISTS idx_auth_users_username
    ON auth.users(username);

-- Active users composite index
CREATE INDEX IF NOT EXISTS idx_auth_users_active
    ON auth.users(is_active, id);

-- Role membership lookups
CREATE INDEX IF NOT EXISTS idx_auth_user_roles_user
    ON auth.user_roles(user_id);

CREATE INDEX IF NOT EXISTS idx_auth_user_roles_role
    ON auth.user_roles(role_id);

-- Audit trail indexes
CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_user
    ON auth.credentials_audit(user_id, occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_auth_credentials_audit_time
    ON auth.credentials_audit(occurred_at DESC);

-- ========================================
-- Feature Table Pattern Indexes
-- ========================================
-- Example for a feature table named "my_features":
--
-- -- Primary spatial index (SPATIAL for geometry operations)
-- CREATE SPATIAL INDEX sidx_my_features_geom
--     ON my_features(geom);
--
-- -- Composite indexes for common WHERE clauses
-- CREATE INDEX idx_my_features_service_layer
--     ON my_features(service_id, layer_id);
--
-- -- Temporal field indexes
-- CREATE INDEX idx_my_features_created_at
--     ON my_features(created_at DESC);
--
-- CREATE INDEX idx_my_features_updated_at
--     ON my_features(updated_at DESC);
--
-- -- Status/category field indexes
-- CREATE INDEX idx_my_features_status
--     ON my_features(status);

-- ========================================
-- Table Optimization
-- ========================================

-- Analyze tables to update statistics after index creation
ANALYZE TABLE stac_collections;
ANALYZE TABLE stac_items;
ANALYZE TABLE auth.users;
ANALYZE TABLE auth.user_roles;
ANALYZE TABLE auth.credentials_audit;

-- Optimize tables to rebuild indexes and reclaim space
OPTIMIZE TABLE stac_collections;
OPTIMIZE TABLE stac_items;
OPTIMIZE TABLE auth.users;
OPTIMIZE TABLE auth.user_roles;
OPTIMIZE TABLE auth.credentials_audit;

-- ========================================
-- Performance Configuration
-- ========================================

-- Recommended MySQL configuration settings for geospatial workloads:
-- Add to my.cnf or my.ini:
--
-- [mysqld]
-- # Buffer pool size (set to 70-80% of available RAM)
-- innodb_buffer_pool_size = 4G
--
-- # Query cache (disabled in MySQL 8.0+, use result set caching in app)
-- # query_cache_type = 0
--
-- # Join buffer for spatial queries
-- join_buffer_size = 256M
--
-- # Sort buffer for ORDER BY queries
-- sort_buffer_size = 64M
--
-- # Read buffer for sequential scans
-- read_buffer_size = 16M
--
-- # Connection pool
-- max_connections = 200
--
-- # Table open cache
-- table_open_cache = 4000
--
-- # Temp table size for in-memory operations
-- tmp_table_size = 256M
-- max_heap_table_size = 256M

-- ========================================
-- Performance Monitoring Queries
-- ========================================

-- Check index usage:
/*
SELECT
    object_schema AS database_name,
    object_name AS table_name,
    index_name,
    count_read,
    count_fetch
FROM performance_schema.table_io_waits_summary_by_index_usage
WHERE object_schema NOT IN ('mysql', 'information_schema', 'performance_schema')
ORDER BY count_read + count_fetch DESC
LIMIT 50;
*/

-- Find tables without primary keys or indexes:
/*
SELECT
    t.table_schema,
    t.table_name,
    t.table_rows,
    ROUND((t.data_length + t.index_length) / 1024 / 1024, 2) AS size_mb
FROM information_schema.tables t
LEFT JOIN information_schema.statistics s
    ON t.table_schema = s.table_schema
    AND t.table_name = s.table_name
WHERE t.table_schema NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')
    AND t.table_type = 'BASE TABLE'
    AND s.index_name IS NULL
ORDER BY t.table_rows DESC;
*/

-- Check for duplicate indexes:
/*
SELECT
    table_schema,
    table_name,
    GROUP_CONCAT(index_name ORDER BY index_name) AS duplicate_indexes
FROM information_schema.statistics
WHERE table_schema NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys')
GROUP BY table_schema, table_name, column_name, seq_in_index
HAVING COUNT(*) > 1;
*/
