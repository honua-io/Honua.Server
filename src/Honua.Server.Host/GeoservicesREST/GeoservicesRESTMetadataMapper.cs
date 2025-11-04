// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.GeoservicesREST;

internal static class GeoservicesRESTMetadataMapper
{
    public static GeoservicesRESTFeatureServiceSummary CreateFeatureServiceSummary(CatalogServiceView serviceView, double currentVersion)
    {
        Guard.NotNull(serviceView);

        var layers = CreateLayerCollection(serviceView);
        var serviceCapabilities = ResolveServiceCapabilities(serviceView);

        return new GeoservicesRESTFeatureServiceSummary
        {
            CurrentVersion = currentVersion,
            ServiceDescription = serviceView.Summary ?? string.Empty,
            HasVersionedData = false,
            SupportsDisconnectedEditing = false,
            SupportsRelationshipsResource = serviceCapabilities.SupportsRelationshipsResource,
            SupportsTrueCurves = false,
            SupportsDatumTransformation = false,
            MaxRecordCount = ResolveMaxRecordCount(serviceView),
            SupportedQueryFormats = "JSON,pJSON,geoJSON,TopoJSON,KML,KMZ,shapefile",
            Capabilities = serviceCapabilities.Capabilities,
            Description = serviceView.Summary ?? string.Empty,
            SupportsDynamicLayers = false,
            AllowGeometryUpdates = serviceCapabilities.AllowGeometryUpdates,
            HasStaticData = !serviceCapabilities.HasEditing,
            SupportsStatistics = true,
            Layers = layers
        };
    }

    public static GeoservicesRESTFeatureServiceSummary CreateMapServiceSummary(CatalogServiceView serviceView, double currentVersion)
    {
        Guard.NotNull(serviceView);

        var layers = CreateLayerCollection(serviceView);
        var serviceCapabilities = ResolveServiceCapabilities(serviceView);

        return new GeoservicesRESTFeatureServiceSummary
        {
            CurrentVersion = currentVersion,
            ServiceDescription = serviceView.Summary ?? string.Empty,
            HasVersionedData = false,
            SupportsDisconnectedEditing = false,
            SupportsRelationshipsResource = serviceCapabilities.SupportsRelationshipsResource,
            SupportsTrueCurves = false,
            SupportsDatumTransformation = false,
            MaxRecordCount = ResolveMaxRecordCount(serviceView),
            SupportedQueryFormats = "JSON,geoJSON",
            SupportedImageFormatTypes = "PNG32,PNG24,PNG,JPG",
            SingleFusedMapCache = false,
            Capabilities = "Map",
            Description = serviceView.Summary ?? string.Empty,
            SupportsDynamicLayers = false,
            AllowGeometryUpdates = false,
            HasStaticData = false,
            SupportsStatistics = true,
            Layers = layers
        };
    }

    public static GeoservicesRESTImageServiceSummary CreateImageServiceSummary(
        CatalogServiceView serviceView,
        IReadOnlyList<RasterDatasetDefinition> datasets,
        double currentVersion)
    {
        Guard.NotNull(serviceView);
        Guard.NotNull(datasets);

        var description = serviceView.Summary ?? string.Empty;
        var fallbackExtent = serviceView.Layers.FirstOrDefault()?.SpatialExtent;
        var extent = datasets.Count > 0
            ? CreateExtentFromLayerExtent(datasets[0].Extent) ?? CreateExtent(fallbackExtent)
            : CreateExtent(fallbackExtent);

        var rasterIds = datasets.Select(dataset => dataset.Id).ToArray();
        var datasetSummaries = datasets.Select(dataset => new GeoservicesRESTImageDatasetInfo
        {
            Id = dataset.Id,
            Title = dataset.Title,
            DefaultStyleId = dataset.Styles.DefaultStyleId,
            StyleIds = dataset.Styles.StyleIds ?? Array.Empty<string>()
        }).ToArray();

        return new GeoservicesRESTImageServiceSummary
        {
            CurrentVersion = currentVersion,
            ServiceDescription = description,
            Name = serviceView.Service.Title ?? serviceView.Service.Id,
            Capabilities = "Image",
            SupportedImageFormatTypes = "PNG32,PNG24,PNG,JPG",
            SingleFusedMapCache = false,
            MaxImageHeight = 4096,
            MaxImageWidth = 4096,
            DefaultCompressionQuality = 75,
            Extent = extent,
            Rasters = rasterIds,
            Datasets = datasetSummaries
        };
    }

