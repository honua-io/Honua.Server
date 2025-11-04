using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for GeoservicesParameterResolver functionality.
/// Tests cover format resolution, limit/offset parsing, boolean parameter parsing,
/// and map scale calculation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "GeoservicesREST")]
[Trait("Speed", "Fast")]
public sealed class GeoservicesRESTParameterResolverTests
{
    #region Test Infrastructure

    private static QueryCollection CreateQueryCollection(Dictionary<string, StringValues> values)
    {
        return new QueryCollection(values);
    }

    private static CatalogServiceView CreateTestServiceView(int? itemLimit = null)
    {
        var service = GeoservicesTestFactory.CreateServiceDefinition(itemLimit);
        return GeoservicesTestFactory.CreateServiceView(service);
    }

    private static CatalogLayerView CreateTestLayerView(int? maxRecordCount = null)
    {
        var layer = GeoservicesTestFactory.CreateLayerDefinition(maxRecordCount: maxRecordCount);
        return GeoservicesTestFactory.CreateLayerView(layer);
    }

    #endregion

    #region ResolveFormat Tests

    [Theory]
    [InlineData("json", GeoservicesResponseFormat.Json, false)]
    [InlineData("pjson", GeoservicesResponseFormat.Json, true)]
    [InlineData("geojson", GeoservicesResponseFormat.GeoJson, false)]
    [InlineData("topojson", GeoservicesResponseFormat.TopoJson, false)]
    [InlineData("kml", GeoservicesResponseFormat.Kml, false)]
    [InlineData("kmz", GeoservicesResponseFormat.Kmz, false)]
    [InlineData("shapefile", GeoservicesResponseFormat.Shapefile, false)]
    [InlineData("csv", GeoservicesResponseFormat.Csv, false)]
    public void ResolveFormat_ValidFormat_ParsesCorrectly(string format, GeoservicesResponseFormat expected, bool prettyPrint)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["f"] = format
        });

        // Act
        var (actualFormat, actualPrettyPrint) = GeoservicesParameterResolver.ResolveFormat(query);

        // Assert
        actualFormat.Should().Be(expected);
        actualPrettyPrint.Should().Be(prettyPrint);
    }

    [Fact]
    public void ResolveFormat_NoParameter_DefaultsToJson()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());

        // Act
        var (format, prettyPrint) = GeoservicesParameterResolver.ResolveFormat(query);

        // Assert
        format.Should().Be(GeoservicesResponseFormat.Json);
        prettyPrint.Should().BeFalse();
    }

    [Theory]
    [InlineData("JSON", GeoservicesResponseFormat.Json)]
    [InlineData("GeoJSON", GeoservicesResponseFormat.GeoJson)]
    [InlineData("PJSON", GeoservicesResponseFormat.Json)]
    public void ResolveFormat_CaseInsensitive_ParsesCorrectly(string format, GeoservicesResponseFormat expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["f"] = format
        });

        // Act
        var (actualFormat, _) = GeoservicesParameterResolver.ResolveFormat(query);

        // Assert
        actualFormat.Should().Be(expected);
    }

    [Fact]
    public void ResolveFormat_WithConfig_UsesConfigDefault()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var config = new GeoservicesRESTConfiguration
        {
            DefaultFormat = "geojson"
        };

        // Act
        var (format, _) = GeoservicesParameterResolver.ResolveFormat(query, config);

        // Assert
        format.Should().Be(GeoservicesResponseFormat.GeoJson);
    }

    #endregion

    #region ResolveLimit Tests

    [Theory]
    [InlineData("10", 10)]
    [InlineData("100", 100)]
    [InlineData("500", 500)]
    [InlineData("1000", 1000)]
    public void ResolveLimit_ValidLimit_ParsesCorrectly(string limit, int expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = limit
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act
        var actualLimit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);

        // Assert
        actualLimit.Should().Be(expected);
    }

    [Fact]
    public void ResolveLimit_ExceedsLayerMax_ClampsToLayerMax()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = "5000"
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act
        var limit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);

        // Assert
        limit.Should().Be(1000); // CRITICAL: Clamped to layer max
    }

    [Fact]
    public void ResolveLimit_ExceedsServiceMax_ClampsToServiceMax()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = "20000"
        });
        var serviceView = CreateTestServiceView(itemLimit: 10000);
        var layerView = CreateTestLayerView(maxRecordCount: 15000);

        // Act
        var limit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);

        // Assert
        limit.Should().Be(10000); // CRITICAL: Clamped to service max
    }

    [Fact]
    public void ResolveLimit_NoParameter_UsesDefaultLimit()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act
        var limit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);

        // Assert
        limit.Should().Be(1000); // Uses layer max as default
    }

    [Fact]
    public void ResolveLimit_Zero_ThrowsException()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = "0"
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    public void ResolveLimit_NegativeValue_ThrowsException(string limit)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = limit
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not_a_number")]
    [InlineData("12.5")]
    public void ResolveLimit_InvalidFormat_ThrowsException(string limit)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = limit
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView();

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView));
    }

    #endregion

    #region ResolveOffset Tests

    [Theory]
    [InlineData("10", 10)]
    [InlineData("100", 100)]
    [InlineData("1000", 1000)]
    [InlineData("10000", 10000)]
    public void ResolveOffset_ValidOffset_ParsesCorrectly(string offset, int expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = offset
        });

        // Act
        var actualOffset = GeoservicesParameterResolver.ResolveOffset(query);

        // Assert
        actualOffset.Should().Be(expected);
    }

    [Fact]
    public void ResolveOffset_Zero_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = "0"
        });

        // Act
        var actualOffset = GeoservicesParameterResolver.ResolveOffset(query);

        // Assert
        actualOffset.Should().BeNull();
    }

    [Fact]
    public void ResolveOffset_NoParameter_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());

        // Act
        var offset = GeoservicesParameterResolver.ResolveOffset(query);

        // Assert
        offset.Should().BeNull();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    public void ResolveOffset_NegativeValue_ThrowsException(string offset)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = offset
        });

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveOffset(query));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not_a_number")]
    [InlineData("10.5")]
    public void ResolveOffset_InvalidFormat_ThrowsException(string offset)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = offset
        });

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveOffset(query));
    }

    #endregion

    #region ResolveBoolean Tests

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("Y", true)]
    [InlineData("N", false)]
    public void ResolveBoolean_ValidValues_ParsesCorrectly(string value, bool expected)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["testParam"] = value
        });

        // Act
        var result = GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: false);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveBoolean_NoParameter_ReturnsDefault()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>());

        // Act
        var resultTrue = GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: true);
        var resultFalse = GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: false);

        // Assert
        resultTrue.Should().BeTrue();
        resultFalse.Should().BeFalse();
    }

    [Theory]
    [InlineData("2")]
    [InlineData("invalid")]
    public void ResolveBoolean_InvalidValue_ThrowsException(string value)
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["testParam"] = value
        });

        // Act & Assert
        Assert.Throws<GeoservicesRESTQueryException>(() =>
            GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: false));
    }

    [Fact]
    public void ResolveBoolean_EmptyString_ReturnsDefault()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["testParam"] = ""
        });

        // Act
        var result = GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: true);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ResolveMapScale Tests

    [Fact]
    public void ResolveMapScale_ValidParameters_CalculatesCorrectly()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90",
            ["imageDisplay"] = "800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().NotBeNull();
        mapScale.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResolveMapScale_NoMapExtent_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["imageDisplay"] = "800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_NoImageDisplay_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_EmptyMapExtent_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "",
            ["imageDisplay"] = "800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_InvalidMapExtent_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "invalid,data",
            ["imageDisplay"] = "800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_InvalidImageDisplay_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90",
            ["imageDisplay"] = "invalid,data"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_NegativePixels_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90",
            ["imageDisplay"] = "-800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_ZeroExtent_ReturnsNull()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "0,0,0,0",
            ["imageDisplay"] = "800,600,96"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().BeNull();
    }

    [Fact]
    public void ResolveMapScale_DefaultDpi_Uses96()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90",
            ["imageDisplay"] = "800,600" // No DPI specified
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().NotBeNull();
        mapScale.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ResolveMapScale_CustomDpi_UsesSpecifiedValue()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["mapExtent"] = "-180,-90,180,90",
            ["imageDisplay"] = "800,600,150"
        });

        // Act
        var mapScale = GeoservicesParameterResolver.ResolveMapScale(query);

        // Assert
        mapScale.Should().NotBeNull();
        mapScale.Should().BeGreaterThan(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ResolveLimit_MultipleValues_UsesLast()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultRecordCount"] = new StringValues(new[] { "100", "200", "300" })
        });
        var serviceView = CreateTestServiceView();
        var layerView = CreateTestLayerView(maxRecordCount: 1000);

        // Act
        var limit = GeoservicesParameterResolver.ResolveLimit(query, serviceView, layerView);

        // Assert
        limit.Should().Be(300); // Last value wins
    }

    [Fact]
    public void ResolveOffset_MultipleValues_UsesLast()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["resultOffset"] = new StringValues(new[] { "10", "20", "30" })
        });

        // Act
        var offset = GeoservicesParameterResolver.ResolveOffset(query);

        // Assert
        offset.Should().Be(30); // Last value wins
    }

    [Fact]
    public void ResolveBoolean_MultipleValues_UsesLast()
    {
        // Arrange
        var query = CreateQueryCollection(new Dictionary<string, StringValues>
        {
            ["testParam"] = new StringValues(new[] { "false", "true", "false" })
        });

        // Act
        var result = GeoservicesParameterResolver.ResolveBoolean(query, "testParam", defaultValue: true);

        // Assert
        result.Should().BeFalse(); // Last value wins
    }

    #endregion
}
