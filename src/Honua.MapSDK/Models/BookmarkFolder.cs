// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.MapSDK.Models;

/// <summary>
/// Represents a folder for organizing bookmarks
/// </summary>
public class BookmarkFolder
{
    /// <summary>
    /// Unique identifier for the folder
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display name for the folder
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// MudBlazor icon name (e.g., Icons.Material.Filled.Folder)
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Color for the folder (hex color or MudBlazor color name)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Parent folder ID for nested folders
    /// </summary>
    public string? ParentFolderId { get; set; }

    /// <summary>
    /// Display order
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// When the folder was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
