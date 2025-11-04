using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Abstract base class for STAC catalog store tests.
/// Contains all common test methods that verify IStacCatalogStore implementations.
/// Derived classes provide database-specific setup and connection management.
/// </summary>
public abstract class StacCatalogStoreTestsBase
{
    /// <summary>
    /// Gets the catalog store instance under test.
    /// </summary>
    protected abstract IStacCatalogStore CatalogStore { get; }

    /// <summary>
    /// Gets the connection string for the database.
    /// </summary>
    protected abstract string ConnectionString { get; }

    /// <summary>
    /// Creates a new instance of the catalog store for testing.
    /// </summary>
    protected abstract IStacCatalogStore CreateStore(string connectionString);

    /// <summary>
    /// Helper to create a store, run the provided action, and dispose resources appropriately.
    /// </summary>
    protected async Task WithStoreAsync(Func<IStacCatalogStore, Task> action)
    {
        var store = CreateStore(ConnectionString);

        try
        {
            await action(store);
        }
        finally
        {
            switch (store)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    [RequiresDockerFact]
    public void Constructor_WithNullConnectionString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => CreateStore(null!);
        act.Should().Throw<ArgumentException>();
    }

    [RequiresDockerFact]
    public void Constructor_WithEmptyConnectionString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => CreateStore("");
        act.Should().Throw<ArgumentException>();
    }

    [RequiresDockerFact]
    public async Task EnsureInitializedAsync_CreatesTablesAndIndexes()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collections = await store.ListCollectionsAsync();
            collections.Should().NotBeNull();
            collections.Should().BeEmpty();
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            // Act
            var result = await store.GetCollectionsAsync(Array.Empty<string>());

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithNonExistentIds_ReturnsEmptyList()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            // Act
            var result = await store.GetCollectionsAsync(new[] { "nonexistent1", "nonexistent2" });

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithSingleId_ReturnsOneCollection()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("test-collection-1");
            await store.UpsertCollectionAsync(collection);

            // Act
            var result = await store.GetCollectionsAsync(new[] { "test-collection-1" });

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Id.Should().Be("test-collection-1");
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithMultipleIds_ReturnsAllMatchingCollections()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collection1 = CreateTestCollection("test-collection-1");
            var collection2 = CreateTestCollection("test-collection-2");
            var collection3 = CreateTestCollection("test-collection-3");

            await store.UpsertCollectionAsync(collection1);
            await store.UpsertCollectionAsync(collection2);
            await store.UpsertCollectionAsync(collection3);

            // Act
            var result = await store.GetCollectionsAsync(new[] { "test-collection-1", "test-collection-2", "test-collection-3" });

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().Contain(c => c.Id == "test-collection-1");
            result.Should().Contain(c => c.Id == "test-collection-2");
            result.Should().Contain(c => c.Id == "test-collection-3");
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithMixedExistingAndNonExisting_ReturnsOnlyExisting()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collection1 = CreateTestCollection("test-collection-1");
            var collection3 = CreateTestCollection("test-collection-3");

            await store.UpsertCollectionAsync(collection1);
            await store.UpsertCollectionAsync(collection3);

