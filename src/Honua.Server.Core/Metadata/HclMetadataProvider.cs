// <copyright file="HclMetadataProvider.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Metadata provider that builds MetadataSnapshot from Configuration V2 (HonuaConfig).
/// This is the bridge that allows Configuration V2 to work with the existing metadata registry system.
/// </summary>
public sealed class HclMetadataProvider : IMetadataProvider
{
    private readonly HonuaConfig config;

    /// <inheritdoc/>
    public bool SupportsChangeNotifications => false;

    /// <inheritdoc/>
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    public HclMetadataProvider(HonuaConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var catalog = this.BuildCatalog();
        var folders = this.BuildFolders();
        var dataSources = this.BuildDataSources();
        var services = this.BuildServices();
        var layers = this.BuildLayers();
        var styles = this.BuildStyles();

        var snapshot = new MetadataSnapshot(
            catalog: catalog,
            folders: folders,
            dataSources: dataSources,
            services: services,
            layers: layers,
            rasterDatasets: Array.Empty<RasterDatasetDefinition>(),
            styles: styles,
            layerGroups: null,
            server: this.BuildServer());

        return Task.FromResult(snapshot);
    }

    private CatalogDefinition BuildCatalog()
    {
        // Create a basic catalog from Configuration V2
        // In a full implementation, this might come from HonuaConfig metadata
        return new CatalogDefinition
        {
            Id = "honua-catalog",
            Title = "Honua Server",
            Description = "Honua Server Configuration V2",
            Version = this.config.Honua.Version,
            Publisher = null,
            Keywords = Array.Empty<string>(),
            ThemeCategories = Array.Empty<string>(),
            Contact = null,
            License = null,
            Extents = null,
        };
    }

    private IReadOnlyList<FolderDefinition> BuildFolders()
    {
        // Configuration V2 doesn't have explicit folders yet
        // Create a default folder for services
        return new[]
        {
            new FolderDefinition
            {
                Id = "default",
                Title = "Services",
                Order = 0
            },
        };
    }

    private IReadOnlyList<DataSourceDefinition> BuildDataSources()
    {
        var dataSources = new List<DataSourceDefinition>();

        foreach (var (key, dataSource) in this.config.DataSources)
        {
            if (dataSource is null)
            {
                continue;
            }

            dataSources.Add(new DataSourceDefinition
            {
                Id = dataSource.Id,
                Provider = dataSource.Provider,
                ConnectionString = dataSource.Connection,
            });
        }

        return dataSources.AsReadOnly();
    }

    private IReadOnlyList<ServiceDefinition> BuildServices()
    {
        var services = new List<ServiceDefinition>();

        foreach (var (key, service) in this.config.Services)
        {
            if (service is null || !service.Enabled)
            {
                continue;
            }

            // Determine service type from Configuration V2 service type
            var serviceType = this.MapServiceType(service.Type);

            services.Add(new ServiceDefinition
            {
                Id = service.Id,
                Title = service.Id, // Use ID as title for now
                FolderId = "default",
                ServiceType = serviceType,
                DataSourceId = this.DetermineDataSourceId(service),
                Enabled = service.Enabled,
                Description = null,
                Keywords = Array.Empty<string>(),
                Links = Array.Empty<LinkDefinition>(),
                Catalog = new CatalogEntryDefinition(),
                Ogc = this.BuildOgcServiceDefinition(service),
                VectorTileOptions = null,
                Layers = Array.Empty<LayerDefinition>(), // Will be populated by MetadataSnapshot
            });
        }

        return services.AsReadOnly();
    }

