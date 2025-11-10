// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains tile handling methods for OGC API Tiles.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static int ResolveTileSize(HttpRequest request)
    {
        if (request.Query.TryGetValue("tileSize", out var sizeValues)
            && sizeValues.ToString().TryParseInt(out var parsed)
            && parsed > 0
            && parsed <= 2048)
        {
            return parsed;
        }

        return 256;
    }

    internal static string ResolveTileFormat(HttpRequest request)
    {
        string? raw = null;
        if (request.Query.TryGetValue("format", out var formatValues))
        {
            raw = formatValues.ToString();
        }
        else if (request.Query.TryGetValue("f", out var alternateValues))
        {
            raw = alternateValues.ToString();
        }

        return RasterFormatHelper.Normalize(raw);
    }

    internal static IReadOnlyList<object> BuildTileMatrixSetLinks(HttpRequest request, string collectionId, string tilesetId)
    {
        return new object[]
        {
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldCrs84QuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldCrs84QuadUri,
                crs = OgcTileMatrixHelper.WorldCrs84QuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldCrs84QuadId}", null, null)
            },
            new
            {
                tileMatrixSet = OgcTileMatrixHelper.WorldWebMercatorQuadId,
                tileMatrixSetUri = OgcTileMatrixHelper.WorldWebMercatorQuadUri,
                crs = OgcTileMatrixHelper.WorldWebMercatorQuadCrs,
                href = BuildHref(request, $"/ogc/collections/{collectionId}/tiles/{tilesetId}/{OgcTileMatrixHelper.WorldWebMercatorQuadId}", null, null)
            }
        };
    }

    internal static object BuildTileMatrixSetSummary(HttpRequest request, string id, string uri, string crs)
    {
        return new
        {
            id,
            title = id,
            tileMatrixSetUri = uri,
            crs,
            links = new[]
            {
                BuildLink(request, $"/ogc/tileMatrixSets/{id}", "self", "application/json", $"{id} definition")
            }
        };
    }

    internal static bool DatasetMatchesCollection(RasterDatasetDefinition dataset, ServiceDefinition service, LayerDefinition layer)
    {
        if (dataset.ServiceId.IsNullOrWhiteSpace() || !string.Equals(dataset.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (dataset.LayerId.HasValue() && !string.Equals(dataset.LayerId, layer.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal static (string Id, string Uri, string Crs)? NormalizeTileMatrixSet(string tileMatrixSetId)
    {
        if (OgcTileMatrixHelper.IsWorldCrs84Quad(tileMatrixSetId))
        {
            return (OgcTileMatrixHelper.WorldCrs84QuadId, OgcTileMatrixHelper.WorldCrs84QuadUri, OgcTileMatrixHelper.WorldCrs84QuadCrs);
        }

        if (OgcTileMatrixHelper.IsWorldWebMercatorQuad(tileMatrixSetId))
        {
            return (OgcTileMatrixHelper.WorldWebMercatorQuadId, OgcTileMatrixHelper.WorldWebMercatorQuadUri, OgcTileMatrixHelper.WorldWebMercatorQuadCrs);
        }

        return null;
    }

    internal static bool TryResolveStyle(RasterDatasetDefinition dataset, string? requestedStyleId, out string styleId, out string? unresolvedStyle)
    {
        var (success, resolvedStyleId, error) = StyleResolutionHelper.TryResolveRasterStyleId(dataset, requestedStyleId);
        styleId = resolvedStyleId ?? string.Empty;
        unresolvedStyle = success ? null : requestedStyleId;
        return success;
    }

    private sealed record SearchCollectionContext(string CollectionId, FeatureContext FeatureContext);

    internal static async Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        RasterDatasetDefinition dataset,
        string? requestedStyleId,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(dataset);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return StyleResolutionHelper.ResolveStyleForRaster(snapshot, dataset, requestedStyleId);
    }

    internal static async Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        string? styleId,
        LayerDefinition layer,
        IMetadataRegistry metadataRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(layer);
        Guard.NotNull(metadataRegistry);

        var snapshot = await metadataRegistry.GetInitializedSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return StyleResolutionHelper.ResolveStyleForLayer(snapshot, layer, styleId);
    }

internal static bool RequiresVectorOverlay(StyleDefinition? style)
{
    if (style?.GeometryType is not { } geometryType)
    {
        return false;
    }

    return !geometryType.Equals("raster", StringComparison.OrdinalIgnoreCase);
}

internal static async Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
    RasterDatasetDefinition dataset,
    double[] bbox,
    IMetadataRegistry metadataRegistry,
    IFeatureRepository repository,
    CancellationToken cancellationToken)
{
    Guard.NotNull(metadataRegistry);

    await metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
    var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

    return await CollectVectorGeometriesAsync(dataset, bbox, snapshot, repository, cancellationToken).ConfigureAwait(false);
}

internal static async Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
    RasterDatasetDefinition dataset,
    double[] bbox,
    MetadataSnapshot snapshot,
    IFeatureRepository repository,
    CancellationToken cancellationToken)
{
    if (bbox.Length < 4)
    {
        return Array.Empty<Geometry>();
    }

    if (dataset.ServiceId.IsNullOrWhiteSpace())
    {
        return Array.Empty<Geometry>();
    }

    var service = snapshot.Services.FirstOrDefault(s =>
        string.Equals(s.Id, dataset.ServiceId, StringComparison.OrdinalIgnoreCase));
    if (service is null || service.Layers.IsNullOrEmpty())
    {
        return Array.Empty<Geometry>();
    }

    LayerDefinition? targetLayer = null;
    if (dataset.LayerId.HasValue())
    {
        targetLayer = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, dataset.LayerId, StringComparison.OrdinalIgnoreCase));
    }

    targetLayer ??= service.Layers[0];
    if (targetLayer is null)
    {
        return Array.Empty<Geometry>();
    }

    var queryBbox = new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]);
    var targetCrs = dataset.Crs.FirstOrDefault() ?? service.Ogc.DefaultCrs;
    var geometries = new List<Geometry>();
    var reader = new GeoJsonReader();
    var offset = 0;
    var reachedLimit = false;

    while (!reachedLimit)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pagedQuery = new FeatureQuery(
            Limit: OverlayFetchBatchSize,
            Offset: offset,
            Bbox: queryBbox,
            ResultType: FeatureResultType.Results,
            Crs: targetCrs);

        var batchCount = 0;

        await foreach (var record in repository.QueryAsync(service.Id, targetLayer.Id, pagedQuery, cancellationToken).ConfigureAwait(false))
        {
            batchCount++;

            try
            {
                var components = FeatureComponentBuilder.BuildComponents(targetLayer, record, pagedQuery);
                if (components.GeometryNode is null)
                {
                    continue;
                }

                var geometry = reader.Read<Geometry>(components.GeometryNode.ToJsonString());
                if (geometry is not null && !geometry.IsEmpty)
                {
                    geometries.Add(geometry);
                    if (geometries.Count >= OverlayFetchMaxFeatures)
                    {
                        reachedLimit = true;
                        break;
                    }
                }
            }
            catch
            {
                // Ignore invalid geometries.
            }
        }

        if (reachedLimit || batchCount < OverlayFetchBatchSize)
        {
            break;
        }

        offset += OverlayFetchBatchSize;
    }

    return geometries.Count == 0
        ? Array.Empty<Geometry>()
        : new ReadOnlyCollection<Geometry>(geometries);
}

    internal static async Task<IResult> RenderVectorTileAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        RasterDatasetDefinition dataset,
        double[] bbox,
        int zoom,
        int tileRow,
        int tileCol,
        string? datetime,
        IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.LayerId.IsNullOrWhiteSpace())
        {
            return Results.File(Array.Empty<byte>(), "application/vnd.mapbox-vector-tile");
        }

        // BUG FIX #7: Check if provider supports MVT generation and return 501 if not
        var mvtBytes = await repository.GenerateMvtTileAsync(dataset.ServiceId, dataset.LayerId, zoom, tileCol, tileRow, datetime, cancellationToken).ConfigureAwait(false);
        if (mvtBytes is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Vector tiles not supported",
                detail: $"The data provider for dataset '{dataset.Id}' does not support native MVT tile generation.");
        }

        return Results.File(mvtBytes, "application/vnd.mapbox-vector-tile");
    }
    internal static double[] ResolveBounds(LayerDefinition layer, RasterDatasetDefinition? dataset)
    {
        if (dataset?.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = dataset.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        if (layer.Extent?.Bbox is { Count: > 0 })
        {
            var candidate = layer.Extent.Bbox[0];
            if (candidate.Length >= 4)
            {
                return new[] { candidate[0], candidate[1], candidate[2], candidate[3] };
            }
        }

        return new[] { -180d, -90d, 180d, 90d };
    }
}
