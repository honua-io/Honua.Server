// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling OGC API Tiles operations including raster and vector tiles.
/// </summary>
internal interface IOgcTilesHandler
{
    /// <summary>
    /// Resolves the tile size from request parameters.
    /// </summary>
    int ResolveTileSize(HttpRequest request);

    /// <summary>
    /// Resolves the tile format from request parameters.
    /// </summary>
    string ResolveTileFormat(HttpRequest request);

    /// <summary>
    /// Builds a tile matrix set summary object.
    /// </summary>
    object BuildTileMatrixSetSummary(HttpRequest request, string id, string uri, string crs);

    /// <summary>
    /// Checks if a raster dataset matches the given collection.
    /// </summary>
    bool DatasetMatchesCollection(RasterDatasetDefinition dataset, ServiceDefinition service, LayerDefinition layer);

    /// <summary>
    /// Normalizes a tile matrix set identifier.
    /// </summary>
    (string Id, string Uri, string Crs)? NormalizeTileMatrixSet(string tileMatrixSetId);

    /// <summary>
    /// Tries to resolve a style ID for a raster dataset.
    /// </summary>
    bool TryResolveStyle(RasterDatasetDefinition dataset, string? requestedStyleId, out string styleId, out string? unresolvedStyle);

    /// <summary>
    /// Resolves a style definition for a raster dataset.
    /// </summary>
    Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        RasterDatasetDefinition dataset,
        string? requestedStyleId,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a style definition for a vector layer.
    /// </summary>
    Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        string? styleId,
        LayerDefinition layer,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a style requires vector overlay.
    /// </summary>
    bool RequiresVectorOverlay(StyleDefinition? style);

    /// <summary>
    /// Collects vector geometries for overlay on raster tiles.
    /// </summary>
    Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
        RasterDatasetDefinition dataset,
        double[] bbox,
        IMetadataRegistry metadataRegistry,
        IFeatureRepository repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renders a vector tile (MVT format).
    /// </summary>
    Task<IResult> RenderVectorTileAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        RasterDatasetDefinition dataset,
        double[] bbox,
        int zoom,
        int tileRow,
        int tileCol,
        string? datetime,
        IFeatureRepository repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves bounds for a layer/dataset.
    /// </summary>
    double[] ResolveBounds(LayerDefinition layer, RasterDatasetDefinition? dataset);
}
