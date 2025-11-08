// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

/// <summary>
/// Metadata provider that loads configuration from JSON files.
/// Orchestrates specialized services for loading, parsing, and building metadata definitions.
/// </summary>
public sealed class JsonMetadataProvider : IMetadataProvider, IDisposable
{
    private readonly string _metadataPath;
    private readonly FileSystemWatcher? _watcher;
    private readonly bool _watchForChanges;
    private readonly JsonMetadataLoader _loader;
    private readonly MetadataSchemaParser _schemaParser;
    private readonly LayerConfigurationBuilder _layerBuilder;
    private readonly RasterConfigurationBuilder _rasterBuilder;

    public bool SupportsChangeNotifications => _watchForChanges;
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    public JsonMetadataProvider(string metadataPath, bool watchForChanges = false)
    {
        if (metadataPath.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Metadata path must be provided", nameof(metadataPath));
        }

        _metadataPath = Path.GetFullPath(metadataPath);
        _watchForChanges = watchForChanges;

        // Initialize specialized services
        _loader = new JsonMetadataLoader();
        _schemaParser = new MetadataSchemaParser();
        _layerBuilder = new LayerConfigurationBuilder(_schemaParser);
        _rasterBuilder = new RasterConfigurationBuilder(_layerBuilder);

        if (_watchForChanges && File.Exists(_metadataPath))
        {
            var directory = Path.GetDirectoryName(_metadataPath);
            var fileName = Path.GetFileName(_metadataPath);

            if (!directory.IsNullOrEmpty() && !fileName.IsNullOrEmpty())
            {
                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Renamed += OnFileChanged;
                _watcher.EnableRaisingEvents = true;
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("file-watcher"));
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileChanged;
            _watcher.Dispose();
        }
    }

    /// <summary>
    /// Loads metadata from the configured file path.
    /// </summary>
    public async Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var document = await _loader.LoadFromFileAsync(_metadataPath, cancellationToken).ConfigureAwait(false);
        return CreateSnapshot(document);
    }

    /// <summary>
    /// Parses metadata from a JSON string.
    /// </summary>
    public static MetadataSnapshot Parse(string json)
    {
        var loader = new JsonMetadataLoader();
        var document = loader.ParseFromString(json);
        return CreateSnapshot(document);
    }

    /// <summary>
    /// Creates a metadata snapshot from a metadata document.
    /// Orchestrates validation and building of all metadata definitions.
    /// </summary>
    private static MetadataSnapshot CreateSnapshot(MetadataDocument document)
    {
        // Validate the document structure and references
        MetadataValidator.Validate(document);

        // Initialize specialized builders
        var schemaParser = new MetadataSchemaParser();
        var layerBuilder = new LayerConfigurationBuilder(schemaParser);
        var rasterBuilder = new RasterConfigurationBuilder(layerBuilder);

        // Build all metadata definitions using specialized services
        var catalog = schemaParser.BuildCatalog(document.Catalog!);
        var folders = schemaParser.BuildFolders(document.Folders);
        var dataSources = schemaParser.BuildDataSources(document.DataSources);
        var services = schemaParser.BuildServices(document.Services);
        var styles = schemaParser.BuildStyles(document.Styles);
        var layers = layerBuilder.BuildLayers(document.Layers);
        var rasterDatasets = rasterBuilder.BuildRasterDatasets(document.RasterDatasets);
        var server = schemaParser.BuildServer(document.Server);

        return new MetadataSnapshot(catalog, folders, dataSources, services, layers, rasterDatasets, styles, server);
    }
}

// Document classes remain in this file for backwards compatibility and to keep
// the deserialization model close to the provider

internal sealed class MetadataDocument
{
    public ServerDocument? Server { get; set; }
    public CatalogDocument? Catalog { get; set; }
    public List<FolderDocument>? Folders { get; set; }
    public List<ServiceDocument>? Services { get; set; }
    public List<LayerDocument>? Layers { get; set; }
    public List<DataSourceDocument>? DataSources { get; set; }
    public List<RasterDatasetDocument>? RasterDatasets { get; set; }
    public List<StyleDocument>? Styles { get; set; }
}

internal sealed class ServerDocument
{
    public List<string>? AllowedHosts { get; set; }
    public CorsDocument? Cors { get; set; }
    public ServerSecurityDocument? Security { get; set; }
    public RbacDocument? Rbac { get; set; }
}

internal sealed class CorsDocument
{
    public bool? Enabled { get; set; }
    public List<string>? AllowedOrigins { get; set; }
    public List<string>? AllowedMethods { get; set; }
    public List<string>? AllowedHeaders { get; set; }
    public List<string>? ExposedHeaders { get; set; }
    public bool? AllowCredentials { get; set; }
    public int? MaxAgeSeconds { get; set; }
}

