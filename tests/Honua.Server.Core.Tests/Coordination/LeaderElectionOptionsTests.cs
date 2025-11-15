// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Coordination;
using Xunit;

namespace Honua.Server.Core.Tests.Coordination;

[Trait("Category", "Unit")]
public class LeaderElectionOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new LeaderElectionOptions();

        // Assert
        options.LeaseDurationSeconds.Should().Be(30);
        options.RenewalIntervalSeconds.Should().Be(10);
        options.ResourceName.Should().Be("honua-server");
        options.KeyPrefix.Should().Be("honua:leader:");
        options.EnableDetailedLogging.Should().BeFalse();
    }

    [Fact]
    public void LeaseDuration_ReturnsCorrectTimeSpan()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 45
        };

        // Act
        var duration = options.LeaseDuration;

        // Assert
        duration.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void RenewalInterval_ReturnsCorrectTimeSpan()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            RenewalIntervalSeconds = 15
        };

        // Act
        var interval = options.RenewalInterval;

        // Assert
        interval.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void GetRedisKey_ReturnsCorrectKey()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            KeyPrefix = "honua:leader:",
            ResourceName = "test-resource"
        };

        // Act
        var key = options.GetRedisKey();

        // Assert
        key.Should().Be("honua:leader:test-resource");
    }

    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test-resource",
            KeyPrefix = "honua:leader:"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithZeroLeaseDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 0
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LeaseDurationSeconds*greater than 0*");
    }

    [Fact]
    public void Validate_WithNegativeLeaseDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = -10
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LeaseDurationSeconds*greater than 0*");
    }

    [Fact]
    public void Validate_WithZeroRenewalInterval_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 0
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RenewalIntervalSeconds*greater than 0*");
    }

    [Fact]
    public void Validate_WithNegativeRenewalInterval_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = -5
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RenewalIntervalSeconds*greater than 0*");
    }

    [Fact]
    public void Validate_WithRenewalIntervalGreaterThanLeaseDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 10,
            RenewalIntervalSeconds = 15
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RenewalIntervalSeconds*must be less than*LeaseDurationSeconds*prevent leadership loss*");
    }

    [Fact]
    public void Validate_WithRenewalIntervalEqualToLeaseDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 30
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RenewalIntervalSeconds*must be less than*LeaseDurationSeconds*");
    }

    [Fact]
    public void Validate_WithRenewalIntervalGreaterThanHalfLeaseDuration_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 20
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RenewalIntervalSeconds*should be at most half of*LeaseDurationSeconds*reliable operation*");
    }

    [Fact]
    public void Validate_WithRenewalIntervalExactlyHalfLeaseDuration_DoesNotThrow()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 15,
            ResourceName = "test"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNullResourceName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = null!
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ResourceName*cannot be null or whitespace*");
    }

    [Fact]
    public void Validate_WithEmptyResourceName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = ""
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ResourceName*cannot be null or whitespace*");
    }

    [Fact]
    public void Validate_WithWhitespaceResourceName_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "   "
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ResourceName*cannot be null or whitespace*");
    }

    [Fact]
    public void Validate_WithNullKeyPrefix_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test",
            KeyPrefix = null!
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KeyPrefix*cannot be null or whitespace*");
    }

    [Fact]
    public void Validate_WithEmptyKeyPrefix_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test",
            KeyPrefix = ""
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KeyPrefix*cannot be null or whitespace*");
    }

    [Fact]
    public void Validate_WithWhitespaceKeyPrefix_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test",
            KeyPrefix = "   "
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KeyPrefix*cannot be null or whitespace*");
    }

    [Theory]
    [InlineData(30, 10)]  // Recommended 3:1 ratio
    [InlineData(60, 20)]  // Recommended 3:1 ratio
    [InlineData(30, 7)]   // Better than 3:1 ratio
    [InlineData(45, 15)]  // Exactly 3:1 ratio
    public void Validate_WithRecommendedRatios_DoesNotThrow(int leaseDuration, int renewalInterval)
    {
        // Arrange
        var options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = leaseDuration,
            RenewalIntervalSeconds = renewalInterval,
            ResourceName = "test"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SectionName_HasCorrectValue()
    {
        // Assert
        LeaderElectionOptions.SectionName.Should().Be("LeaderElection");
    }
}
