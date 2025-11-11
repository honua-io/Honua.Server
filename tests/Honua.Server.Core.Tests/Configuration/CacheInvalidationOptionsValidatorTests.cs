// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration;

public class CacheInvalidationOptionsValidatorTests
{
    private readonly CacheInvalidationOptionsValidator _validator;

    public CacheInvalidationOptionsValidatorTests()
    {
        _validator = new CacheInvalidationOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            RetryCount = 3,
            RetryDelayMs = 100,
            MaxRetryDelayMs = 5000,
            Strategy = CacheInvalidationStrategy.Strict,
            HealthCheckSampleSize = 100,
            MaxDriftPercentage = 1.0,
            ShortTtl = TimeSpan.FromSeconds(30),
            OperationTimeout = TimeSpan.FromSeconds(10)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeRetryCount_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            RetryCount = -1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RetryCount").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithExcessiveRetryCount_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            RetryCount = 11
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RetryCount").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithInvalidRetryDelay_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            RetryDelayMs = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RetryDelayMs").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithMaxDelayLessThanDelay_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            RetryDelayMs = 1000,
            MaxRetryDelayMs = 500 // Less than RetryDelayMs
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxRetryDelayMs").And.Contain("greater than or equal to");
    }

    [Fact]
    public void Validate_WithInvalidHealthCheckSampleSize_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            HealthCheckSampleSize = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HealthCheckSampleSize").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveHealthCheckSampleSize_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            HealthCheckSampleSize = 10001
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HealthCheckSampleSize").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithNegativeDriftPercentage_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            MaxDriftPercentage = -1.0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxDriftPercentage").And.Contain("negative");
    }

    [Fact]
    public void Validate_WithDriftPercentageOver100_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            MaxDriftPercentage = 101.0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxDriftPercentage").And.Contain("exceeds 100%");
    }

    [Fact]
    public void Validate_WithNegativeShortTtl_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            ShortTtl = TimeSpan.FromSeconds(-1)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ShortTtl").And.Contain("positive");
    }

    [Fact]
    public void Validate_WithExcessiveShortTtl_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            ShortTtl = TimeSpan.FromHours(2) // Exceeds 1 hour
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ShortTtl").And.Contain("exceeds");
    }

    [Fact]
    public void Validate_WithNegativeOperationTimeout_ReturnsFail()
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            OperationTimeout = TimeSpan.FromSeconds(-1)
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("OperationTimeout").And.Contain("positive");
    }

    [Theory]
    [InlineData(CacheInvalidationStrategy.Strict)]
    [InlineData(CacheInvalidationStrategy.Eventual)]
    [InlineData(CacheInvalidationStrategy.ShortTTL)]
    public void Validate_WithValidStrategies_ReturnsSuccess(CacheInvalidationStrategy strategy)
    {
        // Arrange
        var options = new CacheInvalidationOptions
        {
            Strategy = strategy
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }
}
