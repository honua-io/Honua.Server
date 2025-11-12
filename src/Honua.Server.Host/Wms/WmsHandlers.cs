// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Main entry point for WMS operations. Delegates to specialized handlers.
/// </summary>
internal static class WmsHandlers
{
    /// <summary>
    /// Handles all WMS requests and routes them to the appropriate specialized handler.
    /// </summary>
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer rasterRenderer,
        [FromServices] IFeatureRepository featureRepository,
        [FromServices] IRasterTileCacheProvider cacheProvider,
        [FromServices] IRasterTileCacheMetrics cacheMetrics,
        [FromServices] IOptions<WmsOptions> wmsOptions,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(rasterRegistry);
        Guard.NotNull(rasterRenderer);
        Guard.NotNull(featureRepository);

        var request = context.Request;
        var query = request.Query;

        var service = QueryParsingHelpers.GetQueryValue(query, "service");
        if (!service.EqualsIgnoreCase("WMS"))
        {
            return WmsSharedHelpers.CreateException("InvalidParameterValue", "Parameter 'service' must be set to 'WMS'.");
        }

        var requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
        if (requestValue.IsNullOrWhiteSpace())
        {
            return WmsSharedHelpers.CreateException("MissingParameterValue", "Parameter 'request' is required.");
        }

        // WMS 1.3.0 Compliance: VERSION parameter validation
        // GetCapabilities may omit VERSION, but all other requests must specify VERSION=1.3.0
        var version = QueryParsingHelpers.GetQueryValue(query, "version");
        if (!requestValue.EqualsIgnoreCase("GetCapabilities"))
        {
            if (version.IsNullOrWhiteSpace())
            {
                return WmsSharedHelpers.CreateException("MissingParameterValue", "Parameter 'version' is required for WMS 1.3.0.");
            }
            if (!version.EqualsIgnoreCase("1.3.0"))
            {
                return WmsSharedHelpers.CreateException("InvalidParameterValue", $"Parameter 'version' must be '1.3.0'. Requested version '{version}' is not supported.");
            }
        }

        await metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return requestValue.Trim().ToUpperInvariant() switch
            {
                "GETCAPABILITIES" => await WmsCapabilitiesHandlers.HandleGetCapabilitiesAsync(request, snapshot, rasterRegistry, cancellationToken).ConfigureAwait(false),
                "GETMAP" => await WmsGetMapHandlers.HandleGetMapAsync(request, snapshot, rasterRegistry, rasterRenderer, cacheProvider, cacheMetrics, wmsOptions, cancellationToken).ConfigureAwait(false),
                "GETFEATUREINFO" => await WmsGetFeatureInfoHandlers.HandleGetFeatureInfoAsync(request, metadataRegistry, rasterRegistry, featureRepository, cancellationToken).ConfigureAwait(false),
                "DESCRIBELAYER" => await WmsDescribeLayerHandlers.HandleDescribeLayerAsync(request, snapshot, rasterRegistry, cancellationToken).ConfigureAwait(false),
                "GETLEGENDGRAPHIC" => await WmsGetLegendGraphicHandlers.HandleGetLegendGraphicAsync(request, snapshot, rasterRegistry, cancellationToken).ConfigureAwait(false),
                _ => WmsSharedHelpers.CreateException("OperationNotSupported", $"Request '{requestValue}' is not supported.")
            };
        }
        catch (InvalidOperationException ex)
        {
            return WmsSharedHelpers.CreateException("InvalidParameterValue", ex.Message);
        }
        catch (FormatException ex)
        {
            return WmsSharedHelpers.CreateException("InvalidParameterValue", ex.Message);
        }
    }
}
