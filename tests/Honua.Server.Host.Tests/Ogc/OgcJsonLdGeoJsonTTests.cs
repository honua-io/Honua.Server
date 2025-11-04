using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public class OgcJsonLdGeoJsonTTests
{
    [Theory]
    [InlineData("jsonld", OgcResponseFormat.JsonLd)]
    [InlineData("json-ld", OgcResponseFormat.JsonLd)]
    [InlineData("application/ld+json", OgcResponseFormat.JsonLd)]
    [InlineData("geojson-t", OgcResponseFormat.GeoJsonT)]
    [InlineData("geojsont", OgcResponseFormat.GeoJsonT)]
    [InlineData("application/geo+json-t", OgcResponseFormat.GeoJsonT)]
    public void ParseFormat_WithJsonLdOrGeoJsonT_ReturnsCorrectFormat(string formatString, OgcResponseFormat expectedFormat)
    {
        // Act
        var result = OgcQueryParser.ParseFormat(formatString);

        // Assert
        Assert.Equal(expectedFormat, result.Format);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(OgcResponseFormat.JsonLd, "application/ld+json")]
    [InlineData(OgcResponseFormat.GeoJsonT, "application/geo+json-t")]
    public void GetMimeType_WithNewFormats_ReturnsCorrectMimeType(OgcResponseFormat format, string expectedMimeType)
    {
        // Act
        var mimeType = OgcQueryParser.GetMimeType(format);

        // Assert
        Assert.Equal(expectedMimeType, mimeType);
    }

    [Fact]
    public void TryMapMediaType_WithJsonLd_ReturnsJsonLdFormat()
    {
        // This test verifies the content negotiation through Accept headers
        // We'll test this by creating a mock HTTP request with the Accept header

        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept"] = "application/ld+json";

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.JsonLd, format);
        Assert.Equal("application/ld+json", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void TryMapMediaType_WithGeoJsonT_ReturnsGeoJsonTFormat()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept"] = "application/geo+json-t";

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.GeoJsonT, format);
        Assert.Equal("application/geo+json-t", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveResponseFormat_WithJsonLdQueryParameter_ReturnsJsonLdFormat()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?f=jsonld");

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.JsonLd, format);
        Assert.Equal("application/ld+json", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveResponseFormat_WithGeoJsonTQueryParameter_ReturnsGeoJsonTFormat()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?f=geojson-t");

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.GeoJsonT, format);
        Assert.Equal("application/geo+json-t", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveResponseFormat_WithAcceptHeader_PrefersJsonLdOverDefault()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept"] = "application/ld+json, application/json;q=0.8";

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.JsonLd, format);
        Assert.Equal("application/ld+json", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveResponseFormat_WithMultipleAcceptHeaders_SelectsHighestQuality()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept"] = "application/geo+json-t;q=0.9, application/ld+json;q=1.0, application/json;q=0.5";

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.Equal(OgcResponseFormat.JsonLd, format);
    }

    [Fact]
    public void ResolveResponseFormat_QueryParameterOverridesAcceptHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?f=geojson-t");
        context.Request.Headers["Accept"] = "application/ld+json";

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        // Query parameter should take precedence
        Assert.Equal(OgcResponseFormat.GeoJsonT, format);
        Assert.Equal("application/geo+json-t", contentType);
        Assert.Null(error);
    }

    [Fact]
    public void ResolveResponseFormat_WithUnsupportedFormat_ReturnsError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?f=unsupported-format");

        // Act
        var (format, contentType, error) = OgcQueryParser.ResolveResponseFormat(context.Request);

        // Assert
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("json-ld")]
    [InlineData("jsonld")]
    [InlineData("JSONLD")]
    [InlineData("JSON-LD")]
    public void ParseFormat_IsCaseInsensitive_ForJsonLd(string formatString)
    {
        // Act
        var result = OgcQueryParser.ParseFormat(formatString);

        // Assert
        Assert.Equal(OgcResponseFormat.JsonLd, result.Format);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("geojson-t")]
    [InlineData("geojsont")]
    [InlineData("GEOJSON-T")]
    [InlineData("GeoJsonT")]
    public void ParseFormat_IsCaseInsensitive_ForGeoJsonT(string formatString)
    {
        // Act
        var result = OgcQueryParser.ParseFormat(formatString);

        // Assert
        Assert.Equal(OgcResponseFormat.GeoJsonT, result.Format);
        Assert.Null(result.Error);
    }
}
