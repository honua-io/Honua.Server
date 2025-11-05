// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Query;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors.Query;

[Trait("Category", "Unit")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "AdvancedFilter")]
public class AdvancedFilterParsingTests
{
    #region Logical Operators

    [Fact]
    public void Parse_WithAndOperator_ReturnsLogicalExpression()
    {
        // Arrange
        const string filter = "temperature gt 20 and humidity lt 80";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("and");
        logical.Left.Should().BeOfType<ComparisonExpression>();
        logical.Right.Should().BeOfType<ComparisonExpression>();

        var left = (ComparisonExpression)logical.Left!;
        left.Property.Should().Be("temperature");
        left.Operator.Should().Be(ComparisonOperator.GreaterThan);
        left.Value.Should().Be(20.0);

        var right = (ComparisonExpression)logical.Right!;
        right.Property.Should().Be("humidity");
        right.Operator.Should().Be(ComparisonOperator.LessThan);
        right.Value.Should().Be(80.0);
    }

    [Fact]
    public void Parse_WithOrOperator_ReturnsLogicalExpression()
    {
        // Arrange
        const string filter = "status eq 'active' or status eq 'pending'";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("or");
    }

    [Fact]
    public void Parse_WithNotOperator_ReturnsLogicalExpression()
    {
        // Arrange
        const string filter = "not (status eq 'inactive')";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("not");
        logical.Left.Should().BeOfType<ComparisonExpression>();
        logical.Right.Should().BeNull();
    }

    [Fact]
    public void Parse_WithComplexLogicalExpression_ParsesCorrectly()
    {
        // Arrange
        const string filter = "(temperature gt 20 and humidity lt 80) or (temperature lt 0 and humidity gt 90)";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("or");
        logical.Left.Should().BeOfType<LogicalExpression>();
        logical.Right.Should().BeOfType<LogicalExpression>();
    }

    #endregion

    #region String Functions

    [Fact]
    public void Parse_WithContainsFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "contains(name, 'Weather')";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("contains");
        func.Arguments.Should().HaveCount(2);
        func.Arguments[0].Should().Be("name");
        func.Arguments[1].Should().Be("Weather");
    }

    [Fact]
    public void Parse_WithStartsWithFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "startswith(name, 'Temp')";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("startswith");
        func.Arguments[0].Should().Be("name");
        func.Arguments[1].Should().Be("Temp");
    }

    [Fact]
    public void Parse_WithEndsWithFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "endswith(name, 'Sensor')";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("endswith");
    }

    [Fact]
    public void Parse_WithToLowerFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "tolower(name) eq 'temperature'";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("comparison");
        func.Arguments.Should().HaveCount(3);

        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc.Should().NotBeNull();
        innerFunc!.Name.Should().Be("tolower");
    }

    [Fact]
    public void Parse_WithLengthFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "length(name) gt 10";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("comparison");

        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("length");
    }

    #endregion

    #region Math Functions

    [Fact]
    public void Parse_WithRoundFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "round(result) eq 21";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("round");
    }

    [Fact]
    public void Parse_WithFloorFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "floor(result) eq 20";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("floor");
    }

    [Fact]
    public void Parse_WithCeilingFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "ceiling(result) eq 22";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("ceiling");
    }

    #endregion

    #region Spatial Functions

    [Fact]
    public void Parse_WithGeoDistanceFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "geo.distance(location, geometry'POINT(-122.4194 37.7749)') lt 1000";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();

        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("geo.distance");
        innerFunc.Arguments.Should().HaveCount(2);
        innerFunc.Arguments[0].Should().Be("location");
        innerFunc.Arguments[1].ToString().Should().Contain("POINT");
    }

    [Fact]
    public void Parse_WithGeoIntersectsFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "geo.intersects(location, geometry'POLYGON((...))')";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        func.Name.Should().Be("geo.intersects");
    }

    #endregion

    #region Temporal Functions

    [Fact]
    public void Parse_WithYearFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "year(phenomenonTime) eq 2025";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("year");
    }

    [Fact]
    public void Parse_WithMonthFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "month(phenomenonTime) eq 11";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("month");
    }

    [Fact]
    public void Parse_WithHourFunction_ReturnsFunctionExpression()
    {
        // Arrange
        const string filter = "hour(phenomenonTime) ge 12";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        var func = (FunctionExpression)options.Filter!;
        var innerFunc = func.Arguments[0] as FunctionExpression;
        innerFunc!.Name.Should().Be("hour");
    }

    #endregion

    #region Complex Combinations

    [Fact]
    public void Parse_WithCombinedFunctionAndLogical_ParsesCorrectly()
    {
        // Arrange
        const string filter = "contains(name, 'Temp') and result gt 20";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("and");
        logical.Left.Should().BeOfType<FunctionExpression>();
        logical.Right.Should().BeOfType<ComparisonExpression>();
    }

    [Fact]
    public void Parse_WithNestedFunctions_ParsesCorrectly()
    {
        // Arrange
        const string filter = "tolower(name) eq 'temperature sensor'";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<FunctionExpression>();
    }

    [Fact]
    public void Parse_WithMultipleLogicalOperators_RespectsOperatorPrecedence()
    {
        // Arrange - AND has higher precedence than OR
        const string filter = "name eq 'A' or name eq 'B' and result gt 20";

        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter.Should().BeOfType<LogicalExpression>();

        var logical = (LogicalExpression)options.Filter!;
        logical.Operator.Should().Be("or");
        logical.Right.Should().BeOfType<LogicalExpression>(); // "name eq 'B' and result gt 20" is grouped
    }

    #endregion
}
