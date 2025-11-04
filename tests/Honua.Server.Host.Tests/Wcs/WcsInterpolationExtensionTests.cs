using Honua.Server.Host.Wcs;
using Xunit;

namespace Honua.Server.Host.Tests.Wcs;

public class WcsInterpolationExtensionTests
{
    [Theory]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/nearest-neighbor", "near")]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/linear", "bilinear")]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/cubic", "cubic")]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/cubic-spline", "cubicspline")]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/average", "average")]
    public void TryParseInterpolation_WithOgcUri_ReturnsGdalMethod(string ogcUri, string expectedGdalMethod)
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation(ogcUri, out var gdalMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedGdalMethod, gdalMethod);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("nearest-neighbor", "near")]
    [InlineData("linear", "bilinear")]
    [InlineData("cubic", "cubic")]
    [InlineData("cubic-spline", "cubicspline")]
    [InlineData("average", "average")]
    public void TryParseInterpolation_WithShortForm_ReturnsGdalMethod(string shortForm, string expectedGdalMethod)
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation(shortForm, out var gdalMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedGdalMethod, gdalMethod);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("near")]
    [InlineData("bilinear")]
    [InlineData("cubic")]
    [InlineData("lanczos")]
    [InlineData("average")]
    [InlineData("mode")]
    public void TryParseInterpolation_WithGdalMethod_ReturnsGdalMethod(string gdalMethod)
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation(gdalMethod, out var parsedMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(gdalMethod, parsedMethod);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseInterpolation_WithNull_ReturnsDefault()
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation(null, out var gdalMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal("near", gdalMethod);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseInterpolation_WithEmpty_ReturnsDefault()
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation("", out var gdalMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal("near", gdalMethod);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseInterpolation_WithUnsupportedMethod_ReturnsFalse()
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation("unsupported-method", out var gdalMethod, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("near", gdalMethod); // Returns default
        Assert.NotNull(error);
        Assert.Contains("unsupported-method", error);
        Assert.Contains("not supported", error);
    }

    [Theory]
    [InlineData("nearest-neighbor")]
    [InlineData("linear")]
    [InlineData("cubic")]
    [InlineData("http://www.opengis.net/def/interpolation/OGC/1/nearest-neighbor")]
    [InlineData(null)]
    [InlineData("")]
    public void IsSupported_WithValidMethod_ReturnsTrue(string? method)
    {
        // Act
        var result = WcsInterpolationHelper.IsSupported(method);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSupported_WithUnsupportedMethod_ReturnsFalse()
    {
        // Act
        var result = WcsInterpolationHelper.IsSupported("invalid-method");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetDefaultGdalMethod_ReturnsNearestNeighbor()
    {
        // Act
        var defaultMethod = WcsInterpolationHelper.GetDefaultGdalMethod();

        // Assert
        Assert.Equal("near", defaultMethod);
    }

    [Fact]
    public void GetSupportedInterpolationUris_ReturnsOgcUris()
    {
        // Act
        var uris = WcsInterpolationHelper.GetSupportedInterpolationUris().ToList();

        // Assert
        Assert.Contains("http://www.opengis.net/def/interpolation/OGC/1/nearest-neighbor", uris);
        Assert.Contains("http://www.opengis.net/def/interpolation/OGC/1/linear", uris);
        Assert.Contains("http://www.opengis.net/def/interpolation/OGC/1/cubic", uris);
        Assert.Contains("http://www.opengis.net/def/interpolation/OGC/1/cubic-spline", uris);
        Assert.Contains("http://www.opengis.net/def/interpolation/OGC/1/average", uris);
        Assert.Equal(5, uris.Count);
    }

    [Theory]
    [InlineData("near", "Nearest neighbor")]
    [InlineData("bilinear", "Bilinear")]
    [InlineData("cubic", "Cubic convolution")]
    [InlineData("lanczos", "Lanczos")]
    public void GetMethodDescription_ReturnsDescription(string gdalMethod, string expectedDescriptionPart)
    {
        // Act
        var description = WcsInterpolationHelper.GetMethodDescription(gdalMethod);

        // Assert
        Assert.NotNull(description);
        Assert.Contains(expectedDescriptionPart, description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NEAREST-NEIGHBOR", "near")]
    [InlineData("LINEAR", "bilinear")]
    [InlineData("CUBIC", "cubic")]
    public void TryParseInterpolation_IsCaseInsensitive(string input, string expectedGdalMethod)
    {
        // Act
        var result = WcsInterpolationHelper.TryParseInterpolation(input, out var gdalMethod, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedGdalMethod, gdalMethod);
        Assert.Null(error);
    }
}
