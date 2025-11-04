-- Migration: Add CHECK Constraints to Auth Tables
-- Description: Recreates auth_users table with CHECK constraints for data validation
-- Database: SQLite
-- Date: 2025-10-18
-- Version: 1.0

-- Note: SQLite requires recreating tables to add CHECK constraints
-- This migration preserves all existing data

-- Step 1: Create new table with CHECK constraints
CREATE TABLE IF NOT EXISTS auth_users_new (
    id                  TEXT PRIMARY KEY,
    subject             TEXT,
    username            TEXT,
    email               TEXT,
    password_hash       BLOB,
    password_salt       BLOB,
    hash_algorithm      TEXT,
    hash_parameters     TEXT,
    is_active           INTEGER NOT NULL DEFAULT 1,
    is_locked           INTEGER NOT NULL DEFAULT 0,
    failed_attempts     INTEGER NOT NULL DEFAULT 0,
    last_failed_at      TEXT,
    last_login_at       TEXT,
    created_at          TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at          TEXT NOT NULL DEFAULT (datetime('now')),
    is_service_account  INTEGER NOT NULL DEFAULT 0,
    password_changed_at TEXT,
    password_expires_at TEXT,
    UNIQUE(username),
    UNIQUE(email),
    UNIQUE(subject),
    -- CHECK constraint: failed_attempts must be between 0 and 100
    CHECK (failed_attempts >= 0 AND failed_attempts <= 100),
    -- CHECK constraint: valid authentication method (OIDC or local)
    CHECK (
        (subject IS NOT NULL AND password_hash IS NULL) OR
        (username IS NOT NULL AND password_hash IS NOT NULL) OR
        (email IS NOT NULL AND password_hash IS NOT NULL)
    )
);

-- Step 2: Copy data from old table to new table
INSERT INTO auth_users_new
    SELECT * FROM auth_users;

-- Step 3: Drop old table
DROP TABLE auth_users;

-- Step 4: Rename new table to original name
ALTER TABLE auth_users_new RENAME TO auth_users;

-- Step 5: Recreate triggers
DROP TRIGGER IF EXISTS trg_auth_users_updated_at;
CREATE TRIGGER trg_auth_users_updated_at
AFTER UPDATE ON auth_users
FOR EACH ROW
BEGIN
    UPDATE auth_users SET updated_at = datetime('now') WHERE rowid = NEW.rowid;
END;

-- Step 6: Recreate indexes
CREATE INDEX IF NOT EXISTS idx_auth_users_subject ON auth_users(subject) WHERE subject IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_email ON auth_users(email) WHERE email IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_username ON auth_users(username) WHERE username IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_auth_users_active ON auth_users(is_active, id);
CREATE INDEX IF NOT EXISTS idx_auth_users_password_expires_at ON auth_users(password_expires_at);
