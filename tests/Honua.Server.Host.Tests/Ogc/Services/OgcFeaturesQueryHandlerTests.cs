// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc.Services;

public class OgcFeaturesQueryHandlerTests
{
    private readonly OgcFeaturesQueryHandler _handler;
    private readonly Mock<IOgcCollectionResolver> _mockCollectionResolver;
    private readonly Mock<IOgcFeaturesGeoJsonHandler> _mockGeoJsonHandler;

    public OgcFeaturesQueryHandlerTests()
    {
        _mockCollectionResolver = new Mock<IOgcCollectionResolver>();
        _mockGeoJsonHandler = new Mock<IOgcFeaturesGeoJsonHandler>();
        _handler = new OgcFeaturesQueryHandler(_mockCollectionResolver.Object, _mockGeoJsonHandler.Object);
    }

    [Fact]
    public void ParseItemsQuery_WithValidParameters_ReturnsQuery()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var queryParams = new Dictionary<string, StringValues>
        {
            ["limit"] = "10",
            ["offset"] = "0"
        };
        context.Request.Query = new QueryCollection(queryParams);

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Ogc = new OgcConfiguration
            {
                ItemLimit = 1000
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var (query, contentCrs, includeCount, error) = _handler.ParseItemsQuery(
            context.Request,
            service,
            layer,
            null);

        // Assert
        Assert.Null(error);
        Assert.NotNull(query);
        Assert.Equal(10, query.Limit);
        Assert.Equal(0, query.Offset);
    }

    [Fact]
    public void ParseItemsQuery_WithInvalidParameter_ReturnsError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var queryParams = new Dictionary<string, StringValues>
        {
            ["invalid_param"] = "value"
        };
        context.Request.Query = new QueryCollection(queryParams);

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Ogc = new OgcConfiguration
            {
                ItemLimit = 1000
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var (query, contentCrs, includeCount, error) = _handler.ParseItemsQuery(
            context.Request,
            service,
            layer,
            null);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void ParseItemsQuery_WithBboxParameter_ParsesBoundingBox()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var queryParams = new Dictionary<string, StringValues>
        {
            ["bbox"] = "-180,-90,180,90",
            ["limit"] = "10"
        };
        context.Request.Query = new QueryCollection(queryParams);

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Ogc = new OgcConfiguration
            {
                ItemLimit = 1000
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var (query, contentCrs, includeCount, error) = _handler.ParseItemsQuery(
            context.Request,
            service,
            layer,
            null);

        // Assert
        Assert.Null(error);
        Assert.NotNull(query);
        Assert.NotNull(query.Bbox);
    }

    [Fact]
    public void BuildQueryablesSchema_WithValidLayer_ReturnsSchema()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            Title = "Test Layer",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "integer", Nullable = false },
                new() { Name = "name", DataType = "string", Nullable = true }
            }
        };

        // Act
        var schema = _handler.BuildQueryablesSchema(layer);

        // Assert
        Assert.NotNull(schema);
        var schemaDict = schema as IDictionary<string, object>;
        Assert.NotNull(schemaDict);
        Assert.Contains("type", schemaDict.Keys);
        Assert.Contains("properties", schemaDict.Keys);
    }

    [Fact]
    public void ConvertExtent_WithValidExtent_ReturnsOgcExtent()
    {
        // Arrange
        var extent = new LayerExtentDefinition
        {
            Bbox = new List<IReadOnlyList<double>>
            {
                new List<double> { -180, -90, 180, 90 }
            },
            Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
        };

        // Act
        var result = _handler.ConvertExtent(extent);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void ConvertExtent_WithNullExtent_ReturnsNull()
    {
        // Act
        var result = _handler.ConvertExtent(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void BuildOrderedStyleIds_WithDefaultStyle_ReturnsDefaultFirst()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            DefaultStyleId = "default",
            StyleIds = new List<string> { "style1", "default", "style2" }
        };

        // Act
        var styleIds = _handler.BuildOrderedStyleIds(layer);

        // Assert
        Assert.NotEmpty(styleIds);
        Assert.Equal("default", styleIds[0]); // Default should be first
        Assert.Equal(3, styleIds.Count); // Should not duplicate
    }

    [Fact]
    public void BuildOrderedStyleIds_WithNoStyles_ReturnsEmpty()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            StyleIds = new List<string>()
        };

        // Act
        var styleIds = _handler.BuildOrderedStyleIds(layer);

        // Assert
        Assert.Empty(styleIds);
    }

    [Fact]
    public void ParseItemsQuery_WithLimitExceedingMax_ClampsToMax()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var queryParams = new Dictionary<string, StringValues>
        {
            ["limit"] = "10000" // Exceeds max
        };
        context.Request.Query = new QueryCollection(queryParams);

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Ogc = new OgcConfiguration
            {
                ItemLimit = 1000 // Max limit
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var (query, contentCrs, includeCount, error) = _handler.ParseItemsQuery(
            context.Request,
            service,
            layer,
            null);

        // Assert
        Assert.Null(error);
        Assert.NotNull(query);
        Assert.True(query.Limit <= 1000); // Should be clamped to max
    }

    [Fact]
    public void ParseItemsQuery_WithResultTypeHits_SetsIncludeCountTrue()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var queryParams = new Dictionary<string, StringValues>
        {
            ["resultType"] = "hits",
            ["limit"] = "10"
        };
        context.Request.Query = new QueryCollection(queryParams);

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Ogc = new OgcConfiguration
            {
                ItemLimit = 1000
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            IdField = "id",
            GeometryField = "geom"
        };

        // Act
        var (query, contentCrs, includeCount, error) = _handler.ParseItemsQuery(
            context.Request,
            service,
            layer,
            null);

        // Assert
        Assert.Null(error);
        Assert.NotNull(query);
        Assert.True(includeCount);
    }
}
