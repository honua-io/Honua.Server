-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

-- Honua Visual Commenting System - Database Schema
-- SQLite compatible schema with PostgreSQL comments for reference

-- =====================================================
-- MAP COMMENTS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS map_comments (
    id TEXT PRIMARY KEY,
    map_id TEXT NOT NULL,
    layer_id TEXT,
    feature_id TEXT,

    -- Author information
    author TEXT NOT NULL,
    author_user_id TEXT,
    is_guest INTEGER NOT NULL DEFAULT 0,
    guest_email TEXT,

    -- Comment content
    comment_text TEXT NOT NULL,
    comment_markdown TEXT,

    -- Spatial data
    geometry_type TEXT NOT NULL DEFAULT 'point', -- point, line, polygon, none
    geometry TEXT,  -- GeoJSON geometry
    longitude REAL,
    latitude REAL,

    -- Timestamps
    created_at TEXT NOT NULL,
    updated_at TEXT,

    -- Threading
    parent_id TEXT,
    thread_depth INTEGER NOT NULL DEFAULT 0,

    -- Status and resolution
    status TEXT NOT NULL DEFAULT 'open', -- open, resolved, closed
    resolved_by TEXT,
    resolved_at TEXT,

    -- Categorization
    category TEXT,
    priority TEXT NOT NULL DEFAULT 'medium', -- low, medium, high, critical
    color TEXT,

    -- Moderation and visibility
    is_approved INTEGER NOT NULL DEFAULT 1,
    is_deleted INTEGER NOT NULL DEFAULT 0,
    is_pinned INTEGER NOT NULL DEFAULT 0,

    -- Enhanced features
    mentioned_users TEXT,  -- JSON array of user IDs
    attachments TEXT,      -- JSON array of attachment objects

    -- Tracking
    ip_address TEXT,
    user_agent TEXT,

    -- Metrics
    reply_count INTEGER NOT NULL DEFAULT 0,
    like_count INTEGER NOT NULL DEFAULT 0,

    -- Custom metadata
    metadata TEXT  -- JSON object for extensibility
);

-- =====================================================
-- INDEXES FOR PERFORMANCE
-- =====================================================
CREATE INDEX IF NOT EXISTS idx_map_comments_map_id ON map_comments(map_id);
CREATE INDEX IF NOT EXISTS idx_map_comments_layer_id ON map_comments(layer_id);
CREATE INDEX IF NOT EXISTS idx_map_comments_feature_id ON map_comments(feature_id);
CREATE INDEX IF NOT EXISTS idx_map_comments_author_user_id ON map_comments(author_user_id);
CREATE INDEX IF NOT EXISTS idx_map_comments_parent_id ON map_comments(parent_id);
CREATE INDEX IF NOT EXISTS idx_map_comments_status ON map_comments(status);
CREATE INDEX IF NOT EXISTS idx_map_comments_created_at ON map_comments(created_at);
CREATE INDEX IF NOT EXISTS idx_map_comments_category ON map_comments(category);
CREATE INDEX IF NOT EXISTS idx_map_comments_is_deleted ON map_comments(is_deleted);
CREATE INDEX IF NOT EXISTS idx_map_comments_is_approved ON map_comments(is_approved);

-- Spatial index for location-based queries (if using PostGIS)
-- CREATE INDEX idx_map_comments_geometry ON map_comments USING GIST(geometry);

-- =====================================================
-- COMMENT REACTIONS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS map_comment_reactions (
    id TEXT PRIMARY KEY,
    comment_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    reaction_type TEXT NOT NULL DEFAULT 'like',
    created_at TEXT NOT NULL,

    FOREIGN KEY (comment_id) REFERENCES map_comments(id) ON DELETE CASCADE,
    UNIQUE (comment_id, user_id, reaction_type)
);

CREATE INDEX IF NOT EXISTS idx_comment_reactions_comment_id ON map_comment_reactions(comment_id);
CREATE INDEX IF NOT EXISTS idx_comment_reactions_user_id ON map_comment_reactions(user_id);

