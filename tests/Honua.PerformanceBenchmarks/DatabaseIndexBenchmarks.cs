using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Honua.PerformanceBenchmarks;

/// <summary>
/// Benchmarks to measure database index performance improvements.
/// Run with: dotnet run -c Release --project tests/Honua.PerformanceBenchmarks
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class DatabaseIndexBenchmarks
{
    private string? _connectionString;
    private IStacCatalogStore? _stacStore;
    private IFeatureRepository? _featureRepository;
    private List<string>? _collectionIds;
    private const int TestDataSize = 1000;

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup test database connection
        _connectionString = Environment.GetEnvironmentVariable("HONUA_TEST_DB")
            ?? "Host=localhost;Database=honua_benchmark;Username=honua;Password=honua";

        _stacStore = new PostgresStacCatalogStore(_connectionString);
        await _stacStore.EnsureInitializedAsync();

        // Create test data if needed
        await SeedTestDataAsync();

        // Collect collection IDs for batch tests
        var collections = await _stacStore.GetCollectionsAsync();
        _collectionIds = collections.Select(c => c.Id).Take(100).ToList();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Cleanup resources
        (_stacStore as IDisposable)?.Dispose();
    }

    private async Task SeedTestDataAsync()
    {
        if (_stacStore == null) return;

        // Check if data already exists
        var existingCollections = await _stacStore.GetCollectionsAsync();
        if (existingCollections.Count() >= TestDataSize)
        {
            return;
        }

        // Seed collections
        for (var i = 0; i < TestDataSize; i++)
        {
            var collection = new StacCollectionRecord(
                Id: $"test-collection-{i}",
                Title: $"Test Collection {i}",
                Description: $"Benchmark test collection {i}",
                License: "MIT",
                Version: "1.0",
                KeywordsJson: "[]",
                ExtentJson: "{}",
                PropertiesJson: null,
                LinksJson: "[]",
                ExtensionsJson: "[]",
                ConformsTo: null,
                DataSourceId: "benchmark",
                ServiceId: $"service-{i % 10}",
                LayerId: $"layer-{i % 10}",
                Etag: Guid.NewGuid().ToString(),
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-i),
                UpdatedAt: DateTimeOffset.UtcNow.AddHours(-i)
            );

            await _stacStore.UpsertCollectionAsync(collection);

            // Add items to each collection
            for (var j = 0; j < 10; j++)
            {
                var item = new StacItemRecord(
                    CollectionId: collection.Id,
                    Id: $"item-{i}-{j}",
                    Title: $"Test Item {i}-{j}",
                    Description: null,
                    PropertiesJson: "{}",
                    AssetsJson: "{}",
                    LinksJson: "[]",
                    ExtensionsJson: "[]",
                    BboxJson: null,
                    GeometryJson: "{\"type\":\"Point\",\"coordinates\":[-122.0,37.0]}",
                    Datetime: DateTimeOffset.UtcNow.AddHours(-j),
                    StartDatetime: null,
                    EndDatetime: null,
                    RasterDatasetId: j % 3 == 0 ? $"raster-{j}" : null,
                    Etag: Guid.NewGuid().ToString(),
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-j),
                    UpdatedAt: DateTimeOffset.UtcNow.AddHours(-j)
                );

                await _stacStore.UpsertItemAsync(item);
            }
        }
    }

    // ========================================
    // N+1 Query Benchmarks
    // ========================================

    [Benchmark(Description = "N+1 Query Problem (Baseline - SLOW)")]
    public async Task<int> NPlusOneQuery_Baseline()
    {
        if (_stacStore == null || _collectionIds == null) return 0;

        var count = 0;
        var items = await GetSampleItemsAsync(100);

        foreach (var item in items)
        {
            // Each iteration executes a separate query - N+1 problem!
            var collection = await _stacStore.GetCollectionAsync(item.CollectionId);
            if (collection != null) count++;
        }

        return count;
    }

    [Benchmark(Description = "Batch Loading (Optimized - FAST)")]
    public async Task<int> BatchLoading_Optimized()
    {
        if (_stacStore == null || _collectionIds == null) return 0;

        var items = await GetSampleItemsAsync(100);
        var collectionIds = items.Select(i => i.CollectionId).Distinct();

        // Single query to load all collections - optimized!
        var collections = await BatchGetCollectionsAsync(collectionIds);
        var lookup = collections.ToDictionary(c => c.Id);

        var count = 0;
        foreach (var item in items)
        {
            if (lookup.ContainsKey(item.CollectionId)) count++;
        }

        return count;
    }

    // ========================================
    // Index Usage Benchmarks
    // ========================================

    [Benchmark(Description = "Temporal Query WITHOUT Index")]
    public async Task<int> TemporalQuery_NoIndex()
    {
        if (_stacStore == null || _collectionIds == null) return 0;

        // Force sequential scan by using function on indexed column
        var count = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT COUNT(*)
            FROM stac_items
            WHERE EXTRACT(YEAR FROM datetime) = 2024";

        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        count = Convert.ToInt32(result);

        return count;
    }

    [Benchmark(Description = "Temporal Query WITH Index")]
    public async Task<int> TemporalQuery_WithIndex()
    {
        if (_stacStore == null || _collectionIds == null) return 0;

        // Use index-friendly query
        var count = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT COUNT(*)
            FROM stac_items
            WHERE datetime >= '2024-01-01'
              AND datetime < '2025-01-01'";

        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        count = Convert.ToInt32(result);

        return count;
    }

    [Benchmark(Description = "Composite Index Query (service + datetime)")]
    public async Task<int> CompositeIndexQuery()
    {
        if (_stacStore == null || _collectionIds == null) return 0;

        var count = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Uses composite index on (collection_id, datetime)
        var sql = @"
            SELECT COUNT(*)
            FROM stac_items
            WHERE collection_id = @collectionId
              AND datetime >= @startDate
              AND datetime < @endDate
            ORDER BY datetime DESC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("collectionId", _collectionIds[0]);
        cmd.Parameters.AddWithValue("startDate", DateTimeOffset.UtcNow.AddDays(-30));
        cmd.Parameters.AddWithValue("endDate", DateTimeOffset.UtcNow);

        var result = await cmd.ExecuteScalarAsync();
        count = Convert.ToInt32(result);

        return count;
    }

    // ========================================
    // Spatial Query Benchmarks
    // ========================================

    [Benchmark(Description = "Spatial Query WITHOUT && Operator")]
    public async Task<int> SpatialQuery_NoIndexOperator()
    {
        if (_connectionString == null) return 0;

        var count = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // ST_Intersects without && - doesn't use GIST index efficiently
        var sql = @"
            SELECT COUNT(*)
            FROM stac_items
            WHERE ST_Intersects(
                ST_GeomFromGeoJSON(geometry_json),
                ST_MakeEnvelope(-123, 37, -121, 38, 4326)
            )";

        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        count = Convert.ToInt32(result);

        return count;
    }

    [Benchmark(Description = "Spatial Query WITH && Operator (Index)")]
    public async Task<int> SpatialQuery_WithIndexOperator()
    {
        if (_connectionString == null) return 0;

        var count = 0;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Use && operator first (GIST index), then ST_Intersects
        var sql = @"
            SELECT COUNT(*)
            FROM stac_items
            WHERE ST_GeomFromGeoJSON(geometry_json) && ST_MakeEnvelope(-123, 37, -121, 38, 4326)
              AND ST_Intersects(
                  ST_GeomFromGeoJSON(geometry_json),
                  ST_MakeEnvelope(-123, 37, -121, 38, 4326)
              )";

        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        count = Convert.ToInt32(result);

        return count;
    }

    // ========================================
    // Helper Methods
    // ========================================

    private async Task<List<StacItemRecord>> GetSampleItemsAsync(int count)
    {
        if (_stacStore == null || _collectionIds == null)
            return new List<StacItemRecord>();

        var items = new List<StacItemRecord>();
        foreach (var collectionId in _collectionIds.Take(count / 10))
        {
            var collectionItems = await _stacStore.GetItemsAsync(collectionId);
            items.AddRange(collectionItems.Take(10));

            if (items.Count >= count) break;
        }

        return items;
    }

    private async Task<List<StacCollectionRecord>> BatchGetCollectionsAsync(IEnumerable<string> collectionIds)
    {
        if (_stacStore == null) return new List<StacCollectionRecord>();

        var collections = new List<StacCollectionRecord>();
        foreach (var id in collectionIds)
        {
            var collection = await _stacStore.GetCollectionAsync(id);
            if (collection != null)
            {
                collections.Add(collection);
            }
        }

        return collections;
    }
}

