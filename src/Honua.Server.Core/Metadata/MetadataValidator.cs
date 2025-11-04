// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

internal static class MetadataValidator
{
    private static readonly HashSet<string> SupportedRasterTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "geotiff",
        "cog",
        "cloud-optimized-geotiff",
        "vector"
    };

    public static MetadataDocument Validate(MetadataDocument document)
    {
        if (document.Catalog?.Id is null)
        {
            throw new InvalidDataException("Metadata catalog must include an 'id'.");
        }

        var folderIds = ValidateFolders(document.Folders);
        var dataSourceIds = ValidateDataSources(document.DataSources);
        var styleIds = ValidateStyles(document.Styles);
        var serviceIds = ValidateServices(document.Services, folderIds, dataSourceIds);
        var layerIds = ValidateLayers(document.Layers, serviceIds, styleIds);
        ValidateRasterDatasets(document.RasterDatasets, serviceIds, layerIds, styleIds);

        return document;
    }

    private static HashSet<string> ValidateFolders(List<FolderDocument>? folders)
    {
        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders ?? Enumerable.Empty<FolderDocument>())
        {
            if (folder.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Folders must include an 'id'.");
            }

            if (!folderIds.Add(folder.Id))
            {
                throw new InvalidDataException($"Duplicate folder id '{folder.Id}'.");
            }
        }

        return folderIds;
    }

    private static HashSet<string> ValidateDataSources(List<DataSourceDocument>? dataSources)
    {
        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataSource in dataSources ?? Enumerable.Empty<DataSourceDocument>())
        {
            if (dataSource.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Data sources must include an 'id'.");
            }

            if (!dataSourceIds.Add(dataSource.Id))
            {
                throw new InvalidDataException($"Duplicate data source id '{dataSource.Id}'.");
            }

            if (dataSource.Provider.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Data source '{dataSource.Id}' is missing provider.");
            }

            if (dataSource.ConnectionString.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Data source '{dataSource.Id}' is missing connectionString.");
            }
        }

        return dataSourceIds;
    }

    private static HashSet<string> ValidateStyles(List<StyleDocument>? styles)
    {
        var styleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var style in styles ?? Enumerable.Empty<StyleDocument>())
        {
            if (style?.Id.IsNullOrWhiteSpace() == true)
            {
                throw new InvalidDataException("Styles must include an 'id'.");
            }

            if (!styleIds.Add(style.Id))
            {
                throw new InvalidDataException($"Duplicate style id '{style.Id}'.");
            }
        }

        foreach (var style in styles ?? Enumerable.Empty<StyleDocument>())
        {
            if (style is null)
            {
                continue;
            }

            ValidateStyleDocument(style);
        }

        return styleIds;
    }

    private static HashSet<string> ValidateServices(
        List<ServiceDocument>? services,
        HashSet<string> folderIds,
        HashSet<string> dataSourceIds)
    {
        var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services ?? Enumerable.Empty<ServiceDocument>())
        {
            if (service.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Services must include an 'id'.");
            }

            if (!serviceIds.Add(service.Id))
            {
                throw new InvalidDataException($"Duplicate service id '{service.Id}'.");
            }

            if (service.FolderId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' is missing folderId.");
            }

            if (!folderIds.Contains(service.FolderId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown folder '{service.FolderId}'.");
            }

            if (service.DataSourceId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' is missing dataSourceId.");
            }

            if (!dataSourceIds.Contains(service.DataSourceId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown dataSource '{service.DataSourceId}'.");
            }
        }

        return serviceIds;
    }

    private static HashSet<string> ValidateLayers(
        List<LayerDocument>? layers,
        HashSet<string> serviceIds,
        HashSet<string> styleIds)
    {
        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layers ?? Enumerable.Empty<LayerDocument>())
        {
            if (layer.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layers must include an 'id'.");
            }

            if (!layerIds.Add(layer.Id))
            {
                throw new InvalidDataException($"Duplicate layer id '{layer.Id}'.");
            }

            if (layer.ServiceId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing serviceId.");
            }

            if (!serviceIds.Contains(layer.ServiceId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown service '{layer.ServiceId}'.");
            }

            if (layer.GeometryType.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing geometryType.");
            }

            if (layer.IdField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing idField.");
            }

            if (layer.GeometryField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing geometryField.");
            }

            ValidateLayerStyles(layer, styleIds);
            ValidateLayerFields(layer);
            ValidateLayerAttachments(layer);
        }

        return layerIds;
    }

    private static void ValidateLayerStyles(LayerDocument layer, HashSet<string> styleIds)
    {
        if (layer.Styles is null)
        {
            return;
        }

        if (layer.Styles.DefaultStyleId.HasValue() &&
            !styleIds.Contains(layer.Styles.DefaultStyleId))
        {
            throw new InvalidDataException($"Layer '{layer.Id}' references unknown default style '{layer.Styles.DefaultStyleId}'.");
        }

        foreach (var styleId in layer.Styles.StyleIds ?? Enumerable.Empty<string>())
        {
            if (!styleIds.Contains(styleId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown style '{styleId}'.");
            }
        }
    }

    private static void ValidateLayerFields(LayerDocument layer)
    {
        if (layer.Fields is null)
        {
            return;
        }

        foreach (var field in layer.Fields)
        {
            if (field is null)
            {
                continue;
            }

            if (field.Name.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' contains a field without a name.");
            }
        }
    }

    private static void ValidateLayerAttachments(LayerDocument layer)
    {
        if (layer.Attachments is null)
        {
            return;
        }

        if (layer.Attachments.Enabled == true && layer.Attachments.StorageProfileId.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' has attachments enabled but is missing storageProfileId.");
        }

        if (layer.Attachments.MaxSizeMiB is int maxSize && maxSize <= 0)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' attachments maxSizeMiB must be greater than zero.");
        }

        if (layer.Attachments.AllowedContentTypes is not null)
        {
            foreach (var contentType in layer.Attachments.AllowedContentTypes)
            {
                if (contentType.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Layer '{layer.Id}' attachments allowedContentTypes contains an empty value.");
                }
            }
        }

        if (layer.Attachments.DisallowedContentTypes is not null)
        {
            foreach (var contentType in layer.Attachments.DisallowedContentTypes)
            {
                if (contentType.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Layer '{layer.Id}' attachments disallowedContentTypes contains an empty value.");
                }
            }
        }

        if (layer.Attachments.ExposeOgcLinks == true && layer.Attachments.Enabled != true)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' cannot expose attachment links for OGC because attachments are disabled.");
        }
    }

    private static void ValidateRasterDatasets(
        List<RasterDatasetDocument>? rasterDatasets,
        HashSet<string> serviceIds,
        HashSet<string> layerIds,
        HashSet<string> styleIds)
    {
        var rasterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raster in rasterDatasets ?? Enumerable.Empty<RasterDatasetDocument>())
        {
            if (raster?.Id.IsNullOrWhiteSpace() == true)
            {
                throw new InvalidDataException("Raster datasets must include an 'id'.");
            }

            if (!rasterIds.Add(raster.Id))
            {
                throw new InvalidDataException($"Duplicate raster dataset id '{raster.Id}'.");
            }

            if (raster.ServiceId.HasValue() && !serviceIds.Contains(raster.ServiceId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown service '{raster.ServiceId}'.");
            }

            if (raster.LayerId.HasValue() && !layerIds.Contains(raster.LayerId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown layer '{raster.LayerId}'.");
            }

            if (raster.Source is null)
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' must include a source definition.");
            }

            if (raster.Source.Type.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' source is missing type.");
            }

            if (!SupportedRasterTypes.Contains(raster.Source.Type))
            {
                var supported = string.Join(", ", SupportedRasterTypes.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
                throw new InvalidDataException($"Raster dataset '{raster.Id}' source type '{raster.Source.Type}' is not supported. Supported types: {supported}.");
            }

            if (raster.Source.Uri.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' source is missing uri.");
            }

            ValidateRasterStyles(raster, styleIds);
        }
    }

    private static void ValidateRasterStyles(RasterDatasetDocument raster, HashSet<string> styleIds)
    {
        if (raster.Styles is null)
        {
            return;
        }

        if (raster.Styles.DefaultStyleId.HasValue() &&
            !styleIds.Contains(raster.Styles.DefaultStyleId))
        {
            throw new InvalidDataException($"Raster dataset '{raster.Id}' default style '{raster.Styles.DefaultStyleId}' is not defined.");
        }

        foreach (var styleId in raster.Styles.StyleIds ?? Enumerable.Empty<string>())
        {
            if (!styleIds.Contains(styleId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown style '{styleId}'.");
            }
        }
    }

    private static void ValidateStyleDocument(StyleDocument style)
    {
        var renderer = NormalizeRendererName(style.Renderer);

        if (renderer == "simple")
        {
            if (style.Simple is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' with renderer 'simple' must include a simple symbol definition.");
            }
        }
        else if (renderer == "uniqueValue")
        {
            if (style.UniqueValue is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' with renderer 'uniqueValue' must include unique value configuration.");
            }

            if (style.UniqueValue.Field.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Style '{style.Id}' unique value renderer must specify a field.");
            }

            if (style.UniqueValue.Classes is null || style.UniqueValue.Classes.Count == 0)
            {
                throw new InvalidDataException($"Style '{style.Id}' unique value renderer must include at least one class definition.");
            }

            foreach (var valueClass in style.UniqueValue.Classes)
            {
                if (valueClass?.Value.IsNullOrWhiteSpace() == true)
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer contains a class without a value.");
                }

                if (valueClass.Symbol is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer class '{valueClass.Value}' is missing a symbol definition.");
                }
            }
        }
        else
        {
            throw new InvalidDataException($"Style '{style.Id}' specifies unsupported renderer '{style.Renderer}'.");
        }
    }

    private static string NormalizeRendererName(string? renderer)
    {
        if (renderer.IsNullOrWhiteSpace())
        {
            return "simple";
        }

        return renderer.Trim().ToLowerInvariant() switch
        {
            "simple" => "simple",
            "unique-value" => "uniqueValue",
            "uniquevalue" => "uniqueValue",
            "unique_value" => "uniqueValue",
            _ => renderer.Trim()
        };
    }
}
