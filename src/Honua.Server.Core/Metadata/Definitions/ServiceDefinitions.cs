// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.Server.Core.VectorTiles;

namespace Honua.Server.Core.Metadata;

public sealed record ServiceDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public required string ServiceType { get; init; }
    public required string DataSourceId { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Description { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public OgcServiceDefinition Ogc { get; init; } = new();
    public VectorTileOptions? VectorTileOptions { get; init; }
    public IReadOnlyList<LayerDefinition> Layers { get; init; } = Array.Empty<LayerDefinition>();
}

public sealed record OgcServiceDefinition
{
    // API Protocol Opt-ins (per-service level)
    // Note: These are only effective if the corresponding global setting is enabled
    // Default is false - APIs must be explicitly enabled per service
    public bool CollectionsEnabled { get; init; }  // OGC API Features
    public bool WfsEnabled { get; init; }
    public bool WmsEnabled { get; init; }
    public bool WmtsEnabled { get; init; }
    public bool CswEnabled { get; init; }
    public bool WcsEnabled { get; init; }

    // Export Format Opt-ins (per-service level)
    // Default is false - export formats must be explicitly enabled
    public ExportFormatsDefinition ExportFormats { get; init; } = new();

    // OGC API Features configuration
    public int? ItemLimit { get; init; }
    public string? DefaultCrs { get; init; }
    public IReadOnlyList<string> AdditionalCrs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConformanceClasses { get; init; } = Array.Empty<string>();

    // WFS configuration
    public IReadOnlyList<WfsStoredQueryDefinition> StoredQueries { get; init; } = Array.Empty<WfsStoredQueryDefinition>();
}

public sealed record ExportFormatsDefinition
{
    // All formats default to false - must be explicitly enabled per service
    public bool GeoJsonEnabled { get; init; } = true;  // GeoJSON is always safe, enabled by default
    public bool HtmlEnabled { get; init; } = true;     // HTML is read-only, enabled by default
    public bool CsvEnabled { get; init; }
    public bool KmlEnabled { get; init; }
    public bool KmzEnabled { get; init; }
    public bool ShapefileEnabled { get; init; }
    public bool GeoPackageEnabled { get; init; }
    public bool FlatGeobufEnabled { get; init; }
    public bool GeoArrowEnabled { get; init; }
    public bool GeoParquetEnabled { get; init; }
    public bool PmTilesEnabled { get; init; }
    public bool TopoJsonEnabled { get; init; }
}

public sealed record WfsStoredQueryDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Abstract { get; init; }
    public required string LayerId { get; init; }
    public required string FilterCql { get; init; }
    public IReadOnlyList<WfsStoredQueryParameterDefinition> Parameters { get; init; } = Array.Empty<WfsStoredQueryParameterDefinition>();
}

public sealed record WfsStoredQueryParameterDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Abstract { get; init; }
}
