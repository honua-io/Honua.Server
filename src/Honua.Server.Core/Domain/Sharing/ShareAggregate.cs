// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;
using Honua.Server.Core.Domain.Sharing.Events;

namespace Honua.Server.Core.Domain.Sharing;

/// <summary>
/// Aggregate root representing a shareable link for maps.
/// Encapsulates all business rules and invariants for share management.
/// </summary>
public sealed class ShareAggregate : AggregateRoot<string>
{
    private readonly List<ShareComment> _comments = new();

    /// <summary>
    /// Gets the map configuration ID being shared
    /// </summary>
    public string MapId { get; private set; }

    /// <summary>
    /// Gets the user who created the share
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// Gets the permission level for this share
    /// </summary>
    public SharePermission Permission { get; private set; }

    /// <summary>
    /// Gets whether the share allows guest (non-authenticated) access
    /// </summary>
    public bool AllowGuestAccess { get; private set; }

    /// <summary>
    /// Gets the expiration date (null = never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Gets when the share was created
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the number of times this share has been accessed
    /// </summary>
    public int AccessCount { get; private set; }

    /// <summary>
    /// Gets the last time the share was accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; private set; }

    /// <summary>
    /// Gets whether the share is still active
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the password protection for this share
    /// </summary>
    public SharePassword? Password { get; private set; }

    /// <summary>
    /// Gets the embed configuration for this share
    /// </summary>
    public ShareConfiguration Configuration { get; private set; }

    /// <summary>
    /// Gets the comments associated with this share
    /// </summary>
    public IReadOnlyCollection<ShareComment> Comments => _comments.AsReadOnly();

    /// <summary>
    /// Gets whether the share is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    /// <summary>
    /// Gets whether the share is valid (active and not expired)
    /// </summary>
    public bool IsValid => IsActive && !IsExpired;

    /// <summary>
    /// Gets whether the share is password protected
    /// </summary>
    public bool IsPasswordProtected => Password != null;

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor
    private ShareAggregate() : base(string.Empty)
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new share with the specified parameters.
    /// This is the factory method for creating shares.
    /// </summary>
    /// <param name="mapId">The map configuration ID to share</param>
    /// <param name="createdBy">The user creating the share</param>
    /// <param name="permission">The permission level</param>
    /// <param name="allowGuestAccess">Whether to allow guest access</param>
    /// <param name="expiresAt">Optional expiration date</param>
    /// <param name="password">Optional password for protection</param>
    /// <param name="configuration">Optional custom configuration</param>
    /// <returns>A new ShareAggregate instance</returns>
    /// <exception cref="ArgumentException">Thrown when input is invalid</exception>
    public static ShareAggregate Create(
        string mapId,
        string? createdBy,
        SharePermission permission,
        bool allowGuestAccess = true,
        DateTime? expiresAt = null,
        SharePassword? password = null,
        ShareConfiguration? configuration = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(mapId))
            throw new ArgumentException("Map ID cannot be empty", nameof(mapId));

        if (mapId.Length > 100)
            throw new ArgumentException("Map ID must not exceed 100 characters", nameof(mapId));

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future", nameof(expiresAt));

        // Create the share
        var token = GenerateToken();
        var share = new ShareAggregate
        {
            Id = token,
            MapId = mapId,
            CreatedBy = createdBy,
            Permission = permission,
            AllowGuestAccess = allowGuestAccess,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            AccessCount = 0,
            LastAccessedAt = null,
            IsActive = true,
            Password = password,
            Configuration = configuration ?? ShareConfiguration.CreateDefault()
        };

        // Raise domain event
        share.RaiseDomainEvent(new ShareCreatedEvent(
            token,
            mapId,
            createdBy,
            permission,
            allowGuestAccess,
            expiresAt,
            password != null));

