// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Query;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors.Query;

[Trait("Category", "Unit")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "QueryParser")]
public class QueryOptionsParserTests
{
    [Theory]
    [InlineData("name eq 'Weather Station'", "name", ComparisonOperator.Equals, "Weather Station")]
    [InlineData("temperature gt 20", "temperature", ComparisonOperator.GreaterThan, 20d)]
    [InlineData("temperature ge 20.5", "temperature", ComparisonOperator.GreaterThanOrEqual, 20.5d)]
    [InlineData("temperature lt 30", "temperature", ComparisonOperator.LessThan, 30d)]
    [InlineData("temperature le 30.0", "temperature", ComparisonOperator.LessThanOrEqual, 30d)]
    [InlineData("status ne 'inactive'", "status", ComparisonOperator.NotEquals, "inactive")]
    public void Parse_WithValidFilter_ReturnsCorrectFilterExpression(
        string filter,
        string expectedProperty,
        ComparisonOperator expectedOperator,
        object expectedValue)
    {
        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().BeOfType<ComparisonExpression>();
        var comparison = (ComparisonExpression)options.Filter!;
        comparison.Property.Should().Be(expectedProperty);
        comparison.Operator.Should().Be(expectedOperator);
        comparison.Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithNullOrEmptyFilter_ReturnsNullFilter(string? filter)
    {
        // Act
        var options = QueryOptionsParser.Parse(filter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().BeNull();
    }

    [Theory]
    [InlineData("Locations", 1)]
    [InlineData("Locations,Datastreams", 2)]
    [InlineData("Locations, Datastreams, HistoricalLocations", 3)]
    public void Parse_WithExpand_ReturnsCorrectExpandOptions(string expand, int expectedCount)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, expand, null, null, null, null, false);

        // Assert
        options.Expand.Should().NotBeNull();
        options.Expand!.Properties.Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithNullOrEmptyExpand_ReturnsNullExpand(string? expand)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, expand, null, null, null, null, false);

        // Assert
        options.Expand.Should().BeNull();
    }

    [Theory]
    [InlineData("name", 1)]
    [InlineData("name,description", 2)]
    [InlineData("name, description, properties", 3)]
    public void Parse_WithSelect_ReturnsCorrectSelectList(string select, int expectedCount)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, null, select, null, null, null, false);

        // Assert
        options.Select.Should().NotBeNull();
        options.Select.Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData("name", SortDirection.Ascending)]
    [InlineData("name asc", SortDirection.Ascending)]
    [InlineData("name desc", SortDirection.Descending)]
    [InlineData("phenomenonTime desc", SortDirection.Descending)]
    public void Parse_WithOrderBy_ReturnsCorrectOrderByOptions(string orderby, SortDirection expectedDirection)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, null, null, orderby, null, null, false);

        // Assert
        options.OrderBy.Should().NotBeNull();
        options.OrderBy.Should().HaveCount(1);
        options.OrderBy![0].Direction.Should().Be(expectedDirection);
    }

    [Fact]
    public void Parse_WithMultipleOrderBy_ReturnsCorrectOrderByList()
    {
        // Arrange
        const string orderby = "name asc, phenomenonTime desc";

        // Act
        var options = QueryOptionsParser.Parse(null, null, null, orderby, null, null, false);

        // Assert
        options.OrderBy.Should().NotBeNull();
        options.OrderBy.Should().HaveCount(2);
        options.OrderBy![0].Property.Should().Be("name");
        options.OrderBy[0].Direction.Should().Be(SortDirection.Ascending);
        options.OrderBy[1].Property.Should().Be("phenomenonTime");
        options.OrderBy[1].Direction.Should().Be(SortDirection.Descending);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Parse_WithTop_ReturnsCorrectTopValue(int top)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, null, null, null, top, null, false);

        // Assert
        options.Top.Should().Be(top);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    public void Parse_WithSkip_ReturnsCorrectSkipValue(int skip)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, null, null, null, null, skip, false);

        // Assert
        options.Skip.Should().Be(skip);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parse_WithCount_ReturnsCorrectCountValue(bool count)
    {
        // Act
        var options = QueryOptionsParser.Parse(null, null, null, null, null, null, count);

        // Assert
        options.Count.Should().Be(count);
    }

    [Fact]
    public void Parse_WithAllParameters_ReturnsCompleteQueryOptions()
    {
        // Arrange
        const string filter = "temperature gt 20";
        const string expand = "Datastream,FeatureOfInterest";
        const string select = "id,result,phenomenonTime";
        const string orderby = "phenomenonTime desc";
        const int top = 50;
        const int skip = 100;
        const bool count = true;

        // Act
        var options = QueryOptionsParser.Parse(filter, expand, select, orderby, top, skip, count);

        // Assert
        options.Should().NotBeNull();
        options.Filter.Should().NotBeNull();
        options.Expand.Should().NotBeNull();
        options.Select.Should().NotBeNull();
        options.OrderBy.Should().NotBeNull();
        options.Top.Should().Be(top);
        options.Skip.Should().Be(skip);
        options.Count.Should().Be(count);
    }

    [Theory]
    [InlineData("name")] // Missing operator and value
    [InlineData("name eq")] // Missing value
    public void Parse_WithInvalidFilter_ReturnsNullFilter(string invalidFilter)
    {
        // Act
        var options = QueryOptionsParser.Parse(invalidFilter, null, null, null, null, null, false);

        // Assert
        options.Filter.Should().BeNull();
    }
}
