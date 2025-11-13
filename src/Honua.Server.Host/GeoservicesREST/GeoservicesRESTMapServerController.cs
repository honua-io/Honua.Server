// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.GeoservicesREST;

[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services/{folderId}/{serviceId}/MapServer")]
public sealed class GeoservicesRESTMapServerController : ControllerBase
{
    private const double GeoServicesVersion = 10.81;

    private readonly ICatalogProjectionService catalog;
    private readonly IFeatureRepository repository;
    private readonly IRasterDatasetRegistry rasterRegistry;
    private readonly IRasterRenderer rasterRenderer;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<GeoservicesRESTMapServerController> logger;

    public GeoservicesRESTMapServerController(
        ICatalogProjectionService catalog,
        IFeatureRepository repository,
        IRasterDatasetRegistry rasterRegistry,
        IRasterRenderer rasterRenderer,
        IMetadataRegistry metadataRegistry,
        IServiceProvider serviceProvider,
        ILogger<GeoservicesRESTMapServerController> logger)
    {
        this.catalog = Guard.NotNull(catalog);
        this.repository = Guard.NotNull(repository);
        this.rasterRegistry = Guard.NotNull(rasterRegistry);
        this.rasterRenderer = Guard.NotNull(rasterRenderer);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.serviceProvider = Guard.NotNull(serviceProvider);
        this.logger = Guard.NotNull(logger);
    }

    [HttpGet]
    public ActionResult<GeoservicesRESTFeatureServiceSummary> GetService(string folderId, string serviceId)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var summary = GeoservicesRESTMetadataMapper.CreateMapServiceSummary(serviceView, GeoServicesVersion);
        return this.Ok(summary);
    }

    [HttpGet("{layerIndex:int}/query")]
    public Task<IActionResult> QueryLayerAsync(string folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "ArcGIS MapServer Query",
            [
                ("arcgis.operation", "Query"),
                ("arcgis.service", serviceId),
                ("arcgis.layer_index", layerIndex)
            ],
            async activity => await ForwardFeatureQueryAsync(folderId, serviceId, layerIndex, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("{layerIndex:int}/query")]
    public Task<IActionResult> QueryLayerPostAsync(string folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "ArcGIS MapServer Query",
            [
                ("arcgis.operation", "Query"),
                ("arcgis.service", serviceId),
                ("arcgis.layer_index", layerIndex),
                ("arcgis.method", "POST")
            ],
            async activity => await ForwardFeatureQueryAsync(folderId, serviceId, layerIndex, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.RasterTiles,
            "ArcGIS MapServer Export",
            [
                ("arcgis.operation", "Export"),
                ("arcgis.service", serviceId)
            ],
            async activity =>
            {
                var serviceView = ResolveService(folderId, serviceId);
                if (serviceView is null)
                {
                    return this.NotFound();
                }

                // Parse export parameters using shared helper
                // Note: MapServer supports vector rendering fallback for services without raster datasets
                var (parameters, error) = await GeoservicesRESTRasterExportHelper.TryParseExportRequestAsync(
                    Request,
                    serviceView,
                    this.rasterRegistry,
                    cancellationToken,
                    datasetFilter: null,
                    fallbackDatasetFactory: CreateFallbackDataset).ConfigureAwait(false);

                if (error is not null)
                {
                    return error;
                }

                // Collect vector geometries for overlay rendering (MapServer-specific)
                var vectorGeometries = await CollectVectorGeometriesAsync(serviceView, parameters!.Dataset, parameters.Bbox, cancellationToken).ConfigureAwait(false);

                var selectedStyle = await GeoservicesRESTRasterExportHelper.ResolveStyleDefinitionAsync(
                    this.metadataRegistry,
                    parameters.Dataset,
                    parameters.StyleId,
                    cancellationToken).ConfigureAwait(false);

                var renderRequest = new RasterRenderRequest(
                    parameters.Dataset,
                    parameters.Bbox,
                    parameters.Width,
                    parameters.Height,
                    parameters.SourceCrs,
                    parameters.TargetCrs,
                    parameters.Format,
                    parameters.Transparent,
                    parameters.StyleId,
                    selectedStyle,
                    vectorGeometries);

                var result = await this.rasterRenderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);

                if (result.Content.CanSeek)
                {
                    result.Content.Seek(0, SeekOrigin.Begin);
                }

                this.Response.Headers["X-Rendered-Dataset"] = parameters.Dataset.Id;
                this.Response.Headers["X-Target-CRS"] = parameters.TargetCrs;

                return this.File(result.Content, result.ContentType);
            }).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Geometry>> CollectVectorGeometriesAsync(
        CatalogServiceView serviceView,
        RasterDatasetDefinition dataset,
        double[] bbox,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.RasterTiles,
            "Collect Vector Geometries",
            [
                ("arcgis.service", serviceView.Service.Id),
                ("arcgis.dataset", dataset.Id)
            ],
            async activity =>
            {
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                var memoryBefore = System.GC.GetTotalMemory(false);

                if (bbox.Length < 4)
                {
                    return Array.Empty<Geometry>();
                }

                if (serviceView.Layers.Count == 0)
                {
                    return Array.Empty<Geometry>();
                }

                CatalogLayerView? targetLayer = null;
                if (dataset.LayerId.HasValue())
                {
                    targetLayer = serviceView.Layers.FirstOrDefault(layer =>
                        layer.Layer.Id.EqualsIgnoreCase(dataset.LayerId));
                }

                targetLayer ??= serviceView.Layers[0];
                if (targetLayer is null)
                {
                    return Array.Empty<Geometry>();
                }

                activity.AddTag("arcgis.layer", targetLayer.Layer.Id);

                var query = new FeatureQuery(
                    Limit: 500,
                    Bbox: new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]),
                    ResultType: FeatureResultType.Results,
                    Crs: dataset.Crs.FirstOrDefault() ?? serviceView.Service.Ogc.DefaultCrs);

                var geometries = new List<Geometry>();
                var reader = new GeoJsonReader();
                var malformedCount = 0;

                await foreach (var record in this.repository.QueryAsync(serviceView.Service.Id, targetLayer.Layer.Id, query, cancellationToken).ConfigureAwait(false))
                {
                    var components = FeatureComponentBuilder.BuildComponents(targetLayer.Layer, record, query);
                    if (components.GeometryNode is null)
                    {
                        continue;
                    }

                    try
                    {
                        var geometry = reader.Read<Geometry>(components.GeometryNode.ToJsonString());
                        if (geometry is not null && !geometry.IsEmpty)
                        {
                            geometries.Add(geometry);
                        }
                    }
                    catch
                    {
                        // Ignore malformed geometries for raster overlay rendering.
                        malformedCount++;
                    }
                }

                startTime.Stop();
                var memoryAfter = System.GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;

                activity.AddTag("arcgis.geometry_count", geometries.Count);
                activity.AddTag("arcgis.malformed_geometry_count", malformedCount);
                activity.AddTag("arcgis.query_execution_time_ms", startTime.ElapsedMilliseconds);
                activity.AddTag("arcgis.memory_used_bytes", memoryUsed);

                // Log slow queries (>1 second)
                if (startTime.ElapsedMilliseconds > 1000)
                {
                    this.logger.LogWarning(
                        "Slow vector geometry collection detected. Service={ServiceId}, Layer={LayerId}, Dataset={DatasetId}, GeometryCount={GeometryCount}, Duration={DurationMs}ms",
                        serviceView.Service.Id,
                        targetLayer.Layer.Id,
                        dataset.Id,
                        geometries.Count,
                        startTime.ElapsedMilliseconds);
                }

                return geometries.Count == 0
                    ? (IReadOnlyList<Geometry>)Array.Empty<Geometry>()
                    : new ReadOnlyCollection<Geometry>(geometries);
            }).ConfigureAwait(false);
    }

    private Task<IActionResult> ForwardFeatureQueryAsync(
        string folderId,
        string serviceId,
        int layerIndex,
        CancellationToken cancellationToken)
    {
        var featureController = ActivatorUtilities.CreateInstance<GeoservicesRESTFeatureServerController>(this.serviceProvider);
        featureController.ControllerContext = ControllerContext;
        featureController.Url = Url;
        return featureController.QueryAsync(folderId, serviceId, layerIndex, cancellationToken);
    }


    private static RasterDatasetDefinition? CreateFallbackDataset(CatalogServiceView serviceView)
    {
        if (serviceView.Layers.Count == 0)
        {
            return null;
        }

        var primaryLayer = serviceView.Layers[0];
        var styleIds = primaryLayer.Layer.StyleIds ?? Array.Empty<string>();

        return new RasterDatasetDefinition
        {
            Id = $"{serviceView.Service.Id}-auto-map",
            Title = primaryLayer.Layer.Title ?? serviceView.Service.Title ?? serviceView.Service.Id,
            ServiceId = serviceView.Service.Id,
            LayerId = primaryLayer.Layer.Id,
            Crs = primaryLayer.Layer.Crs,
            Catalog = primaryLayer.Layer.Catalog,
            Extent = primaryLayer.Layer.Extent,
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = primaryLayer.Layer.DefaultStyleId,
                StyleIds = styleIds
            },
            Source = new RasterSourceDefinition
            {
                Type = "vector",
                Uri = string.Empty
            }
        };
    }

    [HttpGet("{layerIndex:int}")]
    public async Task<ActionResult<GeoservicesRESTLayerDetailResponse>> GetLayer(string folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return this.NotFound();
        }

        var style = await ResolveDefaultStyleAsync(layerView, cancellationToken).ConfigureAwait(false);
        var detail = GeoservicesRESTMetadataMapper.CreateLayerDetailResponse(serviceView, layerView, layerIndex, GeoServicesVersion, style);
        return this.Ok(detail);
    }

    [HttpGet("layers")]
    public async Task<ActionResult<GeoservicesRESTLayersResponse>> GetLayers(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var layers = new List<GeoservicesRESTLayerDetailResponse>(serviceView.Layers.Count);
        for (var index = 0; index < serviceView.Layers.Count; index++)
        {
            var layerView = serviceView.Layers[index];
            var style = await ResolveDefaultStyleAsync(layerView, cancellationToken).ConfigureAwait(false);
            layers.Add(GeoservicesRESTMetadataMapper.CreateLayerDetailResponse(serviceView, layerView, index, GeoServicesVersion, style));
        }

        return this.Ok(new GeoservicesRESTLayersResponse
        {
            CurrentVersion = GeoServicesVersion,
            Layers = new ReadOnlyCollection<GeoservicesRESTLayerDetailResponse>(layers),
            Tables = Array.Empty<object>()
        });
    }

    [HttpGet("legend")]
    public async Task<ActionResult<GeoservicesRESTLegendResponse>> GetLegendAsync(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var legend = GeoservicesRESTLegendBuilder.BuildLegend(serviceView, snapshot, GeoServicesVersion);
        return this.Ok(legend);
    }

    [HttpGet("identify")]
    public async Task<IActionResult> IdentifyAsync(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        if (!GeoservicesRESTIdentifyTranslator.TryParse(Request, serviceView, out var identifyContext, out var error))
        {
            return error!;
        }

        var results = new List<GeoservicesRESTIdentifyResult>();
        foreach (var layerIndex in identifyContext.LayerIds)
        {
            var layerView = ResolveLayer(serviceView, layerIndex);
            if (layerView is null)
            {
                continue;
            }

            if (!IsVisibleAtScale(layerView.Layer, identifyContext.MapScale))
            {
                continue;
            }

            var query = BuildIdentifyQuery(identifyContext, layerView);
            await foreach (var record in this.repository.QueryAsync(serviceView.Service.Id, layerView.Layer.Id, query, cancellationToken).ConfigureAwait(false))
            {
                var components = FeatureComponentBuilder.BuildComponents(layerView.Layer, record, query);
                var attributes = CreateIdentifyAttributes(layerView.Layer, components);
                var geometry = identifyContext.ReturnGeometry ? AsJsonObject(components.GeometryNode) : null;
                var geometryType = identifyContext.ReturnGeometry
                    ? GeoservicesRESTMetadataMapper.MapGeometryType(layerView.Layer.GeometryType ?? string.Empty)
                    : "esriGeometryNull";

                results.Add(new GeoservicesRESTIdentifyResult
                {
                    LayerId = layerIndex,
                    LayerName = layerView.Layer.Title ?? layerView.Layer.Id,
                    DisplayFieldName = layerView.Layer.DisplayField ?? layerView.Layer.IdField,
                    GeometryType = geometryType,
                    Value = components.DisplayName,
                    Attributes = attributes,
                    Geometry = geometry
                });
            }
        }

        GeoservicesRESTSpatialReference? spatialReference = null;
        if (identifyContext.OutputWkid.HasValue)
        {
            spatialReference = new GeoservicesRESTSpatialReference { Wkid = identifyContext.OutputWkid.Value };
        }

        return this.Ok(new GeoservicesRESTIdentifyResponse
        {
            Results = results,
            SpatialReference = spatialReference
        });
    }

    [HttpGet("find")]
    public async Task<IActionResult> FindAsync(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        if (!GeoservicesRESTFindTranslator.TryParse(Request, serviceView, out var context, out var error))
        {
            return error!;
        }

        var results = new List<GeoservicesRESTFindResult>();
        foreach (var layerIndex in context.LayerIds)
        {
            var layerView = ResolveLayer(serviceView, layerIndex);
            if (layerView is null)
            {
                continue;
            }

            var query = BuildFindQuery(context, layerView);
            await foreach (var record in this.repository.QueryAsync(serviceView.Service.Id, layerView.Layer.Id, query, cancellationToken).ConfigureAwait(false))
            {
                var components = FeatureComponentBuilder.BuildComponents(layerView.Layer, record, query);
                var matchField = DetermineMatchField(context.SearchFields, components.Properties, components.DisplayName, context.SearchText);
                var value = matchField is not null && components.Properties.TryGetValue(matchField, out var fieldValue)
                    ? fieldValue
                    : components.DisplayName;

                results.Add(new GeoservicesRESTFindResult
                {
                    LayerId = layerIndex,
                    LayerName = layerView.Layer.Title ?? layerView.Layer.Id,
                    DisplayFieldName = layerView.Layer.DisplayField ?? layerView.Layer.IdField,
                    FoundFieldName = matchField ?? context.SearchFields.FirstOrDefault() ?? layerView.Layer.DisplayField ?? layerView.Layer.IdField,
                    Value = value,
                    Attributes = components.Properties,
                    Geometry = context.ReturnGeometry ? AsJsonObject(components.GeometryNode) : null
                });
            }
        }

        return this.Ok(new GeoservicesRESTFindResponse { Results = results });
    }

    private FeatureQuery BuildFindQuery(GeoservicesRESTFindContext context, CatalogLayerView layerView)
    {
        var expression = GeoservicesRESTFindTranslator.BuildFindExpression(context, layerView.Layer);
        var filter = expression is null ? null : new QueryFilter(expression);
        return new FeatureQuery(
            Limit: context.MaxRecordCount,
            Offset: 0,
            Filter: filter,
            ResultType: FeatureResultType.Results,
            Crs: context.TargetCrs);
    }

    private static string? DetermineMatchField(
        IReadOnlyList<string> searchFields,
        IReadOnlyDictionary<string, object?> attributes,
        string? displayName,
        string searchText)
    {
        foreach (var field in searchFields)
        {
            if (attributes.TryGetValue(field, out var raw) && raw is not null)
            {
                var candidate = Convert.ToString(raw, CultureInfo.InvariantCulture);
                if (!candidate.IsNullOrEmpty() && Contains(candidate, searchText))
                {
                    return field;
                }
            }
        }

        if (!displayName.IsNullOrEmpty() && Contains(displayName, searchText))
        {
            return null;
        }

        return searchFields.FirstOrDefault();
    }

    private static bool Contains(string candidate, string searchText)
    {
        return candidate.IndexOfIgnoreCase(searchText) >= 0;
    }

    private FeatureQuery BuildIdentifyQuery(GeoservicesRESTIdentifyContext context, CatalogLayerView layerView)
    {
        var sortOrders = new List<FeatureSortOrder>();
        if (layerView.Layer.IdField.HasValue())
        {
            sortOrders.Add(new FeatureSortOrder(layerView.Layer.IdField, FeatureSortDirection.Ascending));
        }

        return new FeatureQuery(
            Limit: null,
            Offset: null,
            Bbox: context.Geometry,
            Filter: context.Filter,
            SortOrders: sortOrders,
            ResultType: FeatureResultType.Results,
            Crs: layerView.Layer.Storage?.Srid is int srid ? $"EPSG:{srid}" : null);
    }

    private static IReadOnlyDictionary<string, object?> CreateIdentifyAttributes(LayerDefinition layer, FeatureComponents components)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (layer.IdField.HasValue() && components.RawId is not null)
        {
            attributes[layer.IdField] = components.RawId;
        }

        foreach (var pair in components.Properties)
        {
            attributes[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(attributes);
    }

    private static JsonObject? AsJsonObject(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonObject obj)
        {
            return obj;
        }

        var json = node.ToJsonString();
        return JsonNode.Parse(json) as JsonObject;
    }

    private CatalogServiceView? ResolveService(string folderId, string serviceId)
    {
        if (serviceId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var service = this.catalog.GetService(serviceId);
        if (service is null)
        {
            return null;
        }

        if (!service.Service.FolderId.EqualsIgnoreCase(folderId))
        {
            return null;
        }

        return SupportsMapServer(service.Service) ? service : null;
    }

    private async Task<StyleDefinition?> ResolveDefaultStyleAsync(CatalogLayerView layerView, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return StyleResolutionHelper.ResolveStyleForLayer(snapshot, layerView.Layer, null);
    }

    private async Task<StyleDefinition?> ResolveStyleAsync(string styleId, LayerDefinition layer, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return StyleResolutionHelper.ResolveStyleForLayer(snapshot, layer, styleId);
    }

    private static CatalogLayerView? ResolveLayer(CatalogServiceView serviceView, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= serviceView.Layers.Count)
        {
            return null;
        }

        return serviceView.Layers[layerIndex];
    }

    private static bool SupportsMapServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("MapServer")
            || service.ServiceType.EqualsIgnoreCase("map")
            || service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature");
    }

    private static bool IsVisibleAtScale(LayerDefinition layer, double? mapScale)
    {
        if (!mapScale.HasValue || mapScale.Value <= 0)
        {
            return true;
        }

        var scale = mapScale.Value;

        if (layer.MinScale is double minScale && minScale > 0 && scale > minScale)
        {
            return false;
        }

        if (layer.MaxScale is double maxScale && maxScale > 0 && scale < maxScale)
        {
            return false;
        }

        return true;
    }

    [HttpGet("generateKml")]
    public async Task<IActionResult> GenerateKmlAsync(string folderId, string serviceId, CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        // Parse common parameters
        var layerIdsParam = this.Request.Query["layers"].ToString();
        if (layerIdsParam.IsNullOrWhiteSpace())
        {
            return this.BadRequest(new { error = "Parameter 'layers' is required." });
        }

        var docName = this.Request.Query["docName"].ToString();
        if (docName.IsNullOrWhiteSpace())
        {
            docName = serviceView.Service.Title ?? serviceView.Service.Id;
        }

        // Parse layer IDs
        var layerIds = layerIdsParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToList();

        if (layerIds.Count == 0)
        {
            return this.BadRequest(new { error = "At least one layer must be specified." });
        }

        // Build KML document with folders for each layer
        var kmlBuilder = new System.Text.StringBuilder();
        kmlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        kmlBuilder.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        kmlBuilder.AppendLine("  <Document>");
        kmlBuilder.AppendLine($"    <name>{System.Security.SecurityElement.Escape(docName)}</name>");

        foreach (var layerIndex in layerIds)
        {
            var layerView = ResolveLayer(serviceView, layerIndex);
            if (layerView is null)
            {
                continue;
            }

            var query = new FeatureQuery(
                Limit: 1000,
                ResultType: FeatureResultType.Results,
                Crs: "EPSG:4326");

            var features = new List<Core.Serialization.KmlFeatureContent>();
            await foreach (var record in this.repository.QueryAsync(serviceView.Service.Id, layerView.Layer.Id, query, cancellationToken).ConfigureAwait(false))
            {
                var components = FeatureComponentBuilder.BuildComponents(layerView.Layer, record, query);
                var featureId = record.Attributes.TryGetValue(layerView.Layer.IdField, out var id)
                    ? Convert.ToString(id, CultureInfo.InvariantCulture)
                    : null;

                features.Add(new Core.Serialization.KmlFeatureContent(
                    featureId,
                    components.DisplayName,
                    components.GeometryNode,
                    components.Properties));
            }

            if (features.Count > 0)
            {
                var layerKml = Core.Serialization.KmlFeatureFormatter.WriteFeatureCollection(
                    layerView.Layer.Id,
                    layerView.Layer,
                    features,
                    features.Count,
                    features.Count);

                // Extract placemarks from the generated KML and wrap in a folder
                var startIndex = layerKml.IndexOf("<Placemark>", StringComparison.Ordinal);
                if (startIndex >= 0)
                {
                    var endIndex = layerKml.LastIndexOf("</Placemark>", StringComparison.Ordinal);
                    if (endIndex >= 0)
                    {
                        var placemarks = layerKml.Substring(startIndex, endIndex - startIndex + "</Placemark>".Length);
                        kmlBuilder.AppendLine("    <Folder>");
                        kmlBuilder.AppendLine($"      <name>{System.Security.SecurityElement.Escape(layerView.Layer.Title ?? layerView.Layer.Id)}</name>");
                        kmlBuilder.AppendLine($"      {placemarks}");
                        kmlBuilder.AppendLine("    </Folder>");
                    }
                }
            }
        }

        kmlBuilder.AppendLine("  </Document>");
        kmlBuilder.AppendLine("</kml>");

        var kmz = this.Request.Query["kmz"].ToString();
        var useKmz = kmz.EqualsIgnoreCase("true");

        if (useKmz)
        {
            // Create KMZ (zipped KML)
            var kmzBytes = Core.Serialization.KmzArchiveBuilder.CreateArchive(kmlBuilder.ToString());
            return this.File(kmzBytes, "application/vnd.google-earth.kmz", $"{docName}.kmz");
        }

        return Content(kmlBuilder.ToString(), "application/vnd.google-earth.kml+xml");
    }
}
