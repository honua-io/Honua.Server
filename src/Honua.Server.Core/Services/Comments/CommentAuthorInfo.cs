// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Contains information about the comment author and request context
/// </summary>
public sealed record CommentAuthorInfo
{
    /// <summary>
    /// Gets the display name of the author
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Gets the authenticated user ID of the author (optional)
    /// </summary>
    public string? AuthorUserId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the author is a guest user
    /// </summary>
    /// <remarks>
    /// Defaults to false. Guest comments may require moderation.
    /// </remarks>
    public bool IsGuest { get; init; } = false;

    /// <summary>
    /// Gets the email address for guest users (optional)
    /// </summary>
    public string? GuestEmail { get; init; }

    /// <summary>
    /// Gets the IP address of the request (optional)
    /// </summary>
    /// <remarks>
    /// Used for auditing and spam prevention
    /// </remarks>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets the User-Agent header from the request (optional)
    /// </summary>
    /// <remarks>
    /// Used for auditing and analytics
    /// </remarks>
    public string? UserAgent { get; init; }
}
