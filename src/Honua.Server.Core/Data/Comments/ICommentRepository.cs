// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models;

namespace Honua.Server.Core.Data.Comments;

/// <summary>
/// Repository interface for map comment operations
/// </summary>
public interface ICommentRepository
{
    // Comment CRUD operations
    Task<MapComment> CreateCommentAsync(MapComment comment, CancellationToken cancellationToken = default);
    Task<MapComment?> GetCommentByIdAsync(string commentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetCommentsByMapIdAsync(string mapId, CommentFilter? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetCommentsByLayerIdAsync(string mapId, string layerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetCommentsByFeatureIdAsync(string mapId, string featureId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetCommentsByAuthorAsync(string authorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetRepliesAsync(string parentCommentId, CancellationToken cancellationToken = default);
    Task UpdateCommentAsync(MapComment comment, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default);
    Task<int> GetCommentCountAsync(string mapId, CommentFilter? filter = null, CancellationToken cancellationToken = default);

    // Status operations
    Task UpdateCommentStatusAsync(string commentId, string status, string? resolvedBy = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetCommentsByStatusAsync(string mapId, string status, CancellationToken cancellationToken = default);

    // Moderation operations
    Task ApproveCommentAsync(string commentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MapComment>> GetPendingCommentsAsync(int limit = 100, CancellationToken cancellationToken = default);

    // Reaction operations
    Task<CommentReaction> AddReactionAsync(CommentReaction reaction, CancellationToken cancellationToken = default);
    Task RemoveReactionAsync(string reactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentReaction>> GetReactionsAsync(string commentId, CancellationToken cancellationToken = default);
    Task<int> GetReactionCountAsync(string commentId, string reactionType, CancellationToken cancellationToken = default);

    // Notification operations
    Task<CommentNotification> CreateNotificationAsync(CommentNotification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentNotification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task MarkNotificationAsReadAsync(string notificationId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadNotificationCountAsync(string userId, CancellationToken cancellationToken = default);

    // Search operations
    Task<IReadOnlyList<MapComment>> SearchCommentsAsync(string mapId, string searchTerm, CancellationToken cancellationToken = default);

    // Analytics operations
    Task<CommentAnalytics> GetCommentAnalyticsAsync(string mapId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter options for querying comments
/// </summary>
public class CommentFilter
{
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? AuthorUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IncludeDeleted { get; set; } = false;
    public bool IncludeUnapproved { get; set; } = false;
    public bool RootCommentsOnly { get; set; } = false;
    public int? Limit { get; set; }
    public int? Offset { get; set; }
    public string? SortBy { get; set; } = "created_at";
    public string? SortOrder { get; set; } = "desc";
}

/// <summary>
/// Comment analytics data
/// </summary>
public class CommentAnalytics
{
    public int TotalComments { get; set; }
    public int OpenComments { get; set; }
    public int ResolvedComments { get; set; }
    public int ClosedComments { get; set; }
    public Dictionary<string, int> CommentsByCategory { get; set; } = new();
    public Dictionary<string, int> CommentsByPriority { get; set; } = new();
    public Dictionary<string, int> CommentsByAuthor { get; set; } = new();
    public double AverageResponseTime { get; set; }
    public int CommentsWithReplies { get; set; }
    public int TotalReplies { get; set; }
}
