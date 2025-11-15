// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Results;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Ogc.Services;
using Honua.Server.Host.Tests.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

[Trait("Category", "Unit")]
public class CollectionServiceTests
{
    private readonly Mock<IMetadataRegistry> metadataRegistryMock;
    private readonly Mock<IFeatureContextResolver> contextResolverMock;
    private readonly OgcCacheHeaderService cacheHeaderService;
    private readonly Mock<IOgcFeaturesRenderingHandler> renderingHandlerMock;
    private readonly Mock<IOgcCollectionsCache> collectionsCacheMock;
    private readonly OgcLinkBuilder linkBuilder;
    private readonly CollectionService service;

    public CollectionServiceTests()
    {
        metadataRegistryMock = new Mock<IMetadataRegistry>();
        contextResolverMock = new Mock<IFeatureContextResolver>();

        var cacheOptions = Options.Create(new CacheHeaderOptions
        {
            EnableCaching = true,
            EnableETagGeneration = true
        });
        cacheHeaderService = new OgcCacheHeaderService(cacheOptions);

        renderingHandlerMock = new Mock<IOgcFeaturesRenderingHandler>();
        collectionsCacheMock = new Mock<IOgcCollectionsCache>();
        linkBuilder = new OgcLinkBuilder();

        service = new CollectionService(
            metadataRegistryMock.Object,
            contextResolverMock.Object,
            cacheHeaderService,
            renderingHandlerMock.Object,
            collectionsCacheMock.Object,
            linkBuilder);
    }

    [Fact]
    public async Task GetCollectionsAsync_ReturnsAllCollections()
    {
        // Arrange
        var layer1 = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithTitle("Layer 1")
            .WithServiceId("service1")
            .Build();

        var layer2 = new LayerDefinitionBuilder()
            .WithId("layer2")
            .WithTitle("Layer 2")
            .WithServiceId("service1")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithEnabled(true)
            .WithCollectionsEnabled(true)
            .WithLayers(layer1, layer2)
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1)
            .WithLayers(layer1, layer2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns(false);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();

        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.GetArrayLength().Should().Be(2);

        var collectionArray = collections.EnumerateArray().ToList();
        collectionArray[0].GetProperty("id").GetString().Should().Be("service1::layer1");
        collectionArray[0].GetProperty("title").GetString().Should().Be("Layer 1");

        collectionArray[1].GetProperty("id").GetString().Should().Be("service1::layer2");
        collectionArray[1].GetProperty("title").GetString().Should().Be("Layer 2");
    }

