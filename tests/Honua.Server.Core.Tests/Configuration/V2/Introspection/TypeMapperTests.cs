// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Configuration.V2.Introspection;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Introspection;

public sealed class TypeMapperTests
{
    [Theory]
    [InlineData("postgresql", "integer", "int")]
    [InlineData("postgresql", "bigint", "long")]
    [InlineData("postgresql", "text", "string")]
    [InlineData("postgresql", "boolean", "bool")]
    [InlineData("postgresql", "timestamp", "datetime")]
    [InlineData("postgresql", "uuid", "guid")]
    [InlineData("postgresql", "geometry", "geometry")]
    [InlineData("postgresql", "point", "geometry")]
    [InlineData("postgresql", "double precision", "double")]
    public void MapToHonuaType_PostgreSql_MapsCorrectly(string provider, string dbType, string expected)
    {
        // Act
        var result = TypeMapper.MapToHonuaType(provider, dbType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("sqlite", "integer", "int")]
    [InlineData("sqlite", "text", "string")]
    [InlineData("sqlite", "real", "double")]
    [InlineData("sqlite", "blob", "binary")]
    [InlineData("sqlite", "datetime", "datetime")]
    [InlineData("sqlite", "geometry", "geometry")]
    public void MapToHonuaType_Sqlite_MapsCorrectly(string provider, string dbType, string expected)
    {
        // Act
        var result = TypeMapper.MapToHonuaType(provider, dbType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("sqlserver", "int", "int")]
    [InlineData("sqlserver", "bigint", "long")]
    [InlineData("sqlserver", "nvarchar", "string")]
    [InlineData("sqlserver", "bit", "bool")]
    [InlineData("sqlserver", "datetime2", "datetime")]
    [InlineData("sqlserver", "uniqueidentifier", "guid")]
    [InlineData("sqlserver", "geometry", "geometry")]
    public void MapToHonuaType_SqlServer_MapsCorrectly(string provider, string dbType, string expected)
    {
        // Act
        var result = TypeMapper.MapToHonuaType(provider, dbType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("mysql", "int", "int")]
    [InlineData("mysql", "bigint", "long")]
    [InlineData("mysql", "varchar", "string")]
    [InlineData("mysql", "tinyint", "int")]
    [InlineData("mysql", "datetime", "datetime")]
    [InlineData("mysql", "geometry", "geometry")]
    [InlineData("mysql", "json", "json")]
    public void MapToHonuaType_MySql_MapsCorrectly(string provider, string dbType, string expected)
    {
        // Act
        var result = TypeMapper.MapToHonuaType(provider, dbType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapToHonuaType_UnknownProvider_ReturnsStringAsDefault()
    {
        // Act
        var result = TypeMapper.MapToHonuaType("unknown_provider", "some_type");

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void MapToHonuaType_UnknownType_ReturnsStringAsDefault()
    {
        // Act
        var result = TypeMapper.MapToHonuaType("postgresql", "unknown_type");

        // Assert
        Assert.Equal("string", result);
    }

    [Theory]
    [InlineData("point", "Point")]
    [InlineData("linestring", "LineString")]
    [InlineData("polygon", "Polygon")]
    [InlineData("multipoint", "MultiPoint")]
    [InlineData("multilinestring", "MultiLineString")]
    [InlineData("multipolygon", "MultiPolygon")]
    [InlineData("geometrycollection", "GeometryCollection")]
    [InlineData("geometry", "Geometry")]
    [InlineData("POINT", "Point")]
    [InlineData("st_point", "Point")]
    public void NormalizeGeometryType_VariousFormats_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var result = TypeMapper.NormalizeGeometryType(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
