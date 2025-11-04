-- STAC Temporal Index Performance Benchmark Script
-- This script helps measure the performance improvement of temporal indexes
-- Run this on PostgreSQL with a populated STAC catalog

-- Prerequisites:
-- 1. Database must have at least 10,000 STAC items with temporal data
-- 2. Run this script twice: once before applying 002_temporal_indexes.sql, once after

-- ============================================================================
-- PART 1: Setup test data (if needed)
-- ============================================================================

-- Create a test collection if it doesn't exist
INSERT INTO stac_collections (id, title, description, license, version, keywords_json, extent_json, properties_json, links_json, extensions_json, conforms_to, data_source_id, service_id, layer_id, etag, created_at, updated_at)
VALUES (
    'benchmark-collection',
    'Benchmark Collection',
    'Collection for temporal index benchmarking',
    'MIT',
    '1.0.0',
    '[]',
    '{"spatial":{"bbox":[[-180,-90,180,90]]},"temporal":{"interval":[["2024-01-01T00:00:00Z","2024-12-31T23:59:59Z"]]}}',
    NULL,
    '[]',
    '[]',
    NULL,
    NULL,
    NULL,
    NULL,
    'test',
    NOW(),
    NOW()
)
ON CONFLICT (id) DO NOTHING;

-- Generate sample items with temporal data (adjust the count as needed)
-- This creates items with different temporal patterns:
-- - Some with datetime only
-- - Some with start_datetime and end_datetime (ranges)
-- - Some with all three fields
DO $$
DECLARE
    i INTEGER;
    base_date TIMESTAMP WITH TIME ZONE := '2024-01-01'::TIMESTAMPTZ;
BEGIN
    FOR i IN 1..10000 LOOP
        -- Vary the temporal pattern
        IF i % 3 = 0 THEN
            -- Point-in-time: datetime only
            INSERT INTO stac_items (
                collection_id, id, title, description, properties_json, assets_json,
                links_json, extensions_json, bbox_json, geometry_json,
                datetime, start_datetime, end_datetime,
                raster_dataset_id, etag, created_at, updated_at
            ) VALUES (
                'benchmark-collection',
                'item-' || i,
                'Benchmark Item ' || i,
                'Test item for benchmarking',
                NULL,
                '{}',
                '[]',
                '[]',
                '[-180,-90,180,90]',
                NULL,
                base_date + (i || ' days')::INTERVAL,
                NULL,
                NULL,
                NULL,
                'test-' || i,
                NOW(),
                NOW()
            )
            ON CONFLICT (collection_id, id) DO NOTHING;
        ELSIF i % 3 = 1 THEN
            -- Range: start_datetime and end_datetime
            INSERT INTO stac_items (
                collection_id, id, title, description, properties_json, assets_json,
                links_json, extensions_json, bbox_json, geometry_json,
                datetime, start_datetime, end_datetime,
                raster_dataset_id, etag, created_at, updated_at
            ) VALUES (
                'benchmark-collection',
                'item-' || i,
                'Benchmark Item ' || i,
                'Test item for benchmarking',
                NULL,
                '{}',
                '[]',
                '[]',
                '[-180,-90,180,90]',
                NULL,
                NULL,
                base_date + (i || ' days')::INTERVAL,
                base_date + ((i + 7) || ' days')::INTERVAL,
                NULL,
                'test-' || i,
                NOW(),
                NOW()
            )
            ON CONFLICT (collection_id, id) DO NOTHING;
        ELSE
            -- All three: datetime, start_datetime, end_datetime
            INSERT INTO stac_items (
                collection_id, id, title, description, properties_json, assets_json,
                links_json, extensions_json, bbox_json, geometry_json,
                datetime, start_datetime, end_datetime,
                raster_dataset_id, etag, created_at, updated_at
            ) VALUES (
                'benchmark-collection',
                'item-' || i,
                'Benchmark Item ' || i,
                'Test item for benchmarking',
                NULL,
                '{}',
                '[]',
                '[]',
                '[-180,-90,180,90]',
                NULL,
                base_date + (i || ' days')::INTERVAL,
                base_date + ((i - 1) || ' days')::INTERVAL,
                base_date + ((i + 1) || ' days')::INTERVAL,
                NULL,
                'test-' || i,
                NOW(),
                NOW()
            )
            ON CONFLICT (collection_id, id) DO NOTHING;
        END IF;
    END LOOP;
END $$;

-- Update statistics
ANALYZE stac_items;

\echo '============================================================================'
\echo 'Test data created/verified. Proceeding with benchmarks...'
\echo '============================================================================'
\echo ''

-- ============================================================================
-- PART 2: Warm up the cache
-- ============================================================================
\echo 'Warming up cache...'
SELECT COUNT(*) FROM stac_items WHERE collection_id = 'benchmark-collection';
\echo ''

-- ============================================================================
-- PART 3: Benchmark queries
-- ============================================================================

\echo '============================================================================'
\echo 'BENCHMARK 1: Temporal range query (most common use case)'
\echo 'Find items with temporal overlap in 2024 Q1-Q2'
\echo '============================================================================'

\timing on

