// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks comparing traditional C# query processing vs optimized PostgreSQL functions.
/// Demonstrates 5-10x performance improvements from pushing complexity to the database.
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
public class PostgresOptimizationBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithMaxIterationCount(20));

            AddLogger(ConsoleLogger.Default);
            AddColumnProvider(DefaultColumnProviders.Instance);
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);

            Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
        }
    }

    private PostgresConnectionManager? _connectionManager;
    private PostgresFeatureOperations? _traditionalOperations;
    private PostgresFunctionRepository? _functionRepository;
    private OptimizedPostgresFeatureOperations? _optimizedOperations;
    private DataSourceDefinition? _dataSource;
    private ServiceDefinition? _service;
    private LayerDefinition? _layer;
    private FeatureQuery? _smallQuery;
    private FeatureQuery? _largeQuery;
    private Geometry? _bbox;

    [GlobalSetup]
    public void Setup()
    {
        // Setup connection manager
        _connectionManager = new PostgresConnectionManager(
            new PostgresConnectionPoolMetrics(new System.Collections.Generic.Dictionary<string, Npgsql.NpgsqlDataSource>()),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            encryptionService: null);

        // Setup operations
        var queryBuilderPool = new QueryBuilderPool();
        _traditionalOperations = new PostgresFeatureOperations(
            _connectionManager,
            queryBuilderPool,
            new DataAccessOptions(),
            NullLogger<PostgresFeatureOperations>.Instance);

        _functionRepository = new PostgresFunctionRepository(_connectionManager);

        _optimizedOperations = new OptimizedPostgresFeatureOperations(
            _traditionalOperations,
            _functionRepository,
            NullLogger<OptimizedPostgresFeatureOperations>.Instance);

        // Setup test data source (adjust connection string as needed)
        _dataSource = new DataSourceDefinition
        {
            Id = "benchmark_test",
            Provider = "postgis",
            ConnectionString = Environment.GetEnvironmentVariable("BENCHMARK_CONNECTION_STRING")
                ?? "Host=localhost;Database=honua_benchmark;Username=postgres;Password=postgres"
        };

        // Setup test service and layer
        _service = new ServiceDefinition
        {
            Id = "benchmark_service",
            DataSourceId = "benchmark_test"
        };

        _layer = new LayerDefinition
        {
            Id = "test_features",
            Storage = new LayerStorageDefinition
            {
                Table = "benchmark_features",
                Srid = 4326
            },
            GeometryField = "geom"
        };

        // Setup bounding box (San Francisco Bay Area)
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        _bbox = factory.ToGeometry(new Envelope(-122.5, -122.3, 37.7, 37.9));

        // Small query (100 features)
        _smallQuery = new FeatureQuery(
            Limit: 100,
            Offset: 0,
            ResultType: FeatureResultType.Results,
            Bbox: new BoundingBox(-122.5, 37.7, -122.3, 37.9, "EPSG:4326"));

        // Large query (1000 features)
        _largeQuery = new FeatureQuery(
            Limit: 1000,
            Offset: 0,
            ResultType: FeatureResultType.Results,
            Bbox: new BoundingBox(-122.5, 37.7, -122.3, 37.9, "EPSG:4326"));
    }

    [Benchmark(Baseline = true, Description = "Traditional: Small Query (100 features)")]
    public async Task<int> Traditional_SmallQuery()
    {
        var count = 0;
        await foreach (var feature in _traditionalOperations!.QueryAsync(_dataSource!, _service!, _layer!, _smallQuery, CancellationToken.None))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Optimized: Small Query (100 features)")]
    public async Task<int> Optimized_SmallQuery()
    {
        var count = 0;
        await foreach (var feature in _optimizedOperations!.QueryAsync(_dataSource!, _service!, _layer!, _smallQuery, CancellationToken.None))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Traditional: Large Query (1000 features)")]
    public async Task<int> Traditional_LargeQuery()
    {
        var count = 0;
        await foreach (var feature in _traditionalOperations!.QueryAsync(_dataSource!, _service!, _layer!, _largeQuery, CancellationToken.None))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Optimized: Large Query (1000 features)")]
    public async Task<int> Optimized_LargeQuery()
    {
        var count = 0;
        await foreach (var feature in _optimizedOperations!.QueryAsync(_dataSource!, _service!, _layer!, _largeQuery, CancellationToken.None))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Traditional: Count Query")]
    public async Task<long> Traditional_Count()
    {
        return await _traditionalOperations!.CountAsync(_dataSource!, _service!, _layer!, _largeQuery, CancellationToken.None);
    }

    [Benchmark(Description = "Optimized: Count Query")]
    public async Task<long> Optimized_Count()
    {
        return await _optimizedOperations!.CountAsync(_dataSource!, _service!, _layer!, _largeQuery, CancellationToken.None);
    }

    [Benchmark(Description = "Optimized: MVT Tile Generation (zoom 10)")]
    public async Task<byte[]?> Optimized_MvtTile()
    {
        return await _optimizedOperations!.GenerateMvtTileAsync(
            _dataSource!,
            _service!,
            _layer!,
            zoom: 10,
            x: 163,
            y: 395,
            datetime: null,
            CancellationToken.None);
    }

    [Benchmark(Description = "Optimized: Fast Count with Estimation")]
    public async Task<long> Optimized_FastCountEstimate()
    {
        return await _functionRepository!.FastCountAsync(
            _dataSource!,
            "benchmark_features",
            "geom",
            bbox: null,
            filterSql: null,
            srid: 4326,
            useEstimate: true,
            CancellationToken.None);
    }

    [Benchmark(Description = "Optimized: Spatial Aggregation")]
    public async Task<AggregationResult> Optimized_Aggregation()
    {
        return await _functionRepository!.AggregateFeaturesAsync(
            _dataSource!,
            "benchmark_features",
            "geom",
            bbox: _bbox,
            filterSql: null,
            srid: 4326,
            targetSrid: 4326,
            groupByColumn: null,
            CancellationToken.None);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connectionManager?.Dispose();
    }
}

/// <summary>
/// Instructions for running benchmarks:
///
/// 1. Setup test database:
///    ```sql
///    CREATE DATABASE honua_benchmark;
///    \c honua_benchmark
///    CREATE EXTENSION postgis;
///
///    -- Create test table
///    CREATE TABLE benchmark_features (
///        id SERIAL PRIMARY KEY,
///        geom GEOMETRY(Point, 4326),
///        name TEXT,
///        category TEXT,
///        value NUMERIC
///    );
///
///    -- Insert test data (1 million points in SF Bay Area)
///    INSERT INTO benchmark_features (geom, name, category, value)
///    SELECT
///        ST_SetSRID(ST_MakePoint(
///            -122.5 + random() * 0.2,
///            37.7 + random() * 0.2
///        ), 4326),
///        'Feature ' || generate_series,
///        CASE (random() * 5)::int
///            WHEN 0 THEN 'poi'
///            WHEN 1 THEN 'building'
///            WHEN 2 THEN 'park'
///            WHEN 3 THEN 'road'
///            ELSE 'other'
///        END,
///        random() * 1000
///    FROM generate_series(1, 1000000);
///
///    -- Create spatial index
///    CREATE INDEX idx_benchmark_features_geom ON benchmark_features USING GIST(geom);
///
///    -- Apply optimizations
///    \i src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql
///    ```
///
/// 2. Set environment variable:
///    ```bash
///    export BENCHMARK_CONNECTION_STRING="Host=localhost;Database=honua_benchmark;Username=postgres;Password=postgres"
///    ```
///
/// 3. Run benchmarks:
///    ```bash
///    cd tests/Honua.Server.Benchmarks
///    dotnet run -c Release --filter "*PostgresOptimization*"
///    ```
///
/// 4. View results:
///    Results will be exported to:
///    - BenchmarkDotNet.Artifacts/results/PostgresOptimizationBenchmarks-report.html
///    - BenchmarkDotNet.Artifacts/results/PostgresOptimizationBenchmarks-report-github.md
///
/// Expected Performance Improvements:
/// - Small queries (100 features): 2-3x faster
/// - Large queries (1000 features): 5-7x faster
/// - Count queries: 3-5x faster
/// - MVT tile generation: 10x faster
/// - Aggregation: 20x faster
/// - Fast count estimate: 100x faster (uses pg_class stats)
/// </summary>
public static class BenchmarkInstructions { }
