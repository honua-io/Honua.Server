using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Host.Wmts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Wmts;

/// <summary>
/// Unit tests for WMTS 1.0.0 (Web Map Tile Service) implementation.
/// Tests conformance to OGC WMTS specification.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WmtsTests
{
    private static readonly XNamespace Wmts = "http://www.opengis.net/wmts/1.0";
    private static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";

    [Fact]
    public async Task GetCapabilities_ReturnsValidResponse()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var content = await GetResultContentAsync(result);
        content.Should().Contain("Capabilities");
        content.Should().Contain("ServiceIdentification");
        content.Should().Contain("OperationsMetadata");

        var doc = XDocument.Parse(content);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Wmts + "Capabilities");
        doc.Root.Attribute("version")?.Value.Should().Be("1.0.0");
    }

    [Fact]
    public async Task GetCapabilities_IncludesAllOperations()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var operations = doc.Descendants(Ows + "Operation")
            .Select(op => op.Attribute("name")?.Value)
            .ToList();

        operations.Should().Contain("GetCapabilities");
        operations.Should().Contain("GetTile");
        operations.Should().Contain("GetFeatureInfo");
    }

    [Fact]
    public async Task GetCapabilities_ListsLayers()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>
        {
            CreateMockRasterDataset("layer1", "Layer 1"),
            CreateMockRasterDataset("layer2", "Layer 2")
        };

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var layers = doc.Descendants(Wmts + "Layer").ToList();
        layers.Should().HaveCount(2);

        var layerIds = layers
            .Select(l => l.Element(Ows + "Identifier")?.Value)
            .ToList();
        layerIds.Should().Contain("layer1");
        layerIds.Should().Contain("layer2");
    }

    [Fact]
    public async Task GetCapabilities_IncludesTileMatrixSets()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>
        {
            CreateMockRasterDataset("test-layer", "Test Layer")
        };

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var tileMatrixSets = doc.Descendants(Wmts + "TileMatrixSet")
            .Select(tms => tms.Element(Ows + "Identifier")?.Value)
            .ToList();

        tileMatrixSets.Should().Contain("WorldCRS84Quad");
        tileMatrixSets.Should().Contain("WorldWebMercatorQuad");
    }

    [Fact]
    public async Task GetCapabilities_IncludesConformanceClasses()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var profiles = doc.Descendants(Ows + "ServiceIdentification")
            .Elements(Ows + "Profile")
            .Select(p => p.Value)
            .ToList();

        // Verify core WMTS conformance classes
        profiles.Should().Contain("http://www.opengis.net/spec/wmts/1.0/conf/core");
        profiles.Should().Contain("http://www.opengis.net/spec/wmts/1.0/conf/getcapabilities");
        profiles.Should().Contain("http://www.opengis.net/spec/wmts/1.0/conf/gettile");
        profiles.Should().Contain("http://www.opengis.net/spec/wmts/1.0/conf/kvp");
        profiles.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetTile_WithMissingLayer_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetTile");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task GetTile_WithInvalidLayer_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetTile", new Dictionary<string, StringValues>
        {
            ["layer"] = "non-existent",
            ["tilematrixset"] = "WorldCRS84Quad",
            ["tilematrix"] = "0",
            ["tilerow"] = "0",
            ["tilecol"] = "0"
        });

        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("LayerNotDefined");
    }

    [Fact]
    public async Task GetTile_WithInvalidTileMatrixSet_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetTile", new Dictionary<string, StringValues>
        {
            ["layer"] = "test-layer",
            ["tilematrixset"] = "InvalidMatrixSet",
            ["tilematrix"] = "0",
            ["tilerow"] = "0",
            ["tilecol"] = "0"
        });

        rasterRegistry.Datasets = new List<RasterDatasetDefinition>
        {
            CreateMockRasterDataset("test-layer", "Test Layer")
        };

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    [Fact]
    public async Task GetTile_WithOutOfRangeCoordinates_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetTile", new Dictionary<string, StringValues>
        {
            ["layer"] = "test-layer",
            ["tilematrixset"] = "WorldCRS84Quad",
            ["tilematrix"] = "0",
            ["tilerow"] = "999",
            ["tilecol"] = "999"
        });

        rasterRegistry.Datasets = new List<RasterDatasetDefinition>
        {
            CreateMockRasterDataset("test-layer", "Test Layer")
        };

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("TileOutOfRange");
    }

    [Fact]
    public async Task GetFeatureInfo_RequiresParameters()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetFeatureInfo");

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert - GetFeatureInfo requires layer, tilematrixset, tilematrix, tilerow, tilecol, i, j parameters
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task InvalidServiceParameter_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("GetCapabilities");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "WMS",
            ["request"] = "GetCapabilities"
        });

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    [Fact]
    public async Task MissingRequestParameter_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "WMTS"
        });

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task UnsupportedRequest_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository) = CreateTestContext("UnsupportedOperation");

        // Act
        var result = await WmtsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, cache, featureRepository, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    private static (HttpContext context, IMetadataRegistry registry, TestRasterRegistry rasterRegistry, IRasterRenderer? renderer, IRasterTileCacheProvider? cache, Core.Data.IFeatureRepository featureRepository) CreateTestContext(
        string requestType,
        Dictionary<string, StringValues>? additionalParams = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.PathBase = "";

        var queryParams = new Dictionary<string, StringValues>
        {
            ["service"] = "WMTS",
            ["request"] = requestType
        };

        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                queryParams[param.Key] = param.Value;
            }
        }

        context.Request.Query = new QueryCollection(queryParams);

        var metadata = new MetadataSnapshot(
            new CatalogDefinition { Id = "test", Title = "Test Catalog", Description = "Test" },
            Array.Empty<FolderDefinition>(),
            Array.Empty<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            Array.Empty<LayerGroupDefinition>(),
            new ServerDefinition()
        );

        var metadataRegistry = new TestMetadataRegistry(metadata);
        var rasterRegistry = new TestRasterRegistry();
        var featureRepository = new TestFeatureRepository();

        return (context, metadataRegistry, rasterRegistry, null, null, featureRepository);
    }

    private sealed class TestMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public TestMetadataRegistry(MetadataSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public MetadataSnapshot Snapshot => _snapshot;
        public bool IsInitialized => true;
        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => new ValueTask<MetadataSnapshot>(_snapshot);
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Microsoft.Extensions.Primitives.IChangeToken GetChangeToken() => throw new NotImplementedException();
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }
    }

    private sealed class TestRasterRegistry : IRasterDatasetRegistry
    {
        public List<RasterDatasetDefinition> Datasets { get; set; } = new();

        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
            new ValueTask<IReadOnlyList<RasterDatasetDefinition>>(Datasets);

        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, CancellationToken cancellationToken = default) =>
            new ValueTask<IReadOnlyList<RasterDatasetDefinition>>(Datasets.Where(d => d.ServiceId == serviceId).ToList());

        public ValueTask<RasterDatasetDefinition?> FindAsync(string datasetId, CancellationToken cancellationToken = default) =>
            new ValueTask<RasterDatasetDefinition?>(Datasets.FirstOrDefault(d => d.Id == datasetId));
    }

    private sealed class TestFeatureRepository : Core.Data.IFeatureRepository
    {
        public async IAsyncEnumerable<Core.Data.FeatureRecord> QueryAsync(string serviceId, string layerId, Core.Data.FeatureQuery? query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<long> CountAsync(string serviceId, string layerId, Core.Data.FeatureQuery? query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<Core.Data.FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, Core.Data.FeatureQuery? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<Core.Data.FeatureRecord?>(null);

        public Task<Core.Data.FeatureRecord> CreateAsync(string serviceId, string layerId, Core.Data.FeatureRecord record, Core.Data.IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Core.Data.FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, Core.Data.FeatureRecord record, Core.Data.IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, Core.Data.IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<IReadOnlyList<Core.Data.StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<Core.Data.StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, Core.Data.FeatureQuery? filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Core.Data.StatisticsResult>>(Array.Empty<Core.Data.StatisticsResult>());

        public Task<IReadOnlyList<Core.Data.DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, Core.Data.FeatureQuery? filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Core.Data.DistinctResult>>(Array.Empty<Core.Data.DistinctResult>());

        public Task<Core.Data.BoundingBox?> QueryExtentAsync(string serviceId, string layerId, Core.Data.FeatureQuery? filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<Core.Data.BoundingBox?>(null);
    }

    private static RasterDatasetDefinition CreateMockRasterDataset(string id, string title)
    {
        return new RasterDatasetDefinition
        {
            Id = id,
            Title = title,
            Description = $"Description for {title}",
            ServiceId = "test-service",
            LayerId = "test-layer",
            Keywords = new[] { "raster", "test" },
            Crs = new[] { "EPSG:4326", "EPSG:3857" },
            Source = new RasterSourceDefinition
            {
                Type = "gdal",
                Uri = "/path/to/raster.tif",
                MediaType = "image/tiff"
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Crs = "EPSG:4326"
            },
            Cache = new RasterCacheDefinition
            {
                Enabled = true,
                ZoomLevels = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }
            },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "default",
                StyleIds = new[] { "default" }
            }
        };
    }

    private static async Task<string> GetResultContentAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext();
        context.RequestServices = serviceProvider;
        var responseBody = new System.IO.MemoryStream();
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        responseBody.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(responseBody);
        return await reader.ReadToEndAsync();
    }
}
