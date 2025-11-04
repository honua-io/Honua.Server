using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Features;

[Trait("Category", "Unit")]
public sealed class FeatureManagementServiceTests
{
    private readonly Mock<ILogger<FeatureManagementService>> _loggerMock;
    private readonly Mock<HealthCheckService> _healthCheckServiceMock;

    public FeatureManagementServiceTests()
    {
        _loggerMock = new Mock<ILogger<FeatureManagementService>>();
        _healthCheckServiceMock = new Mock<HealthCheckService>();
    }

    [Fact]
    public async Task IsFeatureAvailableAsync_EnabledFeature_ReturnsTrue()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = true }
        };

        var service = CreateService(options);

        // Act
        var result = await service.IsFeatureAvailableAsync("AdvancedCaching");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsFeatureAvailableAsync_DisabledFeature_ReturnsFalse()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = false }
        };

        var service = CreateService(options);

        // Act
        var result = await service.IsFeatureAvailableAsync("AdvancedCaching");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetFeatureStatusAsync_EnabledFeature_ReturnsHealthyStatus()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = true }
        };

        var service = CreateService(options);

        // Act
        var status = await service.GetFeatureStatusAsync("AdvancedCaching");

        // Assert
        Assert.Equal("AdvancedCaching", status.Name);
        Assert.True(status.IsAvailable);
        Assert.False(status.IsDegraded);
        Assert.Equal(FeatureDegradationState.Healthy, status.State);
    }

    [Fact]
    public async Task GetFeatureStatusAsync_DisabledFeature_ReturnsDisabledStatus()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = false }
        };

        var service = CreateService(options);

        // Act
        var status = await service.GetFeatureStatusAsync("AdvancedCaching");

        // Assert
        Assert.Equal("AdvancedCaching", status.Name);
        Assert.False(status.IsAvailable);
        Assert.False(status.IsDegraded);
        Assert.Equal(FeatureDegradationState.Disabled, status.State);
    }

    [Fact]
    public async Task DisableFeatureAsync_NonRequiredFeature_DisablesSuccessfully()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                Required = false
            }
        };

        var service = CreateService(options);

        // Act
        await service.DisableFeatureAsync("AdvancedCaching", "Test disable");
        var result = await service.IsFeatureAvailableAsync("AdvancedCaching");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DisableFeatureAsync_RequiredFeature_DoesNotDisable()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                Required = true
            }
        };

        var service = CreateService(options);

        // Act
        await service.DisableFeatureAsync("AdvancedCaching", "Test disable");
        var result = await service.IsFeatureAvailableAsync("AdvancedCaching");

        // Assert - required features cannot be manually disabled
        Assert.True(result);
    }

    [Fact]
    public async Task EnableFeatureAsync_DisabledFeature_EnablesSuccessfully()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = true }
        };

        var service = CreateService(options);

        // Act
        await service.DisableFeatureAsync("AdvancedCaching", "Test");
        await service.EnableFeatureAsync("AdvancedCaching");
        var result = await service.IsFeatureAvailableAsync("AdvancedCaching");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CheckFeatureHealthAsync_HealthyHealth_MaintainsHealthyStatus()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                MinHealthScore = 50
            }
        };

        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            HealthStatus.Healthy,
            TimeSpan.Zero);

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(
                It.IsAny<Func<HealthCheckRegistration, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var service = CreateService(options, _healthCheckServiceMock.Object);

        // Act
        var status = await service.CheckFeatureHealthAsync("AdvancedCaching");

        // Assert
        Assert.True(status.IsAvailable);
        Assert.False(status.IsDegraded);
        Assert.Equal(100, status.HealthScore);
    }

    [Fact]
    public async Task CheckFeatureHealthAsync_UnhealthyHealth_DegradesFeature()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                Required = false,
                MinHealthScore = 50,
                Strategy = new DegradationStrategy
                {
                    Type = DegradationType.Fallback
                }
            }
        };

        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            HealthStatus.Unhealthy,
            TimeSpan.Zero);

        _healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(
                It.IsAny<Func<HealthCheckRegistration, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var service = CreateService(options, _healthCheckServiceMock.Object);

        // Act
        var status = await service.CheckFeatureHealthAsync("AdvancedCaching");

        // Assert
        Assert.True(status.IsAvailable);
        Assert.True(status.IsDegraded);
        Assert.Equal(0, status.HealthScore);
        status.ActiveDegradation.Should().Be(DegradationType.Fallback);
    }

    [Fact]
    public async Task GetAllFeatureStatusesAsync_ReturnsAllFeatures()
    {
        // Arrange
        var options = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions { Enabled = true },
            AIConsultant = new FeatureOptions { Enabled = false },
            StacCatalog = new FeatureOptions { Enabled = true }
        };

        var service = CreateService(options);

        // Act
        var statuses = await service.GetAllFeatureStatusesAsync();

        // Assert
        Assert.True(statuses.Count >= 3);
        Assert.True(statuses.ContainsKey("AdvancedCaching"));
        Assert.True(statuses.ContainsKey("AIConsultant"));
        Assert.True(statuses.ContainsKey("StacCatalog"));
    }

    private FeatureManagementService CreateService(
        FeatureFlagsOptions options,
        HealthCheckService? healthCheckService = null)
    {
        var optionsMonitor = new TestOptionsMonitor<FeatureFlagsOptions>(options);

        return new FeatureManagementService(
            optionsMonitor,
            _loggerMock.Object,
            healthCheckService);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public TestOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
