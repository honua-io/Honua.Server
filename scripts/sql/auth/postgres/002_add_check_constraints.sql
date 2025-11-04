-- Migration: Add CHECK Constraints to Auth Tables
-- Description: Adds CHECK constraints for data validation on auth.users table
-- Database: PostgreSQL
-- Date: 2025-10-18
-- Version: 1.0

-- Add CHECK constraint for failed_attempts
-- Ensures failed_attempts is between 0 and 100
ALTER TABLE auth.users ADD CONSTRAINT chk_auth_users_failed_attempts
    CHECK (failed_attempts >= 0 AND failed_attempts <= 100);

-- Add CHECK constraint for authentication method validation
-- Ensures users have either OIDC (subject) or local auth (username/email + password)
ALTER TABLE auth.users ADD CONSTRAINT chk_auth_users_auth_method
    CHECK (
        (subject IS NOT NULL AND password_hash IS NULL) OR
        (username IS NOT NULL AND password_hash IS NOT NULL) OR
        (email IS NOT NULL AND password_hash IS NOT NULL)
    );

-- Add comments to document the constraints
COMMENT ON CONSTRAINT chk_auth_users_failed_attempts ON auth.users IS
    'Validates failed login attempts are within reasonable bounds (0-100)';

COMMENT ON CONSTRAINT chk_auth_users_auth_method ON auth.users IS
    'Ensures users have valid authentication method: OIDC (subject only) or local (username/email with password)';
