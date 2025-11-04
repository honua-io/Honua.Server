-- PostgreSQL Rollback Script for Migration 008
-- Purpose: Remove performance indexes added in 008_add_performance_indexes.sql
-- IMPORTANT: Use CONCURRENTLY to avoid blocking production queries
-- Note: DROP INDEX CONCURRENTLY requires PostgreSQL 9.2+

-- ========================================
-- Rollback Alert History Indexes
-- ========================================

DROP INDEX CONCURRENTLY IF EXISTS idx_alert_history_fingerprint_datetime;
DROP INDEX CONCURRENTLY IF EXISTS idx_alert_history_severity_timestamp;
DROP INDEX CONCURRENTLY IF EXISTS idx_alert_history_status_timestamp;
DROP INDEX CONCURRENTLY IF EXISTS idx_alert_history_environment;

-- ========================================
-- Rollback STAC Collections Indexes
-- ========================================

DROP INDEX CONCURRENTLY IF EXISTS idx_stac_collections_service_layer;
DROP INDEX CONCURRENTLY IF EXISTS idx_stac_collections_data_source;

-- ========================================
-- Rollback STAC Items Indexes
-- ========================================

DROP INDEX CONCURRENTLY IF EXISTS idx_stac_items_spatial_temporal_gist;
DROP INDEX CONCURRENTLY IF EXISTS idx_stac_items_bbox_datetime;
DROP INDEX CONCURRENTLY IF EXISTS idx_stac_items_collection_raster;

-- ========================================
-- Reset Statistics Targets
-- ========================================

-- Reset to default statistics target (100)
ALTER TABLE alert_history ALTER COLUMN fingerprint SET STATISTICS DEFAULT;
ALTER TABLE alert_history ALTER COLUMN severity SET STATISTICS DEFAULT;
ALTER TABLE alert_history ALTER COLUMN status SET STATISTICS DEFAULT;
ALTER TABLE stac_collections ALTER COLUMN service_id SET STATISTICS DEFAULT;
ALTER TABLE stac_collections ALTER COLUMN layer_id SET STATISTICS DEFAULT;
ALTER TABLE stac_items ALTER COLUMN collection_id SET STATISTICS DEFAULT;
ALTER TABLE stac_items ALTER COLUMN datetime SET STATISTICS DEFAULT;

-- ========================================
-- Update Statistics
-- ========================================

ANALYZE alert_history;
ANALYZE stac_collections;
ANALYZE stac_items;

-- ========================================
-- Verification
-- ========================================

-- Verify indexes have been removed:
-- SELECT
--     schemaname,
--     tablename,
--     indexname
-- FROM pg_indexes
-- WHERE indexname IN (
--     'idx_alert_history_fingerprint_datetime',
--     'idx_alert_history_severity_timestamp',
--     'idx_alert_history_status_timestamp',
--     'idx_alert_history_environment',
--     'idx_stac_collections_service_layer',
--     'idx_stac_collections_data_source',
--     'idx_stac_items_spatial_temporal_gist',
--     'idx_stac_items_bbox_datetime',
--     'idx_stac_items_collection_raster'
-- );
-- Expected: No rows returned
