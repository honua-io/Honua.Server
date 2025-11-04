// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestMetadataTranslator
{
    private static readonly Regex IdentifierPattern = new("[^a-zA-Z0-9_]+", RegexOptions.Compiled);
    private readonly GeoservicesRestMetadataTranslatorOptions _options;

    public GeoservicesRestMetadataTranslator(GeoservicesRestMetadataTranslatorOptions? options = null)
    {
        _options = options ?? new GeoservicesRestMetadataTranslatorOptions();
    }

    public GeoservicesRestMigrationPlan Translate(
        Uri sourceServiceUri,
        string targetServiceId,
        string targetFolderId,
        string targetDataSourceId,
        GeoservicesRestFeatureServiceInfo serviceInfo,
        IReadOnlyList<GeoservicesRestLayerInfo> layers,
        IReadOnlyCollection<int>? selectedLayerIds = null)
    {
        Guard.NotNull(sourceServiceUri);
        Guard.NotNullOrWhiteSpace(targetServiceId);
        Guard.NotNullOrWhiteSpace(targetFolderId);
        Guard.NotNullOrWhiteSpace(targetDataSourceId);
        Guard.NotNull(serviceInfo);
        Guard.NotNull(layers);

        if (layers.Count == 0)
        {
            throw new InvalidOperationException("The source service does not expose any layers to migrate.");
        }

        var serviceId = SanitizeIdentifier(targetServiceId, "service");
        var additionalCrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultCrs = ResolveCrs(serviceInfo.SpatialReference, additionalCrs);

        var serviceKeywords = ResolveKeywords(serviceInfo)?.ToList();

        var serviceDocument = new ServiceDocument
        {
            Id = serviceId,
            Title = ResolveServiceTitle(serviceInfo, serviceId),
            FolderId = targetFolderId,
            ServiceType = "feature",
            DataSourceId = targetDataSourceId,
            Enabled = true,
            Description = ResolveServiceDescription(serviceInfo),
            Keywords = serviceKeywords,
            Catalog = new CatalogEntryDocument
            {
                Summary = serviceInfo.Description ?? serviceInfo.ServiceDescription,
                Keywords = serviceKeywords
            },
            Ogc = new OgcServiceDocument
            {
                CollectionsEnabled = true,
                ItemLimit = serviceInfo.MaxRecordCount,
                DefaultCrs = defaultCrs,
                AdditionalCrs = additionalCrs.Count > 0 ? additionalCrs.ToList() : null
            }
        };

        var plans = new List<GeoservicesRestLayerMigrationPlan>();
        var usedLayerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layerInfo in layers)
        {
            if (selectedLayerIds is not null && !selectedLayerIds.Contains(layerInfo.Id))
            {
                continue;
            }

            var objectIdField = layerInfo.ObjectIdField.HasValue()
                ? layerInfo.ObjectIdField!
                : ResolveObjectIdField(layerInfo);

            if (objectIdField.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException($"Layer '{layerInfo.Name}' does not expose an objectIdField.");
            }

            var layerId = CreateLayerIdentifier(serviceId, layerInfo, usedLayerIds);
            var tableName = CreateTableName(layerId, layerInfo, usedTableNames);
            var geometryColumn = _options.GeometryColumnName.IsNullOrWhiteSpace() ? "shape" : _options.GeometryColumnName!;

            var layerDocument = BuildLayerDocument(
                serviceId,
                layerId,
                layerInfo,
                objectIdField,
                tableName,
                geometryColumn,
                defaultCrs,
                targetDataSourceId,
                additionalCrs);

            var schema = BuildSchemaDefinition(
                serviceId,
                layerId,
                objectIdField,
                layerDocument,
                tableName,
                geometryColumn);

            var layerUri = new Uri(sourceServiceUri, $"{layerInfo.Id}");
            plans.Add(new GeoservicesRestLayerMigrationPlan
            {
                LayerDocument = layerDocument,
                LayerId = layerId,
                LayerTitle = layerInfo.Name,
                Schema = schema,
                SourceLayer = layerInfo,
                SourceLayerUri = layerUri
            });
        }

        if (plans.Count == 0)
        {
            throw new InvalidOperationException("No layers matched the requested set for migration.");
        }

        return new GeoservicesRestMigrationPlan
        {
            ServiceDocument = serviceDocument,
            LayerDocuments = plans.Select(plan => plan.LayerDocument).ToList(),
            ServiceId = serviceId,
            ServiceTitle = serviceDocument.Title,
            DataSourceId = serviceDocument.DataSourceId ?? string.Empty,
            Layers = plans,
            SourceServiceUri = sourceServiceUri,
            SourceServiceName = serviceInfo.Name ?? serviceInfo.ServiceDescription ?? serviceId
        };
    }

    private LayerDocument BuildLayerDocument(
        string serviceId,
        string layerId,
        GeoservicesRestLayerInfo layerInfo,
        string objectIdField,
        string tableName,
        string geometryColumn,
        string? defaultCrs,
        string dataSourceId,
        HashSet<string> serviceAdditionalCrs)
    {
        var geometryType = MapGeometryType(layerInfo.GeometryType);
        if (geometryType is null)
        {
            throw new InvalidOperationException($"Layer '{layerInfo.Name}' reports unsupported geometry type '{layerInfo.GeometryType}'.");
        }

        var srid = ResolveSrid(layerInfo, defaultCrs, serviceAdditionalCrs);
        var crsList = new List<string>();
        if (defaultCrs.HasValue())
        {
            crsList.Add(defaultCrs);
        }
        if (srid.Crs is not null && !crsList.Contains(srid.Crs, StringComparer.OrdinalIgnoreCase))
        {
            crsList.Add(srid.Crs);
        }

        var extent = BuildExtent(layerInfo.Extent, srid.Crs);
        var fields = BuildFields(layerInfo, objectIdField, geometryColumn);
        var query = new LayerQueryDocument
        {
            MaxRecordCount = layerInfo.MaxRecordCount ?? layerInfo.StandardMaxRecordCount ?? layerInfo.TileMaxRecordCount ?? srid.DefaultMaxRecordCount,
            SupportedParameters = new List<string> { "bbox", "limit", "offset", "datetime" }
        };

        var storage = new LayerStorageDocument
        {
            Table = tableName,
            GeometryColumn = geometryColumn,
            PrimaryKey = objectIdField,
            TemporalColumn = layerInfo.TimeField,
            Srid = srid.Srid,
            Crs = srid.Crs
        };

        var catalogEntry = new CatalogEntryDocument
        {
            Summary = layerInfo.Description,
            Keywords = layerInfo.Description is null ? null : SplitKeywords(layerInfo.Description).ToList()
        };

        return new LayerDocument
        {
            Id = layerId,
            ServiceId = serviceId,
            Title = layerInfo.Name,
            Description = layerInfo.Description,
            GeometryType = geometryType,
            IdField = objectIdField,
            DisplayField = ResolveDisplayField(layerInfo, objectIdField),
            GeometryField = geometryColumn,
            Crs = crsList,
            Catalog = catalogEntry,
            Extent = extent,
            Query = query,
            Storage = storage,
            Fields = fields,
            ItemType = "feature",
            Editing = new LayerEditingDocument
            {
                Capabilities = LayerEditCapabilitiesDocumentDisabled,
                Constraints = LayerEditConstraintsDocumentEmpty
            }
        };
    }

    private static readonly LayerEditCapabilitiesDocument LayerEditCapabilitiesDocumentDisabled = new()
    {
        AllowAdd = false,
        AllowUpdate = false,
        AllowDelete = false,
        RequireAuthentication = true,
        AllowedRoles = new List<string>()
    };

    private static readonly LayerEditConstraintsDocument LayerEditConstraintsDocumentEmpty = new()
    {
        ImmutableFields = new List<string>(),
        RequiredFields = new List<string>(),
        DefaultValues = new Dictionary<string, string?>()
    };

    private static (int? Srid, string? Crs, int? DefaultMaxRecordCount) ResolveSrid(
        GeoservicesRestLayerInfo layerInfo,
        string? defaultCrs,
        HashSet<string> serviceAdditionalCrs)
    {
        int? srid = null;
        string? crs = null;

        if (layerInfo.Extent?.SpatialReference?.Wkid is int wkid && wkid > 0)
        {
            srid = wkid;
            crs = "EPSG:" + wkid.ToString(CultureInfo.InvariantCulture);
        }

        if (layerInfo.Extent?.SpatialReference?.LatestWkid is int latest && latest > 0)
        {
            var latestCrs = "EPSG:" + latest.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(latestCrs, crs, StringComparison.OrdinalIgnoreCase))
            {
                serviceAdditionalCrs.Add(latestCrs);
            }
        }

        if (srid is null && defaultCrs.HasValue() && defaultCrs.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(defaultCrs.AsSpan(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                srid = parsed;
                crs = defaultCrs;
            }
        }

        return (srid, crs, layerInfo.MaxRecordCount ?? layerInfo.StandardMaxRecordCount ?? layerInfo.TileMaxRecordCount);
    }

    private static LayerExtentDocument? BuildExtent(GeoservicesRestExtent? extent, string? crs)
    {
        if (extent is null)
        {
            return null;
        }

        var bbox = new List<double[]> { new[] { extent.XMin, extent.YMin, extent.XMax, extent.YMax } };
        return new LayerExtentDocument
        {
            Bbox = bbox,
            Crs = crs
        };
    }

    private List<FieldDocument> BuildFields(GeoservicesRestLayerInfo layerInfo, string objectIdField, string geometryColumn)
    {
        var fields = new List<FieldDocument>();
        foreach (var field in layerInfo.Fields)
        {
            if (field.Name.IsNullOrWhiteSpace())
            {
                continue;
            }

            var mapping = MapField(field);
            var isObjectId = string.Equals(field.Name, objectIdField, StringComparison.OrdinalIgnoreCase);
            fields.Add(new FieldDocument
            {
                Name = field.Name,
                Alias = field.Alias,
                Type = mapping.DataType,
                StorageType = mapping.StorageType,
                Nullable = isObjectId ? false : (field.Nullable ?? true),
                Editable = isObjectId ? false : (field.Editable ?? true),
                MaxLength = field.Length,
                Precision = field.Precision,
                Scale = field.Scale
            });
        }

        var geometryFieldExists = fields.Any(f => string.Equals(f.Name, geometryColumn, StringComparison.OrdinalIgnoreCase));
        if (!geometryFieldExists)
        {
            fields.Add(new FieldDocument
            {
                Name = geometryColumn,
                Type = "geometry",
                StorageType = "geometry",
                Nullable = true,
                Editable = false
            });
        }

        return fields;
    }

    private LayerSchemaDefinition BuildSchemaDefinition(
        string serviceId,
        string layerId,
        string objectIdField,
        LayerDocument layerDocument,
        string tableName,
        string geometryColumn)
    {
        var fields = new List<LayerFieldSchema>();
        foreach (var field in layerDocument.Fields ?? Enumerable.Empty<FieldDocument>())
        {
            fields.Add(new LayerFieldSchema
            {
                Name = field.Name!,
                DataType = field.Type ?? field.StorageType ?? "text",
                StorageType = field.StorageType ?? field.Type ?? "text",
                Nullable = field.Nullable ?? true,
                Editable = field.Editable ?? true,
                MaxLength = field.MaxLength,
                Precision = field.Precision,
                Scale = field.Scale
            });
        }

        return new LayerSchemaDefinition
        {
            ServiceId = serviceId,
            LayerId = layerId,
            TableName = tableName,
            GeometryColumn = geometryColumn,
            PrimaryKey = objectIdField,
            TemporalColumn = layerDocument.Storage?.TemporalColumn,
            Srid = layerDocument.Storage?.Srid,
            GeometryType = layerDocument.GeometryType,
            Fields = fields
        };
    }

    private static (string DataType, string StorageType) MapField(GeoservicesRestLayerField field)
    {
        var type = (field.Type ?? string.Empty).Trim();
        return type switch
        {
            "esriFieldTypeOID" => ("integer", "integer"),
            "esriFieldTypeSmallInteger" => ("smallint", "smallint"),
            "esriFieldTypeInteger" => ("integer", "integer"),
            "esriFieldTypeSingle" => ("float", "float"),
            "esriFieldTypeDouble" => ("double", "double"),
            "esriFieldTypeDate" => ("datetime", "datetime"),
            "esriFieldTypeGUID" or "esriFieldTypeGlobalID" => ("uuid", "uuid"),
            "esriFieldTypeBlob" => ("blob", "blob"),
            _ => ("text", "text")
        };
    }

    private static string? ResolveDisplayField(GeoservicesRestLayerInfo layerInfo, string objectIdField)
    {
        if (layerInfo.DisplayField.HasValue())
        {
            return layerInfo.DisplayField;
        }

        var candidate = layerInfo.Fields.FirstOrDefault(field =>
            field.Type.Equals("esriFieldTypeString", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field.Name, objectIdField, StringComparison.OrdinalIgnoreCase));
        return candidate?.Name;
    }

    private static string ResolveObjectIdField(GeoservicesRestLayerInfo layerInfo)
    {
        var oidField = layerInfo.Fields.FirstOrDefault(field =>
            field.Type.Equals("esriFieldTypeOID", StringComparison.OrdinalIgnoreCase));
        if (oidField is not null)
        {
            return oidField.Name;
        }

        var numeric = layerInfo.Fields.FirstOrDefault(field =>
            field.Type.Equals("esriFieldTypeInteger", StringComparison.OrdinalIgnoreCase));
        return numeric?.Name ?? string.Empty;
    }

    private string ResolveServiceTitle(GeoservicesRestFeatureServiceInfo serviceInfo, string fallback)
    {
        if (_options.ServiceTitle.HasValue())
        {
            return _options.ServiceTitle!;
        }

        if (serviceInfo.DocumentInfo?.Title.HasValue() == true)
        {
            return serviceInfo.DocumentInfo!.Title!;
        }

        if (serviceInfo.ServiceDescription.HasValue())
        {
            return serviceInfo.ServiceDescription!;
        }

        if (serviceInfo.Name.HasValue())
        {
            return serviceInfo.Name!;
        }

        return fallback;
    }

    private string? ResolveServiceDescription(GeoservicesRestFeatureServiceInfo serviceInfo)
    {
        if (_options.ServiceDescription.HasValue())
        {
            return _options.ServiceDescription;
        }

        if (serviceInfo.Description.HasValue())
        {
            return serviceInfo.Description;
        }

        return serviceInfo.ServiceDescription;
    }

    private static IEnumerable<string>? ResolveKeywords(GeoservicesRestFeatureServiceInfo serviceInfo)
    {
        var keywords = new List<string>();
        if (serviceInfo.DocumentInfo?.Keywords.HasValue() == true)
        {
            keywords.AddRange(SplitKeywords(serviceInfo.DocumentInfo!.Keywords!));
        }

        return keywords.Count == 0 ? null : keywords;
    }

    private static IEnumerable<string> SplitKeywords(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keyword => keyword.HasValue());
    }

    private string CreateLayerIdentifier(string serviceId, GeoservicesRestLayerInfo layerInfo, HashSet<string> usedLayerIds)
    {
        var baseId = _options.LayerIdPrefix.HasValue()
            ? _options.LayerIdPrefix + layerInfo.Id.ToString(CultureInfo.InvariantCulture)
            : layerInfo.Name;

        baseId = baseId.IsNullOrWhiteSpace()
            ? $"{serviceId}-{layerInfo.Id}"
            : baseId;

        var sanitized = SanitizeIdentifier(baseId, $"{serviceId}-{layerInfo.Id}");
        if (!usedLayerIds.Add(sanitized))
        {
            var index = 1;
            var candidate = sanitized;
            while (!usedLayerIds.Add(candidate))
            {
                index++;
                candidate = $"{sanitized}-{index}";
            }

            sanitized = candidate;
        }

        return sanitized;
    }

    private string CreateTableName(string layerId, GeoservicesRestLayerInfo layerInfo, HashSet<string> usedTableNames)
    {
        string baseName;
        if (_options.UseLayerIdsForTables)
        {
            baseName = layerId;
        }
        else
        {
            baseName = SanitizeIdentifier(layerInfo.Name, $"{layerId}_layer");
        }

        if (_options.TableNamePrefix.HasValue())
        {
            baseName = _options.TableNamePrefix + baseName;
        }

        baseName = baseName.ToLowerInvariant();

        if (!usedTableNames.Add(baseName))
        {
            var suffix = 1;
            var candidate = baseName;
            while (!usedTableNames.Add(candidate))
            {
                suffix++;
                candidate = $"{baseName}_{suffix}";
            }

            baseName = candidate;
        }

        return baseName;
    }

    private string? ResolveCrs(GeoservicesRestSpatialReference? spatialReference, HashSet<string> additional)
    {
        if (spatialReference?.Wkid is int wkid && wkid > 0)
        {
            if (spatialReference.LatestWkid is int latest && latest > 0 && latest != wkid)
            {
                additional.Add("EPSG:" + latest.ToString(CultureInfo.InvariantCulture));
            }

            return "EPSG:" + wkid.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? MapGeometryType(string? geometryType)
    {
        if (geometryType.IsNullOrWhiteSpace())
        {
            return null;
        }

        return geometryType switch
        {
            "esriGeometryPoint" => "Point",
            "esriGeometryMultipoint" => "MultiPoint",
            "esriGeometryPolyline" => "MultiLineString",
            "esriGeometryPolygon" => "Polygon",
            _ => null
        };
    }

    private static string SanitizeIdentifier(string? value, string fallback)
    {
        if (value.IsNullOrWhiteSpace())
        {
            value = fallback;
        }

        var lower = value!.Trim().ToLowerInvariant();
        lower = IdentifierPattern.Replace(lower, "-");
        lower = lower.Trim('-');
        if (lower.IsNullOrWhiteSpace())
        {
            lower = fallback.ToLowerInvariant();
        }

        return lower;
    }
}