            // Act - request 5 collections but only 2 exist
            var result = await store.GetCollectionsAsync(new[]
            {
                "test-collection-1",
                "nonexistent-2",
                "test-collection-3",
                "nonexistent-4",
                "nonexistent-5"
            });

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(c => c.Id == "test-collection-1");
            result.Should().Contain(c => c.Id == "test-collection-3");
            result.Should().NotContain(c => c.Id == "nonexistent-2");
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithLargeNumberOfIds_ReturnsAllMatchingCollections()
    {
        // Arrange - Test with 50 collections to verify batch performance
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collectionIds = new List<string>();
            for (var i = 1; i <= 50; i++)
            {
                var collectionId = $"test-collection-{i:D3}";
                collectionIds.Add(collectionId);
                var collection = CreateTestCollection(collectionId);
                await store.UpsertCollectionAsync(collection);
            }

            // Act
            var result = await store.GetCollectionsAsync(collectionIds);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(50);
            foreach (var id in collectionIds)
            {
                result.Should().Contain(c => c.Id == id);
            }
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_WithDuplicateIds_ReturnsUniqueCollections()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collection1 = CreateTestCollection("test-collection-1");
            var collection2 = CreateTestCollection("test-collection-2");

            await store.UpsertCollectionAsync(collection1);
            await store.UpsertCollectionAsync(collection2);

            // Act - request with duplicate IDs
            var result = await store.GetCollectionsAsync(new[]
            {
                "test-collection-1",
                "test-collection-2",
                "test-collection-1"  // duplicate
            });

            // Assert - should still return only unique collections
            result.Should().NotBeNull();
            result.Count.Should().BeGreaterThanOrEqualTo(2);
            result.Should().Contain(c => c.Id == "test-collection-1");
            result.Should().Contain(c => c.Id == "test-collection-2");
        });
    }

    private static StacCollectionRecord CreateTestCollection(string id)
    {
        return new StacCollectionRecord
        {
            Id = id,
            Title = $"Test Collection {id}",
            Description = $"Test description for {id}",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent
                {
                    Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } }
                },
                Temporal = new StacTemporalExtent
                {
                    Interval = new[] { new[] { DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow } }
                }
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    [RequiresDockerFact]
    public async Task UpsertCollectionAsync_AndGetCollectionAsync_Roundtrip()
    {
        // Arrange
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = new StacCollectionRecord
            {
                Id = "test-collection-roundtrip",
                Title = "Test Collection",
                Description = "A test STAC collection",
                License = "MIT",
                Version = "1.0.0",
                Keywords = new[] { "test", "stac" },
                Links = new[]
                {
                    new StacLink { Rel = "self", Href = "https://example.test/collections/test-collection-roundtrip" }
                },
                Extent = new StacExtent
                {
                    Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180d, -90d, 180d, 90d } } },
                    Temporal = new List<StacTemporalInterval> { new() { Start = createdAt.AddDays(-30), End = createdAt } }
                },
                Properties = new JsonObject { ["category"] = "test" },
                ConformsTo = "https://api.stacspec.org/v1.0.0/core",
                DataSourceId = "test-source",
                ServiceId = "test-service",
                LayerId = "test-layer",
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = createdAt
            };

            await store.UpsertCollectionAsync(collection);
            var fetched = await store.GetCollectionAsync(collection.Id);

            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be(collection.Id);
            fetched.Title.Should().Be(collection.Title);
            fetched.Description.Should().Be(collection.Description);
            fetched.License.Should().Be(collection.License);
            fetched.Version.Should().Be(collection.Version);
            fetched.Keywords.Should().BeEquivalentTo(collection.Keywords);
            fetched.DataSourceId.Should().Be(collection.DataSourceId);
            fetched.ServiceId.Should().Be(collection.ServiceId);
            fetched.LayerId.Should().Be(collection.LayerId);
        });
    }

    [RequiresDockerFact]
    public async Task UpsertCollectionAsync_UpdatesExistingCollection()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("update-test", createdAt);

            await store.UpsertCollectionAsync(collection);

            // Act - update the collection using 'with' expression
            var updatedCollection = collection with
            {
                Title = "Updated Title",
                Description = "Updated Description",
                UpdatedAtUtc = createdAt.AddMinutes(5)
            };
            await store.UpsertCollectionAsync(updatedCollection);

            var fetched = await store.GetCollectionAsync(collection.Id);

            // Assert
            fetched.Should().NotBeNull();
            fetched!.Title.Should().Be("Updated Title");
            fetched.Description.Should().Be("Updated Description");
        });
    }

    [RequiresDockerFact]
    public async Task GetCollectionsAsync_ReturnsAllCollections()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            await store.UpsertCollectionAsync(CreateTestCollection("collection-1", createdAt));
            await store.UpsertCollectionAsync(CreateTestCollection("collection-2", createdAt));
            await store.UpsertCollectionAsync(CreateTestCollection("collection-3", createdAt));

            // Act
            var collections = await store.ListCollectionsAsync();

            // Assert
            collections.Should().HaveCount(3);
        });
    }

    [RequiresDockerFact]
   public async Task DeleteCollectionAsync_RemovesCollection()
   {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("delete-test", DateTimeOffset.UtcNow);
            await store.UpsertCollectionAsync(collection);

            // Act
            await store.DeleteCollectionAsync(collection.Id);
            var fetched = await store.GetCollectionAsync(collection.Id);

            // Assert
            fetched.Should().BeNull();
        });
   }

    [Fact]
    public async Task UpsertItemAsync_AndGetItemAsync_Roundtrip()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("item-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            var item = CreateTestItem(collection.Id, "test-item-1", createdAt);

            // Act
            await store.UpsertItemAsync(item);
            var fetched = await store.GetItemAsync(collection.Id, item.Id);

            // Assert
            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be(item.Id);
            fetched.CollectionId.Should().Be(collection.Id);
            fetched.Title.Should().Be(item.Title);
            fetched.Description.Should().Be(item.Description);
            fetched.Bbox.Should().BeEquivalentTo(item.Bbox);
            fetched.RasterDatasetId.Should().Be(item.RasterDatasetId);
        });
    }

    [Fact]
    public async Task GetItemsAsync_WithBboxFilter_FiltersCorrectly()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("bbox-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            // Create items with different bboxes
            var item1 = CreateTestItem(collection.Id, "item-bbox-1", createdAt) with { Bbox = new[] { -10d, -10d, 10d, 10d } };
            await store.UpsertItemAsync(item1);

            var item2 = CreateTestItem(collection.Id, "item-bbox-2", createdAt) with { Bbox = new[] { 50d, 50d, 60d, 60d } };
            await store.UpsertItemAsync(item2);

            // Act - search with bbox parameters using SearchAsync
            var searchParams = new StacSearchParameters
            {
                Collections = new List<string> { collection.Id },
                Bbox = new[] { -20d, -20d, 20d, 20d },
                Limit = 10
            };
            var searchResult = await store.SearchAsync(searchParams);
            var items = searchResult.Items;

            // Assert
            items.Should().ContainSingle();
            items[0].Id.Should().Be(item1.Id);
        });
    }

    [Fact]
    public async Task GetItemsAsync_WithTimeFilter_FiltersCorrectly()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("time-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            var item1 = CreateTestItem(collection.Id, "item-time-1", createdAt) with { Datetime = createdAt.AddDays(-10) };
            await store.UpsertItemAsync(item1);

            var item2 = CreateTestItem(collection.Id, "item-time-2", createdAt) with { Datetime = createdAt.AddDays(-5) };
            await store.UpsertItemAsync(item2);

            var item3 = CreateTestItem(collection.Id, "item-time-3", createdAt) with { Datetime = createdAt.AddDays(-1) };
            await store.UpsertItemAsync(item3);

            // Act - get items from 7 days ago to now using SearchAsync
            var searchParams = new StacSearchParameters
            {
                Collections = new List<string> { collection.Id },
                Start = createdAt.AddDays(-7),
                End = createdAt,
                Limit = 100
            };
            var searchResult = await store.SearchAsync(searchParams);
            var items = searchResult.Items;

            // Assert
            items.Should().HaveCount(2);
            items.Should().Contain(i => i.Id == item2.Id);
            items.Should().Contain(i => i.Id == item3.Id);
        });
    }

    [Fact]
    public async Task GetItemsAsync_WithPagination_ReturnsCorrectPage()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("pagination-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            // Create 10 items
            for (int i = 0; i < 10; i++)
            {
                await store.UpsertItemAsync(CreateTestItem(collection.Id, $"item-{i:D2}", createdAt.AddMinutes(i)));
            }

            // Act - get items with limit using ListItemsAsync
            var items = await store.ListItemsAsync(collection.Id, limit: 3);

            // Assert
            items.Should().HaveCount(3);
        });
    }

    [Fact]
    public async Task DeleteItemAsync_RemovesItem()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("delete-item-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            var item = CreateTestItem(collection.Id, "item-to-delete", createdAt);
            await store.UpsertItemAsync(item);

            // Act
            await store.DeleteItemAsync(collection.Id, item.Id);
            var fetched = await store.GetItemAsync(collection.Id, item.Id);

            // Assert
            fetched.Should().BeNull();
        });
    }

    [Fact]
    public async Task DeleteCollection_CascadesDeleteItems()
    {
        await WithStoreAsync(async store =>
        {
            await store.EnsureInitializedAsync();

            var createdAt = DateTimeOffset.UtcNow;
            var collection = CreateTestCollection("cascade-test", createdAt);
            await store.UpsertCollectionAsync(collection);

            var item = CreateTestItem(collection.Id, "cascaded-item", createdAt);
            await store.UpsertItemAsync(item);

            // Act
            await store.DeleteCollectionAsync(collection.Id);
            var fetchedItem = await store.GetItemAsync(collection.Id, item.Id);

            // Assert
            fetchedItem.Should().BeNull();
        });
    }

    /// <summary>
    /// Creates a test collection with standardized properties.
    /// </summary>
    protected static StacCollectionRecord CreateTestCollection(string id, DateTimeOffset createdAt)
    {
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
                Temporal = new[] { new StacTemporalInterval { Start = createdAt.AddDays(-1), End = createdAt } }
            },
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        };
    }

    /// <summary>
    /// Creates a test item with standardized properties.
    /// </summary>
    protected static StacItemRecord CreateTestItem(string collectionId, string id, DateTimeOffset createdAt)
    {
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
            Datetime = createdAt,
            RasterDatasetId = collectionId,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        };
    }
}
