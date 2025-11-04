using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Memory profiling tests for STAC streaming functionality.
/// These tests verify that streaming maintains constant memory usage.
/// </summary>
public sealed class StacMemoryProfilingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IStacCatalogStore _store;

    public StacMemoryProfilingTests(ITestOutputHelper output)
    {
        _output = output;

        // Use relational store for realistic testing
        var connectionString = "Data Source=:memory:";
        _store = new SqliteStacCatalogStore(connectionString);
    }

    [Fact]
    public async Task StreamingSearch_With10kItems_MaintainsConstantMemory()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("memory-test-10k");
        await _store.UpsertCollectionAsync(collection);

        // Create 10,000 items
        var itemCount = 10_000;
        _output.WriteLine($"Creating {itemCount:N0} test items...");

        await CreateItemsInBatches("memory-test-10k", itemCount, batchSize: 1000);

        // Force garbage collection before test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        var maxMemoryIncrease = 0L;
        var measurements = new List<(int ItemsProcessed, long MemoryBytes, double MemoryMB)>();

        _output.WriteLine($"Starting memory profiling test...");
        _output.WriteLine($"Initial memory: {memoryBefore / 1024 / 1024:F2} MB");

        // Act - Stream all items and measure memory
        var streamedCount = 0;
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "memory-test-10k" }
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedCount++;

            // Measure memory every 1000 items
            if (streamedCount % 1000 == 0)
            {
                var memoryCurrent = GC.GetTotalMemory(forceFullCollection: false);
                var memoryIncrease = memoryCurrent - memoryBefore;
                var memoryMB = memoryIncrease / 1024.0 / 1024.0;

                measurements.Add((streamedCount, memoryIncrease, memoryMB));
                maxMemoryIncrease = Math.Max(maxMemoryIncrease, memoryIncrease);

                _output.WriteLine($"Items: {streamedCount:N0}, Memory: {memoryCurrent / 1024 / 1024:F2} MB, Increase: {memoryMB:F2} MB");
            }
        }

        // Assert
        Assert.Equal(itemCount, streamedCount);

        // Memory increase should be less than 100 MB for streaming 10k items
        var maxMemoryIncreaseMB = maxMemoryIncrease / 1024.0 / 1024.0;
        _output.WriteLine($"\nMax memory increase: {maxMemoryIncreaseMB:F2} MB");

        Assert.True(maxMemoryIncreaseMB < 100,
            $"Memory increased by {maxMemoryIncreaseMB:F2} MB, exceeding 100 MB threshold for 10k items");

        // Verify memory growth is bounded (not linear with item count)
        if (measurements.Count >= 2)
        {
            var firstMeasurement = measurements[0];
            var lastMeasurement = measurements[^1];

            var memoryGrowthRatio = lastMeasurement.MemoryMB / firstMeasurement.MemoryMB;
            _output.WriteLine($"Memory growth ratio: {memoryGrowthRatio:F2}x (should be close to 1.0 for constant memory)");

            // Memory should not grow linearly - allow up to 3x growth as buffers stabilize
            Assert.True(memoryGrowthRatio < 3.0,
                $"Memory grew {memoryGrowthRatio:F2}x, indicating non-constant memory usage");
        }

        _output.WriteLine($"\nSuccessfully streamed {streamedCount:N0} items with bounded memory usage");
    }

    [Fact]
    public async Task TraditionalSearch_With10kItems_LoadsAllIntoMemory()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("memory-test-traditional");
        await _store.UpsertCollectionAsync(collection);

        // Create 10,000 items
        var itemCount = 10_000;
        _output.WriteLine($"Creating {itemCount:N0} test items...");

        await CreateItemsInBatches("memory-test-traditional", itemCount, batchSize: 1000);

        // Force garbage collection before test
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        _output.WriteLine($"Initial memory: {memoryBefore / 1024 / 1024:F2} MB");

        // Act - Traditional search loads all into memory
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "memory-test-traditional" },
            Limit = itemCount
        };

        var result = await _store.SearchAsync(parameters);

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncrease = memoryAfter - memoryBefore;
        var memoryIncreaseMB = memoryIncrease / 1024.0 / 1024.0;

        // Assert
        Assert.Equal(itemCount, result.Items.Count);

        _output.WriteLine($"Memory after traditional search: {memoryAfter / 1024 / 1024:F2} MB");
        _output.WriteLine($"Memory increase: {memoryIncreaseMB:F2} MB");

        // Traditional search will load all items into memory
        // This demonstrates the problem that streaming solves
        _output.WriteLine($"\nTraditional search loaded {result.Items.Count:N0} items into memory");
        _output.WriteLine($"This is why streaming is beneficial for large result sets");
    }

    [Fact]
    public async Task StreamingVsTraditional_MemoryComparison()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("memory-comparison");
        await _store.UpsertCollectionAsync(collection);

        // Create 5,000 items for comparison
        var itemCount = 5_000;
        _output.WriteLine($"Creating {itemCount:N0} test items for comparison...");

        await CreateItemsInBatches("memory-comparison", itemCount, batchSize: 1000);

        var parameters = new StacSearchParameters
        {
            Collections = new[] { "memory-comparison" },
            Limit = itemCount
        };

        // Measure traditional search memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBeforeTraditional = GC.GetTotalMemory(forceFullCollection: true);
        var traditionalResult = await _store.SearchAsync(parameters);
        var memoryAfterTraditional = GC.GetTotalMemory(forceFullCollection: false);
        var traditionalMemoryIncrease = memoryAfterTraditional - memoryBeforeTraditional;

        _output.WriteLine($"\nTraditional Search:");
        _output.WriteLine($"  Items: {traditionalResult.Items.Count:N0}");
        _output.WriteLine($"  Memory increase: {traditionalMemoryIncrease / 1024.0 / 1024.0:F2} MB");

        // Measure streaming search memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBeforeStreaming = GC.GetTotalMemory(forceFullCollection: true);
        var streamingCount = 0;
        var maxStreamingMemory = 0L;

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamingCount++;

            if (streamingCount % 500 == 0)
            {
                var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
                var increase = currentMemory - memoryBeforeStreaming;
                maxStreamingMemory = Math.Max(maxStreamingMemory, increase);
            }
        }

        _output.WriteLine($"\nStreaming Search:");
        _output.WriteLine($"  Items: {streamingCount:N0}");
        _output.WriteLine($"  Max memory increase: {maxStreamingMemory / 1024.0 / 1024.0:F2} MB");

        // Assert - Streaming should use significantly less memory
        var memorySavings = traditionalMemoryIncrease - maxStreamingMemory;
        var memorySavingsPercent = (double)memorySavings / traditionalMemoryIncrease * 100;

        _output.WriteLine($"\nMemory Savings:");
        _output.WriteLine($"  Absolute: {memorySavings / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"  Percentage: {memorySavingsPercent:F1}%");

        // Streaming should use at least 30% less memory
        Assert.True(memorySavingsPercent > 30,
            $"Expected at least 30% memory savings, got {memorySavingsPercent:F1}%");
    }

    [Fact]
    public async Task StreamingSearch_ProcessingTime_IsComparableToTraditional()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("perf-comparison");
        await _store.UpsertCollectionAsync(collection);

        // Create 2,000 items
        var itemCount = 2_000;
        _output.WriteLine($"Creating {itemCount:N0} test items...");

        await CreateItemsInBatches("perf-comparison", itemCount, batchSize: 500);

        var parameters = new StacSearchParameters
        {
            Collections = new[] { "perf-comparison" },
            Limit = itemCount
        };

        // Measure traditional search time
        var swTraditional = Stopwatch.StartNew();
        var traditionalResult = await _store.SearchAsync(parameters);
        swTraditional.Stop();

        _output.WriteLine($"\nTraditional Search:");
        _output.WriteLine($"  Time: {swTraditional.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Items: {traditionalResult.Items.Count:N0}");

        // Measure streaming search time
        var streamingCount = 0;
        var swStreaming = Stopwatch.StartNew();

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamingCount++;
        }

        swStreaming.Stop();

        _output.WriteLine($"\nStreaming Search:");
        _output.WriteLine($"  Time: {swStreaming.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Items: {streamingCount:N0}");

        // Assert
        Assert.Equal(traditionalResult.Items.Count, streamingCount);

        // Streaming might be slightly slower due to overhead, but should be within 2x
        var slowdownRatio = (double)swStreaming.ElapsedMilliseconds / swTraditional.ElapsedMilliseconds;
        _output.WriteLine($"\nStreaming slowdown: {slowdownRatio:F2}x");

        Assert.True(slowdownRatio < 2.0,
            $"Streaming is {slowdownRatio:F2}x slower than traditional, exceeding 2x threshold");

        _output.WriteLine("Streaming performance is acceptable");
    }

    private async Task CreateItemsInBatches(string collectionId, int totalCount, int batchSize)
    {
        var batches = (int)Math.Ceiling((double)totalCount / batchSize);

        for (var batch = 0; batch < batches; batch++)
        {
            var start = batch * batchSize;
            var count = Math.Min(batchSize, totalCount - start);

            var items = new List<StacItemRecord>();
            for (var i = 0; i < count; i++)
            {
                var globalIndex = start + i;
                items.Add(CreateTestItem(collectionId, $"item-{globalIndex:D6}"));
            }

            await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
            {
                ContinueOnError = false,
                UseBulkInsertOptimization = true
            });

            if ((batch + 1) % 5 == 0)
            {
                _output.WriteLine($"  Created {(batch + 1) * batchSize:N0} items...");
            }
        }

        _output.WriteLine($"  Total items created: {totalCount:N0}");
    }

    private static StacCollectionRecord CreateTestCollection(string collectionId)
    {
        return new StacCollectionRecord
        {
            Id = collectionId,
            Title = $"Memory Test Collection {collectionId}",
            Description = "Test collection for memory profiling",
            License = "MIT",
            Extent = new StacExtent
            {
                Spatial = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Temporal = new[]
                {
                    new StacTemporalInterval
                    {
                        Start = DateTimeOffset.UtcNow.AddYears(-1),
                        End = DateTimeOffset.UtcNow
                    }
                }
            },
            Keywords = new List<string> { "test", "memory" },
            Properties = new System.Text.Json.Nodes.JsonObject(),
            Links = Array.Empty<StacLink>(),
            Extensions = Array.Empty<string>(),
            ETag = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static StacItemRecord CreateTestItem(string collectionId, string itemId)
    {
        return new StacItemRecord
        {
            Id = itemId,
            CollectionId = collectionId,
            Title = $"Memory Test Item {itemId}",
            Description = $"Test item {itemId} for memory profiling tests",
            Geometry = """{"type": "Point", "coordinates": [0.0, 0.0]}""",
            Bbox = new[] { -1.0, -1.0, 1.0, 1.0 },
            Datetime = DateTimeOffset.UtcNow,
            Properties = new System.Text.Json.Nodes.JsonObject
            {
                ["test_property"] = "test_value",
                ["item_id"] = itemId
            },
            Assets = new Dictionary<string, StacAsset>
            {
                ["thumbnail"] = new StacAsset
                {
                    Href = $"https://example.com/{itemId}/thumbnail.png",
                    Type = "image/png",
                    Roles = new[] { "thumbnail" }
                },
                ["data"] = new StacAsset
                {
                    Href = $"https://example.com/{itemId}/data.tif",
                    Type = "image/tiff",
                    Roles = new[] { "data" }
                }
            },
            Links = Array.Empty<StacLink>(),
            Extensions = Array.Empty<string>(),
            ETag = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void Dispose()
    {
        (_store as IDisposable)?.Dispose();
    }
}
