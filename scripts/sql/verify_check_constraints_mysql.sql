-- Verification Script for CHECK Constraints
-- Database: MySQL
-- Purpose: Verify all CHECK constraints are properly installed

SELECT '=== Verifying CHECK Constraints Installation ===' AS '';

-- Check auth_users constraints
SELECT 'Auth Users Constraints:' AS '';
SELECT
    CONSTRAINT_NAME,
    CHECK_CLAUSE
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE()
  AND TABLE_NAME = 'auth_users'
ORDER BY CONSTRAINT_NAME;

SELECT '' AS '';

-- Check stac_items constraints
SELECT 'STAC Items Constraints:' AS '';
SELECT
    CONSTRAINT_NAME,
    CHECK_CLAUSE
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = DATABASE()
  AND TABLE_NAME = 'stac_items'
ORDER BY CONSTRAINT_NAME;

SELECT '' AS '';
SELECT '=== Expected Constraints ===' AS '';
SELECT 'Auth Users:' AS '';
SELECT '  - chk_auth_users_failed_attempts' AS '';
SELECT '  - chk_auth_users_auth_method' AS '';
SELECT '' AS '';
SELECT 'STAC Items:' AS '';
SELECT '  - chk_stac_items_temporal' AS '';
SELECT '  - chk_stac_items_temporal_order' AS '';
SELECT '' AS '';