internal sealed class ServerSecurityDocument
{
    public List<string>? AllowedRasterDirectories { get; set; }
}

internal sealed class RbacDocument
{
    public bool? Enabled { get; set; }
    public List<RbacRoleDocument>? Roles { get; set; }
}

internal sealed class RbacRoleDocument
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public List<string>? Permissions { get; set; }
}

internal sealed class CatalogDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? ThemeCategories { get; set; }
    public List<LinkDocument>? Links { get; set; }
    public CatalogContactDocument? Contact { get; set; }
    public CatalogLicenseDocument? License { get; set; }
    public CatalogExtentDocument? Extents { get; set; }
}

internal sealed class CatalogContactDocument
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public string? Phone { get; set; }
    public string? Url { get; set; }
    public string? Role { get; set; }
}

internal sealed class CatalogLicenseDocument
{
    public string? Name { get; set; }
    public string? Url { get; set; }
}

internal sealed class CatalogExtentDocument
{
    public CatalogSpatialExtentDocument? Spatial { get; set; }
    public CatalogTemporalCollectionDocument? Temporal { get; set; }
}

internal sealed class CatalogSpatialExtentDocument
{
    public List<double[]>? Bbox { get; set; }
    public string? Crs { get; set; }
}

internal sealed class CatalogTemporalCollectionDocument
{
    public List<string?[]>? Interval { get; set; }
    public string? Trs { get; set; }
}

internal sealed class CatalogEntryDocument
{
    public string? Summary { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? Themes { get; set; }
    public List<CatalogContactDocument>? Contacts { get; set; }
    public List<LinkDocument>? Links { get; set; }
    public string? Thumbnail { get; set; }
    public int? Ordering { get; set; }
    public CatalogSpatialExtentDocument? SpatialExtent { get; set; }
    public CatalogTemporalExtentDocument? TemporalExtent { get; set; }
}

internal sealed class CatalogTemporalExtentDocument
{
    public string? Start { get; set; }
    public string? End { get; set; }
}

internal sealed class FolderDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public int? Order { get; set; }
}

internal sealed class ServiceDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? FolderId { get; set; }
    public string? ServiceType { get; set; }
    public string? DataSourceId { get; set; }
    public bool? Enabled { get; set; }
    public string? Description { get; set; }
    public List<string>? Keywords { get; set; }
    public List<LinkDocument>? Links { get; set; }
    public CatalogEntryDocument? Catalog { get; set; }
    public OgcServiceDocument? Ogc { get; set; }
}

internal sealed class OgcServiceDocument
{
    public bool? CollectionsEnabled { get; set; }
    public int? ItemLimit { get; set; }
    public string? DefaultCrs { get; set; }
    public List<string>? AdditionalCrs { get; set; }
    public List<string>? ConformanceClasses { get; set; }
}

internal sealed class LayerDocument
{
    public string? Id { get; set; }
    public string? ServiceId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? GeometryType { get; set; }
    public string? IdField { get; set; }
    public string? DisplayField { get; set; }
    public string? GeometryField { get; set; }
    public List<string>? Crs { get; set; }
    public List<string>? Keywords { get; set; }
    public List<LinkDocument>? Links { get; set; }
    public CatalogEntryDocument? Catalog { get; set; }
    public LayerExtentDocument? Extent { get; set; }
    public LayerQueryDocument? Query { get; set; }
    public LayerStorageDocument? Storage { get; set; }
    public List<FieldDocument>? Fields { get; set; }
    public string? ItemType { get; set; }
    public LayerStyleReferenceDocument? Styles { get; set; }
    public LayerEditingDocument? Editing { get; set; }
    public LayerAttachmentDocument? Attachments { get; set; }
    public List<LayerRelationshipDocument>? Relationships { get; set; }
    public double? MinScale { get; set; }
    public double? MaxScale { get; set; }
}

internal sealed class LayerAttachmentDocument
{
    public bool? Enabled { get; set; }
    public string? StorageProfileId { get; set; }
    public int? MaxSizeMiB { get; set; }
    public List<string>? AllowedContentTypes { get; set; }
    public List<string>? DisallowedContentTypes { get; set; }
    public bool? RequireGlobalIds { get; set; }
    public bool? ReturnPresignedUrls { get; set; }
    public bool? ExposeOgcLinks { get; set; }
}

internal sealed class LayerRelationshipDocument
{
    public int? Id { get; set; }
    public string? Role { get; set; }
    public string? Cardinality { get; set; }
    public string? RelatedLayerId { get; set; }
    public string? RelatedTableId { get; set; }
    public string? KeyField { get; set; }
    public string? RelatedKeyField { get; set; }
    public bool? Composite { get; set; }
    public bool? ReturnGeometry { get; set; }
    public string? Semantics { get; set; }
}

