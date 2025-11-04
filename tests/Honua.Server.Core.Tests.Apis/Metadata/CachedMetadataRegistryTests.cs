using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("Redis")]
[Trait("Category", "Integration")]
public class CachedMetadataRegistryTests
{
    private readonly Mock<IMetadataRegistry> _mockInnerRegistry;
    private readonly IDistributedCache _distributedCache;
    private readonly MetadataCacheOptions _options;
    private readonly Mock<ILogger<CachedMetadataRegistry>> _mockLogger;
    private readonly MetadataCacheMetrics _metrics;

    public CachedMetadataRegistryTests()
    {
        _mockInnerRegistry = new Mock<IMetadataRegistry>();
        _distributedCache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _options = new MetadataCacheOptions
        {
            KeyPrefix = "test:metadata:",
            Ttl = TimeSpan.FromMinutes(5),
            WarmCacheOnStartup = false,
            EnableCompression = false // Disable for easier debugging
        };
        _mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();
        _metrics = new MetadataCacheMetrics();
    }

    private CachedMetadataRegistry CreateRegistry(
        IMetadataRegistry? innerRegistry = null,
        IDistributedCache? distributedCache = null,
        MetadataCacheOptions? options = null)
    {
        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options ?? _options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        return new CachedMetadataRegistry(
            innerRegistry ?? _mockInnerRegistry.Object,
            distributedCache ?? _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            _mockLogger.Object,
            retryPolicy: null,
            metrics: _metrics);
    }

    private static MetadataSnapshot CreateTestSnapshot()
    {
        return new MetadataSnapshot(
            catalog: new CatalogDefinition { Id = "test-catalog", Title = "Test Catalog" },
            folders: Array.Empty<FolderDefinition>(),
            dataSources: Array.Empty<DataSourceDefinition>(),
            services: Array.Empty<ServiceDefinition>(),
            layers: Array.Empty<LayerDefinition>());
    }

    [Fact]
    public async Task GetSnapshotAsync_CacheMiss_LoadsFromInnerRegistryAndCachesIt()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var registry = CreateRegistry();

        // Act
        var result = await registry.GetSnapshotAsync();

        // Assert
        result.Should().BeEquivalentTo(snapshot);
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Give async cache write time to complete
        await Task.Delay(100);

