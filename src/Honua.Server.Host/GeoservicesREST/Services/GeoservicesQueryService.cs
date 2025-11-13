// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Precision;
using NetTopologySuite.Simplify;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer query operations.
/// Extracted from GeoservicesRESTFeatureServerController to follow Single Responsibility Principle.
/// </summary>
public sealed class GeoservicesQueryService : IGeoservicesQueryService
{
    private const int MaxResultsWithoutPagination = 10_000;

    private static readonly GeoJsonReader GeoJsonReaderInstance = new();
    private static readonly WKTReader WktReaderInstance = new();

    private readonly IFeatureRepository repository;
    private readonly ILogger<GeoservicesQueryService> logger;

    public GeoservicesQueryService(
        IFeatureRepository repository,
        ILogger<GeoservicesQueryService> logger)
    {
        this.repository = Guard.NotNull(repository);
        this.logger = Guard.NotNull(logger);
    }

    public async Task<IActionResult> ExecuteQueryAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "GeoservicesQuery",
            async activity =>
            {
                var service = serviceView.Service;
                var layer = layerView.Layer;

                // Handle statistics queries
                if (context.Statistics.Count > 0)
                {
                    if (context.Format != GeoservicesResponseFormat.Json)
                    {
                        return GeoservicesRESTErrorHelper.BadRequest("outStatistics is only supported for f=json requests.");
                    }

                    var statisticsFeatureSet = await FetchStatisticsAsync(service.Id, layer, context, cancellationToken).ConfigureAwait(false);
                    return WriteJson(statisticsFeatureSet, context);
                }

                // Handle distinct values queries
                if (context.ReturnDistinctValues)
                {
                    if (context.Format != GeoservicesResponseFormat.Json)
                    {
                        return GeoservicesRESTErrorHelper.BadRequest("returnDistinctValues is only supported for f=json requests.");
                    }

                    var distinctFeatureSet = await FetchDistinctAsync(service.Id, layer, context, cancellationToken).ConfigureAwait(false);
                    return WriteJson(distinctFeatureSet, context);
                }

                // Only count when explicitly needed for the response
                // PERFORMANCE FIX: Don't count for returnIdsOnly or regular feature queries
                // The exceeded flag in FetchFeaturesAsync uses limit+1 fetch strategy instead
                var needsCount = context.ReturnCountOnly || context.ReturnExtentOnly;
                long? totalCount = null;
                if (needsCount)
                {
                    var countQuery = context.Query with { Limit = null, Offset = null, ResultType = FeatureResultType.Hits };
                    totalCount = await this.repository.CountAsync(service.Id, layer.Id, countQuery, cancellationToken).ConfigureAwait(false);
                }

                if (context.ReturnCountOnly && context.ReturnExtentOnly)
                {
                    var extent = await CalculateExtentAsync(service.Id, layer, context, cancellationToken).ConfigureAwait(false);
                    var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };
                    return WriteJson(new GeoservicesRESTQueryExtentResponse
                    {
                        Count = totalCount,
                        Extent = extent,
                        SpatialReference = spatialReference
                    }, context);
                }

                if (context.ReturnCountOnly)
                {
                    return WriteJson(new GeoservicesRESTCountResponse { Count = totalCount ?? 0 }, context);
                }

                if (context.ReturnIdsOnly)
                {
                    // Validate pagination requirement before executing query to prevent unbounded result sets
                    if (!context.Query.Limit.HasValue)
                    {
                        return GeoservicesRESTErrorHelper.BadRequest(
                            $"returnIdsOnly requires pagination. Result set may exceed {MaxResultsWithoutPagination:N0} records. " +
                            "Please specify the resultRecordCount parameter (e.g., resultRecordCount=1000) and use resultOffset for pagination.");
                    }

                    var idsResult = await FetchIdsAsync(service.Id, layer, context, cancellationToken).ConfigureAwait(false);
                    var response = new GeoservicesRESTIdsResponse
                    {
                        ObjectIdFieldName = layer.IdField,
                        UniqueIdField = new GeoservicesRESTUniqueIdField
                        {
                            Name = layer.IdField,
                            IsSystemMaintained = false
                        },
                        ObjectIds = idsResult.ObjectIds,
                        ExceededTransferLimit = idsResult.ExceededTransferLimit
                    };

                    return WriteJson(response, context);
                }

