// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Validation;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Validation;

public class GeometryValidatorTests
{
    private readonly GeometryFactory _geometryFactory;
    private readonly GeometryValidator _validator;

    public GeometryValidatorTests()
    {
        _geometryFactory = new GeometryFactory();
        _validator = new GeometryValidator();
    }

    [Fact]
    public void Validate_WithValidPoint_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = _validator.Validate(point);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithValidLineString_ReturnsSuccess()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10),
            new Coordinate(20, 0)
        };
        var lineString = _geometryFactory.CreateLineString(coords);

        // Act
        var result = _validator.Validate(lineString);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidPolygon_ReturnsSuccess()
    {
        // Arrange
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0) // Closing coordinate
        };
        var polygon = _geometryFactory.CreatePolygon(coords);

        // Act
        var result = _validator.Validate(polygon);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithSelfIntersectingPolygon_ReturnsError()
    {
        // Arrange - Create a bow-tie polygon (self-intersecting)
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        };
        var polygon = _geometryFactory.CreatePolygon(coords);

        // Act
        var result = _validator.Validate(polygon);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WithEmptyGeometry_ReturnsError()
    {
        // Arrange
        var emptyPoint = _geometryFactory.CreatePoint((Coordinate?)null);

        // Act
        var result = _validator.Validate(emptyPoint);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("empty"));
    }

    [Fact]
    public void Validate_WithNullGeometry_ReturnsError()
    {
        // Act
        var result = _validator.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("null") || e.Contains("required"));
    }

    [Fact]
    public void ValidateBounds_WithGeometryInBounds_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(50, 50));
        var minX = 0.0;
        var minY = 0.0;
        var maxX = 100.0;
        var maxY = 100.0;

        // Act
        var result = _validator.ValidateBounds(point, minX, minY, maxX, maxY);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBounds_WithGeometryOutOfBounds_ReturnsError()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(150, 50));
        var minX = 0.0;
        var minY = 0.0;
        var maxX = 100.0;
        var maxY = 100.0;

        // Act
        var result = _validator.ValidateBounds(point, minX, minY, maxX, maxY);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("bounds") || e.Contains("outside"));
    }

    [Fact]
    public void ValidateSrid_WithValidSrid_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));
        point.SRID = 4326; // WGS 84

        // Act
        var result = _validator.ValidateSrid(point, 4326);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSrid_WithMismatchedSrid_ReturnsError()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));
        point.SRID = 4326;

        // Act
        var result = _validator.ValidateSrid(point, 3857); // Different SRID

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("SRID") || e.Contains("coordinate"));
    }
}
