using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

/// <summary>
/// Comprehensive test suite for OData v4 operators and functions.
/// Tests all comparison, logical, arithmetic operators and standard functions.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Category", "OData")]
public sealed class ODataOperatorsComprehensiveTests
{
    private readonly QueryEntityDefinition _entityDefinition;

    public ODataOperatorsComprehensiveTests()
    {
        var service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "stub",
            Enabled = true
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int64", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "description", DataType = "string", Nullable = true },
                new FieldDefinition { Name = "count", DataType = "int32", Nullable = true },
                new FieldDefinition { Name = "price", DataType = "double", Nullable = true },
                new FieldDefinition { Name = "quantity", DataType = "int32", Nullable = true },
                new FieldDefinition { Name = "active", DataType = "boolean", Nullable = true },
                new FieldDefinition { Name = "created_date", DataType = "datetimeoffset", Nullable = true },
                new FieldDefinition { Name = "geom", DataType = "geometry", Nullable = true }
            }
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog" },
            new[] { new FolderDefinition { Id = "root" } },
            new[] { new DataSourceDefinition { Id = "stub", Provider = "stub", ConnectionString = "stub" } },
            new[] { service },
            new[] { layer });

        var builder = new MetadataQueryModelBuilder();
        var entityDef = builder.Build(snapshot, snapshot.Services.Single(), snapshot.Layers.Single());

        _entityDefinition = entityDef;
    }

    #region Comparison Operators (eq, ne, gt, ge, lt, le)

    [Theory]
    [InlineData("count eq 10", QueryBinaryOperator.Equal)]
    [InlineData("count ne 10", QueryBinaryOperator.NotEqual)]
    [InlineData("count gt 10", QueryBinaryOperator.GreaterThan)]
    [InlineData("count ge 10", QueryBinaryOperator.GreaterThanOrEqual)]
    [InlineData("count lt 10", QueryBinaryOperator.LessThan)]
    [InlineData("count le 10", QueryBinaryOperator.LessThanOrEqual)]
    public void ComparisonOperators_ShouldParse(string filter, QueryBinaryOperator expectedOperator)
    {
        // Act
        var clause = ParseFilter(filter);
        var parser = new ODataFilterParser(_entityDefinition);
        var result = parser.Parse(clause);

        // Assert
        result.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)result.Expression!;
        binary.Operator.Should().Be(expectedOperator);
    }

    [Fact]
    public void EqualOperator_WithNull_ShouldTranslateToIsNull()
    {
        // Arrange
        var clause = ParseFilter("name eq null");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Be("t.\"name\" IS NULL");
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void NotEqualOperator_WithNull_ShouldTranslateToIsNotNull()
    {
        // Arrange
        var clause = ParseFilter("name ne null");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Be("t.\"name\" IS NOT NULL");
        parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("count eq 0", "=", 0)]
    [InlineData("count eq -100", "=", -100)]
    [InlineData("count eq 2147483647", "=", 2147483647)]
    public void ComparisonOperators_WithEdgeValues_ShouldWork(string filter, string sqlOp, int expectedValue)
    {
        // Arrange
        var clause = ParseFilter(filter);
        var parser = new ODataFilterParser(_entityDefinition);
        var result = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(result, "t");

        // Assert
        sql.Should().Be($"t.\"count\" {sqlOp} @filter_0");
        parameters.Should().ContainKey("filter_0").WhoseValue.Should().Be(expectedValue);
    }

    #endregion

    #region Logical Operators (and, or, not)

    [Fact]
    public void AndOperator_ShouldCombineConditions()
    {
        // Arrange
        var clause = ParseFilter("active eq true and count gt 5");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Be("(t.\"active\" = @filter_0) AND (t.\"count\" > @filter_1)");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void OrOperator_ShouldCombineConditions()
    {
        // Arrange
        var clause = ParseFilter("active eq true or count eq 0");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Be("(t.\"active\" = @filter_0) OR (t.\"count\" = @filter_1)");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void NotOperator_ShouldNegateCondition()
    {
        // Arrange
        var clause = ParseFilter("not (active eq true)");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Be("NOT (t.\"active\" = @filter_0)");
    }

    [Fact]
    public void ComplexLogicalExpression_ShouldRespectPrecedence()
    {
        // Arrange - (A or B) and C
        var clause = ParseFilter("(name eq 'test' or name eq 'demo') and active eq true");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("AND");
        sql.Should().Contain("OR");
        parameters.Should().HaveCount(3);
    }

    #endregion

    #region Arithmetic Operators (add, sub, mul, div, mod)

    [Theory]
    [InlineData("price add 10 eq 100", QueryBinaryOperator.Add)]
    [InlineData("price sub 10 eq 100", QueryBinaryOperator.Subtract)]
    [InlineData("price mul 2 eq 100", QueryBinaryOperator.Multiply)]
    [InlineData("price div 2 eq 50", QueryBinaryOperator.Divide)]
    [InlineData("count mod 2 eq 0", QueryBinaryOperator.Modulo)]
    public void ArithmeticOperators_ShouldParse(string filter, QueryBinaryOperator expectedOperator)
    {
        // Act
        var clause = ParseFilter(filter);
        var parser = new ODataFilterParser(_entityDefinition);
        var result = parser.Parse(clause);

        // Assert
        result.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)result.Expression!;

        // The arithmetic operation should be in the left side
        binary.Left.Should().BeOfType<QueryBinaryExpression>();
        var arithmeticExpr = (QueryBinaryExpression)binary.Left;
        arithmeticExpr.Operator.Should().Be(expectedOperator);
    }

    [Fact]
    public void AddOperator_ShouldTranslateToSql()
    {
        // Arrange
        var clause = ParseFilter("price add 10 gt 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("+");
        sql.Should().Match("*(t.\"price\" + @filter_*) > @filter_*");
    }

    [Fact]
    public void SubtractOperator_ShouldTranslateToSql()
    {
        // Arrange
        var clause = ParseFilter("price sub 10 lt 50");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("-");
        sql.Should().Match("*(t.\"price\" - @filter_*) < @filter_*");
    }

    [Fact]
    public void MultiplyOperator_ShouldTranslateToSql()
    {
        // Arrange
        var clause = ParseFilter("price mul 2 eq 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("*");
        sql.Should().Match("*(t.\"price\" * @filter_*) = @filter_*");
    }

    [Fact]
    public void DivideOperator_ShouldTranslateToSql()
    {
        // Arrange
        var clause = ParseFilter("price div 2 gt 25");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("/");
        sql.Should().Match("*(t.\"price\" / @filter_*) > @filter_*");
    }

    [Fact]
    public void ModuloOperator_ShouldTranslateToSql()
    {
        // Arrange
        var clause = ParseFilter("count mod 2 eq 0");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("%");
        sql.Should().Match("*(t.\"count\" % @filter_*) = @filter_*");
    }

    [Fact]
    public void ComplexArithmetic_WithMultipleOperators_ShouldWork()
    {
        // Arrange - (price * quantity) - 10 > 100
        var clause = ParseFilter("price mul quantity sub 10 gt 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Act
        var parameters = new Dictionary<string, object?>();
        var translator = new SqlFilterTranslator(_entityDefinition, parameters, name => $"\"{name}\"");
        var sql = translator.Translate(filter, "t");

        // Assert
        sql.Should().Contain("*");
        sql.Should().Contain("-");
        parameters.Should().HaveCount(2); // 10 and 100
    }

    #endregion

    #region String Functions

    [Fact]
    public void ContainsFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("contains(name, 'test')");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)filter.Expression!;
        function.Name.Should().Be("contains");
        function.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void StartsWithFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("startswith(name, 'prefix')");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)filter.Expression!;
        function.Name.Should().Be("startswith");
        function.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void EndsWithFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("endswith(name, 'suffix')");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)filter.Expression!;
        function.Name.Should().Be("endswith");
        function.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void LengthFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("length(name) gt 10");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("length");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void IndexOfFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("indexof(name, 'sub') gt 0");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("indexof");
        function.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void SubstringFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("substring(name, 0, 5) eq 'hello'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("substring");
        function.Arguments.Should().HaveCount(3);
    }

    [Fact]
    public void ToLowerFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("tolower(name) eq 'test'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("tolower");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void ToUpperFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("toupper(name) eq 'TEST'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("toupper");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void TrimFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("trim(name) eq 'test'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("trim");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void ConcatFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("concat(name, description) eq 'testdemo'");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("concat");
        function.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void SubstringOfFunction_ODataV3Compatibility_ShouldParse()
    {
        // Arrange - OData v3 syntax: substringof('substring', field)
        var clause = ParseFilter("substringof('test', name)");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)filter.Expression!;
        function.Name.Should().Be("substringof");
        function.Arguments.Should().HaveCount(2);
    }

    #endregion

    #region Date/Time Functions

    [Theory]
    [InlineData("year(created_date) eq 2024", "year")]
    [InlineData("month(created_date) eq 6", "month")]
    [InlineData("day(created_date) eq 15", "day")]
    [InlineData("hour(created_date) eq 14", "hour")]
    [InlineData("minute(created_date) eq 30", "minute")]
    [InlineData("second(created_date) eq 45", "second")]
    public void DateTimeExtractFunctions_ShouldParse(string filter, string expectedFunction)
    {
        // Act
        var clause = ParseFilter(filter);
        var parser = new ODataFilterParser(_entityDefinition);
        var result = parser.Parse(clause);

        // Assert
        result.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)result.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be(expectedFunction);
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void DateFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("date(created_date) eq 2024-06-15");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("date");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void TimeFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("time(created_date) gt 12:00:00");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("time");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void NowFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("created_date gt now()");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Right.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Right;
        function.Name.Should().Be("now");
        function.Arguments.Should().HaveCount(0);
    }

    #endregion

    #region Math Functions

    [Fact]
    public void RoundFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("round(price) eq 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("round");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void FloorFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("floor(price) lt 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("floor");
        function.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public void CeilingFunction_ShouldParse()
    {
        // Arrange
        var clause = ParseFilter("ceiling(price) gt 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Left.Should().BeOfType<QueryFunctionExpression>();
        var function = (QueryFunctionExpression)binary.Left;
        function.Name.Should().Be("ceiling");
        function.Arguments.Should().HaveCount(1);
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void ComplexExpression_MixedOperatorsAndFunctions_ShouldParse()
    {
        // Arrange - Complex filter: (startswith(name, 'test') and count > 10) or (price * 2 < 100)
        var clause = ParseFilter("(startswith(name, 'test') and count gt 10) or (price mul 2 lt 100)");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.Or);
    }

    [Fact]
    public void ComplexExpression_WithDateAndArithmetic_ShouldParse()
    {
        // Arrange - year(created_date) eq 2024 and (price + 10) > 100
        var clause = ParseFilter("year(created_date) eq 2024 and price add 10 gt 100");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.And);
    }

    [Fact]
    public void ComplexExpression_WithStringFunctionsAndLogic_ShouldParse()
    {
        // Arrange - tolower(name) eq 'test' or (length(description) > 100 and contains(description, 'important'))
        var clause = ParseFilter("tolower(name) eq 'test' or (length(description) gt 100 and contains(description, 'important'))");
        var parser = new ODataFilterParser(_entityDefinition);
        var filter = parser.Parse(clause);

        // Assert
        filter.Expression.Should().BeOfType<QueryBinaryExpression>();
        var binary = (QueryBinaryExpression)filter.Expression!;
        binary.Operator.Should().Be(QueryBinaryOperator.Or);
    }

    #endregion

    #region Helper Methods

    private FilterClause ParseFilter(string filter)
    {
        var model = BuildModel();
        var parser = new ODataQueryOptionParser(
            model,
            (IEdmStructuredType)model.FindType("Queries.TestEntity"),
            (IEdmNavigationSource)model.EntityContainer.FindEntitySet("TestEntities"),
            new Dictionary<string, string> { ["$filter"] = filter });
        return parser.ParseFilter();
    }

    private IEdmModel BuildModel()
    {
        var model = new EdmModel();
        var entityType = new EdmEntityType("Queries", "TestEntity");
        entityType.AddStructuralProperty("id", EdmPrimitiveTypeKind.Int64);
        entityType.AddStructuralProperty("name", EdmPrimitiveTypeKind.String);
        entityType.AddStructuralProperty("description", EdmPrimitiveTypeKind.String);
        entityType.AddStructuralProperty("count", EdmPrimitiveTypeKind.Int32);
        entityType.AddStructuralProperty("price", EdmPrimitiveTypeKind.Double);
        entityType.AddStructuralProperty("quantity", EdmPrimitiveTypeKind.Int32);
        entityType.AddStructuralProperty("active", EdmPrimitiveTypeKind.Boolean);
        entityType.AddStructuralProperty("created_date", EdmPrimitiveTypeKind.DateTimeOffset);
        entityType.AddStructuralProperty("geom", EdmCoreModel.Instance.GetSpatial(EdmPrimitiveTypeKind.GeometryPoint, isNullable: true));
        model.AddElement(entityType);

        var container = new EdmEntityContainer("Queries", "Container");
        model.AddElement(container);

        container.AddEntitySet("TestEntities", entityType);
        return model;
    }

    #endregion
}
