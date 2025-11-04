// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Handles WMS GetLegendGraphic operations.
/// Implements WMS 1.3.0 specification for legend generation.
/// </summary>
internal static class WmsGetLegendGraphicHandlers
{
    /// <summary>
    /// Handles the WMS GetLegendGraphic request.
    /// </summary>
    public static async Task<IResult> HandleGetLegendGraphicAsync(
        HttpRequest request,
        [FromServices] MetadataSnapshot snapshot,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(snapshot);
        Guard.NotNull(rasterRegistry);

        var layerName = QueryParsingHelpers.GetQueryValue(request.Query, "layer");
        if (layerName.IsNullOrWhiteSpace())
        {
            return WmsSharedHelpers.CreateException("MissingParameterValue", "Parameter 'layer' is required.");
        }

        var dataset = await WmsSharedHelpers.ResolveDatasetAsync(layerName, rasterRegistry, cancellationToken).ConfigureAwait(false);
        if (dataset is null)
        {
            return WmsSharedHelpers.CreateException("LayerNotDefined", $"Layer '{layerName}' was not found.");
        }

        // Parse optional width and height parameters
        var widthRaw = QueryParsingHelpers.GetQueryValue(request.Query, "width");
        var heightRaw = QueryParsingHelpers.GetQueryValue(request.Query, "height");
        int? width = widthRaw.HasValue() && int.TryParse(widthRaw, out var w) ? w : null;
        int? height = heightRaw.HasValue() && int.TryParse(heightRaw, out var h) ? h : null;

        // Resolve the requested style (optional STYLE parameter)
        var styleToken = QueryParsingHelpers.GetQueryValue(request.Query, "style");
        var styleId = WmsSharedHelpers.ResolveRequestedStyleId(dataset, styleToken);
        var styleDefinition = WmsSharedHelpers.ResolveStyleDefinition(snapshot, styleId);

        // WMS 1.3.0 Compliance: Generate proper legend graphic based on style definition
        var legendBytes = WmsLegendRenderer.GenerateLegend(dataset, styleDefinition, width, height);

        return Results.Bytes(legendBytes, "image/png");
    }
}