    public static GeoservicesRESTLayerInfo CreateLayerInfo(ServiceDefinition service, CatalogLayerView layerView, int layerIndex)
    {
        Guard.NotNull(service);

        Guard.NotNull(layerView);

        var layer = layerView.Layer;
        return new GeoservicesRESTLayerInfo
        {
            Id = layerIndex,
            Name = layer.Title,
            Type = "Feature Layer",
            Description = layerView.Summary ?? layer.Description,
            GeometryType = MapGeometryType(layer.GeometryType),
            DefaultVisibility = true,
            ParentLayerId = -1,
            SubLayerIds = null,
            MinScale = layer.MinScale ?? 0d,
            MaxScale = layer.MaxScale ?? 0d,
            Extent = CreateExtent(layerView.SpatialExtent ?? layer.Catalog?.SpatialExtent),
            TimeInfo = CreateTimeInfo(service, layerView)
        };
    }

    public static GeoservicesRESTLayerDetailResponse CreateLayerDetailResponse(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        int layerIndex,
        double currentVersion,
        StyleDefinition? style = null)
    {
        Guard.NotNull(serviceView);
        Guard.NotNull(layerView);

        var layer = layerView.Layer;
        var fields = CreateFieldDefinitions(layer);
        var extent = CreateExtent(layerView.SpatialExtent ?? layer.Catalog?.SpatialExtent);
        var spatialReference = layer.Storage?.Srid is int storageSrid
            ? new GeoservicesRESTSpatialReference { Wkid = storageSrid }
            : extent?.SpatialReference;
        var layerCapabilities = ResolveLayerCapabilities(layer);
        var globalIdField = ResolveGlobalIdField(layer);
        var hasAttachments = layer.Attachments.Enabled;
        var relationships = BuildLayerRelationships(serviceView, layerView);

        return new GeoservicesRESTLayerDetailResponse
        {
            CurrentVersion = currentVersion,
            Id = layerIndex.ToString(CultureInfo.InvariantCulture),
            Name = layer.Title,
            ObjectIdField = layer.IdField,
            GlobalIdField = globalIdField,
            GeometryType = MapGeometryType(layer.GeometryType),
            DisplayField = layer.DisplayField ?? layer.IdField,
            Description = layer.Description ?? layerView.Summary,
            HasM = false,
            HasZ = false,
            HasAttachments = hasAttachments,
            SupportsStatistics = true,
            SupportsAdvancedQueries = true,
            SupportsTrueCurves = false,
            SupportsCoordinatesQuantization = false,
            SupportsReturningQueryExtent = true,
            SupportsPagination = true,
            SupportsOrderBy = true,
            SupportsDistinct = true,
            AllowGeometryUpdates = layerCapabilities.AllowGeometryUpdates,
            SupportsRollbackOnFailureParameter = layerCapabilities.SupportsRollbackOnFailureParameter,
            MaxRecordCount = ResolveLayerMaxRecordCount(serviceView, layerView),
            SourceSpatialReference = spatialReference,
            Extent = extent,
            Fields = fields,
            AdvancedQueryCapabilities = new GeoservicesRESTAdvancedQueryCapabilities
            {
                SupportsPagination = true,
                SupportsTrueCurve = false,
                SupportsQueryWithDistance = false,
                SupportsReturningQueryExtent = true,
                SupportsStatistics = true,
                SupportsOrderBy = true,
                SupportsDistinct = true
            },
            Capabilities = layerCapabilities.Capabilities,
            DrawingInfo = style is null ? null : StyleFormatConverter.CreateEsriDrawingInfo(style, layer.GeometryType),
            TimeInfo = CreateTimeInfo(serviceView.Service, layerView),
            MinScale = layer.MinScale ?? 0d,
            MaxScale = layer.MaxScale ?? 0d,
            Relationships = relationships
        };
    }

