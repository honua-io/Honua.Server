-- PostgreSQL Performance Indexes Migration
-- Purpose: Add missing indexes to improve query performance
-- Target Tables: Feature tables, STAC tables, Auth tables
-- Performance Impact: Expected 50-80% improvement on spatial and filtered queries

-- ========================================
-- STAC Performance Indexes
-- ========================================

-- Index on collection foreign key for faster item lookups
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_items_collection_id
    ON stac_items(collection_id);

-- Composite index for temporal queries (most common filter)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_items_temporal
    ON stac_items(collection_id, datetime DESC, start_datetime, end_datetime)
    WHERE datetime IS NOT NULL OR start_datetime IS NOT NULL;

-- Spatial index on geometry for bbox queries
-- Note: Uses expression index with ST_GeomFromGeoJSON for JSON storage
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_items_geometry_gist
    ON stac_items USING GIST (ST_GeomFromGeoJSON(geometry_json))
    WHERE geometry_json IS NOT NULL;

-- Raster dataset lookups (for COG/Zarr queries)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_items_raster_dataset
    ON stac_items(raster_dataset_id)
    WHERE raster_dataset_id IS NOT NULL;

-- Service/Layer composite index for collection filtering
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_collections_service_layer
    ON stac_collections(service_id, layer_id)
    WHERE service_id IS NOT NULL AND layer_id IS NOT NULL;

-- Data source lookups for metadata queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_collections_data_source
    ON stac_collections(data_source_id)
    WHERE data_source_id IS NOT NULL;

-- Updated_at index for cache invalidation queries
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_collections_updated
    ON stac_collections(updated_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_stac_items_updated
    ON stac_items(collection_id, updated_at DESC);

-- ========================================
-- Authentication & RBAC Indexes
-- ========================================

-- User lookup by subject (OIDC/OAuth)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_users_subject
    ON auth.users(subject)
    WHERE subject IS NOT NULL;

-- User lookup by email (local auth)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_users_email
    ON auth.users(email)
    WHERE email IS NOT NULL;

-- User lookup by username (local auth)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_users_username
    ON auth.users(username)
    WHERE username IS NOT NULL;

-- Active users filter (most queries filter on is_active)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_users_active
    ON auth.users(is_active, id);

-- Role membership lookups
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_user_roles_user
    ON auth.user_roles(user_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_user_roles_role
    ON auth.user_roles(role_id);

-- Audit trail queries (by user and by date)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_credentials_audit_user
    ON auth.credentials_audit(user_id, occurred_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_auth_credentials_audit_time
    ON auth.credentials_audit(occurred_at DESC);

-- ========================================
-- Feature Table Pattern Indexes
-- ========================================
-- Note: These are template indexes to be created for each feature table
-- The LayerIndexCreator class handles dynamic creation
-- This documents the pattern for reference

-- Example for a feature table named "my_features":
--
-- -- Primary spatial index (GIST for geometry operations)
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_geom_gist
--     ON my_features USING GIST (geom);
--
-- -- Bounding box index (optimized for ST_Intersects with &&)
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_geom_bbox
--     ON my_features USING GIST (ST_Envelope(geom));
--
-- -- Composite indexes for common WHERE clauses
-- -- (service_id, layer_id) for multi-tenant queries
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_service_layer
--     ON my_features(service_id, layer_id);
--
-- -- Temporal field indexes
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_created_at
--     ON my_features(created_at DESC);
--
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_updated_at
--     ON my_features(updated_at DESC);
--
-- -- Status/category fields (typical filtered columns)
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_status
--     ON my_features(status)
--     WHERE status IS NOT NULL;
--
-- -- Composite spatial + temporal index (for time-series geospatial queries)
-- CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_my_features_geom_time
--     ON my_features USING GIST (geom, created_at);

-- ========================================
-- Query Optimization Settings
-- ========================================

-- Analyze tables to update statistics after index creation
ANALYZE stac_collections;
ANALYZE stac_items;
ANALYZE auth.users;
ANALYZE auth.user_roles;
ANALYZE auth.credentials_audit;

-- Increase statistics target for frequently filtered columns
ALTER TABLE stac_items ALTER COLUMN collection_id SET STATISTICS 1000;
ALTER TABLE stac_items ALTER COLUMN datetime SET STATISTICS 1000;
ALTER TABLE auth.users ALTER COLUMN subject SET STATISTICS 500;
ALTER TABLE auth.users ALTER COLUMN email SET STATISTICS 500;

-- ========================================
-- Performance Monitoring Queries
-- ========================================

-- Check index usage statistics:
-- SELECT
--     schemaname,
--     tablename,
--     indexname,
--     idx_scan,
--     idx_tup_read,
--     idx_tup_fetch
-- FROM pg_stat_user_indexes
-- WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
-- ORDER BY idx_scan DESC;

-- Find missing indexes (queries doing sequential scans):
-- SELECT
--     schemaname,
--     tablename,
--     seq_scan,
--     seq_tup_read,
--     idx_scan,
--     seq_tup_read / NULLIF(seq_scan, 0) as avg_seq_tup_read
-- FROM pg_stat_user_tables
-- WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
--   AND seq_scan > 0
-- ORDER BY seq_tup_read DESC
-- LIMIT 20;

-- Index bloat check:
-- SELECT
--     schemaname || '.' || tablename AS table,
--     indexname AS index,
--     pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
--     idx_scan AS index_scans
-- FROM pg_stat_user_indexes
-- ORDER BY pg_relation_size(indexrelid) DESC;
