using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.GeoservicesREST;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for GeoservicesRESTQueryTranslator functionality.
/// Tests cover query parameter parsing, format resolution, pagination, result type flags,
/// field resolution, spatial filters, statistics, and error handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTQueryTranslatorTests
{
    #region Test Infrastructure

    private static HttpContext CreateHttpContext(IQueryCollection? query = null)
    {
        var context = new DefaultHttpContext();
        if (query is not null)
        {
            context.Request.QueryString = BuildQueryString(query);
        }
        return context;
    }

    private static QueryString BuildQueryString(IQueryCollection query)
    {
        var pairs = new List<string>();
        foreach (var kvp in query)
        {
            foreach (var value in kvp.Value)
            {
                pairs.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(value ?? string.Empty)}");
            }
        }
        return new QueryString($"?{string.Join("&", pairs)}");
    }

    private static QueryCollection CreateQueryCollection(Dictionary<string, StringValues> values)
    {
        return new QueryCollection(values);
    }

    private static CatalogServiceView CreateTestServiceView(int? itemLimit = null)
    {
        var service = GeoservicesTestFactory.CreateServiceDefinition(itemLimit);
        return GeoservicesTestFactory.CreateServiceView(service);
    }

    private static CatalogLayerView CreateTestLayerView(int? maxRecordCount = null, IEnumerable<FieldDefinition>? fields = null)
    {
        var layer = GeoservicesTestFactory.CreateLayerDefinition(
            fields: fields,
            maxRecordCount: maxRecordCount,
            geometryField: "shape",
            geometryType: "esriGeometryPolygon");
        return GeoservicesTestFactory.CreateLayerView(layer);
    }

    #endregion

    #region Format Resolution Tests

    [Theory]
    [InlineData("json", GeoservicesResponseFormat.Json, false)]
    [InlineData("pjson", GeoservicesResponseFormat.Json, true)]
    [InlineData("geojson", GeoservicesResponseFormat.GeoJson, false)]
    [InlineData("topojson", GeoservicesResponseFormat.TopoJson, false)]
    [InlineData("kml", GeoservicesResponseFormat.Kml, false)]
    [InlineData("kmz", GeoservicesResponseFormat.Kmz, false)]
    [InlineData("shapefile", GeoservicesResponseFormat.Shapefile, false)]
    [InlineData("csv", GeoservicesResponseFormat.Csv, false)]
    public void TryParse_FormatParameter_ParsesCorrectly(string format, GeoservicesResponseFormat expected, bool prettyPrint)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["f"] = format
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Format.Should().Be(expected);
        queryContext.PrettyPrint.Should().Be(prettyPrint);
    }

    [Fact]
    public void TryParse_NoFormatParameter_DefaultsToJson()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Format.Should().Be(GeoservicesResponseFormat.Json);
        queryContext.PrettyPrint.Should().BeFalse();
    }

    #endregion

    #region Pagination Tests

    [Theory]
    [InlineData("10", 10)]
    [InlineData("100", 100)]
    [InlineData("1000", 1000)]
    [InlineData("5000", 1000)] // Should be clamped to layer max
    public void TryParse_ResultRecordCount_ParsesCorrectly(string limit, int expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = limit
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Limit.Should().Be(expected);
    }

    [Theory]
    [InlineData("100", 100)]
    [InlineData("1000", 1000)]
    [InlineData("10000", 10000)]
    public void TryParse_ResultOffset_ParsesCorrectly(string offset, int expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = offset
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Offset.Should().Be(expected);
    }

    [Fact]
    public void TryParse_ResultOffset_Zero_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = "0"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Offset.Should().BeNull();
    }

    [Fact]
    public void TryParse_NoOffset_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Offset.Should().BeNull();
    }

    #endregion

    #region Result Type Flag Tests

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void TryParse_ReturnGeometry_ParsesCorrectly(string value, bool expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnGeometry"] = value
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnGeometry.Should().Be(expected);
    }

    [Fact]
    public void TryParse_ReturnCountOnly_SetsResultTypeToHits()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnCountOnly"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnCountOnly.Should().BeTrue();
        queryContext.Query.ResultType.Should().Be(FeatureResultType.Hits);
    }

    [Fact]
    public void TryParse_ReturnIdsOnly_SetsFlagCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnIdsOnly"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnIdsOnly.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ReturnExtentOnly_SetsFlagCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnExtentOnly"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnExtentOnly.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ReturnDistinctValues_SetsFlagAndDisablesGeometry()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnDistinctValues"] = "true",
            ["returnGeometry"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnDistinctValues.Should().BeTrue();
        queryContext.ReturnGeometry.Should().BeFalse(); // CRITICAL: Geometry must be disabled for DISTINCT
    }

    #endregion

    #region Flag Validation Tests

    [Fact]
    public void TryParse_ReturnCountOnlyAndReturnIdsOnly_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnCountOnly"] = "true",
            ["returnIdsOnly"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Field Resolution Tests

    [Fact]
    public void TryParse_OutFieldsAsterisk_ReturnsAllFields()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "*"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.OutFields.Should().Be("*");
        queryContext.Query.PropertyNames.Should().BeNull(); // null means "all fields"
    }

    [Fact]
    public void TryParse_OutFieldsSpecific_ReturnsSelectedFields()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outFields"] = "name,population"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.OutFields.Should().Be("name,population");
        queryContext.Query.PropertyNames.Should().NotBeNull();
        queryContext.Query.PropertyNames.Should().Contain("name");
        queryContext.Query.PropertyNames.Should().Contain("population");
        queryContext.Query.PropertyNames.Should().Contain("objectid"); // ID field always included
    }

    [Fact]
    public void TryParse_ReturnIdsOnly_OverridesOutFields()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnIdsOnly"] = "true",
            ["outFields"] = "name,population"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.ReturnIdsOnly.Should().BeTrue();
        queryContext.OutFields.Should().Be("objectid");
    }

    #endregion

    #region Spatial Filter Tests

    [Fact]
    public void TryParse_GeometryEnvelope_ParsesSpatialFilter()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["geometry"] = "-180,-90,180,90",
            ["geometryType"] = "esriGeometryEnvelope"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Bbox.Should().NotBeNull();
        queryContext.Query.Bbox!.MinX.Should().Be(-180);
        queryContext.Query.Bbox.MinY.Should().Be(-90);
        queryContext.Query.Bbox.MaxX.Should().Be(180);
        queryContext.Query.Bbox.MaxY.Should().Be(90);
    }

    [Fact]
    public void TryParse_NoGeometry_NoSpatialFilter()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.Bbox.Should().BeNull();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void TryParse_OutStatistics_DisablesGeometry()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outStatistics"] = @"[{""statisticType"":""count"",""onStatisticField"":""objectid"",""outStatisticFieldName"":""total""}]",
            ["returnGeometry"] = "true"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Statistics.Should().NotBeEmpty();
        queryContext.ReturnGeometry.Should().BeFalse(); // CRITICAL: Geometry disabled for statistics
    }

    [Fact]
    public void TryParse_GroupByFieldsWithoutStatistics_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["groupByFieldsForStatistics"] = "name"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TryParse_DistinctAndStatistics_ReturnsError()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["returnDistinctValues"] = "true",
            ["outStatistics"] = @"[{""statisticType"":""count"",""onStatisticField"":""objectid"",""outStatisticFieldName"":""total""}]"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public void TryParse_OrderByFields_ParsesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["orderByFields"] = "name ASC,population DESC"
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.Query.SortOrders.Should().NotBeNull();
        queryContext.Query.SortOrders.Should().HaveCount(2);
    }

    #endregion

    #region Target WKID Tests

    [Theory]
    [InlineData("4326", 4326)]
    [InlineData("3857", 3857)]
    [InlineData("EPSG:4326", 4326)]
    [InlineData("epsg:3857", 3857)]
    public void TryParse_OutSR_ParsesCorrectly(string outSR, int expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["outSR"] = outSR
        });
        var context = CreateHttpContext(query);
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act
        var result = GeoservicesRESTQueryTranslator.TryParse(
            context.Request,
            serviceView,
            layerView,
            out var queryContext,
            out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        queryContext.TargetWkid.Should().Be(expected);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void TryParse_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GeoservicesRESTQueryTranslator.TryParse(
                null!,
                serviceView,
                layerView,
                out _,
                out _));
    }

    [Fact]
    public void TryParse_NullServiceView_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateHttpContext();
        var layerView = CreateTestLayerView();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GeoservicesRESTQueryTranslator.TryParse(
                context.Request,
                null!,
                layerView,
                out _,
                out _));
    }

    [Fact]
    public void TryParse_NullLayerView_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateHttpContext();
        var serviceView = CreateTestServiceView();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GeoservicesRESTQueryTranslator.TryParse(
                context.Request,
                serviceView,
                null!,
                out _,
                out _));
    }

    #endregion
}
