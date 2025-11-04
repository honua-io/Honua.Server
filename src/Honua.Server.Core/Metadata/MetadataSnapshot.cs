// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.VectorTiles;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public sealed class MetadataSnapshot
{
    private readonly IReadOnlyDictionary<string, ServiceDefinition> _serviceIndex;
    private readonly IReadOnlyDictionary<string, StyleDefinition> _styleIndex;

    public MetadataSnapshot(
        CatalogDefinition catalog,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition>? rasterDatasets = null,
        IReadOnlyList<StyleDefinition>? styles = null,
        ServerDefinition? server = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Folders = folders ?? throw new ArgumentNullException(nameof(folders));
        DataSources = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        Layers = layers ?? throw new ArgumentNullException(nameof(layers));
        RasterDatasets = rasterDatasets ?? Array.Empty<RasterDatasetDefinition>();
        Styles = styles ?? Array.Empty<StyleDefinition>();
        Server = server ?? ServerDefinition.Default;

        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        ValidateMetadata(Catalog, Server, Folders, DataSources, services, Layers, RasterDatasets, Styles);

        var serviceMap = new Dictionary<string, ServiceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            var attachedLayers = layers
                .Where(l => string.Equals(l.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var serviceWithLayers = service with
            {
                Layers = new ReadOnlyCollection<LayerDefinition>(attachedLayers)
            };

            serviceMap[serviceWithLayers.Id] = serviceWithLayers;
        }

        Services = new ReadOnlyCollection<ServiceDefinition>(serviceMap.Values.ToList());
        _serviceIndex = serviceMap;

        var styleMap = new Dictionary<string, StyleDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in Styles)
        {
            if (style is null)
            {
                continue;
            }

            styleMap[style.Id] = style;
        }

        _styleIndex = styleMap;
    }

    public CatalogDefinition Catalog { get; }
    public IReadOnlyList<FolderDefinition> Folders { get; }
    public IReadOnlyList<DataSourceDefinition> DataSources { get; }
    public IReadOnlyList<ServiceDefinition> Services { get; }
    public IReadOnlyList<LayerDefinition> Layers { get; }
    public IReadOnlyList<RasterDatasetDefinition> RasterDatasets { get; }
    public IReadOnlyList<StyleDefinition> Styles { get; }
    public ServerDefinition Server { get; }

    private static void ValidateMetadata(
        CatalogDefinition catalog,
        ServerDefinition server,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets,
        IReadOnlyList<StyleDefinition> styles)
    {
        if (catalog.Id.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException("Catalog id must be provided.");
        }

        // Protocol-specific validation (warnings only, not blocking)
        ValidateProtocolRequirements(services, layers, rasterDatasets);

        if (server.Cors.AllowCredentials && server.Cors.AllowAnyOrigin)
        {
            throw new InvalidDataException("CORS configuration cannot allow credentials when all origins are allowed. Specify explicit origins or disable credential forwarding.");
        }

        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (folder is null)
            {
                continue;
            }

            if (folder.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Folders must include an id.");
            }

            if (!folderIds.Add(folder.Id))
            {
                throw new InvalidDataException($"Duplicate folder id '{folder.Id}'.");
            }
        }

        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataSource in dataSources)
        {
            if (dataSource is null)
            {
                continue;
            }

            if (dataSource.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Data sources must include an id.");
            }

            if (!dataSourceIds.Add(dataSource.Id))
            {
                throw new InvalidDataException($"Duplicate data source id '{dataSource.Id}'.");
            }
        }

        var styleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in styles)
        {
            if (style is null)
            {
                continue;
            }

            if (style.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Styles must include an id.");
            }

            if (!styleIds.Add(style.Id))
            {
                throw new InvalidDataException($"Duplicate style id '{style.Id}'.");
            }

            ValidateStyleDefinition(style);
        }

        var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            if (service.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Services must include an id.");
            }

            if (!serviceIds.Add(service.Id))
            {
                throw new InvalidDataException($"Duplicate service id '{service.Id}'.");
            }

            if (service.FolderId.IsNullOrWhiteSpace() || !folderIds.Contains(service.FolderId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown folder '{service.FolderId}'.");
            }

            if (service.DataSourceId.IsNullOrWhiteSpace() || !dataSourceIds.Contains(service.DataSourceId))
            {
                throw new InvalidDataException($"Service '{service.Id}' references unknown data source '{service.DataSourceId}'.");
            }

            ValidateStoredQueries(service);
        }

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            if (layer.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layers must include an id.");
            }

            if (!layerIds.Add(layer.Id))
            {
                throw new InvalidDataException($"Duplicate layer id '{layer.Id}'.");
            }

            if (layer.ServiceId.IsNullOrWhiteSpace() || !serviceIds.Contains(layer.ServiceId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown service '{layer.ServiceId}'.");
            }

            if (layer.GeometryType.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryType.");
            }

            if (layer.IdField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing an idField.");
            }

            if (layer.GeometryField.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryField.");
            }

            if (layer.DefaultStyleId.HasValue() && !styleIds.Contains(layer.DefaultStyleId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown default style '{layer.DefaultStyleId}'.");
            }

            foreach (var styleId in layer.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Layer '{layer.Id}' references unknown style '{styleId}'.");
                }
            }

            if (layer.MinScale is < 0)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' minScale cannot be negative.");
            }

            if (layer.MaxScale is < 0)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' maxScale cannot be negative.");
            }

            if (layer.MinScale is double minScale && minScale > 0 &&
                layer.MaxScale is double maxScale && maxScale > 0 && maxScale > minScale)
            {
                throw new InvalidDataException($"Layer '{layer.Id}' maxScale ({maxScale}) cannot be greater than minScale ({minScale}).");
            }
        }

        var rasterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raster in rasterDatasets)
        {
            if (raster is null)
            {
                continue;
            }

            if (raster.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Raster datasets must include an id.");
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
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source type.");
            }

            if (raster.Source.Uri.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' is missing a source uri.");
            }

            if (raster.Styles.DefaultStyleId.HasValue() && !styleIds.Contains(raster.Styles.DefaultStyleId))
            {
                throw new InvalidDataException($"Raster dataset '{raster.Id}' default style '{raster.Styles.DefaultStyleId}' is not defined.");
            }

            foreach (var styleId in raster.Styles.StyleIds)
            {
                if (!styleIds.Contains(styleId))
                {
                    throw new InvalidDataException($"Raster dataset '{raster.Id}' references unknown style '{styleId}'.");
                }
            }
        }
    }

    private static void ValidateStyleDefinition(StyleDefinition style)
    {
        if (style.Format.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a format.");
        }

        if (style.GeometryType.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Style '{style.Id}' must specify a geometryType.");
        }

        foreach (var rule in style.Rules)
        {
            if (rule is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' contains an undefined rule entry.");
            }

            if (rule.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Style '{style.Id}' contains a rule without an id.");
            }

            if (rule.Symbolizer is null)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' is missing a symbolizer definition.");
            }

            if (rule.Filter is { } filter)
            {
                if (filter.Field.IsNullOrWhiteSpace() || filter.Value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' filter must include both field and value.");
                }
            }

            if (rule.MinScale is double minScale && rule.MaxScale is double maxScale && minScale > maxScale)
            {
                throw new InvalidDataException($"Style '{style.Id}' rule '{rule.Id}' has minScale greater than maxScale.");
            }
        }

        var renderer = style.Renderer?.Trim().ToLowerInvariant();
        switch (renderer)
        {
            case null or "" or "simple":
                if (style.Simple is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'simple' must include simple symbol details.");
                }
                break;
            case "uniquevalue":
            case "unique-value":
                if (style.UniqueValue is null)
                {
                    throw new InvalidDataException($"Style '{style.Id}' with renderer 'uniqueValue' must include unique value configuration.");
                }

                if (style.UniqueValue.Field.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must specify a field.");
                }

                if (style.UniqueValue.Classes.Count == 0)
                {
                    throw new InvalidDataException($"Style '{style.Id}' unique value renderer must include at least one class.");
                }

                foreach (var valueClass in style.UniqueValue.Classes)
                {
                    if (valueClass is null || valueClass.Value.IsNullOrWhiteSpace())
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer contains a class without a value.");
                    }

                    if (valueClass.Symbol is null)
                    {
                        throw new InvalidDataException($"Style '{style.Id}' unique value renderer class '{valueClass.Value}' is missing a symbol definition.");
                    }
                }
                break;
            default:
                throw new InvalidDataException($"Style '{style.Id}' specifies unsupported renderer '{style.Renderer}'.");
        }
    }

    private static void ValidateStoredQueries(ServiceDefinition service)
    {
        if (service.Ogc.StoredQueries.Count == 0)
        {
            return;
        }

        var queryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var storedQuery in service.Ogc.StoredQueries)
        {
            if (storedQuery is null)
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a null stored query.");
            }

            if (storedQuery.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' contains a stored query without an id.");
            }

            if (!queryIds.Add(storedQuery.Id))
            {
                throw new InvalidDataException($"Service '{service.Id}' has duplicate stored query id '{storedQuery.Id}'.");
            }

            if (storedQuery.Title.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must have a title.");
            }

            if (storedQuery.LayerId.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a layerId.");
            }

            if (storedQuery.FilterCql.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' must specify a filterCql expression.");
            }

            var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in storedQuery.Parameters)
            {
                if (parameter is null)
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a null parameter.");
                }

                if (parameter.Name.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' contains a parameter without a name.");
                }

                if (!parameterNames.Add(parameter.Name))
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' has duplicate parameter '{parameter.Name}'.");
                }

                if (parameter.Type.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a type.");
                }

                if (parameter.Title.IsNullOrWhiteSpace())
                {
                    throw new InvalidDataException($"Service '{service.Id}' stored query '{storedQuery.Id}' parameter '{parameter.Name}' must have a title.");
                }
            }
        }
    }

    public ServiceDefinition GetService(string id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (!_serviceIndex.TryGetValue(id, out var service))
        {
            throw new ServiceNotFoundException(id);
        }

        return service;
    }

    public bool TryGetService(string id, out ServiceDefinition service)
    {
        if (id is null)
        {
            service = null!;
            return false;
        }

        return _serviceIndex.TryGetValue(id, out service!);
    }

    public bool TryGetLayer(string serviceId, string layerId, out LayerDefinition layer)
    {
        layer = null!;
        if (!TryGetService(serviceId, out var service))
        {
            return false;
        }

        var match = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        layer = match;
        return true;
    }

    public bool TryGetStyle(string styleId, out StyleDefinition style)
    {
        return _styleIndex.TryGetValue(styleId, out style!);
    }

    public StyleDefinition GetStyle(string styleId)
    {
        if (TryGetStyle(styleId, out var style))
        {
            return style;
        }

        throw new StyleNotFoundException(styleId);
    }

    private static void ValidateProtocolRequirements(
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets)
    {
        // Group layers by service for validation
        var layersByService = layers.GroupBy(l => l.ServiceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            if (!layersByService.TryGetValue(service.Id, out var serviceLayers))
            {
                continue;
            }

            foreach (var layer in serviceLayers)
            {
                var validationResults = ProtocolMetadataValidator.ValidateLayer(layer, service, includeWarnings: true);

                foreach (var result in validationResults)
                {
                    // Log errors as warnings since they're protocol-specific, not structural issues
                    if (result.Errors.Count > 0)
                    {
                        var errorMessages = string.Join("; ", result.Errors);
                        System.Diagnostics.Debug.WriteLine($"[{result.Protocol}] Layer '{layer.Id}' validation: {errorMessages}");
                    }

                    // Log warnings for informational purposes
                    if (result.Warnings.Count > 0)
                    {
                        var warningMessages = string.Join("; ", result.Warnings);
                        System.Diagnostics.Debug.WriteLine($"[{result.Protocol}] Layer '{layer.Id}' recommendations: {warningMessages}");
                    }
                }
            }
        }

        // Validate raster datasets
        foreach (var raster in rasterDatasets)
        {
            var validationResults = ProtocolMetadataValidator.ValidateRasterDataset(raster, includeWarnings: true);

            foreach (var result in validationResults)
            {
                if (result.Errors.Count > 0)
                {
                    var errorMessages = string.Join("; ", result.Errors);
                    System.Diagnostics.Debug.WriteLine($"[{result.Protocol}] Raster '{raster.Id}' validation: {errorMessages}");
                }

                if (result.Warnings.Count > 0)
                {
                    var warningMessages = string.Join("; ", result.Warnings);
                    System.Diagnostics.Debug.WriteLine($"[{result.Protocol}] Raster '{raster.Id}' recommendations: {warningMessages}");
                }
            }
        }
    }
}

