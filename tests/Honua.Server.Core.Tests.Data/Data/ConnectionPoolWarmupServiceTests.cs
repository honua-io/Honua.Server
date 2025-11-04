// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class ConnectionPoolWarmupServiceTests
{
    private readonly Mock<IMetadataRegistry> _mockMetadataRegistry;
    private readonly Mock<IDataStoreProviderFactory> _mockProviderFactory;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly ILogger<ConnectionPoolWarmupService> _logger;

    public ConnectionPoolWarmupServiceTests()
    {
        _mockMetadataRegistry = new Mock<IMetadataRegistry>();
        _mockProviderFactory = new Mock<IDataStoreProviderFactory>();
        _mockEnvironment = new Mock<IHostEnvironment>();
        _logger = NullLogger<ConnectionPoolWarmupService>.Instance;

        // Default setup for environment
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNotWarmUp()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions { Enabled = false };
        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give time for any background tasks

        // Assert
        _mockMetadataRegistry.Verify(
            m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_InDevelopment_SkipsWarmup()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            EnableInDevelopment = false
        };
        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Assert
        _mockMetadataRegistry.Verify(
            m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_InDevelopmentWithOverride_PerformsWarmup()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            EnableInDevelopment = true,
            StartupDelayMs = 0
        };

        var snapshot = CreateTestSnapshot(3);
        SetupMetadataRegistry(snapshot);

        var warmupCount = 0;
        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref warmupCount))
            .Returns(Task.CompletedTask);

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300); // Wait for background warmup

        // Assert - Verify that warmup actually happened (connections were tested)
        warmupCount.Should().Be(3);
    }

    [Fact]
    public async Task StartAsync_RespectsStartupDelay()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 500
        };

        var snapshot = CreateTestSnapshot(1);
        SetupMetadataRegistry(snapshot);
        SetupProviderFactory();

        var service = CreateService(options);
        var sw = Stopwatch.StartNew();

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(700); // Wait for delay + warmup
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(500);
    }

    [Fact]
    public async Task StartAsync_WarmsUpAllDataSources()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0,
            MaxDataSources = 10
        };

        var snapshot = CreateTestSnapshot(3);
        SetupMetadataRegistry(snapshot);

        var testConnectivityCallCount = 0;
        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref testConnectivityCallCount))
            .Returns(Task.CompletedTask);

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300); // Wait for background warmup

        // Assert
        testConnectivityCallCount.Should().Be(3);
    }

    [Fact]
    public async Task StartAsync_RespectsMaxConcurrency()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0,
            MaxConcurrentWarmups = 2
        };

        var snapshot = CreateTestSnapshot(5);
        SetupMetadataRegistry(snapshot);

        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(100); // Simulate work

                lock (lockObj)
                {
                    concurrentCount--;
                }
            });

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(800); // Wait for all warmups to complete

        // Assert
        maxConcurrent.Should().BeLessThanOrEqualTo(2, "should respect MaxConcurrentWarmups setting");
    }

    [Fact]
    public async Task StartAsync_HandlesConnectionFailures_DoesNotThrow()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0
        };

        var snapshot = CreateTestSnapshot(3);
        SetupMetadataRegistry(snapshot);

        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act & Assert - Should not throw
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);

        // The service should have attempted to warm up despite failures
        mockProvider.Verify(
            p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_RespectsTimeout()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0,
            TimeoutMs = 100
        };

        var snapshot = CreateTestSnapshot(1);
        SetupMetadataRegistry(snapshot);

        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(async (DataSourceDefinition ds, CancellationToken ct) =>
            {
                // Simulate slow connection - should timeout
                await Task.Delay(500, ct);
            });

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300); // Wait for timeout to occur

        // Assert - Should have attempted connection and timed out gracefully
        mockProvider.Verify(
            p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_RespectsMaxDataSources()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0,
            MaxDataSources = 3
        };

        var snapshot = CreateTestSnapshot(10);
        SetupMetadataRegistry(snapshot);

        var warmupCount = 0;
        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref warmupCount))
            .Returns(Task.CompletedTask);

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        // Assert
        warmupCount.Should().BeLessThanOrEqualTo(3, "should respect MaxDataSources limit");
    }

    [Fact]
    public async Task StartAsync_WhenMetadataNotInitialized_WaitsForInitialization()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0
        };

        _mockMetadataRegistry.Setup(m => m.IsInitialized).Returns(false);

        var initializationComplete = false;
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                initializationComplete = true;
                return ValueTask.CompletedTask;
            });

        var snapshot = CreateTestSnapshot(1);
        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (initializationComplete)
                {
                    return ValueTask.FromResult<MetadataSnapshot>(snapshot);
                }
                return ValueTask.FromResult<MetadataSnapshot>(null!);
            });

        SetupProviderFactory();

        var service = CreateService(options);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        // Assert
        _mockMetadataRegistry.Verify(
            m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenNoDataSources_CompletesGracefully()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions
        {
            Enabled = true,
            StartupDelayMs = 0
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "test-catalog" },
            Array.Empty<FolderDefinition>(),
            new List<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>());

        SetupMetadataRegistry(snapshot);

        var service = CreateService(options);

        // Act & Assert - Should not throw
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        _mockProviderFactory.Verify(
            f => f.Create(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions { Enabled = false };
        var service = CreateService(options);

        // Act & Assert
        await service.StopAsync(CancellationToken.None);
    }

    private ConnectionPoolWarmupService CreateService(ConnectionPoolWarmupOptions options)
    {
        return new ConnectionPoolWarmupService(
            _mockMetadataRegistry.Object,
            _mockProviderFactory.Object,
            _logger,
            Options.Create(options),
            _mockEnvironment.Object);
    }

    private MetadataSnapshot CreateTestSnapshot(int dataSourceCount)
    {
        var dataSources = new List<DataSourceDefinition>();
        for (int i = 0; i < dataSourceCount; i++)
        {
            dataSources.Add(new DataSourceDefinition
            {
                Id = $"datasource-{i}",
                Provider = "postgis",
                ConnectionString = $"Host=localhost;Database=test{i};Username=test;Password=test"
            });
        }

        return new MetadataSnapshot(
            new CatalogDefinition { Id = "test-catalog" },
            Array.Empty<FolderDefinition>(),
            dataSources,
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>());
    }

    private void SetupMetadataRegistry(MetadataSnapshot snapshot)
    {
        _mockMetadataRegistry.Setup(m => m.IsInitialized).Returns(true);
        _mockMetadataRegistry
            .Setup(m => m.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockMetadataRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
    }

    private void SetupProviderFactory()
    {
        var mockProvider = new Mock<IDataStoreProvider>();
        mockProvider
            .Setup(p => p.TestConnectivityAsync(It.IsAny<DataSourceDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockProviderFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(mockProvider.Object);
    }
}
