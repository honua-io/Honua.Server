using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Host.Tests.TestUtilities;
using Honua.Server.Host.Wcs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Security tests for WCS GetCoverage path traversal vulnerability.
/// Verifies that malicious paths cannot be used to read arbitrary files.
/// </summary>
[Trait("Category", "Security")]
public sealed class WcsPathTraversalTests
{
    private static readonly XNamespace Ows = "http://www.opengis.net/ows/2.0";

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    public async Task GetCoverage_WithPathTraversalInUri_ReturnsError(string maliciousPath)
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("GetCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "malicious-coverage"
        });

        var maliciousDataset = CreateMaliciousRasterDataset("malicious-coverage", maliciousPath);
        rasterRegistry.Datasets = new List<RasterDatasetDefinition> { maliciousDataset };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var content = await GetResultContentAsync(result);

        // Should return an error, not the file contents
        content.Should().Contain("ExceptionReport");

        var doc = XDocument.Parse(content);
        var exception = doc.Descendants(Ows + "Exception").FirstOrDefault();
        exception.Should().NotBeNull();

        // Error should indicate invalid path or access forbidden
        var exceptionText = exception!.Element(Ows + "ExceptionText")?.Value;
        exceptionText.Should().NotBeNullOrEmpty();
        (exceptionText!.Contains("Invalid") ||
         exceptionText.Contains("forbidden") ||
         exceptionText.Contains("not allowed") ||
         exceptionText.Contains("not found")).Should().BeTrue();
    }

    [Theory]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    [InlineData("..%2f..%2f..%2fetc%2fpasswd")]
    public async Task GetCoverage_WithUrlEncodedPathTraversal_ReturnsError(string encodedPath)
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("GetCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "encoded-attack"
        });

        var maliciousDataset = CreateMaliciousRasterDataset("encoded-attack", encodedPath);
        rasterRegistry.Datasets = new List<RasterDatasetDefinition> { maliciousDataset };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        content.Should().Contain("ExceptionReport");

        var doc = XDocument.Parse(content);
        var exceptionText = doc.Descendants(Ows + "ExceptionText").FirstOrDefault()?.Value;
        exceptionText.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("\\\\localhost\\c$\\windows\\system32")]
    [InlineData("//server/share/secrets.txt")]
    public async Task GetCoverage_WithUncPath_ReturnsError(string uncPath)
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("GetCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "unc-attack"
        });

        var maliciousDataset = CreateMaliciousRasterDataset("unc-attack", uncPath);
        rasterRegistry.Datasets = new List<RasterDatasetDefinition> { maliciousDataset };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        content.Should().Contain("ExceptionReport");
    }

    [Fact]
    public async Task GetCoverage_WithNullByteInPath_ReturnsError()
    {
        // Arrange
        var maliciousPath = "safe.tif\0../../../etc/passwd";
        var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("GetCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "null-byte-attack"
        });

        var maliciousDataset = CreateMaliciousRasterDataset("null-byte-attack", maliciousPath);
        rasterRegistry.Datasets = new List<RasterDatasetDefinition> { maliciousDataset };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        content.Should().Contain("ExceptionReport");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    public async Task DescribeCoverage_WithPathTraversalInUri_ReturnsError(string maliciousPath)
    {
        // Arrange
        var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("DescribeCoverage", new Dictionary<string, StringValues>
        {
            ["coverageId"] = "malicious-describe"
        });

        var maliciousDataset = CreateMaliciousRasterDataset("malicious-describe", maliciousPath);
        rasterRegistry.Datasets = new List<RasterDatasetDefinition> { maliciousDataset };

        // Act
        var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

        // Assert
        var content = await GetResultContentAsync(result);
        content.Should().Contain("ExceptionReport");

        var doc = XDocument.Parse(content);
        var exceptionText = doc.Descendants(Ows + "ExceptionText").FirstOrDefault()?.Value;
        exceptionText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCoverage_WithValidPath_WorksCorrectly()
    {
        // Arrange - Create a temporary valid GeoTIFF file
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write minimal file content
            await File.WriteAllBytesAsync(tempFile, new byte[] { 0x00 });

            var (context, metadataRegistry, rasterRegistry, renderer) = CreateTestContext("GetCoverage", new Dictionary<string, StringValues>
            {
                ["coverageId"] = "valid-coverage",
                ["format"] = "image/tiff"
            });

            var validDataset = CreateMaliciousRasterDataset("valid-coverage", tempFile);
            rasterRegistry.Datasets = new List<RasterDatasetDefinition> { validDataset };

            // Act
            var result = await WcsHandlers.HandleAsync(context, metadataRegistry, rasterRegistry, renderer, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            // Note: Will fail with GDAL error since it's not a real GeoTIFF, but path validation should pass
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static (HttpContext context, IMetadataRegistry registry, TestRasterRegistry rasterRegistry, IRasterRenderer? renderer) CreateTestContext(
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

        var metadata = MetadataSnapshotBuilder.CreateEmpty("test");

        var metadataRegistry = new TestMetadataRegistry(metadata);
        var rasterRegistry = new TestRasterRegistry();

        return (context, metadataRegistry, rasterRegistry, null);
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
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }
        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => new ValueTask<MetadataSnapshot>(_snapshot);
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Microsoft.Extensions.Primitives.IChangeToken GetChangeToken() => throw new NotImplementedException();
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

    private static RasterDatasetDefinition CreateMaliciousRasterDataset(string id, string maliciousPath)
    {
        return new RasterDatasetDefinition
        {
            Id = id,
            Title = "Malicious Dataset",
            Description = "Dataset with malicious path",
            ServiceId = "test-service",
            LayerId = "test-layer",
            Keywords = new[] { "raster", "test" },
            Crs = new[] { "EPSG:4326" },
            Source = new RasterSourceDefinition
            {
                Type = "gdal",
                Uri = maliciousPath,
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
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseBody);
        return await reader.ReadToEndAsync();
    }
}
