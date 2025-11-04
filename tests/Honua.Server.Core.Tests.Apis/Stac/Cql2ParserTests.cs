using System;
using System.Collections.Generic;
using Honua.Server.Core.Stac.Cql2;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

public sealed class Cql2ParserTests
{
    [Fact]
    public void Parse_SimpleComparisonExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""cloud_cover""},
                10
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var comparison = Assert.IsType<Cql2ComparisonExpression>(result);
        Assert.Equal("=", comparison.Operator);
        Assert.Equal(2, comparison.Arguments.Count);

        var propertyRef = Assert.IsType<Cql2PropertyRef>(comparison.Arguments[0]);
        Assert.Equal("cloud_cover", propertyRef.Property);

        var literal = Assert.IsType<Cql2Literal>(comparison.Arguments[1]);
        Assert.Equal(10L, literal.Value);
    }

    [Fact]
    public void Parse_LogicalAndExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""<"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        10
                    ]
                },
                {
                    ""op"": ""="",
                    ""args"": [
                        {""property"": ""sensor""},
                        ""Landsat""
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var logical = Assert.IsType<Cql2LogicalExpression>(result);
        Assert.Equal("and", logical.Operator);
        Assert.Equal(2, logical.Arguments.Count);

        var firstComparison = Assert.IsType<Cql2ComparisonExpression>(logical.Arguments[0]);
        Assert.Equal("<", firstComparison.Operator);

        var secondComparison = Assert.IsType<Cql2ComparisonExpression>(logical.Arguments[1]);
        Assert.Equal("=", secondComparison.Operator);
    }

    [Fact]
    public void Parse_NotExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""not"",
            ""args"": [
                {
                    ""op"": "">"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        50
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var notExpr = Assert.IsType<Cql2NotExpression>(result);
        Assert.Single(notExpr.Arguments);

        var innerComparison = Assert.IsType<Cql2ComparisonExpression>(notExpr.Arguments[0]);
        Assert.Equal(">", innerComparison.Operator);
    }

    [Fact]
    public void Parse_IsNullExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""isNull"",
            ""args"": [
                {""property"": ""end_datetime""}
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var isNull = Assert.IsType<Cql2IsNullExpression>(result);
        Assert.Single(isNull.Arguments);

        var propertyRef = Assert.IsType<Cql2PropertyRef>(isNull.Arguments[0]);
        Assert.Equal("end_datetime", propertyRef.Property);
    }

    [Fact]
    public void Parse_LikeExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""like"",
            ""args"": [
                {""property"": ""title""},
                ""%imagery%""
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var like = Assert.IsType<Cql2LikeExpression>(result);
        Assert.Equal(2, like.Arguments.Count);

        var propertyRef = Assert.IsType<Cql2PropertyRef>(like.Arguments[0]);
        Assert.Equal("title", propertyRef.Property);

        var literal = Assert.IsType<Cql2Literal>(like.Arguments[1]);
        Assert.Equal("%imagery%", literal.Value);
    }

    [Fact]
    public void Parse_BetweenExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""between"",
            ""args"": [
                {""property"": ""cloud_cover""},
                10,
                50
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var between = Assert.IsType<Cql2BetweenExpression>(result);
        Assert.Equal(3, between.Arguments.Count);

        var propertyRef = Assert.IsType<Cql2PropertyRef>(between.Arguments[0]);
        Assert.Equal("cloud_cover", propertyRef.Property);

        var lower = Assert.IsType<Cql2Literal>(between.Arguments[1]);
        Assert.Equal(10L, lower.Value);

        var upper = Assert.IsType<Cql2Literal>(between.Arguments[2]);
        Assert.Equal(50L, upper.Value);
    }

    [Fact]
    public void Parse_InExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""in"",
            ""args"": [
                {""property"": ""sensor""},
                ""Landsat"",
                ""Sentinel"",
                ""MODIS""
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var inExpr = Assert.IsType<Cql2InExpression>(result);
        Assert.Equal(4, inExpr.Arguments.Count);

        var propertyRef = Assert.IsType<Cql2PropertyRef>(inExpr.Arguments[0]);
        Assert.Equal("sensor", propertyRef.Property);
    }

    [Fact]
    public void Parse_ComplexNestedExpression_Success()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""or"",
                    ""args"": [
                        {
                            ""op"": ""="",
                            ""args"": [
                                {""property"": ""sensor""},
                                ""Landsat""
                            ]
                        },
                        {
                            ""op"": ""="",
                            ""args"": [
                                {""property"": ""sensor""},
                                ""Sentinel""
                            ]
                        }
                    ]
                },
                {
                    ""op"": ""<"",
                    ""args"": [
                        {""property"": ""cloud_cover""},
                        10
                    ]
                }
            ]
        }";

        // Act
        var result = Cql2Parser.Parse(filterJson);

        // Assert
        Assert.NotNull(result);
        var andExpr = Assert.IsType<Cql2LogicalExpression>(result);
        Assert.Equal("and", andExpr.Operator);
        Assert.Equal(2, andExpr.Arguments.Count);

        var orExpr = Assert.IsType<Cql2LogicalExpression>(andExpr.Arguments[0]);
        Assert.Equal("or", orExpr.Operator);
        Assert.Equal(2, orExpr.Arguments.Count);
    }

    [Fact]
    public void Parse_NullOrEmptyJson_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(null!));
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(string.Empty));
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse("   "));
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(invalidJson));
    }

    [Fact]
    public void Parse_MissingOperator_ThrowsException()
    {
        // Arrange
        var filterJson = @"{
            ""args"": [
                {""property"": ""cloud_cover""},
                10
            ]
        }";

        // Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(filterJson));
    }

    [Fact]
    public void Parse_MissingArgs_ThrowsException()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""=""
        }";

        // Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(filterJson));
    }

    [Fact]
    public void Parse_UnsupportedOperator_ThrowsException()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""unsupported_op"",
            ""args"": [
                {""property"": ""cloud_cover""},
                10
            ]
        }";

        // Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(filterJson));
    }

    [Fact]
    public void Parse_WrongNumberOfArgs_ThrowsException()
    {
        // Arrange - comparison operator requires exactly 2 args
        var filterJson = @"{
            ""op"": ""="",
            ""args"": [
                {""property"": ""cloud_cover""}
            ]
        }";

        // Act & Assert
        Assert.Throws<Cql2ParseException>(() => Cql2Parser.Parse(filterJson));
    }
}
