-- Migration: Create Sharing Tables
-- Description: Creates tables for map sharing with tokens and comments
-- Author: Honua Team
-- Date: 2025-11-12

-- ============================================================
-- PostgreSQL Migration
-- ============================================================

-- Create share_tokens table
CREATE TABLE IF NOT EXISTS share_tokens (
    token VARCHAR(100) PRIMARY KEY,
    map_id VARCHAR(100) NOT NULL,
    created_by VARCHAR(100),
    permission VARCHAR(20) NOT NULL DEFAULT 'view',
    allow_guest_access BOOLEAN NOT NULL DEFAULT true,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    access_count INTEGER NOT NULL DEFAULT 0,
    last_accessed_at TIMESTAMPTZ,
    is_active BOOLEAN NOT NULL DEFAULT true,
    password_hash VARCHAR(500),
    embed_settings JSONB
);

-- Create indexes for share_tokens
CREATE INDEX IF NOT EXISTS idx_share_tokens_map_id ON share_tokens(map_id);
CREATE INDEX IF NOT EXISTS idx_share_tokens_created_by ON share_tokens(created_by);
CREATE INDEX IF NOT EXISTS idx_share_tokens_expires_at ON share_tokens(expires_at) WHERE expires_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_share_tokens_active ON share_tokens(is_active) WHERE is_active = true;

-- Create share_comments table
CREATE TABLE IF NOT EXISTS share_comments (
    id VARCHAR(100) PRIMARY KEY,
    share_token VARCHAR(100) NOT NULL,
    map_id VARCHAR(100) NOT NULL,
    author VARCHAR(200) NOT NULL,
    is_guest BOOLEAN NOT NULL DEFAULT true,
    guest_email VARCHAR(200),
    comment_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_approved BOOLEAN NOT NULL DEFAULT false,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    parent_id VARCHAR(100),
    location_x DOUBLE PRECISION,
    location_y DOUBLE PRECISION,
    ip_address VARCHAR(45),
    user_agent VARCHAR(500),
    FOREIGN KEY (share_token) REFERENCES share_tokens(token) ON DELETE CASCADE,
    FOREIGN KEY (parent_id) REFERENCES share_comments(id) ON DELETE SET NULL
);

-- Create indexes for share_comments
CREATE INDEX IF NOT EXISTS idx_share_comments_token ON share_comments(share_token);
CREATE INDEX IF NOT EXISTS idx_share_comments_map_id ON share_comments(map_id);
CREATE INDEX IF NOT EXISTS idx_share_comments_parent ON share_comments(parent_id) WHERE parent_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_share_comments_approved ON share_comments(is_approved, is_deleted) WHERE NOT is_deleted;
CREATE INDEX IF NOT EXISTS idx_share_comments_created ON share_comments(created_at DESC);

-- Add comment to tables
COMMENT ON TABLE share_tokens IS 'Stores shareable link tokens for maps with permissions and expiration';
COMMENT ON TABLE share_comments IS 'Stores comments on shared maps from guests and authenticated users';

-- ============================================================
-- SQLite Migration
-- ============================================================

-- Note: For SQLite, use the following SQL:

-- CREATE TABLE IF NOT EXISTS share_tokens (
--     token TEXT PRIMARY KEY,
--     map_id TEXT NOT NULL,
--     created_by TEXT,
--     permission TEXT NOT NULL DEFAULT 'view',
--     allow_guest_access INTEGER NOT NULL DEFAULT 1,
--     expires_at TEXT,
--     created_at TEXT NOT NULL,
--     access_count INTEGER NOT NULL DEFAULT 0,
--     last_accessed_at TEXT,
--     is_active INTEGER NOT NULL DEFAULT 1,
--     password_hash TEXT,
--     embed_settings TEXT
-- );
--
-- CREATE INDEX IF NOT EXISTS idx_share_tokens_map_id ON share_tokens(map_id);
-- CREATE INDEX IF NOT EXISTS idx_share_tokens_created_by ON share_tokens(created_by);
--
-- CREATE TABLE IF NOT EXISTS share_comments (
--     id TEXT PRIMARY KEY,
--     share_token TEXT NOT NULL,
--     map_id TEXT NOT NULL,
--     author TEXT NOT NULL,
--     is_guest INTEGER NOT NULL DEFAULT 1,
--     guest_email TEXT,
--     comment_text TEXT NOT NULL,
--     created_at TEXT NOT NULL,
--     is_approved INTEGER NOT NULL DEFAULT 0,
--     is_deleted INTEGER NOT NULL DEFAULT 0,
--     parent_id TEXT,
--     location_x REAL,
--     location_y REAL,
--     ip_address TEXT,
--     user_agent TEXT,
--     FOREIGN KEY (share_token) REFERENCES share_tokens(token) ON DELETE CASCADE
-- );
--
-- CREATE INDEX IF NOT EXISTS idx_share_comments_token ON share_comments(share_token);
-- CREATE INDEX IF NOT EXISTS idx_share_comments_map_id ON share_comments(map_id);

