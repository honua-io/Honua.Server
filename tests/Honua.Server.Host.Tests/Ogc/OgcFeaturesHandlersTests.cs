using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Tests.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcFeaturesHandlersTests
{
    private static readonly OgcCacheHeaderService CacheHeaderService = new(Options.Create(new CacheHeaderOptions()));

    [Fact]
    public async Task GetCollectionItems_UsesPreloadedAttachmentsWithoutPerFeatureLookup()
    {
        // Arrange
        const string serviceId = "transport";
        const string layerId = "roads";

        var snapshotBuilder = new MetadataSnapshotBuilder()
            .WithCatalog("catalog")
            .WithDataSource("primary")
            .WithService(serviceId, "root", "primary", configure: service =>
            {
                service.WithServiceType("FeatureServer");
                service.WithEnabled(true);
            })
            .WithLayer(layerId, serviceId, configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithAttachments("default-profile", enabled: true, exposeOgcLinks: true);
                layer.WithField(new FieldDefinition
                {
                    Name = "name",
                    Alias = "display_name",
                    DataType = "string",
                    Nullable = true
                });
            });

        var snapshot = snapshotBuilder.Build();
        var service = snapshot.Services.Single();
        var layer = snapshot.Layers.Single(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        var dataSource = snapshot.DataSources.Single();

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var featureContext = new FeatureContext(snapshot, service, layer, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock
            .Setup(r => r.ResolveAsync(serviceId, layerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureContext);

        var featureRecords = new[]
        {
            CreateFeatureRecord(layer, 1, "Alpha"),
            CreateFeatureRecord(layer, 2, "Bravo")
        };

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(featureRecords));

        var listBatchResult = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new[]
            {
                CreateAttachmentDescriptor(service, layer, featureId: "1", attachmentId: "att-1", name: "alpha.txt")
            },
            ["2"] = new[]
            {
                CreateAttachmentDescriptor(service, layer, featureId: "2", attachmentId: "att-2", name: "bravo.txt")
            }
        };

        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);
        orchestratorMock
            .Setup(o => o.ListBatchAsync(service.Id, layer.Id, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listBatchResult);
        orchestratorMock
            .Setup(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Per-feature attachment lookup should not be used when batch results are available."));

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadataRegistryMock
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MetadataSnapshot>(snapshot));

        var metricsMock = new Mock<IApiMetrics>(MockBehavior.Strict);
        metricsMock
            .Setup(m => m.RecordFeaturesReturned("ogc-api-features", service.Id, layer.Id, featureRecords.Length));

        var geoPackageExporter = new Mock<IGeoPackageExporter>(MockBehavior.Strict);
        var shapefileExporter = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var flatGeobufExporter = new Mock<IFlatGeobufExporter>(MockBehavior.Strict);
        var geoArrowExporter = new Mock<IGeoArrowExporter>(MockBehavior.Strict);
        var csvExporter = new Mock<ICsvExporter>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        var collectionId = OgcSharedHandlers.BuildCollectionId(service, layer);
        httpContext.Request.Path = $"/ogc/collections/{collectionId}/items";
        httpContext.Response.Body = new MemoryStream();

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            collectionId,
            httpContext.Request,
            resolverMock.Object,
            repositoryMock.Object,
            geoPackageExporter.Object,
            shapefileExporter.Object,
            flatGeobufExporter.Object,
            geoArrowExporter.Object,
            csvExporter.Object,
            orchestratorMock.Object,
            metadataRegistryMock.Object,
            metricsMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        // Assert
        orchestratorMock.Verify(o => o.ListBatchAsync(service.Id, layer.Id, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        orchestratorMock.Verify(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(httpContext.Response.Body);
        var root = document.RootElement;
        root.GetProperty("features").GetArrayLength().Should().Be(featureRecords.Length);

        var enclosureCounts = root.GetProperty("features")
            .EnumerateArray()
            .Select(feature =>
                feature.GetProperty("links")
                    .EnumerateArray()
                    .Count(link => string.Equals(link.GetProperty("rel").GetString(), "enclosure", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        enclosureCounts.Should().AllSatisfy(count => count.Should().Be(1));
    }

    [Fact]
    public async Task GetCollectionItems_StreamsGeoJson_WhenAttachmentsDisabled()
    {
        // Arrange
        const string serviceId = "transport";
        const string layerId = "roads";

        var snapshotBuilder = new MetadataSnapshotBuilder()
            .WithCatalog("catalog")
            .WithDataSource("primary")
            .WithService(serviceId, "root", "primary", configure: service =>
            {
                service.WithServiceType("FeatureServer");
                service.WithEnabled(true);
            })
            .WithLayer(layerId, serviceId, configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithField(new FieldDefinition
                {
                    Name = "name",
                    Alias = "display_name",
                    DataType = "string",
                    Nullable = true
                });
            });

        var snapshot = snapshotBuilder.Build();
        var service = snapshot.Services.Single();
        var layer = snapshot.Layers.Single(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        var dataSource = snapshot.DataSources.Single();

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var featureContext = new FeatureContext(snapshot, service, layer, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock
            .Setup(r => r.ResolveAsync(serviceId, layerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureContext);

        var featureRecords = new[]
        {
            CreateFeatureRecord(layer, 1, "Alpha"),
            CreateFeatureRecord(layer, 2, "Bravo"),
            CreateFeatureRecord(layer, 3, "Charlie")
        };

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(featureRecords));

        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadataRegistryMock
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MetadataSnapshot>(snapshot));

        var metricsMock = new Mock<IApiMetrics>(MockBehavior.Strict);
        metricsMock
            .Setup(m => m.RecordFeaturesReturned("ogc-api-features", service.Id, layer.Id, featureRecords.Length));

        var geoPackageExporter = new Mock<IGeoPackageExporter>(MockBehavior.Strict);
        var shapefileExporter = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var flatGeobufExporter = new Mock<IFlatGeobufExporter>(MockBehavior.Strict);
        var geoArrowExporter = new Mock<IGeoArrowExporter>(MockBehavior.Strict);
        var csvExporter = new Mock<ICsvExporter>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext();
        var collectionId = OgcSharedHandlers.BuildCollectionId(service, layer);
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.Path = $"/ogc/collections/{collectionId}/items";
        httpContext.Response.Body = new MemoryStream();

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            collectionId,
            httpContext.Request,
            resolverMock.Object,
            repositoryMock.Object,
            geoPackageExporter.Object,
            shapefileExporter.Object,
            flatGeobufExporter.Object,
            geoArrowExporter.Object,
            csvExporter.Object,
            orchestratorMock.Object,
            metadataRegistryMock.Object,
            metricsMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.ContentType.Should().Be("application/geo+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(httpContext.Response.Body);
        var root = document.RootElement;
        root.GetProperty("numberReturned").GetInt32().Should().Be(featureRecords.Length);
        root.GetProperty("features").GetArrayLength().Should().Be(featureRecords.Length);

        var firstFeature = root.GetProperty("features")[0];
        var properties = firstFeature.GetProperty("properties");
        properties.TryGetProperty("name", out _).Should().BeTrue("streaming output should emit canonical field names");
        properties.TryGetProperty("display_name", out _).Should().BeFalse("aliases must not appear as separate properties in streaming output");

        orchestratorMock.VerifyAll();
        metricsMock.VerifyAll();
    }

    [Fact]
    public async Task BuildLegacyCollectionItemsResponse_UsesCanonicalCollectionId()
    {
        // Arrange
        const string serviceId = "transport";
        const string layerId = "roads";

        var snapshotBuilder = new MetadataSnapshotBuilder()
            .WithCatalog("catalog")
            .WithDataSource("primary")
            .WithService(serviceId, "root", "primary", configure: service =>
            {
                service.WithServiceType("FeatureServer");
                service.WithEnabled(true);
            })
            .WithLayer(layerId, serviceId, configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithField(new FieldDefinition
                {
                    Name = "name",
                    Alias = "display_name",
                    DataType = "string",
                    Nullable = true
                });
            });

        var snapshot = snapshotBuilder.Build();
        var service = snapshot.Services.Single();
        var layer = snapshot.Layers.Single(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        var dataSource = snapshot.DataSources.Single();

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var featureContext = new FeatureContext(snapshot, service, layer, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock
            .Setup(r => r.ResolveAsync(serviceId, layerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureContext);

        var featureRecords = new[]
        {
            CreateFeatureRecord(layer, 1, "Alpha"),
            CreateFeatureRecord(layer, 2, "Bravo")
        };

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(featureRecords));

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadataRegistryMock
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MetadataSnapshot>(snapshot));

        var metricsMock = new Mock<IApiMetrics>(MockBehavior.Strict);
        metricsMock
            .Setup(m => m.RecordFeaturesReturned("ogc-api-features", service.Id, layer.Id, featureRecords.Length));

        var catalogMock = new Mock<ICatalogProjectionService>(MockBehavior.Strict);
        catalogMock
            .Setup(c => c.GetService(serviceId))
            .Returns(new CatalogServiceView
            {
                Service = service,
                FolderTitle = "root",
                Layers = new[]
                {
                    new CatalogLayerView
                    {
                        Layer = layer
                    }
                }
            });

        var geoPackageExporter = new Mock<IGeoPackageExporter>(MockBehavior.Strict);
        var shapefileExporter = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var flatGeobufExporter = new Mock<IFlatGeobufExporter>(MockBehavior.Strict);
        var geoArrowExporter = new Mock<IGeoArrowExporter>(MockBehavior.Strict);
        var csvExporter = new Mock<ICsvExporter>(MockBehavior.Strict);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.Path = $"/ogc/{serviceId}/collections/{layerId}/items";
        httpContext.Response.Body = new MemoryStream();

        // Act
        var result = await OgcApiEndpointExtensions.BuildLegacyCollectionItemsResponse(
            serviceId,
            layerId,
            httpContext.Request,
            catalogMock.Object,
            resolverMock.Object,
            repositoryMock.Object,
            geoPackageExporter.Object,
            shapefileExporter.Object,
            flatGeobufExporter.Object,
            geoArrowExporter.Object,
            csvExporter.Object,
            orchestratorMock.Object,
            metadataRegistryMock.Object,
            metricsMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.ContentType.Should().Be("application/geo+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(httpContext.Response.Body);
        document.RootElement.GetProperty("numberReturned").GetInt32().Should().Be(featureRecords.Length);

        catalogMock.VerifyAll();
        resolverMock.VerifyAll();
        repositoryMock.VerifyAll();
        metricsMock.VerifyAll();
    }

    [Fact]
    public async Task GetCollectionItems_StreamsHtml_WhenAttachmentsDisabled()
    {
        // Arrange
        const string serviceId = "transport";
        const string layerId = "roads";

        var snapshotBuilder = new MetadataSnapshotBuilder()
            .WithCatalog("catalog")
            .WithDataSource("primary")
            .WithService(serviceId, "root", "primary", configure: service =>
            {
                service.WithServiceType("FeatureServer");
                service.WithEnabled(true);
            })
            .WithLayer(layerId, serviceId, configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithField(new FieldDefinition
                {
                    Name = "name",
                    Alias = "display_name",
                    DataType = "string",
                    Nullable = true
                });
            });

        var snapshot = snapshotBuilder.Build();
        var service = snapshot.Services.Single();
        var layer = snapshot.Layers.Single(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        var dataSource = snapshot.DataSources.Single();

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var featureContext = new FeatureContext(snapshot, service, layer, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock
            .Setup(r => r.ResolveAsync(serviceId, layerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureContext);

        var featureRecords = new[]
        {
            CreateFeatureRecord(layer, 1, "Alpha"),
            CreateFeatureRecord(layer, 2, "Bravo"),
            CreateFeatureRecord(layer, 3, "Charlie")
        };

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(featureRecords));

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadataRegistryMock
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MetadataSnapshot>(snapshot));

        var metricsMock = new Mock<IApiMetrics>(MockBehavior.Strict);
        metricsMock
            .Setup(m => m.RecordFeaturesReturned("ogc-api-features", service.Id, layer.Id, featureRecords.Length));

        var geoPackageExporter = new Mock<IGeoPackageExporter>(MockBehavior.Strict);
        var shapefileExporter = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var flatGeobufExporter = new Mock<IFlatGeobufExporter>(MockBehavior.Strict);
        var geoArrowExporter = new Mock<IGeoArrowExporter>(MockBehavior.Strict);
        var csvExporter = new Mock<ICsvExporter>(MockBehavior.Strict);
        var orchestratorMock = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext();
        var collectionId = OgcSharedHandlers.BuildCollectionId(service, layer);
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("example.test");
        httpContext.Request.Path = $"/ogc/collections/{collectionId}/items";
        httpContext.Request.QueryString = new QueryString("?f=html");
        httpContext.Response.Body = new MemoryStream();

        // Act
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            collectionId,
            httpContext.Request,
            resolverMock.Object,
            repositoryMock.Object,
            geoPackageExporter.Object,
            shapefileExporter.Object,
            flatGeobufExporter.Object,
            geoArrowExporter.Object,
            csvExporter.Object,
            orchestratorMock.Object,
            metadataRegistryMock.Object,
            metricsMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.ContentType.Should().Be(OgcSharedHandlers.HtmlContentType);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var html = await reader.ReadToEndAsync().ConfigureAwait(false);

        html.Should().Contain("<details", "streaming HTML output should include feature details");
        html.Should().Contain("Number returned")
            .And.Contain("Number matched");
        html.Should().Contain("Feature ID");

        orchestratorMock.VerifyAll();
        metricsMock.VerifyAll();
    }

    [Fact]
    public async Task GetSearch_DoesNotCallCount_WhenOffsetProvidedWithoutCountRequest()
    {
        // Arrange
        var builder = new MetadataSnapshotBuilder()
            .WithCatalog("catalog")
            .WithDataSource("primary")
            .WithService("svc", "root", "primary", configure: service =>
            {
                service.WithServiceType("FeatureServer");
                service.WithEnabled(true);
            })
            .WithLayer("roads", "svc", configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithField("name");
            })
            .WithLayer("trails", "svc", configure: layer =>
            {
                layer.WithGeometryType("Point");
                layer.WithField("name");
            });

        var snapshot = builder.Build();
        var service = snapshot.Services.Single();
        var roads = snapshot.Layers.Single(l => l.Id == "roads");
        var trails = snapshot.Layers.Single(l => l.Id == "trails");
        var dataSource = snapshot.DataSources.Single();

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var roadsContext = new FeatureContext(snapshot, service, roads, dataSource, providerMock.Object);
        var trailsContext = new FeatureContext(snapshot, service, trails, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock
            .Setup(r => r.ResolveAsync(service.Id, roads.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roadsContext);
        resolverMock
            .Setup(r => r.ResolveAsync(service.Id, trails.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trailsContext);

        var roadsFeatures = new[]
        {
            CreateFeatureRecord(roads, 1, "Main"),
            CreateFeatureRecord(roads, 2, "Second"),
            CreateFeatureRecord(roads, 3, "Third")
        };
        var trailsFeatures = new[]
        {
            CreateFeatureRecord(trails, 10, "Blue"),
            CreateFeatureRecord(trails, 11, "Red")
        };

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, roads.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(roadsFeatures));
        repositoryMock
            .Setup(r => r.QueryAsync(service.Id, trails.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(trailsFeatures));

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        metadataRegistryMock
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MetadataSnapshot>(snapshot));

        var metricsMock = new Mock<IApiMetrics>(MockBehavior.Strict);

        var geoPackageExporter = new Mock<IGeoPackageExporter>(MockBehavior.Strict);
        var shapefileExporter = new Mock<IShapefileExporter>(MockBehavior.Strict);
        var flatGeobufExporter = new Mock<IFlatGeobufExporter>(MockBehavior.Strict);
        var geoArrowExporter = new Mock<IGeoArrowExporter>(MockBehavior.Strict);
        var csvExporter = new Mock<ICsvExporter>(MockBehavior.Strict);
        var attachmentOrchestrator = new Mock<IFeatureAttachmentOrchestrator>(MockBehavior.Strict);

        var requestContext = new DefaultHttpContext();
        requestContext.Request.Scheme = "https";
        requestContext.Request.Host = new HostString("example.test");
        requestContext.Request.Path = "/ogc/search";
        requestContext.Request.QueryString = new QueryString("?collections=svc::roads,svc::trails&limit=2&offset=1");
        requestContext.Response.Body = new MemoryStream();

        // Act
        var result = await OgcFeaturesHandlers.GetSearch(
            requestContext.Request,
            resolverMock.Object,
            repositoryMock.Object,
            geoPackageExporter.Object,
            shapefileExporter.Object,
            flatGeobufExporter.Object,
            geoArrowExporter.Object,
            csvExporter.Object,
            attachmentOrchestrator.Object,
            metadataRegistryMock.Object,
            metricsMock.Object,
            CacheHeaderService,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(requestContext).ConfigureAwait(false);

        // Assert
        repositoryMock.Verify(r => r.CountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        requestContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        requestContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = JsonDocument.Parse(requestContext.Response.Body);
        var root = document.RootElement;
        root.TryGetProperty("numberMatched", out _).Should().BeFalse("count was not requested so numberMatched should be omitted");
    }

    private static FeatureRecord CreateFeatureRecord(LayerDefinition layer, int id, string name)
    {
        return new FeatureRecord(new Dictionary<string, object?>
        {
            [layer.IdField] = id,
            [layer.GeometryField] = null,
            ["name"] = name
        });
    }

    private static AttachmentDescriptor CreateAttachmentDescriptor(ServiceDefinition service, LayerDefinition layer, string featureId, string attachmentId, string name)
    {
        return new AttachmentDescriptor
        {
            AttachmentObjectId = attachmentId.GetHashCode(),
            AttachmentId = attachmentId,
            ServiceId = service.Id,
            LayerId = layer.Id,
            FeatureId = featureId,
            Name = name,
            MimeType = "application/octet-stream",
            SizeBytes = 1024,
            ChecksumSha256 = "checksum",
            StorageProvider = "s3",
            StorageKey = $"attachments/{attachmentId}",
            CreatedUtc = DateTimeOffset.UtcNow
        };
    }

    private static async IAsyncEnumerable<FeatureRecord> EnumerateAsync(IEnumerable<FeatureRecord> records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }
}