public sealed record CorsDefinition
{
    public static CorsDefinition Disabled => new();

    public bool Enabled { get; init; }
    public bool AllowAnyOrigin { get; init; }
    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedMethods { get; init; } = Array.Empty<string>();
    public bool AllowAnyMethod { get; init; }
    public IReadOnlyList<string> AllowedHeaders { get; init; } = Array.Empty<string>();
    public bool AllowAnyHeader { get; init; }
    public IReadOnlyList<string> ExposedHeaders { get; init; } = Array.Empty<string>();
    public bool AllowCredentials { get; init; }
    public int? MaxAge { get; init; }
}

public sealed record ServerDefinition
{
    public static ServerDefinition Default => new();

    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
    public CorsDefinition Cors { get; init; } = CorsDefinition.Disabled;
    public ServerSecurityDefinition Security { get; init; } = ServerSecurityDefinition.Default;
}

public sealed record ServerSecurityDefinition
{
    public static ServerSecurityDefinition Default => new();

    /// <summary>
    /// Allowed base directories for raster data access.
    /// All raster file paths must resolve to within one of these directories.
    /// If empty, path validation is disabled (not recommended for production).
    /// </summary>
    public IReadOnlyList<string> AllowedRasterDirectories { get; init; } = Array.Empty<string>();
}

