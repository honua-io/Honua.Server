// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wms;

/// <summary>
/// Handles WMS GetFeatureInfo operations.
/// </summary>
internal static class WmsGetFeatureInfoHandlers
{
    private sealed record WmsLayerContext(RasterDatasetDefinition Dataset, string RequestedLayerName, string CanonicalLayerName);

    /// <summary>
    /// Handles the WMS GetFeatureInfo request.
    /// </summary>
    public static async Task<IResult> HandleGetFeatureInfoAsync(
        HttpRequest request,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IFeatureRepository featureRepository,
        CancellationToken cancellationToken) =>
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "WMS GetFeatureInfo",
            [("wms.operation", "GetFeatureInfo")],
            async activity =>
            {
                var query = request.Query;
                var layerContexts = await ResolveDatasetContextsAsync(query, rasterRegistry, cancellationToken).ConfigureAwait(false);
                var targetContext = layerContexts[0];
                var dataset = targetContext.Dataset;
                var requestedLayerName = targetContext.RequestedLayerName;
                var canonicalLayerName = targetContext.CanonicalLayerName;

                var queryLayersRaw = QueryParsingHelpers.GetQueryValue(query, "query_layers");
                if (queryLayersRaw.HasValue())
                {
                    var queryLayers = QueryParsingHelpers.ParseCsv(queryLayersRaw);
                    if (queryLayers.Count != 1)
                    {
                        throw new InvalidOperationException("Honua WMS MVP supports exactly one query layer per request.");
                    }

                    var queryLayer = queryLayers[0];
                    var matchedContext = FindLayerContext(layerContexts, queryLayer);
                    if (matchedContext is null)
                    {
                        throw new InvalidOperationException($"Layer '{queryLayer}' is not available for feature info on '{requestedLayerName}'.");
                    }

                    targetContext = matchedContext;
                    dataset = targetContext.Dataset;
                    requestedLayerName = queryLayer;
                    canonicalLayerName = targetContext.CanonicalLayerName;
                }

                if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.LayerId.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException($"Layer '{requestedLayerName}' is not linked to a feature layer for GetFeatureInfo.");
                }

                // Parse CRS first, then parse bbox with correct axis order for the CRS
                var targetCrs = CrsNormalizationHelper.NormalizeForWms(QueryParsingHelpers.GetQueryValue(query, "crs") ?? QueryParsingHelpers.GetQueryValue(query, "srs"));

                var bbox = WmsSharedHelpers.ParseBoundingBox(QueryParsingHelpers.GetQueryValue(query, "bbox"), targetCrs);
                var width = WmsSharedHelpers.ParsePositiveInt(query, "width");
                var height = WmsSharedHelpers.ParsePositiveInt(query, "height");

                var pixelX = ParsePixelCoordinate(query, width, "i", "x", "column");
                var pixelY = ParsePixelCoordinate(query, height, "j", "y", "row");
                var featureCount = ParseFeatureCount(query);
                var infoFormat = NormalizeInfoFormat(QueryParsingHelpers.GetQueryValue(query, "info_format"));

                var xSpan = bbox[2] - bbox[0];
                var ySpan = bbox[3] - bbox[1];
                if (xSpan <= 0d || ySpan <= 0d)
                {
                    throw new InvalidOperationException("Parameter 'bbox' must define a valid extent.");
                }

                var pixelWidth = xSpan / width;
                var pixelHeight = ySpan / height;

                var coordinateX = bbox[0] + (pixelX + 0.5d) * pixelWidth;
                var coordinateY = bbox[1] + (pixelY + 0.5d) * pixelHeight;

                var halfWidth = Math.Max(pixelWidth * 0.5d, 1e-9);
                var halfHeight = Math.Max(pixelHeight * 0.5d, 1e-9);

                var searchMinX = Math.Max(coordinateX - halfWidth, bbox[0]);
                var searchMaxX = Math.Min(coordinateX + halfWidth, bbox[2]);
                var searchMinY = Math.Max(coordinateY - halfHeight, bbox[1]);
                var searchMaxY = Math.Min(coordinateY + halfHeight, bbox[3]);

