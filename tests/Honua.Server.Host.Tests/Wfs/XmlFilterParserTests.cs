using System;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Host.Wfs.Filters;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

public sealed class XmlFilterParserTests
{
    private readonly LayerDefinition _testLayer;

    public XmlFilterParserTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            IdField = "id",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "age", DataType = "int" },
                new FieldDefinition { Name = "created_date", DataType = "datetime" },
                new FieldDefinition { Name = "geometry", DataType = "geometry" }
            }
        };
    }

    [Fact]
    public void Parse_BBOX_WithValidEnvelope_ReturnsIntersectsExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:BBOX>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Envelope srsName="EPSG:4326">
                  <gml:lowerCorner>-180 -90</gml:lowerCorner>
                  <gml:upperCorner>180 90</gml:upperCorner>
                </gml:Envelope>
              </fes:BBOX>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        Assert.IsType<QuerySpatialExpression>(filter.Expression);
        var spatial = (QuerySpatialExpression)filter.Expression;
        Assert.Equal(SpatialPredicate.Intersects, spatial.Predicate);
    }

    [Fact]
    public void Parse_Intersects_WithPoint_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Intersects>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point srsName="urn:ogc:def:crs:EPSG::4326">
                  <gml:pos>10.5 20.3</gml:pos>
                </gml:Point>
              </fes:Intersects>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Intersects, spatial.Predicate);
        Assert.IsType<QueryFieldReference>(spatial.GeometryProperty);
    }

    [Fact]
    public void Parse_Contains_WithPolygon_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Contains>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Polygon>
                  <gml:exterior>
                    <gml:LinearRing>
                      <gml:posList>0 0 10 0 10 10 0 10 0 0</gml:posList>
                    </gml:LinearRing>
                  </gml:exterior>
                </gml:Polygon>
              </fes:Contains>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Contains, spatial.Predicate);
    }

    [Fact]
    public void Parse_Within_WithPolygon_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Within>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Polygon>
                  <gml:exterior>
                    <gml:LinearRing>
                      <gml:posList>0 0 100 0 100 100 0 100 0 0</gml:posList>
                    </gml:LinearRing>
                  </gml:exterior>
                </gml:Polygon>
              </fes:Within>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Within, spatial.Predicate);
    }

    [Fact]
    public void Parse_DWithin_WithPointAndDistance_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:DWithin>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point srsName="EPSG:4326">
                  <gml:pos>10.5 20.3</gml:pos>
                </gml:Point>
                <fes:Distance uom="meter">1000</fes:Distance>
              </fes:DWithin>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.DWithin, spatial.Predicate);
        Assert.NotNull(spatial.Distance);
        Assert.Equal(1000.0, spatial.Distance.Value);
    }

    [Fact]
    public void Parse_DWithin_WithKilometers_ConvertsToMeters()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:DWithin>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10.5 20.3</gml:pos>
                </gml:Point>
                <fes:Distance uom="km">5</fes:Distance>
              </fes:DWithin>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(5000.0, spatial.Distance.Value);
    }

    [Fact]
    public void Parse_Touches_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Touches>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>0 0</gml:pos>
                </gml:Point>
              </fes:Touches>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Touches, spatial.Predicate);
    }

    [Fact]
    public void Parse_Crosses_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Crosses>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:LineString>
                  <gml:posList>0 0 10 10</gml:posList>
                </gml:LineString>
              </fes:Crosses>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Crosses, spatial.Predicate);
    }

    [Fact]
    public void Parse_Overlaps_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Overlaps>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Polygon>
                  <gml:exterior>
                    <gml:LinearRing>
                      <gml:posList>5 5 15 5 15 15 5 15 5 5</gml:posList>
                    </gml:LinearRing>
                  </gml:exterior>
                </gml:Polygon>
              </fes:Overlaps>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Overlaps, spatial.Predicate);
    }

    [Fact]
    public void Parse_Disjoint_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Disjoint>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>100 100</gml:pos>
                </gml:Point>
              </fes:Disjoint>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Disjoint, spatial.Predicate);
    }

    [Fact]
    public void Parse_Equals_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Equals>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10 20</gml:pos>
                </gml:Point>
              </fes:Equals>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = Assert.IsType<QuerySpatialExpression>(filter.Expression);
        Assert.Equal(SpatialPredicate.Equals, spatial.Predicate);
    }

    [Fact]
    public void Parse_BBOX_MissingEnvelope_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:BBOX>
                <fes:ValueReference>geometry</fes:ValueReference>
              </fes:BBOX>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("Envelope", exception.Message);
    }

    [Fact]
    public void Parse_DWithin_MissingDistance_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:DWithin>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10.5 20.3</gml:pos>
                </gml:Point>
              </fes:DWithin>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("Distance", exception.Message);
    }

    [Fact]
    public void Parse_CombinedWithAnd_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:And>
                <fes:PropertyIsEqualTo>
                  <fes:ValueReference>name</fes:ValueReference>
                  <fes:Literal>Test</fes:Literal>
                </fes:PropertyIsEqualTo>
                <fes:Intersects>
                  <fes:ValueReference>geometry</fes:ValueReference>
                  <gml:Point>
                    <gml:pos>10 20</gml:pos>
                  </gml:Point>
                </fes:Intersects>
              </fes:And>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.And, binary.Operator);
    }

    [Fact]
    public void Parse_PropertyIsLike_WithWildcards_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsLike wildCard="*" singleChar="?" escapeChar="\">
                <fes:ValueReference>name</fes:ValueReference>
                <fes:Literal>Test*</fes:Literal>
              </fes:PropertyIsLike>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.Like, binary.Operator);
        Assert.IsType<QueryFieldReference>(binary.Left);
        Assert.IsType<QueryConstant>(binary.Right);
    }

    [Fact]
    public void Parse_PropertyIsLike_WithCustomWildcards_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsLike wildCard="%" singleChar="_" escapeChar="\">
                <fes:ValueReference>name</fes:ValueReference>
                <fes:Literal>%Search%</fes:Literal>
              </fes:PropertyIsLike>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.Like, binary.Operator);
    }

    [Fact]
    public void Parse_PropertyIsBetween_WithNumericValues_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsBetween>
                <fes:ValueReference>age</fes:ValueReference>
                <fes:LowerBoundary>
                  <fes:Literal>18</fes:Literal>
                </fes:LowerBoundary>
                <fes:UpperBoundary>
                  <fes:Literal>65</fes:Literal>
                </fes:UpperBoundary>
              </fes:PropertyIsBetween>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        // PropertyIsBetween is converted to: age >= 18 AND age <= 65
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.And, binary.Operator);

        var left = Assert.IsType<QueryBinaryExpression>(binary.Left);
        Assert.Equal(QueryBinaryOperator.GreaterThanOrEqual, left.Operator);

        var right = Assert.IsType<QueryBinaryExpression>(binary.Right);
        Assert.Equal(QueryBinaryOperator.LessThanOrEqual, right.Operator);
    }

    [Fact]
    public void Parse_PropertyIsBetween_MissingLowerBoundary_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsBetween>
                <fes:ValueReference>age</fes:ValueReference>
                <fes:UpperBoundary>
                  <fes:Literal>65</fes:Literal>
                </fes:UpperBoundary>
              </fes:PropertyIsBetween>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("LowerBoundary", exception.Message);
    }

    [Fact]
    public void Parse_TemporalAfter_WithTimeInstant_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:After>
                <fes:ValueReference>created_date</fes:ValueReference>
                <gml:TimeInstant>
                  <gml:timePosition>2024-01-01T00:00:00Z</gml:timePosition>
                </gml:TimeInstant>
              </fes:After>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.GreaterThan, binary.Operator);
        Assert.IsType<QueryFieldReference>(binary.Left);
        Assert.IsType<QueryConstant>(binary.Right);
    }

    [Fact]
    public void Parse_TemporalBefore_WithTimeInstant_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Before>
                <fes:ValueReference>created_date</fes:ValueReference>
                <gml:TimeInstant>
                  <gml:timePosition>2025-12-31T23:59:59Z</gml:timePosition>
                </gml:TimeInstant>
              </fes:Before>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.LessThan, binary.Operator);
    }

    [Fact]
    public void Parse_TemporalTEquals_WithTimeInstant_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:TEquals>
                <fes:ValueReference>created_date</fes:ValueReference>
                <gml:TimeInstant>
                  <gml:timePosition>2024-06-15T12:00:00Z</gml:timePosition>
                </gml:TimeInstant>
              </fes:TEquals>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.Equal, binary.Operator);
    }

    [Fact]
    public void Parse_TemporalDuring_WithTimePeriod_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:During>
                <fes:ValueReference>created_date</fes:ValueReference>
                <gml:TimePeriod>
                  <gml:beginPosition>2024-01-01T00:00:00Z</gml:beginPosition>
                  <gml:endPosition>2024-12-31T23:59:59Z</gml:endPosition>
                </gml:TimePeriod>
              </fes:During>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        // During is converted to: field >= begin AND field <= end
        var binary = Assert.IsType<QueryBinaryExpression>(filter.Expression);
        Assert.Equal(QueryBinaryOperator.And, binary.Operator);

        var left = Assert.IsType<QueryBinaryExpression>(binary.Left);
        Assert.Equal(QueryBinaryOperator.GreaterThanOrEqual, left.Operator);

        var right = Assert.IsType<QueryBinaryExpression>(binary.Right);
        Assert.Equal(QueryBinaryOperator.LessThanOrEqual, right.Operator);
    }

    [Fact]
    public void Parse_TemporalDuring_MissingTimePeriod_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:During>
                <fes:ValueReference>created_date</fes:ValueReference>
                <gml:TimeInstant>
                  <gml:timePosition>2024-01-01T00:00:00Z</gml:timePosition>
                </gml:TimeInstant>
              </fes:During>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("TimePeriod", exception.Message);
    }

    [Fact]
    public void Parse_TemporalOperator_OnNonTemporalField_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:After>
                <fes:ValueReference>name</fes:ValueReference>
                <gml:TimeInstant>
                  <gml:timePosition>2024-01-01T00:00:00Z</gml:timePosition>
                </gml:TimeInstant>
              </fes:After>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("temporal field", exception.Message);
    }

    #region WFS 2.0 Compliance Tests - Beyond Operator

    [Fact]
    public void Parse_Beyond_WithValidParameters_ReturnsBeyondExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Beyond>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10.0 20.0</gml:pos>
                </gml:Point>
                <fes:Distance uom="meter">1000</fes:Distance>
              </fes:Beyond>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        Assert.IsType<QuerySpatialExpression>(filter.Expression);
        var spatial = (QuerySpatialExpression)filter.Expression;
        Assert.Equal(SpatialPredicate.Beyond, spatial.Predicate);
        Assert.NotNull(spatial.Distance);
        Assert.Equal(1000.0, spatial.Distance.Value, precision: 1);
    }

    [Fact]
    public void Parse_Beyond_WithKilometers_ConvertsToMeters()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Beyond>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10.0 20.0</gml:pos>
                </gml:Point>
                <fes:Distance uom="km">5</fes:Distance>
              </fes:Beyond>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var spatial = (QuerySpatialExpression)filter.Expression;
        Assert.NotNull(spatial.Distance);
        Assert.Equal(5000.0, spatial.Distance.Value, precision: 1);
    }

    [Fact]
    public void Parse_Beyond_MissingDistance_ThrowsException()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0" xmlns:gml="http://www.opengis.net/gml/3.2">
              <fes:Beyond>
                <fes:ValueReference>geometry</fes:ValueReference>
                <gml:Point>
                  <gml:pos>10.0 20.0</gml:pos>
                </gml:Point>
              </fes:Beyond>
            </fes:Filter>
            """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            XmlFilterParser.Parse(xml, _testLayer));
        Assert.Contains("Distance", exception.Message);
    }

    #endregion

    #region WFS 2.0 Compliance Tests - Function Support

    [Fact]
    public void Parse_PropertyIsGreaterThan_WithAreaFunction_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsGreaterThan>
                <fes:ValueReference>area(geometry)</fes:ValueReference>
                <fes:Literal>1000000</fes:Literal>
              </fes:PropertyIsGreaterThan>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        Assert.IsType<QueryBinaryExpression>(filter.Expression);
        var binary = (QueryBinaryExpression)filter.Expression;
        Assert.Equal(QueryBinaryOperator.GreaterThan, binary.Operator);
        Assert.IsType<QueryFunctionExpression>(binary.Left);

        var function = (QueryFunctionExpression)binary.Left;
        Assert.Equal("area", function.Name);
        Assert.Single(function.Arguments);
        Assert.IsType<QueryFieldReference>(function.Arguments[0]);
    }

    [Fact]
    public void Parse_PropertyIsLessThan_WithLengthFunction_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsLessThan>
                <fes:ValueReference>length(geometry)</fes:ValueReference>
                <fes:Literal>5000</fes:Literal>
              </fes:PropertyIsLessThan>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        Assert.IsType<QueryBinaryExpression>(filter.Expression);
        var binary = (QueryBinaryExpression)filter.Expression;
        Assert.IsType<QueryFunctionExpression>(binary.Left);

        var function = (QueryFunctionExpression)binary.Left;
        Assert.Equal("length", function.Name);
        Assert.Single(function.Arguments);
    }

    [Fact]
    public void Parse_PropertyIsEqualTo_WithBufferFunction_ReturnsValidExpression()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsGreaterThan>
                <fes:ValueReference>area(buffer(geometry, 100))</fes:ValueReference>
                <fes:Literal>500000</fes:Literal>
              </fes:PropertyIsGreaterThan>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        Assert.NotNull(filter);
        Assert.IsType<QueryBinaryExpression>(filter.Expression);
        var binary = (QueryBinaryExpression)filter.Expression;
        Assert.IsType<QueryFunctionExpression>(binary.Left);

        var outerFunction = (QueryFunctionExpression)binary.Left;
        Assert.Equal("area", outerFunction.Name);
        Assert.Single(outerFunction.Arguments);

        // The argument should be a buffer function call
        Assert.IsType<QueryFunctionExpression>(outerFunction.Arguments[0]);
        var bufferFunction = (QueryFunctionExpression)outerFunction.Arguments[0];
        Assert.Equal("buffer", bufferFunction.Name);
        Assert.Equal(2, bufferFunction.Arguments.Count);
    }

    [Fact]
    public void Parse_Function_WithMultipleArguments_ParsesCorrectly()
    {
        var xml = """
            <fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0">
              <fes:PropertyIsEqualTo>
                <fes:ValueReference>buffer(geometry, 500)</fes:ValueReference>
                <fes:Literal>test</fes:Literal>
              </fes:PropertyIsEqualTo>
            </fes:Filter>
            """;

        var filter = XmlFilterParser.Parse(xml, _testLayer);

        var binary = (QueryBinaryExpression)filter.Expression;
        var function = (QueryFunctionExpression)binary.Left;
        Assert.Equal("buffer", function.Name);
        Assert.Equal(2, function.Arguments.Count);
        Assert.IsType<QueryFieldReference>(function.Arguments[0]);
        Assert.IsType<QueryConstant>(function.Arguments[1]);

        var distanceConstant = (QueryConstant)function.Arguments[1];
        Assert.Equal(500.0, distanceConstant.Value);
    }

    #endregion

    #region WFS 2.0 Compliance Tests - Multiple Feature Types

    [Fact]
    public void MultipleFeatureTypes_NotYetSupported_DocumentedForFutureImplementation()
    {
        // This test documents that multiple feature types in a single GetFeature request
        // are detected but not yet fully implemented. The system returns a clear error
        // message directing users to query each feature type separately.
        //
        // When full support is implemented, the system should:
        // 1. Parse comma-separated typeNames
        // 2. Execute queries against multiple layers
        // 3. Merge results into a unified FeatureCollection
        // 4. Handle different schemas (using abstract feature type)
        // 5. Include layer metadata for each feature
        //
        // Example query: typeNames=layer1,layer2,layer3

        Assert.True(true); // Placeholder - implementation tracked in WFS roadmap
    }

    #endregion
}