public sealed record CatalogDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Publisher { get; init; }
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ThemeCategories { get; init; } = Array.Empty<string>();
    public CatalogContactDefinition? Contact { get; init; }
    public CatalogLicenseDefinition? License { get; init; }
    public CatalogExtentDefinition? Extents { get; init; }
}

public sealed record FolderDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
}

public sealed record DataSourceDefinition
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string ConnectionString { get; init; }
}

public sealed record ServiceDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FolderId { get; init; }
    public required string ServiceType { get; init; }
    public required string DataSourceId { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Description { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public OgcServiceDefinition Ogc { get; init; } = new();
    public VectorTileOptions? VectorTileOptions { get; init; }
    public IReadOnlyList<LayerDefinition> Layers { get; init; } = Array.Empty<LayerDefinition>();
}

public sealed record OgcServiceDefinition
{
    // API Protocol Opt-ins (per-service level)
    // Note: These are only effective if the corresponding global setting is enabled
    // Default is false - APIs must be explicitly enabled per service
    public bool CollectionsEnabled { get; init; }  // OGC API Features
    public bool WfsEnabled { get; init; }
    public bool WmsEnabled { get; init; }
    public bool WmtsEnabled { get; init; }
    public bool CswEnabled { get; init; }
    public bool WcsEnabled { get; init; }

    // Export Format Opt-ins (per-service level)
    // Default is false - export formats must be explicitly enabled
    public ExportFormatsDefinition ExportFormats { get; init; } = new();

    // OGC API Features configuration
    public int? ItemLimit { get; init; }
    public string? DefaultCrs { get; init; }
    public IReadOnlyList<string> AdditionalCrs { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConformanceClasses { get; init; } = Array.Empty<string>();

    // WFS configuration
    public IReadOnlyList<WfsStoredQueryDefinition> StoredQueries { get; init; } = Array.Empty<WfsStoredQueryDefinition>();
}

public sealed record ExportFormatsDefinition
{
    // All formats default to false - must be explicitly enabled per service
    public bool GeoJsonEnabled { get; init; } = true;  // GeoJSON is always safe, enabled by default
    public bool HtmlEnabled { get; init; } = true;     // HTML is read-only, enabled by default
    public bool CsvEnabled { get; init; }
    public bool KmlEnabled { get; init; }
    public bool KmzEnabled { get; init; }
    public bool ShapefileEnabled { get; init; }
    public bool GeoPackageEnabled { get; init; }
    public bool FlatGeobufEnabled { get; init; }
    public bool GeoArrowEnabled { get; init; }
    public bool GeoParquetEnabled { get; init; }
    public bool PmTilesEnabled { get; init; }
    public bool TopoJsonEnabled { get; init; }
}

public sealed record WfsStoredQueryDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Abstract { get; init; }
    public required string LayerId { get; init; }
    public required string FilterCql { get; init; }
    public IReadOnlyList<WfsStoredQueryParameterDefinition> Parameters { get; init; } = Array.Empty<WfsStoredQueryParameterDefinition>();
}

public sealed record WfsStoredQueryParameterDefinition
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Abstract { get; init; }
}

