// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Query;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors.Query;

[Trait("Category", "Unit")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "AdvancedFilterSQL")]
public class AdvancedFilterSqlBuilderTests
{
    #region Comparison Expressions

    [Fact]
    public void BuildWhereClause_WithSimpleComparison_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new ComparisonExpression
        {
            Property = "temperature",
            Operator = ComparisonOperator.GreaterThan,
            Value = 20.5
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("temperature > @p0");
        parameters.Should().ContainKey("p0");
        parameters["p0"].Should().Be(20.5);
    }

    [Fact]
    public void BuildWhereClause_WithEqualsOperator_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new ComparisonExpression
        {
            Property = "name",
            Operator = ComparisonOperator.Equals,
            Value = "Weather Station"
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("name = @p0");
        parameters["p0"].Should().Be("Weather Station");
    }

    #endregion

    #region Logical Expressions

    [Fact]
    public void BuildWhereClause_WithAndOperator_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new LogicalExpression
        {
            Operator = "and",
            Left = new ComparisonExpression
            {
                Property = "temperature",
                Operator = ComparisonOperator.GreaterThan,
                Value = 20
            },
            Right = new ComparisonExpression
            {
                Property = "humidity",
                Operator = ComparisonOperator.LessThan,
                Value = 80
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("(temperature > @p0 AND humidity < @p1)");
        parameters.Should().ContainKey("p0");
        parameters.Should().ContainKey("p1");
        parameters["p0"].Should().Be(20);
        parameters["p1"].Should().Be(80);
    }

    [Fact]
    public void BuildWhereClause_WithOrOperator_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new LogicalExpression
        {
            Operator = "or",
            Left = new ComparisonExpression
            {
                Property = "status",
                Operator = ComparisonOperator.Equals,
                Value = "active"
            },
            Right = new ComparisonExpression
            {
                Property = "status",
                Operator = ComparisonOperator.Equals,
                Value = "pending"
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("(status = @p0 OR status = @p1)");
    }

    [Fact]
    public void BuildWhereClause_WithNotOperator_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new LogicalExpression
        {
            Operator = "not",
            Left = new ComparisonExpression
            {
                Property = "status",
                Operator = ComparisonOperator.Equals,
                Value = "inactive"
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("(NOT status = @p0)");
        parameters["p0"].Should().Be("inactive");
    }

    #endregion

    #region String Functions

    [Fact]
    public void BuildWhereClause_WithContainsFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "contains",
            Arguments = new object[] { "name", "Weather" }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("name LIKE @p0");
        parameters["p0"].Should().Be("%Weather%");
    }

    [Fact]
    public void BuildWhereClause_WithStartsWithFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "startswith",
            Arguments = new object[] { "name", "Temp" }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("name LIKE @p0");
        parameters["p0"].Should().Be("Temp%");
    }

    [Fact]
    public void BuildWhereClause_WithEndsWithFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "endswith",
            Arguments = new object[] { "name", "Sensor" }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("name LIKE @p0");
        parameters["p0"].Should().Be("%Sensor");
    }

