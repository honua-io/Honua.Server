// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Parameter object for creating a new comment
/// </summary>
/// <remarks>
/// This class uses the Parameter Object pattern to reduce the number of method parameters
/// from 19 individual parameters to a single cohesive object with related properties grouped
/// into logical sub-objects.
/// </remarks>
public sealed record CreateCommentParameters
{
    /// <summary>
    /// Gets the target location where this comment is attached
    /// </summary>
    /// <remarks>
    /// Required. Specifies the map, and optionally the layer or feature.
    /// </remarks>
    public required CommentTargetInfo Target { get; init; }

    /// <summary>
    /// Gets the content and spatial information for the comment
    /// </summary>
    /// <remarks>
    /// Required. Contains the comment text and optional geometry.
    /// </remarks>
    public required CommentContentInfo Content { get; init; }

    /// <summary>
    /// Gets information about the comment author and request context
    /// </summary>
    /// <remarks>
    /// Required. Contains author details and request metadata.
    /// </remarks>
    public required CommentAuthorInfo Author { get; init; }

    /// <summary>
    /// Gets optional settings and metadata for the comment
    /// </summary>
    /// <remarks>
    /// Optional. Contains category, priority, and visual settings.
    /// </remarks>
    public CommentOptionsInfo? Options { get; init; }
}