public sealed record LayerTemporalDefinition
{
    public static LayerTemporalDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? StartField { get; init; }
    public string? EndField { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? FixedValues { get; init; }
    public string? MinValue { get; init; }
    public string? MaxValue { get; init; }
    public string? Period { get; init; } // e.g., "P1D" for 1 day interval
}

public sealed record LayerDefinition
{
    public required string Id { get; init; }
    public required string ServiceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string GeometryType { get; init; }
    public required string IdField { get; init; }
    public string? DisplayField { get; init; }
    public required string GeometryField { get; init; }
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();
    public LayerExtentDefinition? Extent { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public LayerQueryDefinition Query { get; init; } = new();
    public LayerEditingDefinition Editing { get; init; } = LayerEditingDefinition.Disabled;
    public LayerAttachmentDefinition Attachments { get; init; } = LayerAttachmentDefinition.Disabled;
    public LayerStorageDefinition? Storage { get; init; }
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = Array.Empty<FieldDefinition>();
    public string ItemType { get; init; } = "feature";
    public string? DefaultStyleId { get; init; }
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LayerRelationshipDefinition> Relationships { get; init; } = Array.Empty<LayerRelationshipDefinition>();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public LayerTemporalDefinition Temporal { get; init; } = LayerTemporalDefinition.Disabled;
    public OpenRosa.OpenRosaLayerDefinition? OpenRosa { get; init; }
    public Iso19115Metadata? Iso19115 { get; init; }
    public StacMetadata? Stac { get; init; }
}

public sealed record LayerRelationshipDefinition
{
    public int Id { get; init; }
    public string Role { get; init; } = "esriRelRoleOrigin";
    public string Cardinality { get; init; } = "esriRelCardinalityOneToMany";
    public required string RelatedLayerId { get; init; }
    public string? RelatedTableId { get; init; }
    public required string KeyField { get; init; }
    public required string RelatedKeyField { get; init; }
    public bool? Composite { get; init; }
    public bool? ReturnGeometry { get; init; }
    public LayerRelationshipSemantics Semantics { get; init; } = LayerRelationshipSemantics.Unknown;
}

public enum LayerRelationshipSemantics
{
    Unknown,
    PrimaryKeyForeignKey
}

public sealed record LayerExtentDefinition
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
    public IReadOnlyList<TemporalIntervalDefinition> Temporal { get; init; } = Array.Empty<TemporalIntervalDefinition>();
    public string? TemporalReferenceSystem { get; init; }
}

public sealed record TemporalIntervalDefinition
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}