    [Fact]
    public void BuildWhereClause_WithLengthFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "length", Arguments = new object[] { "name" } },
                "GreaterThan",
                10
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("LENGTH(name) > @p0");
        parameters["p0"].Should().Be(10);
    }

    [Fact]
    public void BuildWhereClause_WithToLowerFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "tolower", Arguments = new object[] { "name" } },
                "Equals",
                "temperature"
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("LOWER(name) = @p0");
        parameters["p0"].Should().Be("temperature");
    }

    [Fact]
    public void BuildWhereClause_WithToUpperFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "toupper", Arguments = new object[] { "name" } },
                "Equals",
                "TEMPERATURE"
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("UPPER(name) = @p0");
        parameters["p0"].Should().Be("TEMPERATURE");
    }

    #endregion

    #region Math Functions

    [Fact]
    public void BuildWhereClause_WithRoundFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "round", Arguments = new object[] { "result" } },
                "Equals",
                21
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("ROUND(result) = @p0");
        parameters["p0"].Should().Be(21);
    }

    [Fact]
    public void BuildWhereClause_WithFloorFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "floor", Arguments = new object[] { "result" } },
                "Equals",
                20
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("FLOOR(result) = @p0");
    }

    [Fact]
    public void BuildWhereClause_WithCeilingFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "ceiling", Arguments = new object[] { "result" } },
                "Equals",
                22
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("CEILING(result) = @p0");
    }

    #endregion

    #region Spatial Functions

    [Fact]
    public void BuildWhereClause_WithGeoDistanceFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression
                {
                    Name = "geo.distance",
                    Arguments = new object[] { "location", "geometry'POINT(-122.4194 37.7749)'" }
                },
                "LessThan",
                1000
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Contain("ST_Distance(location");
        sql.Should().Contain("ST_GeomFromText");
        sql.Should().Contain("< @");
        parameters.Should().ContainKey("p0");
        parameters.Should().ContainKey("p1");
    }

    [Fact]
    public void BuildWhereClause_WithGeoIntersectsFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "geo.intersects",
            Arguments = new object[] { "location", "geometry'POLYGON((0 0, 1 0, 1 1, 0 1, 0 0))'" }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Contain("ST_Intersects(location");
        sql.Should().Contain("ST_GeomFromText");
        parameters.Should().ContainKey("p0");
    }

    #endregion

    #region Temporal Functions

    [Fact]
    public void BuildWhereClause_WithYearFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "year", Arguments = new object[] { "phenomenonTime" } },
                "Equals",
                2025
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("EXTRACT(YEAR FROM phenomenon_time) = @p0");
        parameters["p0"].Should().Be(2025);
    }

    [Fact]
    public void BuildWhereClause_WithMonthFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "month", Arguments = new object[] { "phenomenonTime" } },
                "Equals",
                11
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("EXTRACT(MONTH FROM phenomenon_time) = @p0");
        parameters["p0"].Should().Be(11);
    }

    [Fact]
    public void BuildWhereClause_WithHourFunction_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new FunctionExpression
        {
            Name = "comparison",
            Arguments = new object[]
            {
                new FunctionExpression { Name = "hour", Arguments = new object[] { "phenomenonTime" } },
                "GreaterThanOrEqual",
                12
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("EXTRACT(HOUR FROM phenomenon_time) >= @p0");
        parameters["p0"].Should().Be(12);
    }

    #endregion

    #region Complex Combinations

    [Fact]
    public void BuildWhereClause_WithCombinedFunctionAndLogical_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new LogicalExpression
        {
            Operator = "and",
            Left = new FunctionExpression
            {
                Name = "contains",
                Arguments = new object[] { "name", "Temp" }
            },
            Right = new ComparisonExpression
            {
                Property = "result",
                Operator = ComparisonOperator.GreaterThan,
                Value = 20
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("(name LIKE @p0 AND result > @p1)");
        parameters["p0"].Should().Be("%Temp%");
        parameters["p1"].Should().Be(20);
    }

    [Fact]
    public void BuildWhereClause_WithNestedLogicalExpressions_ReturnsCorrectSql()
    {
        // Arrange
        var filter = new LogicalExpression
        {
            Operator = "or",
            Left = new LogicalExpression
            {
                Operator = "and",
                Left = new ComparisonExpression
                {
                    Property = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 20
                },
                Right = new ComparisonExpression
                {
                    Property = "humidity",
                    Operator = ComparisonOperator.LessThan,
                    Value = 80
                }
            },
            Right = new LogicalExpression
            {
                Operator = "and",
                Left = new ComparisonExpression
                {
                    Property = "temperature",
                    Operator = ComparisonOperator.LessThan,
                    Value = 0
                },
                Right = new ComparisonExpression
                {
                    Property = "humidity",
                    Operator = ComparisonOperator.GreaterThan,
                    Value = 90
                }
            }
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("((temperature > @p0 AND humidity < @p1) OR (temperature < @p2 AND humidity > @p3))");
        parameters.Should().HaveCount(4);
    }

    [Fact]
    public void BuildWhereClause_WithNullFilter_ReturnsEmptySql()
    {
        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(null);

        // Assert
        sql.Should().BeEmpty();
        parameters.Should().BeEmpty();
    }

    #endregion

    #region Property Mapping

    [Fact]
    public void BuildWhereClause_MapsODataPropertiesToDatabaseColumns()
    {
        // Arrange
        var filter = new ComparisonExpression
        {
            Property = "phenomenonTime",
            Operator = ComparisonOperator.GreaterThan,
            Value = "2025-01-01"
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("phenomenon_time > @p0");
    }

    [Fact]
    public void BuildWhereClause_HandlesCamelCaseToSnakeCase()
    {
        // Arrange
        var filter = new ComparisonExpression
        {
            Property = "resultTime",
            Operator = ComparisonOperator.LessThan,
            Value = "2025-12-31"
        };

        // Act
        var (sql, parameters) = AdvancedFilterSqlBuilder.BuildWhereClause(filter);

        // Assert
        sql.Should().Be("result_time < @p0");
    }

    #endregion
}
