using System;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Query;

/// <summary>
/// Tests for Filter-CRS parameter functionality in CQL2 spatial filters.
/// Validates that geometries in filter expressions are correctly associated with their CRS
/// and that the system properly handles CRS transformations for spatial predicates.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "CQL2")]
[Trait("Area", "Filter-CRS")]
public sealed class FilterCrsTransformationTests
{
    private readonly LayerDefinition _testLayer;

    public FilterCrsTransformationTests()
    {
        _testLayer = new LayerDefinition
        {
            Id = "test_layer",
            ServiceId = "test_service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Srid = 4326,
                Crs = "EPSG:4326"
            },
            Fields =
            [
                new FieldDefinition { Name = "id", DataType = "int", StorageType = "integer" },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "text" }
            ]
        };
    }

    #region Filter-CRS with EPSG Code Tests

    [Fact]
    public void Parse_SpatialIntersectsWithEpsgFilterCrs_SetsSridCorrectly()
    {
        // Arrange - Point in EPSG:3857 (Web Mercator)
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-13627640.0, 4544450.0]
                }
            ]
        }";

        var filterCrs = "http://www.opengis.net/def/crs/EPSG/0/3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expression);

        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        Assert.Equal("geo.intersects", funcExpr.Name);
        Assert.Equal(2, funcExpr.Arguments.Count);

        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        // Verify SRID is set to Web Mercator (3857)
        Assert.NotNull(geometryValue.Srid);
        Assert.Equal(3857, geometryValue.Srid.Value);
        Assert.Contains("POINT", geometryValue.WellKnownText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_SpatialIntersectsWithEpsg4326FilterCrs_SetsSridCorrectly()
    {
        // Arrange - Point in EPSG:4326 (WGS84)
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-122.4194, 37.7749]
                }
            ]
        }";

        var filterCrs = "http://www.opengis.net/def/crs/EPSG/0/4326";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.NotNull(geometryValue.Srid);
        Assert.Equal(4326, geometryValue.Srid.Value);
    }

    [Fact]
    public void Parse_SpatialIntersectsWithShortEpsgCode_SetsSridCorrectly()
    {
        // Arrange - Using short EPSG:3857 format
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-13627640.0, 4544450.0]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.NotNull(geometryValue.Srid);
        Assert.Equal(3857, geometryValue.Srid.Value);
    }

    #endregion

    #region Filter-CRS with OGC URN Tests

    [Fact]
    public void Parse_SpatialIntersectsWithCrs84FilterCrs_SetsSridTo4326()
    {
        // Arrange - Using OGC CRS84 (which maps to WGS84/4326)
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-122.4194, 37.7749]
                }
            ]
        }";

        var filterCrs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.NotNull(geometryValue.Srid);
        Assert.Equal(4326, geometryValue.Srid.Value);
    }

    #endregion

    #region Filter-CRS with Different Spatial Predicates

    [Fact]
    public void Parse_SpatialContainsWithFilterCrs_SetsSridCorrectly()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""s_contains"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-13627640.0, 4544450.0]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        Assert.Equal("geo.contains", funcExpr.Name);

        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);
        Assert.Equal(3857, geometryValue.Srid!.Value);
    }

    [Fact]
    public void Parse_SpatialWithinWithFilterCrs_SetsSridCorrectly()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""s_within"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Polygon"",
                    ""coordinates"": [[
                        [-13630000, 4540000],
                        [-13620000, 4540000],
                        [-13620000, 4550000],
                        [-13630000, 4550000],
                        [-13630000, 4540000]
                    ]]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        Assert.Equal("geo.within", funcExpr.Name);

        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);
        Assert.Equal(3857, geometryValue.Srid!.Value);
    }

    [Fact]
    public void Parse_SpatialCrossesWithFilterCrs_SetsSridCorrectly()
    {
        // Arrange
        var filterJson = @"{
            ""op"": ""s_crosses"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""LineString"",
                    ""coordinates"": [
                        [-13630000, 4540000],
                        [-13620000, 4550000]
                    ]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        Assert.Equal("geo.crosses", funcExpr.Name);

        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);
        Assert.Equal(3857, geometryValue.Srid!.Value);
    }

    #endregion

    #region Filter-CRS with Complex Geometries

    [Fact]
    public void Parse_PolygonWithFilterCrs_SetsSridCorrectly()
    {
        // Arrange - Polygon in EPSG:3857
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Polygon"",
                    ""coordinates"": [[
                        [-13630000, 4540000],
                        [-13620000, 4540000],
                        [-13620000, 4550000],
                        [-13630000, 4550000],
                        [-13630000, 4540000]
                    ]]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.Equal(3857, geometryValue.Srid!.Value);
        Assert.Contains("POLYGON", geometryValue.WellKnownText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MultiPointWithFilterCrs_SetsSridCorrectly()
    {
        // Arrange - MultiPoint in EPSG:3857
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""MultiPoint"",
                    ""coordinates"": [
                        [-13627640.0, 4544450.0],
                        [-13627700.0, 4544500.0]
                    ]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.Equal(3857, geometryValue.Srid!.Value);
        Assert.Contains("MULTIPOINT", geometryValue.WellKnownText, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region No Filter-CRS Tests (Default Behavior)

    [Fact]
    public void Parse_SpatialIntersectsWithoutFilterCrs_DoesNotSetSrid()
    {
        // Arrange - No Filter-CRS provided
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-122.4194, 37.7749]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, null);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        // Without Filter-CRS, SRID should not be set (null)
        Assert.Null(geometryValue.Srid);
    }

    [Fact]
    public void Parse_SpatialIntersectsWithEmptyFilterCrs_DoesNotSetSrid()
    {
        // Arrange - Empty Filter-CRS
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-122.4194, 37.7749]
                }
            ]
        }";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, string.Empty);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        Assert.Null(geometryValue.Srid);
    }

    #endregion

    #region Geometry with Embedded CRS Tests

    [Fact]
    public void Parse_GeometryWithEmbeddedSrid_PrefersSridOverFilterCrs()
    {
        // Arrange - Geometry already has SRID set
        var filterJson = @"{
            ""op"": ""s_intersects"",
            ""args"": [
                {""property"": ""geom""},
                {
                    ""type"": ""Point"",
                    ""coordinates"": [-122.4194, 37.7749],
                    ""crs"": {
                        ""type"": ""name"",
                        ""properties"": {
                            ""name"": ""EPSG:4326""
                        }
                    }
                }
            ]
        }";

        // Filter-CRS is 3857, but geometry specifies 4326
        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var funcExpr = Assert.IsType<QueryFunctionExpression>(result.Expression);
        var geometryArg = Assert.IsType<QueryConstant>(funcExpr.Arguments[1]);
        var geometryValue = Assert.IsType<QueryGeometryValue>(geometryArg.Value);

        // Embedded CRS should take precedence
        Assert.NotNull(geometryValue.Srid);
        Assert.Equal(4326, geometryValue.Srid.Value);
    }

    #endregion

    #region CRS Helper Tests

    [Theory]
    [InlineData("EPSG:3857", 3857)]
    [InlineData("EPSG:4326", 4326)]
    [InlineData("EPSG:2154", 2154)] // Lambert 93 (France)
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/3857", 3857)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326", 4326)]
    [InlineData("http://www.opengis.net/def/crs/OGC/1.3/CRS84", 4326)]
    public void CrsHelper_ParseCrs_SupportsMultipleFormats(string crsIdentifier, int expectedSrid)
    {
        // Act
        var srid = CrsHelper.ParseCrs(crsIdentifier);

        // Assert
        Assert.Equal(expectedSrid, srid);
    }

    [Theory]
    [InlineData("EPSG:3857", "http://www.opengis.net/def/crs/EPSG/0/3857")]
    [InlineData("3857", "http://www.opengis.net/def/crs/EPSG/0/3857")]
    [InlineData("EPSG:4326", "http://www.opengis.net/def/crs/OGC/1.3/CRS84")]
    [InlineData("4326", "http://www.opengis.net/def/crs/OGC/1.3/CRS84")]
    [InlineData("http://www.opengis.net/def/crs/OGC/1.3/CRS84", "http://www.opengis.net/def/crs/OGC/1.3/CRS84")]
    public void CrsHelper_NormalizeIdentifier_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var normalized = CrsHelper.NormalizeIdentifier(input);

        // Assert
        Assert.Equal(expected, normalized, ignoreCase: true);
    }

    #endregion

    #region Complex Filter Tests

    [Fact]
    public void Parse_ComplexFilterWithMultipleSpatialPredicatesAndFilterCrs_SetsSridOnAllGeometries()
    {
        // Arrange - AND of two spatial predicates
        var filterJson = @"{
            ""op"": ""and"",
            ""args"": [
                {
                    ""op"": ""s_intersects"",
                    ""args"": [
                        {""property"": ""geom""},
                        {
                            ""type"": ""Point"",
                            ""coordinates"": [-13627640.0, 4544450.0]
                        }
                    ]
                },
                {
                    ""op"": ""s_within"",
                    ""args"": [
                        {""property"": ""geom""},
                        {
                            ""type"": ""Polygon"",
                            ""coordinates"": [[
                                [-13630000, 4540000],
                                [-13620000, 4540000],
                                [-13620000, 4550000],
                                [-13630000, 4550000],
                                [-13630000, 4540000]
                            ]]
                        }
                    ]
                }
            ]
        }";

        var filterCrs = "EPSG:3857";

        // Act
        var result = Cql2JsonParser.Parse(filterJson, _testLayer, filterCrs);

        // Assert
        var andExpr = Assert.IsType<QueryBinaryExpression>(result.Expression);
        Assert.Equal(QueryBinaryOperator.And, andExpr.Operator);

        // Check left side (intersects)
        var leftFunc = Assert.IsType<QueryFunctionExpression>(andExpr.Left);
        var leftGeomArg = Assert.IsType<QueryConstant>(leftFunc.Arguments[1]);
        var leftGeomValue = Assert.IsType<QueryGeometryValue>(leftGeomArg.Value);
        Assert.Equal(3857, leftGeomValue.Srid!.Value);

        // Check right side (within)
        var rightFunc = Assert.IsType<QueryFunctionExpression>(andExpr.Right);
        var rightGeomArg = Assert.IsType<QueryConstant>(rightFunc.Arguments[1]);
        var rightGeomValue = Assert.IsType<QueryGeometryValue>(rightGeomArg.Value);
        Assert.Equal(3857, rightGeomValue.Srid!.Value);
    }

    #endregion
}
