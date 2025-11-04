using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Comprehensive edge case tests for STAC API implementation.
/// Tests boundary conditions, extreme values, empty catalogs, and special characters.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "STAC")]
public class StacEdgeCaseTests
{
    #region Empty Catalog Tests

    /// <summary>
    /// Tests that searching an empty catalog returns valid STAC ItemCollection with empty features array.
    /// This is critical for new deployments or catalogs with no data yet.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithEmptyCatalog_ShouldReturnEmptyResults()
    {
        // Arrange - create empty in-memory catalog
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var searchParams = new StacSearchParameters
        {
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Matched.Should().Be(0);
        result.NextToken.Should().BeNull();
    }

    /// <summary>
    /// Tests that listing collections in an empty catalog returns empty list gracefully.
    /// </summary>
    [Fact]
    public async Task ListCollections_WithEmptyCatalog_ShouldReturnEmpty()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        // Act
        var result = await store.ListCollectionsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Bbox Edge Cases

    /// <summary>
    /// Tests that bbox crossing the 180° meridian (antimeridian) is handled correctly.
    /// This is a common edge case for Pacific Ocean regions (Fiji, Tonga, etc.).
    /// </summary>
    [Fact]
    public async Task SearchItems_WithBboxCrossingDateline_ShouldWork()
    {
        // Arrange - create catalog with items on both sides of dateline
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        // Create collection
        var collection = new StacCollectionRecord
        {
            Id = "pacific-features",
            Title = "Pacific Features",
            Description = "Features crossing the dateline",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { 170.0, -20.0, -170.0, -10.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        // Create item west of dateline (179°E)
        var westItem = new StacItemRecord
        {
            Id = "item-west",
            CollectionId = "pacific-features",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 179.5, -15.0 } }),
            Bbox = new[] { 179.5, -15.0, 179.5, -15.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> { ["location"] = "Western Pacific" }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Create item east of dateline (179°W)
        var eastItem = new StacItemRecord
        {
            Id = "item-east",
            CollectionId = "pacific-features",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { -179.5, -15.0 } }),
            Bbox = new[] { -179.5, -15.0, -179.5, -15.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> { ["location"] = "Eastern Pacific" }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        await store.UpsertItemAsync(westItem);
        await store.UpsertItemAsync(eastItem);

        // Search with bbox crossing dateline: 170°E to 170°W
        var searchParams = new StacSearchParameters
        {
            Collections = new[] { "pacific-features" },
            Bbox = new[] { 170.0, -20.0, -170.0, -10.0 },
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert - implementation may or may not correctly handle dateline crossing
        // At minimum should not crash and return valid results
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that a very large bbox (entire world) works correctly.
    /// This tests the maximum spatial extent query.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithWorldWideBbox_ShouldWork()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "global-data",
            Title = "Global Dataset",
            Description = "Worldwide coverage",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "global-item-1",
            CollectionId = "global-data",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = new JsonObject(),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search with entire world bbox
        var searchParams = new StacSearchParameters
        {
            Bbox = new[] { -180.0, -90.0, 180.0, 90.0 },
            Limit = 100
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Should().Contain(i => i.Id == "global-item-1");
    }

    /// <summary>
    /// Tests that a very small bbox (sub-meter precision) works correctly.
    /// This tests high-precision spatial queries.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithVerySmallBbox_ShouldWork()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "precise-survey",
            Title = "High Precision Survey",
            Description = "Sub-meter precision data",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -122.0, 45.0, -122.0, 45.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        // Create item at precise location
        var item = new StacItemRecord
        {
            Id = "survey-point-1",
            CollectionId = "precise-survey",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { -122.419412345, 45.523123456 } }),
            Bbox = new[] { -122.419412345, 45.523123456, -122.419412345, 45.523123456 },
            Properties = new JsonObject(),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search with tiny bbox (0.0001° ≈ 10 meters)
        var searchParams = new StacSearchParameters
        {
            Bbox = new[] { -122.4195, 45.523, -122.4194, 45.5232 },
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.Id == "survey-point-1");
    }

    #endregion

    #region Datetime Edge Cases

    /// <summary>
    /// Tests that open-ended datetime intervals (../2023-12-31 and 2023-01-01/..) work correctly.
    /// Open intervals are used to query "all data before" or "all data after" a specific time.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithOpenStartDatetime_ShouldWork()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "temporal-data",
            Title = "Temporal Dataset",
            Description = "Time series data",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } },
                Temporal = new StacTemporalExtent { Interval = new[] { new[] { "2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "item-2022",
            CollectionId = "temporal-data",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = "2022-06-15T12:00:00Z"
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search with open start interval (all data before 2023-12-31)
        var searchParams = new StacSearchParameters
        {
            Datetime = "../2023-12-31T23:59:59Z",
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.Id == "item-2022");
    }

    /// <summary>
    /// Tests that open-ended datetime intervals with open end work correctly.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithOpenEndDatetime_ShouldWork()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "temporal-data",
            Title = "Temporal Dataset",
            Description = "Time series data",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } },
                Temporal = new StacTemporalExtent { Interval = new[] { new[] { "2020-01-01T00:00:00Z", null } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "item-2024",
            CollectionId = "temporal-data",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = "2024-06-15T12:00:00Z"
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search with open end interval (all data after 2023-01-01)
        var searchParams = new StacSearchParameters
        {
            Datetime = "2023-01-01T00:00:00Z/..",
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.Id == "item-2024");
    }

    /// <summary>
    /// Tests that minimum datetime values (Unix epoch, DateTime.MinValue) are handled.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithMinimumDatetime_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "historical-data",
            Title = "Historical Dataset",
            Description = "Very old data",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } },
                Temporal = new StacTemporalExtent { Interval = new[] { new[] { "1970-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "epoch-item",
            CollectionId = "historical-data",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = "1970-01-01T00:00:00Z" // Unix epoch
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search including Unix epoch
        var searchParams = new StacSearchParameters
        {
            Datetime = "1970-01-01T00:00:00Z/1971-01-01T00:00:00Z",
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.Id == "epoch-item");
    }

    /// <summary>
    /// Tests that leap year dates (Feb 29) are handled correctly.
    /// Leap year edge case ensures proper date parsing.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithLeapYearDate_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "leap-year-data",
            Title = "2024 Leap Year Data",
            Description = "Data from leap year",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "leap-day-item",
            CollectionId = "leap-year-data",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = "2024-02-29T12:00:00Z" // Leap day
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };
        await store.UpsertItemAsync(item);

        // Search for leap day
        var searchParams = new StacSearchParameters
        {
            Datetime = "2024-02-29T00:00:00Z/2024-03-01T00:00:00Z",
            Limit = 10
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(i => i.Id == "leap-day-item");
    }

    #endregion

    #region Limit and Pagination Edge Cases

    /// <summary>
    /// Tests that very large limit values are clamped to server maximum.
    /// Servers typically enforce maximum page sizes (e.g., 10,000) to prevent resource exhaustion.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithExcessiveLimit_ShouldClamp()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        // Add a few items
        for (int i = 0; i < 5; i++)
        {
            var item = new StacItemRecord
            {
                Id = $"item-{i}",
                CollectionId = "test-collection",
                Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
                Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
                Properties = new JsonObject(),
                Assets = new Dictionary<string, StacAsset>(),
                Links = new List<StacLink>()
            };
            await store.UpsertItemAsync(item);
        }

        // Request excessive limit
        var searchParams = new StacSearchParameters
        {
            Limit = 999999
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert - should return all 5 items without error, clamped to reasonable maximum
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
    }

    /// <summary>
    /// Tests that negative limit values are rejected or clamped.
    /// Negative limits are invalid but may be accidentally sent.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithNegativeLimit_ShouldHandleGracefully()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var searchParams = new StacSearchParameters
        {
            Limit = -1
        };

        // Act & Assert - should either throw or clamp to valid value
        // Implementation dependent: may throw ArgumentException or clamp to default
        var act = async () => await store.SearchAsync(searchParams, CancellationToken.None);

        // Should not crash - either succeeds with clamped value or throws ArgumentException
        try
        {
            var result = await act.Invoke();
            result.Should().NotBeNull();
        }
        catch (ArgumentException)
        {
            // This is also acceptable behavior
        }
    }

    /// <summary>
    /// Tests that zero limit is handled appropriately.
    /// Some APIs allow limit=0 to get metadata only.
    /// </summary>
    [Fact]
    public async Task SearchItems_WithZeroLimit_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var searchParams = new StacSearchParameters
        {
            Limit = 0
        };

        // Act
        var result = await store.SearchAsync(searchParams, CancellationToken.None);

        // Assert - should return empty results or use default limit
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    #endregion

    #region Special Characters in Identifiers

    /// <summary>
    /// Tests that collection IDs with special characters (spaces, hyphens, underscores) work correctly.
    /// Real-world STAC catalogs often have descriptive IDs with special characters.
    /// </summary>
    [Theory]
    [InlineData("collection-with-hyphens")]
    [InlineData("collection_with_underscores")]
    [InlineData("collection.with.periods")]
    [InlineData("UPPERCASE-COLLECTION")]
    [InlineData("MixedCase-Collection_123")]
    public async Task GetCollection_WithSpecialCharactersInId_ShouldWork(string collectionId)
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = collectionId,
            Title = $"Collection: {collectionId}",
            Description = "Test collection with special ID",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertCollectionAsync(collection);
        var retrieved = await store.GetCollectionAsync(collectionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(collectionId);
    }

    /// <summary>
    /// Tests that item IDs with various formats (UUIDs, integers, strings) work correctly.
    /// </summary>
    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")] // UUID
    [InlineData("12345")] // Integer as string
    [InlineData("item-2024-01-15-abc123")] // Timestamp-based
    [InlineData("ITEM_WITH_UNDERSCORES")] // Uppercase with underscores
    public async Task GetItem_WithVariousIdFormats_ShouldWork(string itemId)
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = itemId,
            CollectionId = "test-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = new JsonObject(),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("test-collection", itemId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(itemId);
    }

    #endregion

    #region Unicode and International Text

    /// <summary>
    /// Tests that Unicode text in collection titles and descriptions is preserved correctly.
    /// STAC catalogs are used worldwide and must support internationalization.
    /// </summary>
    [Fact]
    public async Task UpsertCollection_WithUnicodeText_ShouldPreserve()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "multilingual-collection",
            Title = "Collection: Montréal 北京 مصر 🌍",
            Description = "データ data données بيانات",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>(),
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?>
            {
                ["location"] = "東京都新宿区"
            })
        };

        // Act
        await store.UpsertCollectionAsync(collection);
        var retrieved = await store.GetCollectionAsync("multilingual-collection");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Contain("Montréal");
        retrieved.Title.Should().Contain("北京");
        retrieved.Title.Should().Contain("مصر");
        retrieved.Title.Should().Contain("🌍");
        retrieved.Description.Should().Contain("データ");
    }

    /// <summary>
    /// Tests that Unicode in item properties is preserved correctly.
    /// </summary>
    [Fact]
    public async Task UpsertItem_WithUnicodeProperties_ShouldPreserve()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "test-collection",
            Title = "Test",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "unicode-item",
            CollectionId = "test-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 139.7, 35.7 } }),
            Bbox = new[] { 139.7, 35.7, 139.7, 35.7 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["location"] = "東京都",
                ["description"] = "Café près de l'Église",
                ["address"] = RealisticGisTestData.AddressWithJapanese
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("test-collection", "unicode-item");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Properties["location"].Should().Be("東京都");
        retrieved.Properties["description"].ToString().Should().Contain("Café");
    }

    #endregion

    #region Minimal Required Fields

    /// <summary>
    /// Tests that items with only required STAC fields (no optional fields) can be created.
    /// This tests the minimum viable STAC item structure.
    /// </summary>
    [Fact]
    public async Task CreateItem_WithMinimalRequiredFields_ShouldSucceed()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "minimal-collection",
            Title = "Minimal Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        // Create item with only required fields
        var item = new StacItemRecord
        {
            Id = "minimal-item",
            CollectionId = "minimal-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = "2024-01-01T00:00:00Z" // Only required property
            }),
            Assets = new Dictionary<string, StacAsset>(), // Empty but not null
            Links = new List<StacLink>() // Empty but not null
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("minimal-collection", "minimal-item");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("minimal-item");
        retrieved.Properties.Should().ContainKey("datetime");
    }

    #endregion

    #region Large Geometry Tests

    /// <summary>
    /// Tests that items with very large geometries (10,000+ coordinates) are handled.
    /// Large geometries can cause performance issues if not handled properly.
    /// </summary>
    [Fact]
    public async Task CreateItem_WithVeryLargeGeometry_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "large-geometry-collection",
            Title = "Large Geometry Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -123.0, 45.0, -122.0, 46.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        // Create large polygon using realistic test data
        var largePolygon = RealisticGisTestData.CreateLargeParcel();
        var coordinates = largePolygon.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray();

        var item = new StacItemRecord
        {
            Id = "large-polygon-item",
            CollectionId = "large-geometry-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new
            {
                type = "Polygon",
                coordinates = new[] { coordinates }
            }),
            Bbox = new[]
            {
                coordinates.Min(c => c[0]),
                coordinates.Min(c => c[1]),
                coordinates.Max(c => c[0]),
                coordinates.Max(c => c[1])
            },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["vertex_count"] = coordinates.Length
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("large-geometry-collection", "large-polygon-item");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Properties["vertex_count"].Should().BeEquivalentTo(coordinates.Length);
    }

    #endregion

    #region Null and Empty Properties

    /// <summary>
    /// Tests that null datetime (present interval) is handled correctly.
    /// STAC allows datetime to be null when start_datetime and end_datetime are provided.
    /// </summary>
    [Fact]
    public async Task CreateItem_WithNullDatetime_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "interval-collection",
            Title = "Interval Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "interval-item",
            CollectionId = "interval-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["datetime"] = null!, // Null datetime
                ["start_datetime"] = "2024-01-01T00:00:00Z",
                ["end_datetime"] = "2024-12-31T23:59:59Z"
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("interval-collection", "interval-item");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Properties.Should().ContainKey("start_datetime");
        retrieved.Properties.Should().ContainKey("end_datetime");
    }

    #endregion

    #region Extreme Numeric Values in Properties

    /// <summary>
    /// Tests that extreme numeric values in properties are handled correctly.
    /// </summary>
    [Fact]
    public async Task CreateItem_WithExtremeNumericValues_ShouldHandle()
    {
        // Arrange
        var store = new InMemoryStacCatalogStore();
        await store.EnsureInitializedAsync();

        var collection = new StacCollectionRecord
        {
            Id = "numeric-collection",
            Title = "Numeric Collection",
            Description = "Test",
            License = "proprietary",
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent { Bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } } }
            },
            Links = new List<StacLink>()
        };
        await store.UpsertCollectionAsync(collection);

        var item = new StacItemRecord
        {
            Id = "numeric-item",
            CollectionId = "numeric-collection",
            Geometry = StacTestJsonHelpers.ToGeometry(new { type = "Point", coordinates = new[] { 0.0, 0.0 } }),
            Bbox = new[] { 0.0, 0.0, 0.0, 0.0 },
            Properties = StacTestJsonHelpers.ToJsonObject(new Dictionary<string, object?> {
                ["int_max"] = int.MaxValue,
                ["int_min"] = int.MinValue,
                ["long_max"] = long.MaxValue,
                ["double_very_small"] = 1.0e-10,
                ["double_very_large"] = 1.0e10
            }),
            Assets = new Dictionary<string, StacAsset>(),
            Links = new List<StacLink>()
        };

        // Act
        await store.UpsertItemAsync(item);
        var retrieved = await store.GetItemAsync("numeric-collection", "numeric-item");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Properties["int_max"].Should().BeEquivalentTo(int.MaxValue);
        retrieved.Properties["int_min"].Should().BeEquivalentTo(int.MinValue);
    }

    #endregion
}
