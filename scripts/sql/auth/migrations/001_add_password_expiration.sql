-- Migration: Add Password Expiration Support
-- Description: Adds password_changed_at, password_expires_at, and is_service_account columns to auth_users table
-- Date: 2025-10-18
-- Version: 1.0

-- SQLite Migration
-- Add new columns for password expiration tracking
ALTER TABLE auth_users ADD COLUMN is_service_account INTEGER NOT NULL DEFAULT 0;
ALTER TABLE auth_users ADD COLUMN password_changed_at TEXT;
ALTER TABLE auth_users ADD COLUMN password_expires_at TEXT;

-- Add index on password_expires_at for efficient expiration checks
CREATE INDEX IF NOT EXISTS idx_auth_users_password_expires_at ON auth_users(password_expires_at);

-- Update existing users: Set password_changed_at to created_at for users with passwords
UPDATE auth_users
SET password_changed_at = created_at
WHERE password_hash IS NOT NULL
  AND password_changed_at IS NULL;

-- Add audit record for migration
INSERT INTO auth_credentials_audit (user_id, action, details, occurred_at)
SELECT id, 'schema_migration', 'Added password expiration fields', datetime('now')
FROM auth_users
WHERE password_hash IS NOT NULL;

-- PostgreSQL Migration (for reference)
-- Uncomment and run these commands if using PostgreSQL:
/*
ALTER TABLE auth_users ADD COLUMN IF NOT EXISTS is_service_account BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE auth_users ADD COLUMN IF NOT EXISTS password_changed_at TIMESTAMP WITH TIME ZONE;
ALTER TABLE auth_users ADD COLUMN IF NOT EXISTS password_expires_at TIMESTAMP WITH TIME ZONE;

CREATE INDEX IF NOT EXISTS idx_auth_users_password_expires_at ON auth_users(password_expires_at);

UPDATE auth_users
SET password_changed_at = created_at
WHERE password_hash IS NOT NULL
  AND password_changed_at IS NULL;

INSERT INTO auth_credentials_audit (user_id, action, details, occurred_at)
SELECT id, 'schema_migration', 'Added password expiration fields', NOW()
FROM auth_users
WHERE password_hash IS NOT NULL;
*/

-- SQL Server Migration (for reference)
-- Uncomment and run these commands if using SQL Server:
/*
ALTER TABLE auth_users ADD is_service_account BIT NOT NULL DEFAULT 0;
ALTER TABLE auth_users ADD password_changed_at DATETIMEOFFSET;
ALTER TABLE auth_users ADD password_expires_at DATETIMEOFFSET;

CREATE INDEX idx_auth_users_password_expires_at ON auth_users(password_expires_at);

UPDATE auth_users
SET password_changed_at = created_at
WHERE password_hash IS NOT NULL
  AND password_changed_at IS NULL;

INSERT INTO auth_credentials_audit (user_id, action, details, occurred_at)
SELECT id, 'schema_migration', 'Added password expiration fields', SYSDATETIMEOFFSET()
FROM auth_users
WHERE password_hash IS NOT NULL;
*/
