// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Mapping;
using Honua.Integration.Azure.Models;
using Honua.Integration.Azure.Services;
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Services;

public class SensorThingsMapperTests
{
    private readonly Mock<ISensorThingsRepository> _repositoryMock;
    private readonly Mock<IDeviceMappingService> _mappingServiceMock;
    private readonly Mock<IOptionsMonitor<AzureIoTHubOptions>> _optionsMock;
    private readonly Mock<ILogger<SensorThingsMapper>> _loggerMock;
    private readonly SensorThingsMapper _mapper;

    public SensorThingsMapperTests()
    {
        _repositoryMock = new Mock<ISensorThingsRepository>();
        _mappingServiceMock = new Mock<IDeviceMappingService>();
        _optionsMock = new Mock<IOptionsMonitor<AzureIoTHubOptions>>();
        _loggerMock = new Mock<ILogger<SensorThingsMapper>>();

        var options = new AzureIoTHubOptions
        {
            TelemetryParsing = new TelemetryParsingOptions
            {
                PreserveSystemProperties = true,
                PreserveApplicationProperties = true
            }
        };

        _optionsMock.Setup(x => x.CurrentValue).Returns(options);

        var defaultMapping = new DeviceMappingRule
        {
            ThingNameTemplate = "IoT Device: {deviceId}",
            ThingDescriptionTemplate = "Device {deviceId}"
        };

        _mappingServiceMock
            .Setup(x => x.GetMappingForDevice(It.IsAny<string>()))
            .Returns(defaultMapping);

        _mappingServiceMock
            .Setup(x => x.GetConfiguration())
            .Returns(new DeviceMappingConfiguration
            {
                Defaults = new DefaultMappingRules()
            });

        _mapper = new SensorThingsMapper(
            _repositoryMock.Object,
            _mappingServiceMock.Object,
            _optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithNewDevice_ShouldCreateThingAndObservations()
    {
        // Arrange
        var message = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow,
            Telemetry = new Dictionary<string, object>
            {
                ["temperature"] = 25.5,
                ["humidity"] = 60
            },
            SystemProperties = new Dictionary<string, object>(),
            ApplicationProperties = new Dictionary<string, object>()
        };

        // Setup repository to return empty results (device doesn't exist)
        _repositoryMock
            .Setup(x => x.GetThingsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Thing>
            {
                Values = new List<Thing>(),
                TotalCount = 0
            });

        _repositoryMock
            .Setup(x => x.CreateThingAsync(It.IsAny<Thing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Thing t, CancellationToken ct) => t with { Id = "thing-001" });

        _repositoryMock
            .Setup(x => x.GetThingDatastreamsAsync(It.IsAny<string>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Datastream> { Values = new List<Datastream>() });

        _repositoryMock
            .Setup(x => x.GetSensorsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Sensor> { Values = new List<Sensor>() });

        _repositoryMock
            .Setup(x => x.CreateSensorAsync(It.IsAny<Sensor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sensor s, CancellationToken ct) => s with { Id = Guid.NewGuid().ToString() });

        _repositoryMock
            .Setup(x => x.GetObservedPropertiesAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ObservedProperty> { Values = new List<ObservedProperty>() });

        _repositoryMock
            .Setup(x => x.CreateObservedPropertyAsync(It.IsAny<ObservedProperty>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObservedProperty op, CancellationToken ct) => op with { Id = Guid.NewGuid().ToString() });

        _repositoryMock
            .Setup(x => x.CreateDatastreamAsync(It.IsAny<Datastream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Datastream ds, CancellationToken ct) => ds with { Id = Guid.NewGuid().ToString() });

        _repositoryMock
            .Setup(x => x.CreateObservationsBatchAsync(It.IsAny<IReadOnlyList<Observation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Observation> obs, CancellationToken ct) => obs);

        // Act
        var result = await _mapper.ProcessMessagesAsync(new[] { message });

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);
        result.ObservationsCreated.Should().Be(2); // temperature + humidity

        // Verify Thing was created
        _repositoryMock.Verify(
            x => x.CreateThingAsync(It.IsAny<Thing>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify observations were created
        _repositoryMock.Verify(
            x => x.CreateObservationsBatchAsync(
                It.Is<IReadOnlyList<Observation>>(obs => obs.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithEmptyTelemetry_ShouldNotCreateObservations()
    {
        // Arrange
        var message = new IoTHubMessage
        {
            DeviceId = "device-002",
            EnqueuedTime = DateTime.UtcNow,
            Telemetry = new Dictionary<string, object>(),
            SystemProperties = new Dictionary<string, object>(),
            ApplicationProperties = new Dictionary<string, object>()
        };

        _repositoryMock
            .Setup(x => x.GetThingsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Thing>
            {
                Values = new List<Thing>
                {
                    new Thing { Id = "thing-002", Name = "Existing Thing", Description = "Test" }
                }
            });

        // Act
        var result = await _mapper.ProcessMessagesAsync(new[] { message });

        // Assert
        result.ObservationsCreated.Should().Be(0);

        // Verify no observations were created
        _repositoryMock.Verify(
            x => x.CreateObservationsBatchAsync(
                It.IsAny<IReadOnlyList<Observation>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessagesAsync_WithMultipleMessages_ShouldProcessAll()
    {
        // Arrange
        var messages = new[]
        {
            new IoTHubMessage
            {
                DeviceId = "device-001",
                EnqueuedTime = DateTime.UtcNow,
                Telemetry = new Dictionary<string, object> { ["temp"] = 20 },
                SystemProperties = new Dictionary<string, object>(),
                ApplicationProperties = new Dictionary<string, object>()
            },
            new IoTHubMessage
            {
                DeviceId = "device-002",
                EnqueuedTime = DateTime.UtcNow,
                Telemetry = new Dictionary<string, object> { ["temp"] = 21 },
                SystemProperties = new Dictionary<string, object>(),
                ApplicationProperties = new Dictionary<string, object>()
            }
        };

        SetupBasicMocks();

        // Act
        var result = await _mapper.ProcessMessagesAsync(messages);

        // Assert
        result.SuccessCount.Should().Be(2);
    }

    private void SetupBasicMocks()
    {
        _repositoryMock
            .Setup(x => x.GetThingsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Thing> { Values = new List<Thing>() });

        _repositoryMock
            .Setup(x => x.CreateThingAsync(It.IsAny<Thing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Thing t, CancellationToken ct) => t with { Id = Guid.NewGuid().ToString() });

        _repositoryMock
            .Setup(x => x.GetThingDatastreamsAsync(It.IsAny<string>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Datastream> { Values = new List<Datastream>() });

        _repositoryMock
            .Setup(x => x.GetSensorsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Sensor> { Values = new List<Sensor>() });

        _repositoryMock
            .Setup(x => x.CreateSensorAsync(It.IsAny<Sensor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Sensor s, CancellationToken ct) => s);

        _repositoryMock
            .Setup(x => x.GetObservedPropertiesAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ObservedProperty> { Values = new List<ObservedProperty>() });

        _repositoryMock
            .Setup(x => x.CreateObservedPropertyAsync(It.IsAny<ObservedProperty>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObservedProperty op, CancellationToken ct) => op);

        _repositoryMock
            .Setup(x => x.CreateDatastreamAsync(It.IsAny<Datastream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Datastream ds, CancellationToken ct) => ds);

        _repositoryMock
            .Setup(x => x.CreateObservationsBatchAsync(It.IsAny<IReadOnlyList<Observation>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Observation> obs, CancellationToken ct) => obs);
    }
}
