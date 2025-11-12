// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a saved map bookmark/view
/// </summary>
public class Bookmark
{
    /// <summary>
    /// Unique identifier for the bookmark
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display name for the bookmark
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Map center coordinates [longitude, latitude]
    /// </summary>
    public double[] Center { get; set; } = new[] { 0.0, 0.0 };

    /// <summary>
    /// Zoom level
    /// </summary>
    public double Zoom { get; set; }

    /// <summary>
    /// Map bearing (rotation) in degrees
    /// </summary>
    public double Bearing { get; set; }

    /// <summary>
    /// Map pitch (tilt) in degrees
    /// </summary>
    public double Pitch { get; set; }

    /// <summary>
    /// Thumbnail image URL or base64 data URL
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// When the bookmark was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created the bookmark
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Optional folder ID for organization
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Additional metadata (layer visibility, filters, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Whether the bookmark is publicly accessible
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Shareable URL for this bookmark
    /// </summary>
    public string? ShareUrl { get; set; }

    /// <summary>
    /// Last time the bookmark was accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Number of times this bookmark has been accessed
    /// </summary>
    public int AccessCount { get; set; }
}
