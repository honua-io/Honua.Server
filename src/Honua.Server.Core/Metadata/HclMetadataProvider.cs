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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Metadata provider that builds MetadataSnapshot from Configuration V2 (HonuaConfig).
/// This is the bridge that allows Configuration V2 to work with the existing metadata registry system.
/// Supports hot-reload via file watcher and distributed change notifications for high availability.
/// </summary>
public sealed class HclMetadataProvider : IMetadataProvider, IReloadableMetadataProvider, IDisposable
{
    private readonly IConfigurationChangeNotifier? _changeNotifier;
    private readonly HclConfigurationWatcher? _configWatcher;
    private readonly ILogger<HclMetadataProvider> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private HonuaConfig _config;
    private string? _configurationPath;
    private IDisposable? _fileWatcherSubscription;
    private IDisposable? _redisNotifierSubscription;
    private bool _disposed;

    /// <inheritdoc/>
    public bool SupportsChangeNotifications => true;

    /// <inheritdoc/>
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="HclMetadataProvider"/> class.
    /// </summary>
    /// <param name="config">The initial HonuaConfig to use.</param>
    /// <param name="changeNotifier">Optional configuration change notifier for Redis-based HA notifications.</param>
    /// <param name="configWatcher">Optional file system watcher for local file changes.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public HclMetadataProvider(
        HonuaConfig config,
        IConfigurationChangeNotifier? changeNotifier = null,
        HclConfigurationWatcher? configWatcher = null,
        ILogger<HclMetadataProvider>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _changeNotifier = changeNotifier;
        _configWatcher = configWatcher;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HclMetadataProvider>.Instance;

        // Wire up file watcher if available
        if (_configWatcher != null)
        {
            _fileWatcherSubscription = ChangeToken.OnChange(
                () => _configWatcher.CurrentChangeToken,
                async () => await OnFileChangedAsync("file-watcher").ConfigureAwait(false));

            _logger.LogDebug("HclMetadataProvider initialized with file watcher support");
        }

        // Subscribe to Redis change notifications if available
        if (_changeNotifier != null)
        {
            // Note: We'll subscribe asynchronously in LoadAsync since SubscribeAsync is async
            _logger.LogDebug("HclMetadataProvider initialized with Redis change notification support");
        }
    }

    /// <inheritdoc/>
    public async Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Subscribe to Redis notifications on first load if available and not already subscribed
        if (_changeNotifier != null && _redisNotifierSubscription == null)
        {
            try
            {
                _redisNotifierSubscription = await _changeNotifier.SubscribeAsync(
                    OnRedisNotificationAsync,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Subscribed to Redis configuration change notifications");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to subscribe to Redis configuration change notifications. Will continue without Redis support.");
            }
        }

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

        return snapshot;
    }

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_configurationPath))
        {
            _logger.LogWarning("Cannot reload: configuration path not set. Call SetConfigurationPath first.");
            return;
        }

        await ReloadInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the configuration path for later reloading.
    /// This should be called after construction to enable hot-reload functionality.
    /// </summary>
    /// <param name="configurationPath">The path to the .hcl configuration file.</param>
    public void SetConfigurationPath(string configurationPath)
    {
        _configurationPath = configurationPath;
        _logger.LogDebug("Configuration path set to: {ConfigPath}", configurationPath);
    }

    /// <summary>
    /// Internal reload logic that reloads configuration from disk and notifies subscribers.
    /// </summary>
    private async Task ReloadInternalAsync(CancellationToken cancellationToken)
    {
        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Reloading HCL configuration from: {ConfigPath}", _configurationPath);

            // Reload configuration from disk
            var newConfig = await HonuaConfigLoader.LoadAsync(_configurationPath!).ConfigureAwait(false);

            // Update internal config reference
            _config = newConfig;

            _logger.LogInformation("HCL configuration reloaded successfully");

            // Trigger MetadataChanged event to notify subscribers
            MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("config-reload"));

            _logger.LogDebug("MetadataChanged event triggered after reload");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload HCL configuration from: {ConfigPath}", _configurationPath);
            throw;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    /// Callback invoked when the file watcher detects a configuration file change.
    /// </summary>
    private async Task OnFileChangedAsync(string source)
    {
        if (_disposed || string.IsNullOrWhiteSpace(_configurationPath))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Configuration file change detected by {Source}", source);
            await ReloadInternalAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file change notification from {Source}", source);
        }
    }

    /// <summary>
    /// Callback invoked when a Redis configuration change notification is received.
    /// </summary>
    private async Task OnRedisNotificationAsync(string configPath)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Configuration change notification received from Redis for: {ConfigPath}", configPath);

            // Only reload if the notification is for our configuration file
            if (!string.IsNullOrWhiteSpace(_configurationPath) &&
                string.Equals(configPath, _configurationPath, StringComparison.OrdinalIgnoreCase))
            {
                await ReloadInternalAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Ignoring Redis notification for different config path: {NotifiedPath} (current: {CurrentPath})",
                    configPath, _configurationPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Redis configuration change notification for: {ConfigPath}", configPath);
        }
    }

    /// <summary>
    /// Disposes the provider and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Unsubscribe from file watcher
        _fileWatcherSubscription?.Dispose();
        _fileWatcherSubscription = null;

        // Unsubscribe from Redis notifications
        _redisNotifierSubscription?.Dispose();
        _redisNotifierSubscription = null;

        // Dispose reload lock
        _reloadLock.Dispose();

        _logger.LogDebug("HclMetadataProvider disposed");
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
            Version = _config.Honua.Version,
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

        foreach (var (key, dataSource) in _config.DataSources)
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

        foreach (var (key, service) in _config.Services)
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

        foreach (var (key, layer) in _config.Layers)
        {
            if (layer is null)
            {
                continue;
            }

            // Create one LayerDefinition per service in the services array
            // This allows a layer to be published to multiple services (e.g., WFS + WMS + WMTS)
            foreach (var serviceRef in layer.Services)
            {
                // Clean service reference (e.g., "service.wfs" -> "wfs")
                var serviceId = ExtractReference(serviceRef);

                // Create a unique layer ID for this service (e.g., "test_features_wfs" for wfs service)
                // Use the original layer ID if only one service, otherwise append service suffix
                var layerId = layer.Services.Count == 1 ? layer.Id : $"{layer.Id}_{serviceId}";

                layers.Add(new LayerDefinition
                {
                    Id = layerId,
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
        var cors = _config.Honua.Cors;
        var allowedHosts = _config.Honua.AllowedHosts;

        // If no allowed hosts are configured, default to "*" for test/development compatibility
        var hosts = allowedHosts.Count > 0 ? allowedHosts.AsReadOnly() : new List<string> { "*" }.AsReadOnly();

        return new ServerDefinition
        {
            AllowedHosts = hosts,
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
        var layerForService = _config.Layers.Values
            .FirstOrDefault(l => l.Services.Any(s => ExtractReference(s) == service.Id));

        // Clean data source reference (e.g., "data_source.gis_db" -> "gis_db")
        var dataSourceRef = layerForService?.DataSource;
        if (!string.IsNullOrWhiteSpace(dataSourceRef))
        {
            return ExtractReference(dataSourceRef);
        }

        return _config.DataSources.Keys.FirstOrDefault() ?? "default";
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

    /// <summary>
    /// Extract clean reference from reference syntax.
    /// Examples: "data_source.sqlite-test" -> "sqlite-test", "service.wfs" -> "wfs", "odata" -> "odata"
    /// </summary>
    private static string ExtractReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return reference;
        }

        var parts = reference.Split('.');
        return parts.Length > 1 ? parts[^1] : reference;
    }
}
