// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new folder.
/// </summary>
public sealed record CreateFolderRequest
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
}

/// <summary>
/// Request to update an existing folder.
/// </summary>
public sealed record UpdateFolderRequest
{
    public string? Title { get; init; }
    public int? Order { get; init; }
}

/// <summary>
/// Response containing folder details.
/// </summary>
public sealed record FolderResponse
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
    public int ServiceCount { get; init; }
}