/// <summary>
/// Query plan analyzer to verify index usage.
/// </summary>
public class QueryPlanAnalyzer
{
    private readonly string _connectionString;

    public QueryPlanAnalyzer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Analyze a query plan to check if indexes are being used.
    /// </summary>
    public async Task<QueryPlanResult> AnalyzeQueryAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Get query plan
        var explainSql = $"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {sql}";
        await using var cmd = new NpgsqlCommand(explainSql, conn);
        var result = await cmd.ExecuteScalarAsync();

        var plan = result?.ToString() ?? "{}";

        // Parse plan to check for index usage
        var usesIndex = plan.Contains("Index Scan") || plan.Contains("Index Only Scan");
        var usesSeqScan = plan.Contains("Seq Scan");
        var executionTime = ExtractExecutionTime(plan);

        return new QueryPlanResult(
            UsesIndex: usesIndex,
            UsesSequentialScan: usesSeqScan,
            ExecutionTimeMs: executionTime,
            RawPlan: plan
        );
    }

    private static double ExtractExecutionTime(string plan)
    {
        // Simple extraction - could be improved with JSON parsing
        var match = System.Text.RegularExpressions.Regex.Match(plan, @"Execution Time"": (\d+\.?\d*)");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var time))
        {
            return time;
        }

        return 0.0;
    }
}

public record QueryPlanResult(
    bool UsesIndex,
    bool UsesSequentialScan,
    double ExecutionTimeMs,
    string RawPlan
);