-- ============================================================
-- MySQL Migration
-- ============================================================

-- Note: For MySQL, use the following SQL:

-- CREATE TABLE IF NOT EXISTS share_tokens (
--     token VARCHAR(100) PRIMARY KEY,
--     map_id VARCHAR(100) NOT NULL,
--     created_by VARCHAR(100),
--     permission VARCHAR(20) NOT NULL DEFAULT 'view',
--     allow_guest_access BOOLEAN NOT NULL DEFAULT true,
--     expires_at DATETIME,
--     created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
--     access_count INT NOT NULL DEFAULT 0,
--     last_accessed_at DATETIME,
--     is_active BOOLEAN NOT NULL DEFAULT true,
--     password_hash VARCHAR(500),
--     embed_settings JSON,
--     INDEX idx_share_tokens_map_id (map_id),
--     INDEX idx_share_tokens_created_by (created_by)
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
--
-- CREATE TABLE IF NOT EXISTS share_comments (
--     id VARCHAR(100) PRIMARY KEY,
--     share_token VARCHAR(100) NOT NULL,
--     map_id VARCHAR(100) NOT NULL,
--     author VARCHAR(200) NOT NULL,
--     is_guest BOOLEAN NOT NULL DEFAULT true,
--     guest_email VARCHAR(200),
--     comment_text TEXT NOT NULL,
--     created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
--     is_approved BOOLEAN NOT NULL DEFAULT false,
--     is_deleted BOOLEAN NOT NULL DEFAULT false,
--     parent_id VARCHAR(100),
--     location_x DOUBLE,
--     location_y DOUBLE,
--     ip_address VARCHAR(45),
--     user_agent VARCHAR(500),
--     INDEX idx_share_comments_token (share_token),
--     INDEX idx_share_comments_map_id (map_id),
--     FOREIGN KEY (share_token) REFERENCES share_tokens(token) ON DELETE CASCADE
-- ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================
-- SQL Server Migration
-- ============================================================

-- Note: For SQL Server, use the following SQL:

-- CREATE TABLE share_tokens (
--     token NVARCHAR(100) PRIMARY KEY,
--     map_id NVARCHAR(100) NOT NULL,
--     created_by NVARCHAR(100),
--     permission NVARCHAR(20) NOT NULL DEFAULT 'view',
--     allow_guest_access BIT NOT NULL DEFAULT 1,
--     expires_at DATETIMEOFFSET,
--     created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
--     access_count INT NOT NULL DEFAULT 0,
--     last_accessed_at DATETIMEOFFSET,
--     is_active BIT NOT NULL DEFAULT 1,
--     password_hash NVARCHAR(500),
--     embed_settings NVARCHAR(MAX)
-- );
--
-- CREATE INDEX idx_share_tokens_map_id ON share_tokens(map_id);
-- CREATE INDEX idx_share_tokens_created_by ON share_tokens(created_by);
--
-- CREATE TABLE share_comments (
--     id NVARCHAR(100) PRIMARY KEY,
--     share_token NVARCHAR(100) NOT NULL,
--     map_id NVARCHAR(100) NOT NULL,
--     author NVARCHAR(200) NOT NULL,
--     is_guest BIT NOT NULL DEFAULT 1,
--     guest_email NVARCHAR(200),
--     comment_text NVARCHAR(MAX) NOT NULL,
--     created_at DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
--     is_approved BIT NOT NULL DEFAULT 0,
--     is_deleted BIT NOT NULL DEFAULT 0,
--     parent_id NVARCHAR(100),
--     location_x FLOAT,
--     location_y FLOAT,
--     ip_address NVARCHAR(45),
--     user_agent NVARCHAR(500),
--     FOREIGN KEY (share_token) REFERENCES share_tokens(token) ON DELETE CASCADE
-- );
--
-- CREATE INDEX idx_share_comments_token ON share_comments(share_token);
-- CREATE INDEX idx_share_comments_map_id ON share_comments(map_id);
