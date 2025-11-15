// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authorization;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authorization;

/// <summary>
/// Comprehensive test coverage for ResourceAuthorizationCache - a critical security component.
/// Tests cover cache operations, invalidation, TTL handling, concurrency, and error handling.
/// </summary>
public class ResourceAuthorizationCacheTests : IDisposable
{
    private readonly Mock<ILogger<ResourceAuthorizationCache>> _mockLogger;
    private readonly Mock<IOptionsMonitor<ResourceAuthorizationOptions>> _mockOptions;
    private readonly IMemoryCache _memoryCache;
    private readonly ResourceAuthorizationOptions _options;

    public ResourceAuthorizationCacheTests()
    {
        _mockLogger = new Mock<ILogger<ResourceAuthorizationCache>>();
        _mockOptions = new Mock<IOptionsMonitor<ResourceAuthorizationOptions>>();

        _options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 300, // 5 minutes default
            MaxCacheSize = 1000
        };

        _mockOptions.Setup(x => x.CurrentValue).Returns(_options);

        // Create a real memory cache with size limit
        var memoryCacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 10000
        };
        _memoryCache = new MemoryCache(memoryCacheOptions);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ResourceAuthorizationCache(null!, _mockLogger.Object, _mockOptions.Object));

        exception.ParamName.Should().Be("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ResourceAuthorizationCache(_memoryCache, null!, _mockOptions.Object));

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, null!));

        exception.ParamName.Should().Be("options");
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Act
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Assert
        cache.Should().NotBeNull();
        var stats = cache.GetStatistics();
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.EntryCount.Should().Be(0);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WithNonExistentKey_ReturnsFalseAndIncrementsMetrics()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";

        // Act
        var result = cache.TryGet(cacheKey, out var authResult);

        // Assert
        result.Should().BeFalse();
        authResult.Should().BeNull();

        var stats = cache.GetStatistics();
        stats.Misses.Should().Be(1);
        stats.Hits.Should().Be(0);
    }

    [Fact]
    public void TryGet_WithExistingKey_ReturnsTrueAndIncrementsMetrics()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";
        var expectedResult = ResourceAuthorizationResult.Success();

        cache.Set(cacheKey, expectedResult);

        // Act
        var result = cache.TryGet(cacheKey, out var authResult);

        // Assert
        result.Should().BeTrue();
        authResult.Should().NotBeNull();
        authResult!.Succeeded.Should().BeTrue();
        authResult.FromCache.Should().BeTrue();

        var stats = cache.GetStatistics();
        stats.Hits.Should().Be(1);
    }

    [Fact]
    public void TryGet_CalledMultipleTimes_IncrementsMetricsCorrectly()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";

        cache.Set(cacheKey, ResourceAuthorizationResult.Success());

        // Act - 3 hits, 2 misses
        cache.TryGet(cacheKey, out _);
        cache.TryGet(cacheKey, out _);
        cache.TryGet(cacheKey, out _);
        cache.TryGet("nonexistent-key-1", out _);
        cache.TryGet("nonexistent-key-2", out _);

        // Assert
        var stats = cache.GetStatistics();
        stats.Hits.Should().Be(3);
        stats.Misses.Should().Be(2);
        stats.HitRate.Should().BeApproximately(0.6, 0.01); // 3/5 = 0.6
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_WithValidKeyAndResult_StoresInCache()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";
        var result = ResourceAuthorizationResult.Success();

        // Act
        cache.Set(cacheKey, result);

        // Assert
        var retrieved = cache.TryGet(cacheKey, out var authResult);
        retrieved.Should().BeTrue();
        authResult!.Succeeded.Should().BeTrue();
        authResult.FromCache.Should().BeTrue();
    }

    [Fact]
    public void Set_WithFailureResult_StoresFailureCorrectly()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:write:user-456";
        var result = ResourceAuthorizationResult.Fail("Access denied");

        // Act
        cache.Set(cacheKey, result);

        // Assert
        var retrieved = cache.TryGet(cacheKey, out var authResult);
        retrieved.Should().BeTrue();
        authResult!.Succeeded.Should().BeFalse();
        authResult.FailureReason.Should().Be("Access denied");
        authResult.FromCache.Should().BeTrue();
    }

    [Fact]
    public void Set_MarksResultAsFromCache()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";
        var result = ResourceAuthorizationResult.Success(fromCache: false);

        // Act
        cache.Set(cacheKey, result);
        cache.TryGet(cacheKey, out var retrievedResult);

        // Assert
        retrievedResult!.FromCache.Should().BeTrue();
    }

    [Fact]
    public void Set_UpdatesEntryCount()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act
        cache.Set("key1", ResourceAuthorizationResult.Success());
        cache.Set("key2", ResourceAuthorizationResult.Success());
        cache.Set("key3", ResourceAuthorizationResult.Success());

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(3);
    }

    [Fact]
    public void Set_WithSameKeyMultipleTimes_UpdatesValue()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";

        // Act - Set success, then failure
        cache.Set(cacheKey, ResourceAuthorizationResult.Success());
        cache.Set(cacheKey, ResourceAuthorizationResult.Fail("Updated to deny"));

        // Assert
        cache.TryGet(cacheKey, out var result);
        result!.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be("Updated to deny");

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1); // Only one entry
    }

    #endregion

    #region Cache Eviction Tests

    [Fact]
    public void Set_WhenMaxCacheSizeReached_EvictsOldestEntry()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 300,
            MaxCacheSize = 3 // Small limit for testing
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act - Add 4 entries (exceeds max of 3)
        cache.Set("key1", ResourceAuthorizationResult.Success());
        cache.Set("key2", ResourceAuthorizationResult.Success());
        cache.Set("key3", ResourceAuthorizationResult.Success());
        cache.Set("key4", ResourceAuthorizationResult.Success()); // Should evict key1

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(3);
        stats.Evictions.Should().BeGreaterOrEqualTo(1);

        // key4 should definitely be present (just added)
        cache.TryGet("key4", out _).Should().BeTrue();

        // At least one of the earlier entries should be evicted
        var key1Exists = cache.TryGet("key1", out _);
        var key2Exists = cache.TryGet("key2", out _);
        var key3Exists = cache.TryGet("key3", out _);

        // Exactly 2 of the first 3 keys should exist
        var existingCount = (key1Exists ? 1 : 0) + (key2Exists ? 1 : 0) + (key3Exists ? 1 : 0);
        existingCount.Should().Be(2);
    }

    [Fact]
    public void Set_WhenMaxCacheSizeIsZero_NoEvictionOccurs()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 300,
            MaxCacheSize = 0 // No limit
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act - Add many entries
        for (int i = 0; i < 100; i++)
        {
            cache.Set($"key{i}", ResourceAuthorizationResult.Success());
        }

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(100);
        stats.Evictions.Should().Be(0);
    }

    [Fact]
    public void Set_EvictionCallback_RemovesFromCacheKeys()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 300,
            MaxCacheSize = 2
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act
        cache.Set("key1", ResourceAuthorizationResult.Success());
        cache.Set("key2", ResourceAuthorizationResult.Success());
        cache.Set("key3", ResourceAuthorizationResult.Success()); // Evicts key1

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(2); // Only 2 entries remain
    }

    #endregion

    #region TTL and Expiration Tests

    [Fact]
    public async Task Set_WithTTL_EntriesExpireAfterDuration()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 1, // 1 second for fast testing
            MaxCacheSize = 1000
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";

        // Act
        cache.Set(cacheKey, ResourceAuthorizationResult.Success());

        // Immediately should be available
        cache.TryGet(cacheKey, out _).Should().BeTrue();

        // Wait for expiration
        await Task.Delay(1200); // Wait slightly longer than TTL

        // Assert
        cache.TryGet(cacheKey, out _).Should().BeFalse();
    }

    [Fact]
    public void Set_AppliesAbsoluteExpiration()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 600, // 10 minutes
            MaxCacheSize = 1000
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var cacheKey = "authz:layer:layer-1:read:user-123";

        // Act
        cache.Set(cacheKey, ResourceAuthorizationResult.Success());

        // Assert - Entry should exist immediately
        cache.TryGet(cacheKey, out var result).Should().BeTrue();
        result.Should().NotBeNull();
    }

    #endregion

    #region InvalidateResource Tests

    [Fact]
    public void InvalidateResource_WithMatchingPrefix_RemovesAllMatchingEntries()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Add entries for different users on same resource
        cache.Set("authz:layer:layer-1:read:user-1", ResourceAuthorizationResult.Success());
        cache.Set("authz:layer:layer-1:write:user-1", ResourceAuthorizationResult.Success());
        cache.Set("authz:layer:layer-1:read:user-2", ResourceAuthorizationResult.Success());
        cache.Set("authz:layer:layer-2:read:user-1", ResourceAuthorizationResult.Success());

        // Act - Invalidate all entries for layer-1
        cache.InvalidateResource("layer", "layer-1");

        // Assert
        cache.TryGet("authz:layer:layer-1:read:user-1", out _).Should().BeFalse();
        cache.TryGet("authz:layer:layer-1:write:user-1", out _).Should().BeFalse();
        cache.TryGet("authz:layer:layer-1:read:user-2", out _).Should().BeFalse();
        cache.TryGet("authz:layer:layer-2:read:user-1", out _).Should().BeTrue(); // Different resource

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1);
    }

    [Fact]
    public void InvalidateResource_WithNoMatchingEntries_DoesNothing()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        cache.Set("authz:layer:layer-1:read:user-1", ResourceAuthorizationResult.Success());

        // Act
        cache.InvalidateResource("collection", "collection-999");

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1); // Original entry still exists
    }

    [Fact]
    public void InvalidateResource_WithDifferentResourceTypes_OnlyRemovesMatching()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        cache.Set("authz:layer:resource-1:read:user-1", ResourceAuthorizationResult.Success());
        cache.Set("authz:collection:resource-1:read:user-1", ResourceAuthorizationResult.Success());

        // Act
        cache.InvalidateResource("layer", "resource-1");

        // Assert
        cache.TryGet("authz:layer:resource-1:read:user-1", out _).Should().BeFalse();
        cache.TryGet("authz:collection:resource-1:read:user-1", out _).Should().BeTrue();
    }

    #endregion

    #region InvalidateAll Tests

    [Fact]
    public void InvalidateAll_RemovesAllEntries()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        cache.Set("authz:layer:layer-1:read:user-1", ResourceAuthorizationResult.Success());
        cache.Set("authz:layer:layer-2:read:user-1", ResourceAuthorizationResult.Success());
        cache.Set("authz:collection:col-1:read:user-2", ResourceAuthorizationResult.Success());

        // Act
        cache.InvalidateAll();

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(0);

        cache.TryGet("authz:layer:layer-1:read:user-1", out _).Should().BeFalse();
        cache.TryGet("authz:layer:layer-2:read:user-1", out _).Should().BeFalse();
        cache.TryGet("authz:collection:col-1:read:user-2", out _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateAll_WithEmptyCache_DoesNotThrow()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        var act = () => cache.InvalidateAll();
        act.Should().NotThrow();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(0);
    }

    [Fact]
    public void InvalidateAll_ResetsEntryCountButNotMetrics()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        cache.Set("key1", ResourceAuthorizationResult.Success());
        cache.TryGet("key1", out _);
        cache.TryGet("nonexistent", out _);

        var statsBefore = cache.GetStatistics();
        statsBefore.Hits.Should().Be(1);
        statsBefore.Misses.Should().Be(1);

        // Act
        cache.InvalidateAll();

        // Assert
        var statsAfter = cache.GetStatistics();
        statsAfter.EntryCount.Should().Be(0);
        statsAfter.Hits.Should().Be(1); // Metrics are not reset
        statsAfter.Misses.Should().Be(1);
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_ReturnsCorrectInitialState()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.Evictions.Should().Be(0);
        stats.EntryCount.Should().Be(0);
        stats.MaxEntries.Should().Be(_options.MaxCacheSize);
        stats.HitRate.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_CalculatesHitRateCorrectly()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        cache.Set("key1", ResourceAuthorizationResult.Success());

        // Act - 7 hits, 3 misses
        for (int i = 0; i < 7; i++)
        {
            cache.TryGet("key1", out _);
        }
        for (int i = 0; i < 3; i++)
        {
            cache.TryGet($"nonexistent-{i}", out _);
        }

        var stats = cache.GetStatistics();

        // Assert
        stats.Hits.Should().Be(7);
        stats.Misses.Should().Be(3);
        stats.HitRate.Should().BeApproximately(0.7, 0.01); // 7/10 = 0.7
    }

    [Fact]
    public void GetStatistics_WithNoAccesses_HitRateIsZero()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.HitRate.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_ReflectsCurrentEntryCount()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act & Assert - Add entries with proper format
        var key1 = CacheKeyGenerator.GenerateAuthorizationKey("user-1", "layer", "layer-1", "read");
        var key2 = CacheKeyGenerator.GenerateAuthorizationKey("user-2", "layer", "layer-2", "read");

        cache.Set(key1, ResourceAuthorizationResult.Success());
        cache.GetStatistics().EntryCount.Should().Be(1);

        cache.Set(key2, ResourceAuthorizationResult.Success());
        cache.GetStatistics().EntryCount.Should().Be(2);

        // Remove one by invalidating layer-1
        cache.InvalidateResource("layer", "layer-1");
        cache.GetStatistics().EntryCount.Should().Be(1);
    }

    #endregion

    #region BuildCacheKey Tests

    [Fact]
    public void BuildCacheKey_GeneratesCorrectFormat()
    {
        // Act
        var key = ResourceAuthorizationCache.BuildCacheKey("user-123", "layer", "layer-1", "read");

        // Assert
        key.Should().Be("authz:layer:layer-1:read:user-123");
    }

    [Fact]
    public void BuildCacheKey_WithDifferentResourceTypes_GeneratesDifferentKeys()
    {
        // Act
        var layerKey = ResourceAuthorizationCache.BuildCacheKey("user-123", "layer", "resource-1", "read");
        var collectionKey = ResourceAuthorizationCache.BuildCacheKey("user-123", "collection", "resource-1", "read");

        // Assert
        layerKey.Should().NotBe(collectionKey);
    }

    [Fact]
    public void BuildCacheKey_WithDifferentOperations_GeneratesDifferentKeys()
    {
        // Act
        var readKey = ResourceAuthorizationCache.BuildCacheKey("user-123", "layer", "layer-1", "read");
        var writeKey = ResourceAuthorizationCache.BuildCacheKey("user-123", "layer", "layer-1", "write");

        // Assert
        readKey.Should().NotBe(writeKey);
    }

    [Fact]
    public void BuildCacheKey_WithDifferentUsers_GeneratesDifferentKeys()
    {
        // Act
        var user1Key = ResourceAuthorizationCache.BuildCacheKey("user-1", "layer", "layer-1", "read");
        var user2Key = ResourceAuthorizationCache.BuildCacheKey("user-2", "layer", "layer-1", "read");

        // Assert
        user1Key.Should().NotBe(user2Key);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public void ConcurrentAccess_MultipleThreadsReading_ThreadSafe()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        cache.Set("shared-key", ResourceAuthorizationResult.Success());
        var tasks = new List<Task>();
        var successCount = 0;
        var lockObj = new object();

        // Act - Multiple threads reading simultaneously
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (cache.TryGet("shared-key", out var result))
                {
                    lock (lockObj)
                    {
                        successCount++;
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        successCount.Should().Be(100);
        var stats = cache.GetStatistics();
        stats.Hits.Should().Be(100);
    }

    [Fact]
    public void ConcurrentAccess_MultipleThreadsWriting_ThreadSafe()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var tasks = new List<Task>();

        // Act - Multiple threads writing simultaneously
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.Set($"key-{index}", ResourceAuthorizationResult.Success());
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(100);
    }

    [Fact]
    public void ConcurrentAccess_MixedReadWrite_ThreadSafe()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var tasks = new List<Task>();

        // Act - Mix of reads and writes
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => cache.Set($"key-{index}", ResourceAuthorizationResult.Success())));
            tasks.Add(Task.Run(() => cache.TryGet($"key-{index}", out _)));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Should not throw and maintain consistency
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConcurrentAccess_InvalidateWhileReading_ThreadSafe()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        for (int i = 0; i < 50; i++)
        {
            cache.Set($"authz:layer:layer-1:read:user-{i}", ResourceAuthorizationResult.Success());
        }

        var tasks = new List<Task>();

        // Act - Some threads reading, one invalidating
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => cache.TryGet($"authz:layer:layer-1:read:user-{index}", out _)));
        }

        tasks.Add(Task.Run(() => cache.InvalidateResource("layer", "layer-1")));

        // Assert - Should not throw
        var act = () => Task.WaitAll(tasks.ToArray());
        act.Should().NotThrow();
    }

    #endregion

    #region Metrics Integrity Tests

    [Fact]
    public void Metrics_IncrementAtomically_UnderConcurrentAccess()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        cache.Set("key1", ResourceAuthorizationResult.Success());
        var tasks = new List<Task>();

        // Act - Many concurrent hits and misses
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => cache.TryGet("key1", out _))); // Hit
            tasks.Add(Task.Run(() => cache.TryGet($"miss-{i}", out _))); // Miss
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var stats = cache.GetStatistics();
        stats.Hits.Should().Be(100);
        stats.Misses.Should().Be(100);
        (stats.Hits + stats.Misses).Should().Be(200);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Set_WithVeryLongKey_StoresSuccessfully()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var longKey = "authz:" + new string('a', 1000);

        // Act
        cache.Set(longKey, ResourceAuthorizationResult.Success());

        // Assert
        cache.TryGet(longKey, out var result).Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public void Set_WithSpecialCharactersInKey_StoresSuccessfully()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var specialKey = "authz:layer:my-layer/with:special@chars:read:user#123";

        // Act
        cache.Set(specialKey, ResourceAuthorizationResult.Success());

        // Assert
        cache.TryGet(specialKey, out var result).Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public void InvalidateResource_WithEmptyCache_DoesNotThrow()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act & Assert
        var act = () => cache.InvalidateResource("layer", "layer-1");
        act.Should().NotThrow();
    }

    [Fact]
    public void Set_AfterInvalidateAll_CanAddNewEntries()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        cache.Set("key1", ResourceAuthorizationResult.Success());
        cache.InvalidateAll();

        // Act
        cache.Set("key2", ResourceAuthorizationResult.Success());

        // Assert
        cache.TryGet("key2", out var result).Should().BeTrue();
        result.Should().NotBeNull();
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1);
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public void Cache_WithSizeLimit_RespectsMemoryCacheSize()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act - Each entry has Size = 1
        for (int i = 0; i < 100; i++)
        {
            cache.Set($"key-{i}", ResourceAuthorizationResult.Success());
        }

        // Assert - All entries should fit within memory cache size limit
        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(100);
    }

    [Fact]
    public void Cache_EvictionCallback_IncrementsEvictionMetric()
    {
        // Arrange
        var options = new ResourceAuthorizationOptions
        {
            CacheDurationSeconds = 300,
            MaxCacheSize = 5
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Act - Exceed max size
        for (int i = 0; i < 10; i++)
        {
            cache.Set($"key-{i}", ResourceAuthorizationResult.Success());
        }

        // Assert
        var stats = cache.GetStatistics();
        stats.Evictions.Should().BeGreaterOrEqualTo(5);
    }

    #endregion

    #region Integration Tests with CacheKeyGenerator

    [Fact]
    public void Integration_WithCacheKeyGenerator_GeneratesConsistentKeys()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var userId = "user-123";
        var resourceType = "layer";
        var resourceId = "layer-1";
        var operation = "read";

        // Act
        var key1 = CacheKeyGenerator.GenerateAuthorizationKey(userId, resourceType, resourceId, operation);
        var key2 = ResourceAuthorizationCache.BuildCacheKey(userId, resourceType, resourceId, operation);

        // Assert
        key1.Should().Be(key2);

        cache.Set(key1, ResourceAuthorizationResult.Success());
        cache.TryGet(key2, out var result).Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public void Integration_InvalidateResource_WorksWithCacheKeyGenerator()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var resourceType = "layer";
        var resourceId = "layer-1";

        var key1 = CacheKeyGenerator.GenerateAuthorizationKey("user-1", resourceType, resourceId, "read");
        var key2 = CacheKeyGenerator.GenerateAuthorizationKey("user-2", resourceType, resourceId, "write");
        var key3 = CacheKeyGenerator.GenerateAuthorizationKey("user-1", "collection", "col-1", "read");

        cache.Set(key1, ResourceAuthorizationResult.Success());
        cache.Set(key2, ResourceAuthorizationResult.Success());
        cache.Set(key3, ResourceAuthorizationResult.Success());

        // Act
        cache.InvalidateResource(resourceType, resourceId);

        // Assert
        cache.TryGet(key1, out _).Should().BeFalse();
        cache.TryGet(key2, out _).Should().BeFalse();
        cache.TryGet(key3, out _).Should().BeTrue();
    }

    #endregion

    #region Realistic Usage Scenarios

    [Fact]
    public void Scenario_UserAccessingMultipleResources_CachesIndependently()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var userId = "user-123";

        // Act - User accessing different resources
        var layer1ReadKey = CacheKeyGenerator.GenerateAuthorizationKey(userId, "layer", "layer-1", "read");
        var layer1WriteKey = CacheKeyGenerator.GenerateAuthorizationKey(userId, "layer", "layer-1", "write");
        var layer2ReadKey = CacheKeyGenerator.GenerateAuthorizationKey(userId, "layer", "layer-2", "read");
        var collectionReadKey = CacheKeyGenerator.GenerateAuthorizationKey(userId, "collection", "col-1", "read");

        cache.Set(layer1ReadKey, ResourceAuthorizationResult.Success());
        cache.Set(layer1WriteKey, ResourceAuthorizationResult.Fail("No write access"));
        cache.Set(layer2ReadKey, ResourceAuthorizationResult.Success());
        cache.Set(collectionReadKey, ResourceAuthorizationResult.Success());

        // Assert
        cache.TryGet(layer1ReadKey, out var result1).Should().BeTrue();
        result1!.Succeeded.Should().BeTrue();

        cache.TryGet(layer1WriteKey, out var result2).Should().BeTrue();
        result2!.Succeeded.Should().BeFalse();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(4);
    }

    [Fact]
    public void Scenario_ResourceUpdate_InvalidatesAllUsersForThatResource()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);

        // Multiple users have cached access to layer-1
        cache.Set(CacheKeyGenerator.GenerateAuthorizationKey("user-1", "layer", "layer-1", "read"),
            ResourceAuthorizationResult.Success());
        cache.Set(CacheKeyGenerator.GenerateAuthorizationKey("user-2", "layer", "layer-1", "read"),
            ResourceAuthorizationResult.Success());
        cache.Set(CacheKeyGenerator.GenerateAuthorizationKey("user-3", "layer", "layer-1", "write"),
            ResourceAuthorizationResult.Success());
        cache.Set(CacheKeyGenerator.GenerateAuthorizationKey("user-1", "layer", "layer-2", "read"),
            ResourceAuthorizationResult.Success());

        // Act - Layer-1 is updated, invalidate all cached permissions
        cache.InvalidateResource("layer", "layer-1");

        // Assert - All layer-1 permissions invalidated
        cache.TryGet(CacheKeyGenerator.GenerateAuthorizationKey("user-1", "layer", "layer-1", "read"), out _)
            .Should().BeFalse();
        cache.TryGet(CacheKeyGenerator.GenerateAuthorizationKey("user-2", "layer", "layer-1", "read"), out _)
            .Should().BeFalse();
        cache.TryGet(CacheKeyGenerator.GenerateAuthorizationKey("user-3", "layer", "layer-1", "write"), out _)
            .Should().BeFalse();

        // Layer-2 permission still cached
        cache.TryGet(CacheKeyGenerator.GenerateAuthorizationKey("user-1", "layer", "layer-2", "read"), out _)
            .Should().BeTrue();

        var stats = cache.GetStatistics();
        stats.EntryCount.Should().Be(1);
    }

    [Fact]
    public void Scenario_HighVolumeAccess_MaintainsPerformanceMetrics()
    {
        // Arrange
        var cache = new ResourceAuthorizationCache(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        var random = new Random(42);

        // Seed cache with some entries
        for (int i = 0; i < 100; i++)
        {
            var key = CacheKeyGenerator.GenerateAuthorizationKey($"user-{i}", "layer", $"layer-{i}", "read");
            cache.Set(key, ResourceAuthorizationResult.Success());
        }

        // Act - Simulate high volume mixed access
        for (int i = 0; i < 1000; i++)
        {
            var userId = random.Next(0, 150); // Some users not in cache
            var layerId = random.Next(0, 150); // Some layers not in cache
            var key = CacheKeyGenerator.GenerateAuthorizationKey($"user-{userId}", "layer", $"layer-{layerId}", "read");
            cache.TryGet(key, out _);
        }

        // Assert
        var stats = cache.GetStatistics();
        stats.Hits.Should().BeGreaterThan(0);
        stats.Misses.Should().BeGreaterThan(0);
        (stats.Hits + stats.Misses).Should().Be(1000);
        stats.HitRate.Should().BeGreaterThan(0);
        stats.HitRate.Should().BeLessThan(1);
    }

    #endregion
}
