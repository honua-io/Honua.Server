using Honua.Server.Host.Wcs;
using Xunit;

namespace Honua.Server.Host.Tests.Wcs;

public class WcsRangeSubsettingExtensionTests
{
    [Fact]
    public void TryParseRangeSubset_WithNull_ReturnsAllBands()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset(null, 3, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2 }, bandIndices);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithEmpty_ReturnsAllBands()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, bandIndices);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithSingleIndex_ReturnsOneBand()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("1", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0 }, bandIndices); // 1-based input converts to 0-based index
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithMultipleIndices_ReturnsMultipleBands()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("1,3,5", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 2, 4 }, bandIndices); // 1-based converted to 0-based
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithInterval_ReturnsRange()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("0:2", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2 }, bandIndices);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithBandNames_ReturnsCorrectBands()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("Band1,Band3", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 2 }, bandIndices); // Band1=index 0, Band3=index 2
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithMixedFormats_ReturnsCorrectBands()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("Band1,0:1,3", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2 }, bandIndices); // Band1=0, 0:1=0,1, 3=2 -> deduplicated and sorted
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithDuplicates_ReturnsDeduplicated()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("1,1,1", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0 }, bandIndices); // Deduplicated
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithOutOfBoundsIndex_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("10", 3, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("out of bounds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseRangeSubset_WithNegativeIndex_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("-1", 5, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("out of bounds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseRangeSubset_WithInvalidInterval_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("5:2", 5, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("Invalid range interval", error);
    }

    [Fact]
    public void TryParseRangeSubset_WithIntervalOutOfBounds_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("0:10", 5, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("out of bounds", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseRangeSubset_WithZeroBands_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("Band1", 0, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("no bands", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParseRangeSubset_WithInvalidFormat_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("invalid!@#", 5, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("Invalid range subset component", error);
    }

    [Fact]
    public void TryParseRangeSubset_WithWhitespace_IgnoresWhitespace()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset(" 1 , 2 , 3 ", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2 }, bandIndices);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("Band1", 0)]
    [InlineData("Band5", 4)]
    [InlineData("band10", 9)]
    public void TryParseRangeSubset_WithBandName_ParsesCorrectly(string bandName, int expectedIndex)
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset(bandName, 10, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { expectedIndex }, bandIndices);
        Assert.Null(error);
    }

    [Fact]
    public void TryParseRangeSubset_WithUnknownBandName_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("RedChannel", 5, out var bandIndices, out var error);

        // Assert
        Assert.False(result);
        Assert.Empty(bandIndices);
        Assert.NotNull(error);
        Assert.Contains("Cannot resolve band name", error);
    }

    [Fact]
    public void IsValidRangeSubset_WithValidInput_ReturnsTrue()
    {
        // Act
        var result = WcsRangeSubsettingHelper.IsValidRangeSubset("1,2,3", 5, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void IsValidRangeSubset_WithInvalidInput_ReturnsFalse()
    {
        // Act
        var result = WcsRangeSubsettingHelper.IsValidRangeSubset("10", 3, out var error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void FormatBandList_ReturnsFormattedString()
    {
        // Act
        var formatted = WcsRangeSubsettingHelper.FormatBandList(new[] { 0, 2, 4 });

        // Assert
        Assert.Contains("Band1", formatted);
        Assert.Contains("Band3", formatted);
        Assert.Contains("Band5", formatted);
        Assert.Contains("index:0", formatted);
        Assert.Contains("index:2", formatted);
        Assert.Contains("index:4", formatted);
    }

    [Fact]
    public void TryParseRangeSubset_WithZeroBasedIndices_ParsesCorrectly()
    {
        // Act - using 0-based indices directly
        var result = WcsRangeSubsettingHelper.TryParseRangeSubset("0,1,2", 5, out var bandIndices, out var error);

        // Assert
        Assert.True(result);
        Assert.Equal(new[] { 0, 1, 2 }, bandIndices);
        Assert.Null(error);
    }
}
