-- Migration: Add CHECK Constraints to Auth Tables
-- Description: Adds CHECK constraints for data validation on auth_users table
-- Database: MySQL
-- Date: 2025-10-18
-- Version: 1.0

-- Note: MySQL 8.0.16+ supports CHECK constraints

-- Add CHECK constraint for failed_attempts
-- Ensures failed_attempts is between 0 and 100
ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_failed_attempts
    CHECK (failed_attempts >= 0 AND failed_attempts <= 100);

-- Add CHECK constraint for authentication method validation
-- Ensures users have either OIDC (subject) or local auth (username/email + password)
ALTER TABLE auth_users ADD CONSTRAINT chk_auth_users_auth_method
    CHECK (
        (subject IS NOT NULL AND password_hash IS NULL) OR
        (username IS NOT NULL AND password_hash IS NOT NULL) OR
        (email IS NOT NULL AND password_hash IS NOT NULL)
    );
