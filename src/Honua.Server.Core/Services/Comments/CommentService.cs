// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data.Comments;
using Honua.Server.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Service for managing map comments with real-time collaboration features
/// </summary>
public class CommentService
{
    private readonly ICommentRepository _repository;
    private readonly ILogger<CommentService> _logger;

    public CommentService(ICommentRepository repository, ILogger<CommentService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new comment
    /// </summary>
    public async Task<MapComment> CreateCommentAsync(
        string mapId,
        string author,
        string commentText,
        string? authorUserId = null,
        string? layerId = null,
        string? featureId = null,
        string geometryType = CommentGeometryType.Point,
        string? geometry = null,
        double? longitude = null,
        double? latitude = null,
        string? parentId = null,
        string? category = null,
        string priority = CommentPriority.Medium,
        string? color = null,
        bool isGuest = false,
        string? guestEmail = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!CommentGeometryType.IsValid(geometryType))
            throw new ArgumentException($"Invalid geometry type: {geometryType}", nameof(geometryType));

        if (!CommentPriority.IsValid(priority))
            throw new ArgumentException($"Invalid priority: {priority}", nameof(priority));

        // Calculate thread depth
        int threadDepth = 0;
        if (!string.IsNullOrEmpty(parentId))
        {
            var parent = await _repository.GetCommentByIdAsync(parentId, cancellationToken);
            if (parent != null)
            {
                threadDepth = parent.ThreadDepth + 1;
            }
        }

        // Extract @mentions from comment text
        var mentionedUsers = ExtractMentions(commentText);

        var comment = new MapComment
        {
            Id = Guid.NewGuid().ToString(),
            MapId = mapId,
            LayerId = layerId,
            FeatureId = featureId,
            Author = author,
            AuthorUserId = authorUserId,
            IsGuest = isGuest,
            GuestEmail = guestEmail,
            CommentText = commentText,
            GeometryType = geometryType,
            Geometry = geometry,
            Longitude = longitude,
            Latitude = latitude,
            CreatedAt = DateTime.UtcNow,
            ParentId = parentId,
            ThreadDepth = threadDepth,
            Status = CommentStatus.Open,
            Category = category,
            Priority = priority,
            Color = color,
            IsApproved = !isGuest, // Auto-approve authenticated users
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MentionedUsers = mentionedUsers.Count > 0 ? JsonSerializer.Serialize(mentionedUsers) : null
        };

        await _repository.CreateCommentAsync(comment, cancellationToken);

        _logger.LogInformation("Created comment {CommentId} on map {MapId} by {Author}",
            comment.Id, mapId, author);

        return comment;
    }