        // Verify metrics
        var stats = _metrics.GetStatistics();
        stats.Misses.Should().Be(1);
    }

    [Fact]
    public async Task GetSnapshotAsync_CacheHit_ReturnsCachedSnapshot()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var registry = CreateRegistry();

        // First call - cache miss
        await registry.GetSnapshotAsync();
        await Task.Delay(100); // Allow async cache write

        // Act - Second call - cache hit
        var result = await registry.GetSnapshotAsync();

        // Assert
        result.Catalog.Id.Should().Be("test-catalog");
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Verify metrics
        var stats = _metrics.GetStatistics();
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task GetSnapshotAsync_NullDistributedCache_BypassesCache()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var registry = CreateRegistry(distributedCache: null);

        // Act
        var result1 = await registry.GetSnapshotAsync();
        var result2 = await registry.GetSnapshotAsync();

        // Assert
        result1.Should().BeEquivalentTo(snapshot);
        result2.Should().BeEquivalentTo(snapshot);
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task GetSnapshotAsync_CacheExpired_LoadsFromInnerRegistry()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var shortTtlOptions = new MetadataCacheOptions
        {
            KeyPrefix = "test:metadata:",
            Ttl = TimeSpan.FromMilliseconds(100),
            WarmCacheOnStartup = false,
            EnableCompression = false
        };

        var registry = CreateRegistry(options: shortTtlOptions);

        // Act
        await registry.GetSnapshotAsync(); // Cache miss
        await Task.Delay(100); // Allow cache write

        await Task.Delay(150); // Wait for TTL expiration

        await registry.GetSnapshotAsync(); // Should be cache miss again

        // Assert
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ReloadAsync_InvalidatesCache()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _mockInnerRegistry.Setup(x => x.ReloadAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var registry = CreateRegistry();

        // First call - populate cache
        await registry.GetSnapshotAsync();
        await Task.Delay(100); // Allow cache write

        // Act - Reload invalidates cache
        await registry.ReloadAsync();

        // Second call after reload - should be cache miss
        await registry.GetSnapshotAsync();

        // Assert
        _mockInnerRegistry.Verify(x => x.ReloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task Update_InvalidatesCache()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var registry = CreateRegistry();

        // Pre-populate cache
        await registry.GetSnapshotAsync();
        await Task.Delay(100);

        // Act
        registry.Update(snapshot);

        // Assert
        _mockInnerRegistry.Verify(x => x.Update(snapshot), Times.Once);
    }

    [Fact]
    public async Task EnsureInitializedAsync_WarmCacheEnabled_WarmsCache()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _mockInnerRegistry.Setup(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var warmCacheOptions = new MetadataCacheOptions
        {
            KeyPrefix = "test:metadata:",
            WarmCacheOnStartup = true,
            EnableCompression = false
        };

        var registry = CreateRegistry(options: warmCacheOptions);

        // Act
        await registry.EnsureInitializedAsync();
        await Task.Delay(100); // Allow async warming

        // Assert
        _mockInnerRegistry.Verify(x => x.EnsureInitializedAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateCacheAsync_RemovesCachedSnapshot()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var registry = CreateRegistry();

        // Populate cache
        await registry.GetSnapshotAsync();
        await Task.Delay(100);

        // Act
        await registry.InvalidateCacheAsync();

        // Try to get snapshot again - should be cache miss
        await registry.GetSnapshotAsync();

        // Assert
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task GetSnapshotAsync_WithCompression_CompressesAndDecompresses()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var compressionOptions = new MetadataCacheOptions
        {
            KeyPrefix = "test:metadata:",
            EnableCompression = true,
            WarmCacheOnStartup = false
        };

        var registry = CreateRegistry(options: compressionOptions);

        // Act
        var result1 = await registry.GetSnapshotAsync(); // Cache miss
        await Task.Delay(100); // Allow cache write

        var result2 = await registry.GetSnapshotAsync(); // Cache hit (with decompression)

        // Assert
        result1.Catalog.Id.Should().Be("test-catalog");
        result2.Catalog.Id.Should().Be("test-catalog");
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Snapshot_ReturnsInnerRegistrySnapshot()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.Snapshot).Returns(snapshot);

        var registry = CreateRegistry();

        // Act
        var result = registry.Snapshot;

        // Assert
        result.Should().BeEquivalentTo(snapshot);
        _mockInnerRegistry.Verify(x => x.Snapshot, Times.Once);
    }

    [Fact]
    public void IsInitialized_ReturnsInnerRegistryIsInitialized()
    {
        // Arrange
        _mockInnerRegistry.Setup(x => x.IsInitialized).Returns(true);

        var registry = CreateRegistry();

        // Act
        var result = registry.IsInitialized;

        // Assert
        result.Should().BeTrue();
        _mockInnerRegistry.Verify(x => x.IsInitialized, Times.Once);
    }

    [Fact]
    public void GetChangeToken_ReturnsInnerRegistryChangeToken()
    {
        // Arrange
        var mockChangeToken = new Mock<Microsoft.Extensions.Primitives.IChangeToken>();
        _mockInnerRegistry.Setup(x => x.GetChangeToken()).Returns(mockChangeToken.Object);

        var registry = CreateRegistry();

        // Act
        var result = registry.GetChangeToken();

        // Assert
        result.Should().Be(mockChangeToken.Object);
        _mockInnerRegistry.Verify(x => x.GetChangeToken(), Times.Once);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public TestOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    [Fact]
    public async Task GetSnapshotAsync_FallbackEnabled_HandlesRedisFail()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _mockInnerRegistry.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var mockFailingCache = new Mock<IDistributedCache>();
        mockFailingCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

        var fallbackOptions = new MetadataCacheOptions
        {
            KeyPrefix = "test:metadata:",
            FallbackToDiskOnFailure = true,
            EnableCompression = false
        };

        var registry = CreateRegistry(distributedCache: mockFailingCache.Object, options: fallbackOptions);

        // Act
        var result = await registry.GetSnapshotAsync();

        // Assert
        result.Should().BeEquivalentTo(snapshot);
        _mockInnerRegistry.Verify(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MetadataCacheOptions_GetSnapshotCacheKey_ReturnsVersionedKey()
    {
        // Arrange
        var options = new MetadataCacheOptions
        {
            KeyPrefix = "honua:metadata:",
            SchemaVersion = 2
        };

        // Act
        var key = options.GetSnapshotCacheKey();

        // Assert
        key.Should().Be("honua:metadata:snapshot:v2");
    }

    [Fact]
    public void MetadataCacheMetrics_RecordOperations_TracksStatistics()
    {
        // Arrange
        var metrics = new MetadataCacheMetrics();

        // Act
        metrics.RecordCacheHit();
        metrics.RecordCacheHit();
        metrics.RecordCacheMiss();

        // Assert
        var stats = metrics.GetStatistics();
        stats.Hits.Should().Be(2);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.6667, 0.01);
    }

    [Fact]
    public void MetadataCacheMetrics_NoOperations_ReturnsZeroHitRate()
    {
        // Arrange
        var metrics = new MetadataCacheMetrics();

        // Act
        var hitRate = metrics.GetHitRate();

        // Assert
        hitRate.Should().Be(0.0);
    }
}
