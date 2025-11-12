// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Query;

public class CqlFilterParserTests
{
    private readonly CqlFilterParser _parser;

    public CqlFilterParserTests()
    {
        _parser = new CqlFilterParser();
    }

    [Fact]
    public void Parse_WithSimpleEquality_ReturnsFilter()
    {
        // Arrange
        var cql = "name = 'test'";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithNumericComparison_ReturnsFilter()
    {
        // Arrange
        var cql = "population > 1000000";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithLogicalAnd_ReturnsComplexFilter()
    {
        // Arrange
        var cql = "name = 'test' AND value > 100";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithLogicalOr_ReturnsComplexFilter()
    {
        // Arrange
        var cql = "status = 'active' OR status = 'pending'";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithSpatialIntersects_ReturnsSpatialFilter()
    {
        // Arrange
        var cql = "INTERSECTS(geometry, POINT(10 20))";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.IsSpatial.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithBbox_ReturnsBboxFilter()
    {
        // Arrange
        var cql = "BBOX(geometry, -180, -90, 180, 90)";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.IsSpatial.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInOperator_ReturnsInFilter()
    {
        // Arrange
        var cql = "status IN ('active', 'pending', 'completed')";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithLikeOperator_ReturnsLikeFilter()
    {
        // Arrange
        var cql = "name LIKE 'test%'";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithBetweenOperator_ReturnsBetweenFilter()
    {
        // Arrange
        var cql = "age BETWEEN 18 AND 65";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithInvalidSyntax_ReturnsError()
    {
        // Arrange
        var cql = "invalid syntax here &&";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_WithNestedConditions_ReturnsComplexFilter()
    {
        // Arrange
        var cql = "(name = 'test' AND value > 100) OR (status = 'active')";

        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("name IS NULL")]
    [InlineData("value IS NOT NULL")]
    public void Parse_WithNullChecks_ReturnsFilter(string cql)
    {
        // Act
        var result = _parser.Parse(cql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }
}
