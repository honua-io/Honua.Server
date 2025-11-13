// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Query.Expressions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Query;

public class QueryExpressionTests
{
    [Fact]
    public void CreateBinaryExpression_WithEquality_CreatesCorrectExpression()
    {
        // Arrange
        var left = new QueryFieldReference("name");
        var right = new QueryConstant("test");

        // Act
        var expression = new QueryBinaryExpression(
            left,
            QueryBinaryOperator.Equal,
            right);

        // Assert
        expression.Operator.Should().Be(QueryBinaryOperator.Equal);
        expression.Left.Should().Be(left);
        expression.Right.Should().Be(right);
    }

    [Theory]
    [InlineData(QueryBinaryOperator.Equal)]
    [InlineData(QueryBinaryOperator.NotEqual)]
    [InlineData(QueryBinaryOperator.LessThan)]
    [InlineData(QueryBinaryOperator.GreaterThan)]
    [InlineData(QueryBinaryOperator.LessThanOrEqual)]
    [InlineData(QueryBinaryOperator.GreaterThanOrEqual)]
    public void CreateBinaryExpression_WithComparisonOperators_CreatesValidExpression(QueryBinaryOperator op)
    {
        // Arrange
        var left = new QueryFieldReference("value");
        var right = new QueryConstant(100);

        // Act
        var expression = new QueryBinaryExpression(left, op, right);

        // Assert
        expression.Should().NotBeNull();
        expression.Operator.Should().Be(op);
    }

    [Fact]
    public void CreateLogicalExpression_WithAnd_CombinesExpressions()
    {
        // Arrange
        var expr1 = new QueryBinaryExpression(
            new QueryFieldReference("status"),
            QueryBinaryOperator.Equal,
            new QueryConstant("active"));

        var expr2 = new QueryBinaryExpression(
            new QueryFieldReference("priority"),
            QueryBinaryOperator.GreaterThan,
            new QueryConstant(5));

        // Act
        var combined = new QueryBinaryExpression(
            expr1,
            QueryBinaryOperator.And,
            expr2);

        // Assert
        combined.Operator.Should().Be(QueryBinaryOperator.And);
        combined.Left.Should().Be(expr1);
        combined.Right.Should().Be(expr2);
    }

    [Fact]
    public void CreateUnaryExpression_WithNot_NegatesExpression()
    {
        // Arrange
        var innerExpression = new QueryBinaryExpression(
            new QueryFieldReference("archived"),
            QueryBinaryOperator.Equal,
            new QueryConstant(true));

        // Act
        var notExpression = new QueryUnaryExpression(
            QueryUnaryOperator.Not,
            innerExpression);

        // Assert
        notExpression.Operator.Should().Be(QueryUnaryOperator.Not);
        notExpression.Operand.Should().Be(innerExpression);
    }

    [Fact]
    public void CreateFunctionExpression_WithUpperFunction_CreatesCorrectly()
    {
        // Arrange
        var arg = new QueryFieldReference("name");

        // Act
        var function = new QueryFunctionExpression(
            "UPPER",
            new[] { arg });

        // Assert
        function.Name.Should().Be("UPPER");
        function.Arguments.Should().Contain(arg);
    }

    [Fact]
    public void QueryConstant_WithNumericValue_StoresCorrectly()
    {
        // Arrange & Act
        var constant = new QueryConstant(42);

        // Assert
        constant.Value.Should().Be(42);
    }

    [Fact]
    public void QueryConstant_WithStringValue_StoresCorrectly()
    {
        // Arrange & Act
        var constant = new QueryConstant("test string");

        // Assert
        constant.Value.Should().Be("test string");
    }

    [Fact]
    public void QueryFieldReference_WithFieldName_CreatesCorrectly()
    {
        // Arrange & Act
        var field = new QueryFieldReference("my_field");

        // Assert
        field.Name.Should().Be("my_field");
    }
}
