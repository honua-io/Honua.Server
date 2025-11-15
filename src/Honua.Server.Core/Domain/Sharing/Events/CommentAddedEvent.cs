// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing.Events;

/// <summary>
/// Domain event raised when a comment is added to a share.
/// This event can trigger notifications to the share owner and other participants.
/// </summary>
public sealed class CommentAddedEvent : DomainEvent
{
    /// <summary>
    /// Gets the token of the share
    /// </summary>
    public string ShareToken { get; }

    /// <summary>
    /// Gets the map ID
    /// </summary>
    public string MapId { get; }

    /// <summary>
    /// Gets the comment ID
    /// </summary>
    public string CommentId { get; }

    /// <summary>
    /// Gets the author of the comment
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// Gets whether the comment is from a guest
    /// </summary>
    public bool IsGuest { get; }

    /// <summary>
    /// Gets the comment text
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets whether the comment requires approval
    /// </summary>
    public bool RequiresApproval { get; }

    /// <summary>
    /// Initializes a new instance of the CommentAddedEvent
    /// </summary>
    public CommentAddedEvent(
        string shareToken,
        string mapId,
        string commentId,
        string author,
        bool isGuest,
        string text,
        bool requiresApproval)
    {
        ShareToken = shareToken;
        MapId = mapId;
        CommentId = commentId;
        Author = author;
        IsGuest = isGuest;
        Text = text;
        RequiresApproval = requiresApproval;
    }
}
