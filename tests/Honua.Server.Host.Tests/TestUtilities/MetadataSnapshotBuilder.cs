using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Host.Tests.TestUtilities;

/// <summary>
/// Factory for creating MetadataSnapshot instances in tests with sensible defaults.
/// Provides a fluent API to build test metadata with minimal boilerplate.
/// </summary>
public sealed class MetadataSnapshotBuilder
{
    private CatalogDefinition _catalog;
    private readonly List<FolderDefinition> _folders = new();
    private readonly List<DataSourceDefinition> _dataSources = new();
    private readonly List<ServiceDefinition> _services = new();
    private readonly List<LayerDefinition> _layers = new();
    private readonly List<RasterDatasetDefinition> _rasterDatasets = new();
    private readonly List<StyleDefinition> _styles = new();
    private ServerDefinition _server;

    public MetadataSnapshotBuilder()
    {
        // Set up sensible defaults
        _catalog = new CatalogDefinition
        {
            Id = "test-catalog",
            Title = "Test Catalog",
            Description = "Test catalog for unit tests"
        };
        _server = ServerDefinition.Default;
    }

    /// <summary>
    /// Sets the catalog definition.
    /// </summary>
    public MetadataSnapshotBuilder WithCatalog(CatalogDefinition catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        return this;
    }

    /// <summary>
    /// Sets the catalog with minimal properties.
    /// </summary>
    public MetadataSnapshotBuilder WithCatalog(string id, string? title = null, string? description = null)
    {
        _catalog = new CatalogDefinition
        {
            Id = id,
            Title = title ?? id,
            Description = description ?? $"Test catalog {id}"
        };
        return this;
    }

    /// <summary>
    /// Sets the server definition.
    /// </summary>
    public MetadataSnapshotBuilder WithServer(ServerDefinition server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        return this;
    }

    /// <summary>
    /// Adds a folder to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithFolder(string id, string? title = null, int? order = null)
    {
        var folder = new FolderDefinition
        {
            Id = id,
            Title = title ?? id,
            Order = order
        };
        _folders.Add(folder);
        return this;
    }

    /// <summary>
    /// Adds a folder definition to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithFolder(FolderDefinition folder)
    {
        _folders.Add(folder ?? throw new ArgumentNullException(nameof(folder)));
        return this;
    }

    /// <summary>
    /// Adds a data source to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithDataSource(string id, string provider = "test-provider", string? connectionString = null)
    {
        var dataSource = new DataSourceDefinition
        {
            Id = id,
            Provider = provider,
            ConnectionString = connectionString ?? $"Host=localhost;Database={id}"
        };
        _dataSources.Add(dataSource);
        return this;
    }

    /// <summary>
    /// Adds a data source definition to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithDataSource(DataSourceDefinition dataSource)
    {
        _dataSources.Add(dataSource ?? throw new ArgumentNullException(nameof(dataSource)));
        return this;
    }

