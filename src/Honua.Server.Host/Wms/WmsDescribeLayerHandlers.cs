// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Handles WMS DescribeLayer operations.
/// </summary>
internal static class WmsDescribeLayerHandlers
{
    /// <summary>
    /// Handles the WMS DescribeLayer request.
    /// </summary>
    public static async Task<IResult> HandleDescribeLayerAsync(
        HttpRequest request,
        [FromServices] MetadataSnapshot snapshot,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(snapshot);
        Guard.NotNull(rasterRegistry);

        var layersRaw = QueryParsingHelpers.GetQueryValue(request.Query, "layers");
        if (layersRaw.IsNullOrWhiteSpace())
        {
            return WmsSharedHelpers.CreateException("MissingParameterValue", "Parameter 'layers' is required.");
        }

        var layerNames = QueryParsingHelpers.ParseCsv(layersRaw);
        if (layerNames.Count == 0)
        {
            return WmsSharedHelpers.CreateException("InvalidParameterValue", "Parameter 'layers' must include at least one entry.");
        }

        var endpoint = WmsSharedHelpers.BuildEndpointUrl(request);
        var wldNs = XNamespace.Get("http://www.opengis.net/wld");
        var ogcNs = XNamespace.Get("http://www.opengis.net/ogc");

        var layerDescriptions = new List<XElement>();
        foreach (var layerName in layerNames)
        {
            var dataset = await WmsSharedHelpers.ResolveDatasetAsync(layerName, rasterRegistry, cancellationToken).ConfigureAwait(false);
            if (dataset is null)
            {
                return WmsSharedHelpers.CreateException("LayerNotDefined", $"Layer '{layerName}' was not found.");
            }

            var canonicalName = WmsSharedHelpers.BuildLayerName(dataset);
            var layerDescription = new XElement(wldNs + "LayerDescription",
                new XAttribute("name", canonicalName),
                new XAttribute("wfs", endpoint.Replace("/wms", "/wfs")));

            if (dataset.ServiceId.HasValue() && dataset.LayerId.HasValue())
            {
                var owsType = "ft:" + dataset.LayerId;
                var owsName = dataset.ServiceId + ":" + dataset.LayerId;
                var queryElement = new XElement(wldNs + "Query",
                    new XAttribute("typeName", owsName));

                layerDescription.Add(queryElement);
            }

            layerDescriptions.Add(layerDescription);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(wldNs + "WMS_DescribeLayerResponse",
                new XAttribute("version", WmsSharedHelpers.Version),
                new XAttribute(XNamespace.Xmlns + "wld", wldNs),
                new XAttribute(XNamespace.Xmlns + "ogc", ogcNs),
                new XAttribute(XNamespace.Xmlns + "xlink", WmsSharedHelpers.XLink),
                layerDescriptions));

        var xml = document.ToString(SaveOptions.DisableFormatting);
        return Results.Content(xml, "application/xml; charset=utf-8");
    }
}
