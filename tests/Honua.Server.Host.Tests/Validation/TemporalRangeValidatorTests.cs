using Honua.Server.Host.Validation;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

public sealed class TemporalRangeValidatorTests
{
    [Fact]
    public void IsDateInBounds_ValidDate_ReturnsTrue()
    {
        // Arrange
        var date = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.IsDateInBounds(date);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(1899, 12, 31)] // Before minimum
    [InlineData(2101, 1, 1)]   // After maximum
    public void IsDateInBounds_OutOfBoundsDate_ReturnsFalse(int year, int month, int day)
    {
        // Arrange
        var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.IsDateInBounds(date);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDateValid_PastDate_ReturnsTrue()
    {
        // Arrange
        var date = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.IsDateValid(date, allowFuture: false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDateValid_FutureDate_AllowFutureFalse_ReturnsFalse()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddYears(1);

        // Act
        var result = TemporalRangeValidator.IsDateValid(futureDate, allowFuture: false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDateValid_FutureDate_AllowFutureTrue_ReturnsTrue()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddYears(1);

        // Act
        var result = TemporalRangeValidator.IsDateValid(futureDate, allowFuture: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRangeValid_ValidRange_ReturnsTrue()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.IsRangeValid(start, end);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRangeValid_StartAfterEnd_ReturnsFalse()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.IsRangeValid(start, end);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsRangeValid_NullDates_ReturnsTrue()
    {
        // Act
        var result = TemporalRangeValidator.IsRangeValid(null, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRangeValid_ExceedsMaximumTimeSpan_ReturnsFalse()
    {
        // Arrange
        var start = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2100, 12, 31, 0, 0, 0, TimeSpan.Zero); // More than 100 years

        // Act
        var result = TemporalRangeValidator.IsRangeValid(start, end);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_ValidRange_DoesNotThrow()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Act & Assert
        var exception = Record.Exception(() =>
            TemporalRangeValidator.Validate(start, end));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_StartAfterEnd_ThrowsArgumentException()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            TemporalRangeValidator.Validate(start, end));
    }

    [Fact]
    public void Validate_FutureDate_AllowFutureFalse_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var futureDate = DateTimeOffset.UtcNow.AddYears(1);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemporalRangeValidator.Validate(futureDate, null, allowFuture: false));
    }

    [Fact]
    public void Validate_DateOutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var tooEarly = new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemporalRangeValidator.Validate(tooEarly, null));
    }

    [Fact]
    public void TryValidate_ValidRange_ReturnsTrueWithNoError()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.TryValidate(start, end, allowFuture: false, out var errorMessage);

        // Assert
        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidate_InvalidRange_ReturnsFalseWithError()
    {
        // Arrange
        var start = new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = TemporalRangeValidator.TryValidate(start, end, allowFuture: false, out var errorMessage);

        // Assert
        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("before", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidTemporalDateAttribute_ValidDate_Succeeds()
    {
        // Arrange
        var attribute = new ValidTemporalDateAttribute(allowFuture: false);
        var validDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = attribute.IsValid(validDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidTemporalDateAttribute_FutureDate_AllowFutureFalse_Fails()
    {
        // Arrange
        var attribute = new ValidTemporalDateAttribute(allowFuture: false);
        var futureDate = DateTimeOffset.UtcNow.AddYears(1);

        // Act
        var result = attribute.IsValid(futureDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidTemporalDateAttribute_NullValue_Succeeds()
    {
        // Arrange
        var attribute = new ValidTemporalDateAttribute();

        // Act
        var result = attribute.IsValid(null);

        // Assert
        Assert.True(result);
    }
}
