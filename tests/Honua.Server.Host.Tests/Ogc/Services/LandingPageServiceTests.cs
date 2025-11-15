// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Ogc.Services;
using Honua.Server.Host.Tests.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

[Trait("Category", "Unit")]
public class LandingPageServiceTests
{
    private readonly Mock<IMetadataRegistry> metadataRegistryMock;
    private readonly OgcCacheHeaderService cacheHeaderService;
    private readonly Mock<IOgcFeaturesRenderingHandler> renderingHandlerMock;
    private readonly Mock<OgcApiDefinitionCache> apiDefinitionCacheMock;
    private readonly OgcLinkBuilder linkBuilder;
    private readonly LandingPageService service;

    public LandingPageServiceTests()
    {
        metadataRegistryMock = new Mock<IMetadataRegistry>();

        var cacheOptions = Options.Create(new CacheHeaderOptions
        {
            EnableCaching = true,
            EnableETagGeneration = true
        });
        cacheHeaderService = new OgcCacheHeaderService(cacheOptions);

        renderingHandlerMock = new Mock<IOgcFeaturesRenderingHandler>();
        apiDefinitionCacheMock = new Mock<OgcApiDefinitionCache>();
        linkBuilder = new OgcLinkBuilder();

        service = new LandingPageService(
            metadataRegistryMock.Object,
            cacheHeaderService,
            renderingHandlerMock.Object,
            apiDefinitionCacheMock.Object,
            linkBuilder);
    }

    [Fact]
    public async Task GetLandingPageAsync_ReturnsJsonFormat_WhenHtmlNotRequested()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithCatalogId("test-catalog")
            .WithCatalogTitle("Test Catalog")
            .WithCatalogDescription("Test Description")
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var request = new DefaultHttpContext().Request;
        request.Headers["Accept"] = "application/json";

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetLandingPageAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();

        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();

        // Verify JSON structure
        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("catalog", out var catalog).Should().BeTrue();
        catalog.GetProperty("id").GetString().Should().Be("test-catalog");
        catalog.GetProperty("title").GetString().Should().Be("Test Catalog");

