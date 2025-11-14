// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models;

namespace Honua.Server.Core.Services.Comments;

/// <summary>
/// Contains the content and spatial information for a comment
/// </summary>
public sealed record CommentContentInfo
{
    /// <summary>
    /// Gets the text content of the comment
    /// </summary>
    public required string CommentText { get; init; }

    /// <summary>
    /// Gets the type of geometry (Point, LineString, Polygon, etc.)
    /// </summary>
    /// <remarks>
    /// Defaults to Point if not specified
    /// </remarks>
    public string GeometryType { get; init; } = CommentGeometryType.Point;

    /// <summary>
    /// Gets the GeoJSON geometry string (optional)
    /// </summary>
    public string? Geometry { get; init; }

    /// <summary>
    /// Gets the longitude coordinate (optional)
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Gets the latitude coordinate (optional)
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Gets the ID of the parent comment if this is a reply (optional)
    /// </summary>
    public string? ParentId { get; init; }
}
