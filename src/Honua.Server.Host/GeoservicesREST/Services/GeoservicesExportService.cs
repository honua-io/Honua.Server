// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer export operations (Shapefile, KML, CSV, GeoJSON, TopoJSON, WKT, WKB).
/// Extracted from GeoservicesRESTFeatureServerController to reduce controller complexity.
/// </summary>
public sealed class GeoservicesExportService : IGeoservicesExportService
{
    private readonly IFeatureRepository repository;
    private readonly IShapefileExporter shapefileExporter;
    private readonly ICsvExporter csvExporter;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly StreamingKmlWriter streamingKmlWriter;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<GeoservicesExportService> logger;

    public GeoservicesExportService(
        IFeatureRepository repository,
        IShapefileExporter shapefileExporter,
        ICsvExporter csvExporter,
        IMetadataRegistry metadataRegistry,
        StreamingKmlWriter streamingKmlWriter,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GeoservicesExportService> logger)
    {
        this.repository = Guard.NotNull(repository);
        this.shapefileExporter = Guard.NotNull(shapefileExporter);
        this.csvExporter = Guard.NotNull(csvExporter);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.streamingKmlWriter = Guard.NotNull(streamingKmlWriter);
        this.httpContextAccessor = Guard.NotNull(httpContextAccessor);
        this.logger = Guard.NotNull(logger);
    }

    public async Task<IActionResult> ExportShapefileAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var service = serviceView.Service;
        var layer = layerView.Layer;

        var shapefileQuery = context.Query with
        {
            PropertyNames = null,
            ResultType = FeatureResultType.Results
        };

        var contentCrs = shapefileQuery.Crs ?? $"EPSG:{context.TargetWkid}";
        var records = this.repository.QueryAsync(service.Id, layer.Id, shapefileQuery, cancellationToken);
        var export = await this.shapefileExporter.ExportAsync(layer, shapefileQuery, contentCrs, records, cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        var response = GetHttpContext().Response;
        response.Headers["X-Feature-Count"] = export.FeatureCount.ToString(CultureInfo.InvariantCulture);

        return new FileStreamResult(export.Content, "application/zip") { FileDownloadName = export.FileName };
    }

