// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a share is deactivated.
/// This event can trigger cleanup actions or notifications.
/// </summary>
/// <param name="Token">The token of the deactivated share</param>
/// <param name="MapId">The map ID of the deactivated share</param>
/// <param name="DeactivatedBy">The user who deactivated the share</param>
/// <param name="Reason">The reason for deactivation</param>
public sealed record ShareDeactivatedEvent(
    string Token,
    string MapId,
    string? DeactivatedBy,
    string? Reason = null) : DomainEvent;
