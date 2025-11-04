using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Host.Wfs.Filters;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Wfs;

/// <summary>
/// Tests for filter complexity scoring and validation.
/// </summary>
[Collection("CoreTests")]
[Trait("Category", "Unit")]
public sealed class FilterComplexityTests
{
    private readonly LayerDefinition _testLayer;

    public FilterComplexityTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int", Nullable = false },
                new() { Name = "name", DataType = "string", Nullable = true },
                new() { Name = "value", DataType = "double", Nullable = true },
                new() { Name = "category", DataType = "string", Nullable = true },
                new() { Name = "active", DataType = "boolean", Nullable = true }
            }
        };
    }

    #region Basic Complexity Tests

    [Fact]
    public void CalculateComplexity_NullFilter_ReturnsZero()
    {
        // Arrange & Act
        var complexity = FilterComplexityScorer.CalculateComplexity(null);

        // Assert
        Assert.Equal(0, complexity);
    }

    [Fact]
    public void CalculateComplexity_FilterWithNullExpression_ReturnsZero()
    {
        // Arrange
        var filter = new QueryFilter(null);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.Equal(0, complexity);
    }

    [Fact]
    public void CalculateComplexity_SimpleComparison_ReturnsLowScore()
    {
        // Arrange - Simple: name = 'test'
        var filter = CqlFilterParser.Parse("name = 'test'", _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        // Should be: 1 (comparison) + 1 (field) + 0 (constant) = 2
        Assert.True(complexity > 0 && complexity < 10, $"Expected low complexity, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_SimpleAnd_ReturnsModerateScore()
    {
        // Arrange - name = 'test' AND value > 100
        var filter = CqlFilterParser.Parse("name = 'test' AND value > 100", _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        // Should be relatively low since it's a simple AND with no nesting
        Assert.True(complexity > 2 && complexity < 20, $"Expected moderate complexity, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_SimpleOr_ReturnsHigherThanAnd()
    {
        // Arrange
        var andFilter = CqlFilterParser.Parse("name = 'test' AND value > 100", _testLayer);
        var orFilter = CqlFilterParser.Parse("name = 'test' OR value > 100", _testLayer);

        // Act
        var andComplexity = FilterComplexityScorer.CalculateComplexity(andFilter);
        var orComplexity = FilterComplexityScorer.CalculateComplexity(orFilter);

        // Assert
        // OR should be more expensive than AND
        Assert.True(orComplexity > andComplexity,
            $"OR complexity ({orComplexity}) should be greater than AND complexity ({andComplexity})");
    }

    #endregion

    #region Nesting and Depth Tests

    [Fact]
    public void CalculateComplexity_NestedAnd_IncreasesWithDepth()
    {
        // Arrange
        var simple = CqlFilterParser.Parse("name = 'test' AND value > 100", _testLayer);
        var nested = CqlFilterParser.Parse("(name = 'test' AND value > 100) AND (category = 'A' AND active = true)", _testLayer);

        // Act
        var simpleComplexity = FilterComplexityScorer.CalculateComplexity(simple);
        var nestedComplexity = FilterComplexityScorer.CalculateComplexity(nested);

        // Assert
        Assert.True(nestedComplexity > simpleComplexity,
            $"Nested complexity ({nestedComplexity}) should be greater than simple complexity ({simpleComplexity})");
    }

    [Fact]
    public void CalculateComplexity_DeeplyNestedFilter_ReturnsHighScore()
    {
        // Arrange - Deeply nested: ((a AND b) OR (c AND d)) AND ((e OR f) AND (g OR h))
        var filter = CqlFilterParser.Parse(
            "((name = 'a' AND value > 1) OR (name = 'b' AND value > 2)) AND ((name = 'c' OR value > 3) AND (name = 'd' OR value > 4))",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        // Should be quite complex due to multiple levels of nesting
        Assert.True(complexity > 20, $"Expected high complexity for deeply nested filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_ThreeLevelNesting_PenalizesDepth()
    {
        // Arrange - Three levels: (((a AND b) AND c) AND d)
        var filter = CqlFilterParser.Parse(
            "(((name = 'a' AND value > 1) AND category = 'b') AND active = true)",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        // Depth penalty should apply: 5 points per level
        Assert.True(complexity > 10, $"Expected depth penalty to increase complexity, got {complexity}");
    }

    #endregion

    #region OR Operator Tests

    [Fact]
    public void CalculateComplexity_MultipleOrs_AccumulatesScore()
    {
        // Arrange - Multiple ORs: a OR b OR c OR d
        var filter = CqlFilterParser.Parse(
            "name = 'a' OR name = 'b' OR name = 'c' OR name = 'd'",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        // Each OR costs 3, so this should be more expensive
        Assert.True(complexity > 10, $"Expected high complexity for multiple ORs, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_NestedOrs_VeryExpensive()
    {
        // Arrange - Nested ORs are particularly expensive
        var filter = CqlFilterParser.Parse(
            "(name = 'a' OR value > 1) OR (category = 'b' OR active = true)",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 15, $"Expected very high complexity for nested ORs, got {complexity}");
    }

    #endregion

    #region NOT Operator Tests

    [Fact]
    public void CalculateComplexity_NotOperator_AddsComplexity()
    {
        // Arrange
        var simple = CqlFilterParser.Parse("name = 'test'", _testLayer);
        var withNot = CqlFilterParser.Parse("NOT name = 'test'", _testLayer);

        // Act
        var simpleComplexity = FilterComplexityScorer.CalculateComplexity(simple);
        var notComplexity = FilterComplexityScorer.CalculateComplexity(withNot);

        // Assert
        Assert.True(notComplexity > simpleComplexity,
            $"NOT complexity ({notComplexity}) should be greater than simple complexity ({simpleComplexity})");
    }

    [Fact]
    public void CalculateComplexity_NestedNot_IncreasesScore()
    {
        // Arrange
        var filter = CqlFilterParser.Parse("NOT (name = 'test' AND value > 100)", _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 5, $"Expected moderate complexity for NOT with AND, got {complexity}");
    }

    #endregion

    #region Edge Cases and Realistic Scenarios

    [Fact]
    public void CalculateComplexity_VeryComplexFilter_ExceedsDefaultLimit()
    {
        // Arrange - A realistically complex filter that should exceed default limit of 100
        var filter = CqlFilterParser.Parse(
            "((name = 'a' OR name = 'b' OR name = 'c') AND (value > 1 OR value < 100)) OR " +
            "((category = 'x' AND active = true) OR (category = 'y' AND active = false)) OR " +
            "((name = 'd' OR name = 'e') AND (value > 200 OR value < 300 OR value = 250)) OR " +
            "((category = 'z' OR category = 'w') AND (active = true OR name = 'special'))",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 100, $"Expected complexity > 100 for very complex filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_ModerateFilter_StaysUnderLimit()
    {
        // Arrange - A reasonable filter that should stay under the default limit
        var filter = CqlFilterParser.Parse(
            "(name = 'test' AND value > 100) OR (category = 'important' AND active = true)",
            _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity <= 100, $"Expected complexity <= 100 for moderate filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_MultipleFields_EachCountsSeparately()
    {
        // Arrange
        var oneField = CqlFilterParser.Parse("name = 'test'", _testLayer);
        var twoFields = CqlFilterParser.Parse("name = 'test' AND category = 'A'", _testLayer);
        var threeFields = CqlFilterParser.Parse("name = 'test' AND category = 'A' AND value > 100", _testLayer);

        // Act
        var oneComplexity = FilterComplexityScorer.CalculateComplexity(oneField);
        var twoComplexity = FilterComplexityScorer.CalculateComplexity(twoFields);
        var threeComplexity = FilterComplexityScorer.CalculateComplexity(threeFields);

        // Assert
        Assert.True(twoComplexity > oneComplexity, "Two fields should be more complex than one");
        Assert.True(threeComplexity > twoComplexity, "Three fields should be more complex than two");
    }

    #endregion

    #region XML Filter Tests

    [Fact]
    public void CalculateComplexity_XmlFilter_SimpleComparison()
    {
        // Arrange
        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <PropertyIsEqualTo>
                    <PropertyName>name</PropertyName>
                    <Literal>test</Literal>
                </PropertyIsEqualTo>
            </Filter>";

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 0 && complexity < 10, $"Expected low complexity for simple XML filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_XmlFilter_NestedAnd()
    {
        // Arrange
        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <And>
                    <PropertyIsEqualTo>
                        <PropertyName>name</PropertyName>
                        <Literal>test</Literal>
                    </PropertyIsEqualTo>
                    <PropertyIsGreaterThan>
                        <PropertyName>value</PropertyName>
                        <Literal>100</Literal>
                    </PropertyIsGreaterThan>
                </And>
            </Filter>";

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 0, $"Expected positive complexity for XML AND filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_XmlFilter_NestedOr()
    {
        // Arrange
        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <Or>
                    <PropertyIsEqualTo>
                        <PropertyName>name</PropertyName>
                        <Literal>a</Literal>
                    </PropertyIsEqualTo>
                    <PropertyIsEqualTo>
                        <PropertyName>name</PropertyName>
                        <Literal>b</Literal>
                    </PropertyIsEqualTo>
                    <PropertyIsEqualTo>
                        <PropertyName>name</PropertyName>
                        <Literal>c</Literal>
                    </PropertyIsEqualTo>
                </Or>
            </Filter>";

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 5, $"Expected moderate complexity for XML OR filter, got {complexity}");
    }

    [Fact]
    public void CalculateComplexity_XmlFilter_ComplexNested()
    {
        // Arrange
        var xml = @"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <And>
                    <Or>
                        <PropertyIsEqualTo>
                            <PropertyName>name</PropertyName>
                            <Literal>a</Literal>
                        </PropertyIsEqualTo>
                        <PropertyIsEqualTo>
                            <PropertyName>name</PropertyName>
                            <Literal>b</Literal>
                        </PropertyIsEqualTo>
                    </Or>
                    <Or>
                        <PropertyIsGreaterThan>
                            <PropertyName>value</PropertyName>
                            <Literal>100</Literal>
                        </PropertyIsGreaterThan>
                        <PropertyIsLessThan>
                            <PropertyName>value</PropertyName>
                            <Literal>200</Literal>
                        </PropertyIsLessThan>
                    </Or>
                </And>
            </Filter>";

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        // Act
        var complexity = FilterComplexityScorer.CalculateComplexity(filter);

        // Assert
        Assert.True(complexity > 10, $"Expected high complexity for complex nested XML filter, got {complexity}");
    }

    #endregion

    #region Comparison with CQL and XML

    [Fact]
    public void CalculateComplexity_CqlAndXml_ProduceSimilarScores()
    {
        // Arrange - Same logical filter in CQL and XML
        var cqlFilter = CqlFilterParser.Parse("name = 'test' AND value > 100", _testLayer);

        var xmlFilter = XmlFilterParser.Parse(@"
            <Filter xmlns='http://www.opengis.net/fes/2.0'>
                <And>
                    <PropertyIsEqualTo>
                        <PropertyName>name</PropertyName>
                        <Literal>test</Literal>
                    </PropertyIsEqualTo>
                    <PropertyIsGreaterThan>
                        <PropertyName>value</PropertyName>
                        <Literal>100</Literal>
                    </PropertyIsGreaterThan>
                </And>
            </Filter>", _testLayer);

        // Act
        var cqlComplexity = FilterComplexityScorer.CalculateComplexity(cqlFilter);
        var xmlComplexity = FilterComplexityScorer.CalculateComplexity(xmlFilter);

        // Assert
        // Should be exactly equal since they represent the same logical structure
        Assert.Equal(cqlComplexity, xmlComplexity);
    }

    #endregion

    #region Description Test

    [Fact]
    public void GetComplexityDescription_ReturnsNonEmpty()
    {
        // Act
        var description = FilterComplexityScorer.GetComplexityDescription();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains("complexity", description.ToLowerInvariant());
    }

    #endregion
}