public sealed record LayerQueryDefinition
{
    public int? MaxRecordCount { get; init; }
    public IReadOnlyList<string> SupportedParameters { get; init; } = Array.Empty<string>();
    public LayerQueryFilterDefinition? AutoFilter { get; init; }
}

public sealed record LayerQueryFilterDefinition
{
    public string? Cql { get; init; }
    public QueryFilter? Expression { get; init; }
}

public sealed record LayerEditingDefinition
{
    public static LayerEditingDefinition Disabled { get; } = new()
    {
        Capabilities = LayerEditCapabilitiesDefinition.Disabled,
        Constraints = LayerEditConstraintDefinition.Empty
    };

    public LayerEditCapabilitiesDefinition Capabilities { get; init; } = LayerEditCapabilitiesDefinition.Disabled;
    public LayerEditConstraintDefinition Constraints { get; init; } = LayerEditConstraintDefinition.Empty;
}

public sealed record LayerEditCapabilitiesDefinition
{
    public static LayerEditCapabilitiesDefinition Disabled { get; } = new();

    public bool AllowAdd { get; init; }
    public bool AllowUpdate { get; init; }
    public bool AllowDelete { get; init; }
    public bool RequireAuthentication { get; init; } = true;
    public IReadOnlyList<string> AllowedRoles { get; init; } = Array.Empty<string>();
}

public sealed record LayerEditConstraintDefinition
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyDefaults = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());

    public static LayerEditConstraintDefinition Empty { get; } = new();

    public IReadOnlyList<string> ImmutableFields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string?> DefaultValues { get; init; } = EmptyDefaults;
}

public sealed record LayerAttachmentDefinition
{
    public static LayerAttachmentDefinition Disabled { get; } = new();

    public bool Enabled { get; init; }
    public string? StorageProfileId { get; init; }
    public int? MaxSizeMiB { get; init; }
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DisallowedContentTypes { get; init; } = Array.Empty<string>();
    public bool RequireGlobalIds { get; init; }
    public bool ReturnPresignedUrls { get; init; }
    public bool ExposeOgcLinks { get; init; }
}

