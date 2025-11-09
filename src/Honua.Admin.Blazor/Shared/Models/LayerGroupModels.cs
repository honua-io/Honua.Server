// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to create a new layer group.
/// </summary>
public sealed class CreateLayerGroupRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("renderMode")]
    public string RenderMode { get; set; } = "Single";

    [JsonPropertyName("members")]
    public List<LayerGroupMemberDto> Members { get; set; } = new();

    [JsonPropertyName("defaultStyleId")]
    public string? DefaultStyleId { get; set; }

    [JsonPropertyName("styleIds")]
    public List<string> StyleIds { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("minScale")]
    public double? MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double? MaxScale { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("queryable")]
    public bool Queryable { get; set; } = true;
}

/// <summary>
/// Request to update an existing layer group.
/// </summary>
public sealed class UpdateLayerGroupRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("renderMode")]
    public string RenderMode { get; set; } = "Single";

    [JsonPropertyName("members")]
    public List<LayerGroupMemberDto> Members { get; set; } = new();

    [JsonPropertyName("defaultStyleId")]
    public string? DefaultStyleId { get; set; }

    [JsonPropertyName("styleIds")]
    public List<string> StyleIds { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("minScale")]
    public double? MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double? MaxScale { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("queryable")]
    public bool Queryable { get; set; } = true;
}

/// <summary>
/// Layer group response model.
/// </summary>
public sealed class LayerGroupResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("renderMode")]
    public string RenderMode { get; set; } = "Single";

    [JsonPropertyName("members")]
    public List<LayerGroupMemberDto> Members { get; set; } = new();

    [JsonPropertyName("defaultStyleId")]
    public string? DefaultStyleId { get; set; }

    [JsonPropertyName("styleIds")]
    public List<string> StyleIds { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("minScale")]
    public double? MinScale { get; set; }

    [JsonPropertyName("maxScale")]
    public double? MaxScale { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("queryable")]
    public bool Queryable { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Layer group list item (lightweight).
/// </summary>
public sealed class LayerGroupListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; set; }

    [JsonPropertyName("renderMode")]
    public string RenderMode { get; set; } = "Single";

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

/// <summary>
/// Layer group member DTO.
/// </summary>
public sealed class LayerGroupMemberDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Layer";

    [JsonPropertyName("layerId")]
    public string? LayerId { get; set; }

    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
