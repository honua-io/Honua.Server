-- Verification Script for CHECK Constraints
-- Database: SQL Server
-- Purpose: Verify all CHECK constraints are properly installed

PRINT '=== Verifying CHECK Constraints Installation ==='
PRINT ''

-- Check auth.users constraints
PRINT 'Auth Users Constraints:'
SELECT
    name AS constraint_name,
    definition AS constraint_definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('auth.users')
ORDER BY name;

PRINT ''

-- Check stac_items constraints
PRINT 'STAC Items Constraints:'
SELECT
    name AS constraint_name,
    definition AS constraint_definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('stac_items')
ORDER BY name;

PRINT ''
PRINT '=== Expected Constraints ==='
PRINT 'Auth Users:'
PRINT '  - chk_auth_users_failed_attempts'
PRINT '  - chk_auth_users_auth_method'
PRINT ''
PRINT 'STAC Items:'
PRINT '  - chk_stac_items_temporal'
PRINT '  - chk_stac_items_temporal_order'
PRINT ''
