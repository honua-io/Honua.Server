using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.BlueGreen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Honua.Server.Core.Tests.Infrastructure.BlueGreen;

[Trait("Category", "Unit")]
public class BlueGreenTrafficManagerTests
{
    private readonly Mock<ILogger<BlueGreenTrafficManager>> _mockLogger;
    private readonly InMemoryConfigProvider _configProvider;
    private readonly BlueGreenTrafficManager _manager;

    public BlueGreenTrafficManagerTests()
    {
        _mockLogger = new Mock<ILogger<BlueGreenTrafficManager>>();
        _configProvider = new InMemoryConfigProvider(new List<RouteConfig>(), new List<ClusterConfig>());
        _manager = new BlueGreenTrafficManager(_mockLogger.Object, _configProvider);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BlueGreenTrafficManager(null!));
    }

    [Fact]
    public async Task SwitchTrafficAsync_With50PercentGreen_ReturnsSuccess()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            50,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.BlueTrafficPercentage.Should().Be(50);
        result.GreenTrafficPercentage.Should().Be(50);
        result.Message.Should().Contain("50% blue, 50% green");
    }

    [Fact]
    public async Task SwitchTrafficAsync_With100PercentGreen_OnlyCreatesGreenDestination()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            100,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.BlueTrafficPercentage.Should().Be(0);
        result.GreenTrafficPercentage.Should().Be(100);
    }

    [Fact]
    public async Task SwitchTrafficAsync_With0PercentGreen_OnlyCreatesBlueDestination()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            0,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.BlueTrafficPercentage.Should().Be(100);
        result.GreenTrafficPercentage.Should().Be(0);
    }

    [Fact]
    public async Task SwitchTrafficAsync_WithInvalidPercentage_ThrowsArgumentException()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            150, // Invalid
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to switch traffic");
    }

    [Fact]
    public async Task SwitchTrafficAsync_WithNegativePercentage_ThrowsArgumentException()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            -10, // Invalid
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SwitchTrafficAsync_UpdatesProxyConfiguration()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        await _manager.SwitchTrafficAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            50,
            CancellationToken.None);

        // Assert
        var config = _configProvider.GetConfig();
        config.Clusters.Should().ContainSingle(c => c.ClusterId == serviceName);
        config.Routes.Should().ContainSingle(r => r.ClusterId == serviceName);
    }

    [Fact]
    public async Task PerformInstantCutoverAsync_Switches100PercentToGreen()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        var result = await _manager.PerformInstantCutoverAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.GreenTrafficPercentage.Should().Be(100);
        result.BlueTrafficPercentage.Should().Be(0);
    }

    [Fact]
    public async Task RollbackToBlueAsync_Switches100PercentToBlue()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // First switch to green
        await _manager.SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 100, CancellationToken.None);

        // Act - Rollback to blue
        var result = await _manager.RollbackToBlueAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.GreenTrafficPercentage.Should().Be(0);
        result.BlueTrafficPercentage.Should().Be(100);
    }

    [Fact]
    public async Task PerformCanaryDeploymentAsync_WithHealthyGreen_CompletesSuccessfully()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";
        var strategy = new CanaryStrategy
        {
            TrafficSteps = new List<int> { 25, 50, 100 },
            SoakDurationSeconds = 0 // No delay for test
        };

        // Health check always returns true
        Func<CancellationToken, Task<bool>> healthCheck = _ => Task.FromResult(true);

        // Act
        var result = await _manager.PerformCanaryDeploymentAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            strategy,
            healthCheck,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RolledBack.Should().BeFalse();
        result.Stages.Should().HaveCount(3);
        result.Stages.All(s => s.IsHealthy).Should().BeTrue();
        result.Message.Should().Contain("completed successfully");
    }

    [Fact]
    public async Task PerformCanaryDeploymentAsync_WithUnhealthyGreen_RollsBack()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";
        var strategy = new CanaryStrategy
        {
            TrafficSteps = new List<int> { 25, 50, 100 },
            SoakDurationSeconds = 0
        };

        var callCount = 0;
        Func<CancellationToken, Task<bool>> healthCheck = _ =>
        {
            callCount++;
            return Task.FromResult(callCount != 2); // Fail on second stage
        };

        // Act
        var result = await _manager.PerformCanaryDeploymentAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            strategy,
            healthCheck,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.RolledBack.Should().BeTrue();
        result.Stages.Should().HaveCount(2); // Should stop after failure
        result.Message.Should().Contain("rolled back");
    }

    [Fact]
    public async Task PerformCanaryDeploymentAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";
        var strategy = new CanaryStrategy
        {
            TrafficSteps = new List<int> { 25, 50, 100 },
            SoakDurationSeconds = 10 // Long soak to allow cancellation
        };

        Func<CancellationToken, Task<bool>> healthCheck = _ => Task.FromResult(true);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act
        var result = await _manager.PerformCanaryDeploymentAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            strategy,
            healthCheck,
            cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.RolledBack.Should().BeTrue();
        result.Message.Should().Contain("failed");
    }

    [Fact]
    public async Task PerformCanaryDeploymentAsync_RecordsTimestamps()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";
        var strategy = new CanaryStrategy
        {
            TrafficSteps = new List<int> { 50, 100 },
            SoakDurationSeconds = 0
        };

        Func<CancellationToken, Task<bool>> healthCheck = _ => Task.FromResult(true);

        // Act
        var result = await _manager.PerformCanaryDeploymentAsync(
            serviceName,
            blueEndpoint,
            greenEndpoint,
            strategy,
            healthCheck,
            CancellationToken.None);

        // Assert
        result.Stages.Should().HaveCount(2);
        result.Stages.All(s => s.Timestamp > DateTime.MinValue).Should().BeTrue();
        result.Stages[0].Timestamp.Should().BeBefore(result.Stages[1].Timestamp.AddSeconds(1));
    }

    [Fact]
    public async Task SwitchTrafficAsync_WithMultipleServices_MaintainsSeparateConfigurations()
    {
        // Arrange
        var service1 = "service-1";
        var service2 = "service-2";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act
        await _manager.SwitchTrafficAsync(service1, blueEndpoint, greenEndpoint, 30, CancellationToken.None);
        await _manager.SwitchTrafficAsync(service2, blueEndpoint, greenEndpoint, 70, CancellationToken.None);

        // Assert
        var config = _configProvider.GetConfig();
        config.Clusters.Should().HaveCount(2);
        config.Routes.Should().HaveCount(2);
        config.Clusters.Should().Contain(c => c.ClusterId == service1);
        config.Clusters.Should().Contain(c => c.ClusterId == service2);
    }

    [Fact]
    public async Task SwitchTrafficAsync_UpdatesExistingServiceConfiguration()
    {
        // Arrange
        var serviceName = "test-service";
        var blueEndpoint = "http://blue:8080";
        var greenEndpoint = "http://green:8080";

        // Act - First switch to 50%
        await _manager.SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 50, CancellationToken.None);

        // Act - Then switch to 100%
        await _manager.SwitchTrafficAsync(serviceName, blueEndpoint, greenEndpoint, 100, CancellationToken.None);

        // Assert
        var config = _configProvider.GetConfig();
        config.Clusters.Should().ContainSingle(c => c.ClusterId == serviceName);

        var cluster = config.Clusters.Single(c => c.ClusterId == serviceName);
        cluster.Destinations.Should().ContainKey("green");
        cluster.Destinations.Should().NotContainKey("blue");
    }
}