    private IReadOnlyList<LayerDefinition> BuildLayers()
    {
        var layers = new List<LayerDefinition>();

        foreach (var (key, layer) in this.config.Layers)
        {
            if (layer is null)
            {
                continue;
            }

            // Determine which service(s) this layer belongs to
            var serviceId = layer.Services.FirstOrDefault() ?? "default";

            layers.Add(new LayerDefinition
            {
                Id = layer.Id,
                ServiceId = serviceId,
                Title = layer.Title,
                Description = layer.Description,
                GeometryType = layer.Geometry?.Type ?? "Polygon",
                IdField = layer.IdField,
                DisplayField = layer.DisplayField,
                GeometryField = layer.Geometry?.Column ?? "geometry",
                Crs = new[] { $"EPSG:{layer.Geometry?.Srid ?? 4326}" },
                Extent = null,
                Keywords = Array.Empty<string>(),
                Links = Array.Empty<LinkDefinition>(),
                Catalog = new CatalogEntryDefinition(),
                Query = new LayerQueryDefinition(),
                Editing = LayerEditingDefinition.Disabled,
                Attachments = LayerAttachmentDefinition.Disabled,
                Storage = this.BuildLayerStorage(layer),
                SqlView = null, // Configuration V2 doesn't support SQL views yet
                Fields = this.BuildFields(layer),
                ItemType = "feature",
                DefaultStyleId = null,
                StyleIds = Array.Empty<string>(),
                Relationships = Array.Empty<LayerRelationshipDefinition>(),
                MinScale = null,
                MaxScale = null,
                Temporal = LayerTemporalDefinition.Disabled,
                OpenRosa = null,
                Iso19115 = null,
                Stac = null,
                HasZ = false,
                HasM = false,
                ZField = null,
            });
        }

        return layers.AsReadOnly();
    }

    private IReadOnlyList<StyleDefinition> BuildStyles()
    {
        // Configuration V2 doesn't have styles yet
        // Return empty list
        return Array.Empty<StyleDefinition>();
    }

    private ServerDefinition BuildServer()
    {
        var cors = this.config.Honua.Cors;

        return new ServerDefinition
        {
            AllowedHosts = Array.Empty<string>(),
            Cors = cors is not null ? new CorsDefinition
            {
                Enabled = true,
                AllowAnyOrigin = cors.AllowAnyOrigin,
                AllowedOrigins = cors.AllowedOrigins.AsReadOnly(),
                AllowedMethods = Array.Empty<string>(),
                AllowAnyMethod = false,
                AllowedHeaders = Array.Empty<string>(),
                AllowAnyHeader = false,
                ExposedHeaders = Array.Empty<string>(),
                AllowCredentials = cors.AllowCredentials,
                MaxAge = null,
            }
            : CorsDefinition.Disabled,
            Security = ServerSecurityDefinition.Default,
            Rbac = RbacDefinition.Default,
        };
    }

    private string MapServiceType(string configServiceType)
    {
        return configServiceType.ToLowerInvariant() switch
        {
            "odata" => "OData",
            "ogc_api" or "ogcapi" => "OgcApi",
            "wfs" => "WFS",
            "wms" => "WMS",
            "wmts" => "WMTS",
            "csw" => "CSW",
            "wcs" => "WCS",
            _ => configServiceType,
        };
    }

    private string DetermineDataSourceId(ServiceBlock service)
    {
        // In Configuration V2, layers reference data sources
        // We need to find a layer that uses this service and get its data source
        var layerForService = this.config.Layers.Values
            .FirstOrDefault(l => l.Services.Contains(service.Id));

        return layerForService?.DataSource ?? this.config.DataSources.Keys.FirstOrDefault() ?? "default";
    }

