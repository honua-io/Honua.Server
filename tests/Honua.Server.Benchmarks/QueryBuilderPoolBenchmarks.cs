using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class QueryBuilderPoolBenchmarks
{
    private QueryBuilderPool _pool = null!;
    private ServiceDefinition _service = null!;
    private LayerDefinition _layer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new QueryBuilderPool(maxPoolsPerKey: 10, maxTotalPools: 100);

        _service = new ServiceDefinition
        {
            Id = "benchmark-service",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            }
        };

        _layer = new LayerDefinition
        {
            Id = "benchmark-layer",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "name", DataType = "string" },
                new() { Name = "geom", DataType = "geometry" }
            },
            Crs = new List<string> { "EPSG:4326" }
        };

        // Warm up the pool
        _pool.WarmCache(_service, _layer, 4326, 3857, count: 5);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pool?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void DirectConstruction()
    {
        var builder = new PostgresFeatureQueryBuilder(_service, _layer, 4326, 3857);
        // Simulate usage
        _ = builder;
    }

    [Benchmark]
    public void PooledGetAndReturn()
    {
        var builder = _pool.Get(_service, _layer, 4326, 3857);
        _pool.Return(builder, _service, _layer, 4326, 3857);
    }

    [Benchmark]
    public void PooledGetReturnWithWork()
    {
        var builder = _pool.Get(_service, _layer, 4326, 3857);

        // Simulate actual usage
        var query = new FeatureQuery { Limit = 100 };
        var definition = builder.BuildSelect(query);
        _ = definition;

        _pool.Return(builder, _service, _layer, 4326, 3857);
    }

    [Benchmark]
    public void DirectConstructionWithWork()
    {
        var builder = new PostgresFeatureQueryBuilder(_service, _layer, 4326, 3857);

        // Simulate actual usage
        var query = new FeatureQuery { Limit = 100 };
        var definition = builder.BuildSelect(query);
        _ = definition;
    }

    [Benchmark]
    [Arguments(1)]
    [Arguments(5)]
    [Arguments(10)]
    public void MultiplePooledOperations(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var builder = _pool.Get(_service, _layer, 4326, 3857);
            var query = new FeatureQuery { Limit = 100 };
            var definition = builder.BuildSelect(query);
            _ = definition;
            _pool.Return(builder, _service, _layer, 4326, 3857);
        }
    }

    [Benchmark]
    [Arguments(1)]
    [Arguments(5)]
    [Arguments(10)]
    public void MultipleDirectConstructions(int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            var builder = new PostgresFeatureQueryBuilder(_service, _layer, 4326, 3857);
            var query = new FeatureQuery { Limit = 100 };
            var definition = builder.BuildSelect(query);
            _ = definition;
        }
    }

    [Benchmark]
    public void PooledMultipleLayersSequential()
    {
        var layer1 = _layer;
        var layer2 = new LayerDefinition
        {
            Id = "layer2",
            IdField = "id",
            GeometryField = "geom",
            Fields = _layer.Fields,
            Crs = _layer.Crs
        };

        var builder1 = _pool.Get(_service, layer1, 4326, 3857);
        var query = new FeatureQuery { Limit = 100 };
        _ = builder1.BuildSelect(query);
        _pool.Return(builder1, _service, layer1, 4326, 3857);

        var builder2 = _pool.Get(_service, layer2, 4326, 3857);
        _ = builder2.BuildSelect(query);
        _pool.Return(builder2, _service, layer2, 4326, 3857);
    }

    [Benchmark]
    public void WarmCacheOperation()
    {
        var newLayer = new LayerDefinition
        {
            Id = $"layer-{Guid.NewGuid()}",
            IdField = "id",
            GeometryField = "geom",
            Fields = _layer.Fields,
            Crs = _layer.Crs
        };

        _pool.WarmCache(_service, newLayer, 4326, 3857, count: 3);
    }
}
