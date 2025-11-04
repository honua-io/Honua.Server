using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Locking;
using Honua.Server.Core.Raster.Kerchunk;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Kerchunk;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class KerchunkReferenceStoreTests
{
    private readonly FakeKerchunkGenerator _fakeGenerator;
    private readonly FakeKerchunkCacheProvider _fakeCacheProvider;
    private readonly InMemoryDistributedLock _distributedLock;
    private readonly IOptions<HonuaConfiguration> _options;
    private readonly KerchunkReferenceStore _store;

    public KerchunkReferenceStoreTests()
    {
        _fakeGenerator = new FakeKerchunkGenerator();
        _fakeCacheProvider = new FakeKerchunkCacheProvider();
        _distributedLock = new InMemoryDistributedLock(NullLogger<InMemoryDistributedLock>.Instance);
        _options = Options.Create(new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "filesystem",
                Path = "metadata.json"
            },
            RasterCache = new RasterCacheConfiguration
            {
                EnableDistributedLocking = false,
                DistributedLockTimeout = TimeSpan.FromSeconds(5),
                DistributedLockExpiry = TimeSpan.FromSeconds(10)
            }
        });
        _store = new KerchunkReferenceStore(
            _fakeGenerator,
            _fakeCacheProvider,
            _distributedLock,
            _options,
            NullLogger<KerchunkReferenceStore>.Instance);
    }

    [Fact]
    public void Constructor_WithNullGenerator_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new KerchunkReferenceStore(
            null!,
            _fakeCacheProvider,
            _distributedLock,
            _options,
            NullLogger<KerchunkReferenceStore>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("generator");
    }

    [Fact]
    public void Constructor_WithNullCacheProvider_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new KerchunkReferenceStore(
            _fakeGenerator,
            null!,
            _distributedLock,
            _options,
            NullLogger<KerchunkReferenceStore>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new KerchunkReferenceStore(
            _fakeGenerator,
            _fakeCacheProvider,
            _distributedLock,
            _options,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task GetOrGenerateAsync_WithCacheHit_ShouldReturnCachedReferences()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        var options = new KerchunkGenerationOptions();
        var cachedRefs = new KerchunkReferences { SourceUri = sourceUri };

        _fakeCacheProvider.SetCachedReferences(cachedRefs);

        // Act
        var result = await _store.GetOrGenerateAsync(sourceUri, options);

        // Assert
        result.Should().Be(cachedRefs);
        _fakeGenerator.GenerateCallCount.Should().Be(0, "should not generate when cached");
    }

    [Fact]
    public async Task GetOrGenerateAsync_WithCacheMiss_ShouldGenerateAndCache()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        var options = new KerchunkGenerationOptions();
        var generatedRefs = new KerchunkReferences { SourceUri = sourceUri };

        _fakeGenerator.SetGeneratedReferences(generatedRefs);

        // Act
        var result = await _store.GetOrGenerateAsync(sourceUri, options);

        // Assert
        result.Should().Be(generatedRefs);
        _fakeGenerator.GenerateCallCount.Should().Be(1);
        _fakeCacheProvider.SetCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrGenerateAsync_WithConcurrentRequests_ShouldGenerateOnce()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        var options = new KerchunkGenerationOptions();
        var generatedRefs = new KerchunkReferences { SourceUri = sourceUri };

        _fakeGenerator.SetGeneratedReferences(generatedRefs);
        _fakeGenerator.GenerationDelay = TimeSpan.FromMilliseconds(100);

        // Act - Fire off 5 concurrent requests
        var tasks = new Task<KerchunkReferences>[5];
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = _store.GetOrGenerateAsync(sourceUri, options);
        }
        var results = await Task.WhenAll(tasks);

        // Assert - Should only generate once despite 5 concurrent requests
        _fakeGenerator.GenerateCallCount.Should().Be(1, "concurrent requests should be deduplicated");
        results.Should().AllSatisfy(r => r.Should().BeSameAs(generatedRefs));
    }

    [Fact]
    public async Task GenerateAsync_WithForceTrue_ShouldRegenerateEvenIfCached()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        var options = new KerchunkGenerationOptions();
        var cachedRefs = new KerchunkReferences { SourceUri = sourceUri, Version = "old" };
        var newRefs = new KerchunkReferences { SourceUri = sourceUri, Version = "new" };

        _fakeCacheProvider.SetCachedReferences(cachedRefs);
        _fakeGenerator.SetGeneratedReferences(newRefs);

        // Act
        var result = await _store.GenerateAsync(sourceUri, options, force: true);

        // Assert
        result.Should().Be(newRefs);
        _fakeGenerator.GenerateCallCount.Should().Be(1, "force flag should trigger generation");
    }

    [Fact]
    public async Task GenerateAsync_WithForceFalseAndCached_ShouldReturnCached()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        var options = new KerchunkGenerationOptions();
        var cachedRefs = new KerchunkReferences { SourceUri = sourceUri };

        _fakeCacheProvider.SetCachedReferences(cachedRefs);

        // Act
        var result = await _store.GenerateAsync(sourceUri, options, force: false);

        // Assert
        result.Should().Be(cachedRefs);
        _fakeGenerator.GenerateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExistsAsync_ShouldDelegateToCache()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";
        _fakeCacheProvider.SetExists(true);

        // Act
        var exists = await _store.ExistsAsync(sourceUri);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldDelegateToCache()
    {
        // Arrange
        var sourceUri = "s3://bucket/file.nc";

        // Act
        await _store.DeleteAsync(sourceUri);

        // Assert
        _fakeCacheProvider.DeleteCallCount.Should().Be(1);
    }

    // Fake implementations for testing

    private sealed class FakeKerchunkGenerator : IKerchunkGenerator
    {
        private KerchunkReferences? _referencesToReturn;
        public int GenerateCallCount { get; private set; }
        public TimeSpan GenerationDelay { get; set; } = TimeSpan.Zero;

        public void SetGeneratedReferences(KerchunkReferences refs)
        {
            _referencesToReturn = refs;
        }

        public async Task<KerchunkReferences> GenerateAsync(
            string sourceUri,
            KerchunkGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;

            if (GenerationDelay > TimeSpan.Zero)
            {
                await Task.Delay(GenerationDelay, cancellationToken);
            }

            return _referencesToReturn ?? new KerchunkReferences { SourceUri = sourceUri };
        }

        public bool CanHandle(string sourceUri) => true;
    }

    private sealed class FakeKerchunkCacheProvider : IKerchunkCacheProvider
    {
        private readonly Dictionary<string, KerchunkReferences> _cache = new();
        private KerchunkReferences? _defaultCachedReferences;
        private bool _exists;

        public int SetCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public void SetCachedReferences(KerchunkReferences refs)
        {
            _defaultCachedReferences = refs;
        }

        public void SetExists(bool exists)
        {
            _exists = exists;
        }

        public Task<KerchunkReferences?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return Task.FromResult<KerchunkReferences?>(cached);
            }

            return Task.FromResult(_defaultCachedReferences);
        }

        public Task SetAsync(
            string key,
            KerchunkReferences references,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            _cache[key] = references;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_exists || _cache.ContainsKey(key));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
