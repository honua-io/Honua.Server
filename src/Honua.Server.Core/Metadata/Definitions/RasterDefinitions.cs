// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

public sealed record RasterDatasetDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ServiceId { get; init; }
    public string? LayerId { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public LayerExtentDefinition? Extent { get; init; }
    public required RasterSourceDefinition Source { get; init; }
    public RasterStyleDefinition Styles { get; init; } = new();
    public RasterCacheDefinition Cache { get; init; } = new();
    public RasterTemporalDefinition Temporal { get; init; } = RasterTemporalDefinition.Disabled;
    public RasterCdnDefinition Cdn { get; init; } = RasterCdnDefinition.Disabled;
    public StacMetadata? Stac { get; init; }
    public DateTimeOffset? Datetime { get; init; }
}

public sealed record RasterSourceDefinition
{
    public required string Type { get; init; }
    public required string Uri { get; init; }
    public string? MediaType { get; init; }
    public string? CredentialsId { get; init; }
    public bool? DisableHttpRangeRequests { get; init; }
}

public sealed record RasterStyleDefinition
{
    public string? DefaultStyleId { get; init; }
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();
}

public sealed record RasterCacheDefinition
{
    public bool Enabled { get; init; } = true;
    public bool Preseed { get; init; }
    public IReadOnlyList<int> ZoomLevels { get; init; } = Array.Empty<int>();
}

public sealed record RasterTemporalDefinition
{
    public static RasterTemporalDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? FixedValues { get; init; }
    public string? MinValue { get; init; }
    public string? MaxValue { get; init; }
    public string? Period { get; init; } // e.g., "P1D" for 1 day interval
}

public sealed record RasterCdnDefinition
{
    public static RasterCdnDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? Policy { get; init; } // "NoCache", "ShortLived", "MediumLived", "LongLived", "VeryLongLived", "Immutable"
    public int? MaxAge { get; init; }
    public int? SharedMaxAge { get; init; }
    public bool? Public { get; init; }
    public bool? Immutable { get; init; }
    public bool? MustRevalidate { get; init; }
    public bool? NoStore { get; init; }
    public bool? NoTransform { get; init; }
    public int? StaleWhileRevalidate { get; init; }
    public int? StaleIfError { get; init; }
}
