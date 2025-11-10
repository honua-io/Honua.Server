// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using Azure.Messaging.EventHubs;
using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Services;

public class IoTHubMessageParserTests
{
    private readonly Mock<IOptionsMonitor<AzureIoTHubOptions>> _optionsMock;
    private readonly Mock<ILogger<IoTHubMessageParser>> _loggerMock;
    private readonly IoTHubMessageParser _parser;

    public IoTHubMessageParserTests()
    {
        _optionsMock = new Mock<IOptionsMonitor<AzureIoTHubOptions>>();
        _loggerMock = new Mock<ILogger<IoTHubMessageParser>>();

        var options = new AzureIoTHubOptions
        {
            TelemetryParsing = new TelemetryParsingOptions
            {
                DefaultFormat = "Json",
                PreserveSystemProperties = true,
                PreserveApplicationProperties = true
            }
        };

        _optionsMock.Setup(x => x.CurrentValue).Returns(options);

        _parser = new IoTHubMessageParser(_optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ParseMessageAsync_WithValidJsonTelemetry_ShouldParseSuccessfully()
    {
        // Arrange
        var telemetryJson = @"{""temperature"": 25.5, ""humidity"": 60}";
        var eventData = CreateEventData(telemetryJson, "device-001");

        // Act
        var result = await _parser.ParseMessageAsync(eventData);

        // Assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be("device-001");
        result.Telemetry.Should().ContainKey("temperature");
        result.Telemetry["temperature"].Should().Be(25.5);
        result.Telemetry.Should().ContainKey("humidity");
        result.Telemetry["humidity"].Should().Be(60L);
    }

    [Fact]
    public async Task ParseMessageAsync_WithNestedJsonTelemetry_ShouldFlattenProperties()
    {
        // Arrange
        var telemetryJson = @"{""sensor"": {""temp"": 25.5, ""unit"": ""C""}}";
        var eventData = CreateEventData(telemetryJson, "device-002");

        // Act
        var result = await _parser.ParseMessageAsync(eventData);

        // Assert
        result.Telemetry.Should().ContainKey("sensor.temp");
        result.Telemetry["sensor.temp"].Should().Be(25.5);
        result.Telemetry.Should().ContainKey("sensor.unit");
        result.Telemetry["sensor.unit"].Should().Be("C");
    }

    [Fact]
    public async Task ParseMessageAsync_ShouldExtractSystemProperties()
    {
        // Arrange
        var telemetryJson = @"{""value"": 100}";
        var eventData = CreateEventData(telemetryJson, "device-003");

        // Act
        var result = await _parser.ParseMessageAsync(eventData);

        // Assert
        result.SystemProperties.Should().ContainKey("iothub-connection-device-id");
        result.SystemProperties["iothub-connection-device-id"].Should().Be("device-003");
    }

    [Fact]
    public async Task ParseMessageAsync_WithApplicationProperties_ShouldExtractThem()
    {
        // Arrange
        var telemetryJson = @"{""value"": 100}";
        var eventData = CreateEventData(telemetryJson, "device-004");
        eventData.Properties.Add("customProperty", "customValue");

        // Act
        var result = await _parser.ParseMessageAsync(eventData);

        // Assert
        result.ApplicationProperties.Should().ContainKey("customProperty");
        result.ApplicationProperties["customProperty"].Should().Be("customValue");
    }

    [Fact]
    public async Task ParseMessagesAsync_ShouldHandleMultipleMessages()
    {
        // Arrange
        var events = new[]
        {
            CreateEventData(@"{""temp"": 20}", "device-001"),
            CreateEventData(@"{""temp"": 21}", "device-002"),
            CreateEventData(@"{""temp"": 22}", "device-003")
        };

        // Act
        var results = await _parser.ParseMessagesAsync(events);

        // Assert
        results.Should().HaveCount(3);
        results[0].DeviceId.Should().Be("device-001");
        results[1].DeviceId.Should().Be("device-002");
        results[2].DeviceId.Should().Be("device-003");
    }

    [Fact]
    public async Task ParseMessageAsync_WithMissingDeviceId_ShouldThrowException()
    {
        // Arrange
        var telemetryJson = @"{""value"": 100}";
        var body = Encoding.UTF8.GetBytes(telemetryJson);
        var eventData = new EventData(body);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _parser.ParseMessageAsync(eventData));
    }

    [Fact]
    public async Task ParseMessageAsync_WithInvalidJson_ShouldReturnRawString()
    {
        // Arrange
        var invalidJson = "not valid json";
        var eventData = CreateEventData(invalidJson, "device-005");

        // Act
        var result = await _parser.ParseMessageAsync(eventData);

        // Assert
        result.Telemetry.Should().ContainKey("raw");
        result.Telemetry["raw"].Should().Be(invalidJson);
    }

    private static EventData CreateEventData(string telemetryJson, string deviceId)
    {
        var body = Encoding.UTF8.GetBytes(telemetryJson);
        var eventData = new EventData(body);

        // Add IoT Hub system properties
        eventData.SystemProperties.Add("iothub-connection-device-id", deviceId);
        eventData.SystemProperties.Add("iothub-enqueuedtime", DateTime.UtcNow);

        return eventData;
    }
}