internal sealed class RasterDatasetDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ServiceId { get; set; }
    public string? LayerId { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? Crs { get; set; }
    public CatalogEntryDocument? Catalog { get; set; }
    public LayerExtentDocument? Extent { get; set; }
    public RasterSourceDocument? Source { get; set; }
    public RasterStyleDocument? Styles { get; set; }
    public RasterCacheDocument? Cache { get; set; }
}

internal sealed class RasterSourceDocument
{
    public string? Type { get; set; }
    public string? Uri { get; set; }
    public string? MediaType { get; set; }
    public string? CredentialsId { get; set; }
    public bool? DisableHttpRangeRequests { get; set; }
}

internal sealed class RasterStyleDocument
{
    public string? DefaultStyleId { get; set; }
    public List<string>? StyleIds { get; set; }
}

internal sealed class RasterCacheDocument
{
    public bool? Enabled { get; set; }
    public bool? Preseed { get; set; }
    public List<int>? ZoomLevels { get; set; }
}

internal sealed class LayerStyleReferenceDocument
{
    public string? DefaultStyleId { get; set; }
    public List<string>? StyleIds { get; set; }
}

internal sealed class StyleDocument
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Renderer { get; set; }
    public string? Format { get; set; }
    public string? GeometryType { get; set; }
    public List<StyleRuleDocument>? Rules { get; set; }
    public SimpleStyleDocument? Simple { get; set; }
    public UniqueValueStyleDocument? UniqueValue { get; set; }
}

internal sealed class StyleRuleDocument
{
    public string? Id { get; set; }
    public bool? Default { get; set; }
    public string? Label { get; set; }
    public double? MinScale { get; set; }
    public double? MaxScale { get; set; }
    public StyleRuleFilterDocument? Filter { get; set; }
    public SimpleStyleDocument? Symbolizer { get; set; }
}

internal sealed class StyleRuleFilterDocument
{
    public string? Field { get; set; }
    public string? Value { get; set; }
}

internal sealed class SimpleStyleDocument
{
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? SymbolType { get; set; }
    public string? FillColor { get; set; }
    public string? StrokeColor { get; set; }
    public double? StrokeWidth { get; set; }
    public string? StrokeStyle { get; set; }
    public string? IconHref { get; set; }
    public double? Size { get; set; }
    public double? Opacity { get; set; }
}

internal sealed class UniqueValueStyleDocument
{
    public string? Field { get; set; }
    public SimpleStyleDocument? DefaultSymbol { get; set; }
    public List<UniqueValueStyleClassDocument>? Classes { get; set; }
}

internal sealed class UniqueValueStyleClassDocument
{
    public string? Value { get; set; }
    public SimpleStyleDocument? Symbol { get; set; }
}

internal sealed class LayerExtentDocument
{
    public List<double[]>? Bbox { get; set; }
    public string? Crs { get; set; }
    public LayerTemporalExtentDocument? Temporal { get; set; }
}

internal sealed class LayerTemporalExtentDocument
{
    public List<string?[]>? Interval { get; set; }
    public string? Trs { get; set; }
}

internal sealed class LayerQueryDocument
{
    public int? MaxRecordCount { get; set; }
    public List<string>? SupportedParameters { get; set; }
    public LayerQueryFilterDocument? AutoFilter { get; set; }
}

internal sealed class LayerQueryFilterDocument
{
    public string? Cql { get; set; }
}

internal sealed class LayerEditingDocument
{
    public LayerEditCapabilitiesDocument? Capabilities { get; set; }
    public LayerEditConstraintsDocument? Constraints { get; set; }
}

internal sealed class LayerEditCapabilitiesDocument
{
    public bool? AllowAdd { get; set; }
    public bool? AllowUpdate { get; set; }
    public bool? AllowDelete { get; set; }
    public bool? RequireAuthentication { get; set; }
    public List<string>? AllowedRoles { get; set; }
}

internal sealed class LayerEditConstraintsDocument
{
    public List<string>? ImmutableFields { get; set; }
    public List<string>? RequiredFields { get; set; }
    public Dictionary<string, string?>? DefaultValues { get; set; }
}

internal sealed class LayerStorageDocument
{
    public string? Table { get; set; }
    public string? GeometryColumn { get; set; }
    public string? PrimaryKey { get; set; }
    public string? TemporalColumn { get; set; }
    public int? Srid { get; set; }
    public string? Crs { get; set; }
}

internal sealed class FieldDocument
{
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public string? Type { get; set; }
    public string? StorageType { get; set; }
    public bool? Nullable { get; set; }
    public bool? Editable { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
}

internal sealed class LinkDocument
{
    public string? Href { get; set; }
    public string? Rel { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
}

internal sealed class DataSourceDocument
{
    public string? Id { get; set; }
    public string? Provider { get; set; }
    public string? ConnectionString { get; set; }
}
