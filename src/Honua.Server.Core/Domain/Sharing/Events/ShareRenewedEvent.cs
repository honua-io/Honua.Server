// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a share's expiration is renewed/extended.
/// This event can trigger notifications to share recipients.
/// </summary>
/// <param name="Token">The token of the renewed share</param>
/// <param name="MapId">The map ID of the renewed share</param>
/// <param name="PreviousExpiresAt">The previous expiration date</param>
/// <param name="NewExpiresAt">The new expiration date</param>
/// <param name="RenewedBy">The user who renewed the share</param>
public sealed record ShareRenewedEvent(
    string Token,
    string MapId,
    DateTime? PreviousExpiresAt,
    DateTime? NewExpiresAt,
    string? RenewedBy) : DomainEvent;
