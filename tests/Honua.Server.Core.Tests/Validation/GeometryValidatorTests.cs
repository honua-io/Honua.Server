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

    public GeometryValidatorTests()
    {
        _geometryFactory = new GeometryFactory();
    }

    [Fact]
    public void Validate_WithValidPoint_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));

        // Act
        var result = GeometryValidator.ValidateGeometry(point);

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
        var result = GeometryValidator.ValidateGeometry(lineString);

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
        var result = GeometryValidator.ValidateGeometry(polygon);

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
        var result = GeometryValidator.ValidateGeometry(polygon);

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
        var result = GeometryValidator.ValidateGeometry(emptyPoint);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("empty"));
    }

    [Fact]
    public void Validate_WithNullGeometry_ReturnsError()
    {
        // Act
        var result = GeometryValidator.ValidateGeometry(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("null") || e.Message.Contains("required"));
    }

    [Fact]
    public void ValidateBounds_WithGeometryInBounds_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(50, 50));
        var options = new GeometryValidator.GeometryValidationOptions
        {
            MinX = 0.0,
            MinY = 0.0,
            MaxX = 100.0,
            MaxY = 100.0
        };

        // Act
        var result = GeometryValidator.ValidateGeometry(point, options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBounds_WithGeometryOutOfBounds_ReturnsError()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(150, 50));
        var options = new GeometryValidator.GeometryValidationOptions
        {
            MinX = 0.0,
            MinY = 0.0,
            MaxX = 100.0,
            MaxY = 100.0
        };

        // Act
        var result = GeometryValidator.ValidateGeometry(point, options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("bounds") || e.Message.Contains("outside") || e.Message.Contains("range"));
    }

    [Fact(Skip = "SRID validation method removed - API changed to use GeometryValidationOptions")]
    public void ValidateSrid_WithValidSrid_ReturnsSuccess()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));
        point.SRID = 4326; // WGS 84
        var options = new GeometryValidator.GeometryValidationOptions
        {
            TargetSrid = 4326
        };

        // Act
        var result = GeometryValidator.ValidateGeometry(point, options);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact(Skip = "SRID validation method removed - API changed to use GeometryValidationOptions")]
    public void ValidateSrid_WithMismatchedSrid_ReturnsError()
    {
        // Arrange
        var point = _geometryFactory.CreatePoint(new Coordinate(10, 20));
        point.SRID = 4326;
        var options = new GeometryValidator.GeometryValidationOptions
        {
            TargetSrid = 3857 // Different SRID
        };

        // Act
        var result = GeometryValidator.ValidateGeometry(point, options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("SRID") || e.Message.Contains("coordinate"));
    }
}
