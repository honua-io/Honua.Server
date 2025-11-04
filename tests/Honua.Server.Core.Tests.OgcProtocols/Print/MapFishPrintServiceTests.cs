using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Print.MapFish;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Print.MapFish;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Styling;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Print;

[Trait("Category", "Unit")]
public class MapFishPrintServiceTests
{
    private static readonly byte[] PngPixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=");

    [Fact]
    public async Task CreateReport_WithBoundingBox_ReturnsPdfStream()
    {
        // Arrange
        var (service, application) = BuildService();
        var spec = new MapFishPrintSpec
        {
            Layout = application.DefaultLayout,
            OutputFormat = "pdf",
            Attributes = new MapFishPrintSpecAttributes
            {
                Title = "Test Map",
                Map = new MapFishPrintMapSpec
                {
                    BoundingBox = new[] { 0d, 0d, 1000d, 1000d },
                    Projection = "EPSG:3857",
                    Dpi = 150,
                    Layers = new List<MapFishPrintLayerSpec>
                    {
                        new()
                        {
                            Type = "wms",
                            Layers = new List<string> { "test-imagery" }
                        }
                    }
                }
            }
        };

        // Act
        var result = await service.CreateReportAsync(application.Id, spec, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().EndWith(".pdf");
        using var memory = new MemoryStream();
        await result.Content.CopyToAsync(memory);
        memory.Length.Should().BeGreaterThan(0);
        result.ScaleDenominator.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReport_WithCenterAndScale_ComputesBoundingBox()
    {
        // Arrange
        var (service, application, capturedRequestAccessor) = BuildServiceWithRendererCapture();
        var layout = application.Layouts.First(l => l.Name == application.DefaultLayout);
        const double scale = 10_000d;
        const int dpi = 150;
        var spec = new MapFishPrintSpec
        {
            Layout = application.DefaultLayout,
            OutputFormat = "pdf",
            Attributes = new MapFishPrintSpecAttributes
            {
                Map = new MapFishPrintMapSpec
                {
                    Center = new[] { 5000d, 5000d },
                    Scale = scale,
                    Dpi = dpi,
                    Projection = "EPSG:3857",
                    Layers = new List<MapFishPrintLayerSpec>
                    {
                        new()
                        {
                            Layers = new List<string> { "test-imagery" }
                        }
                    }
                }
            }
        };

        // Act
        await service.CreateReportAsync(application.Id, spec, CancellationToken.None);

        // Assert
        var request = capturedRequestAccessor();

        request.Should().NotBeNull();
        request!.BoundingBox.Should().NotBeNull();
        request.BoundingBox.Length.Should().Be(4);

        var expectedHalfWidth = layout.Map.WidthPixels / (double)dpi * 0.0254 * scale / 2d;
        var expectedHalfHeight = layout.Map.HeightPixels / (double)dpi * 0.0254 * scale / 2d;
        request.BoundingBox[0].Should().BeApproximately(5000d - expectedHalfWidth, 1e-3);
        request.BoundingBox[1].Should().BeApproximately(5000d - expectedHalfHeight, 1e-3);
        request.BoundingBox[2].Should().BeApproximately(5000d + expectedHalfWidth, 1e-3);
        request.BoundingBox[3].Should().BeApproximately(5000d + expectedHalfHeight, 1e-3);
    }

    private static (MapFishPrintService Service, MapFishPrintApplicationDefinition Application) BuildService()
    {
        var metadataRegistryMock = CreateMetadataRegistry();
        var datasetRegistryMock = CreateDatasetRegistry();
        var applicationStoreMock = CreateApplicationStore();
        var rendererMock = CreateRendererMock();

        var service = new MapFishPrintService(
            applicationStoreMock.Object,
            metadataRegistryMock.Object,
            datasetRegistryMock.Object,
            rendererMock.Object,
            NullLogger<MapFishPrintService>.Instance);

        return (service, MapFishPrintDefaults.Create()[0]);
    }

    private static (MapFishPrintService Service, MapFishPrintApplicationDefinition Application, Func<RasterRenderRequest?> RequestAccessor) BuildServiceWithRendererCapture()
    {
        var metadataRegistryMock = CreateMetadataRegistry();
        var datasetRegistryMock = CreateDatasetRegistry();
        var applicationStoreMock = CreateApplicationStore();
        RasterRenderRequest? captured = null;
        var rendererMock = CreateRendererMock(request => captured = request);

        var service = new MapFishPrintService(
            applicationStoreMock.Object,
            metadataRegistryMock.Object,
            datasetRegistryMock.Object,
            rendererMock.Object,
            NullLogger<MapFishPrintService>.Instance);

        return (service, MapFishPrintDefaults.Create()[0], () => captured);
    }

    private static Mock<IMetadataRegistry> CreateMetadataRegistry()
    {
        var snapshot = CreateMetadataSnapshot();
        var registry = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        registry.Setup(r => r.EnsureInitializedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        registry.Setup(r => r.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        registry.SetupGet(r => r.Snapshot).Returns(snapshot);
        return registry;
    }

    private static Mock<IRasterDatasetRegistry> CreateDatasetRegistry()
    {
        var dataset = CreateRasterDataset();
        var registry = new Mock<IRasterDatasetRegistry>(MockBehavior.Strict);
        registry.Setup(r => r.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
            {
                if (string.Equals(id, dataset.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return dataset;
                }

                if (id.Contains(':', StringComparison.Ordinal))
                {
                    var parts = id.Split(':', 2, StringSplitOptions.TrimEntries);
                    if (string.Equals(parts[^1], dataset.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return dataset;
                    }
                }

                return null;
            });
        return registry;
    }

    private static Mock<IMapFishPrintApplicationStore> CreateApplicationStore()
    {
        var application = MapFishPrintDefaults.Create()[0];
        var dictionary = new Dictionary<string, MapFishPrintApplicationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [application.Id] = application
        };

        var store = new Mock<IMapFishPrintApplicationStore>(MockBehavior.Strict);
        store.Setup(s => s.GetApplicationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dictionary);
        store.Setup(s => s.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
                dictionary.TryGetValue(id, out var value) ? value : null);
        return store;
    }

    private static Mock<IRasterRenderer> CreateRendererMock(Action<RasterRenderRequest>? capture = null)
    {
        var renderer = new Mock<IRasterRenderer>(MockBehavior.Strict);
        renderer.Setup(r => r.RenderAsync(It.IsAny<RasterRenderRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RasterRenderRequest, CancellationToken>((request, _) => capture?.Invoke(request))
            .Returns<RasterRenderRequest, CancellationToken>((request, _) =>
            {
                var stream = new MemoryStream(PngPixel, writable: false);
                var result = new RasterRenderResult(stream, "image/png", request.Width, request.Height);
                return Task.FromResult(result);
            });
        return renderer;
    }

    private static MetadataSnapshot CreateMetadataSnapshot()
    {
        var catalog = new CatalogDefinition { Id = "catalog", Title = "Catalog" };
        var folder = new FolderDefinition { Id = "root", Title = "Root" };
        var dataSource = new DataSourceDefinition { Id = "source", Provider = "stub", ConnectionString = "ignored" };
        var service = new ServiceDefinition
        {
            Id = "default",
            Title = "Imagery",
            FolderId = folder.Id,
            DataSourceId = dataSource.Id,
            ServiceType = "raster",
            Enabled = true
        };

        var snapshot = new MetadataSnapshot(
            catalog,
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            Array.Empty<LayerDefinition>(),
            new[] { CreateRasterDataset() },
            Array.Empty<StyleDefinition>());

        return snapshot;
    }

    private static RasterDatasetDefinition CreateRasterDataset()
    {
        return new RasterDatasetDefinition
        {
            Id = "test-imagery",
            Title = "Test Imagery",
            ServiceId = "default",
            Crs = new[] { "EPSG:3857" },
            Source = new RasterSourceDefinition
            {
                Type = "memory",
                Uri = "memory://test"
            },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = null,
                StyleIds = Array.Empty<string>()
            },
            Cache = new RasterCacheDefinition
            {
                Enabled = false
            }
        };
    }
}