public sealed record LayerStorageDefinition
{
    public string? Table { get; init; }
    public string? GeometryColumn { get; init; }
    public string? PrimaryKey { get; init; }
    public string? TemporalColumn { get; init; }
    public int? Srid { get; init; }
    public string? Crs { get; init; }
}

public sealed record FieldDefinition
{
    public required string Name { get; init; }
    public string? Alias { get; init; }
    public string? DataType { get; init; }
    public string? StorageType { get; init; }
    public bool Nullable { get; init; } = true;
    public bool Editable { get; init; } = true;
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public FieldDomainDefinition? Domain { get; init; }
}

public sealed record FieldDomainDefinition
{
    public required string Type { get; init; }  // "codedValue" or "range"
    public string? Name { get; init; }
    public IReadOnlyList<CodedValueDefinition>? CodedValues { get; init; }
    public RangeDomainDefinition? Range { get; init; }
}

public sealed record CodedValueDefinition
{
    public required string Name { get; init; }
    public required object Code { get; init; }
}

public sealed record RangeDomainDefinition
{
    public required object MinValue { get; init; }
    public required object MaxValue { get; init; }
}

public sealed record LinkDefinition
{
    public required string Href { get; init; }
    public string? Rel { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
}

public sealed record CatalogEntryDefinition
{
    public string? Summary { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CatalogContactDefinition> Contacts { get; init; } = Array.Empty<CatalogContactDefinition>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public string? Thumbnail { get; init; }
    public int? Ordering { get; init; }
    public CatalogSpatialExtentDefinition? SpatialExtent { get; init; }
    public CatalogTemporalExtentDefinition? TemporalExtent { get; init; }
}

public sealed record CatalogContactDefinition
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Organization { get; init; }
    public string? Phone { get; init; }
    public string? Url { get; init; }
    public string? Role { get; init; }
}

public sealed record CatalogLicenseDefinition
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

public sealed record CatalogExtentDefinition
{
    public CatalogSpatialExtentDefinition? Spatial { get; init; }
    public CatalogTemporalCollectionDefinition? Temporal { get; init; }
}

public sealed record CatalogSpatialExtentDefinition
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
}

public sealed record CatalogTemporalCollectionDefinition
{
    public IReadOnlyList<CatalogTemporalExtentDefinition> Interval { get; init; } = Array.Empty<CatalogTemporalExtentDefinition>();
    public string? TemporalReferenceSystem { get; init; }
}

public sealed record CatalogTemporalExtentDefinition
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}

public sealed record RasterTemporalDefinition
{
    public static RasterTemporalDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? FixedValues { get; init; }
    public string? MinValue { get; init; }
    public string? MaxValue { get; init; }
    public string? Period { get; init; } // e.g., "P1D" for 1 day interval
}

public sealed record RasterCdnDefinition
{
    public static RasterCdnDefinition Disabled => new() { Enabled = false };

    public bool Enabled { get; init; }
    public string? Policy { get; init; } // "NoCache", "ShortLived", "MediumLived", "LongLived", "VeryLongLived", "Immutable"
    public int? MaxAge { get; init; }
    public int? SharedMaxAge { get; init; }
    public bool? Public { get; init; }
    public bool? Immutable { get; init; }
    public bool? MustRevalidate { get; init; }
    public bool? NoStore { get; init; }
    public bool? NoTransform { get; init; }
    public int? StaleWhileRevalidate { get; init; }
    public int? StaleIfError { get; init; }
}

public sealed record RasterDatasetDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ServiceId { get; init; }
    public string? LayerId { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Crs { get; init; } = Array.Empty<string>();
    public CatalogEntryDefinition Catalog { get; init; } = new();
    public LayerExtentDefinition? Extent { get; init; }
    public required RasterSourceDefinition Source { get; init; }
    public RasterStyleDefinition Styles { get; init; } = new();
    public RasterCacheDefinition Cache { get; init; } = new();
    public RasterTemporalDefinition Temporal { get; init; } = RasterTemporalDefinition.Disabled;
    public RasterCdnDefinition Cdn { get; init; } = RasterCdnDefinition.Disabled;
    public StacMetadata? Stac { get; init; }
    public DateTimeOffset? Datetime { get; init; }
}

