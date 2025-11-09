// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Defines a layer group - a renderable composite of multiple layers served as a single unit.
/// Layer groups are one of GeoServer's most popular features, allowing multiple layers to be
/// combined and served as a unified layer in WMS, WFS, and OGC API Features.
/// </summary>
public sealed record LayerGroupDefinition
{
    /// <summary>
    /// Unique identifier for the layer group.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display title for the layer group.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Service that this layer group belongs to.
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Optional description of the layer group.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this layer group is enabled and available.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Render mode for the layer group composite.
    /// </summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Single;

    /// <summary>
    /// Ordered list of members (layers or nested groups) in this group.
    /// Members are rendered in order, with the first member at the bottom.
    /// </summary>
    public IReadOnlyList<LayerGroupMember> Members { get; init; } = Array.Empty<LayerGroupMember>();

    /// <summary>
    /// Spatial extent/bounding box for the layer group.
    /// If not specified, it will be calculated from member layers.
    /// </summary>
    public LayerExtentDefinition? Extent { get; init; }

    /// <summary>
    /// Keywords for metadata and discovery.
    /// </summary>
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Related links (documentation, metadata, etc.).
    /// </summary>
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();

    /// <summary>
    /// Catalog metadata for this layer group.
    /// </summary>
    public CatalogEntryDefinition Catalog { get; init; } = new();

    /// <summary>
    /// Coordinate reference systems supported by this group.
    /// If not specified, inherited from member layers.
    /// </summary>
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Default style to apply to the entire group (optional).
    /// Individual member styles take precedence.
    /// </summary>
    public string? DefaultStyleId { get; init; }

    /// <summary>
    /// Available styles for this group.
    /// </summary>
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Minimum scale denominator for visibility (0 = always visible at small scales).
    /// </summary>
    public double? MinScale { get; init; }

    /// <summary>
    /// Maximum scale denominator for visibility (0 = always visible at large scales).
    /// </summary>
    public double? MaxScale { get; init; }

    /// <summary>
    /// Whether this group can be queried (GetFeatureInfo, GetFeature).
    /// </summary>
    public bool Queryable { get; init; } = true;

    /// <summary>
    /// WMS-specific configuration.
    /// </summary>
    public LayerGroupWmsDefinition Wms { get; init; } = new();
}

/// <summary>
/// Represents a member of a layer group (can be a layer or another group for nesting).
/// </summary>
public sealed record LayerGroupMember
{
    /// <summary>
    /// Type of member (Layer or Group).
    /// </summary>
    public required LayerGroupMemberType Type { get; init; }

    /// <summary>
    /// ID of the layer (when Type = Layer).
    /// </summary>
    public string? LayerId { get; init; }

    /// <summary>
    /// ID of the nested group (when Type = Group).
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Display order within the group (0-based, lower numbers render first/bottom).
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Opacity for this member (0.0 = transparent, 1.0 = opaque).
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// Optional style override for this member.
    /// If not specified, the layer's default style is used.
    /// </summary>
    public string? StyleId { get; init; }

    /// <summary>
    /// Whether this member is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the referenced ID (either LayerId or GroupId).
    /// </summary>
    public string GetReferencedId() => Type switch
    {
        LayerGroupMemberType.Layer => LayerId ?? throw new InvalidOperationException("LayerId is required when Type is Layer"),
        LayerGroupMemberType.Group => GroupId ?? throw new InvalidOperationException("GroupId is required when Type is Group"),
        _ => throw new InvalidOperationException($"Unknown LayerGroupMemberType: {Type}")
    };
}

/// <summary>
/// Type of layer group member.
/// </summary>
public enum LayerGroupMemberType
{
    /// <summary>
    /// Member is a regular layer.
    /// </summary>
    Layer,

    /// <summary>
    /// Member is a nested layer group.
    /// </summary>
    Group
}

/// <summary>
/// Render mode for layer group composites.
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// Single mode: Render all layers as a single composite image.
    /// This is the most efficient mode for WMS tile caching.
    /// </summary>
    Single,

    /// <summary>
    /// Opaque mode: Render layers as separate images with opaque backgrounds.
    /// </summary>
    Opaque,

    /// <summary>
    /// Transparent mode: Render layers as separate transparent images.
    /// Allows for more flexible client-side composition.
    /// </summary>
    Transparent
}

/// <summary>
/// WMS-specific configuration for layer groups.
/// </summary>
public sealed record LayerGroupWmsDefinition
{
    /// <summary>
    /// Whether to advertise this group in WMS GetCapabilities.
    /// </summary>
    public bool AdvertiseInCapabilities { get; init; } = true;

    /// <summary>
    /// Whether the group can be requested directly via GetMap.
    /// </summary>
    public bool AllowDirectRequest { get; init; } = true;

    /// <summary>
    /// Whether to expand the group into individual layers in GetCapabilities,
    /// or show it as a single named layer.
    /// </summary>
    public bool ShowAsNamedLayer { get; init; } = true;

    /// <summary>
    /// Attribution text for WMS.
    /// </summary>
    public string? Attribution { get; init; }

    /// <summary>
    /// Authority URL namespace.
    /// </summary>
    public string? AuthorityUrl { get; init; }

    /// <summary>
    /// Authority identifier.
    /// </summary>
    public string? AuthorityId { get; init; }
}
