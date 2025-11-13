// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Cdn;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Wmts;

/// <summary>
/// WMTS 1.0.0 (Web Map Tile Service) implementation.
/// Provides access to pre-rendered or dynamically-rendered map tiles.
/// </summary>
internal static class WmtsHandlers
{
    private static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer? rasterRenderer,
        [FromServices] IRasterTileCacheProvider? cacheProvider,
        [FromServices] Core.Data.IFeatureRepository featureRepository,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(rasterRegistry);

        var request = context.Request;
        var query = request.Query;

        var serviceValue = QueryParsingHelpers.GetQueryValue(query, "service");
        if (!serviceValue.EqualsIgnoreCase("WMTS"))
        {
            return CreateExceptionReport("InvalidParameterValue", "service", "Parameter 'service' must be set to 'WMTS'.");
        }

        var requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
        if (requestValue.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "request", "Parameter 'request' is required.");
        }

        var metadataSnapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return requestValue.ToUpperInvariant() switch
            {
                "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(request, metadataSnapshot, rasterRegistry, cancellationToken),
                "GETTILE" => await HandleGetTileAsync(request, query, rasterRegistry, rasterRenderer, cacheProvider, cancellationToken),
                "GETFEATUREINFO" => await HandleGetFeatureInfoAsync(request, query, rasterRegistry, featureRepository, cancellationToken),
                _ => CreateExceptionReport("InvalidParameterValue", "request", $"Request '{requestValue}' is not supported.")
            };
        }
        catch (Exception ex)
        {
            // Include exception type to aid debugging when stack traces aren't available
            return CreateExceptionReport("NoApplicableCode", null, $"Error processing request: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Task<IResult> HandleGetCapabilitiesAsync(
        HttpRequest request,
        MetadataSnapshot metadata,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        return ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "WMTS GetCapabilities",
            [("wmts.operation", "GetCapabilities")],
            async activity =>
            {
                var builder = new WmtsCapabilitiesBuilder(rasterRegistry);
                var capabilities = await builder.BuildCapabilitiesAsync(metadata, request, cancellationToken).ConfigureAwait(false);

                return Results.Content(capabilities, "application/xml; charset=utf-8");
            });
    }

    private static async Task<IResult> HandleGetTileAsync(
        HttpRequest request,
        IQueryCollection query,
        IRasterDatasetRegistry rasterRegistry,
        IRasterRenderer? rasterRenderer,
        IRasterTileCacheProvider? cacheProvider,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.RasterTiles,
            "WMTS GetTile",
            [("wmts.operation", "GetTile")],
            async activity =>
            {
                var layer = QueryParsingHelpers.GetQueryValue(query, "layer");
                if (layer.IsNullOrWhiteSpace())
                {
                    return CreateExceptionReport("MissingParameterValue", "layer", "Parameter 'layer' is required.");
                }

                var tileMatrixSet = QueryParsingHelpers.GetQueryValue(query, "tilematrixset");
                if (tileMatrixSet.IsNullOrWhiteSpace())
                {
                    return CreateExceptionReport("MissingParameterValue", "tilematrixset", "Parameter 'tilematrixset' is required.");
                }

                if (!OgcTileMatrixHelper.IsSupportedMatrixSet(tileMatrixSet))
                {
                    return CreateExceptionReport("InvalidParameterValue", "tilematrixset", $"TileMatrixSet '{tileMatrixSet}' is not supported.");
                }

                var tileMatrixStr = QueryParsingHelpers.GetQueryValue(query, "tilematrix");
                if (tileMatrixStr.IsNullOrEmpty() || !OgcTileMatrixHelper.TryParseZoom(tileMatrixStr, out var zoom))
                {
                    return CreateExceptionReport("InvalidParameterValue", "tilematrix", "Parameter 'tilematrix' must be a valid integer.");
                }

                var tileRowStr = QueryParsingHelpers.GetQueryValue(query, "tilerow");
                if (!tileRowStr.TryParseInt(out var row))
                {
                    return CreateExceptionReport("InvalidParameterValue", "tilerow", "Parameter 'tilerow' must be a valid integer.");
                }

                var tileColStr = QueryParsingHelpers.GetQueryValue(query, "tilecol");
                if (!tileColStr.TryParseInt(out var col))
                {
                    return CreateExceptionReport("InvalidParameterValue", "tilecol", "Parameter 'tilecol' must be a valid integer.");
                }

                if (!OgcTileMatrixHelper.IsValidTileCoordinate(zoom, row, col))
                {
                    return CreateExceptionReport("TileOutOfRange", null, $"Tile coordinates ({zoom}/{row}/{col}) are out of range.");
                }

                var dataset = await rasterRegistry.FindAsync(layer, cancellationToken).ConfigureAwait(false);
                if (dataset == null)
                {
                    return CreateExceptionReport("LayerNotDefined", "layer", $"Layer '{layer}' not found.");
                }

                var format = QueryParsingHelpers.GetQueryValue(query, "format") ?? "image/png";

                // Parse and validate TIME parameter if temporal is enabled
                var timeValue = QueryParsingHelpers.GetQueryValue(query, "time");
                if (dataset.Temporal.Enabled)
                {
                    timeValue = ValidateTimeParameter(timeValue, dataset.Temporal);
                }

                // Check cache first (include time in cache key if temporal)
                if (cacheProvider != null)
                {
                    var cacheKey = new RasterTileCacheKey(dataset.Id, tileMatrixSet, zoom, row, col, "default", format, true, 256, timeValue);
                    var cachedTile = await cacheProvider.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);

                    if (cachedTile != null)
                    {
                        return CreateBytesResultWithCdn(cachedTile.Value.Content.ToArray(), cachedTile.Value.ContentType, dataset.Cdn);
                    }
                }

                // Render tile dynamically
                if (rasterRenderer == null)
                {
                    return CreateExceptionReport("NoApplicableCode", null, "Tile rendering is not available.");
                }

                var bbox = OgcTileMatrixHelper.GetBoundingBox(tileMatrixSet, zoom, row, col);
                var sourceCrs = OgcTileMatrixHelper.IsWorldWebMercatorQuad(tileMatrixSet)
                    ? "EPSG:3857"
                    : "EPSG:4326";

                var renderRequest = new RasterRenderRequest(
                    dataset,
                    bbox,
                    256,
                    256,
                    sourceCrs,
                    sourceCrs,
                    format,
                    true,
                    "default",
                    Time: timeValue
                );

                var renderResult = await rasterRenderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);

                // Convert stream to bytes
                using var ms = new System.IO.MemoryStream();
                await renderResult.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                var tileBytes = ms.ToArray();

                // Cache the result
                if (cacheProvider != null && tileBytes.Length > 0)
                {
                    var cacheKey = new RasterTileCacheKey(dataset.Id, tileMatrixSet, zoom, row, col, "default", format, true, 256, timeValue);
                    var cacheEntry = new RasterTileCacheEntry(tileBytes, renderResult.ContentType, DateTimeOffset.UtcNow);
                    await cacheProvider.StoreAsync(cacheKey, cacheEntry, cancellationToken).ConfigureAwait(false);
                }

                return CreateBytesResultWithCdn(tileBytes, renderResult.ContentType, dataset.Cdn);
            });
    }

    private static IResult CreateBytesResultWithCdn(byte[] content, string contentType, RasterCdnDefinition cdnDefinition)
    {
        if (!cdnDefinition.Enabled)
        {
            return Results.Bytes(content, contentType);
        }

        var policy = CdnCachePolicy.FromRasterDefinition(cdnDefinition);
        var cacheControl = policy.ToCacheControlHeader();

        return new BytesResultWithHeaders(content, contentType, cacheControl);
    }

    private sealed class BytesResultWithHeaders : IResult
    {
        private readonly byte[] content;
        private readonly string contentType;
        private readonly string cacheControl;

        public BytesResultWithHeaders(byte[] content, string contentType, string cacheControl)
        {
            this.content = Guard.NotNull(content);
            this.contentType = Guard.NotNullOrWhiteSpace(contentType);
            this.cacheControl = Guard.NotNullOrWhiteSpace(cacheControl);
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;
            httpContext.Response.Headers.CacheControl = _cacheControl;
            httpContext.Response.Headers.Vary = "Accept-Encoding";
            return httpContext.Response.Body.WriteAsync(_content).AsTask();
        }
    }

    private static async Task<IResult> HandleGetFeatureInfoAsync(
        HttpRequest request,
        IQueryCollection query,
        IRasterDatasetRegistry rasterRegistry,
        Core.Data.IFeatureRepository featureRepository,
        CancellationToken cancellationToken)
    {
        // Parse required parameters
        var layer = QueryParsingHelpers.GetQueryValue(query, "layer");
        if (layer.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "layer", "Parameter 'layer' is required.");
        }

        var tileMatrixSet = QueryParsingHelpers.GetQueryValue(query, "tilematrixset");
        if (tileMatrixSet.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "tilematrixset", "Parameter 'tilematrixset' is required.");
        }

        if (!OgcTileMatrixHelper.IsSupportedMatrixSet(tileMatrixSet))
        {
            return CreateExceptionReport("InvalidParameterValue", "tilematrixset", $"TileMatrixSet '{tileMatrixSet}' is not supported.");
        }

        var tileMatrixStr = QueryParsingHelpers.GetQueryValue(query, "tilematrix");
        if (tileMatrixStr.IsNullOrEmpty() || !OgcTileMatrixHelper.TryParseZoom(tileMatrixStr, out var zoom))
        {
            return CreateExceptionReport("InvalidParameterValue", "tilematrix", "Parameter 'tilematrix' must be a valid integer.");
        }

        var tileRowStr = QueryParsingHelpers.GetQueryValue(query, "tilerow");
        if (!tileRowStr.TryParseInt(out var tileRow))
        {
            return CreateExceptionReport("InvalidParameterValue", "tilerow", "Parameter 'tilerow' must be a valid integer.");
        }

        var tileColStr = QueryParsingHelpers.GetQueryValue(query, "tilecol");
        if (!tileColStr.TryParseInt(out var tileCol))
        {
            return CreateExceptionReport("InvalidParameterValue", "tilecol", "Parameter 'tilecol' must be a valid integer.");
        }

        if (!OgcTileMatrixHelper.IsValidTileCoordinate(zoom, tileRow, tileCol))
        {
            return CreateExceptionReport("TileOutOfRange", null, $"Tile coordinates ({zoom}/{tileRow}/{tileCol}) are out of range.");
        }

        // Parse pixel coordinates within the tile (I, J)
        var pixelIStr = QueryParsingHelpers.GetQueryValue(query, "i");
        if (!pixelIStr.TryParseInt(out var pixelI) || pixelI < 0 || pixelI >= 256)
        {
            return CreateExceptionReport("InvalidParameterValue", "i", "Parameter 'i' must be an integer between 0 and 255.");
        }

        var pixelJStr = QueryParsingHelpers.GetQueryValue(query, "j");
        if (!pixelJStr.TryParseInt(out var pixelJ) || pixelJ < 0 || pixelJ >= 256)
        {
            return CreateExceptionReport("InvalidParameterValue", "j", "Parameter 'j' must be an integer between 0 and 255.");
        }

        // Get the dataset
        var dataset = await rasterRegistry.FindAsync(layer, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return CreateExceptionReport("LayerNotDefined", "layer", $"Layer '{layer}' not found.");
        }

        // Check if the layer is linked to a feature layer
        if (dataset.ServiceId.IsNullOrWhiteSpace() || dataset.LayerId.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("LayerNotQueryable", "layer", $"Layer '{layer}' is not queryable (not linked to a feature layer).");
        }

        // Parse optional parameters
        var infoFormat = NormalizeInfoFormat(QueryParsingHelpers.GetQueryValue(query, "infoformat"));
        var featureCount = ParseFeatureCount(query);

        // Calculate geographic coordinates from tile coordinates and pixel position
        var tileBbox = OgcTileMatrixHelper.GetBoundingBox(tileMatrixSet, zoom, tileRow, tileCol);
        var tileWidth = tileBbox[2] - tileBbox[0];
        var tileHeight = tileBbox[3] - tileBbox[1];

        var pixelWidth = tileWidth / 256.0;
        var pixelHeight = tileHeight / 256.0;

        var coordinateX = tileBbox[0] + (pixelI + 0.5) * pixelWidth;
        var coordinateY = tileBbox[3] - (pixelJ + 0.5) * pixelHeight; // Note: Y is inverted in tile coordinates

        // Create search bbox around the clicked point
        var halfWidth = Math.Max(pixelWidth * 0.5, 1e-9);
        var halfHeight = Math.Max(pixelHeight * 0.5, 1e-9);

        var searchMinX = coordinateX - halfWidth;
        var searchMaxX = coordinateX + halfWidth;
        var searchMinY = coordinateY - halfHeight;
        var searchMaxY = coordinateY + halfHeight;

        // Determine CRS from tile matrix set
        var crs = OgcTileMatrixHelper.IsWorldWebMercatorQuad(tileMatrixSet) ? "EPSG:3857" : "EPSG:4326";

        var queryBbox = new Core.Data.BoundingBox(searchMinX, searchMinY, searchMaxX, searchMaxY, null, null, crs);
        var featureQuery = new Core.Data.FeatureQuery(Limit: featureCount, Bbox: queryBbox, Crs: crs);

        // Query features
        var features = new List<IDictionary<string, object?>>(featureCount);
        await foreach (var feature in featureRepository.QueryAsync(dataset.ServiceId!, dataset.LayerId!, featureQuery, cancellationToken).ConfigureAwait(false))
        {
            features.Add(new Dictionary<string, object?>(feature.Attributes, StringComparer.OrdinalIgnoreCase));
            if (features.Count >= featureCount)
            {
                break;
            }
        }

        // Build response in requested format
        var jsonPayload = new
        {
            type = "FeatureInfo",
            dataset = dataset.Id,
            service = dataset.ServiceId,
            layer = dataset.LayerId,
            tileMatrixSet,
            zoom,
            tileRow,
            tileCol,
            pixelI,
            pixelJ,
            coordinate = new { x = coordinateX, y = coordinateY, crs },
            features
        };

        return infoFormat switch
        {
            "application/json" => Results.Json(jsonPayload, contentType: "application/json"),
            "application/geo+json" => Results.Json(BuildGeoJsonFeatureInfo(dataset, crs, coordinateX, coordinateY, features), contentType: "application/geo+json"),
            "application/xml" => BuildXmlFeatureInfo(dataset, crs, coordinateX, coordinateY, features),
            "text/html" => Results.Content(BuildHtmlFeatureInfo(dataset, crs, coordinateX, coordinateY, features), "text/html; charset=utf-8"),
            "text/plain" => Results.Text(BuildPlainTextFeatureInfo(dataset, crs, coordinateX, coordinateY, features), "text/plain"),
            _ => CreateExceptionReport("InvalidParameterValue", "infoformat", $"INFO_FORMAT '{infoFormat}' is not supported.")
        };
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
        }).ToList();  // Materialize immediately to avoid lazy evaluation issues

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
            new XAttribute("x", FormatDouble(x)),
            new XAttribute("y", FormatDouble(y))));

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
        var xml = document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        return Results.Content(xml, "application/xml");
    }

    private static string BuildHtmlFeatureInfo(
        RasterDatasetDefinition dataset,
        string crs,
        double x,
        double y,
        IReadOnlyList<IDictionary<string, object?>> features)
    {
        var builder = new System.Text.StringBuilder();
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
            .Append(System.Net.WebUtility.HtmlEncode(dataset.Id))
            .AppendLine("</p>");

        if (dataset.LayerId.HasValue())
        {
            builder.Append("<p><strong>Layer:</strong> ")
                .Append(System.Net.WebUtility.HtmlEncode(dataset.LayerId))
                .AppendLine("</p>");
        }

        builder.Append("<p><strong>Coordinate (")
            .Append(System.Net.WebUtility.HtmlEncode(crs))
            .Append("):</strong> ")
            .Append(System.Net.WebUtility.HtmlEncode(FormatDouble(x)))
            .Append(", ")
            .Append(System.Net.WebUtility.HtmlEncode(FormatDouble(y)))
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
                        .Append(System.Net.WebUtility.HtmlEncode(kvp.Key))
                        .Append("</th><td>")
                        .Append(System.Net.WebUtility.HtmlEncode(FormatFeatureValue(kvp.Value)))
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
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"Dataset: {dataset.Id}");
        if (dataset.LayerId.HasValue())
        {
            builder.AppendLine($"Layer: {dataset.LayerId}");
        }

        builder.AppendLine($"Coordinate ({crs}): {FormatDouble(x)}, {FormatDouble(y)}");

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

    private static string FormatDouble(double value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
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

        return Math.Min(parsed, 50); // Max 50 features
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
            || candidate.EqualsIgnoreCase("xml"))
        {
            return "application/xml";
        }

        if (candidate.EqualsIgnoreCase("text/html")
            || candidate.EqualsIgnoreCase("html"))
        {
            return "text/html";
        }

        if (candidate.EqualsIgnoreCase("text/plain")
            || candidate.EqualsIgnoreCase("plain")
            || candidate.EqualsIgnoreCase("text"))
        {
            return "text/plain";
        }

        return candidate;
    }

    private static string? ValidateTimeParameter(string? timeValue, RasterTemporalDefinition temporal)
    {
        // Use default if no time specified
        if (timeValue.IsNullOrWhiteSpace())
        {
            return temporal.DefaultValue;
        }

        // If fixed values are specified, validate against them
        if (temporal.FixedValues is { Count: > 0 })
        {
            if (!temporal.FixedValues.Contains(timeValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is not in the allowed set: {string.Join(", ", temporal.FixedValues)}");
            }
            return timeValue;
        }

        // If range is specified, validate within bounds
        if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            if (string.CompareOrdinal(timeValue, temporal.MinValue) < 0 || string.CompareOrdinal(timeValue, temporal.MaxValue) > 0)
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is outside the valid range: {temporal.MinValue} to {temporal.MaxValue}");
            }
        }

        return timeValue;
    }

    private static IResult CreateExceptionReport(string exceptionCode, string? locator, string exceptionText)
    {
        var exception = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ows + "ExceptionReport",
                new XAttribute("version", "1.1.0"),
                new XAttribute(XNamespace.Xmlns + "ows", Ows),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(Xsi + "schemaLocation", "http://www.opengis.net/ows/1.1 http://schemas.opengis.net/ows/1.1.0/owsExceptionReport.xsd"),
                new XElement(Ows + "Exception",
                    new XAttribute("exceptionCode", exceptionCode),
                    locator != null ? new XAttribute("locator", locator) : null,
                    new XElement(Ows + "ExceptionText", exceptionText)
                )
            )
        );

        return Results.Content(exception.ToString(), "application/xml; charset=utf-8", statusCode: 400);
    }
}