    private static IReadOnlyList<GeoservicesRESTLayerRelationshipInfo> BuildLayerRelationships(
        CatalogServiceView serviceView,
        CatalogLayerView layerView)
    {
        if (layerView.Relationships.Count == 0)
        {
            return Array.Empty<GeoservicesRESTLayerRelationshipInfo>();
        }

        var relationships = new List<GeoservicesRESTLayerRelationshipInfo>();
        foreach (var relationship in layerView.Relationships)
        {
            if (relationship.Semantics != LayerRelationshipSemantics.PrimaryKeyForeignKey)
            {
                continue;
            }

            var relatedIndex = ResolveRelatedLayerIndex(serviceView, relationship.RelatedLayerId);
            relationships.Add(new GeoservicesRESTLayerRelationshipInfo
            {
                Id = relationship.Id,
                Name = string.IsNullOrWhiteSpace(relationship.RelatedLayerId)
                    ? $"relationship_{relationship.Id}"
                    : relationship.RelatedLayerId!,
                Cardinality = relationship.Cardinality ?? "esriRelCardinalityOneToMany",
                Role = relationship.Role ?? "esriRelRoleOrigin",
                RelatedTableId = relatedIndex
            });
        }

        return relationships.Count == 0
            ? Array.Empty<GeoservicesRESTLayerRelationshipInfo>()
            : new ReadOnlyCollection<GeoservicesRESTLayerRelationshipInfo>(relationships);
    }

    private static GeoservicesRESTTimeInfo? CreateTimeInfo(ServiceDefinition service, CatalogLayerView layerView)
    {
        var temporalField = layerView.Layer.Storage?.TemporalColumn;
        if (string.IsNullOrWhiteSpace(temporalField))
        {
            return null;
        }

        var (start, end) = ResolveTemporalBounds(service, layerView);
        var timeExtent = new long?[] { start, end };

        return new GeoservicesRESTTimeInfo
        {
            StartTimeField = temporalField,
            EndTimeField = null,
            TrackIdField = string.Empty,
            TimeExtent = timeExtent,
            TimeReference = new GeoservicesRESTTimeReference
            {
                TimeZone = "UTC",
                RespectsDaylightSaving = false
            },
            TimeInterval = null,
            TimeIntervalUnits = null
        };
    }

    private static (long? Start, long? End) ResolveTemporalBounds(ServiceDefinition service, CatalogLayerView layerView)
    {
        if (layerView.TemporalExtent is { } temporalExtent)
        {
            return (temporalExtent.Start?.ToUnixTimeMilliseconds(), temporalExtent.End?.ToUnixTimeMilliseconds());
        }

        if (layerView.Layer.Catalog.TemporalExtent is { } catalogExtent)
        {
            return (catalogExtent.Start?.ToUnixTimeMilliseconds(), catalogExtent.End?.ToUnixTimeMilliseconds());
        }

        if (layerView.Layer.Extent?.Temporal is { Count: > 0 } intervals)
        {
            var interval = intervals[0];
            return (interval.Start?.ToUnixTimeMilliseconds(), interval.End?.ToUnixTimeMilliseconds());
        }

        if (service.Catalog.TemporalExtent is { } serviceExtent)
        {
            return (serviceExtent.Start?.ToUnixTimeMilliseconds(), serviceExtent.End?.ToUnixTimeMilliseconds());
        }

        return (null, null);
    }

    public static GeoservicesRESTExtent? CreateExtent(CatalogSpatialExtentDefinition? extent)
    {
        if (extent is null || extent.Bbox.IsNullOrEmpty())
        {
            return null;
        }

        var bbox = extent.Bbox[0];
        if (bbox.Length < 4)
        {
            return null;
        }

        return new GeoservicesRESTExtent
        {
            Xmin = bbox[0],
            Ymin = bbox[1],
            Xmax = bbox[2],
            Ymax = bbox[3],
            SpatialReference = CreateSpatialReference(extent.Crs)
        };
    }

    public static GeoservicesRESTExtent? CreateExtentFromLayerExtent(LayerExtentDefinition? extent)
    {
        if (extent is null || extent.Bbox.IsNullOrEmpty())
        {
            return null;
        }

        var spatial = new CatalogSpatialExtentDefinition
        {
            Bbox = extent.Bbox,
            Crs = extent.Crs
        };

        return CreateExtent(spatial);
    }

    public static GeoservicesRESTSpatialReference CreateSpatialReference(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            return new GeoservicesRESTSpatialReference { Wkid = 4326 };
        }

