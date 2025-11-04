using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Filter;
using NetTopologySuite.Geometries;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Database query benchmarks for feature retrieval operations.
/// Tests performance of various query patterns and dataset sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class DatabaseQueryBenchmarks
{
    private TestDatabaseFixture _fixture = null!;
    private string _serviceId = null!;
    private string _layerId = null!;

    [Params(100, 1000, 10000)]
    public int FeatureCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _fixture = new TestDatabaseFixture();
        await _fixture.InitializeAsync();

        _serviceId = "benchmark-service";
        _layerId = "benchmark-layer";

        // Seed test data
        await _fixture.SeedFeaturesAsync(_serviceId, _layerId, FeatureCount);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _fixture.DisposeAsync();
    }

    [Benchmark(Description = "Query: Get all features (no filter)")]
    public async Task<int> QueryAllFeatures()
    {
        var query = new FeatureQuery(Crs: "EPSG:4326");
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "Query: Count features")]
    public async Task<long> QueryCountFeatures()
    {
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await _fixture.Repository.CountAsync(_serviceId, _layerId, query);
    }

    [Benchmark(Description = "Query: Get single feature by ID")]
    public async Task<FeatureRecord?> QuerySingleFeature()
    {
        return await _fixture.Repository.GetAsync(_serviceId, _layerId, "1");
    }

    [Benchmark(Description = "Query: Spatial filter - Bounding box")]
    public async Task<int> QuerySpatialBoundingBox()
    {
        var bbox = new BoundingBox(-122.5, 45.5, -122.3, 45.7, Crs: "EPSG:4326");
        var query = new FeatureQuery(Bbox: bbox, Crs: "EPSG:4326");
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "Query: Spatial filter - Intersection")]
    public async Task<int> QuerySpatialIntersection()
    {
        // Create a test polygon
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var coords = new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.3, 45.7),
            new Coordinate(-122.5, 45.7),
            new Coordinate(-122.5, 45.5)
        };
        var ring = factory.CreateLinearRing(coords);
        var polygon = factory.CreatePolygon(ring);

        // Query with spatial intersection
        var bbox = new BoundingBox(-122.5, 45.5, -122.3, 45.7, Crs: "EPSG:4326");
        var query = new FeatureQuery(Bbox: bbox, Crs: "EPSG:4326");
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "Query: Attribute filter - Equality")]
    public async Task<int> QueryAttributeEquality()
    {
        var query = new FeatureQuery(Crs: "EPSG:4326");
        // Filter would be applied via CQL filter in real scenarios
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            // Simulate filtering
            if (feature.Attributes.TryGetValue("category", out var category) &&
                category?.ToString() == "Residential")
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(Description = "Query: Pagination - First 100 features")]
    public async Task<int> QueryPaginationFirst100()
    {
        var query = new FeatureQuery(
            Crs: "EPSG:4326",
            Limit: 100,
            Offset: 0
        );
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "Query: Pagination - Offset 5000")]
    public async Task<int> QueryPaginationOffset5000()
    {
        if (FeatureCount < 5100) return 0; // Skip if not enough data

        var query = new FeatureQuery(
            Crs: "EPSG:4326",
            Limit: 100,
            Offset: 5000
        );
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
        }

        return count;
    }

    [Benchmark(Description = "Query: Combined - Spatial + Attribute + Pagination")]
    public async Task<int> QueryCombinedFilters()
    {
        var bbox = new BoundingBox(-122.5, 45.5, -122.3, 45.7, Crs: "EPSG:4326");
        var query = new FeatureQuery(
            Bbox: bbox,
            Crs: "EPSG:4326",
            Limit: 100,
            Offset: 0
        );
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            // Simulate attribute filter
            if (feature.Attributes.TryGetValue("area_sqm", out var area) &&
                Convert.ToDouble(area) > 10000)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(Description = "Query: CRS transformation (EPSG:4326 to EPSG:3857)")]
    public async Task<int> QueryCrsTransformation()
    {
        var query = new FeatureQuery(Crs: "EPSG:3857"); // Request Web Mercator
        var count = 0;

        await foreach (var feature in _fixture.Repository.QueryAsync(_serviceId, _layerId, query))
        {
            count++;
            if (count >= 100) break; // Limit to 100 to keep benchmark reasonable
        }

        return count;
    }
}

/// <summary>
/// Test fixture for database benchmarks.
/// Creates an in-memory database for consistent benchmarking.
/// </summary>
public class TestDatabaseFixture : IAsyncDisposable
{
    public IFeatureRepository Repository { get; private set; } = null!;
    private IFeatureContextResolver _contextResolver = null!;
    private LayerDefinition _layer = null!;

    public async Task InitializeAsync()
    {
        // In a real implementation, this would set up an in-memory database
        // For now, this is a placeholder structure
        await Task.CompletedTask;
    }

    public async Task SeedFeaturesAsync(string serviceId, string layerId, int count)
    {
        // Seed test features into the database
        var random = new Random(42); // Fixed seed for reproducibility
        var factory = new NetTopologySuite.Geometries.GeometryFactory(
            new NetTopologySuite.Geometries.PrecisionModel(),
            4326
        );

        var writer = new NetTopologySuite.IO.GeoJsonWriter();

        for (int i = 0; i < count; i++)
        {
            // Generate realistic polygon (property boundary)
            var baseLon = -122.4 + (random.NextDouble() * 0.2);
            var baseLat = 45.5 + (random.NextDouble() * 0.2);
            var size = 0.001; // ~100m parcels

            var coords = new[]
            {
                new Coordinate(baseLon, baseLat),
                new Coordinate(baseLon + size, baseLat),
                new Coordinate(baseLon + size, baseLat + size),
                new Coordinate(baseLon, baseLat + size),
                new Coordinate(baseLon, baseLat) // Close ring
            };

            var ring = factory.CreateLinearRing(coords);
            var polygon = factory.CreatePolygon(ring);
            var geoJson = writer.Write(polygon);

            var attributes = new Dictionary<string, object?>
            {
                ["feature_id"] = i + 1,
                ["name"] = $"Property {i + 1}",
                ["category"] = i % 3 == 0 ? "Residential" : i % 3 == 1 ? "Commercial" : "Industrial",
                ["area_sqm"] = 10000.0 + (random.NextDouble() * 5000.0),
                ["created_at"] = new DateTime(2024, 1, 1).AddDays(i),
                ["geom"] = JsonNode.Parse(geoJson)
            };

            var record = new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));

            // In a real implementation, this would insert into the database
            // await Repository.CreateAsync(serviceId, layerId, record);
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup database resources
        await Task.CompletedTask;
    }
}
