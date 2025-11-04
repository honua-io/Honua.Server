// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Helper class for parsing and resolving raster export request parameters shared across
/// ImageServer and MapServer export endpoints.
/// </summary>
internal static class GeoservicesRESTRasterExportHelper
{
    /// <summary>
    /// Result of parsing raster export parameters from the HTTP request.
    /// Contains all necessary information to render a raster image.
    /// </summary>
    public sealed record RasterExportParameters(
        double[] Bbox,
        int Width,
        int Height,
        string Format,
        bool Transparent,
        string SourceCrs,
        string TargetCrs,
        string? StyleId,
        RasterDatasetDefinition Dataset);

    /// <summary>
    /// Parses and resolves all raster export parameters from the HTTP request.
    /// Returns the parsed parameters or an error IActionResult if validation fails.
    /// </summary>
    public static async Task<(RasterExportParameters? Parameters, IActionResult? Error)> TryParseExportRequestAsync(
        HttpRequest request,
        CatalogServiceView serviceView,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken,
        Func<RasterDatasetDefinition, bool>? datasetFilter = null,
        Func<CatalogServiceView, RasterDatasetDefinition?>? fallbackDatasetFactory = null)
    {
        // Parse bbox parameter
        if (!GeoservicesRESTRasterRequestParser.TryParseBoundingBox(request.Query["bbox"].ToString(), out var bbox))
        {
            IActionResult error = GeoservicesRESTErrorHelper.BadRequest("Parameter 'bbox' must be provided as xmin,ymin,xmax,ymax.");
            return (null, error);
        }

        // Parse size parameter
        if (!GeoservicesRESTRasterRequestParser.TryParseSize(request.Query["size"].ToString(), out var width, out var height))
        {
            IActionResult error = GeoservicesRESTErrorHelper.BadRequest("Parameter 'size' must be provided as width,height.");
            return (null, error);
        }

        // Resolve format
        var format = GeoservicesRESTRasterRequestParser.ResolveRasterFormat(request);
        var transparent = !request.Query["transparent"].ToString().EqualsIgnoreCase("false");

        // Locate dataset (with optional fallback)
        var rasterId = request.Query["rasterId"].ToString();
        var dataset = await ResolveDatasetAsync(serviceView, rasterRegistry, rasterId, datasetFilter, cancellationToken).ConfigureAwait(false);

        if (dataset is null && fallbackDatasetFactory is not null)
        {
            dataset = fallbackDatasetFactory(serviceView);
        }

        if (dataset is null)
        {
            IActionResult error = new NotFoundObjectResult(new { error = "No raster datasets registered for this service." });
            return (null, error);
        }

        // Resolve CRS
        var sourceCrs = dataset.Crs.Count > 0 ? dataset.Crs[0] : serviceView.Service.Ogc.DefaultCrs ?? "EPSG:4326";
        var targetCrs = request.Query["bboxSR"].ToString();
        if (string.IsNullOrWhiteSpace(targetCrs))
        {
            targetCrs = request.Query["imageSR"].ToString();
        }

        if (string.IsNullOrWhiteSpace(targetCrs))
        {
            targetCrs = sourceCrs;
        }

        // Resolve style
        var styleIdRaw = request.Query.TryGetValue("styleId", out var styleValues) ? styleValues.ToString() : null;
        if (!GeoservicesRESTRasterRequestParser.TryResolveStyle(dataset, styleIdRaw, out var styleId, out var unresolvedStyle))
        {
            IActionResult error = GeoservicesRESTErrorHelper.BadRequest($"Style '{unresolvedStyle}' is not defined for raster dataset '{dataset.Id}'.");
            return (null, error);
        }

        var parameters = new RasterExportParameters(
            bbox,
            width,
            height,
            format,
            transparent,
            sourceCrs,
            targetCrs,
            styleId,
            dataset);

        return (parameters, null);
    }

    /// <summary>
    /// Resolves the style definition for a raster dataset.
    /// Tries requested style first, then default style, then any available style.
    /// </summary>
    public static async Task<StyleDefinition?> ResolveStyleDefinitionAsync(
        IMetadataRegistry metadataRegistry,
        RasterDatasetDefinition dataset,
        string? requestedStyleId,
        CancellationToken cancellationToken)
    {
        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(requestedStyleId) && snapshot.TryGetStyle(requestedStyleId, out var requested))
        {
            return requested;
        }

        if (!string.IsNullOrWhiteSpace(dataset.Styles.DefaultStyleId) && snapshot.TryGetStyle(dataset.Styles.DefaultStyleId, out var defaultStyle))
        {
            return defaultStyle;
        }

        foreach (var candidate in dataset.Styles.StyleIds)
        {
            if (snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a raster dataset for the service, optionally by ID, with fallback to first available dataset.
    /// </summary>
    private static async Task<RasterDatasetDefinition?> ResolveDatasetAsync(
        CatalogServiceView serviceView,
        IRasterDatasetRegistry rasterRegistry,
        string? rasterId,
        Func<RasterDatasetDefinition, bool>? datasetFilter,
        CancellationToken cancellationToken)
    {
        RasterDatasetDefinition? dataset = null;

        // Try to find by explicit ID
        if (!string.IsNullOrWhiteSpace(rasterId))
        {
            dataset = await rasterRegistry.FindAsync(rasterId, cancellationToken).ConfigureAwait(false);

            // Apply filter if provided and dataset was found
            if (dataset is not null && datasetFilter is not null && !datasetFilter(dataset))
            {
                dataset = null;
            }
        }

        // Fall back to first available dataset for the service
        if (dataset is null)
        {
            var datasets = await rasterRegistry.GetByServiceAsync(serviceView.Service.Id, cancellationToken).ConfigureAwait(false);
            dataset = datasetFilter is not null
                ? datasets.FirstOrDefault(datasetFilter)
                : datasets.FirstOrDefault();
        }

        return dataset;
    }
}
