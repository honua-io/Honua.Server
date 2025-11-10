// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Mapping;
using Honua.Integration.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Mapping;

public class DeviceMappingServiceTests
{
    private readonly Mock<IOptionsMonitor<AzureIoTHubOptions>> _optionsMock;
    private readonly Mock<ILogger<DeviceMappingService>> _loggerMock;
    private readonly DeviceMappingService _service;

    public DeviceMappingServiceTests()
    {
        _optionsMock = new Mock<IOptionsMonitor<AzureIoTHubOptions>>();
        _loggerMock = new Mock<ILogger<DeviceMappingService>>();

        var options = new AzureIoTHubOptions();
        _optionsMock.Setup(x => x.CurrentValue).Returns(options);

        _service = new DeviceMappingService(_optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetMappingForDevice_WithNoConfiguration_ShouldReturnDefaults()
    {
        // Act
        var mapping = _service.GetMappingForDevice("device-001");

        // Assert
        mapping.Should().NotBeNull();
        mapping.ThingNameTemplate.Should().Contain("{deviceId}");
    }

    [Fact]
    public void GetConfiguration_ShouldReturnDefaultConfiguration()
    {
        // Act
        var config = _service.GetConfiguration();

        // Assert
        config.Should().NotBeNull();
        config.Defaults.Should().NotBeNull();
        config.Defaults!.AutoCreateThings.Should().BeTrue();
    }

    [Fact]
    public void ResolveTenantId_WithDeviceIdPattern_ShouldMatchPattern()
    {
        // Arrange
        var config = _service.GetConfiguration();
        config.TenantMappings.Add(new TenantMappingRule
        {
            DeviceIdPattern = "tenant1-*",
            TenantId = "tenant-001"
        });

        var message = new IoTHubMessage
        {
            DeviceId = "tenant1-device-123",
            Telemetry = new Dictionary<string, object>()
        };

        // Act
        var tenantId = _service.ResolveTenantId(message);

        // Assert
        tenantId.Should().Be("tenant-001");
    }

    [Fact]
    public void ResolveTenantId_WithPropertyPath_ShouldExtractFromProperties()
    {
        // Arrange
        var config = _service.GetConfiguration();
        config.TenantMappings.Add(new TenantMappingRule
        {
            PropertyPath = "properties.tenantId",
            TenantId = "{propertyValue}"
        });

        var message = new IoTHubMessage
        {
            DeviceId = "device-001",
            ApplicationProperties = new Dictionary<string, object>
            {
                ["tenantId"] = "tenant-002"
            },
            Telemetry = new Dictionary<string, object>()
        };

        // Act
        var tenantId = _service.ResolveTenantId(message);

        // Assert
        tenantId.Should().Be("tenant-002");
    }

    [Fact]
    public void ResolveTenantId_WithNullMessage_ShouldReturnNull()
    {
        // Arrange
        var message = new IoTHubMessage
        {
            DeviceId = "unknown-device",
            Telemetry = new Dictionary<string, object>()
        };

        // Act
        var tenantId = _service.ResolveTenantId(message);

        // Assert
        tenantId.Should().BeNull();
    }

    [Fact]
    public void ResolveTenantId_WithHigherPriorityRule_ShouldPreferIt()
    {
        // Arrange
        var config = _service.GetConfiguration();
        config.TenantMappings.Add(new TenantMappingRule
        {
            DeviceIdPattern = "device-*",
            TenantId = "tenant-low",
            Priority = 1
        });
        config.TenantMappings.Add(new TenantMappingRule
        {
            DeviceIdPattern = "device-*",
            TenantId = "tenant-high",
            Priority = 10
        });

        var message = new IoTHubMessage
        {
            DeviceId = "device-123",
            Telemetry = new Dictionary<string, object>()
        };

        // Act
        var tenantId = _service.ResolveTenantId(message);

        // Assert
        tenantId.Should().Be("tenant-high");
    }
}