    public async Task<IActionResult> ExportKmlAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        bool kmz,
        CancellationToken cancellationToken)
    {
        var layer = layerView.Layer;
        var service = serviceView.Service;

        if (!context.ReturnGeometry)
        {
            return new BadRequestObjectResult(new { error = "KML export requires returnGeometry=true." });
        }

        var collectionId = BuildCollectionIdentifier(service.Id, layer.Id);
        var style = await ResolveDefaultStyleAsync(layer, cancellationToken).ConfigureAwait(false);
        var downloadFileName = BuildKmlFileName(collectionId, kmz);
        var contentType = kmz
            ? "application/vnd.google-earth.kmz"
            : "application/vnd.google-earth.kml+xml; charset=utf-8";

        var response = GetHttpContext().Response;
        response.ContentType = contentType;
        response.Headers["X-Content-Type-Options"] = "nosniff";

        // SECURITY FIX: Use ContentDispositionHeaderValue with proper filename encoding
        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = downloadFileName
        };
        response.Headers["Content-Disposition"] = contentDisposition.ToString();

        try
        {
            if (kmz)
            {
                await this.streamingKmlWriter.WriteKmzAsync(
                    response,
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
                await this.streamingKmlWriter.WriteKmlAsync(
                    response,
                    _repository,
                    service.Id,
                    layer,
                    context,
                    collectionId,
                    style,
                    cancellationToken).ConfigureAwait(false);
            }

            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            this.logger.LogError(ex, "KML export failed for {ServiceId}/{LayerId}.", service.Id, layer.Id);
            return new ObjectResult("KML conversion failed.") { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }

    public async Task<IActionResult> ExportCsvAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var service = serviceView.Service;
        var layer = layerView.Layer;

        return await ActivityScope.Create(HonuaTelemetry.Export, "Export CSV")
            .WithTag("arcgis.export_format", "csv")
            .WithTag("arcgis.service", service.Id)
            .WithTag("arcgis.layer", layer.Id)
            .ExecuteAsync<IActionResult>(async activity =>
            {
                var startTime = Stopwatch.StartNew();
                var memoryBefore = GC.GetTotalMemory(false);

                var csvQuery = context.Query with
                {
                    ResultType = FeatureResultType.Results
                };

                // Count features first to enforce limit
                var featureCount = await this.repository.CountAsync(service.Id, layer.Id, csvQuery, cancellationToken).ConfigureAwait(false);

                // Check maximum feature count limit
                if (featureCount > 10000)
                {
                    this.logger.LogWarning(
                        "CSV export exceeded maximum feature count limit. Service={ServiceId}, Layer={LayerId}, RequestedCount={RequestedCount}, Limit=10000",
                        service.Id,
                        layer.Id,
                        featureCount);
                    return new BadRequestObjectResult(new { error = "CSV export is limited to 10,000 features. Please refine your query to return fewer features." });
                }

                var records = this.repository.QueryAsync(service.Id, layer.Id, csvQuery, cancellationToken);
                var export = await this.csvExporter.ExportAsync(layer, csvQuery, records, cancellationToken).ConfigureAwait(false);

                if (export.Content.CanSeek)
                {
                    export.Content.Seek(0, SeekOrigin.Begin);
                }

                startTime.Stop();
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;
                var exportSizeBytes = export.Content.Length;

                activity.AddTag("arcgis.feature_count", export.FeatureCount);
                activity.AddTag("arcgis.export_size_bytes", exportSizeBytes);
                activity.AddTag("arcgis.duration_ms", startTime.ElapsedMilliseconds);
                activity.AddTag("arcgis.memory_used_bytes", memoryUsed);

                // Log slow exports
                if (startTime.ElapsedMilliseconds > 5000)
                {
                    this.logger.LogWarning(
                        "Slow CSV export detected. Service={ServiceId}, Layer={LayerId}, FeatureCount={FeatureCount}, Duration={DurationMs}ms, Size={SizeBytes} bytes",
                        service.Id,
                        layer.Id,
                        export.FeatureCount,
                        startTime.ElapsedMilliseconds,
                        exportSizeBytes);
                }

                var response = GetHttpContext().Response;
                response.Headers["X-Feature-Count"] = export.FeatureCount.ToString(CultureInfo.InvariantCulture);
                return new FileStreamResult(export.Content, "text/csv") { FileDownloadName = export.FileName };
            });
    }

    public async Task<IActionResult> ExportEmptyShapefileAsync(
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
        var export = await this.shapefileExporter.ExportAsync(layer, shapefileQuery, contentCrs, EmptyFeatureRecords(), cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        var response = GetHttpContext().Response;
        response.Headers["X-Feature-Count"] = "0";
        return new FileStreamResult(export.Content, "application/zip") { FileDownloadName = export.FileName };
    }

    public Task<IActionResult> ExportEmptyKmlAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context)
    {
        var layer = layerView.Layer;
        var service = serviceView.Service;
        var collectionId = BuildCollectionIdentifier(service.Id, layer.Id);
        var style = ResolveDefaultStyleAsync(layer, CancellationToken.None).GetAwaiter().GetResult();

        var payload = KmlFeatureFormatter.WriteFeatureCollection(
            collectionId,
            layer,
            Array.Empty<KmlFeatureContent>(),
            0,
            0,
            style);

        var response = GetHttpContext().Response;
        response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";

        if (context.Format == GeoservicesResponseFormat.Kmz)
        {
            var entryName = BuildKmlEntryName(collectionId);
            var archive = KmzArchiveBuilder.CreateArchive(payload, entryName);
            var downloadName = BuildKmlFileName(collectionId, true);
            return Task.FromResult<IActionResult>(new FileContentResult(archive, "application/vnd.google-earth.kmz") { FileDownloadName = downloadName });
        }

        var bytes = Encoding.UTF8.GetBytes(payload);
        var fileName = BuildKmlFileName(collectionId, false);
        return Task.FromResult<IActionResult>(new FileContentResult(bytes, "application/vnd.google-earth.kml+xml") { FileDownloadName = fileName });
    }

    public async Task<IActionResult> ExportEmptyCsvAsync(
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var csvQuery = context.Query with
        {
            ResultType = FeatureResultType.Results
        };

        var export = await this.csvExporter.ExportAsync(layer, csvQuery, EmptyFeatureRecords(), cancellationToken).ConfigureAwait(false);

        if (export.Content.CanSeek)
        {
            export.Content.Seek(0, SeekOrigin.Begin);
        }

        var response = GetHttpContext().Response;
        response.Headers["X-Feature-Count"] = "0";
        return new FileStreamResult(export.Content, "text/csv") { FileDownloadName = export.FileName };
    }

    private static string BuildCollectionIdentifier(string serviceId, string layerId)
    {
        return $"{serviceId}::{layerId}";
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

    private static IAsyncEnumerable<FeatureRecord> EmptyFeatureRecords()
    {
        return GetEmpty();

        static async IAsyncEnumerable<FeatureRecord> GetEmpty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private async Task<StyleDefinition?> ResolveDefaultStyleAsync(LayerDefinition layer, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(layer.DefaultStyleId) &&
            snapshot.TryGetStyle(layer.DefaultStyleId, out var defaultStyle))
        {
            return defaultStyle;
        }

        foreach (var candidate in layer.StyleIds)
        {
            if (!string.IsNullOrEmpty(candidate) && snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    private HttpContext GetHttpContext()
    {
        return this.httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");
    }
}