public sealed record RasterSourceDefinition
{
    public required string Type { get; init; }
    public required string Uri { get; init; }
    public string? MediaType { get; init; }
    public string? CredentialsId { get; init; }
    public bool? DisableHttpRangeRequests { get; init; }
}

public sealed record RasterStyleDefinition
{
    public string? DefaultStyleId { get; init; }
    public IReadOnlyList<string> StyleIds { get; init; } = Array.Empty<string>();
}

public sealed record RasterCacheDefinition
{
    public bool Enabled { get; init; } = true;
    public bool Preseed { get; init; }
    public IReadOnlyList<int> ZoomLevels { get; init; } = Array.Empty<int>();
}

public sealed record StyleDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string Renderer { get; init; } = "simple";
    public string Format { get; init; } = "legacy";
    public string GeometryType { get; init; } = "polygon";
    public IReadOnlyList<StyleRuleDefinition> Rules { get; init; } = Array.Empty<StyleRuleDefinition>();
    public SimpleStyleDefinition? Simple { get; init; }
    public UniqueValueStyleDefinition? UniqueValue { get; init; }
}

public sealed record StyleRuleDefinition
{
    public required string Id { get; init; }
    public bool IsDefault { get; init; }
    public string? Label { get; init; }
    public RuleFilterDefinition? Filter { get; init; }
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public SimpleStyleDefinition Symbolizer { get; init; } = new();
}

public sealed record RuleFilterDefinition(string Field, string Value);

public sealed record SimpleStyleDefinition
{
    public string? Label { get; init; }
    public string? Description { get; init; }
    public string SymbolType { get; init; } = "shape";
    public string? FillColor { get; init; }
    public string? StrokeColor { get; init; }
    public double? StrokeWidth { get; init; }
    public string? StrokeStyle { get; init; }
    public string? IconHref { get; init; }
    public double? Size { get; init; }
    public double? Opacity { get; init; }
}

public sealed record UniqueValueStyleDefinition
{
    public required string Field { get; init; }
    public SimpleStyleDefinition? DefaultSymbol { get; init; }
    public IReadOnlyList<UniqueValueStyleClassDefinition> Classes { get; init; } = Array.Empty<UniqueValueStyleClassDefinition>();
}

public sealed record UniqueValueStyleClassDefinition
{
    public required string Value { get; init; }
    public SimpleStyleDefinition Symbol { get; init; } = new();
}

// ISO 19115 Metadata Extension
public sealed record Iso19115Metadata
{
    public string? MetadataIdentifier { get; init; }
    public Iso19115MetadataStandard? MetadataStandard { get; init; }
    public Iso19115Contact? MetadataContact { get; init; }
    public Iso19115DateInfo? DateInfo { get; init; }
    public string? SpatialRepresentationType { get; init; } // vector, grid, tin
    public Iso19115SpatialResolution? SpatialResolution { get; init; }
    public string? Language { get; init; } // ISO 639-2 code (e.g., "eng")
    public string? CharacterSet { get; init; } // utf8, utf16, etc.
    public IReadOnlyList<string> TopicCategory { get; init; } = Array.Empty<string>(); // farming, biota, boundaries, etc.
    public Iso19115ResourceConstraints? ResourceConstraints { get; init; }
    public Iso19115Lineage? Lineage { get; init; }
    public Iso19115DataQualityInfo? DataQualityInfo { get; init; }
    public Iso19115MaintenanceInfo? MaintenanceInfo { get; init; }
    public Iso19115DistributionInfo? DistributionInfo { get; init; }
    public Iso19115ReferenceSystemInfo? ReferenceSystemInfo { get; init; }
}

public sealed record Iso19115MetadataStandard
{
    public string? Name { get; init; } // ISO 19115:2014, ISO 19115-1:2014
    public string? Version { get; init; }
}

public sealed record Iso19115Contact
{
    public string? OrganisationName { get; init; }
    public string? IndividualName { get; init; }
    public Iso19115ContactInfo? ContactInfo { get; init; }
    public string? Role { get; init; } // pointOfContact, custodian, owner, etc.
}

public sealed record Iso19115ContactInfo
{
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public Iso19115Address? Address { get; init; }
    public string? OnlineResource { get; init; }
}

