-- Verification Script for CHECK Constraints
-- Database: PostgreSQL
-- Purpose: Verify all CHECK constraints are properly installed

\echo '=== Verifying CHECK Constraints Installation ==='
\echo ''

-- Check auth.users constraints
\echo 'Auth Users Constraints:'
SELECT
    conname AS constraint_name,
    pg_get_constraintdef(oid) AS constraint_definition
FROM pg_constraint
WHERE conrelid = 'auth.users'::regclass
  AND contype = 'c'
ORDER BY conname;

\echo ''

-- Check stac_items constraints
\echo 'STAC Items Constraints:'
SELECT
    conname AS constraint_name,
    pg_get_constraintdef(oid) AS constraint_definition
FROM pg_constraint
WHERE conrelid = 'stac_items'::regclass
  AND contype = 'c'
ORDER BY conname;

\echo ''
\echo '=== Expected Constraints ==='
\echo 'Auth Users:'
\echo '  - chk_auth_users_failed_attempts'
\echo '  - chk_auth_users_auth_method'
\echo ''
\echo 'STAC Items:'
\echo '  - chk_stac_items_temporal'
\echo '  - chk_stac_items_temporal_order'
\echo ''
