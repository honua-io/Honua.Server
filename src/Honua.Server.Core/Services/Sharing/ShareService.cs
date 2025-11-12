// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Data.Sharing;
using Honua.Server.Core.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honua.Server.Core.Services.Sharing;

/// <summary>
/// Service for managing map sharing tokens and operations
/// </summary>
public class ShareService
{
    private readonly IShareRepository _repository;
    private readonly ILogger<ShareService> _logger;

    public ShareService(IShareRepository repository, ILogger<ShareService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new share token for a map
    /// </summary>
    public async Task<ShareToken> CreateShareAsync(
        string mapId,
        string permission,
        string? createdBy = null,
        bool allowGuestAccess = true,
        DateTime? expiresAt = null,
        string? password = null,
        EmbedSettings? embedSettings = null,
        CancellationToken cancellationToken = default)
    {
        if (!SharePermission.IsValid(permission))
        {
            throw new ArgumentException($"Invalid permission: {permission}. Must be one of: view, comment, edit", nameof(permission));
        }

        var token = new ShareToken
        {
            Token = GenerateToken(),
            MapId = mapId,
            CreatedBy = createdBy,
            Permission = permission,
            AllowGuestAccess = allowGuestAccess,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            PasswordHash = string.IsNullOrEmpty(password) ? null : HashPassword(password),
            EmbedSettings = embedSettings != null ? JsonSerializer.Serialize(embedSettings) : null
        };

        await _repository.CreateShareTokenAsync(token, cancellationToken);

        _logger.LogInformation("Created share token {Token} for map {MapId} with permission {Permission}",
            token.Token, mapId, permission);

        return token;
    }

    /// <summary>
    /// Validates a share token and returns it if valid
    /// </summary>
    public async Task<(bool IsValid, ShareToken? Token, string? Error)> ValidateShareAsync(
        string token,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var shareToken = await _repository.GetShareTokenAsync(token, cancellationToken);

        if (shareToken == null)
        {
            return (false, null, "Share token not found");
        }

        if (!shareToken.IsActive)
        {
            return (false, null, "This share link has been deactivated");
        }

        if (shareToken.IsExpired)
        {
            return (false, null, "This share link has expired");
        }

        // Check password if required
        if (!string.IsNullOrEmpty(shareToken.PasswordHash))
        {
            if (string.IsNullOrEmpty(password))
            {
                return (false, shareToken, "Password required");
            }

            if (!VerifyPassword(password, shareToken.PasswordHash))
            {
                return (false, shareToken, "Invalid password");
            }
        }

        // Increment access count
        await _repository.IncrementAccessCountAsync(token, cancellationToken);

        return (true, shareToken, null);
    }

    /// <summary>
    /// Gets all share tokens for a map
    /// </summary>
    public async Task<IReadOnlyList<ShareToken>> GetSharesForMapAsync(
        string mapId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetShareTokensByMapIdAsync(mapId, cancellationToken);
    }

    /// <summary>
    /// Updates a share token
    /// </summary>
    public async Task UpdateShareAsync(
        ShareToken token,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateShareTokenAsync(token, cancellationToken);
    }

    /// <summary>
    /// Deactivates a share token
    /// </summary>
    public async Task DeactivateShareAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeactivateShareTokenAsync(token, cancellationToken);
        _logger.LogInformation("Deactivated share token {Token}", token);
    }

