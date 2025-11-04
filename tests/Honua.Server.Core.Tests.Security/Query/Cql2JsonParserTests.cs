using System;
using System.Text.Json;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

public sealed class Cql2JsonParserTests
{
    private readonly LayerDefinition _testLayer;

    public Cql2JsonParserTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields =
            [
                new FieldDefinition { Name = "id", DataType = "int", StorageType = "integer" },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "text" },
                new FieldDefinition { Name = "age", DataType = "int", StorageType = "integer" },
                new FieldDefinition { Name = "temperature", DataType = "double", StorageType = "double precision" },
                new FieldDefinition { Name = "status", DataType = "string", StorageType = "text" },
                new FieldDefinition { Name = "created", DataType = "datetimeoffset", StorageType = "timestamp" },
                new FieldDefinition { Name = "email", DataType = "string", StorageType = "text" }
            ]
        };
    }

    #region BETWEEN Operator Tests

    [Fact]
    public void Parse_BetweenNumeric_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""age""},
                18,
                65
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        // Should be expanded to: age >= 18 AND age <= 65
        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);

        var leftComparison = Assert.IsType<QueryBinaryExpression>(andExpr.Left);
        Assert.Equal(QueryBinaryOperator.GreaterThanOrEqual, leftComparison.Operator);
        var leftField = Assert.IsType<QueryFieldReference>(leftComparison.Left);
        Assert.Equal("age", leftField.Name);

        var rightComparison = Assert.IsType<QueryBinaryExpression>(andExpr.Right);
        Assert.Equal(QueryBinaryOperator.LessThanOrEqual, rightComparison.Operator);
        var rightField = Assert.IsType<QueryFieldReference>(rightComparison.Left);
        Assert.Equal("age", rightField.Name);
    }

    [Fact]
    public void Parse_BetweenDouble_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""temperature""},
                20.5,
                30.5
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    [Fact]
    public void Parse_BetweenDates_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""created""},
                ""2020-01-01T00:00:00Z"",
                ""2024-12-31T23:59:59Z""
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    [Fact]
    public void Parse_BetweenStrings_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""name""},
                ""A"",
                ""M""
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    [Fact]
    public void Parse_BetweenMissingArguments_ThrowsException()
    {
        // Arrange - missing upper bound
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""age""},
                18
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("three arguments", ex.Message);
    }

    [Fact]
    public void Parse_BetweenTooManyArguments_ThrowsException()
    {
        // Arrange - extra argument
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""age""},
                18,
                65,
                100
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("three arguments", ex.Message);
    }

    [Fact]
    public void Parse_BetweenNonFieldFirst_ThrowsException()
    {
        // Arrange - first argument is not a property
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                18,
                {""property"": ""age""},
                65
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("property reference", ex.Message);
    }

    #endregion

    #region IN Operator Tests

    [Fact]
    public void Parse_InStringList_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""status""},
                [""active"", ""pending"", ""approved""]
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        // Should be expanded to: status = 'active' OR status = 'pending' OR status = 'approved'
        var orExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.Or, orExpr.Operator);
    }

    [Fact]
    public void Parse_InNumericList_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""age""},
                [25, 30, 35, 40]
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var orExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.Or, orExpr.Operator);
    }

    [Fact]
    public void Parse_InSingleValue_Success()
    {
        // Arrange - single value should be optimized to simple equality
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""status""},
                [""active""]
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        // Single value should be optimized to equality
        var eqExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.Equal, eqExpr.Operator);

        var field = Assert.IsType<QueryFieldReference>(eqExpr.Left);
        Assert.Equal("status", field.Name);
    }

    [Fact]
    public void Parse_InEmptyArray_ThrowsException()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""status""},
                []
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_InMissingArguments_ThrowsException()
    {
        // Arrange - missing array
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""status""}
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("two arguments", ex.Message);
    }

    [Fact]
    public void Parse_InNotArray_ThrowsException()
    {
        // Arrange - second argument is not an array
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""status""},
                ""active""
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("array", ex.Message);
    }

    [Fact]
    public void Parse_InNonFieldFirst_ThrowsException()
    {
        // Arrange - first argument is not a property
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                ""status"",
                [""active"", ""pending""]
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("property reference", ex.Message);
    }

    [Fact]
    public void Parse_InLargeArray_Success()
    {
        // Arrange - 100 values to test performance considerations
        var values = string.Join(", ", Enumerable.Range(1, 100).Select(i => i.ToString()));
        var filterJson = $@"{{
            ""op"": ""in"",
            ""args"": [
                {{""property"": ""age""}},
                [{values}]
            ]
        }}";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);
    }

    #endregion

    #region IS NULL Operator Tests

    [Fact]
    public void Parse_IsNull_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": [
                {""property"": ""email""}
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        // IS NULL should be represented as field = NULL
        var eqExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.Equal, eqExpr.Operator);

        var field = Assert.IsType<QueryFieldReference>(eqExpr.Left);
        Assert.Equal("email", field.Name);

        var nullConstant = Assert.IsType<QueryConstant>(eqExpr.Right);
        Assert.Null(nullConstant.Value);
    }

    [Fact]
    public void Parse_IsNullMissingArgument_ThrowsException()
    {
        // Arrange - missing property argument
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": []
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("one argument", ex.Message);
    }

    [Fact]
    public void Parse_IsNullTooManyArguments_ThrowsException()
    {
        // Arrange - extra arguments
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": [
                {""property"": ""email""},
                {""property"": ""name""}
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("one argument", ex.Message);
    }

    [Fact]
    public void Parse_IsNullNonField_ThrowsException()
    {
        // Arrange - argument is not a property
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": [
                ""email""
            ]
        }";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Cql2JsonParser.Parse(filterJson, _testLayer, null));
        Assert.Contains("property reference", ex.Message);
    }

    #endregion

    #region Integration Tests - Complex Queries

    [Fact]
    public void Parse_BetweenWithAnd_Success()
    {
        // Arrange - combining BETWEEN with other operators
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""between"",
                    ""args"": [
                        {""property"": ""age""},
                        18,
                        65
                    ]
                },
                {
                    ""op"": ""="",
                    ""args"": [
                        {""property"": ""status""},
                        ""active""
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    [Fact]
    public void Parse_InWithOr_Success()
    {
        // Arrange - combining IN with other operators
        var filterJson = @"{
            ""op"": ""or"",
            ""args"": [
                {
                    ""op"": ""in"",
                    ""args"": [
                        {""property"": ""status""},
                        [""active"", ""pending""]
                    ]
                },
                {
                    ""op"": "">"",
                    ""args"": [
                        {""property"": ""age""},
                        60
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var orExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.Or, orExpr.Operator);
    }

    [Fact]
    public void Parse_IsNullWithNot_Success()
    {
        // Arrange - IS NOT NULL can be represented as NOT (IS NULL)
        var filterJson = @"{
            ""op"": ""not"",
            ""args"": [
                {
                    ""op"": ""isNull"",
                    ""args"": [
                        {""property"": ""email""}
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var notExpr = Assert.IsType<QueryUnaryExpression>(result.Expression);
        Assert.Equal(QueryUnaryOperator.Not, notExpr.Operator);
    }

    [Fact]
    public void Parse_ComplexQueryWithAllOperators_Success()
    {
        // Arrange - complex query using BETWEEN, IN, and IS NULL
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""between"",
                    ""args"": [
                        {""property"": ""age""},
                        25,
                        45
                    ]
                },
                {
                    ""op"": ""in"",
                    ""args"": [
                        {""property"": ""status""},
                        [""active"", ""pending"", ""approved""]
                    ]
                },
                {
                    ""op"": ""not"",
                    ""args"": [
                        {
                            ""op"": ""isNull"",
                            ""args"": [
                                {""property"": ""email""}
                            ]
                        }
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_BetweenEqualBounds_Success()
    {
        // Arrange - lower bound equals upper bound (effectively equality)
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""age""},
                30,
                30
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);
    }

    [Fact]
    public void Parse_InMixedTypes_Success()
    {
        // Arrange - mixed numeric types in array
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""age""},
                [25, 30.0, 35, 40.5]
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);
    }

    #endregion
}
