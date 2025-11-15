// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models;

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Contains optional settings and metadata for a comment
/// </summary>
public sealed record CommentOptionsInfo
{
    /// <summary>
    /// Gets the category for organizing comments (optional)
    /// </summary>
    /// <remarks>
    /// Examples: "bug", "question", "suggestion", "note"
    /// </remarks>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the priority level of the comment
    /// </summary>
    /// <remarks>
    /// Defaults to Medium if not specified
    /// </remarks>
    public string Priority { get; init; } = CommentPriority.Medium;

    /// <summary>
    /// Gets the color for visual highlighting (optional)
    /// </summary>
    /// <remarks>
    /// Used for color-coding comments in the UI
    /// </remarks>
    public string? Color { get; init; }
}