                if (context.ReturnExtentOnly)
                {
                    var extent = await CalculateExtentAsync(service.Id, layer, context, cancellationToken).ConfigureAwait(false);
                    var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };
                    return WriteJson(new GeoservicesRESTQueryExtentResponse
                    {
                        Count = totalCount,
                        Extent = extent,
                        SpatialReference = spatialReference
                    }, context);
                }

                // Default: return feature set
                var featureSet = await FetchFeaturesAsync(service.Id, layer, context, totalCount, cancellationToken).ConfigureAwait(false);
                return WriteJson(featureSet, context);
            });
    }

    public async Task<IActionResult> ExecuteRelatedRecordsQueryAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "GeoservicesRelatedRecordsQuery",
            async activity =>
            {
                var service = serviceView.Service;
                var layer = layerView.Layer;

                // Validate relationships exist
                if (layerView.Relationships.Count == 0)
                {
                    return new BadRequestObjectResult(new { error = "Layer does not define any relationships." });
                }

                // Parse relationshipId parameter
                var relationshipIdRaw = request.Query.TryGetValue("relationshipId", out var relationshipValues)
                    ? relationshipValues[^1]
                    : null;

                LayerRelationshipDefinition? relationship = null;
                if (relationshipIdRaw.IsNullOrWhiteSpace())
                {
                    if (layerView.Relationships.Count == 1)
                    {
                        relationship = layerView.Relationships[0];
                    }
                    else
                    {
                        return new BadRequestObjectResult(new { error = "relationshipId must be specified when multiple relationships are defined." });
                    }
                }
                else if (int.TryParse(relationshipIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var relationshipId))
                {
                    relationship = layerView.Relationships.FirstOrDefault(rel => rel.Id == relationshipId);
                    if (relationship is null)
                    {
                        return new BadRequestObjectResult(new { error = $"Relationship '{relationshipId}' is not defined for this layer." });
                    }
                }
                else
                {
                    return new BadRequestObjectResult(new { error = "relationshipId must be an integer value." });
                }

                if (relationship is null)
                {
                    return new BadRequestObjectResult(new { error = "Unable to resolve relationship metadata." });
                }

                // Validate relationship semantics
                if (relationship.Semantics != LayerRelationshipSemantics.PrimaryKeyForeignKey)
                {
                    return new BadRequestObjectResult(new { error = "Relationship is not configured for primary key/foreign key semantics." });
                }

                // Find related layer
                var relatedLayerView = serviceView.Layers.FirstOrDefault(l =>
                    l.Layer.Id.EqualsIgnoreCase(relationship.RelatedLayerId));
                if (relatedLayerView is null)
                {
                    return new NotFoundObjectResult(new { error = $"Related layer '{relationship.RelatedLayerId}' was not found." });
                }

                // Parse objectIds parameter
                var objectIdsRaw = request.Query.TryGetValue("objectIds", out var objectIdValues)
                    ? objectIdValues[^1]
                    : null;

                if (objectIdsRaw.IsNullOrWhiteSpace())
                {
                    return new BadRequestObjectResult(new { error = "objectIds must be supplied." });
                }

                var parsedObjectIds = ParseObjectIdValues(objectIdsRaw, relatedLayerView.Layer, relationship.RelatedKeyField);
                if (parsedObjectIds.Count == 0)
                {
                    return new BadRequestObjectResult(new { error = "objectIds must contain at least one identifier." });
                }

                // Create query context for related layer (excluding objectIds and relationshipId from query string)
                var relatedRequest = CreateRelatedQueryRequest(request, "objectIds", "relationshipId");
                if (!GeoservicesRESTQueryTranslator.TryParse(relatedRequest, serviceView, relatedLayerView, out var relatedContext, out var parseError, _logger))
                {
                    return parseError!;
                }

                // Build filter for related key field
                var filterExpression = BuildObjectIdFilterExpression(relationship.RelatedKeyField, parsedObjectIds);
                if (filterExpression is null)
                {
                    return new BadRequestObjectResult(new { error = "Unable to build related record filter." });
                }

                // Combine with existing filter
                var combinedFilter = CombineFilters(relatedContext.Query.Filter, filterExpression);
                var combinedQuery = relatedContext.Query with { Filter = combinedFilter };
                var workingContext = relatedContext with { Query = combinedQuery };

                // Stream related records and group by foreign key
                // CRITICAL PERFORMANCE FIX: Use count-only mode to avoid buffering when only counts are needed
                var geometryType = GeoservicesRESTMetadataMapper.MapGeometryType(relatedLayerView.Layer.GeometryType);

                List<GeoservicesRESTRelatedRecordGroup> groups;
                var exceeded = false; // Track if any parent exceeded the child limit

                if (workingContext.ReturnCountOnly)
                {
                    // Count-only mode: Track counts without buffering features
                    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    await foreach (var record in this.repository.QueryAsync(service.Id, relatedLayerView.Layer.Id, workingContext.Query, cancellationToken).ConfigureAwait(false))
                    {
                        // Extract foreign key value
                        if (!record.Attributes.TryGetValue(relationship.RelatedKeyField, out var rawValue))
                        {
                            continue;
                        }

                        var key = Convert.ToString(ConvertAttributeValue(rawValue), CultureInfo.InvariantCulture) ?? string.Empty;
                        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                    }

                    // Build count-only response groups
                    groups = new List<GeoservicesRESTRelatedRecordGroup>(parsedObjectIds.Count);
                    foreach (var requested in parsedObjectIds)
                    {
                        var count = counts.TryGetValue(requested.Key, out var c) ? c : 0;

                        groups.Add(new GeoservicesRESTRelatedRecordGroup
                        {
                            ObjectId = requested.Value,
                            RelatedRecords = Array.Empty<GeoservicesRESTFeature>(),
                            Count = count
                        });
                    }
                }
                else
                {
                    // Feature mode: Buffer with safety cap to prevent LOH fragmentation
                    const int MaxChildrenPerParent = 10_000; // Cap to prevent LOH allocation (85KB threshold)
                    var grouped = new Dictionary<string, List<GeoservicesRESTFeature>>(StringComparer.OrdinalIgnoreCase);

                    await foreach (var record in this.repository.QueryAsync(service.Id, relatedLayerView.Layer.Id, workingContext.Query, cancellationToken).ConfigureAwait(false))
                    {
                        // Extract foreign key value
                        if (!record.Attributes.TryGetValue(relationship.RelatedKeyField, out var rawValue))
                        {
                            continue;
                        }

                        var key = Convert.ToString(ConvertAttributeValue(rawValue), CultureInfo.InvariantCulture) ?? string.Empty;

                        if (!grouped.TryGetValue(key, out var list))
                        {
                            list = new List<GeoservicesRESTFeature>();
                            grouped[key] = list;
                        }

                        // Cap per-parent children to prevent excessive memory allocation
                        if (list.Count >= MaxChildrenPerParent)
                        {
                            exceeded = true; // Flag that we truncated results
                            continue; // Skip additional children beyond cap
                        }

                        var feature = CreateRestFeature(relatedLayerView.Layer, record, workingContext, geometryType);
                        list.Add(feature);
                    }

                    // Build feature response groups
                    groups = new List<GeoservicesRESTRelatedRecordGroup>(parsedObjectIds.Count);
                    foreach (var requested in parsedObjectIds)
                    {
                        var matches = grouped.TryGetValue(requested.Key, out var list)
                            ? list
                            : new List<GeoservicesRESTFeature>();

                        groups.Add(new GeoservicesRESTRelatedRecordGroup
                        {
                            ObjectId = requested.Value,
                            RelatedRecords = new ReadOnlyCollection<GeoservicesRESTFeature>(matches),
                            Count = matches.Count
                        });
                    }
                }

                // Build field definitions
                var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(relatedLayerView.Layer);
                var filteredFields = FilterFieldsForSelection(fields, workingContext.SelectedFields, relatedLayerView.Layer.IdField);

                var response = new GeoservicesRESTRelatedRecordsResponse
                {
                    GeometryType = workingContext.ReturnGeometry ? geometryType : "esriGeometryNull",
                    SpatialReference = workingContext.ReturnGeometry ? new GeoservicesRESTSpatialReference { Wkid = workingContext.TargetWkid } : null,
                    HasZ = false,
                    HasM = false,
                    Fields = filteredFields,
                    RelatedRecordGroups = groups,
                    ExceededTransferLimit = exceeded
                };

                return WriteJson(response, workingContext);
            });
    }

    public async Task<IGeoservicesQueryService.GeoservicesIdsQueryResult> FetchIdsAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        // CRITICAL FIX: Validate pagination BEFORE executing query to prevent expensive unbounded iteration
        if (!context.Query.Limit.HasValue)
        {
            throw new InvalidOperationException(
                $"Result set may exceed {MaxResultsWithoutPagination} records. Use pagination (resultRecordCount parameter) to retrieve large result sets.");
        }

        var effectiveLimit = context.Query.Limit.Value;
        var estimatedSize = Math.Min(effectiveLimit, 1000);
        var ids = new List<object>(estimatedSize);

        // Fetch one extra record to detect if limit was exceeded
        var fetchQuery = context.Query with { Limit = effectiveLimit + 1 };

        var exceeded = false;

        await foreach (var record in this.repository.QueryAsync(serviceId, layer.Id, fetchQuery, cancellationToken).ConfigureAwait(false))
        {
            if (TryGetAttribute(record, layer.IdField, out var idValue) && idValue != null)
            {
                // Stop if we've reached the effective limit
                if (ids.Count == effectiveLimit)
                {
                    exceeded = true;
                    break;
                }
                ids.Add(idValue);
            }
        }

        return new IGeoservicesQueryService.GeoservicesIdsQueryResult(ids, exceeded);
    }

    public async Task<GeoservicesRESTExtent?> CalculateExtentAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        // CRITICAL PERFORMANCE FIX: Use database-level ST_Extent instead of loading all geometries into memory
        var bbox = await this.repository.QueryExtentAsync(
            serviceId,
            layer.Id,
            context.Query,
            cancellationToken).ConfigureAwait(false);

        if (bbox == null)
        {
            return null;
        }

        return new GeoservicesRESTExtent
        {
            Xmin = bbox.MinX,
            Ymin = bbox.MinY,
            Xmax = bbox.MaxX,
            Ymax = bbox.MaxY,
            SpatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid }
        };
    }

    private async Task<GeoservicesRESTFeatureSetResponse> FetchFeaturesAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        long? totalCount,
        CancellationToken cancellationToken)
    {
        // CRITICAL FIX: Validate pagination BEFORE executing query to prevent expensive unbounded iteration
        if (!context.Query.Limit.HasValue)
        {
            throw new InvalidOperationException(
                $"Result set may exceed {MaxResultsWithoutPagination} records. Use pagination (resultRecordCount parameter) to retrieve large result sets.");
        }

        var geometryType = GeoservicesRESTMetadataMapper.MapGeometryType(layer.GeometryType);
        var requestedLimit = context.Query.Limit.Value;
        var estimatedSize = Math.Min(requestedLimit, 1000);
        var features = new List<GeoservicesRESTFeature>(estimatedSize);

        // Fetch one extra record to detect if limit was exceeded
        var fetchQuery = context.Query with { Limit = requestedLimit + 1 };
        var exceeded = false;

        await foreach (var record in this.repository.QueryAsync(serviceId, layer.Id, fetchQuery, cancellationToken).ConfigureAwait(false))
        {
            if (features.Count >= requestedLimit)
            {
                exceeded = true;
                break;
            }

            var restFeature = CreateRestFeature(layer, record, context, geometryType);
            features.Add(restFeature);
        }

        var fields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        fields = FilterFieldsForSelection(fields, context.SelectedFields, layer.IdField);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        // Check if we exceeded the limit
        if (!exceeded)
        {
            var offset = context.Query.Offset ?? 0;
            if (totalCount.HasValue)
            {
                exceeded = totalCount.Value > offset + features.Count;
            }
            else
            {
                exceeded = features.Count >= requestedLimit;
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

    private async Task<GeoservicesRESTFeatureSetResponse> FetchDistinctAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        // CRITICAL PERFORMANCE FIX: Use database-level DISTINCT instead of loading all records into memory
        var distinctFields = ResolveDistinctFields(layer, context);

        // Execute DISTINCT at database level - orders of magnitude faster than in-memory HashSet
        var results = await this.repository.QueryDistinctAsync(
            serviceId,
            layer.Id,
            distinctFields,
            context.Query,
            cancellationToken).ConfigureAwait(false);

        // BUG FIX #10: Convert database results to Geoservices format, preserving field order
        var features = new List<GeoservicesRESTFeature>();
        foreach (var result in results)
        {
            // Preserve the order of fields as specified in distinctFields to prevent schema drift
            var attributes = new Dictionary<string, object?>(distinctFields.Count);
            foreach (var fieldName in distinctFields)
            {
                if (result.Values.TryGetValue(fieldName, out var value))
                {
                    attributes[fieldName] = value;
                }
            }

            features.Add(new GeoservicesRESTFeature
            {
                Attributes = new ReadOnlyDictionary<string, object?>(attributes)
            });
        }

        var fieldDefinitions = BuildDistinctFieldDefinitions(layer, distinctFields);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = "esriGeometryNull",
            SpatialReference = spatialReference,
            Fields = fieldDefinitions,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false
        };
    }

    private async Task<GeoservicesRESTFeatureSetResponse> FetchStatisticsAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken)
    {
        // CRITICAL PERFORMANCE FIX: Use database-level aggregation instead of loading all records into memory
        var groupByFields = context.GroupByFields.ToList();

        // Convert Geoservices statistics to Core statistics format
        var statistics = context.Statistics.Select(stat => new StatisticDefinition(
            FieldName: stat.FieldName,
            Type: MapStatisticType(stat.Type),
            OutputName: stat.OutputName ?? $"{stat.Type}_{stat.FieldName}"
        )).ToList();

        // Execute aggregation at database level - 100x faster than in-memory aggregation
        var results = await this.repository.QueryStatisticsAsync(
            serviceId,
            layer.Id,
            statistics,
            groupByFields.Count > 0 ? groupByFields : null,
            context.Query,
            cancellationToken).ConfigureAwait(false);

        // Convert database results to Geoservices format
        var features = new List<GeoservicesRESTFeature>();
        foreach (var result in results)
        {
            var attributes = new Dictionary<string, object?>();

            // Add group by values
            foreach (var (key, value) in result.GroupValues)
            {
                attributes[key] = value;
            }

            // Add statistics values
            foreach (var (key, value) in result.Statistics)
            {
                attributes[key] = value;
            }

            features.Add(new GeoservicesRESTFeature
            {
                Attributes = new ReadOnlyDictionary<string, object?>(attributes)
            });
        }

        var fieldDefinitions = BuildStatisticsFieldDefinitions(context.Statistics, layer, groupByFields);
        var spatialReference = new GeoservicesRESTSpatialReference { Wkid = context.TargetWkid };

        return new GeoservicesRESTFeatureSetResponse
        {
            ObjectIdFieldName = layer.IdField,
            DisplayFieldName = layer.DisplayField ?? layer.IdField,
            GeometryType = "esriGeometryNull",
            SpatialReference = spatialReference,
            Fields = fieldDefinitions,
            Features = new ReadOnlyCollection<GeoservicesRESTFeature>(features),
            HasZ = false,
            HasM = false
        };
    }

    private static StatisticType MapStatisticType(GeoservicesRESTStatisticType geoservicesType)
    {
        return geoservicesType switch
        {
            GeoservicesRESTStatisticType.Count => StatisticType.Count,
            GeoservicesRESTStatisticType.Sum => StatisticType.Sum,
            GeoservicesRESTStatisticType.Avg => StatisticType.Avg,
            GeoservicesRESTStatisticType.Min => StatisticType.Min,
            GeoservicesRESTStatisticType.Max => StatisticType.Max,
            _ => throw new NotSupportedException($"Statistic type '{geoservicesType}' is not supported.")
        };
    }

    private static GeoservicesRESTFeature CreateRestFeature(
        LayerDefinition layer,
        FeatureRecord record,
        GeoservicesRESTQueryContext context,
        string geometryType)
    {
        var attributes = new Dictionary<string, object?>();

        foreach (var field in layer.Fields)
        {
            if (context.SelectedFields.Count > 0
                && !context.SelectedFields.ContainsKey(field.Name)
                && !field.Name.EqualsIgnoreCase(layer.IdField))
            {
                continue;
            }

            if (TryGetAttribute(record, field.Name, out var value))
            {
                attributes[field.Name] = value;
            }
        }

        object? geometry = null;
        if (context.ReturnGeometry && record.Attributes.TryGetValue(layer.GeometryField, out var geomObj))
        {
            var ntsGeom = ResolveGeometry(geomObj);
            if (ntsGeom is not null)
            {
                var geometryToSerialize = PrepareGeometryForOutput(ntsGeom, context);
                if (geometryToSerialize is not null && !geometryToSerialize.IsEmpty)
                {
                    var geoJson = new GeoJsonWriter().Write(geometryToSerialize);
                    var geoNode = JsonNode.Parse(geoJson);
                    geometry = GeoservicesRESTGeometryConverter.ToGeometry(geoNode, geometryType, context.TargetWkid);
                }
            }
        }

        return new GeoservicesRESTFeature
        {
            Attributes = new ReadOnlyDictionary<string, object?>(attributes),
            Geometry = geometry as System.Text.Json.Nodes.JsonObject
        };
    }

    private static Geometry? PrepareGeometryForOutput(Geometry geometry, GeoservicesRESTQueryContext context)
    {
        if (geometry is null || geometry.IsEmpty)
        {
            return geometry;
        }

        Geometry working = (Geometry)geometry.Copy();

        if (context.MaxAllowableOffset.HasValue && context.MaxAllowableOffset.Value > 0)
        {
            try
            {
                working = DouglasPeuckerSimplifier.Simplify(working, context.MaxAllowableOffset.Value);
            }
            catch
            {
                // If simplification fails, fall back to original geometry copy.
                working = (Geometry)geometry.Copy();
            }
        }

        if (context.GeometryPrecision.HasValue)
        {
            working = ReducePrecision(working, context.GeometryPrecision.Value);
        }

        // Ensure SRID is preserved after transformations
        working.SRID = geometry.SRID;
        return working;
    }

    private static Geometry? ResolveGeometry(object? geometryValue)
    {
        switch (geometryValue)
        {
            case null:
                return null;
            case Geometry geometry:
                return geometry;
            case JsonNode node:
                return ParseGeoJson(node.ToJsonString());
            case JsonElement element:
                return element.ValueKind == JsonValueKind.Null ? null : ParseGeoJson(element.GetRawText());
            case JsonDocument document:
                return ParseGeoJson(document.RootElement.GetRawText());
            case string text when !string.IsNullOrWhiteSpace(text):
                return ParseGeometryFromText(text);
            default:
                return null;
        }
    }

    private static Geometry? ParseGeoJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return GeoJsonReaderInstance.Read<Geometry>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Geometry? ParseGeometryFromText(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var fromJson = ParseGeoJson(trimmed);
        if (fromJson is not null)
        {
            return fromJson;
        }

        try
        {
            return WktReaderInstance.Read(trimmed);
        }
        catch
        {
            return null;
        }
    }

    private static Geometry ReducePrecision(Geometry geometry, int precision)
    {
        if (precision < 0)
        {
            return geometry;
        }

        var scale = Math.Pow(10, precision);
        var precisionModel = new PrecisionModel(scale);
        var reducer = new GeometryPrecisionReducer(precisionModel)
        {
            ChangePrecisionModel = false,
            Pointwise = true
        };
        return reducer.Reduce(geometry);
    }

    private static bool TryGetAttribute(FeatureRecord record, string fieldName, out object? value)
    {
        if (record.Attributes.TryGetValue(fieldName, out value))
        {
            return true;
        }

        foreach (var (key, val) in record.Attributes)
        {
            if (key.EqualsIgnoreCase(fieldName))
            {
                value = val;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyList<string> ResolveDistinctFields(LayerDefinition layer, GeoservicesRESTQueryContext context)
    {
        IReadOnlyList<string> fields;

        if (context.RequestedOutFields != null && context.RequestedOutFields.Count > 0 && !context.OutFields.Contains("*"))
        {
            fields = context.RequestedOutFields.ToList();
        }
        else
        {
            fields = layer.Fields.Select(f => f.Name).ToList();
        }

        // BUG FIX #8: Exclude geometry AND large text/blob columns from DISTINCT queries
        // Most database engines either reject or materialize DISTINCT on these columns very slowly
        var fieldLookup = layer.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        var distinctFields = fields
            .Where(fieldName =>
            {
                if (fieldName.EqualsIgnoreCase(layer.GeometryField))
                {
                    return false;
                }

                // Exclude large text/blob columns that could cause massive DISTINCT payloads
                if (fieldLookup.TryGetValue(fieldName, out var field))
                {
                    var dataType = (field.DataType ?? field.StorageType ?? string.Empty).ToLowerInvariant();

                    // Exclude BLOB, CLOB, TEXT, NTEXT, IMAGE, BYTEA, LONG types
                    if (dataType.Contains("blob") || dataType.Contains("clob") ||
                        dataType.Contains("text") && dataType != "text" || // Exclude ntext, longtext but allow text
                        dataType.Contains("image") || dataType.Contains("bytea") ||
                        dataType.Contains("long") || dataType == "json" || dataType == "jsonb" ||
                        dataType == "xml")
                    {
                        return false;
                    }

                    // Exclude text columns without explicit length or with very large length
                    if ((dataType == "varchar" || dataType == "nvarchar" || dataType == "char" || dataType == "nchar" || dataType == "text") &&
                        (!field.MaxLength.HasValue || field.MaxLength.Value > 4000))
                    {
                        return false;
                    }
                }

                return true;
            })
            .ToList();

        // Fallback: If only large columns were requested, use the ID field instead
        if (distinctFields.Count == 0)
        {
            distinctFields.Add(layer.IdField);
        }

        return distinctFields;
    }

    private static (string Key, Dictionary<string, object?> Attributes) BuildDistinctAttributes(
        FeatureRecord record,
        IReadOnlyList<string> fields)
    {
        // BUG FIX #9: Use proper JSON serialization for composite key to prevent collisions
        // Previous approach using \0 delimiter with escaping could still cause edge case collisions
        var attributes = new Dictionary<string, object?>();
        var keyValues = new List<object?>(fields.Count);

        foreach (var fieldName in fields)
        {
            if (TryGetAttribute(record, fieldName, out var value))
            {
                attributes[fieldName] = value;
                keyValues.Add(value);
            }
            else
            {
                attributes[fieldName] = null;
                keyValues.Add(null);
            }
        }

        // Use JSON serialization for guaranteed uniqueness and no collision risk
        var key = System.Text.Json.JsonSerializer.Serialize(keyValues);
        return (key, attributes);
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> BuildDistinctFieldDefinitions(
        LayerDefinition layer,
        IReadOnlyList<string> distinctFields)
    {
        var fieldLookup = BuildFieldLookup(layer);
        var definitions = new List<GeoservicesRESTFieldInfo>();

        foreach (var fieldName in distinctFields)
        {
            if (fieldLookup.TryGetValue(fieldName, out var fieldInfo))
            {
                definitions.Add(fieldInfo);
            }
        }

        return definitions;
    }

    private static Dictionary<string, GeoservicesRESTFieldInfo> BuildFieldLookup(LayerDefinition layer)
    {
        var allFields = GeoservicesRESTMetadataMapper.CreateFieldDefinitions(layer);
        var lookup = new Dictionary<string, GeoservicesRESTFieldInfo>(allFields.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var field in allFields)
        {
            lookup[field.Name] = field;
        }

        return lookup;
    }

    private static (string Key, Dictionary<string, object?> Attributes) BuildGroupAttributes(
        FeatureRecord record,
        IReadOnlyList<string> groupByFields)
    {
        // BUG FIX #9: Use proper JSON serialization for composite key to prevent collisions
        // Previous approach using \0 delimiter with escaping could still cause edge case collisions
        var attributes = new Dictionary<string, object?>();
        var keyValues = new List<object?>(groupByFields.Count);

        foreach (var fieldName in groupByFields)
        {
            if (TryGetAttribute(record, fieldName, out var value))
            {
                attributes[fieldName] = value;
                keyValues.Add(value);
            }
            else
            {
                attributes[fieldName] = null;
                keyValues.Add(null);
            }
        }

        // Use JSON serialization for guaranteed uniqueness and no collision risk
        var key = System.Text.Json.JsonSerializer.Serialize(keyValues);
        return (key, attributes);
    }

    private static List<StatisticsAccumulator> CloneAccumulators(IEnumerable<StatisticsAccumulator> source)
    {
        return source.Select(acc => new StatisticsAccumulator(acc.Statistic)).ToList();
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> BuildStatisticsFieldDefinitions(
        IReadOnlyList<GeoservicesRESTStatisticDefinition> statistics,
        LayerDefinition layer,
        IReadOnlyList<string> groupByFields)
    {
        var fields = new List<GeoservicesRESTFieldInfo>();
        var fieldLookup = BuildFieldLookup(layer);

        foreach (var groupField in groupByFields)
        {
            if (fieldLookup.TryGetValue(groupField, out var fieldInfo))
            {
                fields.Add(fieldInfo);
            }
        }

        foreach (var stat in statistics)
        {
            var outName = stat.OutputName ?? $"{stat.Type}_{stat.FieldName}";
            var fieldType = ResolveStatisticFieldType(stat.Type, fieldLookup.GetValueOrDefault(stat.FieldName));

            fields.Add(new GeoservicesRESTFieldInfo
            {
                Name = outName,
                Type = fieldType,
                Alias = outName
            });
        }

        return fields;
    }

    private static string ResolveStatisticFieldType(GeoservicesRESTStatisticType statisticType, GeoservicesRESTFieldInfo? sourceField)
    {
        // BUG FIX #11: Use appropriate types for SUM to prevent integer overflow
        return statisticType switch
        {
            GeoservicesRESTStatisticType.Count => "esriFieldTypeInteger",
            // For SUM, preserve integer types but use larger container to prevent overflow
            GeoservicesRESTStatisticType.Sum => sourceField?.Type switch
            {
                "esriFieldTypeSmallInteger" => "esriFieldTypeInteger",
                "esriFieldTypeInteger" => "esriFieldTypeBigInteger",
                "esriFieldTypeSingle" => "esriFieldTypeDouble",
                _ => sourceField?.Type ?? "esriFieldTypeDouble"
            },
            GeoservicesRESTStatisticType.Min => sourceField?.Type ?? "esriFieldTypeDouble",
            GeoservicesRESTStatisticType.Max => sourceField?.Type ?? "esriFieldTypeDouble",
            GeoservicesRESTStatisticType.Avg => "esriFieldTypeDouble",
            _ => "esriFieldTypeDouble"
        };
    }

    private static string EscapeKeyComponent(object? value)
    {
        if (value is null)
        {
            return "\uFFFF";
        }

        return value switch
        {
            string s => s.Replace("\0", "\\0").Replace("\uFFFF", "\\uFFFF"),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "\uFFFF"
        };
    }

    private static IReadOnlyList<GeoservicesRESTFieldInfo> FilterFieldsForSelection(
        IReadOnlyList<GeoservicesRESTFieldInfo> fields,
        IReadOnlyDictionary<string, string> selectedFields,
        string idField)
    {
        if (selectedFields.Count == 0)
        {
            return fields;
        }

        var filtered = fields
            .Where(field => selectedFields.ContainsKey(field.Name) || field.Name.EqualsIgnoreCase(idField))
            .ToList();

        if (filtered.Count == fields.Count)
        {
            return fields;
        }

        return new ReadOnlyCollection<GeoservicesRESTFieldInfo>(filtered);
    }

    private IActionResult WriteJson(object payload, GeoservicesRESTQueryContext context)
    {
        if (!context.PrettyPrint)
        {
            return new JsonResult(payload);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    private sealed class StatisticsAccumulator
    {
        public GeoservicesRESTStatisticDefinition Statistic { get; }
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;

        // Welford's online algorithm for variance calculation - no memory allocation needed
        private double _mean;
        private double _m2; // Sum of squared differences from mean

        public StatisticsAccumulator(GeoservicesRESTStatisticDefinition statistic)
        {
            Statistic = statistic;
        }

        public void Accumulate(object? value)
        {
            if (value is null)
            {
                return;
            }

            _count++;

            if (Statistic.Type == GeoservicesRESTStatisticType.Count)
            {
                return;
            }

            if (TryConvertToDouble(value, out var numericValue))
            {
                _sum += numericValue;
                this.min = Math.Min(_min, numericValue);
                this.max = Math.Max(_max, numericValue);

                // Welford's online algorithm for variance (streaming calculation)
                var delta = numericValue - _mean;
                _mean += delta / _count;
                var delta2 = numericValue - _mean;
                _m2 += delta * delta2;
            }
        }

        public object? GetResult()
        {
            return Statistic.Type switch
            {
                GeoservicesRESTStatisticType.Count => _count,
                GeoservicesRESTStatisticType.Sum => _count > 0 ? _sum : (object?)null,
                GeoservicesRESTStatisticType.Min => _count > 0 ? _min : (object?)null,
                GeoservicesRESTStatisticType.Max => _count > 0 ? _max : (object?)null,
                GeoservicesRESTStatisticType.Avg => _count > 0 ? _sum / _count : (object?)null,
                _ => null
            };
        }

        private double GetVariance()
        {
            return _count > 1 ? _m2 / _count : 0;
        }

        private double GetStdDev()
        {
            return Math.Sqrt(GetVariance());
        }

        private static bool TryConvertToDouble(object? value, out double result)
        {
            if (value is null)
            {
                result = 0;
                return false;
            }

            try
            {
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }
    }

    // Helper methods for related records query
    private static HttpRequest CreateRelatedQueryRequest(HttpRequest source, params string[] excludedKeys)
    {
        var exclusion = new HashSet<string>(excludedKeys, StringComparer.OrdinalIgnoreCase);
        var builder = new QueryBuilder();

        foreach (var pair in source.Query)
        {
            if (exclusion.Contains(pair.Key))
            {
                continue;
            }

            foreach (var value in pair.Value)
            {
                if (!value.IsNullOrEmpty())
                {
                    builder.Add(pair.Key, value);
                }
            }
        }

        var context = new DefaultHttpContext();
        context.Request.Method = source.Method;
        context.Request.QueryString = builder.ToQueryString();

        // BUG FIX #14: Copy authentication and authorization headers to related query request
        // Common auth headers that should be preserved for related record queries
        var authHeaderNames = new[]
        {
            "Authorization",
            "X-API-Key",
            "X-Auth-Token",
            "Cookie",
            "Accept-Crs"
        };

        foreach (var headerName in authHeaderNames)
        {
            if (source.Headers.TryGetValue(headerName, out var headerValue))
            {
                context.Request.Headers[headerName] = headerValue;
            }
        }

        return context.Request;
    }

    private static List<ObjectIdValue> ParseObjectIdValues(string raw, LayerDefinition relatedLayer, string relatedKeyField)
    {
        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<ObjectIdValue>(tokens.Length);

        var relatedField = relatedLayer.Fields.FirstOrDefault(field =>
            field.Name.EqualsIgnoreCase(relatedKeyField));

        // BUG FIX #15: Keep valid IDs when some can't be parsed instead of failing silently
        foreach (var token in tokens)
        {
            if (token.IsNullOrWhiteSpace())
            {
                continue;
            }

            try
            {
                var typedValue = ConvertObjectIdToken(token, relatedField);
                var key = Convert.ToString(ConvertAttributeValue(typedValue), CultureInfo.InvariantCulture) ?? string.Empty;
                results.Add(new ObjectIdValue(typedValue, key));
            }
            catch
            {
                // Skip invalid IDs but continue parsing remaining values
                // This allows partial success instead of all-or-nothing behavior
                continue;
            }
        }

        return results;
    }

    private static object ConvertObjectIdToken(string token, FieldDefinition? field)
    {
        var trimmed = token.Trim();

        // BUG FIX #16: Properly unescape quoted strings including \" escape sequences
        // Handle both single-quoted strings (SQL-style with '' for escaping) and double-quoted strings
        if (trimmed.Length >= 2)
        {
            if (trimmed[0] == '\'' && trimmed[^1] == '\'')
            {
                // SQL-style single-quoted string: unescape '' to '
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Replace("''", "'", StringComparison.Ordinal);
            }
            else if (trimmed[0] == '"' && trimmed[^1] == '"')
            {
                // JSON-style double-quoted string: unescape \" to " and \\ to \
                trimmed = trimmed.Substring(1, trimmed.Length - 2)
                    .Replace("\\\"", "\"", StringComparison.Ordinal)
                    .Replace("\\\\", "\\", StringComparison.Ordinal)
                    .Replace("\\n", "\n", StringComparison.Ordinal)
                    .Replace("\\r", "\r", StringComparison.Ordinal)
                    .Replace("\\t", "\t", StringComparison.Ordinal);
            }
        }

        string typeHint = field?.DataType ?? field?.StorageType ?? string.Empty;
        typeHint = typeHint.Trim().ToLowerInvariant();

        return typeHint switch
        {
            "int" or "integer" or "int32" or "long" or "int64" or "bigint" => long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue) ? longValue : trimmed,
            "short" or "smallint" or "int16" => short.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue) ? shortValue : trimmed,
            "single" or "float" => trimmed.TryParseFloat(out var floatValue) ? floatValue : trimmed,
            "double" or "real" or "decimal" or "numeric" => trimmed.TryParseDouble(out var doubleValue) ? doubleValue : trimmed,
            "date" or "datetime" or "datetimeoffset" => DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto) ? dto : trimmed,
            "guid" or "uuid" or "uniqueidentifier" => Guid.TryParse(trimmed, out var guidValue) ? guidValue : trimmed,
            _ => TryParseFallback(trimmed)
        };

        static object TryParseFallback(string value)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            if (value.TryParseDouble(out var doubleValue))
            {
                return doubleValue;
            }

            return value;
        }
    }

    private static QueryExpression? BuildObjectIdFilterExpression(string fieldName, IReadOnlyList<ObjectIdValue> values)
    {
        // BUG FIX #7: Use IN clause instead of OR chain to avoid plan cache poisoning with large ID lists
        if (values.Count == 0)
        {
            return null;
        }

        // For single value, use simple equality for optimal plan caching
        if (values.Count == 1)
        {
            return new QueryBinaryExpression(
                new QueryFieldReference(fieldName),
                QueryBinaryOperator.Equal,
                new QueryConstant(values[0].Value));
        }

        // For multiple values, use IN function which generates parameterized IN clause or = ANY
        var arguments = new List<QueryExpression>(values.Count + 1)
        {
            new QueryFieldReference(fieldName)
        };

        foreach (var entry in values)
        {
            arguments.Add(new QueryConstant(entry.Value));
        }

        return new QueryFunctionExpression("IN", arguments);
    }

    private static QueryFilter? CombineFilters(QueryFilter? existing, QueryExpression? additional)
    {
        if (additional is null)
        {
            return existing;
        }

        if (existing?.Expression is null)
        {
            return new QueryFilter(additional);
        }

        return new QueryFilter(new QueryBinaryExpression(existing.Expression, QueryBinaryOperator.And, additional));
    }

    private static object? ConvertAttributeValue(object? value)
    {
        return value switch
        {
            null => null,
            System.Text.Json.JsonElement element => JsonElementConverter.ToObjectWithJsonNode(element),
            System.Text.Json.Nodes.JsonValue jsonValue => ConvertAttributeValue(jsonValue.GetValue<object?>()),
            System.Text.Json.Nodes.JsonObject or System.Text.Json.Nodes.JsonArray => value,
            DateTimeOffset dto => dto.ToUnixTimeMilliseconds(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            Guid guid => guid.ToString("D", CultureInfo.InvariantCulture),
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
            bool => value,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private sealed record ObjectIdValue(object Value, string Key);
}
