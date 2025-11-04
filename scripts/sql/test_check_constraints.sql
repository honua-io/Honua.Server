-- Test Script for CHECK Constraints
-- Database: PostgreSQL (adapt for other databases as needed)
-- Purpose: Verify CHECK constraints prevent invalid data

-- ==============================================================================
-- SETUP: Create test schema and apply migrations
-- ==============================================================================

-- This script assumes auth and stac schemas exist with migrations applied
-- Run this after applying all migrations including CHECK constraints

-- ==============================================================================
-- AUTH USERS TESTS
-- ==============================================================================

\echo '--- Testing auth.users CHECK constraints ---'

-- TEST 1: Valid OIDC user (should succeed)
\echo 'TEST 1: Valid OIDC user'
BEGIN;
INSERT INTO auth.users (id, subject, failed_attempts)
VALUES ('00000000-0000-0000-0000-000000000001', 'oidc-subject-123', 0);
\echo 'SUCCESS: OIDC user inserted'
ROLLBACK;

-- TEST 2: Valid local user with username (should succeed)
\echo 'TEST 2: Valid local user with username'
BEGIN;
INSERT INTO auth.users (id, username, password_hash, failed_attempts)
VALUES ('00000000-0000-0000-0000-000000000002', 'testuser', '\x1234567890abcdef'::bytea, 2);
\echo 'SUCCESS: Local user with username inserted'
ROLLBACK;

-- TEST 3: Valid local user with email (should succeed)
\echo 'TEST 3: Valid local user with email'
BEGIN;
INSERT INTO auth.users (id, email, password_hash, failed_attempts)
VALUES ('00000000-0000-0000-0000-000000000003', 'test@example.com', '\x1234567890abcdef'::bytea, 5);
\echo 'SUCCESS: Local user with email inserted'
ROLLBACK;

-- TEST 4: Invalid - negative failed_attempts (should fail)
\echo 'TEST 4: Invalid - negative failed_attempts (should fail)'
BEGIN;
DO $$
BEGIN
    INSERT INTO auth.users (id, username, password_hash, failed_attempts)
    VALUES ('00000000-0000-0000-0000-000000000004', 'baduser1', '\x1234'::bytea, -1);
    RAISE EXCEPTION 'TEST FAILED: Negative failed_attempts was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Negative failed_attempts rejected';
END $$;
ROLLBACK;

-- TEST 5: Invalid - failed_attempts too high (should fail)
\echo 'TEST 5: Invalid - failed_attempts too high (should fail)'
BEGIN;
DO $$
BEGIN
    INSERT INTO auth.users (id, username, password_hash, failed_attempts)
    VALUES ('00000000-0000-0000-0000-000000000005', 'baduser2', '\x1234'::bytea, 150);
    RAISE EXCEPTION 'TEST FAILED: High failed_attempts was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: High failed_attempts rejected';
END $$;
ROLLBACK;

-- TEST 6: Invalid - subject AND password (should fail)
\echo 'TEST 6: Invalid - subject AND password (should fail)'
BEGIN;
DO $$
BEGIN
    INSERT INTO auth.users (id, subject, password_hash, failed_attempts)
    VALUES ('00000000-0000-0000-0000-000000000006', 'oidc-123', '\x1234'::bytea, 0);
    RAISE EXCEPTION 'TEST FAILED: Subject with password was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Subject with password rejected';
END $$;
ROLLBACK;

-- TEST 7: Invalid - username without password (should fail)
\echo 'TEST 7: Invalid - username without password (should fail)'
BEGIN;
DO $$
BEGIN
    INSERT INTO auth.users (id, username, failed_attempts)
    VALUES ('00000000-0000-0000-0000-000000000007', 'baduser3', 0);
    RAISE EXCEPTION 'TEST FAILED: Username without password was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Username without password rejected';
END $$;
ROLLBACK;

