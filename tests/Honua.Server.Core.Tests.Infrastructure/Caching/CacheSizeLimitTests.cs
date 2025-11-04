using System;
using System.Threading.Tasks;
using Honua.Server.Core.Authorization;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Caching;

/// <summary>
/// Tests for IMemoryCache size limits to prevent memory exhaustion.
/// </summary>
public sealed class CacheSizeLimitTests
{
    [Fact]
    public void MemoryCache_WithSizeLimit_EvictsEntriesWhenLimitReached()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 // Limit to 100 entries
        });

        // Act - add 150 entries with size=1 each
        for (int i = 0; i < 150; i++)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1, // Each entry counts as 1 toward limit
                Priority = CacheItemPriority.Normal
            };
            cache.Set($"key-{i}", $"value-{i}", options);
        }

        // Assert - check that old entries were evicted
        int presentCount = 0;
        for (int i = 0; i < 150; i++)
        {
            if (cache.TryGetValue($"key-{i}", out _))
            {
                presentCount++;
            }
        }

        // Should be <= 100 due to size limit
        Assert.True(presentCount <= 100, $"Expected <= 100 entries, found {presentCount}");

        // Newest entries should be present
        Assert.True(cache.TryGetValue("key-149", out _));
    }

    [Fact]
    public void MemoryCache_WithCompaction_EvictsLowerPriorityEntriesFirst()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100,
            CompactionPercentage = 0.25 // Evict 25% when limit reached
        });

        // Add high priority entries
        for (int i = 0; i < 50; i++)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                Priority = CacheItemPriority.High
            };
            cache.Set($"high-{i}", $"value-{i}", options);
        }

        // Add normal priority entries
        for (int i = 0; i < 50; i++)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                Priority = CacheItemPriority.Normal
            };
            cache.Set($"normal-{i}", $"value-{i}", options);
        }

        // Act - add more entries to trigger compaction
        for (int i = 0; i < 50; i++)
        {
            var options = new MemoryCacheEntryOptions
            {
                Size = 1,
                Priority = CacheItemPriority.Normal
            };
            cache.Set($"new-{i}", $"value-{i}", options);
        }

        // Assert - high priority entries should be more likely to survive
        int highPriorityPresent = 0;
        for (int i = 0; i < 50; i++)
        {
            if (cache.TryGetValue($"high-{i}", out _))
            {
                highPriorityPresent++;
            }
        }

        // Most high priority entries should still be present
        Assert.True(highPriorityPresent > 25, $"Expected >25 high priority entries, found {highPriorityPresent}");
    }

    [Fact]
    public void ResourceAuthorizationCache_RespectsMaxCacheSize()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100
        });

        var optionsMonitor = new TestOptionsMonitor<ResourceAuthorizationOptions>(new ResourceAuthorizationOptions
        {
            MaxCacheSize = 50,
            CacheDurationSeconds = 300
        });

        var cache = new ResourceAuthorizationCache(
            memoryCache,
            NullLogger<ResourceAuthorizationCache>.Instance,
            optionsMonitor);

        // Act - add 75 authorization results
        for (int i = 0; i < 75; i++)
        {
            var cacheKey = $"user-{i}:layer:layer-{i}:read";
            var result = new ResourceAuthorizationResult
            {
                Succeeded = true
            };
            cache.Set(cacheKey, result);
        }

        // Assert
        var stats = cache.GetStatistics();
        Assert.True(stats.EntryCount <= 50, $"Expected <= 50 entries, found {stats.EntryCount}");
        Assert.True(stats.Evictions > 0, "Expected evictions to have occurred");
    }

    [Fact]
    public void CacheMetricsCollector_TracksEvictions()
    {
        // Arrange
        var options = Options.Create(new CacheSizeLimitOptions
        {
            EnableMetrics = true,
            MaxTotalEntries = 100
        });

        var collector = new CacheMetricsCollector(
            NullLogger<CacheMetricsCollector>.Instance,
            options);

        // Act
        for (int i = 0; i < 10; i++)
        {
            collector.RecordHit("test-cache");
        }

        for (int i = 0; i < 5; i++)
        {
            collector.RecordMiss("test-cache");
        }

        for (int i = 0; i < 3; i++)
        {
            collector.RecordEviction("test-cache", EvictionReason.Capacity);
        }

        collector.UpdateEntryCount("test-cache", 50);

        // Assert
        var stats = collector.GetCacheStatistics("test-cache");
        Assert.NotNull(stats);
        Assert.Equal(10, stats!.Hits);
        Assert.Equal(5, stats.Misses);
        Assert.Equal(3, stats.Evictions);
        Assert.Equal(50, stats.EntryCount);
        Assert.Equal(0.667, stats.HitRate, 3); // 10/(10+5) = 0.667
    }

    [Fact]
    public void CacheMetricsCollector_AggregatesOverallStatistics()
    {
        // Arrange
        var options = Options.Create(new CacheSizeLimitOptions
        {
            EnableMetrics = true
        });

        var collector = new CacheMetricsCollector(
            NullLogger<CacheMetricsCollector>.Instance,
            options);

        // Act - simulate multiple caches
        collector.RecordHit("cache-a");
        collector.RecordHit("cache-a");
        collector.RecordMiss("cache-a");
        collector.UpdateEntryCount("cache-a", 100);

        collector.RecordHit("cache-b");
        collector.RecordMiss("cache-b");
        collector.RecordMiss("cache-b");
        collector.UpdateEntryCount("cache-b", 50);

        collector.RecordEviction("cache-a", EvictionReason.Capacity);
        collector.RecordEviction("cache-b", EvictionReason.Capacity);

        // Assert
        var overall = collector.GetOverallStatistics();
        Assert.Equal(3, overall.Hits); // 2 + 1
        Assert.Equal(3, overall.Misses); // 1 + 2
        Assert.Equal(2, overall.Evictions);
        Assert.Equal(150, overall.EntryCount); // 100 + 50
        Assert.Equal(0.5, overall.HitRate); // 3/(3+3) = 0.5
    }

    [Fact]
    public void CacheSizeLimitOptions_ValidatesConfiguration()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = -1 // Invalid
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CacheSizeLimitOptions_CalculatesSizeInBytes()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = 100
        };

        // Act
        var sizeBytes = options.MaxTotalSizeBytes;

        // Assert
        Assert.Equal(100L * 1024 * 1024, sizeBytes); // 100 MB in bytes
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
}
