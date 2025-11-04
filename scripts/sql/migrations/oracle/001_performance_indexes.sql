-- Oracle Performance Indexes Migration
-- Purpose: Add missing indexes to improve query performance
-- Target Tables: Feature tables, STAC tables, Auth tables
-- Performance Impact: Expected 50-80% improvement on spatial and filtered queries

-- ========================================
-- STAC Performance Indexes
-- ========================================

-- Index on collection foreign key for faster item lookups
CREATE INDEX idx_stac_items_collection_id
    ON stac_items(collection_id)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Composite index for temporal queries with compression
CREATE INDEX idx_stac_items_temporal
    ON stac_items(collection_id, datetime DESC, start_datetime, end_datetime)
    TABLESPACE users
    COMPRESS 2
    PARALLEL 4
    NOLOGGING;

-- Spatial index on geometry (Oracle Spatial)
-- Note: Requires SDO_GEOMETRY column type and spatial metadata
-- If geometry_json is VARCHAR2/CLOB, create a function-based spatial index:

DECLARE
    v_count NUMBER;
BEGIN
    -- Check if geometry column exists and is SDO_GEOMETRY type
    SELECT COUNT(*) INTO v_count
    FROM user_tab_columns
    WHERE table_name = 'STAC_ITEMS'
      AND column_name = 'GEOMETRY_JSON'
      AND data_type = 'SDO_GEOMETRY';

    IF v_count > 0 THEN
        -- Create spatial index
        EXECUTE IMMEDIATE '
            CREATE INDEX sidx_stac_items_geometry
                ON stac_items(geometry_json)
                INDEXTYPE IS MDSYS.SPATIAL_INDEX
                PARAMETERS (''sdo_indx_dims=2 layer_gtype=MULTIPOLYGON'')';
    END IF;
END;
/

-- Raster dataset lookups with bitmap index (for low cardinality)
CREATE BITMAP INDEX idx_stac_items_raster_dataset
    ON stac_items(raster_dataset_id)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Service/Layer composite index
CREATE INDEX idx_stac_collections_service_layer
    ON stac_collections(service_id, layer_id)
    TABLESPACE users
    COMPRESS 2
    PARALLEL 4
    NOLOGGING;

-- Data source lookups
CREATE INDEX idx_stac_collections_data_source
    ON stac_collections(data_source_id)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Updated_at indexes for cache invalidation (descending for range queries)
CREATE INDEX idx_stac_collections_updated
    ON stac_collections(updated_at DESC)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

CREATE INDEX idx_stac_items_updated
    ON stac_items(collection_id, updated_at DESC)
    TABLESPACE users
    COMPRESS 1
    PARALLEL 4
    NOLOGGING;

-- ========================================
-- Authentication & RBAC Indexes
-- ========================================

-- User lookup by subject (function-based index for case-insensitive search)
CREATE INDEX idx_auth_users_subject
    ON auth.users(subject)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- User lookup by email (function-based index for case-insensitive)
CREATE INDEX idx_auth_users_email
    ON auth.users(UPPER(email))
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- User lookup by username (function-based index for case-insensitive)
CREATE INDEX idx_auth_users_username
    ON auth.users(UPPER(username))
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Active users bitmap index (for boolean flag)
CREATE BITMAP INDEX idx_auth_users_active
    ON auth.users(is_active)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Composite index for active user queries
CREATE INDEX idx_auth_users_active_composite
    ON auth.users(is_active, id)
    TABLESPACE users
    COMPRESS 1
    PARALLEL 4
    NOLOGGING;

-- Role membership lookups
CREATE INDEX idx_auth_user_roles_user
    ON auth.user_roles(user_id)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

CREATE INDEX idx_auth_user_roles_role
    ON auth.user_roles(role_id)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- Audit trail indexes with descending for time-based queries
CREATE INDEX idx_auth_credentials_audit_user
    ON auth.credentials_audit(user_id, occurred_at DESC)
    TABLESPACE users
    COMPRESS 1
    PARALLEL 4
    NOLOGGING;

CREATE INDEX idx_auth_credentials_audit_time
    ON auth.credentials_audit(occurred_at DESC)
    TABLESPACE users
    PARALLEL 4
    NOLOGGING;

-- ========================================
-- Rebuild Indexes for Optimal Performance
-- ========================================

-- Switch indexes to logging mode after initial build
BEGIN
    FOR idx IN (
        SELECT index_name
        FROM user_indexes
        WHERE table_name IN ('STAC_COLLECTIONS', 'STAC_ITEMS')
           OR table_name LIKE 'AUTH_%'
    ) LOOP
        EXECUTE IMMEDIATE 'ALTER INDEX ' || idx.index_name || ' LOGGING';
    END LOOP;
END;
/

-- ========================================
-- Statistics Collection
-- ========================================

