// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Features;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Services for resolving tile metadata and data sources.
/// </summary>
public sealed record TileResolutionServices
{
    /// <summary>
    /// Resolves collection context from collection ID.
    /// </summary>
    public required IFeatureContextResolver ContextResolver { get; init; }

    /// <summary>
    /// Registry for raster dataset definitions and metadata.
    /// </summary>
    public required IRasterDatasetRegistry RasterRegistry { get; init; }

    /// <summary>
    /// Registry for feature metadata and layer definitions.
    /// </summary>
    public required IMetadataRegistry MetadataRegistry { get; init; }

    /// <summary>
    /// Repository for querying vector feature data.
    /// </summary>
    public required IFeatureRepository Repository { get; init; }
}