    /// <summary>
    /// Cleans up expired tokens
    /// </summary>
    public async Task<int> CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteExpiredTokensAsync(cancellationToken);
        _logger.LogInformation("Cleaned up {Count} expired share tokens", deleted);
        return deleted;
    }

    /// <summary>
    /// Creates a comment on a shared map
    /// </summary>
    public async Task<ShareComment> CreateCommentAsync(
        string shareToken,
        string mapId,
        string author,
        string commentText,
        bool isGuest = true,
        string? guestEmail = null,
        string? parentId = null,
        double? locationX = null,
        double? locationY = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool autoApprove = false,
        CancellationToken cancellationToken = default)
    {
        // Validate share token allows commenting
        var (isValid, token, error) = await ValidateShareAsync(shareToken, cancellationToken: cancellationToken);
        if (!isValid || token == null)
        {
            throw new InvalidOperationException(error ?? "Invalid share token");
        }

        if (token.Permission != SharePermission.Comment && token.Permission != SharePermission.Edit)
        {
            throw new InvalidOperationException("This share link does not allow commenting");
        }

        var comment = new ShareComment
        {
            Id = Guid.NewGuid().ToString(),
            ShareToken = shareToken,
            MapId = mapId,
            Author = author,
            IsGuest = isGuest,
            GuestEmail = guestEmail,
            CommentText = commentText,
            CreatedAt = DateTime.UtcNow,
            IsApproved = autoApprove, // Auto-approve if specified
            ParentId = parentId,
            LocationX = locationX,
            LocationY = locationY,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _repository.CreateCommentAsync(comment, cancellationToken);

        _logger.LogInformation("Created comment {CommentId} on share token {Token} by {Author}",
            comment.Id, shareToken, author);

        return comment;
    }

    /// <summary>
    /// Gets comments for a share token
    /// </summary>
    public async Task<IReadOnlyList<ShareComment>> GetCommentsAsync(
        string shareToken,
        bool includeUnapproved = false,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentsByTokenAsync(shareToken, includeUnapproved, cancellationToken);
    }

    /// <summary>
    /// Gets comments for a map
    /// </summary>
    public async Task<IReadOnlyList<ShareComment>> GetCommentsByMapIdAsync(
        string mapId,
        bool includeUnapproved = false,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetCommentsByMapIdAsync(mapId, includeUnapproved, cancellationToken);
    }

    /// <summary>
    /// Approves a comment
    /// </summary>
    public async Task ApproveCommentAsync(
        string commentId,
        CancellationToken cancellationToken = default)
    {
        await _repository.ApproveCommentAsync(commentId, cancellationToken);
        _logger.LogInformation("Approved comment {CommentId}", commentId);
    }

    /// <summary>
    /// Deletes a comment
    /// </summary>
    public async Task DeleteCommentAsync(
        string commentId,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteCommentAsync(commentId, cancellationToken);
        _logger.LogInformation("Deleted comment {CommentId}", commentId);
    }

    /// <summary>
    /// Gets pending comments awaiting moderation
    /// </summary>
    public async Task<IReadOnlyList<ShareComment>> GetPendingCommentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetPendingCommentsAsync(limit, cancellationToken);
    }

    /// <summary>
    /// Generates embed code for a share token
    /// </summary>
    public string GenerateEmbedCode(ShareToken token, string baseUrl, EmbedCodeType type = EmbedCodeType.Iframe)
    {
        var shareUrl = $"{baseUrl.TrimEnd('/')}/share/{token.Token}";
        var embedSettings = string.IsNullOrEmpty(token.EmbedSettings)
            ? new EmbedSettings()
            : JsonSerializer.Deserialize<EmbedSettings>(token.EmbedSettings) ?? new EmbedSettings();

        if (type == EmbedCodeType.Iframe)
        {
            var allowFullscreen = embedSettings.AllowFullscreen ? " allowfullscreen" : "";
            return $@"<iframe src=""{shareUrl}"" width=""{embedSettings.Width}"" height=""{embedSettings.Height}"" style=""border:none;""{allowFullscreen}></iframe>";
        }
        else if (type == EmbedCodeType.JavaScript)
        {
            return $@"<div id=""honua-map-{token.Token}""></div>
<script src=""{baseUrl.TrimEnd('/')}/js/honua-embed.js""></script>
<script>
  HonuaEmbed.init({{
    container: 'honua-map-{token.Token}',
    shareToken: '{token.Token}',
    width: '{embedSettings.Width}',
    height: '{embedSettings.Height}',
    showZoomControls: {embedSettings.ShowZoomControls.ToString().ToLower()},
    showLayerSwitcher: {embedSettings.ShowLayerSwitcher.ToString().ToLower()},
    showSearch: {embedSettings.ShowSearch.ToString().ToLower()},
    showScaleBar: {embedSettings.ShowScaleBar.ToString().ToLower()},
    showAttribution: {embedSettings.ShowAttribution.ToString().ToLower()}
  }});
</script>";
        }

        throw new ArgumentException($"Unknown embed code type: {type}");
    }

    // Private helper methods
    private static string GenerateToken()
    {
        // Generate a URL-safe random token
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        var hash = HashPassword(password);
        return hash == passwordHash;
    }
}

/// <summary>
/// Types of embed code that can be generated
/// </summary>
public enum EmbedCodeType
{
    Iframe,
    JavaScript
}
