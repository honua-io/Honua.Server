// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a share's expiration is renewed/extended.
/// This event can trigger notifications to share recipients.
/// </summary>
public sealed class ShareRenewedEvent : DomainEvent
{
    /// <summary>
    /// Gets the token of the renewed share
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the map ID of the renewed share
    /// </summary>
    public string MapId { get; }

    /// <summary>
    /// Gets the previous expiration date
    /// </summary>
    public DateTime? PreviousExpiresAt { get; }

    /// <summary>
    /// Gets the new expiration date
    /// </summary>
    public DateTime? NewExpiresAt { get; }

    /// <summary>
    /// Gets the user who renewed the share
    /// </summary>
    public string? RenewedBy { get; }

    /// <summary>
    /// Initializes a new instance of the ShareRenewedEvent
    /// </summary>
    public ShareRenewedEvent(
        string token,
        string mapId,
        DateTime? previousExpiresAt,
        DateTime? newExpiresAt,
        string? renewedBy)
    {
        Token = token;
        MapId = mapId;
        PreviousExpiresAt = previousExpiresAt;
        NewExpiresAt = newExpiresAt;
        RenewedBy = renewedBy;
    }
}
