using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Comprehensive tests for bulk STAC item upsert functionality.
/// </summary>
[Collection("SharedPostgres")]
[Trait("Category", "Integration")]
public sealed class BulkUpsertStacItemsTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public BulkUpsertStacItemsTests(SharedPostgresFixture fixture)
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
    public async Task BulkUpsertItemsAsync_WithEmptyList_ReturnsZeroSuccess()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        // Act
        var result = await store.BulkUpsertItemsAsync(Array.Empty<StacItemRecord>());

        // Assert
        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithNullItems_ThrowsArgumentNullException()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.BulkUpsertItemsAsync(null!));
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithSingleItem_InsertsSuccessfully()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var item = CreateTestItem(collection.Id, "item-1");
        var items = new[] { item };

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
        result.Duration.TotalMilliseconds.Should().BeGreaterThan(0);

        // Verify item was inserted
        var fetched = await store.GetItemAsync(collection.Id, item.Id);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithMultipleItems_InsertsAllSuccessfully()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 100)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert
        result.SuccessCount.Should().Be(100);
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
        result.ItemsPerSecond.Should().BeGreaterThan(0);

        // Verify all items were inserted
        var allItems = await store.ListItemsAsync(collection.Id, 1000);
        allItems.Should().HaveCount(100);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithDuplicateItems_UpdatesExisting()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var item = CreateTestItem(collection.Id, "item-1") with { Title = "Original Title" };
        await store.UpsertItemAsync(item);

        // Act - Bulk upsert with same ID but different title
        var updatedItem = item with { Title = "Updated Title" };
        var result = await store.BulkUpsertItemsAsync(new[] { updatedItem });

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);

        var fetched = await store.GetItemAsync(collection.Id, item.Id);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithLargeBatch_SplitsIntoBatches()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 2500)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        var options = new BulkUpsertOptions { BatchSize = 500 };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(2500);
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithProgressCallback_ReportsProgress()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 250)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        var progressReports = new List<(int processed, int total, int batchNumber)>();
        var options = new BulkUpsertOptions
        {
            BatchSize = 50,
            ReportProgress = true,
            ProgressCallback = (processed, total, batchNumber) =>
            {
                progressReports.Add((processed, total, batchNumber));
            }
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(250);
        progressReports.Should().HaveCount(5); // 250 items / 50 batch size = 5 batches
        progressReports.Last().processed.Should().Be(250);
        progressReports.Last().total.Should().Be(250);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithInvalidItem_RecordsFailure()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var validItem = CreateTestItem(collection.Id, "valid-item");
        var invalidItem = CreateTestItem(collection.Id, "") with { Id = "" }; // Invalid: empty ID

        var options = new BulkUpsertOptions { ContinueOnError = true };

        // Act
        var result = await store.BulkUpsertItemsAsync(new[] { validItem, invalidItem }, options);

        // Assert
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);
        result.IsSuccess.Should().BeFalse();
        result.Failures.Should().HaveCount(1);
        result.Failures[0].ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithContinueOnError_ProcessesAllItems()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = new[]
        {
            CreateTestItem(collection.Id, "item-1"),
            CreateTestItem(collection.Id, "") with { Id = "" }, // Invalid
            CreateTestItem(collection.Id, "item-3"),
            CreateTestItem(collection.Id, "item-4")
        };

        var options = new BulkUpsertOptions { ContinueOnError = true };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(3);
        result.FailureCount.Should().Be(1);
        result.Failures.Should().HaveCount(1);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithoutContinueOnError_StopsOnFirstError()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = new[]
        {
            CreateTestItem(collection.Id, "item-1"),
            CreateTestItem(collection.Id, "") with { Id = "" }, // Invalid - should stop here
            CreateTestItem(collection.Id, "item-3"),
        };

        var options = new BulkUpsertOptions { ContinueOnError = false };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.BulkUpsertItemsAsync(items, options));
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_MeasuresPerformance_CalculatesThroughput()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 1000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert
        result.Duration.TotalMilliseconds.Should().BeGreaterThan(0);
        result.ItemsPerSecond.Should().BeGreaterThan(0);
        result.ItemsPerSecond.Should().BeLessThan(1_000_000); // Sanity check
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithCustomBatchSize_UsesBatchSize()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 500)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        var options = new BulkUpsertOptions { BatchSize = 100 };

        // Act
        var result = await store.BulkUpsertItemsAsync(items, options);

        // Assert
        result.SuccessCount.Should().Be(500);
        result.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_InMemoryImplementation_WorksCorrectly()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("test-collection");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 50)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D2}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert
        result.SuccessCount.Should().Be(50);
        result.FailureCount.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BulkUpsertItemsAsync_WithMixedCollections_InsertsAllItems()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection1 = CreateTestCollection("collection-1");
        var collection2 = CreateTestCollection("collection-2");
        await store.UpsertCollectionAsync(collection1);
        await store.UpsertCollectionAsync(collection2);

        var items = new[]
        {
            CreateTestItem(collection1.Id, "item-1"),
            CreateTestItem(collection1.Id, "item-2"),
            CreateTestItem(collection2.Id, "item-1"),
            CreateTestItem(collection2.Id, "item-2")
        };

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert
        result.SuccessCount.Should().Be(4);
        result.FailureCount.Should().Be(0);

        var col1Items = await store.ListItemsAsync(collection1.Id, 100);
        var col2Items = await store.ListItemsAsync(collection2.Id, 100);
        col1Items.Should().HaveCount(2);
        col2Items.Should().HaveCount(2);
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