        doc.RootElement.TryGetProperty("links", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetLandingPageAsync_ReturnsHtmlFormat_WhenHtmlRequested()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithCatalogId("test-catalog")
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var request = new DefaultHttpContext().Request;
        request.Headers["Accept"] = "text/html";

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(true);

        renderingHandlerMock
            .Setup(x => x.RenderLandingHtml(
                It.IsAny<HttpRequest>(),
                It.IsAny<MetadataSnapshot>(),
                It.IsAny<IReadOnlyList<OgcLink>>()))
            .Returns("<html><body>Test Landing Page</body></html>");

        // Act
        var result = await service.GetLandingPageAsync(request);

        // Assert
        result.Should().NotBeNull();

        // Verify rendering handler was called
        renderingHandlerMock.Verify(
            x => x.RenderLandingHtml(
                It.IsAny<HttpRequest>(),
                It.IsAny<MetadataSnapshot>(),
                It.IsAny<IReadOnlyList<OgcLink>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLandingPageAsync_IncludesServicesInResponse()
    {
        // Arrange
        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithTitle("Service 1")
            .WithServiceType("OgcApiFeatures")
            .WithFolderId("folder1")
            .Build();

        var service2 = new ServiceDefinitionBuilder()
            .WithId("service2")
            .WithTitle("Service 2")
            .WithServiceType("WMS")
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1, service2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetLandingPageAsync(request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("services", out var services).Should().BeTrue();
        services.GetArrayLength().Should().Be(2);

        var serviceArray = services.EnumerateArray().ToList();
        serviceArray[0].GetProperty("id").GetString().Should().Be("service1");
        serviceArray[0].GetProperty("title").GetString().Should().Be("Service 1");
        serviceArray[0].GetProperty("serviceType").GetString().Should().Be("OgcApiFeatures");
        serviceArray[0].GetProperty("folderId").GetString().Should().Be("folder1");

        serviceArray[1].GetProperty("id").GetString().Should().Be("service2");
        serviceArray[1].GetProperty("title").GetString().Should().Be("Service 2");
    }

    [Fact]
    public async Task GetLandingPageAsync_IncludesCorrectLinks()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");
        var request = context.Request;

        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetLandingPageAsync(request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("links", out var links).Should().BeTrue();
        links.GetArrayLength().Should().BeGreaterOrEqualTo(5);

        var linkArray = links.EnumerateArray()
            .Select(link => new
            {
                Href = link.GetProperty("href").GetString(),
                Rel = link.GetProperty("rel").GetString()
            })
            .ToList();

        linkArray.Should().Contain(l => l.Rel == "self" && l.Href!.Contains("/ogc"));
        linkArray.Should().Contain(l => l.Rel == "conformance" && l.Href!.Contains("/ogc/conformance"));
        linkArray.Should().Contain(l => l.Rel == "data" && l.Href!.Contains("/ogc/collections"));
        linkArray.Should().Contain(l => l.Rel == "service-desc" && l.Href!.Contains("/ogc/api"));
        linkArray.Should().Contain(l => l.Rel == "service-doc");
    }

    [Fact]
    public async Task GetApiDefinitionAsync_ReturnsApiDefinition_WhenFileExists()
    {
        // Arrange
        var apiDefinition = "{\"openapi\": \"3.0.0\"}";
        var etag = "\"test-etag\"";
        var lastModified = DateTimeOffset.UtcNow;

        var cacheEntry = new OgcApiDefinitionCacheEntry(apiDefinition, etag, lastModified);

        apiDefinitionCacheMock
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheEntry);

        // Act
        var result = await service.GetApiDefinitionAsync();

        // Assert
        result.Should().NotBeNull();

        // Verify cache was called
        apiDefinitionCacheMock.Verify(
            x => x.GetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetApiDefinitionAsync_Returns500_WhenFileNotFound()
    {
        // Arrange
        apiDefinitionCacheMock
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("API definition not found"));

        // Act
        var result = await service.GetApiDefinitionAsync();

        // Assert
        result.Should().NotBeNull();

        // Execute result to get status code
        var httpContext = new DefaultHttpContext();
        await result.ExecuteAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetLandingPageAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await service.GetLandingPageAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task GetLandingPageAsync_CallsMetadataRegistryOnce()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        await service.GetLandingPageAsync(request);

        // Assert
        metadataRegistryMock.Verify(
            x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLandingPageAsync_IncludesCatalogLinks()
    {
        // Arrange
        var catalogLink = new LinkDefinition
        {
            Href = "https://example.com/about",
            Rel = "about",
            Type = "text/html",
            Title = "About"
        };

        var snapshot = new MetadataSnapshotBuilder()
            .WithCatalog(new CatalogDefinition
            {
                Id = "test",
                Title = "Test",
                Links = new List<LinkDefinition> { catalogLink }
            })
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var request = new DefaultHttpContext().Request;
        renderingHandlerMock
            .Setup(x => x.WantsHtml(It.IsAny<HttpRequest>()))
            .Returns(false);

        // Act
        var result = await service.GetLandingPageAsync(request);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("links", out var links).Should().BeTrue();

        var linkArray = links.EnumerateArray()
            .Select(link => new
            {
                Href = link.GetProperty("href").GetString(),
                Rel = link.GetProperty("rel").GetString()
            })
            .ToList();

        linkArray.Should().Contain(l => l.Rel == "about" && l.Href == "https://example.com/about");
    }

    [Fact]
    public void Constructor_WithNullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LandingPageService(
            null!,
            cacheHeaderService,
            renderingHandlerMock.Object,
            apiDefinitionCacheMock.Object,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadataRegistry");
    }

    [Fact]
    public void Constructor_WithNullCacheHeaderService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LandingPageService(
            metadataRegistryMock.Object,
            null!,
            renderingHandlerMock.Object,
            apiDefinitionCacheMock.Object,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheHeaderService");
    }

    [Fact]
    public void Constructor_WithNullRenderingHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LandingPageService(
            metadataRegistryMock.Object,
            cacheHeaderService,
            null!,
            apiDefinitionCacheMock.Object,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("renderingHandler");
    }

    [Fact]
    public void Constructor_WithNullApiDefinitionCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LandingPageService(
            metadataRegistryMock.Object,
            cacheHeaderService,
            renderingHandlerMock.Object,
            null!,
            linkBuilder);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("apiDefinitionCache");
    }

    [Fact]
    public void Constructor_WithNullLinkBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LandingPageService(
            metadataRegistryMock.Object,
            cacheHeaderService,
            renderingHandlerMock.Object,
            apiDefinitionCacheMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("linkBuilder");
    }
}
