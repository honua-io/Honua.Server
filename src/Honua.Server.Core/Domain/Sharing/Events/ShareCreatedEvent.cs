// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a new share is created.
/// This event can trigger actions like sending notifications or logging.
/// </summary>
public sealed class ShareCreatedEvent : DomainEvent
{
    /// <summary>
    /// Gets the token of the created share
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the map ID being shared
    /// </summary>
    public string MapId { get; }

    /// <summary>
    /// Gets the user who created the share
    /// </summary>
    public string? CreatedBy { get; }

    /// <summary>
    /// Gets the permission level of the share
    /// </summary>
    public SharePermission Permission { get; }

    /// <summary>
    /// Gets whether the share allows guest access
    /// </summary>
    public bool AllowGuestAccess { get; }

    /// <summary>
    /// Gets the expiration date of the share
    /// </summary>
    public DateTime? ExpiresAt { get; }

    /// <summary>
    /// Gets whether the share is password protected
    /// </summary>
    public bool IsPasswordProtected { get; }

    /// <summary>
    /// Initializes a new instance of the ShareCreatedEvent
    /// </summary>
    public ShareCreatedEvent(
        string token,
        string mapId,
        string? createdBy,
        SharePermission permission,
        bool allowGuestAccess,
        DateTime? expiresAt,
        bool isPasswordProtected)
    {
        Token = token;
        MapId = mapId;
        CreatedBy = createdBy;
        Permission = permission;
        AllowGuestAccess = allowGuestAccess;
        ExpiresAt = expiresAt;
        IsPasswordProtected = isPasswordProtected;
    }
}
