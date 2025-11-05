// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to create a new folder.
/// </summary>
public sealed class CreateFolderRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }
}

/// <summary>
/// Request to update an existing folder.
/// </summary>
public sealed class UpdateFolderRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }
}

/// <summary>
/// Response containing folder details.
/// </summary>
public sealed class FolderResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }

    [JsonPropertyName("serviceCount")]
    public int ServiceCount { get; set; }
}

/// <summary>
/// Folder tree node for UI display.
/// </summary>
public sealed class FolderTreeNode
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public int ServiceCount { get; set; }
    public List<FolderTreeNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
}