    /// <summary>
    /// Adds a service to the snapshot with default settings.
    /// </summary>
    public MetadataSnapshotBuilder WithService(
        string id,
        string folderId,
        string dataSourceId,
        string? title = null,
        string serviceType = "feature",
        bool enabled = true,
        Action<ServiceBuilder>? configure = null)
    {
        var builder = new ServiceBuilder(id, folderId, dataSourceId)
            .WithTitle(title ?? id)
            .WithServiceType(serviceType)
            .WithEnabled(enabled);

        configure?.Invoke(builder);

        _services.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a service definition to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithService(ServiceDefinition service)
    {
        _services.Add(service ?? throw new ArgumentNullException(nameof(service)));
        return this;
    }

    /// <summary>
    /// Adds a layer to the snapshot with default settings.
    /// </summary>
    public MetadataSnapshotBuilder WithLayer(
        string id,
        string serviceId,
        string? title = null,
        string geometryType = "Point",
        string idField = "objectid",
        string geometryField = "shape",
        Action<LayerBuilder>? configure = null)
    {
        var builder = new LayerBuilder(id, serviceId)
            .WithTitle(title ?? id)
            .WithGeometryType(geometryType)
            .WithIdField(idField)
            .WithGeometryField(geometryField);

        configure?.Invoke(builder);

        _layers.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a layer definition to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithLayer(LayerDefinition layer)
    {
        _layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
        return this;
    }

    /// <summary>
    /// Adds a raster dataset to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithRasterDataset(
        string id,
        string uri,
        string? title = null,
        string? serviceId = null,
        string? layerId = null,
        Action<RasterDatasetBuilder>? configure = null)
    {
        var builder = new RasterDatasetBuilder(id, uri)
            .WithTitle(title ?? id)
            .WithServiceId(serviceId)
            .WithLayerId(layerId);

        configure?.Invoke(builder);

        _rasterDatasets.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a raster dataset definition to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithRasterDataset(RasterDatasetDefinition rasterDataset)
    {
        _rasterDatasets.Add(rasterDataset ?? throw new ArgumentNullException(nameof(rasterDataset)));
        return this;
    }

    /// <summary>
    /// Adds a style to the snapshot.
    /// </summary>
    public MetadataSnapshotBuilder WithStyle(StyleDefinition style)
    {
        _styles.Add(style ?? throw new ArgumentNullException(nameof(style)));
        return this;
    }

    /// <summary>
    /// Builds the MetadataSnapshot.
    /// </summary>
    public MetadataSnapshot Build()
    {
        return new MetadataSnapshot(
            _catalog,
            _folders,
            _dataSources,
            _services,
            _layers,
            _rasterDatasets,
            _styles,
            _server);
    }

    /// <summary>
    /// Creates an empty snapshot with just a catalog.
    /// Useful for tests that don't need any services or layers.
    /// </summary>
    public static MetadataSnapshot CreateEmpty(string catalogId = "test-catalog")
    {
        return new MetadataSnapshotBuilder()
            .WithCatalog(catalogId)
            .Build();
    }

    /// <summary>
    /// Creates a minimal valid snapshot with one folder, one data source, one service, and one layer.
    /// This is the most common setup for feature-based tests.
    /// </summary>
    public static MetadataSnapshot CreateDefault(
        string folderId = "test-folder",
        string dataSourceId = "test-datasource",
        string serviceId = "test-service",
        string layerId = "test-layer")
    {
        return new MetadataSnapshotBuilder()
            .WithFolder(folderId, "Test Folder")
            .WithDataSource(dataSourceId)
            .WithService(serviceId, folderId, dataSourceId, "Test Service")
            .WithLayer(layerId, serviceId, "Test Layer")
            .Build();
    }

    /// <summary>
    /// Creates a snapshot configured for OGC API Features testing.
    /// </summary>
    public static MetadataSnapshot CreateForOgcFeatures(
        string folderId = "test-folder",
        string dataSourceId = "test-datasource",
        string serviceId = "test-service",
        string layerId = "test-layer")
    {
        return new MetadataSnapshotBuilder()
            .WithFolder(folderId)
            .WithDataSource(dataSourceId)
            .WithService(serviceId, folderId, dataSourceId, configure: s =>
                s.WithOgc(o => o.WithCollectionsEnabled(true)))
            .WithLayer(layerId, serviceId)
            .Build();
    }

    /// <summary>
    /// Creates a snapshot for testing attachment features.
    /// </summary>
    public static MetadataSnapshot CreateWithAttachments(
        string folderId = "test-folder",
        string dataSourceId = "test-datasource",
        string serviceId = "test-service",
        string layerId = "test-layer",
        string storageProfileId = "test-storage-profile")
    {
        return new MetadataSnapshotBuilder()
            .WithFolder(folderId)
            .WithDataSource(dataSourceId)
            .WithService(serviceId, folderId, dataSourceId, configure: s =>
                s.WithOgc(o => o.WithCollectionsEnabled(true)))
            .WithLayer(layerId, serviceId, configure: l =>
                l.WithAttachments(storageProfileId, exposeOgcLinks: true))
            .Build();
    }

    /// <summary>
    /// Creates a snapshot for WCS/raster testing.
    /// </summary>
    public static MetadataSnapshot CreateForRaster(
        string rasterDatasetId = "test-raster",
        string rasterUri = "/data/test.tif")
    {
        return new MetadataSnapshotBuilder()
            .WithRasterDataset(rasterDatasetId, rasterUri)
            .Build();
    }
}

/// <summary>
/// Builder for creating ServiceDefinition instances with a fluent API.
/// </summary>
public sealed class ServiceBuilder
{
    private readonly string _id;
    private readonly string _folderId;
    private readonly string _dataSourceId;
    private string _title;
    private string _serviceType = "feature";
    private bool _enabled = true;
    private string? _description;
    private List<string> _keywords = new();
    private OgcServiceDefinition _ogc = new();

    public ServiceBuilder(string id, string folderId, string dataSourceId)
    {
        _id = id;
        _folderId = folderId;
        _dataSourceId = dataSourceId;
        _title = id;
    }

    public ServiceBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public ServiceBuilder WithServiceType(string serviceType)
    {
        _serviceType = serviceType;
        return this;
    }

    public ServiceBuilder WithEnabled(bool enabled)
    {
        _enabled = enabled;
        return this;
    }

    public ServiceBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ServiceBuilder WithKeywords(params string[] keywords)
    {
        _keywords.AddRange(keywords);
        return this;
    }

    public ServiceBuilder WithOgc(Action<OgcServiceBuilder> configure)
    {
        var builder = new OgcServiceBuilder();
        configure(builder);
        _ogc = builder.Build();
        return this;
    }

    public ServiceDefinition Build()
    {
        return new ServiceDefinition
        {
            Id = _id,
            Title = _title,
            FolderId = _folderId,
            ServiceType = _serviceType,
            DataSourceId = _dataSourceId,
            Enabled = _enabled,
            Description = _description,
            Keywords = _keywords,
            Ogc = _ogc
        };
    }
}

/// <summary>
/// Builder for creating OgcServiceDefinition instances.
/// </summary>
public sealed class OgcServiceBuilder
{
    private bool _collectionsEnabled;
    private bool _wfsEnabled;
    private bool _wmsEnabled;
    private bool _wmtsEnabled;
    private bool _cswEnabled;
    private bool _wcsEnabled;

    public OgcServiceBuilder WithCollectionsEnabled(bool enabled = true)
    {
        _collectionsEnabled = enabled;
        return this;
    }

    public OgcServiceBuilder WithWfsEnabled(bool enabled = true)
    {
        _wfsEnabled = enabled;
        return this;
    }

    public OgcServiceBuilder WithWmsEnabled(bool enabled = true)
    {
        _wmsEnabled = enabled;
        return this;
    }

    public OgcServiceBuilder WithWmtsEnabled(bool enabled = true)
    {
        _wmtsEnabled = enabled;
        return this;
    }

    public OgcServiceBuilder WithCswEnabled(bool enabled = true)
    {
        _cswEnabled = enabled;
        return this;
    }

    public OgcServiceBuilder WithWcsEnabled(bool enabled = true)
    {
        _wcsEnabled = enabled;
        return this;
    }

    public OgcServiceDefinition Build()
    {
        return new OgcServiceDefinition
        {
            CollectionsEnabled = _collectionsEnabled,
            WfsEnabled = _wfsEnabled,
            WmsEnabled = _wmsEnabled,
            WmtsEnabled = _wmtsEnabled,
            CswEnabled = _cswEnabled,
            WcsEnabled = _wcsEnabled
        };
    }
}

/// <summary>
/// Builder for creating LayerDefinition instances with a fluent API.
/// </summary>
public sealed class LayerBuilder
{
    private readonly string _id;
    private readonly string _serviceId;
    private string _title;
    private string _geometryType = "Point";
    private string _idField = "objectid";
    private string _geometryField = "shape";
    private string? _description;
    private List<FieldDefinition> _fields = new();
    private LayerAttachmentDefinition _attachments = LayerAttachmentDefinition.Disabled;
    private LayerTemporalDefinition _temporal = LayerTemporalDefinition.Disabled;

    public LayerBuilder(string id, string serviceId)
    {
        _id = id;
        _serviceId = serviceId;
        _title = id;

        // Add default id field
        _fields.Add(new FieldDefinition
        {
            Name = "objectid",
            DataType = "integer",
            StorageType = "int4",
            Nullable = false
        });
    }

    public LayerBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public LayerBuilder WithGeometryType(string geometryType)
    {
        _geometryType = geometryType;
        return this;
    }

    public LayerBuilder WithIdField(string idField)
    {
        _idField = idField;
        return this;
    }

    public LayerBuilder WithGeometryField(string geometryField)
    {
        _geometryField = geometryField;
        return this;
    }

    public LayerBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public LayerBuilder WithField(FieldDefinition field)
    {
        _fields.Add(field);
        return this;
    }

    public LayerBuilder WithField(string name, string dataType = "string", bool nullable = true)
    {
        _fields.Add(new FieldDefinition
        {
            Name = name,
            DataType = dataType,
            Nullable = nullable
        });
        return this;
    }

    public LayerBuilder WithAttachments(
        string storageProfileId,
        bool enabled = true,
        bool exposeOgcLinks = true,
        int? maxSizeMiB = null)
    {
        _attachments = new LayerAttachmentDefinition
        {
            Enabled = enabled,
            StorageProfileId = storageProfileId,
            ExposeOgcLinks = exposeOgcLinks,
            MaxSizeMiB = maxSizeMiB
        };
        return this;
    }

    public LayerBuilder WithTemporal(
        string startField,
        string? endField = null,
        bool enabled = true)
    {
        _temporal = new LayerTemporalDefinition
        {
            Enabled = enabled,
            StartField = startField,
            EndField = endField
        };
        return this;
    }

    public LayerDefinition Build()
    {
        return new LayerDefinition
        {
            Id = _id,
            ServiceId = _serviceId,
            Title = _title,
            Description = _description,
            GeometryType = _geometryType,
            IdField = _idField,
            GeometryField = _geometryField,
            Fields = _fields,
            Attachments = _attachments,
            Temporal = _temporal
        };
    }
}

/// <summary>
/// Builder for creating RasterDatasetDefinition instances with a fluent API.
/// </summary>
public sealed class RasterDatasetBuilder
{
    private readonly string _id;
    private readonly string _uri;
    private string _title;
    private string? _description;
    private string? _serviceId;
    private string? _layerId;
    private List<string> _keywords = new();
    private List<string> _crs = new() { "EPSG:4326" };
    private string _sourceType = "gdal";
    private string _mediaType = "image/tiff";

    public RasterDatasetBuilder(string id, string uri)
    {
        _id = id;
        _uri = uri;
        _title = id;
    }

    public RasterDatasetBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public RasterDatasetBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public RasterDatasetBuilder WithServiceId(string? serviceId)
    {
        _serviceId = serviceId;
        return this;
    }

    public RasterDatasetBuilder WithLayerId(string? layerId)
    {
        _layerId = layerId;
        return this;
    }

    public RasterDatasetBuilder WithKeywords(params string[] keywords)
    {
        _keywords.AddRange(keywords);
        return this;
    }

    public RasterDatasetBuilder WithCrs(params string[] crs)
    {
        _crs = new List<string>(crs);
        return this;
    }

    public RasterDatasetBuilder WithSourceType(string sourceType)
    {
        _sourceType = sourceType;
        return this;
    }

    public RasterDatasetBuilder WithMediaType(string mediaType)
    {
        _mediaType = mediaType;
        return this;
    }

    public RasterDatasetDefinition Build()
    {
        return new RasterDatasetDefinition
        {
            Id = _id,
            Title = _title,
            Description = _description,
            ServiceId = _serviceId,
            LayerId = _layerId,
            Keywords = _keywords,
            Crs = _crs,
            Source = new RasterSourceDefinition
            {
                Type = _sourceType,
                Uri = _uri,
                MediaType = _mediaType
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Crs = "EPSG:4326"
            }
        };
    }
}
