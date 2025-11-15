// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

public sealed record CatalogDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Publisher { get; init; }
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ThemeCategories { get; init; } = Array.Empty<string>();
    public CatalogContactDefinition? Contact { get; init; }
    public CatalogLicenseDefinition? License { get; init; }
    public CatalogExtentDefinition? Extents { get; init; }
}

public sealed record CatalogEntryDefinition
{
    public string? Summary { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CatalogContactDefinition> Contacts { get; init; } = Array.Empty<CatalogContactDefinition>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public string? Thumbnail { get; init; }
    public int? Ordering { get; init; }
    public CatalogSpatialExtentDefinition? SpatialExtent { get; init; }
    public CatalogTemporalExtentDefinition? TemporalExtent { get; init; }
}

public sealed record CatalogContactDefinition
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Organization { get; init; }
    public string? Phone { get; init; }
    public string? Url { get; init; }
    public string? Role { get; init; }
}

public sealed record CatalogLicenseDefinition
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

public sealed record CatalogExtentDefinition
{
    public CatalogSpatialExtentDefinition? Spatial { get; init; }
    public CatalogTemporalCollectionDefinition? Temporal { get; init; }
}

public sealed record CatalogSpatialExtentDefinition
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
}

public sealed record CatalogTemporalCollectionDefinition
{
    public IReadOnlyList<CatalogTemporalExtentDefinition> Interval { get; init; } = Array.Empty<CatalogTemporalExtentDefinition>();
    public string? TemporalReferenceSystem { get; init; }
}

public sealed record CatalogTemporalExtentDefinition
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
