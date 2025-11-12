// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Honua.Server.Core.Services.Comments;
using Honua.Server.Core.Models;
using Honua.Server.Core.Data.Comments;
using Honua.Server.Host.Hubs;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for managing map comments with real-time collaboration
/// Supports visual annotations, threaded discussions, and spatial comments
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/maps/{mapId}/comments")]
[Produces("application/json")]
public class CommentsController : ControllerBase
{
    private readonly ILogger<CommentsController> _logger;
    private readonly CommentService _commentService;
    private readonly IHubContext<CommentHub> _commentHub;

    public CommentsController(
        ILogger<CommentsController> logger,
        CommentService commentService,
        IHubContext<CommentHub> commentHub)
    {
        _logger = logger;
        _commentService = commentService;
        _commentHub = commentHub;
    }

    // ==================== Comment CRUD Operations ====================

    /// <summary>
    /// Creates a new comment on the map
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="request">Comment creation data</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid request</response>
    [HttpPost]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(MapCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MapCommentResponse>> CreateComment(
        [FromRoute] string mapId,
        [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var comment = await _commentService.CreateCommentAsync(
                mapId,
                request.Author ?? userId,
                request.CommentText,
                userId,
                request.LayerId,
                request.FeatureId,
                request.GeometryType ?? CommentGeometryType.Point,
                request.Geometry,
                request.Longitude,
                request.Latitude,
                request.ParentId,
                request.Category,
                request.Priority ?? CommentPriority.Medium,
                request.Color,
                false,
                null,
                ipAddress,
                userAgent);

            // Broadcast to SignalR clients
            await _commentHub.Clients.Group($"map_{mapId}")
                .SendAsync(CommentHubEvents.CommentCreated, comment);

            // Create notifications for mentioned users
            if (!string.IsNullOrEmpty(comment.MentionedUsers))
            {
                var mentions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(comment.MentionedUsers);
                if (mentions != null)
                {
                    foreach (var mentionedUser in mentions)
                    {
                        await _commentService.CreateNotificationAsync(
                            comment.Id,
                            mentionedUser,
                            CommentNotificationType.Mentioned);
                    }
                }
            }

            // Create notification for parent comment author (reply)
            if (!string.IsNullOrEmpty(comment.ParentId))
            {
                var parentComment = await _commentService.GetCommentAsync(comment.ParentId);
                if (parentComment != null && parentComment.AuthorUserId != userId)
                {
                    await _commentService.CreateNotificationAsync(
                        comment.Id,
                        parentComment.AuthorUserId!,
                        CommentNotificationType.Reply);
                }
            }

            var response = MapToResponse(comment);

            _logger.LogInformation("Created comment {CommentId} on map {MapId} by {UserId}",
                comment.Id, mapId, userId);

            return CreatedAtAction(nameof(GetComment), new { mapId, commentId = comment.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific comment by ID
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">Comment found</response>
    /// <response code="404">Comment not found</response>
    [HttpGet("{commentId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MapCommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapCommentResponse>> GetComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var comment = await _commentService.GetCommentAsync(commentId);

        if (comment == null || comment.MapId != mapId)
        {
            return NotFound(new { error = "Comment not found" });
        }

        return Ok(MapToResponse(comment));
    }

    /// <summary>
    /// Gets all comments for a map with optional filtering
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="status">Filter by status (open, resolved, closed)</param>
    /// <param name="category">Filter by category</param>
    /// <param name="priority">Filter by priority</param>
    /// <param name="authorUserId">Filter by author</param>
    /// <param name="rootOnly">Only return root comments (not replies)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="offset">Offset for pagination</param>
    /// <response code="200">List of comments</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> GetComments(
        [FromRoute] string mapId,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? authorUserId = null,
        [FromQuery] bool rootOnly = false,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null)
    {
        var filter = new CommentFilter
        {
            Status = status,
            Category = category,
            Priority = priority,
            AuthorUserId = authorUserId,
            RootCommentsOnly = rootOnly,
            Limit = limit,
            Offset = offset,
            IncludeUnapproved = User.Identity?.IsAuthenticated ?? false
        };

        var comments = await _commentService.GetMapCommentsAsync(mapId, filter);
        var responses = comments.Select(MapToResponse).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Gets comments for a specific layer
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="layerId">The layer ID</param>
    /// <response code="200">List of comments</response>
    [HttpGet("layer/{layerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> GetLayerComments(
        [FromRoute] string mapId,
        [FromRoute] string layerId)
    {
        var comments = await _commentService.GetLayerCommentsAsync(mapId, layerId);
        var responses = comments.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    /// <summary>
    /// Gets comments for a specific feature
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="featureId">The feature ID</param>
    /// <response code="200">List of comments</response>
    [HttpGet("feature/{featureId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> GetFeatureComments(
        [FromRoute] string mapId,
        [FromRoute] string featureId)
    {
        var comments = await _commentService.GetFeatureCommentsAsync(mapId, featureId);
        var responses = comments.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    /// <summary>
    /// Gets replies to a comment (threaded discussion)
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The parent comment ID</param>
    /// <response code="200">List of replies</response>
    [HttpGet("{commentId}/replies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> GetReplies(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var replies = await _commentService.GetRepliesAsync(commentId);
        var responses = replies.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    /// <summary>
    /// Updates an existing comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <param name="request">Updated comment data</param>
    /// <response code="200">Comment updated successfully</response>
    /// <response code="404">Comment not found</response>
    /// <response code="403">Not authorized to update this comment</response>
    [HttpPut("{commentId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(MapCommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MapCommentResponse>> UpdateComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId,
        [FromBody] UpdateCommentRequest request)
    {
        var userId = User.Identity?.Name ?? "";
        var comment = await _commentService.GetCommentAsync(commentId);

        if (comment == null || comment.MapId != mapId)
        {
            return NotFound(new { error = "Comment not found" });
        }

        // Check if user owns the comment or is admin
        if (comment.AuthorUserId != userId && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You can only edit your own comments" });
        }

        if (request.CommentText != null)
            comment.CommentText = request.CommentText;

        if (request.Category != null)
            comment.Category = request.Category;

        if (request.Priority != null)
            comment.Priority = request.Priority;

        if (request.Color != null)
            comment.Color = request.Color;

        var updated = await _commentService.UpdateCommentAsync(comment);

        // Broadcast to SignalR clients
        await _commentHub.Clients.Group($"map_{mapId}")
            .SendAsync(CommentHubEvents.CommentUpdated, updated);

        return Ok(MapToResponse(updated));
    }

    /// <summary>
    /// Deletes a comment (soft delete)
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="204">Comment deleted successfully</response>
    /// <response code="404">Comment not found</response>
    /// <response code="403">Not authorized to delete this comment</response>
    [HttpDelete("{commentId}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var userId = User.Identity?.Name ?? "";
        var comment = await _commentService.GetCommentAsync(commentId);

        if (comment == null || comment.MapId != mapId)
        {
            return NotFound(new { error = "Comment not found" });
        }

        // Check if user owns the comment or is admin
        if (comment.AuthorUserId != userId && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = "You can only delete your own comments" });
        }

        await _commentService.DeleteCommentAsync(commentId);

        // Broadcast to SignalR clients
        await _commentHub.Clients.Group($"map_{mapId}")
            .SendAsync(CommentHubEvents.CommentDeleted, new { MapId = mapId, CommentId = commentId });

        return NoContent();
    }

    // ==================== Status Management ====================

    /// <summary>
    /// Resolves a comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">Comment resolved successfully</response>
    /// <response code="404">Comment not found</response>
    [HttpPost("{commentId}/resolve")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var userId = User.Identity?.Name ?? "";
        var comment = await _commentService.GetCommentAsync(commentId);

        if (comment == null || comment.MapId != mapId)
        {
            return NotFound(new { error = "Comment not found" });
        }

        await _commentService.ResolveCommentAsync(commentId, userId);

        // Broadcast to SignalR clients
        await _commentHub.Clients.Group($"map_{mapId}")
            .SendAsync(CommentHubEvents.CommentStatusChanged, new
            {
                MapId = mapId,
                CommentId = commentId,
                Status = CommentStatus.Resolved,
                ResolvedBy = userId
            });

        return Ok(new { message = "Comment resolved" });
    }

    /// <summary>
    /// Reopens a resolved comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">Comment reopened successfully</response>
    /// <response code="404">Comment not found</response>
    [HttpPost("{commentId}/reopen")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReopenComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var comment = await _commentService.GetCommentAsync(commentId);

        if (comment == null || comment.MapId != mapId)
        {
            return NotFound(new { error = "Comment not found" });
        }

        await _commentService.ReopenCommentAsync(commentId);

        // Broadcast to SignalR clients
        await _commentHub.Clients.Group($"map_{mapId}")
            .SendAsync(CommentHubEvents.CommentStatusChanged, new
            {
                MapId = mapId,
                CommentId = commentId,
                Status = CommentStatus.Open,
                ResolvedBy = (string?)null
            });

        return Ok(new { message = "Comment reopened" });
    }

    // ==================== Reactions ====================

    /// <summary>
    /// Adds a reaction to a comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <param name="request">Reaction data</param>
    /// <response code="201">Reaction added successfully</response>
    [HttpPost("{commentId}/reactions")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddReaction(
        [FromRoute] string mapId,
        [FromRoute] string commentId,
        [FromBody] AddReactionRequest request)
    {
        var userId = User.Identity?.Name ?? "";

        var reaction = await _commentService.AddReactionAsync(
            commentId,
            userId,
            request.ReactionType ?? "like");

        // Broadcast to SignalR clients
        await _commentHub.Clients.Group($"map_{mapId}")
            .SendAsync(CommentHubEvents.CommentReactionAdded, new
            {
                MapId = mapId,
                CommentId = commentId,
                UserId = userId,
                ReactionType = reaction.ReactionType
            });

        return Created("", new { message = "Reaction added" });
    }

    /// <summary>
    /// Gets reactions for a comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">List of reactions</response>
    [HttpGet("{commentId}/reactions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CommentReactionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CommentReactionResponse>>> GetReactions(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        var reactions = await _commentService.GetReactionsAsync(commentId);
        var responses = reactions.Select(r => new CommentReactionResponse
        {
            Id = r.Id,
            CommentId = r.CommentId,
            UserId = r.UserId,
            ReactionType = r.ReactionType,
            CreatedAt = r.CreatedAt
        }).ToList();

        return Ok(responses);
    }

    // ==================== Analytics & Search ====================

    /// <summary>
    /// Searches comments in a map
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="q">Search query</param>
    /// <response code="200">Search results</response>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> SearchComments(
        [FromRoute] string mapId,
        [FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Search query is required" });
        }

        var comments = await _commentService.SearchCommentsAsync(mapId, q);
        var responses = comments.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    /// <summary>
    /// Gets comment analytics for a map
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <response code="200">Analytics data</response>
    [HttpGet("analytics")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(CommentAnalytics), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentAnalytics>> GetAnalytics([FromRoute] string mapId)
    {
        var analytics = await _commentService.GetAnalyticsAsync(mapId);
        return Ok(analytics);
    }

    /// <summary>
    /// Exports comments to CSV
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <response code="200">CSV file</response>
    [HttpGet("export")]
    [Authorize(Policy = "RequireUser")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportComments([FromRoute] string mapId)
    {
        var comments = await _commentService.GetMapCommentsAsync(mapId);
        var csv = _commentService.ExportToCSV(comments);

        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"map-{mapId}-comments-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ==================== Moderation ====================

    /// <summary>
    /// Approves a pending comment
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">Comment approved</response>
    [HttpPost("{commentId}/approve")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveComment(
        [FromRoute] string mapId,
        [FromRoute] string commentId)
    {
        await _commentService.ApproveCommentAsync(commentId);
        return Ok(new { message = "Comment approved" });
    }

    /// <summary>
    /// Gets pending comments awaiting moderation
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <response code="200">List of pending comments</response>
    [HttpGet("pending")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(List<MapCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MapCommentResponse>>> GetPendingComments([FromRoute] string mapId)
    {
        var comments = await _commentService.GetPendingCommentsAsync();
        var filtered = comments.Where(c => c.MapId == mapId);
        var responses = filtered.Select(MapToResponse).ToList();
        return Ok(responses);
    }

    // Helper methods
    private MapCommentResponse MapToResponse(MapComment comment)
    {
        return new MapCommentResponse
        {
            Id = comment.Id,
            MapId = comment.MapId,
            LayerId = comment.LayerId,
            FeatureId = comment.FeatureId,
            Author = comment.Author,
            AuthorUserId = comment.AuthorUserId,
            IsGuest = comment.IsGuest,
            CommentText = comment.CommentText,
            GeometryType = comment.GeometryType,
            Geometry = comment.Geometry,
            Longitude = comment.Longitude,
            Latitude = comment.Latitude,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            ParentId = comment.ParentId,
            ThreadDepth = comment.ThreadDepth,
            Status = comment.Status,
            ResolvedBy = comment.ResolvedBy,
            ResolvedAt = comment.ResolvedAt,
            Category = comment.Category,
            Priority = comment.Priority,
            Color = comment.Color,
            IsApproved = comment.IsApproved,
            IsPinned = comment.IsPinned,
            ReplyCount = comment.ReplyCount,
            LikeCount = comment.LikeCount,
            Attachments = comment.Attachments
        };
    }
}

// ==================== Request/Response Models ====================

public class CreateCommentRequest
{
    public string? Author { get; set; }

    [Required]
    [MaxLength(10000)]
    public string CommentText { get; set; } = string.Empty;

    public string? LayerId { get; set; }
    public string? FeatureId { get; set; }
    public string? GeometryType { get; set; }
    public string? Geometry { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public string? ParentId { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Color { get; set; }
}

public class UpdateCommentRequest
{
    public string? CommentText { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Color { get; set; }
}

public class AddReactionRequest
{
    public string? ReactionType { get; set; } = "like";
}

public class MapCommentResponse
{
    public string Id { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string? LayerId { get; set; }
    public string? FeatureId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? AuthorUserId { get; set; }
    public bool IsGuest { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public string? Geometry { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ParentId { get; set; }
    public int ThreadDepth { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Category { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsApproved { get; set; }
    public bool IsPinned { get; set; }
    public int ReplyCount { get; set; }
    public int LikeCount { get; set; }
    public string? Attachments { get; set; }
}

public class CommentReactionResponse
{
    public string Id { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ReactionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