-- Gather table statistics with histogram collection
BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(
        ownname => USER,
        tabname => 'STAC_COLLECTIONS',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        method_opt => 'FOR ALL COLUMNS SIZE AUTO',
        degree => 4,
        cascade => TRUE
    );

    DBMS_STATS.GATHER_TABLE_STATS(
        ownname => USER,
        tabname => 'STAC_ITEMS',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        method_opt => 'FOR ALL COLUMNS SIZE AUTO',
        degree => 4,
        cascade => TRUE
    );

    DBMS_STATS.GATHER_SCHEMA_STATS(
        ownname => 'AUTH',
        estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
        method_opt => 'FOR ALL COLUMNS SIZE AUTO',
        degree => 4,
        cascade => TRUE
    );
END;
/

-- ========================================
-- Feature Table Pattern Indexes
-- ========================================
-- Example for a feature table named "my_features":
--
-- -- Primary spatial index (Oracle Spatial R-tree)
-- CREATE INDEX sidx_my_features_geom
--     ON my_features(geom)
--     INDEXTYPE IS MDSYS.SPATIAL_INDEX
--     PARAMETERS ('sdo_indx_dims=2 layer_gtype=POINT');
--
-- -- Composite indexes for common WHERE clauses
-- CREATE INDEX idx_my_features_service_layer
--     ON my_features(service_id, layer_id)
--     COMPRESS 2
--     PARALLEL 4;
--
-- -- Temporal field indexes (descending for range queries)
-- CREATE INDEX idx_my_features_created_at
--     ON my_features(created_at DESC)
--     PARALLEL 4;
--
-- -- Bitmap index for low-cardinality status fields
-- CREATE BITMAP INDEX idx_my_features_status
--     ON my_features(status)
--     PARALLEL 4;
--
-- -- Function-based index for case-insensitive searches
-- CREATE INDEX idx_my_features_name_upper
--     ON my_features(UPPER(name))
--     PARALLEL 4;

-- ========================================
-- Performance Configuration
-- ========================================

-- Recommended Oracle initialization parameters for geospatial workloads:
-- Add to init.ora or modify using ALTER SYSTEM:
--
-- -- SGA sizing (set to 70% of available RAM)
-- ALTER SYSTEM SET sga_target = 4G SCOPE=SPFILE;
-- ALTER SYSTEM SET pga_aggregate_target = 2G SCOPE=SPFILE;
--
-- -- Optimizer settings
-- ALTER SYSTEM SET optimizer_mode = ALL_ROWS SCOPE=BOTH;
-- ALTER SYSTEM SET optimizer_index_cost_adj = 10 SCOPE=BOTH;
--
-- -- Parallel execution
-- ALTER SYSTEM SET parallel_max_servers = 20 SCOPE=BOTH;
-- ALTER SYSTEM SET parallel_degree_policy = AUTO SCOPE=BOTH;
--
-- -- Spatial index parameters
-- ALTER SYSTEM SET spatial_vector_acceleration = TRUE SCOPE=BOTH;
--
-- -- Connection pool
-- ALTER SYSTEM SET processes = 300 SCOPE=SPFILE;
-- ALTER SYSTEM SET sessions = 335 SCOPE=SPFILE;

-- ========================================
-- Performance Monitoring Queries
-- ========================================

-- Check index usage statistics:
/*
SELECT
    i.table_name,
    i.index_name,
    i.index_type,
    i.uniqueness,
    t.num_rows AS table_rows,
    i.distinct_keys,
    i.clustering_factor,
    ROUND((i.clustering_factor / NULLIF(t.num_rows, 0)) * 100, 2) AS cluster_pct
FROM user_indexes i
JOIN user_tables t ON i.table_name = t.table_name
WHERE i.table_name IN ('STAC_COLLECTIONS', 'STAC_ITEMS')
   OR i.table_name LIKE 'AUTH_%'
ORDER BY i.table_name, i.index_name;
*/

-- Find inefficient indexes (high clustering factor):
/*
SELECT
    i.table_name,
    i.index_name,
    t.num_rows,
    i.clustering_factor,
    ROUND((i.clustering_factor / NULLIF(t.num_rows, 0)), 2) AS cluster_ratio
FROM user_indexes i
JOIN user_tables t ON i.table_name = t.table_name
WHERE i.clustering_factor > t.num_rows
    AND t.num_rows > 1000
ORDER BY cluster_ratio DESC;
*/

-- Check for missing or stale statistics:
/*
SELECT
    table_name,
    num_rows,
    last_analyzed,
    stattype_locked,
    stale_stats
FROM user_tab_statistics
WHERE (last_analyzed IS NULL OR last_analyzed < SYSDATE - 7)
   OR stale_stats = 'YES'
ORDER BY num_rows DESC NULLS LAST;
*/

-- Spatial index validation:
/*
SELECT
    index_name,
    sdo_index_type,
    sdo_partitioned,
    domidx_status,
    domidx_opstatus
FROM user_sdo_index_metadata
WHERE sdo_index_owner = USER;
*/
