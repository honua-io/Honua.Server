// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a share is deactivated.
/// This event can trigger cleanup actions or notifications.
/// </summary>
public sealed class ShareDeactivatedEvent : DomainEvent
{
    /// <summary>
    /// Gets the token of the deactivated share
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the map ID of the deactivated share
    /// </summary>
    public string MapId { get; }

    /// <summary>
    /// Gets the user who deactivated the share
    /// </summary>
    public string? DeactivatedBy { get; }

    /// <summary>
    /// Gets the reason for deactivation
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the ShareDeactivatedEvent
    /// </summary>
    public ShareDeactivatedEvent(
        string token,
        string mapId,
        string? deactivatedBy,
        string? reason = null)
    {
        Token = token;
        MapId = mapId;
        DeactivatedBy = deactivatedBy;
        Reason = reason;
    }
}