    [Fact]
    public async Task GetCollectionsAsync_CacheHit_ReturnsCachedResponse()
    {
        // Arrange
        var cachedContent = "{\"collections\":[]}";
        var cachedETag = "\"cached-etag\"";

        var cacheEntry = new OgcCollectionsCacheEntry
        {
            Content = cachedContent,
            ContentType = "application/json",
            ETag = cachedETag,
            CachedAt = DateTimeOffset.UtcNow
        };

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                null,
                "json",
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns((string? sId, string fmt, string? lang, out OgcCollectionsCacheEntry? entry) =>
            {
                entry = cacheEntry;
                return true;
            });

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        result.Should().NotBeNull();

        // Verify metadata registry was NOT called (cache hit)
        metadataRegistryMock.Verify(
            x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCollectionsAsync_CacheMiss_GeneratesAndCachesResponse()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithEnabled(true)
            .WithCollectionsEnabled(true)
            .WithLayers(layer)
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1)
            .WithLayers(layer)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns(false);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        result.Should().NotBeNull();

        // Verify cache was written to
        collectionsCacheMock.Verify(
            x => x.SetCollectionsAsync(
                null,
                "json",
                It.IsAny<string?>(),
                It.IsAny<string>(),
                "application/json",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCollectionsAsync_HtmlFormat_CachesHtmlResponse()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithEnabled(true)
            .WithCollectionsEnabled(true)
            .WithLayers(layer)
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1)
            .WithLayers(layer)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns(false);

        var request = new DefaultHttpContext().Request;
        request.Headers["Accept"] = "text/html";

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(true);

        renderingHandlerMock
            .Setup(x => x.RenderCollectionsHtml(
                It.IsAny<HttpRequest>(),
                It.IsAny<MetadataSnapshot>(),
                It.IsAny<IReadOnlyList<OgcSharedHandlers.CollectionSummary>>()))
            .Returns("<html><body>Collections</body></html>");

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        result.Should().NotBeNull();

        // Verify HTML was cached
        collectionsCacheMock.Verify(
            x => x.SetCollectionsAsync(
                null,
                "html",
                It.IsAny<string?>(),
                It.IsAny<string>(),
                OgcSharedHandlers.HtmlContentType,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCollectionsAsync_SkipsDisabledServices()
    {
        // Arrange
        var layer1 = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var layer2 = new LayerDefinitionBuilder()
            .WithId("layer2")
            .WithServiceId("service2")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithEnabled(true)
            .WithCollectionsEnabled(true)
            .WithLayers(layer1)
            .Build();

        var service2 = new ServiceDefinitionBuilder()
            .WithId("service2")
            .WithEnabled(false) // Disabled
            .WithCollectionsEnabled(true)
            .WithLayers(layer2)
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1, service2)
            .WithLayers(layer1, layer2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns(false);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.GetArrayLength().Should().Be(1);

        var collectionArray = collections.EnumerateArray().ToList();
        collectionArray[0].GetProperty("id").GetString().Should().Be("service1::layer1");
    }

    [Fact]
    public async Task GetCollectionsAsync_SkipsServicesWithCollectionsDisabled()
    {
        // Arrange
        var layer1 = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var layer2 = new LayerDefinitionBuilder()
            .WithId("layer2")
            .WithServiceId("service2")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithEnabled(true)
            .WithCollectionsEnabled(true)
            .WithLayers(layer1)
            .Build();

        var service2 = new ServiceDefinitionBuilder()
            .WithId("service2")
            .WithEnabled(true)
            .WithCollectionsEnabled(false) // Collections disabled
            .WithLayers(layer2)
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1, service2)
            .WithLayers(layer1, layer2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        collectionsCacheMock
            .Setup(x => x.TryGetCollections(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                out It.Ref<OgcCollectionsCacheEntry?>.IsAny))
            .Returns(false);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionsAsync(request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetCollectionAsync_ReturnsSpecificCollection()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithTitle("Layer 1")
            .WithDescription("Test Description")
            .WithServiceId("service1")
            .WithExtent(-180, -90, 180, 90)
            .WithCrs("http://www.opengis.net/def/crs/OGC/1.3/CRS84")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .Build();

        var context = new FeatureContext(service1, layer, null!);

        contextResolverMock
            .Setup(x => x.ResolveAsync(
                "service1::layer1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FeatureContext>.Success(context));

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionAsync("service1::layer1", request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();

        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("id").GetString().Should().Be("service1::layer1");
        doc.RootElement.GetProperty("title").GetString().Should().Be("Layer 1");
        doc.RootElement.GetProperty("description").GetString().Should().Be("Test Description");
    }

    [Fact]
    public async Task GetCollectionAsync_ReturnsNotFound_ForUnknownCollection()
    {
        // Arrange
        var error = new Error("not_found", "Collection not found");

        contextResolverMock
            .Setup(x => x.ResolveAsync(
                "unknown::collection",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FeatureContext>.Failure(error));

        var request = new DefaultHttpContext().Request;

        // Act
        var result = await service.GetCollectionAsync("unknown::collection", request);

        // Assert
        result.Should().NotBeNull();

        // Execute result to get status code
        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetCollectionAsync_JsonFormat_ReturnsJson()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .Build();

        var context = new FeatureContext(service1, layer, null!);

        contextResolverMock
            .Setup(x => x.ResolveAsync(
                "service1::layer1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FeatureContext>.Success(context));

        var request = new DefaultHttpContext().Request;
        request.Headers["Accept"] = "application/json";

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionAsync("service1::layer1", request);

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();
    }

    [Fact]
    public async Task GetCollectionAsync_HtmlFormat_ReturnsHtml()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithServiceId("service1")
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .Build();

        var context = new FeatureContext(service1, layer, null!);

        contextResolverMock
            .Setup(x => x.ResolveAsync(
                "service1::layer1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FeatureContext>.Success(context));

        var request = new DefaultHttpContext().Request;
        request.Headers["Accept"] = "text/html";

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(true);

        // Act
        var result = await service.GetCollectionAsync("service1::layer1", request);

        // Assert
        result.Should().NotBeNull();

        // Verify render method was called
        renderingHandlerMock.Verify(
            x => x.WantsHtml(It.IsAny<HttpRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCollectionAsync_IncludesLayerMetadata()
    {
        // Arrange
        var layer = new LayerDefinitionBuilder()
            .WithId("layer1")
            .WithTitle("Test Layer")
            .WithDescription("Test Description")
            .WithServiceId("service1")
            .WithItemType("feature")
            .WithExtent(-180, -90, 180, 90)
            .WithKeywords("test", "layer")
            .WithDefaultStyleId("default-style")
            .WithStyleIds("style1", "style2")
            .WithMinScale(1000)
            .WithMaxScale(10000)
            .Build();

        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .Build();

        var context = new FeatureContext(service1, layer, null!);

        contextResolverMock
            .Setup(x => x.ResolveAsync(
                "service1::layer1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FeatureContext>.Success(context));

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetCollectionAsync("service1::layer1", request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("itemType").GetString().Should().Be("feature");
        doc.RootElement.GetProperty("defaultStyle").GetString().Should().Be("default-style");
        doc.RootElement.GetProperty("minScale").GetDouble().Should().Be(1000);
        doc.RootElement.GetProperty("maxScale").GetDouble().Should().Be(10000);

        var keywords = doc.RootElement.GetProperty("keywords");
        keywords.GetArrayLength().Should().Be(2);

        var styleIds = doc.RootElement.GetProperty("styleIds");
        styleIds.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCollectionsAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await service.GetCollectionsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task GetCollectionAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await service.GetCollectionAsync("test", null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public void Constructor_WithNullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CollectionService(
            null!,
            contextResolverMock.Object,
            cacheHeaderService,
            renderingHandlerMock.Object,
            collectionsCacheMock.Object,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadataRegistry");
    }

    [Fact]
    public void Constructor_WithNullContextResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CollectionService(
            metadataRegistryMock.Object,
            null!,
            cacheHeaderService,
            renderingHandlerMock.Object,
            collectionsCacheMock.Object,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("contextResolver");
    }
}
