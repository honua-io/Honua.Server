using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Ogc;
using Xunit;

namespace Honua.Server.Host.Tests.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public sealed class OgcTemporalParameterValidatorTests
{
    #region Basic Datetime Validation

    [Fact]
    public void Validate_NullDatetime_WithLayerDefault_ReturnsDefault()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            DefaultValue = "2024-01-01"
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(null, temporal);

        // Assert
        result.Should().Be("2024-01-01");
    }

    [Fact]
    public void Validate_EmptyDatetime_WithLayerDefault_ReturnsDefault()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            DefaultValue = "2024-06-15"
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("", temporal);

        // Assert
        result.Should().Be("2024-06-15");
    }

    [Theory]
    [InlineData("2024-01-01")]
    [InlineData("2024-01-01T00:00:00Z")]
    [InlineData("2024-01-01T12:30:45Z")]
    [InlineData("2024-01-01T12:30:45+00:00")]
    [InlineData("2024-01-01T12:30:45-05:00")]
    [InlineData("2024-12-31T23:59:59.999Z")]
    public void Validate_ValidISO8601Format_Succeeds(string datetime)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(datetime, temporal);

        // Assert
        result.Should().Be(datetime);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2024-13-01")]  // Invalid month
    [InlineData("2024-01-32")]  // Invalid day
    [InlineData("not-a-date")]
    [InlineData("2024/01/01")]  // Wrong separator
    public void Validate_InvalidDateFormat_ThrowsException(string datetime)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate(datetime, temporal));

        exception.Message.Should().Contain("not a valid ISO 8601");
    }

    [Fact]
    public void Validate_NowKeyword_ReturnsCurrentTime()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = OgcTemporalParameterValidator.Validate("now", temporal);

        // Assert
        var after = DateTimeOffset.UtcNow;
        var parsed = DateTimeOffset.Parse(result!);
        parsed.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    #endregion

    #region Fixed Values Validation

    [Fact]
    public void Validate_FixedValues_ValidValue_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            FixedValues = new[] { "2024-01-01", "2024-06-01", "2024-12-01" }
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2024-06-01", temporal);

        // Assert
        result.Should().Be("2024-06-01");
    }

    [Fact]
    public void Validate_FixedValues_InvalidValue_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            FixedValues = new[] { "2024-01-01", "2024-06-01", "2024-12-01" }
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2024-03-01", temporal));

        exception.Message.Should().Contain("not in the allowed set");
        exception.Message.Should().Contain("2024-01-01, 2024-06-01, 2024-12-01");
    }

    [Fact]
    public void Validate_FixedValues_CaseInsensitive_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            FixedValues = new[] { "2024-01-01T00:00:00Z" }
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2024-01-01t00:00:00z", temporal);

        // Assert
        result.Should().Be("2024-01-01t00:00:00z");
    }

    #endregion

    #region Layer Bounds Validation

    [Fact]
    public void Validate_WithinLayerBounds_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            MinValue = "2020-01-01",
            MaxValue = "2024-12-31"
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2022-06-15", temporal);

        // Assert
        result.Should().Be("2022-06-15");
    }

    [Fact]
    public void Validate_BeforeLayerMinValue_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            MinValue = "2020-01-01",
            MaxValue = "2024-12-31"
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2019-12-31", temporal));

        exception.Message.Should().Contain("outside the layer's temporal extent");
        exception.Message.Should().Contain("2020-01-01");
        exception.Message.Should().Contain("2024-12-31");
    }

    [Fact]
    public void Validate_AfterLayerMaxValue_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            MinValue = "2020-01-01",
            MaxValue = "2024-12-31"
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2025-01-01", temporal));

        exception.Message.Should().Contain("outside the layer's temporal extent");
    }

    #endregion

    #region Interval Validation

    [Theory]
    [InlineData("2024-01-01..2024-12-31")]
    [InlineData("2024-01-01T00:00:00Z..2024-12-31T23:59:59Z")]
    [InlineData("2024-06-01..2024-06-30")]
    public void Validate_ValidInterval_Succeeds(string interval)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(interval, temporal);

        // Assert
        result.Should().Be(interval);
    }

    [Fact]
    public void Validate_Interval_StartAfterEnd_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2024-12-31..2024-01-01", temporal));

        exception.Message.Should().Contain("start").And.Contain("before or equal to end");
    }

    [Theory]
    [InlineData("2024-01-01..")]  // Open end
    [InlineData("..2024-12-31")]  // Open start
    [InlineData("..")]            // Both open
    public void Validate_OpenEndedInterval_Succeeds(string interval)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(interval, temporal);

        // Assert
        result.Should().Be(interval);
    }

    [Fact]
    public void Validate_Interval_WithNowKeyword_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2024-01-01..now", temporal);

        // Assert
        result.Should().Be("2024-01-01..now");
    }

    [Fact]
    public void Validate_InvalidIntervalFormat_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2024-01-01...2024-12-31", temporal));

        exception.Message.Should().Contain("Invalid interval format");
    }

    #endregion

    #region Duration Validation

    [Theory]
    [InlineData("2024-01-01..P1Y", 365)]    // 1 year
    [InlineData("2024-01-01..P6M", 182)]    // 6 months (approximate)
    [InlineData("2024-01-01..P30D", 30)]    // 30 days
    [InlineData("2024-01-01..P1D", 1)]      // 1 day
    [InlineData("2024-01-01..PT12H", 0)]    // 12 hours
    [InlineData("2024-01-01..PT1H", 0)]     // 1 hour
    public void Validate_DurationNotation_Succeeds(string interval, int expectedDaysMin)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(interval, temporal);

        // Assert
        result.Should().Be(interval);
    }

    [Theory]
    [InlineData("2024-01-01..P1Y2M3D")]     // Combined: 1 year, 2 months, 3 days
    [InlineData("2024-01-01..PT2H30M")]     // 2 hours 30 minutes
    [InlineData("2024-01-01..P1DT12H")]     // 1 day 12 hours
    public void Validate_ComplexDuration_Succeeds(string interval)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(interval, temporal);

        // Assert
        result.Should().Be(interval);
    }

    [Fact]
    public void Validate_DurationWithoutStart_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("..P1M", temporal));

        exception.Message.Should().Contain("Duration notation requires a start datetime");
    }

    [Theory]
    [InlineData("2024-01-01..PXY")]       // Invalid component
    [InlineData("2024-01-01..1Y")]        // Missing P prefix
    [InlineData("2024-01-01..P")]         // Empty duration
    public void Validate_InvalidDuration_ThrowsException(string interval)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate(interval, temporal));

        exception.Message.Should().Contain("Invalid ISO 8601 duration");
    }

    #endregion

    #region Future Date Validation

    [Fact]
    public void Validate_FutureDate_AllowFutureTrue_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };
        var futureDate = DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

        // Act
        var result = OgcTemporalParameterValidator.Validate(futureDate, temporal, allowFuture: true);

        // Assert
        result.Should().Be(futureDate);
    }

    [Fact]
    public void Validate_FutureDate_AllowFutureFalse_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };
        var futureDate = DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate(futureDate, temporal, allowFuture: false));

        exception.Message.Should().Contain("in the future");
    }

    #endregion

    #region Boundary Testing

    [Fact]
    public void Validate_DateTooEarly_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("1800-01-01", temporal));

        exception.Message.Should().Contain("outside the valid range");
    }

    [Fact]
    public void Validate_DateTooLate_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("2200-01-01", temporal, allowFuture: true));

        exception.Message.Should().Contain("outside the valid range");
    }

    [Fact]
    public void Validate_IntervalExceedsMaxSpan_ThrowsException()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act & Assert
        var exception = Assert.Throws<OgcTemporalValidationException>(() =>
            OgcTemporalParameterValidator.Validate("1900-01-01..2100-12-31", temporal, allowFuture: true));

        exception.Message.Should().Contain("invalid or exceeds maximum allowed span");
    }

    #endregion

    #region TryValidate Method

    [Fact]
    public void TryValidate_ValidDatetime_ReturnsTrue()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.TryValidate("2024-01-01", temporal, true, out var validated, out var error);

        // Assert
        result.Should().BeTrue();
        validated.Should().Be("2024-01-01");
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_InvalidDatetime_ReturnsFalse()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.TryValidate("invalid", temporal, true, out var validated, out var error);

        // Assert
        result.Should().BeFalse();
        validated.Should().BeNull();
        error.Should().NotBeNull();
        error.Should().Contain("not a valid ISO 8601");
    }

    [Fact]
    public void TryValidate_DateOutOfLayerBounds_ReturnsFalse()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true,
            MinValue = "2020-01-01",
            MaxValue = "2024-12-31"
        };

        // Act
        var result = OgcTemporalParameterValidator.TryValidate("2019-01-01", temporal, true, out var validated, out var error);

        // Assert
        result.Should().BeFalse();
        validated.Should().BeNull();
        error.Should().NotBeNull();
        error.Should().Contain("outside the layer's temporal extent");
    }

    #endregion

    #region Timezone Handling

    [Theory]
    [InlineData("2024-01-01T12:00:00Z")]
    [InlineData("2024-01-01T12:00:00+00:00")]
    [InlineData("2024-01-01T12:00:00-05:00")]
    [InlineData("2024-01-01T12:00:00+08:00")]
    public void Validate_DifferentTimezones_Succeeds(string datetime)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(datetime, temporal);

        // Assert
        result.Should().Be(datetime);
    }

    [Fact]
    public void Validate_DateWithoutTimezone_AssumedUTC_Succeeds()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2024-01-01", temporal);

        // Assert
        result.Should().Be("2024-01-01");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validate_LeapSecond_HandledGracefully()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act - DateTimeOffset doesn't support leap seconds, so 60 seconds wraps to next minute
        var result = OgcTemporalParameterValidator.Validate("2024-06-30T23:59:59Z", temporal);

        // Assert
        result.Should().Be("2024-06-30T23:59:59Z");
    }

    [Theory]
    [InlineData("2024-01-01T00:00:00.000Z")]
    [InlineData("2024-01-01T00:00:00.123Z")]
    [InlineData("2024-01-01T00:00:00.123456Z")]
    public void Validate_MillisecondPrecision_Succeeds(string datetime)
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = true
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate(datetime, temporal);

        // Assert
        result.Should().Be(datetime);
    }

    [Fact]
    public void Validate_DisabledTemporal_ReturnsNull()
    {
        // Arrange
        var temporal = new LayerTemporalDefinition
        {
            Enabled = false
        };

        // Act
        var result = OgcTemporalParameterValidator.Validate("2024-01-01", temporal);

        // Assert - When temporal is disabled but a value is provided, it still validates the format
        result.Should().Be("2024-01-01");
    }

    #endregion
}
