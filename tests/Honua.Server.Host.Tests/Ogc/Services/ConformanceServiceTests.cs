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
public class ConformanceServiceTests
{
    private readonly Mock<IMetadataRegistry> metadataRegistryMock;
    private readonly OgcCacheHeaderService cacheHeaderService;
    private readonly ConformanceService service;

    public ConformanceServiceTests()
    {
        metadataRegistryMock = new Mock<IMetadataRegistry>();

        var cacheOptions = Options.Create(new CacheHeaderOptions
        {
            EnableCaching = true,
            EnableETagGeneration = true
        });
        cacheHeaderService = new OgcCacheHeaderService(cacheOptions);

        service = new ConformanceService(
            metadataRegistryMock.Object,
            cacheHeaderService);
    }

    [Fact]
    public async Task GetConformanceAsync_ReturnsDefaultConformanceClasses()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.GetConformanceAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();

        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();

        // Verify default conformance classes are included
        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);
        var conformsTo = doc.RootElement.GetProperty("conformsTo");

        conformsTo.GetArrayLength().Should().BeGreaterOrEqualTo(10);

        var conformanceList = conformsTo.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        conformanceList.Should().Contain("http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core");
        conformanceList.Should().Contain("http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson");
        conformanceList.Should().Contain("http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30");
    }

    [Fact]
    public async Task GetConformanceAsync_IncludesServiceSpecificConformanceClasses()
    {
        // Arrange
        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithConformanceClasses(
                "http://example.com/spec/custom-1",
                "http://example.com/spec/custom-2")
            .Build();

        var service2 = new ServiceDefinitionBuilder()
            .WithId("service2")
            .WithConformanceClasses(
                "http://example.com/spec/custom-3")
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1, service2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.GetConformanceAsync();

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        okResult.Should().NotBeNull();

        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var conformsTo = doc.RootElement.GetProperty("conformsTo");

        var conformanceList = conformsTo.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        conformanceList.Should().Contain("http://example.com/spec/custom-1");
        conformanceList.Should().Contain("http://example.com/spec/custom-2");
        conformanceList.Should().Contain("http://example.com/spec/custom-3");
    }

    [Fact]
    public async Task GetConformanceAsync_IgnoresNullOrEmptyConformanceClasses()
    {
        // Arrange
        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithConformanceClasses(
                "http://example.com/spec/valid",
                "",
                "   ",
                "http://example.com/spec/another-valid")
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.GetConformanceAsync();

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var conformsTo = doc.RootElement.GetProperty("conformsTo");

        var conformanceList = conformsTo.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        conformanceList.Should().Contain("http://example.com/spec/valid");
        conformanceList.Should().Contain("http://example.com/spec/another-valid");
        conformanceList.Should().NotContain("");
        conformanceList.Should().NotContain("   ");
    }

    [Fact]
    public async Task GetConformanceAsync_RemovesDuplicateConformanceClasses()
    {
        // Arrange
        var service1 = new ServiceDefinitionBuilder()
            .WithId("service1")
            .WithConformanceClasses("http://example.com/spec/duplicate")
            .Build();

        var service2 = new ServiceDefinitionBuilder()
            .WithId("service2")
            .WithConformanceClasses("http://example.com/spec/duplicate")
            .Build();

        var snapshot = new MetadataSnapshotBuilder()
            .WithServices(service1, service2)
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.GetConformanceAsync();

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var conformsTo = doc.RootElement.GetProperty("conformsTo");

        var conformanceList = conformsTo.EnumerateArray()
            .Select(x => x.GetString()!)
            .Where(x => x == "http://example.com/spec/duplicate")
            .ToList();

        conformanceList.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetConformanceAsync_AsyncOperationCompletesSuccessfully()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        using var cts = new CancellationTokenSource();

        // Act
        var resultTask = service.GetConformanceAsync(cts.Token);
        var result = await resultTask;

        // Assert
        resultTask.IsCompletedSuccessfully.Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConformanceAsync_CallsMetadataRegistryOnce()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices()
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        await service.GetConformanceAsync();

        // Assert
        metadataRegistryMock.Verify(
            x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetConformanceAsync_WithNoServices_ReturnsOnlyDefaultConformanceClasses()
    {
        // Arrange
        var snapshot = new MetadataSnapshotBuilder()
            .WithServices() // Empty services
            .Build();

        metadataRegistryMock
            .Setup(x => x.GetInitializedSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Act
        var result = await service.GetConformanceAsync();

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        var json = JsonSerializer.Serialize(okResult!.Value);
        var doc = JsonDocument.Parse(json);
        var conformsTo = doc.RootElement.GetProperty("conformsTo");

        var conformanceList = conformsTo.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        // Should only contain default conformance classes
        conformanceList.Should().HaveCount(OgcSharedHandlers.DefaultConformanceClasses.Length);
        conformanceList.Should().Contain(OgcSharedHandlers.DefaultConformanceClasses);
    }

    [Fact]
    public void Constructor_WithNullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ConformanceService(null!, cacheHeaderService);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadataRegistry");
    }

    [Fact]
    public void Constructor_WithNullCacheHeaderService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ConformanceService(metadataRegistryMock.Object, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheHeaderService");
    }
}
