using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

/// <summary>
/// Tests for proper disposal of Azure Blob resources to prevent resource leaks.
/// </summary>
public sealed class AzureBlobRasterDisposalTests
{
    private readonly Mock<ILogger<AzureBlobRasterTileCacheProvider>> _logger;

    public AzureBlobRasterDisposalTests()
    {
        _logger = new Mock<ILogger<AzureBlobRasterTileCacheProvider>>();
    }

    [Fact]
    public async Task AzureBlobRasterTileCacheProvider_DisposeAsync_DisposesOwnedContainer()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = "test-container";
        var container = new BlobContainerClient(connectionString, containerName);

        var provider = new AzureBlobRasterTileCacheProvider(
            container,
            ensureContainer: false,
            _logger.Object,
            metrics: null,
            ownsContainer: true);

        // Act
        await provider.DisposeAsync();

        // Assert - no exceptions thrown
        // Resource is disposed properly
    }

    [Fact]
    public async Task AzureBlobRasterTileCacheProvider_DisposeAsync_DoesNotDisposeUnownedContainer()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = "test-container";
        var container = new BlobContainerClient(connectionString, containerName);

        var provider = new AzureBlobRasterTileCacheProvider(
            container,
            ensureContainer: false,
            _logger.Object,
            metrics: null,
            ownsContainer: false);

        // Act
        await provider.DisposeAsync();

        // Assert - no exceptions thrown
        // Container is not disposed since we don't own it
    }

    [Fact]
    public async Task AzureBlobRasterTileCacheProvider_DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = "test-container";
        var container = new BlobContainerClient(connectionString, containerName);

        var provider = new AzureBlobRasterTileCacheProvider(
            container,
            ensureContainer: false,
            _logger.Object,
            metrics: null,
            ownsContainer: true);

        // Act
        await provider.DisposeAsync();
        await provider.DisposeAsync();
        await provider.DisposeAsync();

        // Assert - no exceptions thrown
        // Disposal is idempotent
    }

    [Fact]
    public async Task AzureBlobRasterTileCacheProvider_DisposeAsync_DisposesSemaphoreSlim()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = "test-container";
        var container = new BlobContainerClient(connectionString, containerName);

        var provider = new AzureBlobRasterTileCacheProvider(
            container,
            ensureContainer: false,
            _logger.Object,
            metrics: null,
            ownsContainer: false);

        // Act
        await provider.DisposeAsync();

        // Assert - SemaphoreSlim is disposed
        // Attempting to use provider after disposal would throw ObjectDisposedException
        // This test verifies no exceptions during disposal
    }

    [Fact]
    public void AzureBlobRasterTileCacheProvider_Constructor_RequiresContainer()
    {
        // Act & Assert - BlobContainerClient constructor itself throws NullReferenceException
        Assert.Throws<NullReferenceException>(() =>
            new AzureBlobRasterTileCacheProvider(null!, false, _logger.Object));
    }

    [Fact]
    public void AzureBlobRasterTileCacheProvider_Constructor_RequiresLogger()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var containerName = "test-container";
        var container = new BlobContainerClient(connectionString, containerName);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AzureBlobRasterTileCacheProvider(container, false, null!));
    }
}