-- TEST 8: Invalid - no identifier (should fail)
\echo 'TEST 8: Invalid - no identifier (should fail)'
BEGIN;
DO $$
BEGIN
    INSERT INTO auth.users (id, failed_attempts)
    VALUES ('00000000-0000-0000-0000-000000000008', 0);
    RAISE EXCEPTION 'TEST FAILED: User with no identifier was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: User with no identifier rejected';
END $$;
ROLLBACK;

-- ==============================================================================
-- STAC ITEMS TESTS
-- ==============================================================================

\echo ''
\echo '--- Testing stac_items CHECK constraints ---'

-- First create a test collection
BEGIN;
INSERT INTO stac_collections (id, keywords_json, extent_json, links_json, extensions_json, created_at, updated_at)
VALUES ('test-collection', '[]', '{"spatial":{"bbox":[[-180,-90,180,90]]},"temporal":{"interval":[[null,null]]}}', '[]', '[]', NOW(), NOW());

-- TEST 9: Valid point-in-time item (should succeed)
\echo 'TEST 9: Valid point-in-time item'
INSERT INTO stac_items (collection_id, id, datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('test-collection', 'test-item-1', '2024-01-15T12:00:00Z', '{}', '[]', '[]', NOW(), NOW());
\echo 'SUCCESS: Point-in-time item inserted'

-- TEST 10: Valid temporal range item (should succeed)
\echo 'TEST 10: Valid temporal range item'
INSERT INTO stac_items (collection_id, id, start_datetime, end_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
VALUES ('test-collection', 'test-item-2', '2024-01-01T00:00:00Z', '2024-01-31T23:59:59Z', '{}', '[]', '[]', NOW(), NOW());
\echo 'SUCCESS: Temporal range item inserted'

-- TEST 11: Invalid - no temporal data (should fail)
\echo 'TEST 11: Invalid - no temporal data (should fail)'
DO $$
BEGIN
    INSERT INTO stac_items (collection_id, id, assets_json, links_json, extensions_json, created_at, updated_at)
    VALUES ('test-collection', 'test-item-3', '{}', '[]', '[]', NOW(), NOW());
    RAISE EXCEPTION 'TEST FAILED: Item without temporal data was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Item without temporal data rejected';
END $$;

-- TEST 12: Invalid - only start_datetime (should fail)
\echo 'TEST 12: Invalid - only start_datetime (should fail)'
DO $$
BEGIN
    INSERT INTO stac_items (collection_id, id, start_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
    VALUES ('test-collection', 'test-item-4', '2024-01-01T00:00:00Z', '{}', '[]', '[]', NOW(), NOW());
    RAISE EXCEPTION 'TEST FAILED: Item with only start_datetime was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Item with only start_datetime rejected';
END $$;

-- TEST 13: Invalid - only end_datetime (should fail)
\echo 'TEST 13: Invalid - only end_datetime (should fail)'
DO $$
BEGIN
    INSERT INTO stac_items (collection_id, id, end_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
    VALUES ('test-collection', 'test-item-5', '2024-01-31T23:59:59Z', '{}', '[]', '[]', NOW(), NOW());
    RAISE EXCEPTION 'TEST FAILED: Item with only end_datetime was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Item with only end_datetime rejected';
END $$;

-- TEST 14: Invalid - end before start (should fail)
\echo 'TEST 14: Invalid - end before start (should fail)'
DO $$
BEGIN
    INSERT INTO stac_items (collection_id, id, start_datetime, end_datetime, assets_json, links_json, extensions_json, created_at, updated_at)
    VALUES ('test-collection', 'test-item-6', '2024-12-31T23:59:59Z', '2024-01-01T00:00:00Z', '{}', '[]', '[]', NOW(), NOW());
    RAISE EXCEPTION 'TEST FAILED: Item with reversed temporal range was allowed';
EXCEPTION
    WHEN check_violation THEN
        RAISE NOTICE 'SUCCESS: Item with reversed temporal range rejected';
END $$;

ROLLBACK;

\echo ''
\echo '--- All tests completed ---'
