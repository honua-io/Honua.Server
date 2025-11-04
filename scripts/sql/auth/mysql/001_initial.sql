-- Honua authentication & RBAC schema (MySQL)
-- Adapted from PostgreSQL version: scripts/sql/auth/postgres/001_initial.sql
-- Syntax conversions: TIMESTAMPTZ→DATETIME(6), BOOLEAN→TINYINT(1), JSONB→JSON, UUID→CHAR(36), SERIAL→AUTO_INCREMENT

-- Note: MySQL uses databases instead of schemas, but we'll use table name prefixes (auth_*)
-- to maintain organizational structure similar to other database systems.

-- Create auth database if it doesn't exist (optional - uncomment if needed)
-- CREATE DATABASE IF NOT EXISTS auth;
-- USE auth;

-- For single-database deployments, we use auth_ prefix on table names

-- ==============================================
-- Roles Table
-- ==============================================
CREATE TABLE IF NOT EXISTS auth_roles (
    id              VARCHAR(64) PRIMARY KEY,
    name            VARCHAR(128) NOT NULL,
    description     VARCHAR(512) NOT NULL,
    created_at      DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at      DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==============================================
-- Users Table
-- ==============================================
CREATE TABLE IF NOT EXISTS auth_users (
    id                  CHAR(36) PRIMARY KEY,
    subject             VARCHAR(256) NULL,
    username            VARCHAR(128) NULL,
    email               VARCHAR(256) NULL,
    password_hash       VARBINARY(512) NULL,
    password_salt       VARBINARY(256) NULL,
    hash_algorithm      VARCHAR(64) NULL,
    hash_parameters     JSON NULL,
    is_active           TINYINT(1) NOT NULL DEFAULT 1,
    is_locked           TINYINT(1) NOT NULL DEFAULT 0,
    failed_attempts     INT NOT NULL DEFAULT 0,
    last_failed_at      DATETIME(6) NULL,
    last_login_at       DATETIME(6) NULL,
    created_at          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Unique constraints for nullable columns
CREATE UNIQUE INDEX uq_auth_users_username ON auth_users (username);
CREATE UNIQUE INDEX uq_auth_users_email ON auth_users (email);
CREATE UNIQUE INDEX uq_auth_users_subject ON auth_users (subject);

-- ==============================================
-- User Roles Junction Table
-- ==============================================
CREATE TABLE IF NOT EXISTS auth_user_roles (
    user_id     CHAR(36) NOT NULL,
    role_id     VARCHAR(64) NOT NULL,
    granted_at  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    granted_by  CHAR(36) NULL,
    PRIMARY KEY (user_id, role_id),
    CONSTRAINT fk_auth_user_roles_user FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE,
    CONSTRAINT fk_auth_user_roles_role FOREIGN KEY (role_id) REFERENCES auth_roles(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==============================================
-- Credentials Audit Log
-- ==============================================
CREATE TABLE IF NOT EXISTS auth_credentials_audit (
    id              BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id         CHAR(36) NOT NULL,
    action          VARCHAR(64) NOT NULL,
    details         JSON NULL,
    actor_id        CHAR(36) NULL,
    occurred_at     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==============================================
-- Bootstrap State
-- ==============================================
CREATE TABLE IF NOT EXISTS auth_bootstrap_state (
    id              INT PRIMARY KEY DEFAULT 1,
    completed_at    DATETIME(6) NULL,
    mode            VARCHAR(32) NULL,
    metadata        JSON NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ==============================================
-- Seed Data: Default Roles
-- ==============================================
INSERT INTO auth_roles (id, name, description)
VALUES
    ('administrator', 'Administrator', 'Full system control for Honua platform operations.'),
    ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.'),
    ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.')
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    description = VALUES(description),
    updated_at = CURRENT_TIMESTAMP(6);

-- ==============================================
-- Seed Data: Bootstrap State
-- ==============================================
INSERT INTO auth_bootstrap_state (id, completed_at, mode, metadata)
VALUES (1, NULL, NULL, NULL)
ON DUPLICATE KEY UPDATE id = id;

-- ==============================================
-- Performance Indexes (8 indexes total)
-- ==============================================

-- User lookup indexes (indexes 1-3)
CREATE INDEX idx_auth_users_subject ON auth_users(subject);
CREATE INDEX idx_auth_users_email ON auth_users(email);
CREATE INDEX idx_auth_users_username ON auth_users(username);

-- Active users composite index (index 4)
CREATE INDEX idx_auth_users_active ON auth_users(is_active, id);

-- Role membership lookups (indexes 5-6)
CREATE INDEX idx_auth_user_roles_user ON auth_user_roles(user_id);
CREATE INDEX idx_auth_user_roles_role ON auth_user_roles(role_id);

-- Audit trail indexes (indexes 7-8)
CREATE INDEX idx_auth_credentials_audit_user ON auth_credentials_audit(user_id, occurred_at DESC);
CREATE INDEX idx_auth_credentials_audit_time ON auth_credentials_audit(occurred_at DESC);

-- ==============================================
-- Triggers for updated_at columns
-- ==============================================
-- Note: MySQL 8.0+ supports ON UPDATE CURRENT_TIMESTAMP in column definition,
-- which we've already used above. However, for compatibility with older versions
-- or more complex update logic, you can use triggers as shown below.

-- Trigger for roles table
DELIMITER //
CREATE TRIGGER trg_auth_roles_updated_at
BEFORE UPDATE ON auth_roles
FOR EACH ROW
BEGIN
    SET NEW.updated_at = CURRENT_TIMESTAMP(6);
END//
DELIMITER ;

-- Trigger for users table
DELIMITER //
CREATE TRIGGER trg_auth_users_updated_at
BEFORE UPDATE ON auth_users
FOR EACH ROW
BEGIN
    SET NEW.updated_at = CURRENT_TIMESTAMP(6);
END//
DELIMITER ;

-- ==============================================
-- Table Statistics and Optimization
-- ==============================================

-- Update table statistics for query optimizer
ANALYZE TABLE auth_roles;
ANALYZE TABLE auth_users;
ANALYZE TABLE auth_user_roles;
ANALYZE TABLE auth_credentials_audit;
ANALYZE TABLE auth_bootstrap_state;

-- ==============================================
-- Verification Queries
-- ==============================================

-- Verify tables were created
/*
SELECT TABLE_NAME, ENGINE, TABLE_ROWS,
       ROUND((DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) AS size_mb
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'auth_%'
ORDER BY TABLE_NAME;
*/

-- Verify indexes were created
/*
SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'auth_%'
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;
*/

-- Verify default roles exist
/*
SELECT id, name, description, created_at
FROM auth_roles
ORDER BY id;
*/

-- ==============================================
-- Notes
-- ==============================================

-- 1. Character Set: Using utf8mb4 for full Unicode support (including emojis)
-- 2. Collation: utf8mb4_unicode_ci for case-insensitive comparisons
-- 3. Engine: InnoDB for ACID compliance and foreign key support
-- 4. Timestamps: DATETIME(6) provides microsecond precision equivalent to PostgreSQL TIMESTAMPTZ
-- 5. UUIDs: Stored as CHAR(36) in standard format (e.g., '550e8400-e29b-41d4-a716-446655440000')
-- 6. Binary Data: VARBINARY for password hashes and salts
-- 7. JSON: Native JSON type available in MySQL 5.7.8+
-- 8. Auto-increment: Used for audit log primary key (equivalent to PostgreSQL BIGSERIAL)
-- 9. Unique Indexes: Applied to nullable columns to allow multiple NULL values
-- 10. Triggers: Manual triggers added for updated_at (also using ON UPDATE in column definition)

-- ==============================================
-- Migration from PostgreSQL
-- ==============================================

-- Key differences from PostgreSQL version:
-- 1. No schema support - using table name prefixes (auth_) instead
-- 2. TIMESTAMPTZ → DATETIME(6) with explicit UTC handling in application
-- 3. BOOLEAN → TINYINT(1)
-- 4. JSONB → JSON (MySQL 5.7.8+ has native JSON type with automatic validation)
-- 5. TEXT → VARCHAR(n) with appropriate sizes
-- 6. BYTEA → VARBINARY
-- 7. UUID → CHAR(36)
-- 8. SERIAL/BIGSERIAL → INT/BIGINT AUTO_INCREMENT
-- 9. UNIQUE NULLS NOT DISTINCT → Unique indexes (MySQL allows multiple NULLs in unique indexes by default)
-- 10. Function-based triggers → MySQL trigger syntax with DELIMITER
-- 11. ON CONFLICT → ON DUPLICATE KEY UPDATE
-- 12. NOW() → CURRENT_TIMESTAMP(6)

-- ==============================================
-- Compatibility
-- ==============================================

-- Minimum MySQL version: 5.7.8 (for JSON support)
-- Recommended MySQL version: 8.0+ (for better performance and features)
-- Works with: MySQL 5.7.8+, MySQL 8.0+, MariaDB 10.2+