                if (searchMaxX <= searchMinX)
                {
                    searchMinX = Math.Max(coordinateX - pixelWidth, bbox[0]);
                    searchMaxX = Math.Min(coordinateX + pixelWidth, bbox[2]);
                }

                if (searchMaxY <= searchMinY)
                {
                    searchMinY = Math.Max(coordinateY - pixelHeight, bbox[1]);
                    searchMaxY = Math.Min(coordinateY + pixelHeight, bbox[3]);
                }

                if (searchMaxX <= searchMinX)
                {
                    searchMaxX = coordinateX + 1e-9;
                    searchMinX = coordinateX - 1e-9;
                }

                if (searchMaxY <= searchMinY)
                {
                    searchMaxY = coordinateY + 1e-9;
                    searchMinY = coordinateY - 1e-9;
                }

                // Parse and apply TIME parameter for temporal filtering
                // Validate TIME parameter and apply dataset default if needed
                var rawTimeValue = QueryParsingHelpers.GetQueryValue(query, "time");
                var validatedTimeValue = dataset.Temporal.Enabled
                    ? WmsSharedHelpers.ValidateTimeParameter(rawTimeValue, dataset.Temporal)
                    : rawTimeValue;
                var temporalInterval = WmsSharedHelpers.ParseTemporalInterval(validatedTimeValue);

                var queryBbox = new BoundingBox(searchMinX, searchMinY, searchMaxX, searchMaxY, null, null, targetCrs);
                var featureQuery = new FeatureQuery(Limit: featureCount, Bbox: queryBbox, Temporal: temporalInterval, Crs: targetCrs);

                var features = new List<IDictionary<string, object?>>(featureCount);
                await foreach (var feature in featureRepository.QueryAsync(dataset.ServiceId!, dataset.LayerId!, featureQuery, cancellationToken).ConfigureAwait(false))
                {
                    features.Add(new Dictionary<string, object?>(feature.Attributes, StringComparer.OrdinalIgnoreCase));
                    if (features.Count >= featureCount)
                    {
                        break;
                    }
                }

                var jsonPayload = new
                {
                    type = "FeatureInfo",
                    dataset = dataset.Id,
                    service = dataset.ServiceId,
                    layer = dataset.LayerId,
                    coordinate = new { x = coordinateX, y = coordinateY, crs = targetCrs },
                    features
                };

