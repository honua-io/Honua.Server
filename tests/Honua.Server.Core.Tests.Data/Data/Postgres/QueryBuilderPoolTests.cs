using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Postgres;

[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
public sealed class QueryBuilderPoolTests : IDisposable
{
    private readonly QueryBuilderPool _pool;
    private readonly ServiceDefinition _service;
    private readonly LayerDefinition _layer;
    private const int StorageSrid = 4326;
    private const int TargetSrid = 3857;

    public QueryBuilderPoolTests()
    {
        _pool = new QueryBuilderPool(maxPoolsPerKey: 5, maxTotalPools: 20);

        _service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "WFS",
            DataSourceId = "test-datasource",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            }
        };

        _layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "geom", DataType = "geometry" }
            },
            Crs = new List<string> { "EPSG:4326" }
        };
    }

    [Fact]
    public void Get_ShouldReturnValidBuilder()
    {
        // Act
        var builder = _pool.Get(_service, _layer, StorageSrid, TargetSrid);

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<PostgresFeatureQueryBuilder>();
    }

    [Fact]
    public void Get_AndReturn_ShouldReuseBuilder()
    {
        // Arrange
        var builder1 = _pool.Get(_service, _layer, StorageSrid, TargetSrid);
        _pool.Return(builder1, _service, _layer, StorageSrid, TargetSrid);

        // Act
        var builder2 = _pool.Get(_service, _layer, StorageSrid, TargetSrid);

        // Assert
        builder2.Should().BeSameAs(builder1);
    }

    [Fact]
    public void Get_WithDifferentParameters_ShouldReturnDifferentBuilders()
    {
        // Arrange
        var layer2 = new LayerDefinition
        {
            Id = "test-layer-2",
            ServiceId = "test-service",
            Title = "Test Layer 2",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = _layer.Fields,
            Crs = _layer.Crs
        };

        // Act
        var builder1 = _pool.Get(_service, _layer, StorageSrid, TargetSrid);
        var builder2 = _pool.Get(_service, layer2, StorageSrid, TargetSrid);

        // Assert
        builder1.Should().NotBeSameAs(builder2);
    }

    [Fact]
    public void WarmCache_ShouldPreCreateBuilders()
    {
        // Act
        _pool.WarmCache(_service, _layer, StorageSrid, TargetSrid, count: 3);
        var stats = _pool.GetStatistics();

        // Assert
        stats.TotalPools.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var builder1 = _pool.Get(_service, _layer, StorageSrid, TargetSrid);
        var builder2 = _pool.Get(_service, _layer, 4326, 4326);

        // Act
        var stats = _pool.GetStatistics();

        // Assert
        stats.TotalPools.Should().BeGreaterThanOrEqualTo(2);
        stats.MaxTotalPools.Should().Be(20);
        stats.MaxPoolsPerKey.Should().Be(5);
        stats.PoolKeys.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllBuilders()
    {
        // Arrange
        _pool.Get(_service, _layer, StorageSrid, TargetSrid);
        _pool.Get(_service, _layer, 4326, 4326);

        // Act
        _pool.Clear();
        var stats = _pool.GetStatistics();

        // Assert
        stats.TotalPools.Should().Be(0);
        stats.PoolKeys.Should().BeEmpty();
    }

    [Fact]
    public void Pool_WithMaxTotalPoolsExceeded_ShouldEvictLRU()
    {
        // Arrange
        var smallPool = new QueryBuilderPool(maxPoolsPerKey: 5, maxTotalPools: 3);

        // Act - Create 4 different pool entries (exceeds max of 3)
        var builder1 = smallPool.Get(_service, _layer, 4326, 4326);
        var builder2 = smallPool.Get(_service, _layer, 4326, 3857);
        var builder3 = smallPool.Get(_service, _layer, 3857, 4326);
        var builder4 = smallPool.Get(_service, _layer, 3857, 3857); // Should trigger eviction

        var stats = smallPool.GetStatistics();

        // Assert
        stats.TotalPools.Should().BeLessThanOrEqualTo(3);

        smallPool.Dispose();
    }

    [Fact]
    public void Return_WithNullBuilder_ShouldNotThrow()
    {
        // Act
        var act = () => _pool.Return(null!, _service, _layer, StorageSrid, TargetSrid);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Pool_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    var builder = _pool.Get(_service, _layer, StorageSrid, TargetSrid);
                    // Simulate work
                    System.Threading.Thread.SpinWait(10);
                    _pool.Return(builder, _service, _layer, StorageSrid, TargetSrid);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = _pool.GetStatistics();
        stats.TotalPools.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Get_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var pool = new QueryBuilderPool();
        pool.Dispose();

        // Act
        var act = () => pool.Get(_service, _layer, StorageSrid, TargetSrid);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void WarmCache_WithInvalidCount_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => _pool.WarmCache(_service, _layer, StorageSrid, TargetSrid, count: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithInvalidMaxPoolsPerKey_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => new QueryBuilderPool(maxPoolsPerKey: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithInvalidMaxTotalPools_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var act = () => new QueryBuilderPool(maxPoolsPerKey: 10, maxTotalPools: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void Dispose()
    {
        _pool?.Dispose();
    }
}