        return share;
    }

    /// <summary>
    /// Deactivates the share.
    /// Once deactivated, the share can no longer be accessed.
    /// </summary>
    /// <param name="deactivatedBy">The user deactivating the share</param>
    /// <param name="reason">Optional reason for deactivation</param>
    /// <exception cref="InvalidOperationException">Thrown when share is already inactive</exception>
    public void Deactivate(string? deactivatedBy, string? reason = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("Share is already inactive");

        IsActive = false;

        RaiseDomainEvent(new ShareDeactivatedEvent(
            Id,
            MapId,
            deactivatedBy,
            reason));
    }

    /// <summary>
    /// Renews the share by extending its expiration date.
    /// Cannot renew expired shares.
    /// </summary>
    /// <param name="newExpiresAt">The new expiration date</param>
    /// <param name="renewedBy">The user renewing the share</param>
    /// <exception cref="InvalidOperationException">Thrown when share cannot be renewed</exception>
    /// <exception cref="ArgumentException">Thrown when new expiration date is invalid</exception>
    public void Renew(DateTime? newExpiresAt, string? renewedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot renew an inactive share");

        if (IsExpired)
            throw new InvalidOperationException("Cannot renew an expired share");

        if (newExpiresAt.HasValue && newExpiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("New expiration date must be in the future", nameof(newExpiresAt));

        var previousExpiresAt = ExpiresAt;
        ExpiresAt = newExpiresAt;

        RaiseDomainEvent(new ShareRenewedEvent(
            Id,
            MapId,
            previousExpiresAt,
            newExpiresAt,
            renewedBy));
    }

    /// <summary>
    /// Adds a comment from an authenticated user to the share.
    /// Validates that commenting is allowed based on share state and permissions.
    /// </summary>
    /// <param name="author">The user ID of the author</param>
    /// <param name="text">The comment text</param>
    /// <param name="parentId">Optional parent comment ID for threading</param>
    /// <param name="locationX">Optional X coordinate for map annotation</param>
    /// <param name="locationY">Optional Y coordinate for map annotation</param>
    /// <exception cref="InvalidOperationException">Thrown when commenting is not allowed</exception>
    public void AddUserComment(
        string author,
        string text,
        string? parentId = null,
        double? locationX = null,
        double? locationY = null)
    {
        ValidateCommentingAllowed();

        if (Permission == SharePermission.View)
            throw new InvalidOperationException("View-only shares do not allow commenting");

        var comment = ShareComment.CreateUserComment(author, text, parentId, locationX, locationY);
        _comments.Add(comment);

        RaiseDomainEvent(new CommentAddedEvent(
            Id,
            MapId,
            comment.Id,
            author,
            isGuest: false,
            text,
            requiresApproval: false));
    }

    /// <summary>
    /// Adds a comment from a guest to the share.
    /// Validates that guest commenting is allowed based on share state and permissions.
    /// </summary>
    /// <param name="guestName">The name of the guest</param>
    /// <param name="guestEmail">The email of the guest</param>
    /// <param name="text">The comment text</param>
    /// <param name="ipAddress">The IP address of the guest</param>
    /// <param name="userAgent">The user agent of the guest</param>
    /// <param name="parentId">Optional parent comment ID for threading</param>
    /// <param name="locationX">Optional X coordinate for map annotation</param>
    /// <param name="locationY">Optional Y coordinate for map annotation</param>
    /// <exception cref="InvalidOperationException">Thrown when commenting is not allowed</exception>
    public void AddGuestComment(
        string guestName,
        string? guestEmail,
        string text,
        string? ipAddress,
        string? userAgent,
        string? parentId = null,
        double? locationX = null,
        double? locationY = null)
    {
        ValidateCommentingAllowed();

        if (!AllowGuestAccess)
            throw new InvalidOperationException("Guest access is not allowed for this share");

        if (Permission == SharePermission.View)
            throw new InvalidOperationException("View-only shares do not allow commenting");

        var comment = ShareComment.CreateGuestComment(
            guestName,
            guestEmail,
            text,
            ipAddress,
            userAgent,
            parentId,
            locationX,
            locationY);

        _comments.Add(comment);

        RaiseDomainEvent(new CommentAddedEvent(
            Id,
            MapId,
            comment.Id,
            guestName,
            isGuest: true,
            text,
            requiresApproval: true));
    }

    /// <summary>
    /// Validates a password attempt against the share's password.
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>True if the password is valid or no password is required, false otherwise</returns>
    public bool ValidatePassword(string password)
    {
        if (Password == null)
            return true; // No password required

        return Password.Validate(password);
    }

    /// <summary>
    /// Changes the permission level of the share.
    /// </summary>
    /// <param name="newPermission">The new permission level</param>
    /// <exception cref="InvalidOperationException">Thrown when share is inactive</exception>
    public void ChangePermission(SharePermission newPermission)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot change permission on an inactive share");

        Permission = newPermission;
    }

    /// <summary>
    /// Records an access to the share.
    /// Updates the access count and last accessed timestamp.
    /// </summary>
    public void RecordAccess()
    {
        AccessCount++;
        LastAccessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the embed configuration for the share.
    /// </summary>
    /// <param name="newConfiguration">The new configuration</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when share is inactive</exception>
    public void UpdateConfiguration(ShareConfiguration newConfiguration)
    {
        if (newConfiguration == null)
            throw new ArgumentNullException(nameof(newConfiguration));

        if (!IsActive)
            throw new InvalidOperationException("Cannot update configuration on an inactive share");

        Configuration = newConfiguration;
    }

    /// <summary>
    /// Sets or updates the password protection for the share.
    /// </summary>
    /// <param name="password">The new password (null to remove protection)</param>
    /// <exception cref="InvalidOperationException">Thrown when share is inactive</exception>
    public void SetPassword(SharePassword? password)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot change password on an inactive share");

        Password = password;
    }

    /// <summary>
    /// Validates that commenting is allowed on this share.
    /// </summary>
    private void ValidateCommentingAllowed()
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot comment on an inactive share");

        if (IsExpired)
            throw new InvalidOperationException("Cannot comment on an expired share");
    }

    /// <summary>
    /// Generates a unique token for the share.
    /// </summary>
    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Enumeration of share permission levels.
/// Defines what actions are allowed for share recipients.
/// </summary>
public enum SharePermission
{
    /// <summary>
    /// View only - no interaction allowed
    /// </summary>
    View = 0,

    /// <summary>
    /// View and comment - can add comments
    /// </summary>
    Comment = 1,

    /// <summary>
    /// View, comment, and edit - full access
    /// </summary>
    Edit = 2
}