-- =====================================================
-- COMMENT NOTIFICATIONS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS map_comment_notifications (
    id TEXT PRIMARY KEY,
    comment_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    notification_type TEXT NOT NULL, -- mentioned, reply, resolved, reopened, reaction
    is_read INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    read_at TEXT,

    FOREIGN KEY (comment_id) REFERENCES map_comments(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_comment_notifications_user_id ON map_comment_notifications(user_id);
CREATE INDEX IF NOT EXISTS idx_comment_notifications_comment_id ON map_comment_notifications(comment_id);
CREATE INDEX IF NOT EXISTS idx_comment_notifications_is_read ON map_comment_notifications(is_read);
CREATE INDEX IF NOT EXISTS idx_comment_notifications_created_at ON map_comment_notifications(created_at);

-- =====================================================
-- SAMPLE DATA (Optional - Remove in production)
-- =====================================================
/*
INSERT INTO map_comments (
    id, map_id, author, author_user_id, comment_text,
    geometry_type, longitude, latitude, created_at,
    status, priority, category, is_approved
) VALUES (
    'sample-comment-1',
    'map-001',
    'John Doe',
    'user-123',
    'This area needs attention. Found some issues with the data quality.',
    'point',
    -122.4194,
    37.7749,
    '2025-01-15T10:30:00Z',
    'open',
    'high',
    'Data Quality',
    1
);

INSERT INTO map_comments (
    id, map_id, parent_id, author, author_user_id, comment_text,
    geometry_type, created_at, thread_depth, status, priority, is_approved
) VALUES (
    'sample-reply-1',
    'map-001',
    'sample-comment-1',
    'Jane Smith',
    'user-456',
    '@JohnDoe I can help with this. Let me investigate.',
    'none',
    '2025-01-15T11:00:00Z',
    1,
    'open',
    'medium',
    1
);
*/

-- =====================================================
-- VIEWS FOR COMMON QUERIES
-- =====================================================

-- Active comments (not deleted, approved)
CREATE VIEW IF NOT EXISTS active_comments AS
SELECT * FROM map_comments
WHERE is_deleted = 0 AND is_approved = 1;

-- Open comments requiring attention
CREATE VIEW IF NOT EXISTS open_comments AS
SELECT * FROM map_comments
WHERE status = 'open' AND is_deleted = 0 AND is_approved = 1;

-- Comments with their reply counts
CREATE VIEW IF NOT EXISTS comments_with_stats AS
SELECT
    c.*,
    (SELECT COUNT(*) FROM map_comments r WHERE r.parent_id = c.id AND r.is_deleted = 0) as actual_reply_count,
    (SELECT COUNT(*) FROM map_comment_reactions r WHERE r.comment_id = c.id) as actual_like_count
FROM map_comments c
WHERE c.is_deleted = 0;

-- =====================================================
-- TRIGGERS (Optional - for auto-updating counts)
-- =====================================================

-- Note: SQLite doesn't support all trigger features
-- Below are examples that may need adjustment

-- Update parent reply count on insert
CREATE TRIGGER IF NOT EXISTS update_reply_count_on_insert
AFTER INSERT ON map_comments
WHEN NEW.parent_id IS NOT NULL
BEGIN
    UPDATE map_comments
    SET reply_count = reply_count + 1
    WHERE id = NEW.parent_id;
END;

-- Update parent reply count on delete
CREATE TRIGGER IF NOT EXISTS update_reply_count_on_delete
AFTER UPDATE ON map_comments
WHEN NEW.is_deleted = 1 AND OLD.parent_id IS NOT NULL
BEGIN
    UPDATE map_comments
    SET reply_count = reply_count - 1
    WHERE id = OLD.parent_id AND reply_count > 0;
END;

-- =====================================================
-- MAINTENANCE QUERIES
-- =====================================================

-- Clean up old deleted comments (run periodically)
-- DELETE FROM map_comments WHERE is_deleted = 1 AND updated_at < datetime('now', '-90 days');

-- Recalculate reply counts (if they get out of sync)
/*
UPDATE map_comments SET reply_count = (
    SELECT COUNT(*) FROM map_comments r
    WHERE r.parent_id = map_comments.id AND r.is_deleted = 0
);
*/

-- Recalculate like counts
/*
UPDATE map_comments SET like_count = (
    SELECT COUNT(*) FROM map_comment_reactions r
    WHERE r.comment_id = map_comments.id AND r.reaction_type = 'like'
);
*/

-- =====================================================
-- ANALYTICS QUERIES
-- =====================================================

-- Comments by status
-- SELECT status, COUNT(*) as count FROM map_comments WHERE is_deleted = 0 GROUP BY status;

-- Comments by priority
-- SELECT priority, COUNT(*) as count FROM map_comments WHERE is_deleted = 0 GROUP BY priority;

-- Most active commenters
-- SELECT author_user_id, author, COUNT(*) as comment_count
-- FROM map_comments WHERE is_deleted = 0
-- GROUP BY author_user_id, author
-- ORDER BY comment_count DESC LIMIT 10;

-- Response time analytics
-- SELECT
--     AVG(CAST((julianday(resolved_at) - julianday(created_at)) * 24 * 60 AS INTEGER)) as avg_minutes_to_resolve
-- FROM map_comments
-- WHERE status = 'resolved' AND resolved_at IS NOT NULL;
