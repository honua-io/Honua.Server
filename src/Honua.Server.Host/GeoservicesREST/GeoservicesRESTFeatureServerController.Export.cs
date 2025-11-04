// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Export endpoints for various formats (Shapefile, CSV, KML, GeoJSON, TopoJSON, WKT, WKB).
/// All exports use streaming to avoid memory exhaustion.
/// </summary>
public sealed partial class GeoservicesRESTFeatureServerController
{
    /// <summary>
    /// PERFORMANCE FIX: Stream GeoJSON directly to response without materializing full result set.
    /// Hard-capped at 50K features to prevent OOM on unreasonable requests.
    /// </summary>
    private async Task<IActionResult> WriteGeoJsonStreamingAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        const int MaxExportFeatures = 50_000;

        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";
        Response.ContentType = "application/geo+json";

        var stream = Response.Body;
        await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true);

        await writer.WriteAsync("{\"type\":\"FeatureCollection\",");
        await writer.WriteAsync($"\"name\":\"{System.Text.Json.JsonEncodedText.Encode(layer.Title ?? layer.Id)}\",");
        await writer.WriteAsync("\"features\":[");

        var numberReturned = 0;
        var isFirst = true;
        var exceededTransferLimit = false;
        var numberMatched = 0;

        await foreach (var record in _repository.QueryAsync(service.Id, layer.Id, context.Query, cancellationToken).ConfigureAwait(false))
        {
            if (numberReturned >= MaxExportFeatures)
            {
                exceededTransferLimit = true;
                break;
            }

            var components = FeatureComponentBuilder.BuildComponents(layer, record, context.Query);
            var geometry = context.ReturnGeometry ? components.Geometry : null;

            if (!isFirst) await writer.WriteAsync(",");
            isFirst = false;

            await writer.WriteAsync("{\"type\":\"Feature\",");
            await writer.WriteAsync($"\"id\":{System.Text.Json.JsonSerializer.Serialize(components.RawId)},");
            await writer.WriteAsync($"\"geometry\":{System.Text.Json.JsonSerializer.Serialize(geometry)},");
            await writer.WriteAsync($"\"properties\":{System.Text.Json.JsonSerializer.Serialize(components.Properties)}}}");

            numberReturned++;
            numberMatched++;

            if (numberReturned % 100 == 0)
            {
                await writer.FlushAsync();
            }
        }

        await writer.WriteAsync($"],\"numberReturned\":{numberReturned},");
        await writer.WriteAsync($"\"numberMatched\":{numberMatched},");

        if (exceededTransferLimit)
        {
            await writer.WriteAsync("\"exceededTransferLimit\":true,");
            _logger.LogWarning(
                "GeoJSON export exceeded 50K feature limit and was truncated. Service={ServiceId}, Layer={LayerId}, NumberMatched={NumberMatched}, NumberReturned={NumberReturned}",
                service.Id,
                layer.Id,
                numberMatched,
                numberReturned);
        }

        await writer.WriteAsync($"\"timeStamp\":\"{DateTimeOffset.UtcNow:O}\"}}");
        await writer.FlushAsync();

        return new EmptyResult();
    }

    /// <summary>
    /// PERFORMANCE FIX: Stream TopoJSON with hard cap to prevent OOM.
    /// TopoJSON requires arc topology computation, so some buffering is unavoidable.
    /// </summary>
    private async Task<IActionResult> WriteTopoJsonStreamingAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        const int MaxExportFeatures = 10_000; // Lower cap due to topology computation overhead

        var features = new List<TopoJsonFeatureContent>(Math.Min(context.Query.Limit ?? 1000, MaxExportFeatures));
        await foreach (var record in _repository.QueryAsync(service.Id, layer.Id, context.Query, cancellationToken).ConfigureAwait(false))
        {
            if (features.Count >= MaxExportFeatures)
            {
                break;
            }
            features.Add(FeatureComponentBuilder.CreateTopoContent(layer, record, context.Query));
        }

        var collectionId = BuildCollectionIdentifier(service, layer);
        var payload = TopoJsonFeatureFormatter.WriteFeatureCollection(
            collectionId,
            layer,
            features,
            features.Count, // numberMatched = numberReturned (no separate count query)
            features.Count);

        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";
        return Content(payload, "application/topo+json");
    }

    /// <summary>
    /// PERFORMANCE FIX: Stream WKT using WktStreamingWriter infrastructure.
    /// </summary>
    private async Task<IActionResult> WriteWktStreamingAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";
        Response.ContentType = "text/wkt; charset=utf-8";

        var features = _repository.QueryAsync(service.Id, layer.Id, context.Query, cancellationToken);
        var writerContext = new StreamingWriterContext
        {
            ReturnGeometry = context.ReturnGeometry,
            TargetWkid = context.TargetWkid
        };

        // Create logger instance for WktStreamingWriter
        var loggerFactory = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var writerLogger = loggerFactory.CreateLogger<WktStreamingWriter>();
        var writer = new WktStreamingWriter(writerLogger);
        await writer.WriteCollectionAsync(Response.Body, features, layer, writerContext, cancellationToken).ConfigureAwait(false);

        return new EmptyResult();
    }

    /// <summary>
    /// PERFORMANCE FIX: Stream WKB using WkbStreamingWriter infrastructure.
    /// </summary>
    private async Task<IActionResult> WriteWkbStreamingAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";
        Response.ContentType = "application/wkb";

        // SECURITY FIX: Use ContentDispositionHeaderValue with proper filename encoding to prevent header injection attacks.
        // This properly escapes special characters, handles RFC 5987 encoding for international characters via
        // FileNameStar, and prevents CRLF injection vulnerabilities. Sanitization alone is insufficient as even
        // sanitized filenames need proper RFC 2183/5987 escaping for Content-Disposition headers.
        var sanitizedFileName = FileNameHelper.SanitizeSegment(layer.Id);
        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = $"{sanitizedFileName}.wkb"
        };
        Response.Headers["Content-Disposition"] = contentDisposition.ToString();

        var features = _repository.QueryAsync(service.Id, layer.Id, context.Query, cancellationToken);
        var writerContext = new StreamingWriterContext
        {
            ReturnGeometry = context.ReturnGeometry,
            TargetWkid = context.TargetWkid
        };

        // Create logger instance for WkbStreamingWriter
        var loggerFactory = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var writerLogger = loggerFactory.CreateLogger<WkbStreamingWriter>();
        var writer = new WkbStreamingWriter(writerLogger);
        await writer.WriteCollectionAsync(Response.Body, features, layer, writerContext, cancellationToken).ConfigureAwait(false);

        return new EmptyResult();
    }

    private async Task<IActionResult> WriteKmlAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        bool kmz,
        CancellationToken cancellationToken)
    {
        // STREAMING FIX: Use streaming writer to avoid buffering entire payload
        // This eliminates:
        // 1. CountAsync redundant query (streaming writer handles limits internally)
        // 2. List<KmlFeatureContent> buffering all features
        // 3. KmlFeatureFormatter.WriteFeatureCollection building entire DOM
        // 4. Encoding.UTF8.GetBytes doubling memory usage
        // 5. Applies strict 10k record limit to prevent OOM

        if (!context.ReturnGeometry)
        {
            return BadRequest(new { error = "KML export requires returnGeometry=true." });
        }

        var collectionId = BuildCollectionIdentifier(service, layer);
        var style = await ResolveDefaultStyleAsync(layer, cancellationToken).ConfigureAwait(false);

        try
        {
            if (kmz)
            {
                await _streamingKmlWriter.WriteKmzAsync(
                    Response,
                    _repository,
                    service.Id,
                    layer,
                    context,
                    collectionId,
                    style,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _streamingKmlWriter.WriteKmlAsync(
                    Response,
                    _repository,
                    service.Id,
                    layer,
                    context,
                    collectionId,
                    style,
                    cancellationToken).ConfigureAwait(false);
            }

            // Return EmptyResult because we wrote directly to Response
            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "KML export failed for {ServiceId}/{LayerId}.", service.Id, layer.Id);
            return Problem("KML conversion failed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<IActionResult> ExportShapefileAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var shapefileQuery = context.Query with
        {
            PropertyNames = null,
            ResultType = FeatureResultType.Results
        };

        var contentCrs = shapefileQuery.Crs ?? $"EPSG:{context.TargetWkid}";
        var records = _repository.QueryAsync(service.Id, layer.Id, shapefileQuery, cancellationToken);
        var export = await _shapefileExporter.ExportAsync(layer, shapefileQuery, contentCrs, records, cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        Response.Headers["X-Feature-Count"] = export.FeatureCount.ToString(CultureInfo.InvariantCulture);
        return File(export.Content, "application/zip", export.FileName);
    }

    private async Task<IActionResult> ExportKmlAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        StyleDefinition? style,
        CancellationToken cancellationToken)
    {
        var layer = layerView.Layer;
        var service = serviceView.Service;
        var collectionId = CreateCollectionIdentifier(service.Id, layer.Id);
        var isKmz = context.Format == GeoservicesResponseFormat.Kmz;
        var downloadFileName = BuildKmlFileName(collectionId, isKmz);
        var contentType = isKmz
            ? "application/vnd.google-earth.kmz"
            : "application/vnd.google-earth.kml+xml; charset=utf-8";

        Response.ContentType = contentType;
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // SECURITY FIX: Use ContentDispositionHeaderValue with proper filename encoding to prevent header injection attacks.
        // This properly escapes special characters, handles RFC 5987 encoding for international characters via
        // FileNameStar, and prevents CRLF injection vulnerabilities.
        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = downloadFileName
        };
        Response.Headers["Content-Disposition"] = contentDisposition.ToString();

        // BUG FIX #39: KML exports now emit layer symbology when a default style is available.

        try
        {
            // STREAMING FIX: Use streaming writer to avoid buffering entire payload
            // This eliminates:
            // 1. CountAsync redundant query (single-pass enumeration)
            // 2. List<KmlFeatureContent> buffering all features
            // 3. KmlFeatureFormatter.WriteFeatureCollection building entire DOM
            // 4. Encoding.UTF8.GetBytes doubling memory usage
            // 5. Applies strict 10k record limit to prevent OOM

            if (isKmz)
            {
                await _streamingKmlWriter.WriteKmzAsync(
                    Response,
                    _repository,
                    service.Id,
                    layer,
                    context,
                    collectionId,
                    style,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _streamingKmlWriter.WriteKmlAsync(
                    Response,
                    _repository,
                    service.Id,
                    layer,
                    context,
                    collectionId,
                    style,
                    cancellationToken).ConfigureAwait(false);
            }

            // Return EmptyResult because we wrote directly to Response
            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "KML export failed for {ServiceId}/{LayerId}.", service.Id, layer.Id);
            return Problem("KML conversion failed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<IActionResult> ExportCsvAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.Create(HonuaTelemetry.Export, "Export CSV")
            .WithTag("arcgis.export_format", "csv")
            .WithTag("arcgis.service", service.Id)
            .WithTag("arcgis.layer", layer.Id)
            .ExecuteAsync<IActionResult>(async activity =>
            {
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                var memoryBefore = System.GC.GetTotalMemory(false);

                var csvQuery = context.Query with
                {
                    ResultType = FeatureResultType.Results
                };

                // Count features first to enforce limit
                var featureCount = await _repository.CountAsync(service.Id, layer.Id, csvQuery, cancellationToken).ConfigureAwait(false);

                // Check maximum feature count limit
                if (featureCount > 10000)
                {
                    _logger.LogWarning(
                        "CSV export exceeded maximum feature count limit. Service={ServiceId}, Layer={LayerId}, RequestedCount={RequestedCount}, Limit=10000",
                        service.Id,
                        layer.Id,
                        featureCount);
                    return BadRequest(new { error = "CSV export is limited to 10,000 features. Please refine your query to return fewer features." });
                }

                var records = _repository.QueryAsync(service.Id, layer.Id, csvQuery, cancellationToken);
                var export = await _csvExporter.ExportAsync(layer, csvQuery, records, cancellationToken).ConfigureAwait(false);

                if (export.Content.CanSeek)
                {
                    export.Content.Seek(0, SeekOrigin.Begin);
                }

                startTime.Stop();
                var memoryAfter = System.GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;

                var exportSizeBytes = export.Content.Length;

                activity.AddTag("arcgis.feature_count", export.FeatureCount);
                activity.AddTag("arcgis.export_size_bytes", exportSizeBytes);
                activity.AddTag("arcgis.duration_ms", startTime.ElapsedMilliseconds);
                activity.AddTag("arcgis.memory_used_bytes", memoryUsed);

                // Log slow exports
                if (startTime.ElapsedMilliseconds > 5000)
                {
                    _logger.LogWarning(
                        "Slow CSV export detected. Service={ServiceId}, Layer={LayerId}, FeatureCount={FeatureCount}, Duration={DurationMs}ms, Size={SizeBytes} bytes",
                        service.Id,
                        layer.Id,
                        export.FeatureCount,
                        startTime.ElapsedMilliseconds,
                        exportSizeBytes);
                }

                Response.Headers["X-Feature-Count"] = export.FeatureCount.ToString(CultureInfo.InvariantCulture);
                return File(export.Content, "text/csv", export.FileName);
            });
    }

    private async Task<IActionResult> ExportEmptyShapefileAsync(
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var shapefileQuery = context.Query with
        {
            PropertyNames = null,
            ResultType = FeatureResultType.Results
        };

        var contentCrs = shapefileQuery.Crs ?? $"EPSG:{context.TargetWkid}";
        var export = await _shapefileExporter.ExportAsync(layer, shapefileQuery, contentCrs, EmptyFeatureRecords(), cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        Response.Headers["X-Feature-Count"] = "0";
        return File(export.Content, "application/zip", export.FileName);
    }

    private Task<IActionResult> ExportEmptyKmlAsync(
        ServiceDefinition service,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        StyleDefinition? style)
    {
        var layer = layerView.Layer;
        var collectionId = BuildCollectionIdentifier(service, layer);

        // Pass style to KmlFeatureFormatter (style parameter supports null)
        var payload = KmlFeatureFormatter.WriteFeatureCollection(
            collectionId,
            layer,
            Array.Empty<KmlFeatureContent>(),
            0,
            0,
            style);

        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";

        if (context.Format == GeoservicesResponseFormat.Kmz)
        {
            var entryName = BuildKmlEntryName(collectionId);
            var archive = KmzArchiveBuilder.CreateArchive(payload, entryName);
            var downloadName = BuildKmlFileName(collectionId, true);
            return Task.FromResult<IActionResult>(File(archive, "application/vnd.google-earth.kmz", downloadName));
        }

        var bytes = Encoding.UTF8.GetBytes(payload);
        var fileName = BuildKmlFileName(collectionId, false);
        return Task.FromResult<IActionResult>(File(bytes, "application/vnd.google-earth.kml+xml", fileName));
    }

    private async Task<IActionResult> ExportEmptyCsvAsync(
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var csvQuery = context.Query with
        {
            ResultType = FeatureResultType.Results
        };

        var export = await _csvExporter.ExportAsync(layer, csvQuery, EmptyFeatureRecords(), cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        Response.Headers["X-Feature-Count"] = "0";
        return File(export.Content, "text/csv", export.FileName);
    }

    private static IAsyncEnumerable<FeatureRecord> EmptyFeatureRecords()
    {
        return GetEmpty();

        static async IAsyncEnumerable<FeatureRecord> GetEmpty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private static string BuildCollectionIdentifier(ServiceDefinition service, LayerDefinition layer)
    {
        return $"{service.Id}::{layer.Id}";
    }

    private static string BuildKmlFileName(string collectionId, bool kmz)
    {
        var suffix = kmz ? "kmz" : "kml";
        var sanitized = FileNameHelper.SanitizeSegment(collectionId.Replace("::", "__"));
        return $"{sanitized}.{suffix}";
    }

    private static string BuildKmlEntryName(string collectionId)
    {
        var sanitized = FileNameHelper.SanitizeSegment(collectionId.Replace("::", "-"));
        return $"{sanitized}.kml";
    }
}
