using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Tests.Shared;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Apis.Stac;

/// <summary>
/// Performance benchmark tests comparing single insert vs bulk insert.
/// </summary>
[Collection("SharedPostgres")]
public sealed class BulkUpsertPerformanceBenchmark : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly SharedPostgresFixture _fixture;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public BulkUpsertPerformanceBenchmark(ITestOutputHelper output, SharedPostgresFixture fixture)
    {
        _output = output;
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
    public async Task Benchmark_SingleInsert_100Items()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("benchmark-single");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 100)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        foreach (var item in items)
        {
            await store.UpsertItemAsync(item);
        }
        stopwatch.Stop();

        // Assert & Report
        var throughput = items.Count / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Single Insert - 100 items:");
        _output.WriteLine($"  Duration: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Throughput: {throughput:F2} items/sec");

        stopwatch.Elapsed.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmark_BulkInsert_100Items()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("benchmark-bulk");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 100)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D3}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert & Report
        _output.WriteLine($"Bulk Insert - 100 items:");
        _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
        _output.WriteLine($"  Success: {result.SuccessCount}/{items.Count}");

        result.SuccessCount.Should().Be(100);
        result.Duration.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmark_SingleInsert_1000Items()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("benchmark-single-1k");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 1000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        foreach (var item in items)
        {
            await store.UpsertItemAsync(item);
        }
        stopwatch.Stop();

        // Assert & Report
        var throughput = items.Count / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Single Insert - 1000 items:");
        _output.WriteLine($"  Duration: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Throughput: {throughput:F2} items/sec");

        stopwatch.Elapsed.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmark_BulkInsert_1000Items()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("benchmark-bulk-1k");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 1000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert & Report
        _output.WriteLine($"Bulk Insert - 1000 items:");
        _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
        _output.WriteLine($"  Success: {result.SuccessCount}/{items.Count}");

        result.SuccessCount.Should().Be(1000);
        result.Duration.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmark_BulkInsert_5000Items()
    {
        // Arrange
        using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
        await store.EnsureInitializedAsync();

        var collection = CreateTestCollection("benchmark-bulk-5k");
        await store.UpsertCollectionAsync(collection);

        var items = Enumerable.Range(1, 5000)
            .Select(i => CreateTestItem(collection.Id, $"item-{i:D5}"))
            .ToList();

        // Act
        var result = await store.BulkUpsertItemsAsync(items);

        // Assert & Report
        _output.WriteLine($"Bulk Insert - 5000 items:");
        _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
        _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
        _output.WriteLine($"  Success: {result.SuccessCount}/{items.Count}");

        result.SuccessCount.Should().Be(5000);
        result.Duration.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Benchmark_BulkInsertWithDifferentBatchSizes()
    {
        _output.WriteLine("Bulk Insert Performance with Different Batch Sizes (1000 items):");
        _output.WriteLine("================================================================");

        var batchSizes = new[] { 100, 500, 1000, 2000 };

        foreach (var batchSize in batchSizes)
        {
            // Arrange
            using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection($"benchmark-batch-{batchSize}");
            await store.UpsertCollectionAsync(collection);

            var items = Enumerable.Range(1, 1000)
                .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
                .ToList();

            var options = new BulkUpsertOptions { BatchSize = batchSize };

            // Act
            var result = await store.BulkUpsertItemsAsync(items, options);

            // Report
            _output.WriteLine($"Batch Size {batchSize}:");
            _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
            _output.WriteLine("");

            result.SuccessCount.Should().Be(1000);
        }
    }

    [Fact]
    public async Task Benchmark_ComparePostgresOptimizedVsUnoptimized()
    {
        _output.WriteLine("PostgreSQL: Optimized (COPY) vs Unoptimized (Individual Inserts)");
        _output.WriteLine("===================================================================");

        var itemCount = 1000;

        // Test with optimization enabled
        {
            using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("benchmark-optimized");
            await store.UpsertCollectionAsync(collection);

            var items = Enumerable.Range(1, itemCount)
                .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
                .ToList();

            var options = new BulkUpsertOptions { UseBulkInsertOptimization = true };
            var result = await store.BulkUpsertItemsAsync(items, options);

            _output.WriteLine($"Optimized (COPY):");
            _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
            _output.WriteLine("");

            result.SuccessCount.Should().Be(itemCount);
        }

        // Test with optimization disabled
        {
            using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("benchmark-unoptimized");
            await store.UpsertCollectionAsync(collection);

            var items = Enumerable.Range(1, itemCount)
                .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
                .ToList();

            var options = new BulkUpsertOptions { UseBulkInsertOptimization = false };
            var result = await store.BulkUpsertItemsAsync(items, options);

            _output.WriteLine($"Unoptimized (Individual Inserts):");
            _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");

            result.SuccessCount.Should().Be(itemCount);
        }
    }

    [Fact]
    public async Task Benchmark_InMemoryVsPostgres()
    {
        _output.WriteLine("InMemory vs PostgreSQL Performance (1000 items)");
        _output.WriteLine("================================================");

        var itemCount = 1000;

        // Test InMemory
        {
            var store = new InMemoryStacCatalogStore();
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("benchmark-inmemory");
            await store.UpsertCollectionAsync(collection);

            var items = Enumerable.Range(1, itemCount)
                .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
                .ToList();

            var result = await store.BulkUpsertItemsAsync(items);

            _output.WriteLine($"InMemory:");
            _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");
            _output.WriteLine("");

            result.SuccessCount.Should().Be(itemCount);
        }

        // Test PostgreSQL
        {
            using var store = new PostgresStacCatalogStore(_fixture.ConnectionString);
            await store.EnsureInitializedAsync();

            var collection = CreateTestCollection("benchmark-postgres");
            await store.UpsertCollectionAsync(collection);

            var items = Enumerable.Range(1, itemCount)
                .Select(i => CreateTestItem(collection.Id, $"item-{i:D4}"))
                .ToList();

            var result = await store.BulkUpsertItemsAsync(items);

            _output.WriteLine($"PostgreSQL:");
            _output.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2} ms");
            _output.WriteLine($"  Throughput: {result.ItemsPerSecond:F2} items/sec");

            result.SuccessCount.Should().Be(itemCount);
        }
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
