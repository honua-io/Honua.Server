// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.Server.Core.Query.Filter;

namespace Honua.Server.Core.Metadata;

public sealed record LayerDefinition
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public required string IdField { get; init; }
    public string? DisplayField { get; init; }
    public required string GeometryField { get; init; }
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();
    public LayerExtentDefinition? Extent { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public LayerQueryDefinition Query { get; init; } = new();
    public LayerEditingDefinition Editing { get; init; } = LayerEditingDefinition.Disabled;
    public LayerAttachmentDefinition Attachments { get; init; } = LayerAttachmentDefinition.Disabled;
    public LayerStorageDefinition? Storage { get; init; }
    public SqlViewDefinition? SqlView { get; init; }
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = Array.Empty<FieldDefinition>();
    public string ItemType { get; init; } = "feature";
    public string? DefaultStyleId { get; init; }
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LayerRelationshipDefinition> Relationships { get; init; } = Array.Empty<LayerRelationshipDefinition>();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public LayerTemporalDefinition Temporal { get; init; } = LayerTemporalDefinition.Disabled;
    public OpenRosa.OpenRosaLayerDefinition? OpenRosa { get; init; }
    public Iso19115Metadata? Iso19115 { get; init; }
    public StacMetadata? Stac { get; init; }
    public bool HasZ { get; init; }
    public bool HasM { get; init; }
    public string? ZField { get; init; }
}

public sealed record LayerExtentDefinition
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
    public IReadOnlyList<TemporalIntervalDefinition> Temporal { get; init; } = Array.Empty<TemporalIntervalDefinition>();
    public string? TemporalReferenceSystem { get; init; }
}

public sealed record LayerQueryDefinition
{
    public int? MaxRecordCount { get; init; }
    public IReadOnlyList<string> SupportedParameters { get; init; } = Array.Empty<string>();
    public LayerQueryFilterDefinition? AutoFilter { get; init; }
}

public sealed record LayerQueryFilterDefinition
{
    public string? Cql { get; init; }
    public QueryFilter? Expression { get; init; }
}

public sealed record LayerTemporalDefinition
{
    public static LayerTemporalDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? StartField { get; init; }
    public string? EndField { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? FixedValues { get; init; }
    public string? MinValue { get; init; }
    public string? MaxValue { get; init; }
    public string? Period { get; init; } // e.g., "P1D" for 1 day interval
}

public sealed record LayerRelationshipDefinition
{
    public int Id { get; init; }
    public string Role { get; init; } = "esriRelRoleOrigin";
    public string Cardinality { get; init; } = "esriRelCardinalityOneToMany";
    public required string RelatedLayerId { get; init; }
    public string? RelatedTableId { get; init; }
    public required string KeyField { get; init; }
    public required string RelatedKeyField { get; init; }
    public bool? Composite { get; init; }
    public bool? ReturnGeometry { get; init; }
    public LayerRelationshipSemantics Semantics { get; init; } = LayerRelationshipSemantics.Unknown;
}

public enum LayerRelationshipSemantics
{
    Unknown,
    PrimaryKeyForeignKey
}

public sealed record LayerStorageDefinition
{
    public string? Table { get; init; }
    public string? GeometryColumn { get; init; }
    public string? PrimaryKey { get; init; }
    public string? TemporalColumn { get; init; }
    public int? Srid { get; init; }
    public string? Crs { get; init; }
    public bool HasZ { get; init; }
    public bool HasM { get; init; }
}