                return infoFormat switch
                {
                    "application/json" => Results.Json(jsonPayload, contentType: "application/json"),
                    "application/geo+json" => Results.Json(BuildGeoJsonFeatureInfo(dataset, targetCrs, coordinateX, coordinateY, features), contentType: "application/geo+json"),
                    "application/xml" => BuildXmlFeatureInfo(dataset, targetCrs, coordinateX, coordinateY, features),
                    "text/html" => Results.Content(BuildHtmlFeatureInfo(dataset, targetCrs, coordinateX, coordinateY, features), "text/html; charset=utf-8"),
                    "text/plain" => Results.Text(BuildPlainTextFeatureInfo(dataset, targetCrs, coordinateX, coordinateY, features), "text/plain"),
                    _ => throw new InvalidOperationException($"INFO_FORMAT '{infoFormat}' is not supported.")
                };
            }).ConfigureAwait(false);

    private static async Task<IReadOnlyList<WmsLayerContext>> ResolveDatasetContextsAsync(
        IQueryCollection query,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        var layersRaw = QueryParsingHelpers.GetQueryValue(query, "layers");
        if (layersRaw.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Parameter 'layers' is required.");
        }

        var layerNames = QueryParsingHelpers.ParseCsv(layersRaw);
        if (layerNames.Count == 0)
        {
            throw new InvalidOperationException("Parameter 'layers' must include at least one entry.");
        }

        var contexts = new List<WmsLayerContext>(layerNames.Count);
        foreach (var requestedLayerName in layerNames)
        {
            var dataset = await WmsSharedHelpers.ResolveDatasetAsync(requestedLayerName, rasterRegistry, cancellationToken).ConfigureAwait(false);
            if (dataset is null)
            {
                throw new InvalidOperationException($"Layer '{requestedLayerName}' was not found.");
            }

            var canonicalLayerName = WmsSharedHelpers.BuildLayerName(dataset);
            contexts.Add(new WmsLayerContext(dataset, requestedLayerName, canonicalLayerName));
        }

        return contexts;
    }

    private static WmsLayerContext? FindLayerContext(IReadOnlyList<WmsLayerContext> contexts, string layerName)
    {
        foreach (var context in contexts)
        {
            if (context.RequestedLayerName.EqualsIgnoreCase(layerName) ||
                context.CanonicalLayerName.EqualsIgnoreCase(layerName))
            {
                return context;
            }
        }

        return null;
    }

    private static int ParsePixelCoordinate(IQueryCollection query, int max, params string[] keys)
    {
        foreach (var key in keys)
        {
            var raw = QueryParsingHelpers.GetQueryValue(query, key);
            if (raw.IsNullOrWhiteSpace())
            {
                continue;
            }

            if (!raw.TryParseInt(out var parsed) || parsed < 0)
            {
                throw new InvalidOperationException($"Parameter '{key}' must be a non-negative integer.");
            }

            if (parsed >= max)
            {
                throw new InvalidOperationException($"Parameter '{key}' must be less than {max}.");
            }

            return parsed;
        }

        throw new InvalidOperationException($"One of the parameters '{string.Join("','", keys)}' is required.");
    }

    private static int ParseFeatureCount(IQueryCollection query)
    {
        var raw = QueryParsingHelpers.GetQueryValue(query, "feature_count");
        if (raw.IsNullOrWhiteSpace())
        {
            return 1;
        }

        if (!raw.TryParseInt(out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException("Parameter 'feature_count' must be a positive integer.");
        }

        return Math.Min(parsed, WmsSharedHelpers.MaxFeatureInfoCount);
    }

    private static string NormalizeInfoFormat(string? infoFormat)
    {
        if (infoFormat.IsNullOrWhiteSpace())
        {
            return "application/json";
        }

        var candidate = infoFormat.Trim();
        candidate = candidate.Replace(' ', '+');
        var delimiterIndex = candidate.IndexOf(';');
        if (delimiterIndex >= 0)
        {
            candidate = candidate.Substring(0, delimiterIndex).Trim();
        }

        if (candidate.EqualsIgnoreCase("application/json")
            || candidate.EqualsIgnoreCase("json"))
        {
            return "application/json";
        }

        if (candidate.EqualsIgnoreCase("application/geo+json")
            || candidate.EqualsIgnoreCase("geojson"))
        {
            return "application/geo+json";
        }

        if (candidate.EqualsIgnoreCase("application/xml")
            || candidate.EqualsIgnoreCase("text/xml")
            || candidate.EqualsIgnoreCase("application/vnd.ogc.gml"))
        {
            return "application/xml";
        }

        if (candidate.EqualsIgnoreCase("text/html")
            || candidate.EqualsIgnoreCase("application/xhtml+xml"))
        {
            return "text/html";
        }

        if (candidate.EqualsIgnoreCase("text/plain"))
        {
            return "text/plain";
        }

        throw new InvalidOperationException($"INFO_FORMAT '{infoFormat}' is not supported. Supported values: application/json, application/geo+json, application/xml, text/html, text/plain.");
    }

    private static object BuildGeoJsonFeatureInfo(
        RasterDatasetDefinition dataset,
        string crs,
        double x,
        double y,
        IReadOnlyList<IDictionary<string, object?>> features)
    {
        var featureItems = features.Select((attributes, index) => new
        {
            type = "Feature",
            id = index + 1,
            geometry = (object?)null,
            properties = attributes
        });

        return new
        {
            type = "FeatureCollection",
            dataset = dataset.Id,
            layer = dataset.LayerId,
            coordinate = new { x, y, crs },
            features = featureItems
        };
    }

    private static IResult BuildXmlFeatureInfo(
        RasterDatasetDefinition dataset,
        string crs,
        double x,
        double y,
        IReadOnlyList<IDictionary<string, object?>> features)
    {
        var root = new XElement("FeatureInfo",
            new XAttribute("dataset", dataset.Id),
            new XAttribute("crs", crs));

        if (dataset.LayerId.HasValue())
        {
            root.SetAttributeValue("layer", dataset.LayerId);
        }

        root.Add(new XElement("Coordinate",
            new XAttribute("x", WmsSharedHelpers.FormatDouble(x)),
            new XAttribute("y", WmsSharedHelpers.FormatDouble(y))));

        if (features.Count == 0)
        {
            root.Add(new XElement("Message", "No features found."));
        }
        else
        {
            var featuresElement = new XElement("Features");
            for (var i = 0; i < features.Count; i++)
            {
                var featureElement = new XElement("Feature", new XAttribute("index", i + 1));
                foreach (var kvp in features[i])
                {
                    featureElement.Add(new XElement("Property",
                        new XAttribute("name", kvp.Key),
                        FormatFeatureValue(kvp.Value)));
                }

                featuresElement.Add(featureElement);
            }

            root.Add(featuresElement);
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var xml = document.ToString(SaveOptions.DisableFormatting);
        return Results.Content(xml, "application/xml");
    }

    private static string BuildHtmlFeatureInfo(
        RasterDatasetDefinition dataset,
        string crs,
        double x,
        double y,
        IReadOnlyList<IDictionary<string, object?>> features)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Feature Info</title>");
        builder.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:1.5rem;}table{border-collapse:collapse;margin-bottom:1rem;}th,td{border:1px solid #ccc;padding:0.35rem 0.6rem;text-align:left;}th{background:#f5f5f5;}</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h1>Feature Info</h1>");
        builder.Append("<p><strong>Dataset:</strong> ")
            .Append(WebUtility.HtmlEncode(dataset.Id))
            .AppendLine("</p>");

        if (dataset.LayerId.HasValue())
        {
            builder.Append("<p><strong>Layer:</strong> ")
                .Append(WebUtility.HtmlEncode(dataset.LayerId))
                .AppendLine("</p>");
        }

        builder.Append("<p><strong>Coordinate (")
            .Append(WebUtility.HtmlEncode(crs))
            .Append("):</strong> ")
            .Append(WebUtility.HtmlEncode(WmsSharedHelpers.FormatDouble(x)))
            .Append(", ")
            .Append(WebUtility.HtmlEncode(WmsSharedHelpers.FormatDouble(y)))
            .AppendLine("</p>");

        if (features.Count == 0)
        {
            builder.AppendLine("<p>No features found.</p>");
        }
        else
        {
            for (var i = 0; i < features.Count; i++)
            {
                builder.Append("<h2>Feature ")
                    .Append(i + 1)
                    .AppendLine("</h2>");
                builder.AppendLine("<table><tbody>");
                foreach (var kvp in features[i])
                {
                    builder.Append("<tr><th>")
                        .Append(WebUtility.HtmlEncode(kvp.Key))
                        .Append("</th><td>")
                        .Append(WebUtility.HtmlEncode(FormatFeatureValue(kvp.Value)))
                        .AppendLine("</td></tr>");
                }

                builder.AppendLine("</tbody></table>");
            }
        }

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string BuildPlainTextFeatureInfo(
        RasterDatasetDefinition dataset,
        string crs,
        double x,
        double y,
        IReadOnlyList<IDictionary<string, object?>> features)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Dataset: {dataset.Id}");
        if (dataset.LayerId.HasValue())
        {
            builder.AppendLine($"Layer: {dataset.LayerId}");
        }

        builder.AppendLine($"Coordinate ({crs}): {WmsSharedHelpers.FormatDouble(x)}, {WmsSharedHelpers.FormatDouble(y)}");

        if (features.Count == 0)
        {
            builder.AppendLine("No features found.");
        }
        else
        {
            for (var i = 0; i < features.Count; i++)
            {
                builder.AppendLine($"Feature {i + 1}:");
                foreach (var kvp in features[i])
                {
                    builder.Append("  ")
                        .Append(kvp.Key)
                        .Append(": ")
                        .AppendLine(FormatFeatureValue(kvp.Value));
                }
            }
        }

        return builder.ToString();
    }

    private static string FormatFeatureValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            string text => text,
            double number => number.ToString("G", CultureInfo.InvariantCulture),
            float number => number.ToString("G", CultureInfo.InvariantCulture),
            decimal number => number.ToString("G", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            byte[] bytes when bytes.Length == 0 => "(binary: 0 bytes)",
            byte[] bytes => $"(binary: {bytes.Length} bytes)",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
