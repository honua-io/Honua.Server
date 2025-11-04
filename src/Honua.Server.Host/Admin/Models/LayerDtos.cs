// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new layer.
/// </summary>
public sealed record CreateLayerRequest
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public required string IdField { get; init; }
    public required string GeometryField { get; init; }
    public string? DisplayField { get; init; }
    public List<string> Crs { get; init; } = new() { "EPSG:4326" };
    public List<string> Keywords { get; init; } = new();
}

/// <summary>
/// Request to update an existing layer.
/// </summary>
public sealed record UpdateLayerRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? DisplayField { get; init; }
    public List<string> Crs { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
}

/// <summary>
/// Response containing layer details.
/// </summary>
public sealed record LayerResponse
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public required string IdField { get; init; }
    public required string GeometryField { get; init; }
    public string? DisplayField { get; init; }
    public List<string> Crs { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// Lightweight layer list item for list views.
/// </summary>
public sealed record LayerListItem
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public required string GeometryType { get; init; }
}
