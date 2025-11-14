// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Specifies where a comment is attached (map, layer, or feature)
/// </summary>
public sealed record CommentTargetInfo
{
    /// <summary>
    /// Gets the ID of the map this comment belongs to
    /// </summary>
    public required string MapId { get; init; }

    /// <summary>
    /// Gets the ID of the layer this comment is attached to (optional)
    /// </summary>
    public string? LayerId { get; init; }

    /// <summary>
    /// Gets the ID of the feature this comment is attached to (optional)
    /// </summary>
    public string? FeatureId { get; init; }
}
