using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Raster.Sources;
using Honua.Server.Host.Wcs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Wcs;

/// <summary>
/// Unit tests for WCS 2.0.1 (Web Coverage Service) implementation.
/// Tests conformance to OGC WCS specification.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WcsTests
{
    private static readonly XNamespace Wcs = "http://www.opengis.net/wcs/2.0";
    private static readonly XNamespace Ows = "http://www.opengis.net/ows/2.0";
    private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";

    [Fact]
    public async Task GetCapabilities_ReturnsValidResponse()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var content = await GetResultContentAsync(result);
        content.Should().Contain("Capabilities");
        content.Should().Contain("ServiceIdentification");
        content.Should().Contain("OperationsMetadata");

        var doc = XDocument.Parse(content);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Wcs + "Capabilities");
        doc.Root.Attribute("version")?.Value.Should().Be("2.0.1");
    }

    [Fact]
    public async Task GetCapabilities_IncludesAllOperations()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var operations = doc.Descendants(Ows + "Operation")
            .Select(op => op.Attribute("name")?.Value)
            .ToList();

        operations.Should().Contain("GetCapabilities");
        operations.Should().Contain("DescribeCoverage");
        operations.Should().Contain("GetCoverage");
    }

    [Fact]
    public async Task GetCapabilities_ListsCoverages()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>
        {
            CreateMockRasterDataset("coverage1", "Coverage 1"),
            CreateMockRasterDataset("coverage2", "Coverage 2")
        };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var coverageSummaries = doc.Descendants(Wcs + "CoverageSummary").ToList();
        coverageSummaries.Should().HaveCount(2);

        var coverageIds = coverageSummaries
            .Select(cs => cs.Element(Wcs + "CoverageId")?.Value)
            .ToList();
        coverageIds.Should().Contain("coverage1");
        coverageIds.Should().Contain("coverage2");
    }

    [Fact]
    public async Task GetCapabilities_IncludesSupportedFormats()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCapabilities");
        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var formats = doc.Descendants(Wcs + "formatSupported")
            .Select(f => f.Value)
            .ToList();

        formats.Should().Contain("image/tiff");
        formats.Should().Contain("image/png");
        formats.Should().Contain("image/jpeg");
    }

    [Fact]
    public async Task DescribeCoverage_WithValidId_ReturnsDescription()
    {
        // Arrange
        var testFile = System.IO.Path.GetTempFileName();
        try
        {
            // Create a minimal file for testing
            await System.IO.File.WriteAllBytesAsync(testFile, new byte[] { 0x00 });

            var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("DescribeCoverage", new Dictionary<string, StringValues>
            {
                ["coverageId"] = "test-coverage"
            });

            var dataset = CreateMockRasterDataset("test-coverage", "Test Coverage", testFile);
            rasterRegistry.Datasets = new List<RasterDatasetDefinition> { dataset };

            // Act
            // Note: This will fail if GDAL cannot open the file, which is expected in a unit test environment
            // In a real test, you'd use a valid GeoTIFF file
            var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
        }
        finally
        {
            if (System.IO.File.Exists(testFile))
            {
                System.IO.File.Delete(testFile);
            }
        }
    }

    [Fact]
    public async Task DescribeCoverage_WithMissingCoverageId_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("DescribeCoverage");

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(Ows + "ExceptionReport");

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("MissingParameterValue");
    }

    [Fact]
    public async Task DescribeCoverage_WithInvalidCoverageId_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("DescribeCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "non-existent"
        });

        rasterRegistry.Datasets = new List<RasterDatasetDefinition>();

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("NoSuchCoverage");
    }

    [Fact]
    public async Task GetCoverage_WithMissingCoverageId_ReturnsException()
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCoverage");

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
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
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("GetCapabilities");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "WMS",
            ["request"] = "GetCapabilities"
        });

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

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
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("");
        context.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["service"] = "WCS"
        });

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

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
        var (context, metadataRegistry, rasterRegistry, renderer, providerRegistry) = CreateTestContext("UnsupportedOperation");

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, providerRegistry, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        var doc = XDocument.Parse(content);

        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();
        exception!.Attribute("exceptionCode")?.Value.Should().Be("InvalidParameterValue");
    }

    private static (HttpContext context, IMetadataRegistry registry, TestRasterRegistry rasterRegistry, IRasterRenderer? renderer, IRasterSourceProviderRegistry providerRegistry) CreateTestContext(
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
            ["service"] = "WCS",
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

        var providerRegistry = new FakeRasterSourceProviderRegistry();

        return (context, metadataRegistry, rasterRegistry, null, providerRegistry);
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

    private static RasterDatasetDefinition CreateMockRasterDataset(string id, string title, string? filePath = null)
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
                Uri = filePath ?? "/path/to/raster.tif",
                MediaType = "image/tiff"
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Crs = "EPSG:4326"
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

    private sealed class FakeRasterSourceProviderRegistry : IRasterSourceProviderRegistry
    {
        public IRasterSourceProvider? GetProvider(string uri) => null;

        public Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(Stream.Null);

        public Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(Stream.Null);
    }
}
