// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new service.
/// </summary>
public sealed record CreateServiceRequest
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public required string ServiceType { get; init; }
    public required string DataSourceId { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public ServiceOgcOptionsDto? OgcOptions { get; init; }
}

/// <summary>
/// Request to update an existing service.
/// </summary>
public sealed record UpdateServiceRequest
{
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public ServiceOgcOptionsDto? OgcOptions { get; init; }
}

/// <summary>
/// Response containing service details.
/// </summary>
public sealed record ServiceResponse
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public required string ServiceType { get; init; }
    public required string DataSourceId { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = new();
    public bool Enabled { get; init; }
    public int LayerCount { get; init; }
    public ServiceOgcOptionsDto? OgcOptions { get; init; }
    public DateTime CreatedAt { get; init; } // TODO: Add to metadata model
    public DateTime? ModifiedAt { get; init; } // TODO: Add to metadata model
}

/// <summary>
/// Lightweight service list item for list views.
/// </summary>
public sealed record ServiceListItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public required string ServiceType { get; init; }
    public bool Enabled { get; init; }
    public int LayerCount { get; init; }
}

/// <summary>
/// OGC service configuration options.
/// </summary>
public sealed record ServiceOgcOptionsDto
{
    public bool WfsEnabled { get; init; }
    public bool WmsEnabled { get; init; }
    public bool WmtsEnabled { get; init; }
    public bool CollectionsEnabled { get; init; }
    public int? ItemLimit { get; init; }
    public string? DefaultCrs { get; init; }
}