public sealed record Iso19115Address
{
    public string? DeliveryPoint { get; init; }
    public string? City { get; init; }
    public string? AdministrativeArea { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed record Iso19115DateInfo
{
    public DateTimeOffset? Creation { get; init; }
    public DateTimeOffset? Publication { get; init; }
    public DateTimeOffset? Revision { get; init; }
}

public sealed record Iso19115SpatialResolution
{
    public int? EquivalentScale { get; init; } // e.g., 24000 for 1:24000
    public double? Distance { get; init; } // in meters
}

public sealed record Iso19115ResourceConstraints
{
    public IReadOnlyList<string> UseLimitation { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AccessConstraints { get; init; } = Array.Empty<string>(); // copyright, patent, license, etc.
    public IReadOnlyList<string> UseConstraints { get; init; } = Array.Empty<string>(); // copyright, patent, otherRestrictions, etc.
    public IReadOnlyList<string> OtherConstraints { get; init; } = Array.Empty<string>();
}

public sealed record Iso19115Lineage
{
    public string? Statement { get; init; }
    public IReadOnlyList<Iso19115Source> Sources { get; init; } = Array.Empty<Iso19115Source>();
    public IReadOnlyList<Iso19115ProcessStep> ProcessSteps { get; init; } = Array.Empty<Iso19115ProcessStep>();
}

public sealed record Iso19115Source
{
    public string? Description { get; init; }
    public int? ScaleDenominator { get; init; }
}

public sealed record Iso19115ProcessStep
{
    public required string Description { get; init; }
    public DateTimeOffset? DateTime { get; init; }
}

public sealed record Iso19115DataQualityInfo
{
    public string? Scope { get; init; } // dataset, series, featureType, etc.
    public Iso19115PositionalAccuracy? PositionalAccuracy { get; init; }
    public string? Completeness { get; init; }
    public string? LogicalConsistency { get; init; }
}

public sealed record Iso19115PositionalAccuracy
{
    public double? Value { get; init; }
    public string? Unit { get; init; } // meter, feet, etc.
    public string? EvaluationMethod { get; init; }
}

public sealed record Iso19115MaintenanceInfo
{
    public string? MaintenanceFrequency { get; init; } // continual, daily, weekly, monthly, quarterly, annually, etc.
    public DateTimeOffset? NextUpdate { get; init; }
    public string? UpdateScope { get; init; } // dataset, series, etc.
}

public sealed record Iso19115DistributionInfo
{
    public Iso19115Distributor? Distributor { get; init; }
    public IReadOnlyList<Iso19115DistributionFormat> DistributionFormats { get; init; } = Array.Empty<Iso19115DistributionFormat>();
    public Iso19115TransferOptions? TransferOptions { get; init; }
}

public sealed record Iso19115Distributor
{
    public string? OrganisationName { get; init; }
    public Iso19115ContactInfo? ContactInfo { get; init; }
}

public sealed record Iso19115DistributionFormat
{
    public required string Name { get; init; } // GeoPackage, Shapefile, GeoTIFF, etc.
    public string? Version { get; init; }
}

public sealed record Iso19115TransferOptions
{
    public string? OnlineResource { get; init; }
}

public sealed record Iso19115ReferenceSystemInfo
{
    public string? Code { get; init; } // e.g., "2227"
    public string? CodeSpace { get; init; } // e.g., "EPSG"
    public string? Version { get; init; }
}

// STAC Metadata Extension
public sealed record StacMetadata
{
    public bool Enabled { get; init; } = true;
    public string? CollectionId { get; init; }
    public string? License { get; init; } // SPDX license identifier (e.g., "CC-BY-4.0", "proprietary")
    public IReadOnlyList<StacProvider> Providers { get; init; } = Array.Empty<StacProvider>();
    public IReadOnlyDictionary<string, StacAssetDefinition> Assets { get; init; } = new Dictionary<string, StacAssetDefinition>();
    public IReadOnlyDictionary<string, StacAssetDefinition> ItemAssets { get; init; } = new Dictionary<string, StacAssetDefinition>();
    public IReadOnlyDictionary<string, object> Summaries { get; init; } = new Dictionary<string, object>();
    public IReadOnlyList<string> StacExtensions { get; init; } = Array.Empty<string>();
    public string? ItemIdTemplate { get; init; } // e.g., "roads-{road_id}"
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}

public sealed record StacProvider
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // producer, licensor, processor, host
    public string? Url { get; init; }
}

public sealed record StacAssetDefinition
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; } // MIME type
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // data, metadata, thumbnail
    public string? Href { get; init; }
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}
