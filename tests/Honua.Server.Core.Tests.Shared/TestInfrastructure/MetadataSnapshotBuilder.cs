using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Factory for creating MetadataSnapshot instances in tests with sensible defaults.
/// Provides a fluent API to build test metadata with minimal boilerplate.
/// Copied from Honua.Server.Host test utilities to allow reuse in core tests.
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
        _catalog = new CatalogDefinition
        {
            Id = "test-catalog",
            Title = "Test Catalog",
            Description = "Test catalog for unit tests"
        };
        _server = ServerDefinition.Default;
    }

    public MetadataSnapshotBuilder WithCatalog(CatalogDefinition catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        return this;
    }

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

    public MetadataSnapshotBuilder WithServer(ServerDefinition server)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        return this;
    }

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

    public MetadataSnapshotBuilder WithFolder(FolderDefinition folder)
    {
        _folders.Add(folder ?? throw new ArgumentNullException(nameof(folder)));
        return this;
    }

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

    public MetadataSnapshotBuilder WithDataSource(DataSourceDefinition dataSource)
    {
        _dataSources.Add(dataSource ?? throw new ArgumentNullException(nameof(dataSource)));
        return this;
    }

    public MetadataSnapshotBuilder WithService(
        string id,
        string folderId,
        string dataSourceId,
        string? title = null,
        Action<ServiceBuilder>? configure = null)
    {
        var builder = new ServiceBuilder(id, folderId, dataSourceId);
        if (!string.IsNullOrEmpty(title))
        {
            builder.WithTitle(title);
        }

        configure?.Invoke(builder);
        return WithService(builder.Build());
    }

    public MetadataSnapshotBuilder WithService(ServiceDefinition service)
    {
        _services.Add(service ?? throw new ArgumentNullException(nameof(service)));
        return this;
    }

    public MetadataSnapshotBuilder WithLayer(
        string id,
        string serviceId,
        string? title = null,
        Action<LayerBuilder>? configure = null)
    {
        var builder = new LayerBuilder(id, serviceId);
        if (!string.IsNullOrEmpty(title))
        {
            builder.WithTitle(title);
        }

        configure?.Invoke(builder);
        return WithLayer(builder.Build());
    }

    public MetadataSnapshotBuilder WithLayer(LayerDefinition layer)
    {
        _layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
        return this;
    }

    public MetadataSnapshotBuilder WithRasterDataset(RasterDatasetDefinition rasterDataset)
    {
        _rasterDatasets.Add(rasterDataset ?? throw new ArgumentNullException(nameof(rasterDataset)));
        return this;
    }

    public MetadataSnapshotBuilder WithStyle(StyleDefinition style)
    {
        _styles.Add(style ?? throw new ArgumentNullException(nameof(style)));
        return this;
    }

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

    public static MetadataSnapshot CreateEmpty(string catalogId = "test-catalog")
    {
        return new MetadataSnapshotBuilder()
            .WithCatalog(catalogId)
            .Build();
    }
}

public sealed class ServiceBuilder
{
    private readonly string _id;
    private readonly string _folderId;
    private readonly string _dataSourceId;
    private string _title;
    private string _serviceType = "feature";
    private bool _enabled = true;
    private string? _description;
    private readonly List<string> _keywords = new();
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

public sealed class LayerBuilder
{
    private readonly string _id;
    private readonly string _serviceId;
    private string _title;
    private string _geometryType = "Point";
    private string _idField = "objectid";
    private string _geometryField = "shape";
    private string? _description;
    private readonly List<FieldDefinition> _fields = new();
    private LayerAttachmentDefinition _attachments = LayerAttachmentDefinition.Disabled;
    private LayerTemporalDefinition _temporal = LayerTemporalDefinition.Disabled;

    public LayerBuilder(string id, string serviceId)
    {
        _id = id;
        _serviceId = serviceId;
        _title = id;

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
        _fields.Add(field ?? throw new ArgumentNullException(nameof(field)));
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
            GeometryType = _geometryType,
            IdField = _idField,
            GeometryField = _geometryField,
            Description = _description,
            Fields = _fields,
            Attachments = _attachments,
            Temporal = _temporal
        };
    }
}