    /// <summary>
    /// Gets a comment by ID
    /// </summary>
    public async Task<MapComment?> GetCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentByIdAsync(commentId, cancellationToken);
    }

    /// <summary>
    /// Gets all comments for a map with optional filtering
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> GetMapCommentsAsync(
        string mapId,
        CommentFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentsByMapIdAsync(mapId, filter, cancellationToken);
    }

    /// <summary>
    /// Gets comments for a specific layer
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> GetLayerCommentsAsync(
        string mapId,
        string layerId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentsByLayerIdAsync(mapId, layerId, cancellationToken);
    }

    /// <summary>
    /// Gets comments for a specific feature
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> GetFeatureCommentsAsync(
        string mapId,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentsByFeatureIdAsync(mapId, featureId, cancellationToken);
    }

    /// <summary>
    /// Gets replies to a comment (threaded discussion)
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> GetRepliesAsync(
        string commentId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetRepliesAsync(commentId, cancellationToken);
    }

    /// <summary>
    /// Updates an existing comment
    /// </summary>
    public async Task<MapComment> UpdateCommentAsync(
        MapComment comment,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCommentByIdAsync(comment.Id, cancellationToken);
        if (existing == null)
            throw new InvalidOperationException($"Comment {comment.Id} not found");

        // Extract new mentions
        var mentionedUsers = ExtractMentions(comment.CommentText);
        comment.MentionedUsers = mentionedUsers.Count > 0 ? JsonSerializer.Serialize(mentionedUsers) : null;

        await _repository.UpdateCommentAsync(comment, cancellationToken);

        _logger.LogInformation("Updated comment {CommentId}", comment.Id);

        return comment;
    }

    /// <summary>
    /// Deletes a comment (soft delete)
    /// </summary>
    public async Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteCommentAsync(commentId, cancellationToken);
        _logger.LogInformation("Deleted comment {CommentId}", commentId);
    }

    /// <summary>
    /// Resolves a comment
    /// </summary>
    public async Task ResolveCommentAsync(
        string commentId,
        string resolvedBy,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateCommentStatusAsync(commentId, CommentStatus.Resolved, resolvedBy, cancellationToken);
        _logger.LogInformation("Resolved comment {CommentId} by {ResolvedBy}", commentId, resolvedBy);
    }

    /// <summary>
    /// Reopens a resolved comment
    /// </summary>
    public async Task ReopenCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateCommentStatusAsync(commentId, CommentStatus.Open, null, cancellationToken);
        _logger.LogInformation("Reopened comment {CommentId}", commentId);
    }

    /// <summary>
    /// Closes a comment
    /// </summary>
    public async Task CloseCommentAsync(
        string commentId,
        string closedBy,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateCommentStatusAsync(commentId, CommentStatus.Closed, closedBy, cancellationToken);
        _logger.LogInformation("Closed comment {CommentId} by {ClosedBy}", commentId, closedBy);
    }

    /// <summary>
    /// Adds a reaction to a comment
    /// </summary>
    public async Task<CommentReaction> AddReactionAsync(
        string commentId,
        string userId,
        string reactionType = "like",
        CancellationToken cancellationToken = default)
    {
        var reaction = new CommentReaction
        {
            Id = Guid.NewGuid().ToString(),
            CommentId = commentId,
            UserId = userId,
            ReactionType = reactionType,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddReactionAsync(reaction, cancellationToken);

        _logger.LogInformation("User {UserId} reacted '{ReactionType}' to comment {CommentId}",
            userId, reactionType, commentId);

        return reaction;
    }

    /// <summary>
    /// Removes a reaction from a comment
    /// </summary>
    public async Task RemoveReactionAsync(string reactionId, CancellationToken cancellationToken = default)
    {
        await _repository.RemoveReactionAsync(reactionId, cancellationToken);
    }

    /// <summary>
    /// Gets reactions for a comment
    /// </summary>
    public async Task<IReadOnlyList<CommentReaction>> GetReactionsAsync(
        string commentId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetReactionsAsync(commentId, cancellationToken);
    }

    /// <summary>
    /// Approves a pending comment (moderation)
    /// </summary>
    public async Task ApproveCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        await _repository.ApproveCommentAsync(commentId, cancellationToken);
        _logger.LogInformation("Approved comment {CommentId}", commentId);
    }

    /// <summary>
    /// Gets pending comments awaiting moderation
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> GetPendingCommentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetPendingCommentsAsync(limit, cancellationToken);
    }

    /// <summary>
    /// Searches comments
    /// </summary>
    public async Task<IReadOnlyList<MapComment>> SearchCommentsAsync(
        string mapId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        return await _repository.SearchCommentsAsync(mapId, searchTerm, cancellationToken);
    }

    /// <summary>
    /// Gets comment analytics for a map
    /// </summary>
    public async Task<CommentAnalytics> GetAnalyticsAsync(
        string mapId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentAnalyticsAsync(mapId, cancellationToken);
    }

    /// <summary>
    /// Creates a notification for a user
    /// </summary>
    public async Task<CommentNotification> CreateNotificationAsync(
        string commentId,
        string userId,
        string notificationType,
        CancellationToken cancellationToken = default)
    {
        var notification = new CommentNotification
        {
            Id = Guid.NewGuid().ToString(),
            CommentId = commentId,
            UserId = userId,
            NotificationType = notificationType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateNotificationAsync(notification, cancellationToken);

        _logger.LogInformation("Created notification for user {UserId} about comment {CommentId}",
            userId, commentId);

        return notification;
    }

    /// <summary>
    /// Gets notifications for a user
    /// </summary>
    public async Task<IReadOnlyList<CommentNotification>> GetUserNotificationsAsync(
        string userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetUserNotificationsAsync(userId, unreadOnly, cancellationToken);
    }

    /// <summary>
    /// Marks a notification as read
    /// </summary>
    public async Task MarkNotificationAsReadAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        await _repository.MarkNotificationAsReadAsync(notificationId, cancellationToken);
    }

    /// <summary>
    /// Gets unread notification count for a user
    /// </summary>
    public async Task<int> GetUnreadNotificationCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetUnreadNotificationCountAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Attaches a file to a comment
    /// </summary>
    public async Task<MapComment> AddAttachmentAsync(
        string commentId,
        CommentAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        var comment = await _repository.GetCommentByIdAsync(commentId, cancellationToken);
        if (comment == null)
            throw new InvalidOperationException($"Comment {commentId} not found");

        var attachments = string.IsNullOrEmpty(comment.Attachments)
            ? new List<CommentAttachment>()
            : JsonSerializer.Deserialize<List<CommentAttachment>>(comment.Attachments) ?? new List<CommentAttachment>();

        attachments.Add(attachment);
        comment.Attachments = JsonSerializer.Serialize(attachments);

        await _repository.UpdateCommentAsync(comment, cancellationToken);

        _logger.LogInformation("Added attachment {AttachmentId} to comment {CommentId}",
            attachment.Id, commentId);

        return comment;
    }

    /// <summary>
    /// Exports comments to CSV format
    /// </summary>
    public string ExportToCSV(IEnumerable<MapComment> comments)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID,Author,CreatedAt,Status,Priority,Category,CommentText,Longitude,Latitude");

        foreach (var comment in comments)
        {
            sb.AppendLine($"\"{comment.Id}\",\"{comment.Author}\",\"{comment.CreatedAt:O}\"," +
                $"\"{comment.Status}\",\"{comment.Priority}\",\"{comment.Category ?? ""}\"," +
                $"\"{EscapeCSV(comment.CommentText)}\",{comment.Longitude},{comment.Latitude}");
        }

        return sb.ToString();
    }

    // Private helper methods

    /// <summary>
    /// Extracts @mentions from comment text
    /// Returns list of mentioned usernames/IDs
    /// </summary>
    private List<string> ExtractMentions(string text)
    {
        var mentions = new List<string>();
        var regex = new Regex(@"@([a-zA-Z0-9_]+)");
        var matches = regex.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                mentions.Add(match.Groups[1].Value);
            }
        }

        return mentions.Distinct().ToList();
    }

    private string EscapeCSV(string text)
    {
        if (text == null) return "";
        return text.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
    }
}

/// <summary>
/// Notification types for comment events
/// </summary>
public static class CommentNotificationType
{
    public const string Mentioned = "mentioned";
    public const string Reply = "reply";
    public const string Resolved = "resolved";
    public const string Reopened = "reopened";
    public const string Reaction = "reaction";
    public const string CommentOnYourFeature = "comment_on_your_feature";
}