        if (CultureInvariantHelpers.StartsWithIgnoreCase(crs, "EPSG:"))
        {
            var code = crs.Substring(5);
            if (int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wkid))
            {
                return new GeoservicesRESTSpatialReference { Wkid = wkid };
            }
        }

        if (int.TryParse(crs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return new GeoservicesRESTSpatialReference { Wkid = numeric };
        }

        return new GeoservicesRESTSpatialReference { Wkid = 4326 };
    }

    public static string MapGeometryType(string geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return "esriGeometryPolygon";
        }

        return geometryType.ToLowerInvariant() switch
        {
            "point" => "esriGeometryPoint",
            "multipoint" => "esriGeometryMultipoint",
            "polyline" or "line" or "linestring" or "multilinestring" => "esriGeometryPolyline",
            "polygon" or "multipolygon" => "esriGeometryPolygon",
            "geometrycollection" => "esriGeometryPolygon",
            _ => "esriGeometryPolygon"
        };
    }

    public static IReadOnlyList<GeoservicesRESTFieldInfo> CreateFieldDefinitions(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var resolvedFields = FieldMetadataResolver.ResolveFields(layer, includeGeometry: false, includeIdField: true);
        var fields = new List<GeoservicesRESTFieldInfo>(resolvedFields.Count);

        foreach (var field in resolvedFields)
        {
            var domain = MapFieldDomain(field.Domain);

            fields.Add(new GeoservicesRESTFieldInfo
            {
                Name = field.Name,
                Alias = string.IsNullOrWhiteSpace(field.Alias) ? field.Name : field.Alias!,
                Type = FieldMetadataResolver.MapToGeoServicesType(field, layer.IdField),
                Nullable = field.Nullable,
                Editable = field.Editable,
                Domain = domain,
                DefaultValue = null,
                Length = field.MaxLength,
                Precision = field.Precision,
                Scale = field.Scale
            });
        }

        return new ReadOnlyCollection<GeoservicesRESTFieldInfo>(fields);
    }

    private static GeoservicesRESTDomain? MapFieldDomain(FieldDomainDefinition? domain)
    {
        if (domain is null)
        {
            return null;
        }

        if (domain.Type.EqualsIgnoreCase("codedValue") && domain.CodedValues is not null)
        {
            var codedValues = domain.CodedValues
                .Select(cv => new GeoservicesRESTCodedValue
                {
                    Name = cv.Name,
                    Code = cv.Code
                })
                .ToArray();

            return new GeoservicesRESTDomain
            {
                Type = "codedValue",
                Name = domain.Name,
                CodedValues = new ReadOnlyCollection<GeoservicesRESTCodedValue>(codedValues),
                Range = null
            };
        }

        if (domain.Type.EqualsIgnoreCase("range") && domain.Range is not null)
        {
            var minValue = Convert.ToDouble(domain.Range.MinValue);
            var maxValue = Convert.ToDouble(domain.Range.MaxValue);

            return new GeoservicesRESTDomain
            {
                Type = "range",
                Name = domain.Name,
                CodedValues = null,
                Range = new[] { minValue, maxValue }
            };
        }

        return null;
    }

    public static int ResolveMaxRecordCount(CatalogServiceView service)
    {
        Guard.NotNull(service);

        var serviceLimit = service.Service.Ogc.ItemLimit;
        var layerLimit = service.Layers
            .Select(l => l.Layer.Query.MaxRecordCount)
            .Where(limit => limit.HasValue)
            .Select(limit => limit!.Value)
            .DefaultIfEmpty()
            .Max();

        if (layerLimit > 0 && serviceLimit is null)
        {
            return layerLimit;
        }

        if (serviceLimit is null)
        {
            return layerLimit > 0 ? layerLimit : DefaultMaxRecordCount;
        }

        return layerLimit > 0 ? Math.Min(serviceLimit.Value, layerLimit) : serviceLimit.Value;
    }

    public static int ResolveLayerMaxRecordCount(CatalogServiceView service, CatalogLayerView layer)
    {
        Guard.NotNull(service);

        Guard.NotNull(layer);

        var serviceLimit = service.Service.Ogc.ItemLimit;
        var layerLimit = layer.Layer.Query.MaxRecordCount;

        if (serviceLimit.HasValue && layerLimit.HasValue)
        {
            return Math.Min(serviceLimit.Value, layerLimit.Value);
        }

        if (layerLimit.HasValue)
        {
            return layerLimit.Value;
        }

        return serviceLimit ?? DefaultMaxRecordCount;
    }


    private static ReadOnlyCollection<GeoservicesRESTLayerInfo> CreateLayerCollection(CatalogServiceView serviceView)
    {
        var layers = new List<GeoservicesRESTLayerInfo>(serviceView.Layers.Count);
        for (var index = 0; index < serviceView.Layers.Count; index++)
        {
            layers.Add(CreateLayerInfo(serviceView.Service, serviceView.Layers[index], index));
        }

        return new ReadOnlyCollection<GeoservicesRESTLayerInfo>(layers);
    }

    private static ServiceCapabilityInfo ResolveServiceCapabilities(CatalogServiceView serviceView)
    {
        var capabilities = new List<string> { "Query" };
        static void AddCapability(List<string> list, string value)
        {
            if (!list.Any(item => item.EqualsIgnoreCase(value)))
            {
                list.Add(value);
            }
        }

        var allowAdd = false;
        var allowUpdate = false;
        var allowDelete = false;

        foreach (var layerView in serviceView.Layers)
        {
            var editing = layerView.Layer.Editing?.Capabilities ?? LayerEditCapabilitiesDefinition.Disabled;

            if (editing.AllowAdd)
            {
                allowAdd = true;
                AddCapability(capabilities, "Create");
            }

            if (editing.AllowDelete)
            {
                allowDelete = true;
                AddCapability(capabilities, "Delete");
            }

            if (editing.AllowUpdate)
            {
                allowUpdate = true;
                AddCapability(capabilities, "Update");
            }

            if (layerView.Layer.Attachments.Enabled)
            {
                AddCapability(capabilities, "Uploads");
            }
        }

        if (allowAdd || allowUpdate || allowDelete)
        {
            AddCapability(capabilities, "Editing");
        }

        var supportsRelationships = serviceView.Layers.Any(layerView =>
            layerView.Relationships.Any(rel => rel.Semantics == LayerRelationshipSemantics.PrimaryKeyForeignKey));

        var capabilityString = string.Join(',', capabilities);
        return new ServiceCapabilityInfo(
            capabilityString,
            allowUpdate,
            allowAdd || allowUpdate || allowDelete,
            supportsRelationships);
    }

    private static LayerCapabilityInfo ResolveLayerCapabilities(LayerDefinition layer)
    {
        var capabilities = new List<string> { "Query" };
        static void AddCapability(List<string> list, string value)
        {
            if (!list.Any(item => item.EqualsIgnoreCase(value)))
            {
                list.Add(value);
            }
        }

        var editing = layer.Editing?.Capabilities ?? LayerEditCapabilitiesDefinition.Disabled;
        var allowAdd = editing.AllowAdd;
        var allowUpdate = editing.AllowUpdate;
        var allowDelete = editing.AllowDelete;

        if (allowAdd)
        {
            AddCapability(capabilities, "Create");
        }

        if (allowDelete)
        {
            AddCapability(capabilities, "Delete");
        }

        if (allowUpdate)
        {
            AddCapability(capabilities, "Update");
        }

        if (layer.Attachments.Enabled)
        {
            AddCapability(capabilities, "Uploads");
        }

        if (allowAdd || allowUpdate || allowDelete)
        {
            AddCapability(capabilities, "Editing");
        }

        return new LayerCapabilityInfo(
            string.Join(',', capabilities),
            allowUpdate,
            allowAdd || allowUpdate || allowDelete);
    }

    private static int ResolveRelatedLayerIndex(CatalogServiceView serviceView, string relatedLayerId)
    {
        for (var i = 0; i < serviceView.Layers.Count; i++)
        {
            if (serviceView.Layers[i].Layer.Id.EqualsIgnoreCase(relatedLayerId))
            {
                return i;
            }
        }

        return -1;
    }

    private static string? ResolveGlobalIdField(LayerDefinition layer)
    {
        foreach (var field in layer.Fields)
        {
            if (field.Name.EqualsIgnoreCase("globalId"))
            {
                return field.Name;
            }

            var fieldType = FieldMetadataResolver.MapToGeoServicesType(field, layer.IdField);
            if (fieldType.EqualsIgnoreCase("esriFieldTypeGlobalID"))
            {
                return field.Name;
            }
        }

        return null;
    }

    private const int DefaultMaxRecordCount = 1000;

    private readonly record struct ServiceCapabilityInfo(string Capabilities, bool AllowGeometryUpdates, bool HasEditing, bool SupportsRelationshipsResource);

    private readonly record struct LayerCapabilityInfo(string Capabilities, bool AllowGeometryUpdates, bool SupportsRollbackOnFailureParameter);
}
