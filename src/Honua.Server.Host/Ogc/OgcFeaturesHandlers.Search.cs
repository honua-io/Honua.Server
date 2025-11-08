// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcFeaturesHandlers
{
    /// <summary>
    /// Executes a cross-collection search via GET request.
    /// OGC API - Features /search endpoint (GET).
    /// </summary>
    public static async Task<IResult> GetSearch(
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        [FromServices] IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        [FromServices] Services.IOgcFeaturesQueryHandler queryHandler,
        [FromServices] ILogger logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var collections = OgcSharedHandlers.ParseCollectionsParameter(request.Query["collections"]);

        if (collections.Count == 0)
        {
            logger.LogWarning("Search request failed: no collections specified from {RemoteIp}", request.HttpContext.Connection.RemoteIpAddress);
            return OgcSharedHandlers.CreateValidationProblem("collections parameter is required.", "collections");
        }

        logger.LogInformation("Initiating OGC search for {Count} collections: {Collections} from {RemoteIp}",
            collections.Count, string.Join(",", collections), request.HttpContext.Connection.RemoteIpAddress);

        try
        {
            var result = await queryHandler.ExecuteSearchAsync(
                request,
                collections,
                request.Query,
                resolver,
                repository,
                geoPackageExporter,
                shapefileExporter,
                flatGeobufExporter,
                geoArrowExporter,
                csvExporter,
                attachmentOrchestrator,
                metadataRegistry,
                apiMetrics,
                cacheHeaderService,
                attachmentHandler,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            logger.LogInformation("Completed OGC search for {Collections} in {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("OGC search cancelled for {Collections} after {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to execute OGC search for {Collections} after {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Executes a cross-collection search via POST request with JSON body.
    /// OGC API - Features /search endpoint (POST).
    /// </summary>
    public static async Task<IResult> PostSearch(
        HttpRequest request,
        IFeatureContextResolver resolver,
        IFeatureRepository repository,
        IGeoPackageExporter geoPackageExporter,
        IShapefileExporter shapefileExporter,
        IFlatGeobufExporter flatGeobufExporter,
        [FromServices] IGeoArrowExporter geoArrowExporter,
        ICsvExporter csvExporter,
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IMetadataRegistry metadataRegistry,
        IApiMetrics apiMetrics,
        OgcCacheHeaderService cacheHeaderService,
        [FromServices] Services.IOgcFeaturesAttachmentHandler attachmentHandler,
        [FromServices] Services.IOgcFeaturesGeoJsonHandler geoJsonHandler,
        [FromServices] Services.IOgcFeaturesQueryHandler queryHandler,
        [FromServices] ILogger logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var document = await geoJsonHandler.ParseJsonDocumentAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            logger.LogWarning("POST search request rejected: invalid JSON payload from {RemoteIp}",
                request.HttpContext.Connection.RemoteIpAddress);
            return OgcSharedHandlers.CreateValidationProblem("Request body must contain a valid JSON payload.", "body");
        }

        var root = document.RootElement;
        if (!root.TryGetProperty("collections", out var collectionsElement) || collectionsElement.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("POST search request rejected: missing or invalid 'collections' array from {RemoteIp}",
                request.HttpContext.Connection.RemoteIpAddress);
            return OgcSharedHandlers.CreateValidationProblem("Request body must contain a 'collections' array.", "collections");
        }

        var collections = new List<string>();
        foreach (var element in collectionsElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String && !element.GetString().IsNullOrWhiteSpace())
            {
                collections.Add(element.GetString()!.Trim());
            }
        }

        if (collections.Count == 0)
        {
            logger.LogWarning("POST search request rejected: empty 'collections' array from {RemoteIp}",
                request.HttpContext.Connection.RemoteIpAddress);
            return OgcSharedHandlers.CreateValidationProblem("'collections' array must contain at least one collection ID.", "collections");
        }

        logger.LogInformation("Initiating POST OGC search for {Count} collections: {Collections} from {RemoteIp}",
            collections.Count, string.Join(",", collections), request.HttpContext.Connection.RemoteIpAddress);

        var parameters = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            ["collections"] = new StringValues(string.Join(',', collections))
        };

        void AssignIfPresent(string name, Func<JsonElement, string?> converter)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                return;
            }

            var converted = converter(value);
            if (converted.HasValue())
            {
                parameters[name] = new StringValues(converted);
            }
        }

        AssignIfPresent("filter", element => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            _ => null
        });
        AssignIfPresent("filter-lang", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("filter-crs", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("datetime", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("sortby", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("crs", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("f", element => element.ValueKind == JsonValueKind.String ? element.GetString() : null);
        AssignIfPresent("limit", element => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var limit) ? limit.ToString(CultureInfo.InvariantCulture) : null);
        AssignIfPresent("offset", element => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var offset) ? offset.ToString(CultureInfo.InvariantCulture) : null);

        if (root.TryGetProperty("bbox", out var bboxElement) && bboxElement.ValueKind == JsonValueKind.Array)
        {
            var values = bboxElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Number)
                .Select(item => item.GetDouble().ToString("G17", CultureInfo.InvariantCulture))
                .ToArray();

            if (values.Length is 4 or 6)
            {
                parameters["bbox"] = new StringValues(string.Join(',', values));
            }
        }

        if (root.TryGetProperty("bbox-crs", out var bboxCrsElement) && bboxCrsElement.ValueKind == JsonValueKind.String)
        {
            parameters["bbox-crs"] = new StringValues(bboxCrsElement.GetString());
        }

        if (root.TryGetProperty("ids", out var idsElement) && idsElement.ValueKind == JsonValueKind.Array)
        {
            var ids = idsElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !item.GetString().IsNullOrWhiteSpace())
                .Select(item => item.GetString()!.Trim())
                .ToArray();

            if (ids.Length > 0)
            {
                parameters["ids"] = new StringValues(string.Join(',', ids));
            }
        }

        if (root.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Array)
        {
            var props = propertiesElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !item.GetString().IsNullOrWhiteSpace())
                .Select(item => item.GetString()!.Trim())
                .ToArray();

            if (props.Length > 0)
            {
                parameters["properties"] = new StringValues(string.Join(',', props));
            }
        }

        if (!parameters.ContainsKey("filter-lang") && root.TryGetProperty("filter", out var filterElement) &&
            (filterElement.ValueKind == JsonValueKind.Object || filterElement.ValueKind == JsonValueKind.Array))
        {
            parameters["filter-lang"] = new StringValues("cql2-json");
        }

        var queryCollection = new QueryCollection(parameters);

        try
        {
            var result = await queryHandler.ExecuteSearchAsync(
                request,
                collections,
                queryCollection,
                resolver,
                repository,
                geoPackageExporter,
                shapefileExporter,
                flatGeobufExporter,
                geoArrowExporter,
                csvExporter,
                attachmentOrchestrator,
                metadataRegistry,
                apiMetrics,
                cacheHeaderService,
                attachmentHandler,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            logger.LogInformation("Completed POST OGC search for {Collections} in {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogWarning("POST OGC search cancelled for {Collections} after {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed to execute POST OGC search for {Collections} after {ElapsedMs}ms",
                string.Join(",", collections), stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
