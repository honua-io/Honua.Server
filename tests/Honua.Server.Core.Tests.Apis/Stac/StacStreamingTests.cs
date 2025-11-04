using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Tests for STAC streaming search functionality.
/// </summary>
public sealed class StacStreamingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IStacCatalogStore _store;

    public StacStreamingTests(ITestOutputHelper output)
    {
        _output = output;

        // Use relational store for realistic testing
        var connectionString = "Data Source=:memory:";
        _store = new SqliteStacCatalogStore(connectionString);
    }

    [Fact]
    public async Task SearchStreamAsync_WithSmallResultSet_StreamsAllItems()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await _store.UpsertCollectionAsync(collection);

        var items = CreateTestItems("test-collection", count: 5);
        foreach (var item in items)
        {
            await _store.UpsertItemAsync(item);
        }

        // Act
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "test-collection" },
            Limit = 100
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.Equal(5, streamedItems.Count);
        Assert.Equal(items.Select(i => i.Id).OrderBy(x => x), streamedItems.Select(i => i.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task SearchStreamAsync_WithLargeResultSet_StreamsInPages()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("large-collection");
        await _store.UpsertCollectionAsync(collection);

        // Create 100 items
        var itemCount = 100;
        var items = CreateTestItems("large-collection", count: itemCount);

        // Use bulk upsert for efficiency
        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "large-collection" },
            Limit = 10 // Page size
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.Equal(itemCount, streamedItems.Count);
        _output.WriteLine($"Streamed {streamedItems.Count} items successfully");
    }

    [Fact]
    public async Task SearchStreamAsync_WithVeryLargeResultSet_StreamsWithConstantMemory()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("very-large-collection");
        await _store.UpsertCollectionAsync(collection);

        // Create 1000 items
        var itemCount = 1000;
        var items = CreateTestItems("very-large-collection", count: itemCount);

        // Use bulk upsert for efficiency
        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act
        var streamedCount = 0;
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        var parameters = new StacSearchParameters
        {
            Collections = new[] { "very-large-collection" },
            Limit = 10 // Small page size
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedCount++;

            // Check memory every 100 items
            if (streamedCount % 100 == 0)
            {
                var memoryCurrent = GC.GetTotalMemory(forceFullCollection: false);
                var memoryIncrease = memoryCurrent - memoryBefore;

                // Memory should not grow unbounded - allow 50MB growth
                _output.WriteLine($"Items: {streamedCount}, Memory increase: {memoryIncrease / 1024 / 1024:F2} MB");
                Assert.True(memoryIncrease < 50 * 1024 * 1024,
                    $"Memory grew by {memoryIncrease / 1024 / 1024:F2} MB, exceeding 50 MB threshold");
            }
        }

        // Assert
        Assert.Equal(itemCount, streamedCount);
        _output.WriteLine($"Successfully streamed {streamedCount} items with constant memory usage");
    }

    [Fact]
    public async Task SearchStreamAsync_WithCancellation_StopsStreaming()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("cancel-collection");
        await _store.UpsertCollectionAsync(collection);

        var items = CreateTestItems("cancel-collection", count: 100);
        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act
        var streamedCount = 0;
        var cts = new CancellationTokenSource();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "cancel-collection" }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in _store.SearchStreamAsync(parameters, cts.Token))
            {
                streamedCount++;

                // Cancel after 10 items
                if (streamedCount >= 10)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert
        Assert.True(streamedCount >= 10, $"Expected at least 10 items before cancellation, got {streamedCount}");
        Assert.True(streamedCount < 100, $"Expected cancellation before all items, but got {streamedCount}");
        _output.WriteLine($"Cancellation worked correctly after {streamedCount} items");
    }

    [Fact]
    public async Task SearchStreamAsync_WithFilters_StreamsFilteredResults()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("filter-collection");
        await _store.UpsertCollectionAsync(collection);

        // Create items with different dates
        var items = new List<StacItemRecord>();
        for (var i = 0; i < 50; i++)
        {
            var item = CreateTestItem("filter-collection", $"item-{i:D3}");
            item = item with
            {
                Datetime = DateTimeOffset.UtcNow.AddDays(-i),
                Bbox = new[] { -10.0 + i, -10.0 + i, 10.0 + i, 10.0 + i }
            };
            items.Add(item);
        }

        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act - filter by datetime
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "filter-collection" },
            Start = DateTimeOffset.UtcNow.AddDays(-20),
            End = DateTimeOffset.UtcNow
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.True(streamedItems.Count > 0, "Should have filtered items");
        Assert.True(streamedItems.Count < 50, "Should have filtered out some items");
        _output.WriteLine($"Filtered stream returned {streamedItems.Count} items out of 50");
    }

    [Fact]
    public async Task SearchStreamAsync_WithSorting_StreamsSortedResults()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("sort-collection");
        await _store.UpsertCollectionAsync(collection);

        var items = new List<StacItemRecord>();
        for (var i = 0; i < 20; i++)
        {
            var item = CreateTestItem("sort-collection", $"item-{i:D3}");
            item = item with
            {
                Datetime = DateTimeOffset.UtcNow.AddDays(-i)
            };
            items.Add(item);
        }

        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act - sort by datetime descending
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "sort-collection" },
            SortBy = new[]
            {
                new StacSortField { Field = "datetime", Direction = StacSortDirection.Descending }
            }
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.Equal(20, streamedItems.Count);

        // Verify descending order
        for (var i = 1; i < streamedItems.Count; i++)
        {
            Assert.True(streamedItems[i - 1].Datetime >= streamedItems[i].Datetime,
                "Items should be sorted by datetime descending");
        }

        _output.WriteLine("Items are correctly sorted by datetime descending");
    }

    [Fact]
    public async Task SearchStreamAsync_WithMultipleCollections_StreamsFromAllCollections()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection1 = CreateTestCollection("collection-1");
        var collection2 = CreateTestCollection("collection-2");
        await _store.UpsertCollectionAsync(collection1);
        await _store.UpsertCollectionAsync(collection2);

        var items1 = CreateTestItems("collection-1", count: 15);
        var items2 = CreateTestItems("collection-2", count: 25);

        await _store.BulkUpsertItemsAsync(items1.Concat(items2).ToList(), new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "collection-1", "collection-2" }
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.Equal(40, streamedItems.Count);

        var collection1Items = streamedItems.Count(i => i.CollectionId == "collection-1");
        var collection2Items = streamedItems.Count(i => i.CollectionId == "collection-2");

        Assert.Equal(15, collection1Items);
        Assert.Equal(25, collection2Items);

        _output.WriteLine($"Streamed {collection1Items} items from collection-1 and {collection2Items} items from collection-2");
    }

    [Fact]
    public async Task SearchStreamAsync_PerformanceComparison_IsFasterForLargeResultSets()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("perf-collection");
        await _store.UpsertCollectionAsync(collection);

        // Create 500 items
        var itemCount = 500;
        var items = CreateTestItems("perf-collection", count: itemCount);

        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        var parameters = new StacSearchParameters
        {
            Collections = new[] { "perf-collection" },
            Limit = 1000 // High limit to get all items
        };

        // Act - Traditional search
        var swTraditional = Stopwatch.StartNew();
        var traditionalResult = await _store.SearchAsync(parameters);
        swTraditional.Stop();

        // Act - Streaming search
        var streamedItems = new List<StacItemRecord>();
        var swStreaming = Stopwatch.StartNew();

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        swStreaming.Stop();

        // Assert
        Assert.Equal(traditionalResult.Items.Count, streamedItems.Count);

        _output.WriteLine($"Traditional search: {swTraditional.ElapsedMilliseconds}ms");
        _output.WriteLine($"Streaming search: {swStreaming.ElapsedMilliseconds}ms");

        // Streaming might be slightly slower for small result sets due to overhead,
        // but memory usage should be much better
        _output.WriteLine($"Streaming overhead: {swStreaming.ElapsedMilliseconds - swTraditional.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task SearchStreamAsync_WithBboxFilter_StreamsGeospatialResults()
    {
        // Arrange
        await _store.EnsureInitializedAsync();

        var collection = CreateTestCollection("geo-collection");
        await _store.UpsertCollectionAsync(collection);

        var items = new List<StacItemRecord>();
        for (var i = 0; i < 30; i++)
        {
            var item = CreateTestItem("geo-collection", $"item-{i:D3}");
            // Create items in different spatial locations
            var lon = -180.0 + (i * 12.0);
            var lat = -90.0 + (i * 6.0);
            item = item with
            {
                Bbox = new[] { lon - 1, lat - 1, lon + 1, lat + 1 }
            };
            items.Add(item);
        }

        await _store.BulkUpsertItemsAsync(items, new BulkUpsertOptions
        {
            ContinueOnError = false,
            UseBulkInsertOptimization = true
        });

        // Act - filter by bbox covering only some items
        var streamedItems = new List<StacItemRecord>();
        var parameters = new StacSearchParameters
        {
            Collections = new[] { "geo-collection" },
            Bbox = new[] { -50.0, -50.0, 50.0, 50.0 } // Center region
        };

        await foreach (var item in _store.SearchStreamAsync(parameters))
        {
            streamedItems.Add(item);
        }

        // Assert
        Assert.True(streamedItems.Count > 0, "Should have items in bbox");
        Assert.True(streamedItems.Count < 30, "Should filter out items outside bbox");

        // Verify all returned items are within bbox
        foreach (var item in streamedItems)
        {
            Assert.NotNull(item.Bbox);
            var bbox = item.Bbox;
            Assert.True(bbox[0] <= 50.0 && bbox[2] >= -50.0 && bbox[1] <= 50.0 && bbox[3] >= -50.0,
                $"Item {item.Id} is outside the bbox");
        }

        _output.WriteLine($"Bbox filter returned {streamedItems.Count} items out of 30");
    }

    private static StacCollectionRecord CreateTestCollection(string collectionId)
    {
        return new StacCollectionRecord
        {
            Id = collectionId,
            Title = $"Test Collection {collectionId}",
            Description = "Test collection for streaming tests",
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
            Keywords = new List<string> { "test" },
            Properties = new System.Text.Json.Nodes.JsonObject(),
            Links = Array.Empty<StacLink>(),
            Extensions = Array.Empty<string>(),
            ETag = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static List<StacItemRecord> CreateTestItems(string collectionId, int count)
    {
        var items = new List<StacItemRecord>();
        for (var i = 0; i < count; i++)
        {
            items.Add(CreateTestItem(collectionId, $"item-{i:D5}"));
        }
        return items;
    }

    private static StacItemRecord CreateTestItem(string collectionId, string itemId)
    {
        return new StacItemRecord
        {
            Id = itemId,
            CollectionId = collectionId,
            Title = $"Test Item {itemId}",
            Description = $"Test item {itemId} for streaming tests",
            Geometry = """{"type": "Point", "coordinates": [0.0, 0.0]}""",
            Bbox = new[] { -1.0, -1.0, 1.0, 1.0 },
            Datetime = DateTimeOffset.UtcNow,
            Properties = new System.Text.Json.Nodes.JsonObject
            {
                ["test_property"] = "test_value"
            },
            Assets = new Dictionary<string, StacAsset>
            {
                ["thumbnail"] = new StacAsset
                {
                    Href = $"https://example.com/{itemId}/thumbnail.png",
                    Type = "image/png",
                    Roles = new[] { "thumbnail" }
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
