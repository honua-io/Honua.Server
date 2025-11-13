// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Spatial;

public class SpatialPredicateTests
{
    private readonly GeometryFactory _geometryFactory;

    public SpatialPredicateTests()
    {
        _geometryFactory = new GeometryFactory();
    }

    [Fact]
    public void CreateSpatialExpression_WithIntersects_CreatesCorrectly()
    {
        // Arrange
        var field = new QueryFieldReference("geometry");
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));

        // Act
        var spatialExpr = new QuerySpatialExpression(
            SpatialPredicate.Intersects,
            field,
            new QueryConstant(point));

        // Assert
        spatialExpr.Predicate.Should().Be(SpatialPredicate.Intersects);
        spatialExpr.GeometryProperty.Should().Be(field);
    }

    [Theory]
    [InlineData(SpatialPredicate.Intersects)]
    [InlineData(SpatialPredicate.Contains)]
    [InlineData(SpatialPredicate.Within)]
    [InlineData(SpatialPredicate.Overlaps)]
    [InlineData(SpatialPredicate.Touches)]
    [InlineData(SpatialPredicate.Crosses)]
    [InlineData(SpatialPredicate.Disjoint)]
    public void CreateSpatialExpression_WithDifferentPredicates_WorksCorrectly(SpatialPredicate predicate)
    {
        // Arrange
        var field = new QueryFieldReference("geom");
        var polygon = _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        });

        // Act
        var spatialExpr = new QuerySpatialExpression(
            predicate,
            field,
            new QueryConstant(polygon));

        // Assert
        spatialExpr.Predicate.Should().Be(predicate);
        spatialExpr.GeometryProperty.Should().NotBeNull();
    }

    [Fact]
    public void CreateBboxExpression_WithBoundingBox_CreatesCorrectly()
    {
        // Arrange
        var field = new QueryFieldReference("geometry");
        var minX = -180.0;
        var minY = -90.0;
        var maxX = 180.0;
        var maxY = 90.0;

        var envelope = new Envelope(minX, maxX, minY, maxY);
        var bbox = _geometryFactory.ToGeometry(envelope);

        // Act
        var spatialExpr = new QuerySpatialExpression(
            SpatialPredicate.Intersects,
            field,
            new QueryConstant(bbox));

        // Assert
        spatialExpr.Should().NotBeNull();
        spatialExpr.Predicate.Should().Be(SpatialPredicate.Intersects);
    }

    [Fact]
    public void CreateDistanceExpression_WithDWithin_IncludesDistance()
    {
        // Arrange
        var field = new QueryFieldReference("location");
        var point = _geometryFactory.CreatePoint(new Coordinate(0, 0));
        var distance = 1000.0; // meters

        // Act
        var spatialExpr = new QuerySpatialExpression(
            SpatialPredicate.DWithin,
            field,
            new QueryConstant(point),
            distance);

        // Assert
        spatialExpr.Predicate.Should().Be(SpatialPredicate.DWithin);
        spatialExpr.Distance.Should().Be(distance);
    }

    [Fact]
    public void CreateBufferExpression_WithBuffer_CreatesBufferedGeometry()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));
        var bufferDistance = 5.0;

        // Act
        var buffered = point.Buffer(bufferDistance);

        // Assert
        buffered.Should().NotBeNull();
        buffered.Area.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SpatialRelationship_Intersects_WithOverlappingGeometries_ReturnsTrue()
    {
        // Arrange
        var geom1 = _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        });

        var geom2 = _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(5, 5),
            new Coordinate(15, 5),
            new Coordinate(15, 15),
            new Coordinate(5, 15),
            new Coordinate(5, 5)
        });

        // Act
        var intersects = geom1.Intersects(geom2);

        // Assert
        intersects.Should().BeTrue();
    }

    [Fact]
    public void SpatialRelationship_Contains_WithPointInPolygon_ReturnsTrue()
    {
        // Arrange
        var polygon = _geometryFactory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        });

        var point = _geometryFactory.CreatePoint(new Coordinate(5, 5));

        // Act
        var contains = polygon.Contains(point);

        // Assert
        contains.Should().BeTrue();
    }
}
