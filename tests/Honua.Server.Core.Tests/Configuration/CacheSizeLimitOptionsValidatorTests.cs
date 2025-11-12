// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Configuration;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration;

public class CacheSizeLimitOptionsValidatorTests
{
    private readonly CacheSizeLimitOptionsValidator _validator;

    public CacheSizeLimitOptionsValidatorTests()
    {
        _validator = new CacheSizeLimitOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = 100,
            MaxTotalEntries = 10_000,
            EnableAutoCompaction = true,
            ExpirationScanFrequencyMinutes = 1.0,
            EnableMetrics = true,
            CompactionPercentage = 0.25
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeMaxSize_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTotalSizeMB").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithExcessiveMaxSize_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = 10001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTotalSizeMB").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithZeroMaxSize_ReturnsSuccess()
    {
        // Arrange (0 means unlimited)
        var options = new CacheSizeLimitOptions
        {
            MaxTotalSizeMB = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeMaxEntries_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalEntries = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTotalEntries").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithExcessiveMaxEntries_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            MaxTotalEntries = 1_000_001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxTotalEntries").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidExpirationScanFrequency_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            ExpirationScanFrequencyMinutes = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ExpirationScanFrequencyMinutes").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithTooFrequentScanFrequency_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            ExpirationScanFrequencyMinutes = 0.4 // Less than 0.5 minutes
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ExpirationScanFrequencyMinutes").And.Contain("below minimum");
    }

    [Fact]
    public void Validate_WithTooInfrequentScanFrequency_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            ExpirationScanFrequencyMinutes = 61 // Greater than 60 minutes
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ExpirationScanFrequencyMinutes").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidCompactionPercentage_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            CompactionPercentage = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CompactionPercentage").And.Contain("between");
    }

    [Fact]
    public void Validate_WithCompactionPercentageTooLow_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            CompactionPercentage = 0.05 // Less than 0.1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CompactionPercentage").And.Contain("below recommended minimum");
    }

    [Fact]
    public void Validate_WithCompactionPercentageTooHigh_ReturnsFail()
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            CompactionPercentage = 0.6 // Greater than 0.5
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CompactionPercentage").And.Contain("exceeds recommended maximum");
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void Validate_WithValidCompactionPercentages_ReturnsSuccess(double compactionPercentage)
    {
        // Arrange
        var options = new CacheSizeLimitOptions
        {
            CompactionPercentage = compactionPercentage
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
