// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Styling;
using Honua.Server.Host.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Handles WMS GetCapabilities operations.
/// </summary>
internal static class WmsCapabilitiesHandlers
{
    /// <summary>
    /// Handles the WMS GetCapabilities request.
    /// </summary>
    public static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpRequest request,
        [FromServices] MetadataSnapshot snapshot,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] ICapabilitiesCache capabilitiesCache,
        CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "WMS GetCapabilities",
            [("wms.operation", "GetCapabilities")],
            async activity =>
            {
                var datasets = await rasterRegistry.GetAllAsync(cancellationToken).ConfigureAwait(false);
                activity.AddTag("wms.dataset_count", datasets.Count);

                // Extract version and language from request
                var query = request.Query;
                var version = QueryParsingHelpers.GetQueryValue(query, "version") ?? "1.3.0";
                var acceptLanguage = request.Headers["Accept-Language"].FirstOrDefault();

                // Try to get from cache
                if (capabilitiesCache.TryGetCapabilities("wms", "global", version, acceptLanguage, out var cachedXml))
                {
                    activity.AddTag("wms.cache_hit", true);
                    return Results.Content(cachedXml, "application/xml");
                }

                activity.AddTag("wms.cache_hit", false);

                // Cache miss - generate capabilities
                var builder = new WmsCapabilitiesBuilder(rasterRegistry);
                var xml = await builder.BuildCapabilitiesAsync(snapshot, request, cancellationToken).ConfigureAwait(false);

                // Store in cache
                await capabilitiesCache.SetCapabilitiesAsync("wms", "global", version, acceptLanguage, xml, cancellationToken)
                    .ConfigureAwait(false);

                return Results.Content(xml, "application/xml");
            }).ConfigureAwait(false);
}
