using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// SQLite-specific tests for StacCatalogStore.
/// Inherits all common tests from StacCatalogStoreTestsBase and adds SQLite-specific pagination tests.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class SqliteStacCatalogStoreTests : StacCatalogStoreTestsBase, IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqliteStacCatalogStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"honua-stac-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath};Pooling=false";
    }

    protected override IStacCatalogStore CatalogStore => new SqliteStacCatalogStore(_connectionString);

    protected override string ConnectionString => _connectionString;

    protected override IStacCatalogStore CreateStore(string connectionString)
    {
        return new SqliteStacCatalogStore(connectionString);
    }

    // SQLite-specific pagination tests

    [Fact]
    public async Task ListCollectionsAsync_WithPagination_ReturnsPagedResults()
    {
        using var store = new SqliteStacCatalogStore(_connectionString);
        await store.EnsureInitializedAsync();

        // Create 50 collections
        for (int i = 0; i < 50; i++)
        {
            var collection = new StacCollectionRecord
            {
                Id = $"col-{i:D3}",
                Title = $"Collection {i}",
                Description = $"Test collection {i}",
                License = "CC-BY",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await store.UpsertCollectionAsync(collection);
        }

        // Get first page (limit 10)
        var result1 = await store.ListCollectionsAsync(10, null);
        result1.Collections.Count.Should().Be(10);
        result1.TotalCount.Should().Be(50);
        result1.NextToken.Should().NotBeNullOrEmpty();
        result1.Collections[0].Id.Should().Be("col-000");
        result1.Collections[9].Id.Should().Be("col-009");

        // Get second page
        var result2 = await store.ListCollectionsAsync(10, result1.NextToken);
        result2.Collections.Count.Should().Be(10);
        result2.TotalCount.Should().Be(50);
        result2.NextToken.Should().NotBeNullOrEmpty();
        result2.Collections[0].Id.Should().Be("col-010");
        result2.Collections[9].Id.Should().Be("col-019");

        // Verify no overlap
        result1.Collections.Select(c => c.Id).Should().NotIntersectWith(result2.Collections.Select(c => c.Id));

        // Get last page
        var result5 = await store.ListCollectionsAsync(10, "col-040");
        result5.Collections.Count.Should().Be(9); // Only 9 items remain (041-049)
        result5.TotalCount.Should().Be(50);
        result5.NextToken.Should().BeNullOrEmpty(); // No more results
    }

    [Fact]
    public async Task ListCollectionsAsync_WithLargeLimit_ReturnsAllResults()
    {
        using var store = new SqliteStacCatalogStore(_connectionString);
        await store.EnsureInitializedAsync();

        // Create 5 collections
        for (int i = 0; i < 5; i++)
        {
            var collection = new StacCollectionRecord
            {
                Id = $"test-{i}",
                Title = $"Test {i}",
                License = "CC-BY",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await store.UpsertCollectionAsync(collection);
        }

        // Request more than available
        var result = await store.ListCollectionsAsync(100, null);
        result.Collections.Count.Should().Be(5);
        result.TotalCount.Should().Be(5);
        result.NextToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ListCollectionsAsync_WithLimitNormalization_ClampsToValidRange()
    {
        using var store = new SqliteStacCatalogStore(_connectionString);
        await store.EnsureInitializedAsync();

        // Create 10 collections
        for (int i = 0; i < 10; i++)
        {
            var collection = new StacCollectionRecord
            {
                Id = $"item-{i:D2}",
                Title = $"Item {i}",
                License = "MIT",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await store.UpsertCollectionAsync(collection);
        }

        // Test with limit = 0 (should be normalized to 1)
        var result1 = await store.ListCollectionsAsync(0, null);
        result1.Collections.Count.Should().Be(1);

        // Test with limit = -5 (should be normalized to 1)
        var result2 = await store.ListCollectionsAsync(-5, null);
        result2.Collections.Count.Should().Be(1);

        // Test with limit = 2000 (should be normalized to 1000, but we only have 10 items)
        var result3 = await store.ListCollectionsAsync(2000, null);
        result3.Collections.Count.Should().Be(10);
    }

    [Fact]
    public async Task ListCollectionsAsync_WithEmptyStore_ReturnsEmptyResult()
    {
        using var store = new SqliteStacCatalogStore(_connectionString);
        await store.EnsureInitializedAsync();

        var result = await store.ListCollectionsAsync(10, null);
        result.Collections.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.NextToken.Should().BeNullOrEmpty();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // ignore cleanup failures in CI
            }
        }

        return Task.CompletedTask;
    }
}
