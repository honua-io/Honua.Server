using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

public sealed class SridValidatorTests
{
    [Theory]
    [InlineData(0)] // SRID 0 means no SRID - should be valid
    [InlineData(4326)] // WGS 84 - most common
    [InlineData(3857)] // Web Mercator
    [InlineData(4269)] // NAD83
    [InlineData(32610)] // UTM zone 10N
    [InlineData(32750)] // UTM zone 50S
    [InlineData(2154)] // RGF93 / Lambert-93 (France)
    [InlineData(27700)] // British National Grid
    public void IsValid_ValidSrid_ReturnsTrue(int srid)
    {
        // Act
        var result = SridValidator.IsValid(srid);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(-1)] // Negative SRID
    [InlineData(9999999)] // Invalid high value
    [InlineData(1234)] // Random unsupported SRID
    [InlineData(12345)] // Random unsupported SRID
    public void IsValid_InvalidSrid_ReturnsFalse(int srid)
    {
        // Act
        var result = SridValidator.IsValid(srid);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(4326)]
    [InlineData(4269)]
    [InlineData(4258)]
    public void IsGeographic_GeographicSrid_ReturnsTrue(int srid)
    {
        // Act
        var result = SridValidator.IsGeographic(srid);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(32610)]
    [InlineData(2154)]
    [InlineData(27700)]
    public void IsProjected_ProjectedSrid_ReturnsTrue(int srid)
    {
        // Act
        var result = SridValidator.IsProjected(srid);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Validate_ValidSrid_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => SridValidator.Validate(4326));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_InvalidSrid_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SridValidator.Validate(9999999));
    }

    [Theory]
    [InlineData(4326, -100.0, 45.0, true)] // Valid WGS84 coordinates (lon, lat)
    [InlineData(4326, -180.0, -90.0, true)] // Min bounds
    [InlineData(4326, 180.0, 90.0, true)] // Max bounds
    [InlineData(4326, -181.0, 45.0, false)] // Longitude out of range
    [InlineData(4326, 100.0, 91.0, false)] // Latitude out of range
    [InlineData(4326, double.NaN, 45.0, false)] // NaN longitude
    [InlineData(4326, 100.0, double.PositiveInfinity, false)] // Infinity latitude
    public void AreCoordinatesValid_VariousCoordinates_ReturnsExpectedResult(int srid, double x, double y, bool expected)
    {
        // Act
        var result = SridValidator.AreCoordinatesValid(srid, x, y);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(32610, 500000.0, 4000000.0, true)] // Valid UTM coordinates
    [InlineData(32610, 25000000.0, 100.0, false)] // X out of reasonable range
    [InlineData(32610, 100.0, 25000000.0, false)] // Y out of reasonable range
    public void AreCoordinatesValid_ProjectedCoordinates_ReturnsExpectedResult(int srid, double x, double y, bool expected)
    {
        // Act
        var result = SridValidator.AreCoordinatesValid(srid, x, y);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryValidate_ValidSrid_ReturnsTrueWithNoError()
    {
        // Act
        var result = SridValidator.TryValidate(4326, out var errorMessage);

        // Assert
        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidate_InvalidSrid_ReturnsFalseWithError()
    {
        // Act
        var result = SridValidator.TryValidate(9999999, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("not supported", errorMessage);
    }

    [Theory]
    [InlineData(0, "Unspecified")]
    [InlineData(4326, "WGS 84")]
    [InlineData(3857, "Mercator")]
    [InlineData(32610, "UTM zone 10N")]
    [InlineData(32750, "UTM zone 50S")]
    public void GetDescription_VariousSrids_ReturnsDescription(int srid, string expectedSubstring)
    {
        // Act
        var description = SridValidator.GetDescription(srid);

        // Assert
        Assert.Contains(expectedSubstring, description, StringComparison.OrdinalIgnoreCase);
    }
}
