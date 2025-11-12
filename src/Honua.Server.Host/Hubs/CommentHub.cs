// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.Hubs;

/// <summary>
/// SignalR hub for real-time map comment collaboration
/// Supports live comment updates, presence indicators, and notifications
/// </summary>
[Authorize]
public sealed class CommentHub : Hub
{
    private readonly ILogger<CommentHub> _logger;
    private static readonly Dictionary<string, HashSet<string>> _mapViewers = new();
    private static readonly object _viewersLock = new();

    public CommentHub(ILogger<CommentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
        _logger.LogInformation("Comment hub client connected: {ConnectionId}, User: {UserId}",
            Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;

        // Remove from all map viewer groups
        lock (_viewersLock)
        {
            foreach (var (mapId, viewers) in _mapViewers.ToList())
            {
                if (viewers.Remove(Context.ConnectionId))
                {
                    // Notify others that this user left
                    _ = Clients.Group($"map_{mapId}").SendAsync("UserLeftMap", new
                    {
                        UserId = userId,
                        MapId = mapId,
                        Timestamp = DateTime.UtcNow
                    });
                }

                if (viewers.Count == 0)
                {
                    _mapViewers.Remove(mapId);
                }
            }
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "Comment hub client disconnected with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Comment hub client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins a map's comment group for receiving updates
    /// </summary>
    /// <param name="mapId">The map ID to join</param>
    public async Task JoinMap(string mapId)
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        var groupName = $"map_{mapId}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        lock (_viewersLock)
        {
            if (!_mapViewers.ContainsKey(mapId))
            {
                _mapViewers[mapId] = new HashSet<string>();
            }
            _mapViewers[mapId].Add(Context.ConnectionId);
        }

        _logger.LogInformation("User {UserId} joined map {MapId} comment group", userId, mapId);

        // Notify others in the group
        await Clients.OthersInGroup(groupName).SendAsync("UserJoinedMap", new
        {
            UserId = userId,
            MapId = mapId,
            ConnectionId = Context.ConnectionId,
            Timestamp = DateTime.UtcNow
        });

        // Send current viewer count
        var viewerCount = GetMapViewerCount(mapId);
        await Clients.Caller.SendAsync("ViewerCountUpdated", new
        {
            MapId = mapId,
            Count = viewerCount
        });
    }

    /// <summary>
    /// Leaves a map's comment group
    /// </summary>
    /// <param name="mapId">The map ID to leave</param>
    public async Task LeaveMap(string mapId)
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        var groupName = $"map_{mapId}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        lock (_viewersLock)
        {
            if (_mapViewers.TryGetValue(mapId, out var viewers))
            {
                viewers.Remove(Context.ConnectionId);
                if (viewers.Count == 0)
                {
                    _mapViewers.Remove(mapId);
                }
            }
        }

        _logger.LogInformation("User {UserId} left map {MapId} comment group", userId, mapId);

        // Notify others in the group
        await Clients.OthersInGroup(groupName).SendAsync("UserLeftMap", new
        {
            UserId = userId,
            MapId = mapId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gets the current viewer count for a map
    /// </summary>
    public Task<int> GetViewerCount(string mapId)
    {
        return Task.FromResult(GetMapViewerCount(mapId));
    }

    /// <summary>
    /// Updates typing indicator for a comment thread
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment being replied to (null for new comment)</param>
    /// <param name="isTyping">Whether the user is typing</param>
    public async Task UpdateTypingIndicator(string mapId, string? commentId, bool isTyping)
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        var groupName = $"map_{mapId}";

        await Clients.OthersInGroup(groupName).SendAsync("TypingIndicatorChanged", new
        {
            UserId = userId,
            MapId = mapId,
            CommentId = commentId,
            IsTyping = isTyping,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a new comment to all viewers
    /// </summary>
    /// <param name="comment">The new comment</param>
    public async Task BroadcastNewComment(MapComment comment)
    {
        var groupName = $"map_{comment.MapId}";

        _logger.LogInformation("Broadcasting new comment {CommentId} to map {MapId}",
            comment.Id, comment.MapId);

        await Clients.Group(groupName).SendAsync("CommentCreated", comment);
    }

    /// <summary>
    /// Broadcasts a comment update to all viewers
    /// </summary>
    /// <param name="comment">The updated comment</param>
    public async Task BroadcastCommentUpdate(MapComment comment)
    {
        var groupName = $"map_{comment.MapId}";

        _logger.LogInformation("Broadcasting comment update {CommentId} to map {MapId}",
            comment.Id, comment.MapId);

        await Clients.Group(groupName).SendAsync("CommentUpdated", comment);
    }

    /// <summary>
    /// Broadcasts a comment deletion to all viewers
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The deleted comment ID</param>
    public async Task BroadcastCommentDelete(string mapId, string commentId)
    {
        var groupName = $"map_{mapId}";

        _logger.LogInformation("Broadcasting comment deletion {CommentId} from map {MapId}",
            commentId, mapId);

        await Clients.Group(groupName).SendAsync("CommentDeleted", new
        {
            MapId = mapId,
            CommentId = commentId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a comment status change to all viewers
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <param name="status">The new status</param>
    /// <param name="resolvedBy">Who resolved it</param>
    public async Task BroadcastStatusChange(string mapId, string commentId, string status, string? resolvedBy)
    {
        var groupName = $"map_{mapId}";

        await Clients.Group(groupName).SendAsync("CommentStatusChanged", new
        {
            MapId = mapId,
            CommentId = commentId,
            Status = status,
            ResolvedBy = resolvedBy,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a reaction to all viewers
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <param name="userId">Who reacted</param>
    /// <param name="reactionType">Type of reaction</param>
    public async Task BroadcastReaction(string mapId, string commentId, string userId, string reactionType)
    {
        var groupName = $"map_{mapId}";

        await Clients.Group(groupName).SendAsync("CommentReactionAdded", new
        {
            MapId = mapId,
            CommentId = commentId,
            UserId = userId,
            ReactionType = reactionType,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sends a notification to specific users
    /// </summary>
    /// <param name="userIds">User IDs to notify</param>
    /// <param name="notification">Notification data</param>
    public async Task SendNotificationToUsers(string[] userIds, object notification)
    {
        // In a real implementation, you'd track user connections and send to their specific connections
        // For now, we'll send to all connections (they can filter client-side)
        await Clients.All.SendAsync("CommentNotification", notification);
    }

    /// <summary>
    /// Pings the hub to check connectivity
    /// </summary>
    public Task<string> Ping()
    {
        return Task.FromResult("pong");
    }

    // Private helper methods
    private int GetMapViewerCount(string mapId)
    {
        lock (_viewersLock)
        {
            return _mapViewers.TryGetValue(mapId, out var viewers) ? viewers.Count : 0;
        }
    }
}

/// <summary>
/// Comment hub event types for client-side handling
/// </summary>
public static class CommentHubEvents
{
    public const string CommentCreated = "CommentCreated";
    public const string CommentUpdated = "CommentUpdated";
    public const string CommentDeleted = "CommentDeleted";
    public const string CommentStatusChanged = "CommentStatusChanged";
    public const string CommentReactionAdded = "CommentReactionAdded";
    public const string UserJoinedMap = "UserJoinedMap";
    public const string UserLeftMap = "UserLeftMap";
    public const string ViewerCountUpdated = "ViewerCountUpdated";
    public const string TypingIndicatorChanged = "TypingIndicatorChanged";
    public const string CommentNotification = "CommentNotification";
}