-- Query 1: Temporal range with both start and end filters
EXPLAIN (ANALYZE, BUFFERS, TIMING ON)
SELECT collection_id, id, datetime, start_datetime, end_datetime
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND (COALESCE(end_datetime, datetime) IS NULL OR COALESCE(end_datetime, datetime) >= '2024-01-01'::TIMESTAMPTZ)
  AND (COALESCE(start_datetime, datetime) IS NULL OR COALESCE(start_datetime, datetime) <= '2024-06-30'::TIMESTAMPTZ)
ORDER BY collection_id, id
LIMIT 100;

\echo ''
\echo 'Expected with optimized indexes: Index Scan using idx_stac_items_temporal_range, < 50ms'
\echo 'Expected without indexes: Seq Scan on stac_items, > 500ms'
\echo ''

-- ============================================================================
\echo '============================================================================'
\echo 'BENCHMARK 2: Start date filter only'
\echo 'Find items starting after a date'
\echo '============================================================================'

EXPLAIN (ANALYZE, BUFFERS, TIMING ON)
SELECT collection_id, id, datetime, start_datetime, end_datetime
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND (COALESCE(end_datetime, datetime) IS NULL OR COALESCE(end_datetime, datetime) >= '2024-03-01'::TIMESTAMPTZ)
ORDER BY collection_id, COALESCE(end_datetime, datetime)
LIMIT 100;

\echo ''
\echo 'Expected with optimized indexes: Index Scan using idx_stac_items_temporal_start, < 30ms'
\echo 'Expected without indexes: Seq Scan on stac_items, > 300ms'
\echo ''

-- ============================================================================
\echo '============================================================================'
\echo 'BENCHMARK 3: End date filter only'
\echo 'Find items ending before a date'
\echo '============================================================================'

EXPLAIN (ANALYZE, BUFFERS, TIMING ON)
SELECT collection_id, id, datetime, start_datetime, end_datetime
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND (COALESCE(start_datetime, datetime) IS NULL OR COALESCE(start_datetime, datetime) <= '2024-06-30'::TIMESTAMPTZ)
ORDER BY collection_id, COALESCE(start_datetime, datetime) DESC
LIMIT 100;

\echo ''
\echo 'Expected with optimized indexes: Index Scan using idx_stac_items_temporal_end, < 30ms'
\echo 'Expected without indexes: Seq Scan on stac_items, > 300ms'
\echo ''

-- ============================================================================
\echo '============================================================================'
\echo 'BENCHMARK 4: Point-in-time items only'
\echo 'Find items with exact datetime (no ranges)'
\echo '============================================================================'

EXPLAIN (ANALYZE, BUFFERS, TIMING ON)
SELECT collection_id, id, datetime
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND datetime IS NOT NULL
  AND start_datetime IS NULL
  AND end_datetime IS NULL
  AND datetime >= '2024-01-01'::TIMESTAMPTZ
  AND datetime <= '2024-06-30'::TIMESTAMPTZ
ORDER BY collection_id, datetime
LIMIT 100;

\echo ''
\echo 'Expected with optimized indexes: Index Scan using idx_stac_items_datetime_point, < 20ms'
\echo 'Expected without indexes: Seq Scan on stac_items, > 200ms'
\echo ''

-- ============================================================================
\echo '============================================================================'
\echo 'BENCHMARK 5: Range items only'
\echo 'Find items with temporal ranges (start and end)'
\echo '============================================================================'

EXPLAIN (ANALYZE, BUFFERS, TIMING ON)
SELECT collection_id, id, start_datetime, end_datetime
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND start_datetime IS NOT NULL
  AND end_datetime IS NOT NULL
  AND end_datetime >= '2024-01-01'::TIMESTAMPTZ
  AND start_datetime <= '2024-06-30'::TIMESTAMPTZ
ORDER BY collection_id, start_datetime
LIMIT 100;

\echo ''
\echo 'Expected with optimized indexes: Index Scan using idx_stac_items_datetime_range, < 20ms'
\echo 'Expected without indexes: Seq Scan on stac_items, > 200ms'
\echo ''

-- ============================================================================
\echo '============================================================================'
\echo 'BENCHMARK SUMMARY'
\echo '============================================================================'

-- Count queries by type
SELECT
    'Point-in-time items' AS item_type,
    COUNT(*) AS count
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND datetime IS NOT NULL
  AND start_datetime IS NULL
  AND end_datetime IS NULL
UNION ALL
SELECT
    'Range items' AS item_type,
    COUNT(*) AS count
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND start_datetime IS NOT NULL
  AND end_datetime IS NOT NULL
UNION ALL
SELECT
    'Mixed items' AS item_type,
    COUNT(*) AS count
FROM stac_items
WHERE collection_id = 'benchmark-collection'
  AND datetime IS NOT NULL
  AND (start_datetime IS NOT NULL OR end_datetime IS NOT NULL)
UNION ALL
SELECT
    'Total items' AS item_type,
    COUNT(*) AS count
FROM stac_items
WHERE collection_id = 'benchmark-collection';

\echo ''
\echo 'Performance improvement target: 10x faster'
\echo 'If queries show "Seq Scan", indexes are not being used - verify index creation.'
\echo 'If queries show "Index Scan using idx_stac_items_temporal_*", optimization is working!'
\echo ''

\timing off
