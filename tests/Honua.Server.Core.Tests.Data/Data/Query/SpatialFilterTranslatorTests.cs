using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Query;

[Trait("Category", "Unit")]
public class SpatialFilterTranslatorTests
{
    private static readonly WKBWriter WkbWriter = new();

    [Fact]
    public void BuildSpatialFilter_WithIntersectsPredicate_BuildsCorrectSql()
    {
        // Arrange
        var expression = CreateSpatialExpression(
            SpatialPredicate.Intersects,
            "POINT(1 2)");
        var parameters = new Dictionary<string, object?>();

        string BuildPredicate(SpatialPredicate pred, string geomCol, string geomParam)
            => $"ST_Intersects({geomCol}, ST_GeomFromText({geomParam}))";

        // Act
        var result = SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            "geom",
            BuildPredicate,
            parameters);

        // Assert
        result.Should().Contain("ST_Intersects");
        result.Should().Contain("geom");
        parameters.Should().ContainKey("filter_spatial_0");
        parameters["filter_spatial_0"].Should().Be("POINT(1 2)");
    }

    [Fact]
    public void BuildSpatialFilter_WithContainsPredicate_BuildsCorrectSql()
    {
        // Arrange
        var expression = CreateSpatialExpression(
            SpatialPredicate.Contains,
            "POLYGON((0 0, 1 0, 1 1, 0 1, 0 0))");
        var parameters = new Dictionary<string, object?>();

        string BuildPredicate(SpatialPredicate pred, string geomCol, string geomParam)
            => $"ST_Contains({geomCol}, ST_GeomFromText({geomParam}))";

        // Act
        var result = SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            "shape",
            BuildPredicate,
            parameters);

        // Assert
        result.Should().Contain("ST_Contains");
        result.Should().Contain("shape");
    }

    [Fact]
    public void BuildSpatialFilter_WithCustomParameterPrefix_UsesPrefix()
    {
        // Arrange
        var expression = CreateSpatialExpression(
            SpatialPredicate.Intersects,
            "POINT(1 2)");
        var parameters = new Dictionary<string, object?>();

        string BuildPredicate(SpatialPredicate pred, string geomCol, string geomParam)
            => $"ST_Intersects({geomCol}, {geomParam})";

        // Act
        var result = SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            "geom",
            BuildPredicate,
            parameters,
            parameterPrefix: ":");

        // Assert
        result.Should().Contain(":filter_spatial_0");
    }

    [Fact]
    public void BuildSpatialFilter_WithNullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var act = () => SpatialFilterTranslator.BuildSpatialFilter(
            null!,
            "geom",
            (pred, col, param) => "",
            parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("expression");
    }

    [Fact]
    public void BuildSpatialFilter_WithNullGeometryColumn_ThrowsArgumentNullException()
    {
        // Arrange
        var expression = CreateSpatialExpression(SpatialPredicate.Intersects, "POINT(1 2)");
        var parameters = new Dictionary<string, object?>();

        // Act
        var act = () => SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            null!,
            (pred, col, param) => "",
            parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("geometryColumn");
    }

    [Fact]
    public void BuildSpatialFilter_WithNullBuildPredicateSql_ThrowsArgumentNullException()
    {
        // Arrange
        var expression = CreateSpatialExpression(SpatialPredicate.Intersects, "POINT(1 2)");
        var parameters = new Dictionary<string, object?>();

        // Act
        var act = () => SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            "geom",
            null!,
            parameters);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("buildPredicateSql");
    }

    [Fact]
    public void BuildSpatialFilter_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange
        var expression = CreateSpatialExpression(SpatialPredicate.Intersects, "POINT(1 2)");

        // Act
        var act = () => SpatialFilterTranslator.BuildSpatialFilter(
            expression,
            "geom",
            (pred, col, param) => "",
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void NormalizeGeometryLiteral_WithWktString_ReturnsWkt()
    {
        // Arrange
        var wkt = "POINT(10 20)";

        // Act
        var result = SpatialFilterTranslator.NormalizeGeometryLiteral(wkt);

        // Assert
        result.Should().Be(wkt);
    }

    [Fact]
    public void NormalizeGeometryLiteral_WithWkbBytes_ConvertsToWkt()
    {
        // Arrange
        var point = new Point(new Coordinate(10, 20));
        var wkb = WkbWriter.Write(point);

        // Act
        var result = SpatialFilterTranslator.NormalizeGeometryLiteral(wkb);

        // Assert
        result.Should().Contain("POINT");
    }

    [Fact]
    public void NormalizeGeometryLiteral_WithGeometryObject_ConvertsToWkt()
    {
        // Arrange
        var point = new Point(new Coordinate(5, 15));

        // Act
        var result = SpatialFilterTranslator.NormalizeGeometryLiteral(point);

        // Assert
        result.Should().Contain("POINT");
    }

    [Fact]
    public void NormalizeGeometryLiteral_WithNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SpatialFilterTranslator.NormalizeGeometryLiteral(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("geometry");
    }

    [Fact]
    public void NormalizeGeometryLiteral_WithUnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var unsupported = new { type = "custom" };

        // Act
        var act = () => SpatialFilterTranslator.NormalizeGeometryLiteral(unsupported);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(SpatialPredicate.Intersects, "ST_", FunctionNameCasing.Default, "ST_Intersects")]
    [InlineData(SpatialPredicate.Contains, "ST_", FunctionNameCasing.Default, "ST_Contains")]
    [InlineData(SpatialPredicate.Within, "ST_", FunctionNameCasing.Default, "ST_Within")]
    [InlineData(SpatialPredicate.Crosses, "ST_", FunctionNameCasing.Default, "ST_Crosses")]
    [InlineData(SpatialPredicate.Overlaps, "ST_", FunctionNameCasing.Default, "ST_Overlaps")]
    [InlineData(SpatialPredicate.Touches, "ST_", FunctionNameCasing.Default, "ST_Touches")]
    [InlineData(SpatialPredicate.Disjoint, "ST_", FunctionNameCasing.Default, "ST_Disjoint")]
    [InlineData(SpatialPredicate.Equals, "ST_", FunctionNameCasing.Default, "ST_Equals")]
    public void GetSpatialPredicateName_WithStandardPredicates_ReturnsCorrectName(
        SpatialPredicate predicate,
        string prefix,
        FunctionNameCasing casing,
        string expected)
    {
        // Act
        var result = SpatialFilterTranslator.GetSpatialPredicateName(predicate, prefix, casing);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetSpatialPredicateName_WithNoPrefix_ReturnsNoPrefixName()
    {
        // Act
        var result = SpatialFilterTranslator.GetSpatialPredicateName(
            SpatialPredicate.Intersects,
            prefix: "",
            casing: FunctionNameCasing.Default);

        // Assert
        result.Should().Be("Intersects");
    }

    [Theory]
    [InlineData(FunctionNameCasing.Upper, "ST_INTERSECTS")]
    [InlineData(FunctionNameCasing.Lower, "st_intersects")]
    [InlineData(FunctionNameCasing.Pascal, "ST_Intersects")]
    public void GetSpatialPredicateName_WithDifferentCasing_ReturnsCorrectCase(
        FunctionNameCasing casing,
        string expected)
    {
        // Act
        var result = SpatialFilterTranslator.GetSpatialPredicateName(
            SpatialPredicate.Intersects,
            "ST_",
            casing);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidateSpatialPredicate_WithSupportedPredicate_DoesNotThrow()
    {
        // Act
        var act = () => SpatialFilterTranslator.ValidateSpatialPredicate(
            SpatialPredicate.Intersects,
            SpatialPredicate.Intersects,
            SpatialPredicate.Contains);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateSpatialPredicate_WithUnsupportedPredicate_ThrowsNotSupportedException()
    {
        // Act
        var act = () => SpatialFilterTranslator.ValidateSpatialPredicate(
            SpatialPredicate.Crosses,
            SpatialPredicate.Intersects,
            SpatialPredicate.Contains);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ValidateSpatialPredicate_WithNoRestrictions_DoesNotThrow()
    {
        // Act
        var act = () => SpatialFilterTranslator.ValidateSpatialPredicate(
            SpatialPredicate.DWithin);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ExtractEnvelope_WithValidWkt_ReturnsEnvelope()
    {
        // Arrange
        var wkt = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))";

        // Act
        var envelope = SpatialFilterTranslator.ExtractEnvelope(wkt);

        // Assert
        envelope.Should().NotBeNull();
        envelope!.MinX.Should().Be(0);
        envelope.MaxX.Should().Be(10);
        envelope.MinY.Should().Be(0);
        envelope.MaxY.Should().Be(10);
    }

    [Fact]
    public void ExtractEnvelope_WithPoint_ReturnsPointEnvelope()
    {
        // Arrange
        var wkt = "POINT(5 10)";

        // Act
        var envelope = SpatialFilterTranslator.ExtractEnvelope(wkt);

        // Assert
        envelope.Should().NotBeNull();
        envelope!.MinX.Should().Be(5);
        envelope.MaxX.Should().Be(5);
        envelope.MinY.Should().Be(10);
        envelope.MaxY.Should().Be(10);
    }

    [Fact]
    public void ExtractEnvelope_WithEmptyString_ReturnsNull()
    {
        // Act
        var envelope = SpatialFilterTranslator.ExtractEnvelope("");

        // Assert
        envelope.Should().BeNull();
    }

    [Fact]
    public void ExtractEnvelope_WithInvalidWkt_ReturnsNull()
    {
        // Arrange
        var invalidWkt = "INVALID GEOMETRY";

        // Act
        var envelope = SpatialFilterTranslator.ExtractEnvelope(invalidWkt);

        // Assert
        envelope.Should().BeNull();
    }

    [Fact]
    public void AddParameter_AddsParameterAndReturnsReference()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result = SpatialFilterTranslator.AddParameter(
            parameters,
            "test",
            "value",
            "@");

        // Assert
        result.Should().Be("@test_0");
        parameters.Should().ContainKey("test_0");
        parameters["test_0"].Should().Be("value");
    }

    [Fact]
    public void AddParameter_WithMultipleParameters_IncrementsCount()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result1 = SpatialFilterTranslator.AddParameter(parameters, "param", "value1", "@");
        var result2 = SpatialFilterTranslator.AddParameter(parameters, "param", "value2", "@");

        // Assert
        result1.Should().Be("@param_0");
        result2.Should().Be("@param_1");
        parameters.Should().HaveCount(2);
    }

    [Fact]
    public void AddParameter_WithCustomPrefix_UsesPrefix()
    {
        // Arrange
        var parameters = new Dictionary<string, object?>();

        // Act
        var result = SpatialFilterTranslator.AddParameter(
            parameters,
            "test",
            "value",
            ":");

        // Assert
        result.Should().Be(":test_0");
    }

    [Theory]
    [InlineData("POINT(0 0)")]
    [InlineData("LINESTRING(0 0, 1 1)")]
    [InlineData("POLYGON((0 0, 1 0, 1 1, 0 1, 0 0))")]
    [InlineData("MULTIPOINT((0 0), (1 1))")]
    [InlineData("MULTILINESTRING((0 0, 1 1), (2 2, 3 3))")]
    [InlineData("MULTIPOLYGON(((0 0, 1 0, 1 1, 0 1, 0 0)))")]
    [InlineData("GEOMETRYCOLLECTION(POINT(0 0), LINESTRING(0 0, 1 1))")]
    public void NormalizeGeometryLiteral_WithVariousGeometryTypes_HandlesCorrectly(string wkt)
    {
        // Act
        var result = SpatialFilterTranslator.NormalizeGeometryLiteral(wkt);

        // Assert
        result.Should().Be(wkt);
    }

    [Theory]
    [InlineData(SpatialPredicate.DWithin)]
    [InlineData(SpatialPredicate.Beyond)]
    public void GetSpatialPredicateName_WithDistancePredicates_ReturnsCorrectName(
        SpatialPredicate predicate)
    {
        // Act
        var result = SpatialFilterTranslator.GetSpatialPredicateName(predicate);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(predicate.ToString());
    }

    [Fact]
    public void ExtractEnvelope_WithLineString_ReturnsCorrectBounds()
    {
        // Arrange
        var wkt = "LINESTRING(-5 -10, 15 20)";

        // Act
        var envelope = SpatialFilterTranslator.ExtractEnvelope(wkt);

        // Assert
        envelope.Should().NotBeNull();
        envelope!.MinX.Should().Be(-5);
        envelope.MaxX.Should().Be(15);
        envelope.MinY.Should().Be(-10);
        envelope.MaxY.Should().Be(20);
    }

    private QuerySpatialExpression CreateSpatialExpression(
        SpatialPredicate predicate,
        string wkt)
    {
        return new QuerySpatialExpression(
            predicate,
            new QueryFieldReference("geom"),
            new QueryConstant(new QueryGeometryValue(wkt, 4326)));
    }
}
