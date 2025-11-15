// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a comment is added to a share.
/// This event can trigger notifications to the share owner and other participants.
/// </summary>
/// <param name="ShareToken">The token of the share</param>
/// <param name="MapId">The map ID</param>
/// <param name="CommentId">The comment ID</param>
/// <param name="Author">The author of the comment</param>
/// <param name="IsGuest">Whether the comment is from a guest</param>
/// <param name="Text">The comment text</param>
/// <param name="RequiresApproval">Whether the comment requires approval</param>
public sealed record CommentAddedEvent(
    string ShareToken,
    string MapId,
    string CommentId,
    string Author,
    bool IsGuest,
    string Text,
    bool RequiresApproval) : DomainEvent;
