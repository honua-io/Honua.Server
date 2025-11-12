// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Services.Sharing;
using Honua.Server.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for managing map sharing and embeds.
/// Provides zero-click sharing with configurable permissions and guest access.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/maps")]
[Produces("application/json")]
public class ShareController : ControllerBase
{
    private readonly ILogger<ShareController> _logger;
    private readonly ShareService _shareService;

    public ShareController(
        ILogger<ShareController> logger,
        ShareService shareService)
    {
        _logger = logger;
        _shareService = shareService;
    }

    // ==================== Share Token Management ====================

    /// <summary>
    /// Creates a new share link for a map with one click
    /// </summary>
    /// <param name="mapId">The map ID to share</param>
    /// <param name="request">Share configuration</param>
    /// <response code="201">Share link created successfully</response>
    /// <response code="400">Invalid request</response>
    [HttpPost("{mapId}/share")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(ShareTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShareTokenResponse>> CreateShare(
        [FromRoute] string mapId,
        [FromBody] CreateShareRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";

            var token = await _shareService.CreateShareAsync(
                mapId,
                request.Permission ?? SharePermission.View,
                userId,
                request.AllowGuestAccess,
                request.ExpiresAt,
                request.Password,
                request.EmbedSettings);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var shareUrl = $"{baseUrl}/share/{token.Token}";

            var embedCode = _shareService.GenerateEmbedCode(token, baseUrl, EmbedCodeType.Iframe);
            var jsEmbedCode = _shareService.GenerateEmbedCode(token, baseUrl, EmbedCodeType.JavaScript);

            var response = new ShareTokenResponse
            {
                Token = token.Token,
                ShareUrl = shareUrl,
                Permission = token.Permission,
                AllowGuestAccess = token.AllowGuestAccess,
                ExpiresAt = token.ExpiresAt,
                CreatedAt = token.CreatedAt,
                EmbedCode = embedCode,
                JsEmbedCode = jsEmbedCode,
                HasPassword = !string.IsNullOrEmpty(token.PasswordHash)
            };

            _logger.LogInformation("Created share link for map {MapId}: {Token}", mapId, token.Token);
            return CreatedAtAction(nameof(GetShare), new { token = token.Token }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets an existing share token
    /// </summary>
    /// <param name="token">The share token</param>
    /// <param name="password">Optional password if the share is protected</param>
    /// <response code="200">Share token found</response>
    /// <response code="404">Share token not found</response>
    /// <response code="401">Invalid password</response>
    [HttpGet("share/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShareTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ShareTokenResponse>> GetShare(
        [FromRoute] string token,
        [FromQuery] string? password = null)
    {
        var (isValid, shareToken, error) = await _shareService.ValidateShareAsync(token, password);

        if (!isValid || shareToken == null)
        {
            if (error == "Password required" || error == "Invalid password")
            {
                return Unauthorized(new { error });
            }
            return NotFound(new { error = error ?? "Share not found" });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var shareUrl = $"{baseUrl}/share/{token}";

        var response = new ShareTokenResponse
        {
            Token = shareToken.Token,
            ShareUrl = shareUrl,
            MapId = shareToken.MapId,
            Permission = shareToken.Permission,
            AllowGuestAccess = shareToken.AllowGuestAccess,
            ExpiresAt = shareToken.ExpiresAt,
            CreatedAt = shareToken.CreatedAt,
            AccessCount = shareToken.AccessCount,
            HasPassword = !string.IsNullOrEmpty(shareToken.PasswordHash)
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets all share links for a map
    /// </summary>
    /// <param name="mapId">The map ID</param>
    /// <response code="200">List of share tokens</response>
    [HttpGet("{mapId}/shares")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(typeof(List<ShareTokenResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShareTokenResponse>>> GetSharesForMap([FromRoute] string mapId)
    {
        var tokens = await _shareService.GetSharesForMapAsync(mapId);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var responses = tokens.Select(t => new ShareTokenResponse
        {
            Token = t.Token,
            ShareUrl = $"{baseUrl}/share/{t.Token}",
            MapId = t.MapId,
            Permission = t.Permission,
            AllowGuestAccess = t.AllowGuestAccess,
            ExpiresAt = t.ExpiresAt,
            CreatedAt = t.CreatedAt,
            AccessCount = t.AccessCount,
            LastAccessedAt = t.LastAccessedAt,
            IsActive = t.IsActive,
            HasPassword = !string.IsNullOrEmpty(t.PasswordHash)
        }).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Updates a share token
    /// </summary>
    /// <param name="token">The share token</param>
    /// <param name="request">Updated share configuration</param>
    /// <response code="200">Share updated successfully</response>
    /// <response code="404">Share not found</response>
    [HttpPut("share/{token}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateShare(
        [FromRoute] string token,
        [FromBody] UpdateShareRequest request)
    {
        var (isValid, shareToken, _) = await _shareService.ValidateShareAsync(token);

        if (!isValid || shareToken == null)
        {
            return NotFound(new { error = "Share not found" });
        }

        if (request.Permission != null)
            shareToken.Permission = request.Permission;

        if (request.AllowGuestAccess.HasValue)
            shareToken.AllowGuestAccess = request.AllowGuestAccess.Value;

        if (request.ExpiresAt != null)
            shareToken.ExpiresAt = request.ExpiresAt;

        if (request.IsActive.HasValue)
            shareToken.IsActive = request.IsActive.Value;

        await _shareService.UpdateShareAsync(shareToken);

        _logger.LogInformation("Updated share token {Token}", token);
        return Ok(new { message = "Share updated successfully" });
    }

    /// <summary>
    /// Deactivates a share link
    /// </summary>
    /// <param name="token">The share token to deactivate</param>
    /// <response code="204">Share deactivated successfully</response>
    /// <response code="404">Share not found</response>
    [HttpDelete("share/{token}")]
    [Authorize(Policy = "RequireUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateShare([FromRoute] string token)
    {
        var (isValid, _, _) = await _shareService.ValidateShareAsync(token);

        if (!isValid)
        {
            return NotFound(new { error = "Share not found" });
        }

        await _shareService.DeactivateShareAsync(token);
        return NoContent();
    }

    /// <summary>
    /// Generates embed code for a share
    /// </summary>
    /// <param name="token">The share token</param>
    /// <param name="type">Type of embed code (iframe or javascript)</param>
    /// <response code="200">Embed code generated</response>
    /// <response code="404">Share not found</response>
    [HttpGet("share/{token}/embed")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EmbedCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmbedCodeResponse>> GetEmbedCode(
        [FromRoute] string token,
        [FromQuery] string type = "iframe")
    {
        var (isValid, shareToken, error) = await _shareService.ValidateShareAsync(token);

        if (!isValid || shareToken == null)
        {
            return NotFound(new { error = error ?? "Share not found" });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var embedType = type.ToLower() == "javascript" ? EmbedCodeType.JavaScript : EmbedCodeType.Iframe;
        var embedCode = _shareService.GenerateEmbedCode(shareToken, baseUrl, embedType);

        return Ok(new EmbedCodeResponse
        {
            Type = embedType.ToString().ToLower(),
            Code = embedCode
        });
    }

    // ==================== Comment Management ====================

    /// <summary>
    /// Creates a comment on a shared map (guest-friendly)
    /// </summary>
    /// <param name="token">The share token</param>
    /// <param name="request">Comment data</param>
    /// <response code="201">Comment created successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="403">Commenting not allowed</response>
    [HttpPost("share/{token}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShareCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShareCommentResponse>> CreateComment(
        [FromRoute] string token,
        [FromBody] CreateCommentRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var isGuest = !User.Identity?.IsAuthenticated ?? true;
            var author = isGuest ? request.Author : (User.Identity?.Name ?? request.Author);

            var comment = await _shareService.CreateCommentAsync(
                token,
                request.MapId,
                author,
                request.CommentText,
                isGuest,
                request.GuestEmail,
                request.ParentId,
                request.LocationX,
                request.LocationY,
                ipAddress,
                userAgent,
                autoApprove: !isGuest); // Auto-approve authenticated users

            var response = new ShareCommentResponse
            {
                Id = comment.Id,
                Author = comment.Author,
                CommentText = comment.CommentText,
                CreatedAt = comment.CreatedAt,
                IsApproved = comment.IsApproved,
                ParentId = comment.ParentId,
                LocationX = comment.LocationX,
                LocationY = comment.LocationY
            };

            return CreatedAtAction(nameof(GetComments), new { token }, response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets comments for a shared map
    /// </summary>
    /// <param name="token">The share token</param>
    /// <response code="200">List of comments</response>
    [HttpGet("share/{token}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ShareCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShareCommentResponse>>> GetComments([FromRoute] string token)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var comments = await _shareService.GetCommentsAsync(token, includeUnapproved: isAuthenticated);

        var responses = comments.Select(c => new ShareCommentResponse
        {
            Id = c.Id,
            Author = c.Author,
            CommentText = c.CommentText,
            CreatedAt = c.CreatedAt,
            IsApproved = c.IsApproved,
            ParentId = c.ParentId,
            LocationX = c.LocationX,
            LocationY = c.LocationY
        }).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Approves a guest comment
    /// </summary>
    /// <param name="commentId">The comment ID</param>
    /// <response code="200">Comment approved</response>
    /// <response code="404">Comment not found</response>
    [HttpPost("comments/{commentId}/approve")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveComment([FromRoute] string commentId)
    {
        await _shareService.ApproveCommentAsync(commentId);
        return Ok(new { message = "Comment approved" });
    }

    /// <summary>
    /// Deletes a comment
    /// </summary>
    /// <param name="commentId">The comment ID</param>
    /// <response code="204">Comment deleted</response>
    [HttpDelete("comments/{commentId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteComment([FromRoute] string commentId)
    {
        await _shareService.DeleteCommentAsync(commentId);
        return NoContent();
    }

    /// <summary>
    /// Gets pending comments awaiting moderation
    /// </summary>
    /// <response code="200">List of pending comments</response>
    [HttpGet("comments/pending")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(List<ShareCommentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ShareCommentResponse>>> GetPendingComments()
    {
        var comments = await _shareService.GetPendingCommentsAsync();

        var responses = comments.Select(c => new ShareCommentResponse
        {
            Id = c.Id,
            Author = c.Author,
            CommentText = c.CommentText,
            CreatedAt = c.CreatedAt,
            IsApproved = c.IsApproved,
            ParentId = c.ParentId,
            LocationX = c.LocationX,
            LocationY = c.LocationY,
            MapId = c.MapId,
            ShareToken = c.ShareToken
        }).ToList();

        return Ok(responses);
    }
}

// ==================== Request/Response Models ====================

public class CreateShareRequest
{
    public string? Permission { get; set; }
    public bool AllowGuestAccess { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public string? Password { get; set; }
    public EmbedSettings? EmbedSettings { get; set; }
}

public class UpdateShareRequest
{
    public string? Permission { get; set; }
    public bool? AllowGuestAccess { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool? IsActive { get; set; }
}

public class ShareTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
    public string? MapId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public bool AllowGuestAccess { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasPassword { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbedCode { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JsEmbedCode { get; set; }
}

public class EmbedCodeResponse
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class CreateCommentRequest
{
    [Required]
    public string MapId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;

    [Required]
    [MaxLength(5000)]
    public string CommentText { get; set; } = string.Empty;

    [EmailAddress]
    public string? GuestEmail { get; set; }

    public string? ParentId { get; set; }
    public double? LocationX { get; set; }
    public double? LocationY { get; set; }
}

public class ShareCommentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsApproved { get; set; }
    public string? ParentId { get; set; }
    public double? LocationX { get; set; }
    public double? LocationY { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MapId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShareToken { get; set; }
}
