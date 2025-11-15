// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.Tests.Builders;

/// <summary>
/// Builder for creating test LayerDefinition instances.
/// </summary>
public class LayerDefinitionBuilder
{
    private string id = "test-layer";
    private string title = "Test Layer";
    private string? description = "Test layer description";
    private string serviceId = "test-service";
    private string? itemType = "feature";
    private List<string> crs = new();
    private List<string> keywords = new();
    private string? defaultStyleId;
    private List<string> styleIds = new();
    private Envelope? extent;
    private double? minScale;
    private double? maxScale;
    private List<LinkDefinition> links = new();
    private string? dataSourceId;
    private string? schema = "public";
    private string? tableName;

    public LayerDefinitionBuilder WithId(string id)
    {
        this.id = id;
        return this;
    }

    public LayerDefinitionBuilder WithTitle(string title)
    {
        this.title = title;
        return this;
    }

    public LayerDefinitionBuilder WithDescription(string? description)
    {
        this.description = description;
        return this;
    }

    public LayerDefinitionBuilder WithServiceId(string serviceId)
    {
        this.serviceId = serviceId;
        return this;
    }

    public LayerDefinitionBuilder WithItemType(string itemType)
    {
        this.itemType = itemType;
        return this;
    }

    public LayerDefinitionBuilder WithCrs(params string[] crs)
    {
        this.crs = new List<string>(crs);
        return this;
    }

    public LayerDefinitionBuilder WithKeywords(params string[] keywords)
    {
        this.keywords = new List<string>(keywords);
        return this;
    }

    public LayerDefinitionBuilder WithDefaultStyleId(string styleId)
    {
        this.defaultStyleId = styleId;
        return this;
    }

    public LayerDefinitionBuilder WithStyleIds(params string[] styleIds)
    {
        this.styleIds = new List<string>(styleIds);
        return this;
    }

    public LayerDefinitionBuilder WithExtent(double minX, double minY, double maxX, double maxY)
    {
        this.extent = new Envelope(minX, maxX, minY, maxY);
        return this;
    }

    public LayerDefinitionBuilder WithMinScale(double minScale)
    {
        this.minScale = minScale;
        return this;
    }

    public LayerDefinitionBuilder WithMaxScale(double maxScale)
    {
        this.maxScale = maxScale;
        return this;
    }

    public LayerDefinitionBuilder WithDataSource(string dataSourceId, string? schema = "public", string? tableName = null)
    {
        this.dataSourceId = dataSourceId;
        this.schema = schema;
        this.tableName = tableName ?? this.id;
        return this;
    }

    public LayerDefinitionBuilder WithLinks(params LinkDefinition[] links)
    {
        this.links = new List<LinkDefinition>(links);
        return this;
    }

    public LayerDefinition Build()
    {
        return new LayerDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            ServiceId = serviceId,
            ItemType = itemType,
            Crs = crs,
            Keywords = keywords,
            DefaultStyleId = defaultStyleId ?? string.Empty,
            StyleIds = styleIds,
            Extent = extent,
            MinScale = minScale,
            MaxScale = maxScale,
            Links = links,
            DataSourceId = dataSourceId ?? "test-datasource",
            Schema = schema ?? "public",
            TableName = tableName ?? id,
            GeometryColumn = "geom",
            GeometryType = "POINT",
            IdField = "id",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", Type = "integer", IsPrimaryKey = true },
                new() { Name = "name", Type = "text" }
            }
        };
    }
}

/// <summary>
/// Builder for creating test ServiceDefinition instances.
/// </summary>
public class ServiceDefinitionBuilder
{
    private string id = "test-service";
    private string title = "Test Service";
    private string? description = "Test service description";
    private string serviceType = "OgcApiFeatures";
    private string? folderId;
    private bool enabled = true;
    private OgcConfiguration ogc = new();
    private List<LayerDefinition> layers = new();
    private List<string> crs = new();

    public ServiceDefinitionBuilder WithId(string id)
    {
        this.id = id;
        return this;
    }

    public ServiceDefinitionBuilder WithTitle(string title)
    {
        this.title = title;
        return this;
    }

    public ServiceDefinitionBuilder WithDescription(string? description)
    {
        this.description = description;
        return this;
    }

    public ServiceDefinitionBuilder WithServiceType(string serviceType)
    {
        this.serviceType = serviceType;
        return this;
    }

    public ServiceDefinitionBuilder WithFolderId(string folderId)
    {
        this.folderId = folderId;
        return this;
    }

