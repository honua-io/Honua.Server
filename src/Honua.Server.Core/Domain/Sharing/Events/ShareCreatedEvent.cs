// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a new share is created.
/// This event can trigger actions like sending notifications or logging.
/// </summary>
/// <param name="Token">The token of the created share</param>
/// <param name="MapId">The map ID being shared</param>
/// <param name="CreatedBy">The user who created the share</param>
/// <param name="Permission">The permission level of the share</param>
/// <param name="AllowGuestAccess">Whether the share allows guest access</param>
/// <param name="ExpiresAt">The expiration date of the share</param>
/// <param name="IsPasswordProtected">Whether the share is password protected</param>
public sealed record ShareCreatedEvent(
    string Token,
    string MapId,
    string? CreatedBy,
    SharePermission Permission,
    bool AllowGuestAccess,
    DateTime? ExpiresAt,
    bool IsPasswordProtected) : DomainEvent;
