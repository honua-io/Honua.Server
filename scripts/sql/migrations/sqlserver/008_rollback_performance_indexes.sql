-- SQL Server Rollback Script for Migration 008
-- Purpose: Remove performance indexes added in 008_add_performance_indexes.sql
-- IMPORTANT: Index drops are blocking operations in SQL Server
-- Consider running during maintenance window

SET NOCOUNT ON;
GO

-- ========================================
-- Rollback Alert History Indexes
-- ========================================

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_alert_history_fingerprint_datetime' AND object_id = OBJECT_ID('dbo.alert_history'))
BEGIN
    DROP INDEX idx_alert_history_fingerprint_datetime ON dbo.alert_history;
    PRINT 'Dropped index: idx_alert_history_fingerprint_datetime';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_alert_history_severity_timestamp' AND object_id = OBJECT_ID('dbo.alert_history'))
BEGIN
    DROP INDEX idx_alert_history_severity_timestamp ON dbo.alert_history;
    PRINT 'Dropped index: idx_alert_history_severity_timestamp';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_alert_history_status_timestamp' AND object_id = OBJECT_ID('dbo.alert_history'))
BEGIN
    DROP INDEX idx_alert_history_status_timestamp ON dbo.alert_history;
    PRINT 'Dropped index: idx_alert_history_status_timestamp';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_alert_history_environment' AND object_id = OBJECT_ID('dbo.alert_history'))
BEGIN
    DROP INDEX idx_alert_history_environment ON dbo.alert_history;
    PRINT 'Dropped index: idx_alert_history_environment';
END
GO

-- ========================================
-- Rollback STAC Collections Indexes
-- ========================================

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_collections_service_layer' AND object_id = OBJECT_ID('dbo.stac_collections'))
BEGIN
    DROP INDEX idx_stac_collections_service_layer ON dbo.stac_collections;
    PRINT 'Dropped index: idx_stac_collections_service_layer';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_collections_data_source' AND object_id = OBJECT_ID('dbo.stac_collections'))
BEGIN
    DROP INDEX idx_stac_collections_data_source ON dbo.stac_collections;
    PRINT 'Dropped index: idx_stac_collections_data_source';
END
GO

-- ========================================
-- Rollback STAC Items Indexes
-- ========================================

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_bbox_datetime' AND object_id = OBJECT_ID('dbo.stac_items'))
BEGIN
    DROP INDEX idx_stac_items_bbox_datetime ON dbo.stac_items;
    PRINT 'Dropped index: idx_stac_items_bbox_datetime';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_collection_raster' AND object_id = OBJECT_ID('dbo.stac_items'))
BEGIN
    DROP INDEX idx_stac_items_collection_raster ON dbo.stac_items;
    PRINT 'Dropped index: idx_stac_items_collection_raster';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'sidx_stac_items_geometry_spatial' AND object_id = OBJECT_ID('dbo.stac_items'))
BEGIN
    DROP INDEX sidx_stac_items_geometry_spatial ON dbo.stac_items;
    PRINT 'Dropped spatial index: sidx_stac_items_geometry_spatial';
END
GO

-- ========================================
-- Update Statistics
-- ========================================

UPDATE STATISTICS dbo.alert_history WITH FULLSCAN;
UPDATE STATISTICS dbo.stac_collections WITH FULLSCAN;
UPDATE STATISTICS dbo.stac_items WITH FULLSCAN;
GO

PRINT 'Rollback complete - all indexes removed and statistics updated';
GO

-- ========================================
-- Verification
-- ========================================

-- Verify indexes have been removed:
-- SELECT
--     OBJECT_SCHEMA_NAME(object_id) + '.' + OBJECT_NAME(object_id) AS table_name,
--     name AS index_name
-- FROM sys.indexes
-- WHERE name IN (
--     'idx_alert_history_fingerprint_datetime',
--     'idx_alert_history_severity_timestamp',
--     'idx_alert_history_status_timestamp',
--     'idx_alert_history_environment',
--     'idx_stac_collections_service_layer',
--     'idx_stac_collections_data_source',
--     'idx_stac_items_bbox_datetime',
--     'idx_stac_items_collection_raster',
--     'sidx_stac_items_geometry_spatial'
-- );
-- Expected: No rows returned
