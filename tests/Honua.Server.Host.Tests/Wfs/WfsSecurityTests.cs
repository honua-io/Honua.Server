using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Host.Wfs;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Tests for WFS security features including DoS protection and query limits.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "WFS")]
[Trait("Feature", "Security")]
[Trait("Speed", "Fast")]
public class WfsSecurityTests
{
    [Fact]
    public void ParseCount_WithinLimit_ReturnsSuccess()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "5000"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5000);
    }

    [Fact]
    public void ParseCount_ExceedsMaxFeatures_ReturnsFailure()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "999999999"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("999999999");
        result.Error.Message.Should().Contain("10000");
        result.Error.Message.Should().Contain("exceeds the maximum allowed");
    }

    [Fact]
    public void ParseCount_AtMaximumLimit_ReturnsSuccess()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "10000"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10_000);
    }

    [Fact]
    public void ParseCount_JustOverMaximumLimit_ReturnsFailure()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "10001"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("10001");
        result.Error.Message.Should().Contain("10000");
    }

    [Fact]
    public void ParseCount_NotSpecified_ReturnsDefaultCount()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 250 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(250);
    }

    [Fact]
    public void ParseCount_InvalidValue_ReturnsFailure()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "not-a-number"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void ParseCount_NegativeValue_ReturnsFailure()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "-100"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void ParseCount_Zero_ReturnsFailure()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "0"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(9999)]
    public void ParseCount_ValidValuesWithinLimit_ReturnsSuccess(int count)
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = count.ToString()
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(count);
    }

    [Theory]
    [InlineData(10_001)]
    [InlineData(50_000)]
    [InlineData(100_000)]
    [InlineData(999_999_999)]
    public void ParseCount_ValuesExceedingLimit_ReturnsFailure(int count)
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 10_000, DefaultCount = 100 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = count.ToString()
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain(count.ToString());
        result.Error.Message.Should().Contain("10000");
    }

    [Fact]
    public void WfsOptions_DefaultValues_AreSecure()
    {
        // Arrange & Act
        var options = new WfsOptions();

        // Assert
        options.MaxFeatures.Should().Be(10_000, "default MaxFeatures should prevent DoS");
        options.DefaultCount.Should().Be(100, "default DefaultCount should be reasonable");
        options.MaxFeatures.Should().BeLessThan(100_000, "MaxFeatures should have reasonable upper bound");
    }

    [Fact]
    public void WfsOptions_CustomMaxFeatures_IsRespected()
    {
        // Arrange
        var options = new WfsOptions { MaxFeatures = 5_000, DefaultCount = 50 };
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["count"] = "6000"
        });

        // Act
        var result = WfsHelpers.ParseCount(query, options);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("5000");
    }
}
