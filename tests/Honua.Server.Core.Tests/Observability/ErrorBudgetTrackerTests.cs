// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Observability;

public class ErrorBudgetTrackerTests
{
    private readonly Mock<ISliMetrics> _sliMetricsMock;
    private readonly Mock<ILogger<ErrorBudgetTracker>> _loggerMock;
    private readonly SreOptions _options;

    public ErrorBudgetTrackerTests()
    {
        _sliMetricsMock = new Mock<ISliMetrics>();
        _loggerMock = new Mock<ILogger<ErrorBudgetTracker>>();
        _options = new SreOptions
        {
            Enabled = true,
            RollingWindowDays = 28,
            Slos = new Dictionary<string, SloConfig>
            {
                ["test_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.Availability,
                    Target = 0.99
                }
            },
            ErrorBudgetThresholds = new ErrorBudgetThresholds
            {
                WarningThreshold = 0.25,
                CriticalThreshold = 0.10
            }
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_DoesNotThrow()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
            new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void GetErrorBudget_WhenNoData_ReturnsFullBudget()
    {
        // Arrange
        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns((SliStatistics?)null);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var budget = tracker.GetErrorBudget("test_slo");

        // Assert
        budget.Should().NotBeNull();
        budget!.TotalRequests.Should().Be(0);
        budget.BudgetRemaining.Should().Be(1.0);
        budget.Status.Should().Be(ErrorBudgetStatus.Healthy);
    }

    [Fact]
    public void GetErrorBudget_WithHealthyBudget_ReturnsHealthyStatus()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9950 // 99.5% (better than 99% target)
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var budget = tracker.GetErrorBudget("test_slo");

        // Assert
        budget.Should().NotBeNull();
        budget!.TotalRequests.Should().Be(10000);
        budget.FailedRequests.Should().Be(50);
        budget.AllowedErrors.Should().Be(100); // 1% of 10000
        budget.RemainingErrors.Should().Be(50); // 100 - 50
        budget.BudgetRemaining.Should().BeApproximately(0.5, 0.01); // 50% remaining
        budget.Status.Should().Be(ErrorBudgetStatus.Healthy);
        budget.SloMet.Should().BeTrue();
    }

    [Fact]
    public void GetErrorBudget_WithWarningBudget_ReturnsWarningStatus()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9920 // 99.2% (meeting target, but budget running low)
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var budget = tracker.GetErrorBudget("test_slo");

        // Assert
        budget.Should().NotBeNull();
        budget!.AllowedErrors.Should().Be(100);
        budget.FailedRequests.Should().Be(80);
        budget.RemainingErrors.Should().Be(20);
        budget.BudgetRemaining.Should().BeApproximately(0.2, 0.01); // 20% remaining
        budget.Status.Should().Be(ErrorBudgetStatus.Warning);
    }

    [Fact]
    public void GetErrorBudget_WithCriticalBudget_ReturnsCriticalStatus()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9905 // 99.05% (barely meeting target)
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var budget = tracker.GetErrorBudget("test_slo");

        // Assert
        budget.Should().NotBeNull();
        budget!.RemainingErrors.Should().Be(5);
        budget.BudgetRemaining.Should().BeApproximately(0.05, 0.01);
        budget.Status.Should().Be(ErrorBudgetStatus.Critical);
    }

    [Fact]
    public void GetErrorBudget_WithExhaustedBudget_ReturnsExhaustedStatus()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9850 // 98.5% (below 99% target)
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var budget = tracker.GetErrorBudget("test_slo");

        // Assert
        budget.Should().NotBeNull();
        budget!.FailedRequests.Should().Be(150);
        budget.AllowedErrors.Should().Be(100);
        budget.RemainingErrors.Should().Be(0); // Budget exhausted
        budget.BudgetRemaining.Should().Be(0.0);
        budget.Status.Should().Be(ErrorBudgetStatus.Exhausted);
        budget.SloMet.Should().BeFalse();
    }

    [Fact]
    public void GetAllErrorBudgets_ReturnsAllConfiguredSlos()
    {
        // Arrange
        var optionsWithMultipleSlos = new SreOptions
        {
            Enabled = true,
            RollingWindowDays = 28,
            Slos = new Dictionary<string, SloConfig>
            {
                ["slo1"] = new SloConfig { Enabled = true, Type = SliType.Availability, Target = 0.99 },
                ["slo2"] = new SloConfig { Enabled = true, Type = SliType.Latency, Target = 0.95, ThresholdMs = 500 }
            }
        };

        var statistics1 = new SliStatistics
        {
            Name = "slo1",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 1000,
            GoodEvents = 995
        };

        var statistics2 = new SliStatistics
        {
            Name = "slo2",
            Type = SliType.Latency,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 1000,
            GoodEvents = 970
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("slo1", It.IsAny<TimeSpan>()))
            .Returns(statistics1);

        _sliMetricsMock
            .Setup(m => m.GetStatistics("slo2", It.IsAny<TimeSpan>()))
            .Returns(statistics2);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(optionsWithMultipleSlos), _loggerMock.Object);

        // Act
        var budgets = tracker.GetAllErrorBudgets();

        // Assert
        budgets.Should().NotBeNull();
        budgets.Count.Should().Be(2);
        budgets.Should().Contain(b => b.SloName == "slo1");
        budgets.Should().Contain(b => b.SloName == "slo2");
    }

    [Fact]
    public void GetDeploymentPolicy_WithHealthyBudgets_ReturnsNormalPolicy()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9950
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var policy = tracker.GetDeploymentPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.CanDeploy.Should().BeTrue();
        policy.Recommendation.Should().Be(DeploymentRecommendation.Normal);
    }

    [Fact]
    public void GetDeploymentPolicy_WithWarningBudget_ReturnsCautiousPolicy()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9920
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var policy = tracker.GetDeploymentPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.CanDeploy.Should().BeTrue();
        policy.Recommendation.Should().Be(DeploymentRecommendation.Cautious);
        policy.AffectedSlos.Should().Contain("test_slo");
    }

    [Fact]
    public void GetDeploymentPolicy_WithCriticalBudget_ReturnsRestrictedPolicy()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9905
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var policy = tracker.GetDeploymentPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.CanDeploy.Should().BeTrue();
        policy.Recommendation.Should().Be(DeploymentRecommendation.Restricted);
    }

    [Fact]
    public void GetDeploymentPolicy_WithExhaustedBudget_ReturnsHaltPolicy()
    {
        // Arrange
        var statistics = new SliStatistics
        {
            Name = "test_slo",
            Type = SliType.Availability,
            WindowStart = DateTimeOffset.UtcNow.AddDays(-28),
            WindowEnd = DateTimeOffset.UtcNow,
            TotalEvents = 10000,
            GoodEvents = 9850
        };

        _sliMetricsMock
            .Setup(m => m.GetStatistics("test_slo", It.IsAny<TimeSpan>()))
            .Returns(statistics);

        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act
        var policy = tracker.GetDeploymentPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.CanDeploy.Should().BeFalse();
        policy.Recommendation.Should().Be(DeploymentRecommendation.Halt);
        policy.AffectedSlos.Should().Contain("test_slo");
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var tracker = new ErrorBudgetTracker(_sliMetricsMock.Object, Options.Create(_options), _loggerMock.Object);

        // Act & Assert
        var exception = Record.Exception(() => tracker.Dispose());
        exception.Should().BeNull();
    }
}
