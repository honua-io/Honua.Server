-- Verification Script for CHECK Constraints
-- Database: SQLite
-- Purpose: Verify all CHECK constraints are properly installed

.print '=== Verifying CHECK Constraints Installation ==='
.print ''

-- Check auth_users table schema (includes CHECK constraints)
.print 'Auth Users Table Schema (includes CHECK constraints):'
SELECT sql FROM sqlite_master WHERE type='table' AND name='auth_users';

.print ''
.print ''

-- Check stac_items table schema (includes CHECK constraints)
.print 'STAC Items Table Schema (includes CHECK constraints):'
SELECT sql FROM sqlite_master WHERE type='table' AND name='stac_items';

.print ''
.print ''
.print '=== Expected Constraints ==='
.print 'Auth Users:'
.print '  - CHECK (failed_attempts >= 0 AND failed_attempts <= 100)'
.print '  - CHECK ((subject IS NOT NULL AND password_hash IS NULL) OR ...)'
.print ''
.print 'STAC Items:'
.print '  - CHECK (datetime IS NOT NULL OR (start_datetime IS NOT NULL AND end_datetime IS NOT NULL))'
.print '  - CHECK (start_datetime IS NULL OR end_datetime IS NULL OR start_datetime <= end_datetime)'
.print ''
