using FluentAssertions;
using Honua.MapSDK.Models;
using Honua.MapSDK.Utilities;
using Xunit;

namespace Honua.MapSDK.Tests.UtilityTests;

/// <summary>
/// Comprehensive tests for CoordinateConverter utility
/// Tests cover: all coordinate formats, conversions, precision, edge cases, invalid inputs
/// </summary>
public class CoordinateConverterTests
{
    #region Decimal Degrees Tests

    [Theory]
    [InlineData(-122.4194, 37.7749, 6, "37.774900°N, 122.419400°W")]
    [InlineData(0, 0, 6, "0.000000°N, 0.000000°E")]
    [InlineData(-180, -90, 6, "90.000000°S, 180.000000°W")]
    [InlineData(180, 90, 6, "90.000000°N, 180.000000°E")]
    public void ToDecimalDegrees_ShouldFormatCorrectly(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-122.4194, 37.7749, 2, "37.77°N, 122.42°W")]
    [InlineData(-122.4194, 37.7749, 4, "37.7749°N, 122.4194°W")]
    public void ToDecimalDegrees_ShouldRespectPrecision(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToDecimalDegrees_ShouldHandlePositiveCoordinates()
    {
        // Arrange
        double lon = 151.2093;
        double lat = -33.8688;

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 4);

        // Assert
        result.Should().Contain("S"); // Southern hemisphere
        result.Should().Contain("E"); // Eastern hemisphere
    }

    #endregion

    #region Degrees Decimal Minutes (DDM) Tests

    [Theory]
    [InlineData(-122.4194, 37.7749, 3, "37°46.494'N 122°25.164'W")]
    [InlineData(0, 0, 3, "0°0.000'N 0°0.000'E")]
    public void ToDegreesDecimalMinutes_ShouldFormatCorrectly(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDegreesDecimalMinutes(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-122.5, 37.5, 0, "37°30'N 122°30'W")]
    [InlineData(-122.25, 37.25, 0, "37°15'N 122°15'W")]
    public void ToDegreesDecimalMinutes_ShouldHandleExactMinutes(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDegreesDecimalMinutes(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Degrees Minutes Seconds (DMS) Tests

    [Theory]
    [InlineData(-122.4194, 37.7749, 1, "37°46'29.6\" N 122°25'9.8\" W")]
    [InlineData(0, 0, 1, "0°0'0.0\" N 0°0'0.0\" E")]
    public void ToDegreesMinutesSeconds_ShouldFormatCorrectly(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDegreesMinutesSeconds(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-122.0, 37.0, 0, "37°0'0\" N 122°0'0\" W")]
    [InlineData(-122.5, 37.5, 0, "37°30'0\" N 122°30'0\" W")]
    public void ToDegreesMinutesSeconds_ShouldHandleExactDegrees(double lon, double lat, int precision, string expected)
    {
        // Act
        var result = CoordinateConverter.ToDegreesMinutesSeconds(lon, lat, precision);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region UTM Tests

    [Fact]
    public void ToUTM_ShouldFormatSanFrancisco()
    {
        // Arrange
        double lon = -122.4194;
        double lat = 37.7749;

        // Act
        var result = CoordinateConverter.ToUTM(lon, lat);

        // Assert
        result.Should().Contain("10N"); // Zone 10, Northern hemisphere
        result.Should().Contain("mE");
        result.Should().Contain("mN");
    }

    [Fact]
    public void ToUTM_ShouldHandleEquator()
    {
        // Arrange
        double lon = 0;
        double lat = 0;

        // Act
        var result = CoordinateConverter.ToUTM(lon, lat);

        // Assert
        result.Should().Contain("31N"); // Zone 31 for 0° longitude
    }

    [Fact]
    public void ToUTM_ShouldHandleSouthernHemisphere()
    {
        // Arrange
        double lon = 151.2093;
        double lat = -33.8688; // Sydney

        // Act
        var result = CoordinateConverter.ToUTM(lon, lat);

        // Assert
        result.Should().Contain("S"); // Southern hemisphere
        result.Should().Contain("56S"); // Zone 56
    }

    [Theory]
    [InlineData(-180, 0, 1)] // Western edge
    [InlineData(-174, 0, 1)] // Zone 1
    [InlineData(0, 0, 31)]   // Prime meridian - Zone 31
    [InlineData(6, 0, 32)]   // Zone 32
    [InlineData(174, 0, 60)] // Zone 60
    [InlineData(180, 0, 60)] // Eastern edge
    public void ToUTM_ShouldCalculateCorrectZone(double lon, double lat, int expectedZone)
    {
        // Act
        var result = CoordinateConverter.ToUTM(lon, lat);

        // Assert
        result.Should().StartWith($"{expectedZone}");
    }

    #endregion

    #region MGRS Tests

    [Fact]
    public void ToMGRS_ShouldFormatSanFrancisco()
    {
        // Arrange
        double lon = -122.4194;
        double lat = 37.7749;

        // Act
        var result = CoordinateConverter.ToMGRS(lon, lat);

        // Assert
        result.Should().StartWith("10"); // Zone 10
        result.Should().HaveLength(15); // Format: ZZ[Band][Col][Row][5-digit-E][5-digit-N]
    }

    [Fact]
    public void ToMGRS_ShouldHandleEquator()
    {
        // Arrange
        double lon = 0;
        double lat = 0;

        // Act
        var result = CoordinateConverter.ToMGRS(lon, lat);

        // Assert
        result.Should().StartWith("31"); // Zone 31
        result.Should().Contain("M"); // Latitude band M at equator
    }

    [Fact]
    public void ToMGRS_ShouldHandleNorthPole()
    {
        // Arrange
        double lon = 0;
        double lat = 84; // Near North Pole (MGRS stops at 84°N)

        // Act
        var result = CoordinateConverter.ToMGRS(lon, lat);

        // Assert
        result.Should().StartWith("31"); // Zone 31
        result.Should().Contain("X"); // Latitude band X (72-84°N)
    }

    [Fact]
    public void ToMGRS_ShouldHandleSouthernLatitudes()
    {
        // Arrange
        double lon = 0;
        double lat = -80; // Near South Pole

        // Act
        var result = CoordinateConverter.ToMGRS(lon, lat);

        // Assert
        result.Should().StartWith("31"); // Zone 31
        result.Should().Contain("C"); // Latitude band C (-80 to -72)
    }

    #endregion

    #region USNG Tests

    [Fact]
    public void ToUSNG_ShouldBeSameAsMGRS()
    {
        // Arrange
        double lon = -122.4194;
        double lat = 37.7749;

        // Act
        var mgrs = CoordinateConverter.ToMGRS(lon, lat);
        var usng = CoordinateConverter.ToUSNG(lon, lat);

        // Assert
        usng.Should().Be(mgrs);
    }

    #endregion

    #region Format Method Tests

    [Theory]
    [InlineData(CoordinateFormat.DecimalDegrees)]
    [InlineData(CoordinateFormat.DegreesDecimalMinutes)]
    [InlineData(CoordinateFormat.DegreesMinutesSeconds)]
    [InlineData(CoordinateFormat.UTM)]
    [InlineData(CoordinateFormat.MGRS)]
    [InlineData(CoordinateFormat.USNG)]
    public void Format_ShouldHandleAllFormats(CoordinateFormat format)
    {
        // Arrange
        double lon = -122.4194;
        double lat = 37.7749;

        // Act
        var result = CoordinateConverter.Format(lon, lat, format, 6);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Format_ShouldDefaultToDecimalDegrees_ForInvalidFormat()
    {
        // Arrange
        double lon = -122.4194;
        double lat = 37.7749;

        // Act
        var result = CoordinateConverter.Format(lon, lat, (CoordinateFormat)999, 6);

        // Assert
        result.Should().Contain("°N");
        result.Should().Contain("°W");
    }

    #endregion

    #region Format Name and Description Tests

    [Theory]
    [InlineData(CoordinateFormat.DecimalDegrees, "DD")]
    [InlineData(CoordinateFormat.DegreesDecimalMinutes, "DDM")]
    [InlineData(CoordinateFormat.DegreesMinutesSeconds, "DMS")]
    [InlineData(CoordinateFormat.UTM, "UTM")]
    [InlineData(CoordinateFormat.MGRS, "MGRS")]
    [InlineData(CoordinateFormat.USNG, "USNG")]
    public void GetFormatName_ShouldReturnShortName(CoordinateFormat format, string expected)
    {
        // Act
        var result = CoordinateConverter.GetFormatName(format);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(CoordinateFormat.DecimalDegrees, "Decimal Degrees")]
    [InlineData(CoordinateFormat.DegreesDecimalMinutes, "Degrees Decimal Minutes")]
    [InlineData(CoordinateFormat.DegreesMinutesSeconds, "Degrees Minutes Seconds")]
    [InlineData(CoordinateFormat.UTM, "Universal Transverse Mercator")]
    [InlineData(CoordinateFormat.MGRS, "Military Grid Reference System")]
    [InlineData(CoordinateFormat.USNG, "United States National Grid")]
    public void GetFormatDescription_ShouldReturnFullDescription(CoordinateFormat format, string expected)
    {
        // Act
        var result = CoordinateConverter.GetFormatDescription(format);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Scale Formatting Tests

    [Theory]
    [InlineData(25000, "1:25,000")]
    [InlineData(100000, "1:100,000")]
    [InlineData(1000, "1:1,000")]
    [InlineData(1000000, "1:1,000,000")]
    public void FormatScale_ShouldFormatCorrectly(double scale, string expected)
    {
        // Act
        var result = CoordinateConverter.FormatScale(scale);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Elevation Formatting Tests

    [Theory]
    [InlineData(100, MeasurementUnit.Metric, "100 m")]
    [InlineData(100, MeasurementUnit.Imperial, "328 ft")]
    [InlineData(100, MeasurementUnit.Nautical, "100 m")] // Nautical uses metric for elevation
    public void FormatElevation_ShouldHandleUnits(double elevation, MeasurementUnit unit, string expected)
    {
        // Act
        var result = CoordinateConverter.FormatElevation(elevation, unit);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, MeasurementUnit.Metric, "0 m")]
    [InlineData(1000, MeasurementUnit.Metric, "1000 m")]
    [InlineData(-100, MeasurementUnit.Metric, "-100 m")]
    public void FormatElevation_ShouldHandleSpecialValues(double elevation, MeasurementUnit unit, string expected)
    {
        // Act
        var result = CoordinateConverter.FormatElevation(elevation, unit);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Bearing Formatting Tests

    [Theory]
    [InlineData(0, "0.0°")]
    [InlineData(45, "45.0°")]
    [InlineData(90, "90.0°")]
    [InlineData(180, "180.0°")]
    [InlineData(270, "270.0°")]
    [InlineData(359.9, "359.9°")]
    public void FormatBearing_ShouldFormatCorrectly(double bearing, string expected)
    {
        // Act
        var result = CoordinateConverter.FormatBearing(bearing);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(360, "0.0°")] // Should normalize to 0
    [InlineData(720, "0.0°")] // Should normalize to 0
    [InlineData(-90, "270.0°")] // Should normalize negative bearings
    [InlineData(-180, "180.0°")]
    public void FormatBearing_ShouldNormalizeBearing(double bearing, string expected)
    {
        // Act
        var result = CoordinateConverter.FormatBearing(bearing);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ToDecimalDegrees_ShouldHandleNorthPole()
    {
        // Arrange
        double lon = 0;
        double lat = 90;

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 6);

        // Assert
        result.Should().Contain("90.000000°N");
    }

    [Fact]
    public void ToDecimalDegrees_ShouldHandleSouthPole()
    {
        // Arrange
        double lon = 0;
        double lat = -90;

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 6);

        // Assert
        result.Should().Contain("90.000000°S");
    }

    [Fact]
    public void ToDecimalDegrees_ShouldHandleInternationalDateLine()
    {
        // Arrange - Test both sides of the dateline
        double lon1 = 180;
        double lon2 = -180;
        double lat = 0;

        // Act
        var result1 = CoordinateConverter.ToDecimalDegrees(lon1, lat, 6);
        var result2 = CoordinateConverter.ToDecimalDegrees(lon2, lat, 6);

        // Assert
        result1.Should().Contain("180.000000°E");
        result2.Should().Contain("180.000000°W");
    }

    [Fact]
    public void ToDecimalDegrees_ShouldHandlePrimeMeridian()
    {
        // Arrange
        double lon = 0;
        double lat = 51.4778; // Greenwich

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 6);

        // Assert
        result.Should().Contain("0.000000°E");
    }

    [Fact]
    public void ToDecimalDegrees_ShouldHandleEquator()
    {
        // Arrange
        double lon = -78.4678;
        double lat = 0;

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 6);

        // Assert
        result.Should().Contain("0.000000°N");
    }

    #endregion

    #region Precision Edge Cases

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(10)]
    public void ToDecimalDegrees_ShouldHandleVariousPrecisions(int precision)
    {
        // Arrange
        double lon = -122.4194155;
        double lat = 37.7749295;

        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, precision);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("°N");
        result.Should().Contain("°W");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void ToDegreesDecimalMinutes_ShouldHandleVariousPrecisions(int precision)
    {
        // Arrange
        double lon = -122.4194155;
        double lat = 37.7749295;

        // Act
        var result = CoordinateConverter.ToDegreesDecimalMinutes(lon, lat, precision);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("'N");
        result.Should().Contain("'W");
    }

    #endregion

    #region Real World Coordinate Tests

    [Theory]
    [InlineData(-122.4194, 37.7749, "San Francisco")]
    [InlineData(-118.2437, 34.0522, "Los Angeles")]
    [InlineData(-73.9352, 40.7306, "New York")]
    [InlineData(2.3522, 48.8566, "Paris")]
    [InlineData(139.6917, 35.6895, "Tokyo")]
    [InlineData(151.2093, -33.8688, "Sydney")]
    public void Format_ShouldHandleRealWorldCoordinates(double lon, double lat, string city)
    {
        // Act
        var dd = CoordinateConverter.Format(lon, lat, CoordinateFormat.DecimalDegrees, 6);
        var ddm = CoordinateConverter.Format(lon, lat, CoordinateFormat.DegreesDecimalMinutes, 3);
        var dms = CoordinateConverter.Format(lon, lat, CoordinateFormat.DegreesMinutesSeconds, 1);
        var utm = CoordinateConverter.Format(lon, lat, CoordinateFormat.UTM, 6);
        var mgrs = CoordinateConverter.Format(lon, lat, CoordinateFormat.MGRS, 6);

        // Assert
        dd.Should().NotBeNullOrEmpty($"DD format should work for {city}");
        ddm.Should().NotBeNullOrEmpty($"DDM format should work for {city}");
        dms.Should().NotBeNullOrEmpty($"DMS format should work for {city}");
        utm.Should().NotBeNullOrEmpty($"UTM format should work for {city}");
        mgrs.Should().NotBeNullOrEmpty($"MGRS format should work for {city}");
    }

    #endregion

    #region Hemisphere Indicator Tests

    [Theory]
    [InlineData(-122, 37, "N", "W")]  // Northwest
    [InlineData(122, 37, "N", "E")]   // Northeast
    [InlineData(-122, -37, "S", "W")] // Southwest
    [InlineData(122, -37, "S", "E")]  // Southeast
    public void ToDecimalDegrees_ShouldUseCorrectHemisphereIndicators(
        double lon, double lat, string expectedLatDir, string expectedLonDir)
    {
        // Act
        var result = CoordinateConverter.ToDecimalDegrees(lon, lat, 6);

        // Assert
        result.Should().Contain($"°{expectedLatDir}");
        result.Should().Contain($"°{expectedLonDir}");
    }

    #endregion

    #region Very Small and Large Values

    [Theory]
    [InlineData(0.000001, 0.000001)]
    [InlineData(0.0000001, 0.0000001)]
    [InlineData(179.999999, 89.999999)]
    public void Format_ShouldHandleVerySmallAndLargeValues(double lon, double lat)
    {
        // Act
        var result = CoordinateConverter.Format(lon, lat, CoordinateFormat.DecimalDegrees, 8);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region UTM Zone Boundary Tests

    [Theory]
    [InlineData(-180, 0, 1)]  // Zone 1 starts at 180°W
    [InlineData(-174, 0, 1)]  // Still zone 1
    [InlineData(-168, 0, 2)]  // Zone 2 starts at 174°W
    [InlineData(-6, 0, 30)]   // Zone 30
    [InlineData(0, 0, 31)]    // Zone 31 at prime meridian
    [InlineData(6, 0, 32)]    // Zone 32
    [InlineData(174, 0, 60)]  // Zone 60
    [InlineData(179, 0, 60)]  // Still zone 60
    public void ToUTM_ShouldCalculateCorrectZonesAtBoundaries(double lon, double lat, int expectedZone)
    {
        // Act
        var result = CoordinateConverter.ToUTM(lon, lat);

        // Assert
        result.Should().StartWith($"{expectedZone}");
    }

    #endregion
}
