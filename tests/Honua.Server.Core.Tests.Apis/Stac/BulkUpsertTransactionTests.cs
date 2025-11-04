using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Comprehensive tests for STAC bulk upsert atomic transaction behavior.
/// Tests the critical fix for data integrity issues caused by per-batch transactions.
/// </summary>
[Collection("SharedPostgres")]
[Trait("Category", "Integration")]
[Trait("Issue", "BulkUpsertTransaction")]
public sealed class BulkUpsertTransactionTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public BulkUpsertTransactionTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available (Docker required).");
        }
        (_connection, _transaction) = await _fixture.CreateTransactionScopeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_CommitsAllBatchesOnSuccess()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        // Create 2500 items (5 batches of 500)
        var items = Enumerable.Range(1, 2500)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        var options = new BulkUpsertOptions
        {
            BatchSize = 500,
            UseAtomicTransaction = true
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(2500, "all items should be committed in single transaction");
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();

        // Verify all items exist
        var allItems = await store.ListItemsAsync(collection.Id, 3000);
        allItems.Should().HaveCount(2500, "all items should be present after commit");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_RollsBackAllBatchesOnFailure()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        // Create items where batch 3 will fail (invalid collection reference)
        var items = new List<StacItemRecord>();

        // Batch 1: 500 valid items
        for (int i = 1; i <= 500; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch1-{i:D3}"));
        }

        // Batch 2: 500 valid items
        for (int i = 1; i <= 500; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch2-{i:D3}"));
        }

        // Batch 3: 500 items with 1 invalid (non-existent collection)
        for (int i = 1; i <= 499; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch3-{i:D3}"));
        }
        items.Add(CreateTestItem("nonexistent-collection", "item-batch3-500")); // This will fail

        // Batch 4: 500 valid items (should never be committed)
        for (int i = 1; i <= 500; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch4-{i:D3}"));
        }

        var options = new BulkUpsertOptions
        {
            BatchSize = 500,
            UseAtomicTransaction = true,
            ContinueOnError = false // Fail fast
        };

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            store.BulkUpsertItemsAsync(items, options));

        exception.Should().NotBeNull("failure in batch 3 should cause exception");

        // Verify NO items were committed (complete rollback)
        var allItems = await store.ListItemsAsync(collection.Id, 3000);
        allItems.Should().BeEmpty("all batches should be rolled back, leaving zero items");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_RollsBackOnMiddleBatchFailure()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        // Simulate failure in middle batch (batch 35 of 50)
        var items = new List<StacItemRecord>();

        // Batches 1-34: 34,000 valid items (34 batches of 1000)
        for (int i = 1; i <= 34000; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-{i:D5}"));
        }

        // Batch 35: 999 valid + 1 invalid item
        for (int i = 1; i <= 999; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-{34000 + i:D5}"));
        }
        items.Add(CreateTestItem("nonexistent-collection", "item-34999")); // Failure point

        // Batches 36-50: 15,000 items that should never be processed
        for (int i = 1; i <= 15000; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-{35000 + i:D5}"));
        }

        var options = new BulkUpsertOptions
        {
            BatchSize = 1000,
            UseAtomicTransaction = true,
            ContinueOnError = false
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.BulkUpsertItemsAsync(items, options));

        // Verify complete rollback - NO items should exist
        var allItems = await store.ListItemsAsync(collection.Id, 50000);
        allItems.Should().BeEmpty("failure in batch 35 should rollback all 50 batches");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithLegacyMode_CommitsPartialBatches()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = new List<StacItemRecord>();

        // Batch 1: 100 valid items
        for (int i = 1; i <= 100; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch1-{i:D3}"));
        }

        // Batch 2: 99 valid + 1 invalid
        for (int i = 1; i <= 99; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch2-{i:D3}"));
        }
        items.Add(CreateTestItem("nonexistent-collection", "item-batch2-100"));

        // Batch 3: 100 valid items
        for (int i = 1; i <= 100; i++)
        {
            items.Add(CreateTestItem(collection.Id, $"item-batch3-{i:D3}"));
        }

        var options = new BulkUpsertOptions
        {
            BatchSize = 100,
            UseAtomicTransaction = false, // Legacy mode
            ContinueOnError = false
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.BulkUpsertItemsAsync(items, options));

        // Verify partial commit - batch 1 should exist, batch 2-3 should not
        var allItems = await store.ListItemsAsync(collection.Id, 500);
        allItems.Should().HaveCount(100, "legacy mode commits batch 1 before batch 2 fails");
        allItems.All(i => i.Id.StartsWith("item-batch1-")).Should().BeTrue("only batch 1 items should exist");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_HandlesLargeTimeout()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 1000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        var options = new BulkUpsertOptions
        {
            BatchSize = 200,
            UseAtomicTransaction = true,
            TransactionTimeoutSeconds = 7200 // 2 hours for very large catalogs
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(1000);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_HandlesCancellation()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 5000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D5}"))
            .ToList();

        var cts = new CancellationTokenSource();

        var options = new BulkUpsertOptions
        {
            BatchSize = 100,
            UseAtomicTransaction = true,
            ReportProgress = true,
            ProgressCallback = (processed, total, batchNumber) =>
            {
                // Cancel after 3 batches
                if (batchNumber == 3)
                {
                    cts.Cancel();
                }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.BulkUpsertItemsAsync(items, options, cts.Token));

        // Verify rollback - no items should exist
        var allItems = await store.ListItemsAsync(collection.Id, 10000);
        allItems.Should().BeEmpty("cancellation should rollback entire transaction");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_SupportsRepeatableReadIsolation()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 100)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        var options = new BulkUpsertOptions
        {
            BatchSize = 25,
            UseAtomicTransaction = true
        };

        // Act - Start bulk upsert
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(100);
        result.IsSuccess.Should().BeTrue();

        // Verify isolation worked - items are visible after commit
        var allItems = await store.ListItemsAsync(collection.Id, 200);
        allItems.Should().HaveCount(100);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_ReportsProgressCorrectly()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 1000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        var progressReports = new List<(int processed, int total, int batchNumber)>();
        var options = new BulkUpsertOptions
        {
            BatchSize = 200,
            UseAtomicTransaction = true,
            ReportProgress = true,
            ProgressCallback = (processed, total, batchNumber) =>
            {
                progressReports.Add((processed, total, batchNumber));
            }
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(1000);
        progressReports.Should().HaveCount(5, "5 batches of 200");
        progressReports.Last().processed.Should().Be(1000);
        progressReports.Last().batchNumber.Should().Be(5);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithAtomicTransaction_ContinueOnError_CommitsPartialSuccess()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = new List<StacItemRecord>();

        // Mix valid and invalid items
        for (int i = 1; i <= 100; i++)
        {
            if (i == 50) // One invalid item in the middle
            {
                items.Add(CreateTestItem(collection.Id, "") with { Id = "" }); // Invalid
            }
            else
            {
                items.Add(CreateTestItem(collection.Id, $"item-{i:D3}"));
            }
        }

        var options = new BulkUpsertOptions
        {
            BatchSize = 25,
            UseAtomicTransaction = true,
            ContinueOnError = true // Continue despite errors
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(99, "99 valid items should be committed");
        result.FailureCount.Should().Be(1, "1 invalid item should fail");
        result.IsSuccess.Should().BeFalse();

        // Verify committed items
        var allItems = await store.ListItemsAsync(collection.Id, 200);
        allItems.Should().HaveCount(99, "only valid items should be present");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_ConcurrentBulkOperations_MaintainIsolation()
    {
        // Arrange
        using var store1 = new PostgresStacCatalogStore(_fixture.ConnectionString);
        using var store2 = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store1.EnsureInitializedAsync();
        await store2.EnsureInitializedAsync();

        var collection1 = CreateTestCollection("collection-1");
        var collection2 = CreateTestCollection("collection-2");
        await store1.UpsertCollectionAsync(collection1);
        await store2.UpsertCollectionAsync(collection2);

        var items1 = Enumerable.Range(1, 500)
            .Select(i => CreateTestItem(collection1.Id, $"item-{i:D3}"))
            .ToList();

        var items2 = Enumerable.Range(1, 500)
            .Select(i => CreateTestItem(collection2.Id, $"item-{i:D3}"))
            .ToList();

        var options = new BulkUpsertOptions
        {
            BatchSize = 100,
            UseAtomicTransaction = true
        };

        // Act - Run concurrent bulk operations
        var task1 = store1.BulkUpsertItemsAsync(items1, options);
        var task2 = store2.BulkUpsertItemsAsync(items2, options);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        results[0].SuccessCount.Should().Be(500);
        results[1].SuccessCount.Should().Be(500);

        var col1Items = await store1.ListItemsAsync(collection1.Id, 1000);
        var col2Items = await store2.ListItemsAsync(collection2.Id, 1000);
        col1Items.Should().HaveCount(500);
        col2Items.Should().HaveCount(500);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_VeryLargeBatchCount_CompletesSuccessfully()
    {
        // Arrange - Simulate a large STAC catalog with 50 batches
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("large-collection");
        await store.UpsertCollectionAsync(collection);

        // 50,000 items = 50 batches of 1000
        var items = Enumerable.Range(1, 50000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D6}"))
            .ToList();

        var options = new BulkUpsertOptions
        {
            BatchSize = 1000,
            UseAtomicTransaction = true,
            TransactionTimeoutSeconds = 3600 // 1 hour timeout
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(50000, "all 50 batches should commit atomically");
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();

        // Verify count
        var allItems = await store.ListItemsAsync(collection.Id, 60000);
        allItems.Should().HaveCount(50000);
    }

    private static StacCollectionRecord CreateTestCollection(string id)
    {
        var now = DateTimeOffset.UtcNow;
        return new StacCollectionRecord
        {
            Id = id,
            Title = $"Collection {id}",
            Description = $"Test collection {id}",
            License = "MIT",
            Version = "1.0",
            Keywords = new[] { "test" },
            Links = new[] { new StacLink { Rel = "self", Href = $"https://example.test/collections/{id}" } },
            Extent = new StacExtent
            {
                Spatial = new[] { new[] { -180d, -90d, 180d, 90d } },
                Temporal = new[] { new StacTemporalInterval { Start = now.AddDays(-1), End = now } }
            },
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static StacItemRecord CreateTestItem(string collectionId, string id)
    {
        var now = DateTimeOffset.UtcNow;
        return new StacItemRecord
        {
            Id = id,
            CollectionId = collectionId,
            Title = $"Item {id}",
            Description = $"Test item {id}",
            Properties = new JsonObject { ["test"] = "value" },
            Assets = new Dictionary<string, StacAsset>
            {
                ["data"] = new StacAsset
                {
                    Href = $"https://example.test/data/{id}.tif",
                    Type = "image/tiff",
                    Roles = new[] { "data" }
                }
            },
            Links = new[] { new StacLink { Rel = "self", Href = $"https://example.test/items/{id}" } },
            Bbox = new[] { -10d, -10d, 10d, 10d },
            Geometry = "{\"type\":\"Point\",\"coordinates\":[0,0]}",
            Datetime = now,
            RasterDatasetId = collectionId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