    public ServiceDefinitionBuilder WithEnabled(bool enabled)
    {
        this.enabled = enabled;
        return this;
    }

    public ServiceDefinitionBuilder WithOgcConfiguration(OgcConfiguration ogc)
    {
        this.ogc = ogc;
        return this;
    }

    public ServiceDefinitionBuilder WithConformanceClasses(params string[] classes)
    {
        this.ogc = this.ogc with { ConformanceClasses = classes.ToList() };
        return this;
    }

    public ServiceDefinitionBuilder WithCollectionsEnabled(bool enabled)
    {
        this.ogc = this.ogc with { CollectionsEnabled = enabled };
        return this;
    }

    public ServiceDefinitionBuilder WithLayers(params LayerDefinition[] layers)
    {
        this.layers = new List<LayerDefinition>(layers);
        return this;
    }

    public ServiceDefinitionBuilder WithCrs(params string[] crs)
    {
        this.crs = new List<string>(crs);
        return this;
    }

    public ServiceDefinition Build()
    {
        return new ServiceDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            ServiceType = serviceType,
            FolderId = folderId,
            Enabled = enabled,
            Ogc = ogc,
            Layers = layers.AsReadOnly(),
            Crs = crs
        };
    }
}

/// <summary>
/// Builder for creating test MetadataSnapshot instances.
/// </summary>
public class MetadataSnapshotBuilder
{
    private CatalogDefinition catalog = new()
    {
        Id = "test-catalog",
        Title = "Test Catalog",
        Description = "Test catalog description",
        Version = "1.0.0",
        Keywords = new List<string> { "test" },
        Links = new List<LinkDefinition>()
    };

    private List<FolderDefinition> folders = new();
    private List<DataSourceDefinition> dataSources = new()
    {
        new DataSourceDefinition
        {
            Id = "test-datasource",
            Title = "Test Data Source",
            Type = "PostgreSQL",
            ConnectionString = "Server=localhost;Database=test;",
            Schema = "public"
        }
    };
    private List<ServiceDefinition> services = new();
    private List<LayerDefinition> layers = new();
    private List<RasterDatasetDefinition> rasterDatasets = new();
    private List<StyleDefinition> styles = new();
    private List<LayerGroupDefinition> layerGroups = new();
    private ServerDefinition? server;

    public MetadataSnapshotBuilder WithCatalog(CatalogDefinition catalog)
    {
        this.catalog = catalog;
        return this;
    }

    public MetadataSnapshotBuilder WithCatalogId(string id)
    {
        this.catalog = this.catalog with { Id = id };
        return this;
    }

    public MetadataSnapshotBuilder WithCatalogTitle(string title)
    {
        this.catalog = this.catalog with { Title = title };
        return this;
    }

    public MetadataSnapshotBuilder WithCatalogDescription(string description)
    {
        this.catalog = this.catalog with { Description = description };
        return this;
    }

    public MetadataSnapshotBuilder WithFolders(params FolderDefinition[] folders)
    {
        this.folders = new List<FolderDefinition>(folders);
        return this;
    }

    public MetadataSnapshotBuilder WithDataSources(params DataSourceDefinition[] dataSources)
    {
        this.dataSources = new List<DataSourceDefinition>(dataSources);
        return this;
    }

    public MetadataSnapshotBuilder WithServices(params ServiceDefinition[] services)
    {
        this.services = new List<ServiceDefinition>(services);
        return this;
    }

    public MetadataSnapshotBuilder WithLayers(params LayerDefinition[] layers)
    {
        this.layers = new List<LayerDefinition>(layers);
        return this;
    }

    public MetadataSnapshotBuilder WithRasterDatasets(params RasterDatasetDefinition[] rasterDatasets)
    {
        this.rasterDatasets = new List<RasterDatasetDefinition>(rasterDatasets);
        return this;
    }

    public MetadataSnapshotBuilder WithStyles(params StyleDefinition[] styles)
    {
        this.styles = new List<StyleDefinition>(styles);
        return this;
    }

    public MetadataSnapshotBuilder WithLayerGroups(params LayerGroupDefinition[] layerGroups)
    {
        this.layerGroups = new List<LayerGroupDefinition>(layerGroups);
        return this;
    }

    public MetadataSnapshotBuilder WithServer(ServerDefinition server)
    {
        this.server = server;
        return this;
    }

    public MetadataSnapshot Build()
    {
        return new MetadataSnapshot(
            catalog,
            folders,
            dataSources,
            services,
            layers,
            rasterDatasets,
            styles,
            layerGroups,
            server);
    }
}
