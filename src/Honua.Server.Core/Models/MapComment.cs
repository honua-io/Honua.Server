// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Honua.Server.Core.Models;

/// <summary>
/// Represents a visual comment on a map with spatial context
/// Supports point, line, and polygon-based annotations
/// </summary>
[Table("map_comments")]
public class MapComment
{
    /// <summary>
    /// Unique comment ID
    /// </summary>
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Map ID this comment belongs to
    /// </summary>
    [Required]
    [Column("map_id")]
    [MaxLength(100)]
    public string MapId { get; set; } = string.Empty;

    /// <summary>
    /// Layer ID if comment is attached to a specific layer
    /// </summary>
    [Column("layer_id")]
    [MaxLength(100)]
    public string? LayerId { get; set; }

    /// <summary>
    /// Feature ID if comment is attached to a specific feature
    /// </summary>
    [Column("feature_id")]
    [MaxLength(200)]
    public string? FeatureId { get; set; }

    /// <summary>
    /// Comment author (user ID or name)
    /// </summary>
    [Required]
    [Column("author")]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Author user ID for authenticated users
    /// </summary>
    [Column("author_user_id")]
    [MaxLength(100)]
    public string? AuthorUserId { get; set; }

    /// <summary>
    /// Whether this is a guest comment
    /// </summary>
    [Column("is_guest")]
    public bool IsGuest { get; set; } = false;

    /// <summary>
    /// Guest email (for notifications)
    /// </summary>
    [Column("guest_email")]
    [MaxLength(200)]
    public string? GuestEmail { get; set; }

    /// <summary>
    /// Comment text content
    /// </summary>
    [Required]
    [Column("comment_text")]
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Markdown formatted comment (if different from CommentText)
    /// </summary>
    [Column("comment_markdown")]
    public string? CommentMarkdown { get; set; }

    /// <summary>
    /// Geometry type: point, line, polygon, none
    /// </summary>
    [Required]
    [Column("geometry_type")]
    [MaxLength(20)]
    public string GeometryType { get; set; } = "point";

    /// <summary>
    /// GeoJSON geometry for spatial comments
    /// Supports Point, LineString, Polygon
    /// </summary>
    [Column("geometry")]
    public string? Geometry { get; set; }

    /// <summary>
    /// Longitude (for point comments, quick access)
    /// </summary>
    [Column("longitude")]
    public double? Longitude { get; set; }

    /// <summary>
    /// Latitude (for point comments, quick access)
    /// </summary>
    [Column("latitude")]
    public double? Latitude { get; set; }

    /// <summary>
    /// When the comment was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the comment was last updated
    /// </summary>
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Parent comment ID for threaded discussions
    /// </summary>
    [Column("parent_id")]
    [MaxLength(100)]
    public string? ParentId { get; set; }

    /// <summary>
    /// Thread depth (0 for root comments)
    /// </summary>
    [Column("thread_depth")]
    public int ThreadDepth { get; set; } = 0;

    /// <summary>
    /// Comment status: open, resolved, closed
    /// </summary>
    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = CommentStatus.Open;

    /// <summary>
    /// Who resolved the comment
    /// </summary>
    [Column("resolved_by")]
    [MaxLength(200)]
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// When the comment was resolved
    /// </summary>
    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Comment category/tag
    /// </summary>
    [Column("category")]
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// Priority level: low, medium, high, critical
    /// </summary>
    [Column("priority")]
    [MaxLength(20)]
    public string Priority { get; set; } = CommentPriority.Medium;

    /// <summary>
    /// Color for comment marker
    /// </summary>
    [Column("color")]
    [MaxLength(20)]
    public string? Color { get; set; }

    /// <summary>
    /// Whether the comment has been approved (moderation)
    /// </summary>
    [Column("is_approved")]
    public bool IsApproved { get; set; } = true;

    /// <summary>
    /// Whether the comment has been deleted (soft delete)
    /// </summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Whether the comment is pinned (sticky)
    /// </summary>
    [Column("is_pinned")]
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Mentioned user IDs (for notifications)
    /// JSON array of user IDs
    /// </summary>
    [Column("mentioned_users")]
    public string? MentionedUsers { get; set; }

    /// <summary>
    /// Attached file URLs (images, documents)
    /// JSON array of attachment objects
    /// </summary>
    [Column("attachments")]
    public string? Attachments { get; set; }

    /// <summary>
    /// IP address for spam prevention
    /// </summary>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent for spam prevention
    /// </summary>
    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Number of replies to this comment
    /// </summary>
    [Column("reply_count")]
    public int ReplyCount { get; set; } = 0;

    /// <summary>
    /// Like/upvote count
    /// </summary>
    [Column("like_count")]
    public int LikeCount { get; set; } = 0;

    /// <summary>
    /// Custom metadata (JSON)
    /// </summary>
    [Column("metadata")]
    public string? Metadata { get; set; }
}

/// <summary>
/// Comment status constants
/// </summary>
public static class CommentStatus
{
    public const string Open = "open";
    public const string Resolved = "resolved";
    public const string Closed = "closed";

    public static bool IsValid(string status) =>
        status == Open || status == Resolved || status == Closed;
}

/// <summary>
/// Comment priority constants
/// </summary>
public static class CommentPriority
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";

    public static bool IsValid(string priority) =>
        priority == Low || priority == Medium || priority == High || priority == Critical;
}

/// <summary>
/// Comment geometry type constants
/// </summary>
public static class CommentGeometryType
{
    public const string None = "none";
    public const string Point = "point";
    public const string Line = "line";
    public const string Polygon = "polygon";

    public static bool IsValid(string type) =>
        type == None || type == Point || type == Line || type == Polygon;
}

/// <summary>
/// Represents a comment attachment
/// </summary>
public class CommentAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a comment reaction/like
/// </summary>
[Table("map_comment_reactions")]
public class CommentReaction
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("comment_id")]
    [MaxLength(100)]
    public string CommentId { get; set; } = string.Empty;

    [Required]
    [Column("user_id")]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("reaction_type")]
    [MaxLength(20)]
    public string ReactionType { get; set; } = "like";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a comment notification
/// </summary>
[Table("map_comment_notifications")]
public class CommentNotification
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("comment_id")]
    [MaxLength(100)]
    public string CommentId { get; set; } = string.Empty;

    [Required]
    [Column("user_id")]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("notification_type")]
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }
}