    private OgcServiceDefinition BuildOgcServiceDefinition(ServiceBlock service)
    {
        // Extract OGC-specific settings from service.Settings dictionary
        var settings = service.Settings;

        return new OgcServiceDefinition
        {
            CollectionsEnabled = this.GetBoolSetting(settings, "collections_enabled", true),
            WfsEnabled = this.GetBoolSetting(settings, "wfs_enabled", false),
            WmsEnabled = this.GetBoolSetting(settings, "wms_enabled", false),
            WmtsEnabled = this.GetBoolSetting(settings, "wmts_enabled", false),
            CswEnabled = this.GetBoolSetting(settings, "csw_enabled", false),
            WcsEnabled = this.GetBoolSetting(settings, "wcs_enabled", false),
            ExportFormats = new ExportFormatsDefinition
            {
                GeoJsonEnabled = true,
                HtmlEnabled = true,
                CsvEnabled = this.GetBoolSetting(settings, "csv_enabled", false),
                KmlEnabled = this.GetBoolSetting(settings, "kml_enabled", false),
                KmzEnabled = this.GetBoolSetting(settings, "kmz_enabled", false),
                ShapefileEnabled = this.GetBoolSetting(settings, "shapefile_enabled", false),
                GeoPackageEnabled = this.GetBoolSetting(settings, "geopackage_enabled", false),
                FlatGeobufEnabled = this.GetBoolSetting(settings, "flatgeobuf_enabled", false),
                GeoArrowEnabled = this.GetBoolSetting(settings, "geoarrow_enabled", false),
                GeoParquetEnabled = this.GetBoolSetting(settings, "geoparquet_enabled", false),
                PmTilesEnabled = this.GetBoolSetting(settings, "pmtiles_enabled", false),
                TopoJsonEnabled = this.GetBoolSetting(settings, "topojson_enabled", false),
            },
            ItemLimit = this.GetIntSetting(settings, "max_page_size"),
            DefaultCrs = this.GetStringSetting(settings, "default_crs"),
            AdditionalCrs = Array.Empty<string>(),
            ConformanceClasses = Array.Empty<string>(),
            StoredQueries = Array.Empty<WfsStoredQueryDefinition>(),
        };
    }

    private LayerStorageDefinition BuildLayerStorage(LayerBlock layer)
    {
        int? srid = layer.Geometry?.Srid;
        return new LayerStorageDefinition
        {
            Table = layer.Table,
            GeometryColumn = layer.Geometry?.Column,
            PrimaryKey = layer.IdField,
            TemporalColumn = null,
            Srid = srid,
            Crs = srid.HasValue ? $"EPSG:{srid.Value}" : null,
            HasZ = false,
            HasM = false,
        };
    }

    private IReadOnlyList<FieldDefinition> BuildFields(LayerBlock layer)
    {
        if (!layer.IntrospectFields || layer.Fields is null || layer.Fields.Count == 0)
        {
            // Return empty list - fields will be introspected from database
            return Array.Empty<FieldDefinition>();
        }

        var fields = new List<FieldDefinition>();

        foreach (var (fieldName, fieldDef) in layer.Fields)
        {
            fields.Add(new FieldDefinition
            {
                Name = fieldName,
                Alias = fieldName,
                DataType = this.MapFieldType(fieldDef.Type),
                StorageType = fieldDef.Type,
                Nullable = fieldDef.Nullable,
                Editable = true,
                MaxLength = null,
                Precision = null,
                Scale = null,
                Domain = null,
            });
        }

        return fields.AsReadOnly();
    }

    private string MapFieldType(string configFieldType)
    {
        return configFieldType.ToLowerInvariant() switch
        {
            "int" or "integer" => "esriFieldTypeInteger",
            "long" or "bigint" => "esriFieldTypeBigInteger",
            "string" or "text" => "esriFieldTypeString",
            "double" or "float" => "esriFieldTypeDouble",
            "datetime" or "timestamp" => "esriFieldTypeDate",
            "bool" or "boolean" => "esriFieldTypeSmallInteger",
            "geometry" => "esriFieldTypeGeometry",
            _ => "esriFieldTypeString",
        };
    }

    private bool GetBoolSetting(Dictionary<string, object?> settings, string key, bool defaultValue = false)
    {
        if (settings.TryGetValue(key, out var value) && value is bool boolValue)
        {
            return boolValue;
        }

        return defaultValue;
    }

    private int? GetIntSetting(Dictionary<string, object?> settings, string key)
    {
        if (settings.TryGetValue(key, out var value))
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
        }

        return null;
    }

    private string? GetStringSetting(Dictionary<string, object?> settings, string key)
    {
        if (settings.TryGetValue(key, out var value) && value is string stringValue)
        {
            return stringValue;
        }

        return null;
    }
}
