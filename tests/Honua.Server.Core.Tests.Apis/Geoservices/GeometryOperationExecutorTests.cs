using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Geoservices.GeometryService;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using NtsPrecisionModel = NetTopologySuite.Geometries.PrecisionModel;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Geoservices;

[Trait("Category", "Unit")]
public sealed class GeometryOperationExecutorTests
{
    private readonly GeometryOperationExecutor _executor;
    private readonly NtsGeometryFactory _geometryFactory;

    public GeometryOperationExecutorTests()
    {
        var configuration = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = "/tmp/test.json"
            },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 1000,
                    MaxCoordinateCount = 100000
                }
            }
        };

        var configService = new TestConfigurationService(configuration);
        _executor = new GeometryOperationExecutor(configService);
        _geometryFactory = new NtsGeometryFactory(new NtsPrecisionModel(), 4326);
    }

    [Fact]
    public void Buffer_ShouldCreateBufferedGeometries()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new NtsCoordinate(0, 0));
        var operation = new GeometryBufferOperation(
            GeometryType: "esriGeometryPoint",
            SpatialReference: 4326,
            Geometries: new[] { point },
            Distance: 10.0,
            Unit: "meter",
            UnionResults: false);

        // Act
        var results = _executor.Buffer(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeOfType<NtsPolygon>();
        results[0].SRID.Should().Be(4326);
        results[0].Area.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Buffer_ShouldUnionWhenRequested()
    {
        // Arrange
        var point1 = _geometryFactory.CreatePoint(new NtsCoordinate(0, 0));
        var point2 = _geometryFactory.CreatePoint(new NtsCoordinate(5, 5));
        var operation = new GeometryBufferOperation(
            GeometryType: "esriGeometryPoint",
            SpatialReference: 4326,
            Geometries: new[] { point1, point2 },
            Distance: 10.0,
            Unit: "meter",
            UnionResults: true);

        // Act
        var results = _executor.Buffer(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeOfType<NtsPolygon>();
    }

    [Fact]
    public void Buffer_ShouldHandleEmptyGeometries()
    {
        // Arrange
        var operation = new GeometryBufferOperation(
            GeometryType: "esriGeometryPoint",
            SpatialReference: 4326,
            Geometries: Array.Empty<NtsGeometry>(),
            Distance: 10.0,
            Unit: "meter",
            UnionResults: false);

        // Act
        var results = _executor.Buffer(operation);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_ShouldSimplifyGeometries()
    {
        // Arrange
        var coordinates = new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(1, 1),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(3, 1),
            new NtsCoordinate(4, 0),
            new NtsCoordinate(0, 0)
        };
        var polygon = _geometryFactory.CreatePolygon(coordinates);
        var operation = new GeometrySimplifyOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries: new[] { polygon });

        // Act
        var results = _executor.Simplify(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].SRID.Should().Be(4326);
        results[0].Should().NotBeNull();
    }

    [Fact]
    public void Simplify_ShouldHandleEmptyGeometries()
    {
        // Arrange
        var operation = new GeometrySimplifyOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries: Array.Empty<NtsGeometry>());

        // Act
        var results = _executor.Simplify(operation);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Union_ShouldCombineGeometries()
    {
        // Arrange
        var poly1 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(2, 2),
            new NtsCoordinate(0, 2),
            new NtsCoordinate(0, 0)
        });
        var poly2 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(1, 1),
            new NtsCoordinate(3, 1),
            new NtsCoordinate(3, 3),
            new NtsCoordinate(1, 3),
            new NtsCoordinate(1, 1)
        });
        var operation = new GeometrySetOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries: new[] { poly1, poly2 });

        // Act
        var result = _executor.Union(operation);

        // Assert
        result.Should().NotBeNull();
        result!.SRID.Should().Be(4326);
        result.Area.Should().BeGreaterThan(poly1.Area);
    }

    [Fact]
    public void Union_ShouldReturnNullForEmptyGeometries()
    {
        // Arrange
        var operation = new GeometrySetOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries: Array.Empty<NtsGeometry>());

        // Act
        var result = _executor.Union(operation);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Intersect_ShouldComputeIntersections()
    {
        // Arrange
        var poly1 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(2, 2),
            new NtsCoordinate(0, 2),
            new NtsCoordinate(0, 0)
        });
        var poly2 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(1, 1),
            new NtsCoordinate(3, 1),
            new NtsCoordinate(3, 3),
            new NtsCoordinate(1, 3),
            new NtsCoordinate(1, 1)
        });
        var operation = new GeometryPairwiseOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries1: new[] { poly1 },
            Geometries2: new[] { poly2 });

        // Act
        var results = _executor.Intersect(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].SRID.Should().Be(4326);
        results[0].Area.Should().BeGreaterThan(0);
        results[0].Area.Should().BeLessThan(poly1.Area);
    }

    [Fact]
    public void Intersect_ShouldHandleEmptyGeometries()
    {
        // Arrange
        var operation = new GeometryPairwiseOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries1: Array.Empty<NtsGeometry>(),
            Geometries2: Array.Empty<NtsGeometry>());

        // Act
        var results = _executor.Intersect(operation);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Difference_ShouldComputeDifferences()
    {
        // Arrange
        var poly1 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(2, 2),
            new NtsCoordinate(0, 2),
            new NtsCoordinate(0, 0)
        });
        var poly2 = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(1, 1),
            new NtsCoordinate(3, 1),
            new NtsCoordinate(3, 3),
            new NtsCoordinate(1, 3),
            new NtsCoordinate(1, 1)
        });
        var operation = new GeometryPairwiseOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Geometries1: new[] { poly1 },
            Geometries2: new[] { poly2 });

        // Act
        var results = _executor.Difference(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].SRID.Should().Be(4326);
        results[0].Area.Should().BeLessThan(poly1.Area);
    }

    [Fact]
    public void Distance_ShouldComputeDistances()
    {
        // Arrange
        var point1 = _geometryFactory.CreatePoint(new NtsCoordinate(0, 0));
        var point2 = _geometryFactory.CreatePoint(new NtsCoordinate(3, 4));
        var operation = new GeometryDistanceOperation(
            GeometryType: "esriGeometryPoint",
            SpatialReference: 4326,
            Geometries1: new[] { point1 },
            Geometries2: new[] { point2 },
            DistanceUnit: "meter",
            Geodesic: false);

        // Act
        var results = _executor.Distance(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    public void Distance_ShouldHandleEmptyGeometries()
    {
        // Arrange
        var operation = new GeometryDistanceOperation(
            GeometryType: "esriGeometryPoint",
            SpatialReference: 4326,
            Geometries1: Array.Empty<NtsGeometry>(),
            Geometries2: Array.Empty<NtsGeometry>(),
            DistanceUnit: "meter",
            Geodesic: false);

        // Act
        var results = _executor.Distance(operation);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Areas_ShouldComputeAreas()
    {
        // Arrange
        var polygon = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(2, 2),
            new NtsCoordinate(0, 2),
            new NtsCoordinate(0, 0)
        });
        var operation = new GeometryMeasurementOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Polygons: new[] { polygon },
            AreaUnit: "square-meters",
            LengthUnit: null);

        // Act
        var results = _executor.Areas(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void Areas_ShouldHandleEmptyPolygons()
    {
        // Arrange
        var operation = new GeometryMeasurementOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Polygons: Array.Empty<NtsPolygon>(),
            AreaUnit: "square-meters",
            LengthUnit: null);

        // Act
        var results = _executor.Areas(operation);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Lengths_ShouldComputeLengths()
    {
        // Arrange
        var polygon = _geometryFactory.CreatePolygon(new[]
        {
            new NtsCoordinate(0, 0),
            new NtsCoordinate(2, 0),
            new NtsCoordinate(2, 2),
            new NtsCoordinate(0, 2),
            new NtsCoordinate(0, 0)
        });
        var operation = new GeometryMeasurementOperation(
            GeometryType: "esriGeometryPolygon",
            SpatialReference: 4326,
            Polygons: new[] { polygon },
            AreaUnit: null,
            LengthUnit: "meters");

        // Act
        var results = _executor.Lengths(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeApproximately(8.0, 0.001);
    }

    [Fact]
    public void Project_ShouldTransformGeometries()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new NtsCoordinate(-122.5, 45.5));
        var operation = new GeometryProjectOperation(
            GeometryType: "esriGeometryPoint",
            InputSpatialReference: 4326,
            OutputSpatialReference: 3857,
            Geometries: new[] { point });

        // Act
        var results = _executor.Project(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].SRID.Should().Be(3857);
        results[0].Coordinate.X.Should().NotBe(point.Coordinate.X);
    }

    [Fact]
    public void Project_ShouldReturnClonedGeometriesWhenSameSrid()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new NtsCoordinate(-122.5, 45.5));
        var operation = new GeometryProjectOperation(
            GeometryType: "esriGeometryPoint",
            InputSpatialReference: 4326,
            OutputSpatialReference: 4326,
            Geometries: new[] { point });

        // Act
        var results = _executor.Project(operation);

        // Assert
        results.Should().HaveCount(1);
        results[0].SRID.Should().Be(4326);
        results[0].Coordinate.Should().Be(point.Coordinate);
    }

    [Fact]
    public void Project_ShouldHandleEmptyGeometries()
    {
        // Arrange
        var operation = new GeometryProjectOperation(
            GeometryType: "esriGeometryPoint",
            InputSpatialReference: 4326,
            OutputSpatialReference: 3857,
            Geometries: Array.Empty<NtsGeometry>());

        // Act
        var results = _executor.Project(operation);

        // Assert
        results.Should().BeEmpty();
    }

    private sealed class TestConfigurationService : IHonuaConfigurationService
    {
        private readonly HonuaConfiguration _configuration;

        public TestConfigurationService(HonuaConfiguration configuration)
        {
            _configuration = configuration;
        }

        public HonuaConfiguration Current => _configuration;

        public IChangeToken GetChangeToken()
        {
            return new CancellationChangeToken(CancellationToken.None);
        }

        public void Update(HonuaConfiguration configuration)
        {
            throw new NotSupportedException("Update not supported in test configuration service");
        }
    }
}
