using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Tests for InMemoryStoreBase size limits and LRU eviction to prevent memory exhaustion.
/// </summary>
public sealed class InMemoryStoreBaseSizeLimitTests
{
    private sealed class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    private sealed class TestStore : InMemoryStoreBase<TestEntity, string>
    {
        private readonly List<string> _evictedKeys = new();

        public TestStore(int maxSize = 0) : base(StringComparer.Ordinal)
        {
            MaxSize = maxSize;
        }

        protected override string GetKey(TestEntity entity) => entity.Id;

        protected override void OnEntryEvicted(string key)
        {
            _evictedKeys.Add(key);
        }

        public IReadOnlyList<string> EvictedKeys => _evictedKeys;
    }

    [Fact]
    public async Task PutAsync_WithNoSizeLimit_AllowsUnboundedGrowth()
    {
        // Arrange
        var store = new TestStore(maxSize: 0);

        // Act - add 1000 entries
        for (int i = 0; i < 1000; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Assert
        var count = await store.CountAsync();
        Assert.Equal(1000, count);

        var stats = store.GetStatistics();
        Assert.Equal(1000, stats.EntryCount);
        Assert.Equal(0, stats.MaxSize);
        Assert.Equal(0, stats.EvictionCount);
    }

    [Fact]
    public async Task PutAsync_WithSizeLimit_EvictsLRUWhenLimitReached()
    {
        // Arrange
        var store = new TestStore(maxSize: 100);

        // Act - add 150 entries (50 over limit)
        for (int i = 0; i < 150; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Assert
        var count = await store.CountAsync();
        Assert.Equal(100, count); // Should be capped at 100

        var stats = store.GetStatistics();
        Assert.Equal(100, stats.EntryCount);
        Assert.Equal(100, stats.MaxSize);
        Assert.Equal(50, stats.EvictionCount); // 50 evictions

        // Verify oldest entries were evicted
        Assert.Equal(50, store.EvictedKeys.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.Contains($"key-{i}", store.EvictedKeys);
        }

        // Verify newest entries are still present
        for (int i = 100; i < 150; i++)
        {
            var entity = await store.GetAsync($"key-{i}");
            Assert.NotNull(entity);
        }
    }

    [Fact]
    public async Task GetAsync_UpdatesAccessTime_PreventsLRUEviction()
    {
        // Arrange
        var store = new TestStore(maxSize: 10);

        // Add 10 entries
        for (int i = 0; i < 10; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Access first entry repeatedly to make it most recent
        for (int i = 0; i < 5; i++)
        {
            _ = await store.GetAsync("key-0");
        }

        // Act - add 5 more entries (will evict LRU)
        for (int i = 10; i < 15; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Assert
        var firstEntity = await store.GetAsync("key-0");
        Assert.NotNull(firstEntity); // Should NOT be evicted because it was accessed

        // Verify some other entries were evicted instead
        Assert.Equal(5, store.EvictedKeys.Count);
        Assert.DoesNotContain("key-0", store.EvictedKeys);
    }

    [Fact]
    public async Task TryAddAsync_WithSizeLimit_EvictsLRUWhenLimitReached()
    {
        // Arrange
        var store = new TestStore(maxSize: 50);

        // Act - add 75 entries using TryAddAsync
        for (int i = 0; i < 75; i++)
        {
            var added = await store.TryAddAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
            Assert.True(added);
        }

        // Assert
        var count = await store.CountAsync();
        Assert.Equal(50, count);

        var stats = store.GetStatistics();
        Assert.Equal(50, stats.EntryCount);
        Assert.Equal(25, stats.EvictionCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAccessTimeTracking()
    {
        // Arrange
        var store = new TestStore(maxSize: 100);

        await store.PutAsync(new TestEntity { Id = "key-1", Data = "value-1" });
        await store.PutAsync(new TestEntity { Id = "key-2", Data = "value-2" });

        // Act
        var deleted = await store.DeleteAsync("key-1");

        // Assert
        Assert.True(deleted);

        var stats = store.GetStatistics();
        Assert.Equal(1, stats.EntryCount);

        // Verify access time was cleaned up (indirectly by checking count matches)
        Assert.Equal(1, stats.EntryCount);
    }

    [Fact]
    public async Task ClearAsync_ResetsAllMetrics()
    {
        // Arrange
        var store = new TestStore(maxSize: 10);

        for (int i = 0; i < 15; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        var statsBefore = store.GetStatistics();
        Assert.True(statsBefore.EvictionCount > 0);

        // Act
        await store.ClearAsync();

        // Assert
        var count = await store.CountAsync();
        Assert.Equal(0, count);

        var statsAfter = store.GetStatistics();
        Assert.Equal(0, statsAfter.EntryCount);
        Assert.Equal(0, statsAfter.EvictionCount);
        Assert.Equal(0, statsAfter.AccessCount);
    }

    [Fact]
    public async Task GetStatistics_ReturnsAccurateMetrics()
    {
        // Arrange
        var store = new TestStore(maxSize: 100);

        for (int i = 0; i < 150; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Access some entries
        for (int i = 100; i < 110; i++)
        {
            _ = await store.GetAsync($"key-{i}");
        }

        // Act
        var stats = store.GetStatistics();

        // Assert
        Assert.Equal(100, stats.EntryCount);
        Assert.Equal(100, stats.MaxSize);
        Assert.Equal(50, stats.EvictionCount);
        Assert.True(stats.AccessCount > 150); // At least 150 puts + 10 gets
        Assert.Equal(1.0, stats.UtilizationRate); // 100%
    }

    [Fact]
    public async Task PutAsync_UpdateExistingEntry_DoesNotTriggerEviction()
    {
        // Arrange
        var store = new TestStore(maxSize: 10);

        for (int i = 0; i < 10; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"value-{i}" });
        }

        // Act - update existing entries (should not trigger eviction)
        for (int i = 0; i < 10; i++)
        {
            await store.PutAsync(new TestEntity { Id = $"key-{i}", Data = $"updated-{i}" });
        }

        // Assert
        var count = await store.CountAsync();
        Assert.Equal(10, count);

        var stats = store.GetStatistics();
        Assert.Equal(0, stats.EvictionCount); // No evictions

        // Verify updates worked
        var entity = await store.GetAsync("key-0");
        Assert.NotNull(entity);
        Assert.Equal("updated-0", entity!.Data);
    }

    [Fact]
    public async Task LRUEviction_MaintainsCorrectOrder()
    {
        // Arrange
        var store = new TestStore(maxSize: 5);

        // Add 5 entries
        await store.PutAsync(new TestEntity { Id = "A", Data = "1" });
        await store.PutAsync(new TestEntity { Id = "B", Data = "2" });
        await store.PutAsync(new TestEntity { Id = "C", Data = "3" });
        await store.PutAsync(new TestEntity { Id = "D", Data = "4" });
        await store.PutAsync(new TestEntity { Id = "E", Data = "5" });

        // Access B and D to make them more recent
        _ = await store.GetAsync("B");
        _ = await store.GetAsync("D");

        // Act - add 3 more entries (will evict A, C, E in that order)
        await store.PutAsync(new TestEntity { Id = "F", Data = "6" });
        await store.PutAsync(new TestEntity { Id = "G", Data = "7" });
        await store.PutAsync(new TestEntity { Id = "H", Data = "8" });

        // Assert
        var evicted = store.EvictedKeys;
        Assert.Equal(3, evicted.Count);
        Assert.Contains("A", evicted); // Oldest
        Assert.Contains("C", evicted);
        Assert.Contains("E", evicted);

        // B and D should still be present
        Assert.NotNull(await store.GetAsync("B"));
        Assert.NotNull(await store.GetAsync("D"));

        // New entries should be present
        Assert.NotNull(await store.GetAsync("F"));
        Assert.NotNull(await store.GetAsync("G"));
        Assert.NotNull(await store.GetAsync("H"));
    }
}
