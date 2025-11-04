-- Migration: Add CHECK Constraints to Auth Tables
-- Description: Adds CHECK constraints for data validation on auth.users table
-- Database: SQL Server
-- Date: 2025-10-18
-- Version: 1.0

-- Add CHECK constraint for failed_attempts
-- Ensures failed_attempts is between 0 and 100
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'chk_auth_users_failed_attempts'
    AND parent_object_id = OBJECT_ID('auth.users')
)
BEGIN
    ALTER TABLE auth.users ADD CONSTRAINT chk_auth_users_failed_attempts
        CHECK (failed_attempts >= 0 AND failed_attempts <= 100);
END
GO

-- Add CHECK constraint for authentication method validation
-- Ensures users have either OIDC (subject) or local auth (username/email + password)
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'chk_auth_users_auth_method'
    AND parent_object_id = OBJECT_ID('auth.users')
)
BEGIN
    ALTER TABLE auth.users ADD CONSTRAINT chk_auth_users_auth_method
        CHECK (
            (subject IS NOT NULL AND password_hash IS NULL) OR
            (username IS NOT NULL AND password_hash IS NOT NULL) OR
            (email IS NOT NULL AND password_hash IS NOT NULL)
        );
END
GO

-- Add extended properties to document the constraints
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Validates failed login attempts are within reasonable bounds (0-100)',
    @level0type = N'SCHEMA', @level0name = 'auth',
    @level1type = N'TABLE', @level1name = 'users',
    @level2type = N'CONSTRAINT', @level2name = 'chk_auth_users_failed_attempts';
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Ensures users have valid authentication method: OIDC (subject only) or local (username/email with password)',
    @level0type = N'SCHEMA', @level0name = 'auth',
    @level1type = N'TABLE', @level1name = 'users',
    @level2type = N'CONSTRAINT', @level2name = 'chk_auth_users_auth_method';
GO
