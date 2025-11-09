// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new layer group.
/// </summary>
public sealed record CreateLayerGroupRequest
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ServiceId { get; init; }
    public string? Description { get; init; }
    public string RenderMode { get; init; } = "Single";
    public List<LayerGroupMemberDto> Members { get; init; } = new();
    public string? DefaultStyleId { get; init; }
    public List<string> StyleIds { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Queryable { get; init; } = true;
}

/// <summary>
/// Request to update an existing layer group.
/// </summary>
public sealed record UpdateLayerGroupRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string RenderMode { get; init; } = "Single";
    public List<LayerGroupMemberDto> Members { get; init; } = new();
    public string? DefaultStyleId { get; init; }
    public List<string> StyleIds { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Queryable { get; init; } = true;
}

/// <summary>
/// Response containing layer group details.
/// </summary>
public sealed record LayerGroupResponse
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ServiceId { get; init; }
    public string? Description { get; init; }
    public string RenderMode { get; init; } = "Single";
    public List<LayerGroupMemberDto> Members { get; init; } = new();
    public string? DefaultStyleId { get; init; }
    public List<string> StyleIds { get; init; } = new();
    public List<string> Keywords { get; init; } = new();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public bool Enabled { get; init; }
    public bool Queryable { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// Lightweight layer group list item for list views.
/// </summary>
public sealed record LayerGroupListItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string ServiceId { get; init; }
    public string RenderMode { get; init; } = "Single";
    public int MemberCount { get; init; }
    public bool Enabled { get; init; }
}

/// <summary>
/// Layer group member DTO (can be a layer or nested group).
/// </summary>
public sealed record LayerGroupMemberDto
{
    public string Type { get; init; } = "Layer";
    public string? LayerId { get; init; }
    public string? GroupId { get; init; }
    public int Order { get; init; }
    public double Opacity { get; init; } = 1.0;
    public string? StyleId { get; init; }
    public bool Enabled { get; init; } = true;
}
