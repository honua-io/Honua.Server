// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Styling;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Query endpoints for feature queries, statistics, distinct values, and related records.
/// Endpoints:
/// - GET/POST /{layerIndex}/query (Query)
/// - GET/POST /{layerIndex}/queryRelatedRecords (QueryRelatedRecords)
/// </summary>
public sealed partial class GeoservicesRESTFeatureServerController
{
    /// <summary>
    /// Query features from a layer.
    /// Route: GET /rest/services/{serviceId}/FeatureServer/{layerIndex}/query
    /// </summary>
    [HttpGet("{layerIndex:int}/query")]
    public async Task<IActionResult> QueryAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return await ActivityScope.Create(HonuaTelemetry.OgcProtocols, "ArcGIS FeatureServer Query")
            .WithTag("arcgis.operation", "Query")
            .WithTag("arcgis.service", serviceId)
            .WithTag("arcgis.layer_index", layerIndex)
            .ExecuteAsync<IActionResult>(async activity =>
            {
                var resolution = GeoservicesRESTServiceResolutionHelper.ResolveServiceAndLayer(this, _catalog, folderId, serviceId, layerIndex);
                if (resolution.Error is not null)
                {
                    return resolution.Error;
                }

                var serviceView = resolution.ServiceView!;
                var layerView = resolution.LayerView!;

                if (!GeoservicesRESTQueryTranslator.TryParse(Request, serviceView, layerView, out var context, out var error, _logger))
                {
                    return error!;
                }

                var scaleSuppressed = !IsVisibleAtScale(layerView.Layer, context.MapScale);

                if (context.Statistics.Count > 0)
                {
                    if (context.Format != GeoservicesResponseFormat.Json)
                    {
                        return BadRequest(new { error = "outStatistics is only supported for f=json requests." });
                    }

                    // FIX (Bug 38): Reject statistics requests at scale suppression to avoid
                    // heavy processing that will return empty results. This prevents resource
                    // waste and provides clearer feedback to the client.
                    if (scaleSuppressed)
                    {
                        return BadRequest(new { error = "Layer is not visible at the specified map scale. Statistics cannot be computed." });
                    }

                    return await _queryService.ExecuteQueryAsync(serviceView, layerView, context, cancellationToken).ConfigureAwait(false);
                }

                if (context.ReturnDistinctValues)
                {
                    if (context.Format != GeoservicesResponseFormat.Json)
                    {
                        return BadRequest(new { error = "returnDistinctValues is only supported for f=json requests." });
                    }

                    if (scaleSuppressed)
                    {
                        var emptyDistinct = CreateEmptyDistinctFeatureSet(layerView.Layer, context);
                        return WriteJson(emptyDistinct, context);
                    }

                    return await _queryService.ExecuteQueryAsync(serviceView, layerView, context, cancellationToken).ConfigureAwait(false);
                }

                if (context.Format == GeoservicesResponseFormat.Shapefile)
                {
                    if (!context.ReturnGeometry)
                    {
                        return BadRequest(new { error = "Shapefile export requires returnGeometry=true." });
                    }

                    if (context.ReturnCountOnly || context.ReturnIdsOnly)
                    {
                        return BadRequest(new { error = "Shapefile export is not available for count or ID-only queries." });
                    }

                    if (scaleSuppressed)
                    {
                        return await ExportEmptyShapefileAsync(layerView.Layer, context, cancellationToken);
                    }

                    return await ExportShapefileAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                }

                if (context.Format == GeoservicesResponseFormat.Csv)
                {
                    // FIX (Bug 39): Check both ReturnCountOnly and ReturnIdsOnly for CSV export
                    // Previously only checked ReturnCountOnly, causing confusing error for returnIdsOnly=true
                    if (context.ReturnCountOnly)
                    {
                        return BadRequest(new { error = "CSV export is not available for count-only queries. Remove returnCountOnly parameter." });
                    }

                    if (context.ReturnIdsOnly)
                    {
                        return BadRequest(new { error = "CSV export is not available for ID-only queries. Remove returnIdsOnly parameter." });
                    }

                    if (scaleSuppressed)
                    {
                        return await ExportEmptyCsvAsync(layerView.Layer, context, cancellationToken);
                    }

                    return await ExportCsvAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                }

                if (IsKmlFormat(context.Format))
                {
                    if (!context.ReturnGeometry)
                    {
                        return BadRequest(new { error = "KML export requires returnGeometry=true." });
                    }

                    if (context.ReturnCountOnly || context.ReturnIdsOnly)
                    {
                        return BadRequest(new { error = "KML export is not available for count or ID-only queries." });
                    }

                    // FIX (Bug 40): Retrieve default style for KML export to preserve layer styling
                    var style = await ResolveDefaultStyleAsync(layerView.Layer, cancellationToken).ConfigureAwait(false);

                    if (scaleSuppressed)
                    {
                        return await ExportEmptyKmlAsync(serviceView.Service, layerView, context, style);
                    }

                    return await ExportKmlAsync(serviceView, layerView, context, style, cancellationToken);
                }

                if (context.ReturnExtentOnly && context.Format != GeoservicesResponseFormat.Json)
                {
                    return BadRequest(new { error = "returnExtentOnly is only supported for f=json requests." });
                }

                if (scaleSuppressed)
                {
                    return await BuildScaleSuppressedResponseAsync(serviceView, layerView, context);
                }

                // Handle export formats that require special processing
                // PERFORMANCE FIX: These formats stream directly to response without materializing full result sets
                switch (context.Format)
                {
                    case GeoservicesResponseFormat.GeoJson:
                        return await WriteGeoJsonStreamingAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                    case GeoservicesResponseFormat.TopoJson:
                        return await WriteTopoJsonStreamingAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                    case GeoservicesResponseFormat.Kml:
                        return await WriteKmlAsync(serviceView.Service, layerView.Layer, context, kmz: false, cancellationToken);
                    case GeoservicesResponseFormat.Kmz:
                        return await WriteKmlAsync(serviceView.Service, layerView.Layer, context, kmz: true, cancellationToken);
                    case GeoservicesResponseFormat.Wkt:
                        return await WriteWktStreamingAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                    case GeoservicesResponseFormat.Wkb:
                        return await WriteWkbStreamingAsync(serviceView.Service, layerView.Layer, context, cancellationToken);
                    default:
                        // Delegate JSON format queries to the query service
                        return await _queryService.ExecuteQueryAsync(serviceView, layerView, context, cancellationToken);
                }
            });
    }

    /// <summary>
    /// Query features from a layer (POST).
    /// Route: POST /rest/services/{serviceId}/FeatureServer/{layerIndex}/query
    /// </summary>
    [HttpPost("{layerIndex:int}/query")]
    public Task<IActionResult> QueryPostAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return QueryAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    /// <summary>
    /// Query related records.
    /// Route: GET /rest/services/{serviceId}/FeatureServer/{layerIndex}/queryRelatedRecords
    /// </summary>
    [HttpGet("{layerIndex:int}/queryRelatedRecords")]
    public Task<IActionResult> QueryRelatedRecordsGetAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return QueryRelatedRecordsInternalAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    /// <summary>
    /// Query related records (POST).
    /// Route: POST /rest/services/{serviceId}/FeatureServer/{layerIndex}/queryRelatedRecords
    /// </summary>
    [HttpPost("{layerIndex:int}/queryRelatedRecords")]
    public Task<IActionResult> QueryRelatedRecordsPostAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return QueryRelatedRecordsInternalAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    private async Task<IActionResult> QueryRelatedRecordsInternalAsync(
        string folderId,
        string serviceId,
        int layerIndex,
        CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return NotFound();
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return NotFound();
        }

        // Delegate to the query service for the actual implementation
        return await _queryService.ExecuteRelatedRecordsQueryAsync(serviceView, layerView, Request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GeoservicesRESTFeatureSetResponse> FetchDistinctAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        var distinctFields = ResolveDistinctFields(layer, context);
        var fieldDefinitions = BuildFieldLookup(layer);

        // Use database-level SELECT DISTINCT instead of in-memory deduplication
        // This prevents loading up to 100k rows into memory and performs better
        var requestedLimit = context.Query.Limit ?? DefaultMaxRecordCount;
        var offset = context.Query.Offset ?? 0;

        // Apply the limit and offset directly in the query for database-level paging
        var filterQuery = context.Query with
        {
            // Important: The repository's QueryDistinctAsync will apply limit/offset at DB level
            Limit = requestedLimit,
            Offset = offset,
            ResultType = FeatureResultType.Results
        };

        // Delegate to repository which uses SELECT DISTINCT at database level
        var distinctResults = await _repository.QueryDistinctAsync(
            service.Id,
            layer.Id,
            distinctFields,
            filterQuery,
            cancellationToken).ConfigureAwait(false);

        // Check if we got the maximum results - if so, there may be more
        // Note: The database provider enforces its own limits to prevent unbounded queries
        var exceededTransferLimit = distinctResults.Count >= requestedLimit;

        var features = new List<GeoservicesRESTFeature>(distinctResults.Count);
        foreach (var result in distinctResults)
        {
            features.Add(new GeoservicesRESTFeature
            {
                Attributes = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(result.Values, StringComparer.OrdinalIgnoreCase)),
                Geometry = null
            });
        }

        var fields = BuildDistinctFieldDefinitions(distinctFields, fieldDefinitions);

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = "esriGeometryNull",
            SpatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid },
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = exceededTransferLimit
        };
    }

    private static IReadOnlyList<string> ResolveDistinctFields(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        if (context.RequestedOutFields is null)
        {
            return layer.Fields
                .Where(field => !field.Name.EqualsIgnoreCase(layer.GeometryField))
                .Select(field => field.Name)
                .ToArray();
        }

        var requested = context.RequestedOutFields.ToList();
        var filtered = requested
            .Where(field => !field.EqualsIgnoreCase(layer.GeometryField))
            .ToList();

        if (filtered.Count == 0)
        {
            filtered.Add(layer.IdField);
        }

        return filtered;
    }

    private static Dictionary<string, GeoservicesRESTFieldInfo> BuildFieldLookup(LayerDefinition layer)
    {
        var definitions = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        var lookup = new Dictionary<string, GeoservicesRESTFieldInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            lookup[definition.Name] = definition;
        }

        return lookup;
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> BuildDistinctFieldDefinitions(
        IReadOnlyList<string> fields,
        Dictionary<string, GeoservicesRESTFieldInfo> lookup)
    {
        var results = new List<GeoservicesRESTFieldInfo>(fields.Count);
        foreach (var field in fields)
        {
            if (lookup.TryGetValue(field, out var definition))
            {
                results.Add(definition);
            }
            else
            {
                results.Add(new GeoservicesRESTFieldInfo
                {
                    Name = field,
                    Alias = field,
                    Type = "esriFieldTypeString",
                    Nullable = true,
                    Editable = false
                });
            }
        }

        return new ReadOnlyCollection<GeoservicesRESTFieldInfo>(results);
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> BuildStatisticsFieldDefinitions(
        LayerDefinition layer,
        IReadOnlyList<string> groupFields,
        IReadOnlyList<GeoservicesRESTStatisticDefinition> statistics)
    {
        var baseDefinitions = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer)
            .ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);

        var fields = new List<GeoservicesRESTFieldInfo>();

        foreach (var groupField in groupFields)
        {
            if (baseDefinitions.TryGetValue(groupField, out var definition))
            {
                fields.Add(definition);
            }
            else if (groupField.EqualsIgnoreCase(layer.IdField))
            {
                fields.Add(new GeoservicesRESTFieldInfo
                {
                    Name = layer.IdField,
                    Alias = layer.IdField,
                    Type = "esriFieldTypeOID",
                    Nullable = false,
                    Editable = false
                });
            }
        }

        foreach (var statistic in statistics)
        {
            var fieldType = statistic.Type switch
            {
                GeoservicesRESTStatisticType.Count => "esriFieldTypeInteger",
                GeoservicesRESTStatisticType.Sum => "esriFieldTypeDouble",
                GeoservicesRESTStatisticType.Avg => "esriFieldTypeDouble",
                GeoservicesRESTStatisticType.Min or GeoservicesRESTStatisticType.Max => ResolveStatisticFieldType(statistic, baseDefinitions, layer),
                _ => "esriFieldTypeDouble"
            };

            fields.Add(new GeoservicesRESTFieldInfo
            {
                Name = statistic.OutputName,
                Alias = statistic.OutputName,
                Type = fieldType,
                Nullable = true,
                Editable = false
            });
        }

        return new ReadOnlyCollection<GeoservicesRESTFieldInfo>(fields);
    }

    private static string ResolveStatisticFieldType(
        GeoservicesRESTStatisticDefinition statistic,
        IReadOnlyDictionary<string, GeoservicesRESTFieldInfo> baseDefinitions,
        LayerDefinition layer)
    {
        if (statistic.FieldName.HasValue())
        {
            if (baseDefinitions.TryGetValue(statistic.FieldName!, out var definition))
            {
                return definition.Type;
            }

            if (statistic.FieldName.EqualsIgnoreCase(layer.IdField))
            {
                return "esriFieldTypeInteger";
            }
        }

        return "esriFieldTypeDouble";
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

    private GeoservicesRESTFeatureSetResponse CreateEmptyStatisticsFeatureSet(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        var fields = BuildStatisticsFieldDefinitions(layer, context.GroupByFields, context.Statistics);
        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = "esriGeometryNull",
            SpatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid },
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(Array.Empty<GeoservicesRESTFeature>()),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = false
        };
    }

    private GeoservicesRESTFeatureSetResponse CreateEmptyDistinctFeatureSet(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        var distinctFields = ResolveDistinctFields(layer, context);
        var fieldDefinitions = BuildDistinctFieldDefinitions(distinctFields, BuildFieldLookup(layer));
        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = "esriGeometryNull",
            SpatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid },
            Fields = fieldDefinitions,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(Array.Empty<GeoservicesRESTFeature>()),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = false
        };
    }

    private async Task<GeoservicesRESTExtent?> CalculateExtentAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        // PERFORMANCE FIX: Delegate to query service which uses database-level ST_Extent
        // instead of loading all geometries into memory.
        return await _queryService.CalculateExtentAsync(
            service.Id,
            layer,
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private GeoservicesRESTFeature CreateRestFeature(LayerDefinition layer, FeatureRecord record, GeoservicesRESTQueryContext context, string geometryType)
    {
        var components = FeatureComponentBuilder.BuildComponents(layer, record, context.Query);
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in components.Properties)
        {
            if (context.SelectedFields.Count > 0 && !context.SelectedFields.ContainsKey(pair.Key))
            {
                continue;
            }

            attributes[pair.Key] = ConvertAttributeValue(pair.Value);
        }

        if (!attributes.ContainsKey(layer.IdField) && record.Attributes.TryGetValue(layer.IdField, out var idValue))
        {
            attributes[layer.IdField] = ConvertAttributeValue(idValue);
        }

        var geometry = context.ReturnGeometry
            ? GeoservicesRESTGeometryConverter.ToGeometry(components.GeometryNode, geometryType, context.TargetWkid)
            : null;

        return new GeoservicesRESTFeature
        {
            Attributes = new ReadOnlyDictionary<string, object?>(attributes),
            Geometry = geometry
        };
    }

    private Task<IActionResult> BuildScaleSuppressedResponseAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context)
    {
        if (context.ReturnCountOnly && context.ReturnExtentOnly)
        {
            var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };
            return Task.FromResult<IActionResult>(WriteJson(new GeoservicesRESTQueryExtentResponse
            {
                Count = 0,
                Extent = null,
                SpatialReference = spatialReference
            }, context));
        }

        if (context.ReturnCountOnly)
        {
            return Task.FromResult<IActionResult>(WriteJson(new GeoservicesRESTCountResponse { Count = 0 }, context));
        }

        if (context.ReturnIdsOnly)
        {
            var idsResponse = new GeoservicesRESTIdsResponse
            {
                ObjectIdFieldName = layerView.Layer.IdField,
                UniqueIdField = new GeoservicesRESTUniqueIdField
                {
                    Name = layerView.Layer.IdField,
                    IsSystemMaintained = false
                },
                ObjectIds = Array.Empty<object>(),
                ExceededTransferLimit = false
            };

            return Task.FromResult<IActionResult>(WriteJson(idsResponse, context));
        }

        if (context.ReturnExtentOnly)
        {
            var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };
            return Task.FromResult<IActionResult>(WriteJson(new GeoservicesRESTQueryExtentResponse
            {
                Count = 0,
                Extent = null,
                SpatialReference = spatialReference
            }, context));
        }

        switch (context.Format)
        {
            case GeoservicesResponseFormat.GeoJson:
                return Task.FromResult<IActionResult>(CreateEmptyGeoJsonResponse(layerView.Layer, context));
            case GeoservicesResponseFormat.TopoJson:
                return Task.FromResult<IActionResult>(CreateEmptyTopoJsonResponse(serviceView.Service, layerView.Layer, context));
            default:
                {
                    var featureSet = CreateEmptyFeatureSetResponse(layerView.Layer, context);
                    return Task.FromResult<IActionResult>(WriteJson(featureSet, context));
                }
        }
    }

    private GeoservicesRESTFeatureSetResponse CreateEmptyFeatureSetResponse(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = GeoservicesRESTMetadataMapper.MapGeometryType(layer.GeometryType),
            SpatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid },
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(Array.Empty<GeoservicesRESTFeature>()),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = false
        };
    }

    private IActionResult CreateEmptyGeoJsonResponse(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";

        var response = new
        {
            type = "FeatureCollection",
            name = layer.Title ?? layer.Id,
            features = Array.Empty<object>(),
            numberMatched = 0L,
            numberReturned = 0,
            timeStamp = DateTimeOffset.UtcNow
        };

        return CreateJsonResult(response, context.PrettyPrint, "application/geo+json");
    }

    private IActionResult CreateEmptyTopoJsonResponse(ServiceDefinition service, LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        Response.Headers["Content-Crs"] = $"EPSG:{context.TargetWkid}";

        var collectionId = BuildCollectionIdentifier(service, layer);
        var payload = TopoJsonFeatureFormatter.WriteFeatureCollection(
            collectionId,
            layer,
            Array.Empty<TopoJsonFeatureContent>(),
            0,
            0);

        return Content(payload, "application/topo+json");
    }

    // OBSOLETE: This method materializes entire result sets in memory, causing memory exhaustion.
    // It is not currently used and should be removed. Use streaming approaches instead.
    [Obsolete("This method buffers all features in memory. Use streaming query services instead.", error: true)]
    private async Task<GeoservicesRESTFeatureSetResponse> FetchFeaturesAsync(
        ServiceDefinition service,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        long? totalCount,
        CancellationToken cancellationToken)
    {
        var geometryType = GeoservicesRESTMetadataMapper.MapGeometryType(layer.GeometryType);
        var features = new List<GeoservicesRESTFeature>();

        await foreach (var record in _repository.QueryAsync(service.Id, layer.Id, context.Query, cancellationToken).ConfigureAwait(false))
        {
            var restFeature = CreateRestFeature(layer, record, context, geometryType);
            features.Add(restFeature);
        }

        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        var exceeded = false;
        if (context.Query.Limit.HasValue)
        {
            var offset = context.Query.Offset ?? 0;
            if (totalCount.HasValue)
            {
                exceeded = totalCount.Value > offset + features.Count;
            }
            else
            {
                exceeded = features.Count >= context.Query.Limit.Value;
            }
        }

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = geometryType,
            SpatialReference = spatialReference,
            Fields = fields,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false,
            ExceededTransferLimit = exceeded
        };
    }
}
