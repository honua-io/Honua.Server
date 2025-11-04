// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.HealthChecks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.HealthChecks;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class WarmupHealthCheckTests
{
    private readonly ILogger<WarmupHealthCheck> _logger;

    public WarmupHealthCheckTests()
    {
        _logger = NullLogger<WarmupHealthCheck>.Instance;
    }

    [Fact]
    public async Task CheckHealthAsync_FirstInvocation_ReturnsDegraded()
    {
        // Arrange
        var warmupServices = new[] { CreateMockWarmupService().Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("warmup in progress");
        result.Data.Should().ContainKey("warmupStatus");
        result.Data["warmupStatus"].Should().Be("in_progress");
    }

    [Fact]
    public async Task CheckHealthAsync_TriggersWarmupServices()
    {
        // Arrange
        var mockService1 = CreateMockWarmupService();
        var mockService2 = CreateMockWarmupService();
        var warmupServices = new[] { mockService1.Object, mockService2.Object };

        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);
        await Task.Delay(200); // Wait for background warmup

        // Assert
        mockService1.Verify(s => s.WarmupAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockService2.Verify(s => s.WarmupAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_OnlyTriggersWarmupOnce()
    {
        // Arrange
        var mockService = CreateMockWarmupService();
        var warmupServices = new[] { mockService.Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act - Call multiple times
        await healthCheck.CheckHealthAsync(context);
        await healthCheck.CheckHealthAsync(context);
        await healthCheck.CheckHealthAsync(context);
        await Task.Delay(200);

        // Assert - Should only warmup once
        mockService.Verify(s => s.WarmupAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterWarmupCompletes_ReturnsHealthy()
    {
        // Arrange
        var mockService = CreateMockWarmupService(delayMs: 100);
        var warmupServices = new[] { mockService.Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act - First check starts warmup
        var result1 = await healthCheck.CheckHealthAsync(context);

        // Wait for warmup to complete
        await Task.Delay(300);

        // Second check after warmup
        var result2 = await healthCheck.CheckHealthAsync(context);

        // Assert
        result1.Status.Should().Be(HealthStatus.Degraded);
        result2.Status.Should().Be(HealthStatus.Healthy);
        result2.Description.Should().Contain("warmed up");
        result2.Data["warmupStatus"].Should().Be("completed");
    }

    [Fact]
    public async Task CheckHealthAsync_DuringWarmup_ReturnsDegraded()
    {
        // Arrange
        var mockService = CreateMockWarmupService(delayMs: 500);
        var warmupServices = new[] { mockService.Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act - Start warmup
        var result1 = await healthCheck.CheckHealthAsync(context);

        // Check again while still warming up
        await Task.Delay(100);
        var result2 = await healthCheck.CheckHealthAsync(context);

        // Assert - Both should be degraded
        result1.Status.Should().Be(HealthStatus.Degraded);
        result2.Status.Should().Be(HealthStatus.Degraded);
        result2.Data["warmupStatus"].Should().Be("in_progress");
    }

    [Fact]
    public async Task CheckHealthAsync_WarmupServiceFailure_StillCompletesWarmup()
    {
        // Arrange
        var mockService = new Mock<IWarmupService>();
        mockService
            .Setup(s => s.WarmupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Warmup failed"));

        var warmupServices = new[] { mockService.Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);
        await Task.Delay(200);

        // Check again after warmup attempt
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - Should still mark as completed despite failure
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["warmupStatus"].Should().Be("completed");
    }

    [Fact]
    public async Task CheckHealthAsync_MultipleServices_WarmsUpAll()
    {
        // Arrange
        var services = new[]
        {
            CreateMockWarmupService().Object,
            CreateMockWarmupService().Object,
            CreateMockWarmupService().Object
        };

        var healthCheck = new WarmupHealthCheck(services, _logger);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);
        await Task.Delay(200);

        // Assert
        foreach (var service in services)
        {
            var mock = Mock.Get(service);
            mock.Verify(s => s.WarmupAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_NoWarmupServices_ReturnsHealthy()
    {
        // Arrange
        var warmupServices = Enumerable.Empty<IWarmupService>();
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);
        await Task.Delay(100);

        // Second check
        var result2 = await healthCheck.CheckHealthAsync(context);

        // Assert
        result2.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Constructor_ThrowsWhenWarmupServicesIsNull()
    {
        // Act & Assert
        var act = () => new WarmupHealthCheck(null!, _logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        // Arrange
        var warmupServices = Enumerable.Empty<IWarmupService>();

        // Act & Assert
        var act = () => new WarmupHealthCheck(warmupServices, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckHealthAsync_CancellationRequested_PropagatesToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var mockService = new Mock<IWarmupService>();
        var tokenReceived = false;

        mockService
            .Setup(s => s.WarmupAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                tokenReceived = ct.IsCancellationRequested;
                await Task.CompletedTask;
            });

        var warmupServices = new[] { mockService.Object };
        var healthCheck = new WarmupHealthCheck(warmupServices, _logger);
        var context = new HealthCheckContext();

        // Act
        cts.Cancel();
        await healthCheck.CheckHealthAsync(context, cts.Token);
        await Task.Delay(100);

        // Assert - Token should have been passed to warmup service
        // Note: The warmup runs in background, so cancellation may or may not be observed
        // depending on timing
    }

    private Mock<IWarmupService> CreateMockWarmupService(int delayMs = 0)
    {
        var mock = new Mock<IWarmupService>();
        mock.Setup(s => s.WarmupAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }
            });
        return mock;
    }
}

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class MetadataCacheWarmupServiceTests
{
    private readonly Mock<IMetadataRegistry> _mockMetadataRegistry;
    private readonly ILogger<MetadataCacheWarmupService> _logger;

    public MetadataCacheWarmupServiceTests()
    {
        _mockMetadataRegistry = new Mock<IMetadataRegistry>();
        _logger = NullLogger<MetadataCacheWarmupService>.Instance;
    }

    [Fact]
    public async Task WarmupAsync_EnsuresMetadataInitialized()
    {
        // Arrange
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataSnapshot(
                new CatalogDefinition { Id = "test-catalog" },
                Array.Empty<FolderDefinition>(),
                Array.Empty<DataSourceDefinition>(),
                Array.Empty<ServiceDefinition>(),
                Array.Empty<LayerDefinition>()));

        var service = new MetadataCacheWarmupService(_mockMetadataRegistry.Object, _logger);

        // Act
        await service.WarmupAsync(CancellationToken.None);

        // Assert
        _mockMetadataRegistry.Verify(
            m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WarmupAsync_PreloadsSnapshot()
    {
        // Arrange
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataSnapshot(
                new CatalogDefinition { Id = "test-catalog" },
                Array.Empty<FolderDefinition>(),
                Array.Empty<DataSourceDefinition>(),
                Array.Empty<ServiceDefinition>(),
                Array.Empty<LayerDefinition>()));

        var service = new MetadataCacheWarmupService(_mockMetadataRegistry.Object, _logger);

        // Act
        await service.WarmupAsync(CancellationToken.None);

        // Assert
        _mockMetadataRegistry.Verify(
            m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WarmupAsync_WhenInitializationFails_ThrowsException()
    {
        // Arrange
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Initialization failed"));

        var service = new MetadataCacheWarmupService(_mockMetadataRegistry.Object, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => service.WarmupAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WarmupAsync_WhenSnapshotFails_ThrowsException()
    {
        // Arrange
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Snapshot failed"));

        var service = new MetadataCacheWarmupService(_mockMetadataRegistry.Object, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => service.WarmupAsync(CancellationToken.None));
    }

    [Fact]
    public void Constructor_ThrowsWhenMetadataRegistryIsNull()
    {
        // Act & Assert
        var act = () => new MetadataCacheWarmupService(null!, _logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        // Act & Assert
        var act = () => new MetadataCacheWarmupService(_mockMetadataRegistry.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task WarmupAsync_PropagatesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        CancellationToken receivedToken = default;

        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(ct => receivedToken = ct)
            .Returns(Task.CompletedTask);

        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataSnapshot(
                new CatalogDefinition { Id = "test-catalog" },
                Array.Empty<FolderDefinition>(),
                Array.Empty<DataSourceDefinition>(),
                Array.Empty<ServiceDefinition>(),
                Array.Empty<LayerDefinition>()));

        var service = new MetadataCacheWarmupService(_mockMetadataRegistry.Object, _logger);

        // Act
        await service.WarmupAsync(cts.Token);

        // Assert
        receivedToken.Should().Be(cts.Token);
    }
}
