// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Dapper;
using Honua.Server.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Honua.Server.Core.Data.Comments;

/// <summary>
/// SQLite implementation of comment repository
/// </summary>
public class SqliteCommentRepository : ICommentRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteCommentRepository> _logger;

    public SqliteCommentRepository(string connectionString, ILogger<SqliteCommentRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Create map_comments table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS map_comments (
                id TEXT PRIMARY KEY,
                map_id TEXT NOT NULL,
                layer_id TEXT,
                feature_id TEXT,
                author TEXT NOT NULL,
                author_user_id TEXT,
                is_guest INTEGER NOT NULL DEFAULT 0,
                guest_email TEXT,
                comment_text TEXT NOT NULL,
                comment_markdown TEXT,
                geometry_type TEXT NOT NULL DEFAULT 'point',
                geometry TEXT,
                longitude REAL,
                latitude REAL,
                created_at TEXT NOT NULL,
                updated_at TEXT,
                parent_id TEXT,
                thread_depth INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'open',
                resolved_by TEXT,
                resolved_at TEXT,
                category TEXT,
                priority TEXT NOT NULL DEFAULT 'medium',
                color TEXT,
                is_approved INTEGER NOT NULL DEFAULT 1,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                mentioned_users TEXT,
                attachments TEXT,
                ip_address TEXT,
                user_agent TEXT,
                reply_count INTEGER NOT NULL DEFAULT 0,
                like_count INTEGER NOT NULL DEFAULT 0,
                metadata TEXT
            )
        ");

        // Create indexes
        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_map_comments_map_id ON map_comments(map_id);
            CREATE INDEX IF NOT EXISTS idx_map_comments_layer_id ON map_comments(layer_id);
            CREATE INDEX IF NOT EXISTS idx_map_comments_feature_id ON map_comments(feature_id);
            CREATE INDEX IF NOT EXISTS idx_map_comments_author_user_id ON map_comments(author_user_id);
            CREATE INDEX IF NOT EXISTS idx_map_comments_parent_id ON map_comments(parent_id);
            CREATE INDEX IF NOT EXISTS idx_map_comments_status ON map_comments(status);
            CREATE INDEX IF NOT EXISTS idx_map_comments_created_at ON map_comments(created_at);
            CREATE INDEX IF NOT EXISTS idx_map_comments_category ON map_comments(category);
        ");

        // Create comment_reactions table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS map_comment_reactions (
                id TEXT PRIMARY KEY,
                comment_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                reaction_type TEXT NOT NULL DEFAULT 'like',
                created_at TEXT NOT NULL,
                FOREIGN KEY (comment_id) REFERENCES map_comments(id) ON DELETE CASCADE,
                UNIQUE (comment_id, user_id, reaction_type)
            )
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_comment_reactions_comment_id ON map_comment_reactions(comment_id);
            CREATE INDEX IF NOT EXISTS idx_comment_reactions_user_id ON map_comment_reactions(user_id);
        ");

        // Create comment_notifications table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS map_comment_notifications (
                id TEXT PRIMARY KEY,
                comment_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                notification_type TEXT NOT NULL,
                is_read INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                read_at TEXT,
                FOREIGN KEY (comment_id) REFERENCES map_comments(id) ON DELETE CASCADE
            )
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_comment_notifications_user_id ON map_comment_notifications(user_id);
            CREATE INDEX IF NOT EXISTS idx_comment_notifications_is_read ON map_comment_notifications(is_read);
        ");

        _logger.LogInformation("Comment repository schema initialized");
    }

    public async Task<MapComment> CreateCommentAsync(MapComment comment, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO map_comments (
                id, map_id, layer_id, feature_id, author, author_user_id, is_guest, guest_email,
                comment_text, comment_markdown, geometry_type, geometry, longitude, latitude,
                created_at, updated_at, parent_id, thread_depth, status, resolved_by, resolved_at,
                category, priority, color, is_approved, is_deleted, is_pinned,
                mentioned_users, attachments, ip_address, user_agent, reply_count, like_count, metadata
            ) VALUES (
                @Id, @MapId, @LayerId, @FeatureId, @Author, @AuthorUserId, @IsGuest, @GuestEmail,
                @CommentText, @CommentMarkdown, @GeometryType, @Geometry, @Longitude, @Latitude,
                @CreatedAt, @UpdatedAt, @ParentId, @ThreadDepth, @Status, @ResolvedBy, @ResolvedAt,
                @Category, @Priority, @Color, @IsApproved, @IsDeleted, @IsPinned,
                @MentionedUsers, @Attachments, @IpAddress, @UserAgent, @ReplyCount, @LikeCount, @Metadata
            )
        ", new
        {
            comment.Id,
            comment.MapId,
            comment.LayerId,
            comment.FeatureId,
            comment.Author,
            comment.AuthorUserId,
            IsGuest = comment.IsGuest ? 1 : 0,
            comment.GuestEmail,
            comment.CommentText,
            comment.CommentMarkdown,
            comment.GeometryType,
            comment.Geometry,
            comment.Longitude,
            comment.Latitude,
            CreatedAt = comment.CreatedAt.ToString("O"),
            UpdatedAt = comment.UpdatedAt?.ToString("O"),
            comment.ParentId,
            comment.ThreadDepth,
            comment.Status,
            comment.ResolvedBy,
            ResolvedAt = comment.ResolvedAt?.ToString("O"),
            comment.Category,
            comment.Priority,
            comment.Color,
            IsApproved = comment.IsApproved ? 1 : 0,
            IsDeleted = comment.IsDeleted ? 1 : 0,
            IsPinned = comment.IsPinned ? 1 : 0,
            comment.MentionedUsers,
            comment.Attachments,
            comment.IpAddress,
            comment.UserAgent,
            comment.ReplyCount,
            comment.LikeCount,
            comment.Metadata
        });

        // Update parent reply count if this is a reply
        if (!string.IsNullOrEmpty(comment.ParentId))
        {
            await connection.ExecuteAsync(@"
                UPDATE map_comments
                SET reply_count = reply_count + 1
                WHERE id = @ParentId
            ", new { comment.ParentId });
        }

        _logger.LogInformation("Created comment {CommentId} on map {MapId}", comment.Id, comment.MapId);
        return comment;
    }

    public async Task<MapComment?> GetCommentByIdAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var result = await connection.QueryFirstOrDefaultAsync<MapCommentDto>(@"
            SELECT * FROM map_comments WHERE id = @CommentId
        ", new { CommentId = commentId });

        return result != null ? MapToComment(result) : null;
    }

    public async Task<IReadOnlyList<MapComment>> GetCommentsByMapIdAsync(string mapId, CommentFilter? filter = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (whereClause, parameters) = BuildFilterQuery(mapId, filter);
        var orderBy = BuildOrderByClause(filter);
        var limitClause = BuildLimitClause(filter);

        var sql = $@"
            SELECT * FROM map_comments
            WHERE map_id = @MapId {whereClause}
            {orderBy}
            {limitClause}
        ";

        parameters["MapId"] = mapId;

        var results = await connection.QueryAsync<MapCommentDto>(sql, parameters);
        return results.Select(MapToComment).ToList();
    }

    public async Task<IReadOnlyList<MapComment>> GetCommentsByLayerIdAsync(string mapId, string layerId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE map_id = @MapId AND layer_id = @LayerId AND is_deleted = 0
            ORDER BY created_at DESC
        ", new { MapId = mapId, LayerId = layerId });

        return results.Select(MapToComment).ToList();
    }

    public async Task<IReadOnlyList<MapComment>> GetCommentsByFeatureIdAsync(string mapId, string featureId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE map_id = @MapId AND feature_id = @FeatureId AND is_deleted = 0
            ORDER BY created_at DESC
        ", new { MapId = mapId, FeatureId = featureId });

        return results.Select(MapToComment).ToList();
    }

    public async Task<IReadOnlyList<MapComment>> GetCommentsByAuthorAsync(string authorUserId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE author_user_id = @AuthorUserId AND is_deleted = 0
            ORDER BY created_at DESC
        ", new { AuthorUserId = authorUserId });

        return results.Select(MapToComment).ToList();
    }

    public async Task<IReadOnlyList<MapComment>> GetRepliesAsync(string parentCommentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE parent_id = @ParentCommentId AND is_deleted = 0 AND is_approved = 1
            ORDER BY created_at ASC
        ", new { ParentCommentId = parentCommentId });

        return results.Select(MapToComment).ToList();
    }

    public async Task UpdateCommentAsync(MapComment comment, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        comment.UpdatedAt = DateTime.UtcNow;

        await connection.ExecuteAsync(@"
            UPDATE map_comments SET
                comment_text = @CommentText,
                comment_markdown = @CommentMarkdown,
                geometry_type = @GeometryType,
                geometry = @Geometry,
                longitude = @Longitude,
                latitude = @Latitude,
                updated_at = @UpdatedAt,
                status = @Status,
                resolved_by = @ResolvedBy,
                resolved_at = @ResolvedAt,
                category = @Category,
                priority = @Priority,
                color = @Color,
                is_pinned = @IsPinned,
                mentioned_users = @MentionedUsers,
                attachments = @Attachments,
                metadata = @Metadata
            WHERE id = @Id
        ", new
        {
            comment.Id,
            comment.CommentText,
            comment.CommentMarkdown,
            comment.GeometryType,
            comment.Geometry,
            comment.Longitude,
            comment.Latitude,
            UpdatedAt = comment.UpdatedAt?.ToString("O"),
            comment.Status,
            comment.ResolvedBy,
            ResolvedAt = comment.ResolvedAt?.ToString("O"),
            comment.Category,
            comment.Priority,
            comment.Color,
            IsPinned = comment.IsPinned ? 1 : 0,
            comment.MentionedUsers,
            comment.Attachments,
            comment.Metadata
        });

        _logger.LogInformation("Updated comment {CommentId}", comment.Id);
    }

    public async Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Soft delete
        await connection.ExecuteAsync(@"
            UPDATE map_comments
            SET is_deleted = 1, updated_at = @UpdatedAt
            WHERE id = @CommentId
        ", new { CommentId = commentId, UpdatedAt = DateTime.UtcNow.ToString("O") });

        _logger.LogInformation("Deleted comment {CommentId}", commentId);
    }

    public async Task<int> GetCommentCountAsync(string mapId, CommentFilter? filter = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (whereClause, parameters) = BuildFilterQuery(mapId, filter);
        parameters["MapId"] = mapId;

        var sql = $"SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId {whereClause}";

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public async Task UpdateCommentStatusAsync(string commentId, string status, string? resolvedBy = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var resolvedAt = status == CommentStatus.Resolved ? DateTime.UtcNow.ToString("O") : null;

        await connection.ExecuteAsync(@"
            UPDATE map_comments
            SET status = @Status, resolved_by = @ResolvedBy, resolved_at = @ResolvedAt, updated_at = @UpdatedAt
            WHERE id = @CommentId
        ", new
        {
            CommentId = commentId,
            Status = status,
            ResolvedBy = resolvedBy,
            ResolvedAt = resolvedAt,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task<IReadOnlyList<MapComment>> GetCommentsByStatusAsync(string mapId, string status, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE map_id = @MapId AND status = @Status AND is_deleted = 0
            ORDER BY created_at DESC
        ", new { MapId = mapId, Status = status });

        return results.Select(MapToComment).ToList();
    }

    public async Task ApproveCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE map_comments
            SET is_approved = 1, updated_at = @UpdatedAt
            WHERE id = @CommentId
        ", new { CommentId = commentId, UpdatedAt = DateTime.UtcNow.ToString("O") });
    }

    public async Task<IReadOnlyList<MapComment>> GetPendingCommentsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE is_approved = 0 AND is_deleted = 0
            ORDER BY created_at DESC
            LIMIT @Limit
        ", new { Limit = limit });

        return results.Select(MapToComment).ToList();
    }

    public async Task<CommentReaction> AddReactionAsync(CommentReaction reaction, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT OR REPLACE INTO map_comment_reactions (id, comment_id, user_id, reaction_type, created_at)
            VALUES (@Id, @CommentId, @UserId, @ReactionType, @CreatedAt)
        ", new
        {
            reaction.Id,
            reaction.CommentId,
            reaction.UserId,
            reaction.ReactionType,
            CreatedAt = reaction.CreatedAt.ToString("O")
        });

        // Update like count on comment
        await connection.ExecuteAsync(@"
            UPDATE map_comments
            SET like_count = (SELECT COUNT(*) FROM map_comment_reactions WHERE comment_id = @CommentId AND reaction_type = 'like')
            WHERE id = @CommentId
        ", new { reaction.CommentId });

        return reaction;
    }

    public async Task RemoveReactionAsync(string reactionId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get comment ID before deleting
        var commentId = await connection.ExecuteScalarAsync<string>(@"
            SELECT comment_id FROM map_comment_reactions WHERE id = @ReactionId
        ", new { ReactionId = reactionId });

        await connection.ExecuteAsync(@"
            DELETE FROM map_comment_reactions WHERE id = @ReactionId
        ", new { ReactionId = reactionId });

        // Update like count on comment
        if (!string.IsNullOrEmpty(commentId))
        {
            await connection.ExecuteAsync(@"
                UPDATE map_comments
                SET like_count = (SELECT COUNT(*) FROM map_comment_reactions WHERE comment_id = @CommentId AND reaction_type = 'like')
                WHERE id = @CommentId
            ", new { CommentId = commentId });
        }
    }

    public async Task<IReadOnlyList<CommentReaction>> GetReactionsAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<CommentReactionDto>(@"
            SELECT * FROM map_comment_reactions WHERE comment_id = @CommentId
        ", new { CommentId = commentId });

        return results.Select(MapToReaction).ToList();
    }

    public async Task<int> GetReactionCountAsync(string commentId, string reactionType, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comment_reactions
            WHERE comment_id = @CommentId AND reaction_type = @ReactionType
        ", new { CommentId = commentId, ReactionType = reactionType });
    }

    public async Task<CommentNotification> CreateNotificationAsync(CommentNotification notification, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO map_comment_notifications (id, comment_id, user_id, notification_type, is_read, created_at, read_at)
            VALUES (@Id, @CommentId, @UserId, @NotificationType, @IsRead, @CreatedAt, @ReadAt)
        ", new
        {
            notification.Id,
            notification.CommentId,
            notification.UserId,
            notification.NotificationType,
            IsRead = notification.IsRead ? 1 : 0,
            CreatedAt = notification.CreatedAt.ToString("O"),
            ReadAt = notification.ReadAt?.ToString("O")
        });

        return notification;
    }

    public async Task<IReadOnlyList<CommentNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var whereClause = unreadOnly ? "AND is_read = 0" : "";

        var results = await connection.QueryAsync<CommentNotificationDto>($@"
            SELECT * FROM map_comment_notifications
            WHERE user_id = @UserId {whereClause}
            ORDER BY created_at DESC
        ", new { UserId = userId });

        return results.Select(MapToNotification).ToList();
    }

    public async Task MarkNotificationAsReadAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE map_comment_notifications
            SET is_read = 1, read_at = @ReadAt
            WHERE id = @NotificationId
        ", new { NotificationId = notificationId, ReadAt = DateTime.UtcNow.ToString("O") });
    }

    public async Task<int> GetUnreadNotificationCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comment_notifications
            WHERE user_id = @UserId AND is_read = 0
        ", new { UserId = userId });
    }

    public async Task<IReadOnlyList<MapComment>> SearchCommentsAsync(string mapId, string searchTerm, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<MapCommentDto>(@"
            SELECT * FROM map_comments
            WHERE map_id = @MapId
                AND is_deleted = 0
                AND (comment_text LIKE @SearchTerm OR author LIKE @SearchTerm)
            ORDER BY created_at DESC
        ", new { MapId = mapId, SearchTerm = $"%{searchTerm}%" });

        return results.Select(MapToComment).ToList();
    }

    public async Task<CommentAnalytics> GetCommentAnalyticsAsync(string mapId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var totalComments = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId AND is_deleted = 0
        ", new { MapId = mapId });

        var openComments = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId AND status = 'open' AND is_deleted = 0
        ", new { MapId = mapId });

        var resolvedComments = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId AND status = 'resolved' AND is_deleted = 0
        ", new { MapId = mapId });

        var closedComments = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId AND status = 'closed' AND is_deleted = 0
        ", new { MapId = mapId });

        var commentsWithReplies = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM map_comments WHERE map_id = @MapId AND reply_count > 0 AND is_deleted = 0
        ", new { MapId = mapId });

        var totalReplies = await connection.ExecuteScalarAsync<int>(@"
            SELECT SUM(reply_count) FROM map_comments WHERE map_id = @MapId AND is_deleted = 0
        ", new { MapId = mapId });

        return new CommentAnalytics
        {
            TotalComments = totalComments,
            OpenComments = openComments,
            ResolvedComments = resolvedComments,
            ClosedComments = closedComments,
            CommentsWithReplies = commentsWithReplies,
            TotalReplies = totalReplies
        };
    }

    // Private helper methods
    private (string whereClause, Dictionary<string, object?> parameters) BuildFilterQuery(string mapId, CommentFilter? filter)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (filter == null)
        {
            conditions.Add("is_deleted = 0");
            return (conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "", parameters);
        }

        if (!filter.IncludeDeleted)
            conditions.Add("is_deleted = 0");

        if (!filter.IncludeUnapproved)
            conditions.Add("is_approved = 1");

        if (filter.Status != null)
        {
            conditions.Add("status = @Status");
            parameters["Status"] = filter.Status;
        }

        if (filter.Category != null)
        {
            conditions.Add("category = @Category");
            parameters["Category"] = filter.Category;
        }

        if (filter.Priority != null)
        {
            conditions.Add("priority = @Priority");
            parameters["Priority"] = filter.Priority;
        }

        if (filter.AuthorUserId != null)
        {
            conditions.Add("author_user_id = @AuthorUserId");
            parameters["AuthorUserId"] = filter.AuthorUserId;
        }

        if (filter.StartDate.HasValue)
        {
            conditions.Add("created_at >= @StartDate");
            parameters["StartDate"] = filter.StartDate.Value.ToString("O");
        }

        if (filter.EndDate.HasValue)
        {
            conditions.Add("created_at <= @EndDate");
            parameters["EndDate"] = filter.EndDate.Value.ToString("O");
        }

        if (filter.RootCommentsOnly)
        {
            conditions.Add("parent_id IS NULL");
        }

        return (conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "", parameters);
    }

    private string BuildOrderByClause(CommentFilter? filter)
    {
        if (filter?.SortBy == null)
            return "ORDER BY created_at DESC";

        var sortOrder = filter.SortOrder?.ToLower() == "asc" ? "ASC" : "DESC";
        return $"ORDER BY {filter.SortBy} {sortOrder}";
    }

    private string BuildLimitClause(CommentFilter? filter)
    {
        if (filter?.Limit == null)
            return "";

        var offset = filter.Offset ?? 0;
        return $"LIMIT {filter.Limit} OFFSET {offset}";
    }

    private MapComment MapToComment(MapCommentDto dto)
    {
        return new MapComment
        {
            Id = dto.id,
            MapId = dto.map_id,
            LayerId = dto.layer_id,
            FeatureId = dto.feature_id,
            Author = dto.author,
            AuthorUserId = dto.author_user_id,
            IsGuest = dto.is_guest == 1,
            GuestEmail = dto.guest_email,
            CommentText = dto.comment_text,
            CommentMarkdown = dto.comment_markdown,
            GeometryType = dto.geometry_type,
            Geometry = dto.geometry,
            Longitude = dto.longitude,
            Latitude = dto.latitude,
            CreatedAt = DateTime.Parse(dto.created_at),
            UpdatedAt = dto.updated_at != null ? DateTime.Parse(dto.updated_at) : null,
            ParentId = dto.parent_id,
            ThreadDepth = dto.thread_depth,
            Status = dto.status,
            ResolvedBy = dto.resolved_by,
            ResolvedAt = dto.resolved_at != null ? DateTime.Parse(dto.resolved_at) : null,
            Category = dto.category,
            Priority = dto.priority,
            Color = dto.color,
            IsApproved = dto.is_approved == 1,
            IsDeleted = dto.is_deleted == 1,
            IsPinned = dto.is_pinned == 1,
            MentionedUsers = dto.mentioned_users,
            Attachments = dto.attachments,
            IpAddress = dto.ip_address,
            UserAgent = dto.user_agent,
            ReplyCount = dto.reply_count,
            LikeCount = dto.like_count,
            Metadata = dto.metadata
        };
    }

    private CommentReaction MapToReaction(CommentReactionDto dto)
    {
        return new CommentReaction
        {
            Id = dto.id,
            CommentId = dto.comment_id,
            UserId = dto.user_id,
            ReactionType = dto.reaction_type,
            CreatedAt = DateTime.Parse(dto.created_at)
        };
    }

    private CommentNotification MapToNotification(CommentNotificationDto dto)
    {
        return new CommentNotification
        {
            Id = dto.id,
            CommentId = dto.comment_id,
            UserId = dto.user_id,
            NotificationType = dto.notification_type,
            IsRead = dto.is_read == 1,
            CreatedAt = DateTime.Parse(dto.created_at),
            ReadAt = dto.read_at != null ? DateTime.Parse(dto.read_at) : null
        };
    }

    // DTOs for Dapper mapping
#pragma warning disable IDE1006 // Naming Styles
    private record MapCommentDto(
        string id, string map_id, string? layer_id, string? feature_id,
        string author, string? author_user_id, int is_guest, string? guest_email,
        string comment_text, string? comment_markdown,
        string geometry_type, string? geometry, double? longitude, double? latitude,
        string created_at, string? updated_at,
        string? parent_id, int thread_depth,
        string status, string? resolved_by, string? resolved_at,
        string? category, string priority, string? color,
        int is_approved, int is_deleted, int is_pinned,
        string? mentioned_users, string? attachments,
        string? ip_address, string? user_agent,
        int reply_count, int like_count, string? metadata
    );

    private record CommentReactionDto(
        string id, string comment_id, string user_id, string reaction_type, string created_at
    );

    private record CommentNotificationDto(
        string id, string comment_id, string user_id, string notification_type,
        int is_read, string created_at, string? read_at
    );
#pragma warning restore IDE1006 // Naming Styles
}
