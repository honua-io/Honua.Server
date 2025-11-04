using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Host.Wcs;
using Xunit;

namespace Honua.Server.Host.Tests.Wcs;

/// <summary>
/// Unit tests for WCS 2.0/2.1 CRS extension functionality.
/// Tests CRS parsing, validation, and transformation support.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WcsCrsExtensionTests
{
    [Theory]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326", 4326)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/3857", 3857)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/32610", 32610)]
    [InlineData("urn:ogc:def:crs:EPSG::4326", 4326)]
    [InlineData("urn:ogc:def:crs:EPSG::3857", 3857)]
    [InlineData("EPSG:4326", 4326)]
    [InlineData("EPSG:3857", 3857)]
    [InlineData("4326", 4326)]
    public void TryParseCrsUri_WithValidFormats_ParsesCorrectly(string crsUri, int expectedEpsg)
    {
        // Act
        var result = WcsCrsHelper.TryParseCrsUri(crsUri, out var epsgCode);

        // Assert
        result.Should().BeTrue();
        epsgCode.Should().Be(expectedEpsg);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/abc")]
    [InlineData("EPSG:abc")]
    public void TryParseCrsUri_WithInvalidFormats_ReturnsFalse(string? crsUri)
    {
        // Act
        var result = WcsCrsHelper.TryParseCrsUri(crsUri, out var epsgCode);

        // Assert
        result.Should().BeFalse();
        epsgCode.Should().Be(0);
    }

    [Theory]
    [InlineData(4326)]
    [InlineData(3857)]
    [InlineData(32610)]
    [InlineData(32632)]
    [InlineData(3031)]
    [InlineData(3857)]
    [InlineData(27700)]
    public void IsSupportedOutputCrs_WithCommonEpsgCodes_ReturnsTrue(int epsgCode)
    {
        // Act
        var result = WcsCrsHelper.IsSupportedOutputCrs(epsgCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(99999)]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsSupportedOutputCrs_WithUnsupportedEpsgCodes_ReturnsFalse(int epsgCode)
    {
        // Act
        var result = WcsCrsHelper.IsSupportedOutputCrs(epsgCode);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(4326, "http://www.opengis.net/def/crs/EPSG/0/4326")]
    [InlineData(3857, "http://www.opengis.net/def/crs/EPSG/0/3857")]
    [InlineData(32610, "http://www.opengis.net/def/crs/EPSG/0/32610")]
    public void FormatCrsUri_WithEpsgCode_ReturnsCorrectUri(int epsgCode, string expectedUri)
    {
        // Act
        var result = WcsCrsHelper.FormatCrsUri(epsgCode);

        // Assert
        result.Should().Be(expectedUri);
    }

    [Fact]
    public void GetSupportedCrsUris_ReturnsNonEmptyList()
    {
        // Act
        var uris = WcsCrsHelper.GetSupportedCrsUris();

        // Assert
        uris.Should().NotBeNull();
        uris.Should().NotBeEmpty();
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/4326");
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/3857");
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326", null, true)]
    [InlineData(null, "http://www.opengis.net/def/crs/EPSG/0/3857", true)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326", "http://www.opengis.net/def/crs/EPSG/0/3857", true)]
    public void ValidateCrsParameters_WithValidInputs_ReturnsTrue(string? subsettingCrs, string? outputCrs, bool expectedResult)
    {
        // Arrange
        var nativeCrs = "http://www.opengis.net/def/crs/EPSG/0/4326";

        // Act
        var result = WcsCrsHelper.ValidateCrsParameters(subsettingCrs, outputCrs, nativeCrs, out var error);

        // Assert
        result.Should().Be(expectedResult);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("invalid-crs", null)]
    [InlineData(null, "EPSG:99999")]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/abc", null)]
    public void ValidateCrsParameters_WithInvalidInputs_ReturnsFalseWithError(string? subsettingCrs, string? outputCrs)
    {
        // Arrange
        var nativeCrs = "http://www.opengis.net/def/crs/EPSG/0/4326";

        // Act
        var result = WcsCrsHelper.ValidateCrsParameters(subsettingCrs, outputCrs, nativeCrs, out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null, null, 4326, false)]
    [InlineData(null, "http://www.opengis.net/def/crs/EPSG/0/4326", 4326, false)]
    [InlineData(null, "http://www.opengis.net/def/crs/EPSG/0/3857", 4326, true)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/3857", null, 4326, true)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/3857", "http://www.opengis.net/def/crs/EPSG/0/32610", 4326, true)]
    public void NeedsTransformation_WithVariousParameters_ReturnsExpectedResult(
        string? subsettingCrs,
        string? outputCrs,
        int nativeEpsg,
        bool expectedNeedsTransformation)
    {
        // Act
        var result = WcsCrsHelper.NeedsTransformation(subsettingCrs, outputCrs, nativeEpsg);

        // Assert
        result.Should().Be(expectedNeedsTransformation);
    }

    [Fact]
    public void GetNativeCrsUri_WithNullProjection_ReturnsDefaultWgs84()
    {
        // Act
        var result = WcsCrsHelper.GetNativeCrsUri(null);

        // Assert
        result.Should().Be("http://www.opengis.net/def/crs/EPSG/0/4326");
    }

    [Fact]
    public void GetNativeCrsUri_WithEmptyProjection_ReturnsDefaultWgs84()
    {
        // Act
        var result = WcsCrsHelper.GetNativeCrsUri("");

        // Assert
        result.Should().Be("http://www.opengis.net/def/crs/EPSG/0/4326");
    }

    [Theory]
    [InlineData("PROJCS[\"WGS 84 / UTM zone 10N\",GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433],AUTHORITY[\"EPSG\",\"4326\"]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",-123],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"metre\",1,AUTHORITY[\"EPSG\",\"9001\"]],AUTHORITY[\"EPSG\",\"32610\"]]", true)]
    [InlineData("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AXIS[\"Latitude\",NORTH],AXIS[\"Longitude\",EAST],AUTHORITY[\"EPSG\",\"4326\"]]", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("INVALID WKT", false)]
    public void TryExtractEpsgFromProjection_WithVariousInputs_ReturnsExpectedResult(string? wkt, bool expectedSuccess)
    {
        // Act
        var result = WcsCrsHelper.TryExtractEpsgFromProjection(wkt, out var epsgCode);

        // Assert
        result.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            epsgCode.Should().BeGreaterThan(0);
        }
        else
        {
            epsgCode.Should().Be(0);
        }
    }

    [Fact]
    public void GetSupportedCrsUris_IncludesUtmZones()
    {
        // Act
        var uris = WcsCrsHelper.GetSupportedCrsUris();

        // Assert - Check for some UTM zones
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/32610"); // UTM 10N
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/32632"); // UTM 32N
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/32710"); // UTM 10S
    }

    [Fact]
    public void GetSupportedCrsUris_IncludesPolarProjections()
    {
        // Act
        var uris = WcsCrsHelper.GetSupportedCrsUris();

        // Assert - Check for polar projections
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/3031");  // Antarctic Polar Stereographic
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/3413");  // NSIDC Sea Ice Polar Stereographic North
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/3575");  // WGS 84 / North Pole LAEA
    }

    [Fact]
    public void GetSupportedCrsUris_IncludesNationalGrids()
    {
        // Act
        var uris = WcsCrsHelper.GetSupportedCrsUris();

        // Assert - Check for national grids
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/27700"); // British National Grid
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/2056");  // Swiss CH1903+ / LV95
        uris.Should().Contain("http://www.opengis.net/def/crs/EPSG/0/28992"); // Dutch Amersfoort / RD New
    }

    [Fact]
    public void ValidateCrsParameters_WithUnsupportedOutputCrs_ReturnsFalse()
    {
        // Arrange
        var subsettingCrs = (string?)null;
        var outputCrs = "http://www.opengis.net/def/crs/EPSG/0/99999";
        var nativeCrs = "http://www.opengis.net/def/crs/EPSG/0/4326";

        // Act
        var result = WcsCrsHelper.ValidateCrsParameters(subsettingCrs, outputCrs, nativeCrs, out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().Contain("not supported");
    }
}
