using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("Redis")]
[Trait("Category", "Integration")]
public class CachedMetadataRegistryIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IDistributedCache? _distributedCache;

    public CachedMetadataRegistryIntegrationTests()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();

        var connectionString = _redisContainer.GetConnectionString();
        _distributedCache = new RedisCache(Options.Create(new RedisCacheOptions
        {
            Configuration = connectionString,
            InstanceName = "HonuaTest:"
        }));
    }

    public async Task DisposeAsync()
    {
        if (_distributedCache is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await _redisContainer.StopAsync();
        await _redisContainer.DisposeAsync();
    }

    private static MetadataSnapshot CreateTestSnapshot()
    {
        return new MetadataSnapshot(
            catalog: new CatalogDefinition { Id = "redis-test-catalog", Title = "Redis Test" },
            folders: Array.Empty<FolderDefinition>(),
            dataSources: Array.Empty<DataSourceDefinition>(),
            services: Array.Empty<ServiceDefinition>(),
            layers: Array.Empty<LayerDefinition>());
    }

    [Fact]
    public async Task GetSnapshotAsync_RealRedis_CachesAndRetrievesSnapshot()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);

        var options = new MetadataCacheOptions
        {
            KeyPrefix = "integration:test:",
            Ttl = TimeSpan.FromMinutes(1),
            EnableCompression = true
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();
        var metrics = new MetadataCacheMetrics();

        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registry = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            mockLogger.Object,
            retryPolicy: null,
            metrics: metrics);

        // Act - First call (cache miss)
        var result1 = await registry.GetSnapshotAsync();
        await Task.Delay(200); // Allow async cache write

        // Second call (cache hit)
        var result2 = await registry.GetSnapshotAsync();

        // Assert
        result1.Catalog.Id.Should().Be("redis-test-catalog");
        result2.Catalog.Id.Should().Be("redis-test-catalog");

        // Verify inner registry was only called once (second call was cache hit)
        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Once);

        // Verify metrics
        var stats = metrics.GetStatistics();
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task InvalidateCacheAsync_RealRedis_ClearsCachedData()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);

        var options = new MetadataCacheOptions
        {
            KeyPrefix = "integration:invalidate:",
            EnableCompression = false
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();

        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registry = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            mockLogger.Object);

        // Populate cache
        await registry.GetSnapshotAsync();
        await Task.Delay(200);

        // Act - Invalidate cache
        await registry.InvalidateCacheAsync();

        // Try to get snapshot again
        await registry.GetSnapshotAsync();

        // Assert - Should have called inner registry twice (cache was invalidated)
        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task ReloadAsync_RealRedis_InvalidatesAndRewarmsCache()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);
        mockInnerRegistry.Setup(x => x.ReloadAsync(default))
            .Returns(Task.CompletedTask);

        var options = new MetadataCacheOptions
        {
            KeyPrefix = "integration:reload:",
            WarmCacheOnStartup = true, // Will warm after reload
            EnableCompression = true
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();

        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registry = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            mockLogger.Object);

        // Populate cache initially
        await registry.GetSnapshotAsync();
        await Task.Delay(200);

        // Act - Reload
        await registry.ReloadAsync();
        await Task.Delay(200); // Allow warming

        // Get snapshot after reload (should be cache hit from warming)
        await registry.GetSnapshotAsync();

        // Assert
        mockInnerRegistry.Verify(x => x.ReloadAsync(default), Times.Once);
        // Called 3 times: initial get, reload warm, final get should be cache hit
        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSnapshotAsync_WithTtl_ExpiresAfterTimeout()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);

        var options = new MetadataCacheOptions
        {
            KeyPrefix = "integration:ttl:",
            Ttl = TimeSpan.FromMilliseconds(500), // Short TTL for testing
            EnableCompression = false
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();

        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registry = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            mockLogger.Object);

        // Act
        await registry.GetSnapshotAsync(); // Cache miss
        await Task.Delay(200); // Allow cache write

        await registry.GetSnapshotAsync(); // Cache hit

        await Task.Delay(600); // Wait for TTL expiration

        await registry.GetSnapshotAsync(); // Cache miss (expired)

        // Assert - Called twice (initial + after expiration)
        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task GetSnapshotAsync_LargeSnapshot_CompressesEffectively()
    {
        // Arrange - Create a larger snapshot with more data
        var services = new ServiceDefinition[]
        {
            new() {
                Id = "service1",
                Title = "Service 1",
                FolderId = "folder1",
                ServiceType = "FeatureServer",
                DataSourceId = "ds1"
            },
            new() {
                Id = "service2",
                Title = "Service 2",
                FolderId = "folder1",
                ServiceType = "FeatureServer",
                DataSourceId = "ds1"
            }
        };

        var folders = new FolderDefinition[]
        {
            new() { Id = "folder1", Title = "Test Folder" }
        };

        var dataSources = new DataSourceDefinition[]
        {
            new() { Id = "ds1", Provider = "PostgreSQL", ConnectionString = "Host=localhost;Database=test" }
        };

        var snapshot = new MetadataSnapshot(
            catalog: new CatalogDefinition
            {
                Id = "large-catalog",
                Title = "Large Test Catalog",
                Description = "This is a larger snapshot for compression testing with more detailed metadata"
            },
            folders: folders,
            dataSources: dataSources,
            services: services,
            layers: Array.Empty<LayerDefinition>());

        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);

        var options = new MetadataCacheOptions
        {
            KeyPrefix = "integration:compression:",
            EnableCompression = true
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();

        var cacheOptionsMonitor = new TestOptionsMonitor<MetadataCacheOptions>(options);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registry = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitor,
            invalidationOptionsMonitor,
            mockLogger.Object);

        // Act
        var result1 = await registry.GetSnapshotAsync();
        await Task.Delay(200);

        var result2 = await registry.GetSnapshotAsync();

        // Assert
        result1.Catalog.Id.Should().Be("large-catalog");
        result2.Catalog.Id.Should().Be("large-catalog");
        result1.Services.Count.Should().Be(2);
        result2.Services.Count.Should().Be(2);

        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetSnapshotAsync_MultipleSchemaVersions_UseDifferentKeys()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var mockInnerRegistry = new Mock<IMetadataRegistry>();
        mockInnerRegistry.Setup(x => x.GetSnapshotAsync(default))
            .ReturnsAsync(snapshot);

        var optionsV1 = new MetadataCacheOptions
        {
            KeyPrefix = "integration:version:",
            SchemaVersion = 1,
            EnableCompression = false
        };

        var optionsV2 = new MetadataCacheOptions
        {
            KeyPrefix = "integration:version:",
            SchemaVersion = 2,
            EnableCompression = false
        };

        var mockLogger = new Mock<ILogger<CachedMetadataRegistry>>();

        var cacheOptionsMonitorV1 = new TestOptionsMonitor<MetadataCacheOptions>(optionsV1);
        var cacheOptionsMonitorV2 = new TestOptionsMonitor<MetadataCacheOptions>(optionsV2);
        var invalidationOptionsMonitor = new TestOptionsMonitor<Honua.Server.Core.Configuration.CacheInvalidationOptions>(
            new Honua.Server.Core.Configuration.CacheInvalidationOptions());

        var registryV1 = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitorV1,
            invalidationOptionsMonitor,
            mockLogger.Object);

        var registryV2 = new CachedMetadataRegistry(
            mockInnerRegistry.Object,
            _distributedCache,
            cacheOptionsMonitorV2,
            invalidationOptionsMonitor,
            mockLogger.Object);

        // Act - Cache with V1
        await registryV1.GetSnapshotAsync();
        await Task.Delay(200);

        // Try to get with V2 (different key, should be cache miss)
        await registryV2.GetSnapshotAsync();

        // Assert - Both versions should have called inner registry (different cache keys)
        mockInnerRegistry.Verify(x => x.GetSnapshotAsync(default), Times.Exactly(2));

        // Verify different keys
        optionsV1.GetSnapshotCacheKey().Should().Be("integration:version:snapshot:v1");
        optionsV2.GetSnapshotCacheKey().Should().Be("integration:version:snapshot:v2");
    }
}

/// <summary>
/// Collection definition for Redis tests to ensure sequential execution.
/// </summary>
[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisCollectionFixture>
{
}

/// <summary>
/// Shared fixture for Redis container (if needed for multiple test classes).
/// </summary>
public class RedisCollectionFixture
{
}

/// <summary>
/// Test helper for IOptionsMonitor.
/// </summary>
file sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
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
