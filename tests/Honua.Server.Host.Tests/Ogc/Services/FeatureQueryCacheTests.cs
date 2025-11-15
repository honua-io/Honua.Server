// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Host.Ogc.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class FeatureQueryCacheTests
{
    private readonly FeatureQueryCacheOptions _options;
    private readonly FeatureQueryCacheMetrics _metrics;

    public FeatureQueryCacheTests()
    {
        _options = new FeatureQueryCacheOptions
        {
            Enabled = true,
            TtlSeconds = 300,
            InvalidateOnWrite = true
        };
        _metrics = new FeatureQueryCacheMetrics();
    }

    [Fact]
    public void GenerateCacheKey_WithAllParameters_GeneratesConsistentKey()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var serviceId = "wfs-service";
        var layerId = "buildings";
        var bbox = "-122.5,37.7,-122.4,37.8";
        var filter = "type eq 'residential'";
        var parameters = new Dictionary<string, string> { ["color"] = "blue", ["size"] = "large" };

        // Act
        var key1 = cache.GenerateCacheKey(serviceId, layerId, bbox, filter, parameters);
        var key2 = cache.GenerateCacheKey(serviceId, layerId, bbox, filter, parameters);

        // Assert
        Assert.NotNull(key1);
        Assert.Equal(key1, key2); // Should be deterministic
        Assert.Contains(serviceId, key1);
        Assert.Contains(layerId, key1);
    }

    [Fact]
    public void GenerateCacheKey_WithDifferentFilters_GeneratesDifferentKeys()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var serviceId = "wfs-service";
        var layerId = "buildings";

        // Act
        var key1 = cache.GenerateCacheKey(serviceId, layerId, null, "type eq 'residential'", null);
        var key2 = cache.GenerateCacheKey(serviceId, layerId, null, "type eq 'commercial'", null);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateCacheKey_WithNullParameters_UsesDefaults()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var serviceId = "wfs-service";
        var layerId = "buildings";

        // Act
        var key = cache.GenerateCacheKey(serviceId, layerId, null, null, null);

        // Assert
        Assert.NotNull(key);
        Assert.Contains("no-bbox", key);
        Assert.Contains("no-filter", key);
        Assert.Contains("no-params", key);
    }

    [Fact]
    public async Task GetAsync_WhenNotCached_ReturnsNull()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var cacheKey = "features:service:layer:bbox:filter:params";

        // Act
        var result = await cache.GetAsync(cacheKey);

        // Assert
        Assert.Null(result);
        Assert.Equal(1, _metrics.CacheMisses);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var cacheKey = "features:service:layer:bbox:filter:params";
        var features = @"{""type"":""FeatureCollection"",""features"":[]}";

        // Act
        await cache.SetAsync(cacheKey, features, TimeSpan.FromSeconds(60));
        var result = await cache.GetAsync(cacheKey);

        // Assert
        Assert.Equal(features, result);
        Assert.Equal(1, _metrics.CacheHits);
        Assert.Equal(1, _metrics.CacheSet);
    }

    [Fact]
    public async Task SetAsync_WithExpiration_ExpiresCacheEntry()
    {
        // Arrange
        var cache = CreateInMemoryCache();
        var cacheKey = "features:service:layer:bbox:filter:params";
        var features = @"{""type"":""FeatureCollection"",""features"":[]}";

        // Act
        await cache.SetAsync(cacheKey, features, TimeSpan.FromMilliseconds(100));
        var immediateResult = await cache.GetAsync(cacheKey);

        // Wait for expiration
        await Task.Delay(200);
        var expiredResult = await cache.GetAsync(cacheKey);

        // Assert
        Assert.NotNull(immediateResult);
        Assert.Null(expiredResult);
    }

    [Fact]
    public void GetEffectiveTtl_WithNoOverrides_ReturnsDefault()
    {
        // Arrange
        var cache = CreateInMemoryCache();

        // Act
        var ttl = cache.GetEffectiveTtl("service1", "layer1");

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(300), ttl);
    }

    [Fact]
    public void GetEffectiveTtl_WithServiceOverride_ReturnsServiceTtl()
    {
        // Arrange
        _options.ServiceTtlOverrides["service1"] = 600;
        var cache = CreateInMemoryCache();

        // Act
        var ttl = cache.GetEffectiveTtl("service1", "layer1");

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(600), ttl);
    }

    [Fact]
    public void GetEffectiveTtl_WithLayerOverride_ReturnsLayerTtl()
    {
        // Arrange
        _options.ServiceTtlOverrides["service1"] = 600;
        _options.LayerTtlOverrides["service1:layer1"] = 900;
        var cache = CreateInMemoryCache();

        // Act
        var ttl = cache.GetEffectiveTtl("service1", "layer1");

        // Assert - Layer override takes precedence
        Assert.Equal(TimeSpan.FromSeconds(900), ttl);
    }

    [Fact]
    public void Metrics_TrackCacheHitsAndMisses()
    {
        // Arrange
        var metrics = new FeatureQueryCacheMetrics();

        // Act
        metrics.RecordCacheHit();
        metrics.RecordCacheHit();
        metrics.RecordCacheMiss();

        // Assert
        Assert.Equal(2, metrics.CacheHits);
        Assert.Equal(1, metrics.CacheMisses);
        Assert.Equal(2.0 / 3.0, metrics.HitRate);
    }

    [Fact]
    public void Metrics_TrackBytesStored()
    {
        // Arrange
        var metrics = new FeatureQueryCacheMetrics();

        // Act
        metrics.RecordCacheSet(1024);
        metrics.RecordCacheSet(2048);

        // Assert
        Assert.Equal(3072, metrics.TotalBytesStored);
    }

    [Fact]
    public void Metrics_Reset_ClearsAllCounters()
    {
        // Arrange
        var metrics = new FeatureQueryCacheMetrics();
        metrics.RecordCacheHit();
        metrics.RecordCacheMiss();
        metrics.RecordCacheSet(1024);
        metrics.RecordCacheError();

        // Act
        metrics.Reset();

        // Assert
        Assert.Equal(0, metrics.CacheHits);
        Assert.Equal(0, metrics.CacheMisses);
        Assert.Equal(0, metrics.TotalBytesStored);
        Assert.Equal(0, metrics.CacheErrors);
    }

    [Fact]
    public async Task Cache_WhenDisabled_DoesNotCache()
    {
        // Arrange
        _options.Enabled = false;
        var cache = CreateInMemoryCache();
        var cacheKey = "features:service:layer:bbox:filter:params";
        var features = @"{""type"":""FeatureCollection"",""features"":[]}";

        // Act
        await cache.SetAsync(cacheKey, features, TimeSpan.FromSeconds(60));
        var result = await cache.GetAsync(cacheKey);

        // Assert
        Assert.Null(result);
    }

    private InMemoryFeatureQueryCache CreateInMemoryCache()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 * 1024 * 1024 // 100 MB
        });

        return new InMemoryFeatureQueryCache(
            memoryCache,
            Options.Create(_options),
            NullLogger<InMemoryFeatureQueryCache>.Instance,
            _metrics);
    }
}
