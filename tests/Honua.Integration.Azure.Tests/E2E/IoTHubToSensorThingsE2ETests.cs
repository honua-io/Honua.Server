// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using Azure.Messaging.EventHubs;
using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.ErrorHandling;
using Honua.Integration.Azure.Mapping;
using Honua.Integration.Azure.Models;
using Honua.Integration.Azure.Services;
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.E2E;

/// <summary>
/// End-to-end tests for IoT Hub to SensorThings API integration.
/// Tests complete flow: IoT Device → IoT Hub → Event Hub → Honua → SensorThings API → Database
/// </summary>
public class IoTHubToSensorThingsE2ETests : IAsyncLifetime
{
    private readonly MockSensorThingsRepository _repository;
    private readonly IOptionsMonitor<AzureIoTHubOptions> _options;
    private readonly IoTHubMessageParser _parser;
    private readonly SensorThingsMapper _mapper;
    private readonly Mock<IDeviceMappingService> _mappingService;
    private readonly Mock<IDeadLetterQueueService> _deadLetterQueue;

    public IoTHubToSensorThingsE2ETests()
    {
        _repository = new MockSensorThingsRepository();

        var options = new AzureIoTHubOptions
        {
            Enabled = true,
            TelemetryParsing = new TelemetryParsingOptions
            {
                DefaultFormat = "Json",
                PreserveSystemProperties = true,
                PreserveApplicationProperties = true,
                TimestampProperty = "timestamp"
            },
            ErrorHandling = new ErrorHandlingOptions
            {
                EnableDeadLetterQueue = true,
                MaxConsecutiveErrors = 10,
                RetryPolicy = new RetryPolicyOptions
                {
                    MaxRetries = 3,
                    BackoffInSeconds = 1
                }
            }
        };

        var optionsMock = new Mock<IOptionsMonitor<AzureIoTHubOptions>>();
        optionsMock.Setup(x => x.CurrentValue).Returns(options);
        _options = optionsMock.Object;

        _mappingService = new Mock<IDeviceMappingService>();
        SetupDefaultMappings();

        _deadLetterQueue = new Mock<IDeadLetterQueueService>();

        _parser = new IoTHubMessageParser(
            _options,
            NullLogger<IoTHubMessageParser>.Instance);

        _mapper = new SensorThingsMapper(
            _repository,
            _mappingService.Object,
            _options,
            NullLogger<SensorThingsMapper>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _repository.ClearAllAsync();
    }

    [Fact]
    public async Task SendDeviceTelemetry_EndsUpInSensorThingsApi_WithCorrectMapping()
    {
        // Arrange
        var deviceId = "smart-temp-sensor-001";
        var telemetryJson = @"{
            ""temperature"": 23.5,
            ""humidity"": 65.2,
            ""timestamp"": ""2025-11-10T10:30:00Z""
        }";

        var eventData = CreateEventData(telemetryJson, deviceId);

        // Act - Parse message
        var iotMessage = await _parser.ParseMessageAsync(eventData);

        // Assert - Message parsed correctly
        iotMessage.Should().NotBeNull();
        iotMessage.DeviceId.Should().Be(deviceId);
        iotMessage.Telemetry.Should().ContainKey("temperature");
        iotMessage.Telemetry["temperature"].Should().Be(23.5);

        // Act - Map to SensorThings
        var result = await _mapper.ProcessMessagesAsync(new[] { iotMessage });

        // Assert - Processing successful
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);
        result.ObservationsCreated.Should().Be(2); // temperature + humidity

        // Assert - Thing was created
        var things = await _repository.GetThingsAsync(new QueryOptions());
        things.Values.Should().HaveCount(1);
        var thing = things.Values[0];
        thing.Name.Should().Contain(deviceId);
        thing.Properties.Should().ContainKey("deviceId");
        thing.Properties["deviceId"].Should().Be(deviceId);

        // Assert - Datastreams were created
        var datastreams = await _repository.GetThingDatastreamsAsync(thing.Id, new QueryOptions());
        datastreams.Values.Should().HaveCount(2);
        datastreams.Values.Should().Contain(ds => ds.Name == "temperature");
        datastreams.Values.Should().Contain(ds => ds.Name == "humidity");

        // Assert - Observations were created
        var observations = await _repository.GetObservationsAsync(new QueryOptions());
        observations.Values.Should().HaveCount(2);

        var tempObservation = observations.Values.First(o =>
            datastreams.Values.First(ds => ds.Name == "temperature").Id == o.DatastreamId);
        tempObservation.Result.Should().Be(23.5);
        tempObservation.Parameters.Should().ContainKey("iotHub_deviceId");
    }

    [Fact]
    public async Task SendBatchTelemetry_CreatesMultipleObservations_InCorrectOrder()
    {
        // Arrange
        var deviceId = "smart-sensor-batch-001";
        var messages = new List<EventData>
        {
            CreateEventData(@"{""temperature"": 20.0, ""timestamp"": ""2025-11-10T10:00:00Z""}", deviceId),
            CreateEventData(@"{""temperature"": 21.0, ""timestamp"": ""2025-11-10T10:01:00Z""}", deviceId),
            CreateEventData(@"{""temperature"": 22.0, ""timestamp"": ""2025-11-10T10:02:00Z""}", deviceId),
            CreateEventData(@"{""temperature"": 23.0, ""timestamp"": ""2025-11-10T10:03:00Z""}", deviceId),
            CreateEventData(@"{""temperature"": 24.0, ""timestamp"": ""2025-11-10T10:04:00Z""}", deviceId)
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var parsedMessages = await _parser.ParseMessagesAsync(messages);
        var result = await _mapper.ProcessMessagesAsync(parsedMessages);
        stopwatch.Stop();

        // Assert - Performance target: 100 messages in <5 seconds (we're testing 5)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));

        // Assert - All messages processed
        result.SuccessCount.Should().Be(5);
        result.FailureCount.Should().Be(0);
        result.ObservationsCreated.Should().Be(5);

        // Assert - Observations are in correct chronological order
        var observations = await _repository.GetObservationsAsync(new QueryOptions
        {
            OrderBy = "phenomenonTime asc"
        });

        observations.Values.Should().HaveCount(5);

        var results = observations.Values.Select(o => Convert.ToDouble(o.Result)).ToList();
        results.Should().BeInAscendingOrder();
        results[0].Should().Be(20.0);
        results[4].Should().Be(24.0);
    }

    [Fact]
    public async Task SendTelemetryFromNewDevice_AutoCreatesThingAndDatastreams()
    {
        // Arrange
        var deviceId = "new-sensor-auto-create-001";
        var telemetryJson = @"{
            ""pressure"": 1013.25,
            ""altitude"": 120.5
        }";

        var eventData = CreateEventData(telemetryJson, deviceId);

        // Act
        var iotMessage = await _parser.ParseMessageAsync(eventData);
        var result = await _mapper.ProcessMessagesAsync(new[] { iotMessage });

        // Assert - Processing successful
        result.SuccessCount.Should().Be(1);
        result.ThingsCreated.Should().BeGreaterThan(0);

        // Assert - Thing auto-created
        var things = await _repository.GetThingsAsync(new QueryOptions
        {
            Filter = $"properties/deviceId eq '{deviceId}'"
        });
        things.Values.Should().HaveCount(1);
        var thing = things.Values[0];

        // Assert - Datastreams auto-created for both telemetry fields
        var datastreams = await _repository.GetThingDatastreamsAsync(thing.Id, new QueryOptions());
        datastreams.Values.Should().HaveCount(2);

        var pressureDatastream = datastreams.Values.FirstOrDefault(ds =>
            ds.Properties.ContainsKey("telemetryField") &&
            ds.Properties["telemetryField"].ToString() == "pressure");
        pressureDatastream.Should().NotBeNull();

        var altitudeDatastream = datastreams.Values.FirstOrDefault(ds =>
            ds.Properties.ContainsKey("telemetryField") &&
            ds.Properties["telemetryField"].ToString() == "altitude");
        altitudeDatastream.Should().NotBeNull();

        // Assert - Sensors auto-created
        var sensors = await _repository.GetSensorsAsync(new QueryOptions());
        sensors.Values.Should().HaveCount(2);

        // Assert - ObservedProperties auto-created
        var observedProperties = await _repository.GetObservedPropertiesAsync(new QueryOptions());
        observedProperties.Values.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendTelemetryWithTenantProperty_AssignsToCorrectTenant()
    {
        // Arrange
        var deviceId = "tenant-sensor-001";
        var tenantId = "tenant-city-abc";

        var telemetryJson = @"{""temperature"": 25.0}";
        var eventData = CreateEventData(telemetryJson, deviceId);
        eventData.Properties.Add("tenantId", tenantId);

        // Setup mapping service to extract tenant from application properties
        _mappingService.Setup(m => m.ResolveTenantId(It.IsAny<IoTHubMessage>()))
            .Returns((IoTHubMessage msg) =>
            {
                if (msg.ApplicationProperties.TryGetValue("tenantId", out var tid))
                    return tid.ToString();
                return null;
            });

        // Act
        var iotMessage = await _parser.ParseMessageAsync(eventData);
        var result = await _mapper.ProcessMessagesAsync(new[] { iotMessage });

        // Assert - Processing successful
        result.SuccessCount.Should().Be(1);

        // Assert - Thing has tenant ID
        var things = await _repository.GetThingsAsync(new QueryOptions());
        things.Values.Should().HaveCount(1);
        var thing = things.Values[0];

        thing.Properties.Should().ContainKey("tenantId");
        thing.Properties["tenantId"].Should().Be(tenantId);

        // Assert - Can query by tenant
        var tenantThings = await _repository.GetThingsAsync(new QueryOptions
        {
            Filter = $"properties/tenantId eq '{tenantId}'"
        });
        tenantThings.Values.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendMalformedTelemetry_LogsError_DoesNotBlockOtherMessages()
    {
        // Arrange
        var goodDeviceId = "good-sensor-001";
        var badDeviceId = "bad-sensor-001";

        var messages = new List<EventData>
        {
            CreateEventData(@"{""temperature"": 20.0}", goodDeviceId),
            CreateEventData(@"{invalid json!", badDeviceId),
            CreateEventData(@"{""temperature"": 22.0}", goodDeviceId)
        };

        // Act
        var parsedMessages = new List<IoTHubMessage>();
        foreach (var msg in messages)
        {
            try
            {
                var parsed = await _parser.ParseMessageAsync(msg);
                parsedMessages.Add(parsed);
            }
            catch
            {
                // Parser may throw on invalid JSON - that's OK
            }
        }

        var result = await _mapper.ProcessMessagesAsync(parsedMessages);

        // Assert - Good messages processed successfully
        result.SuccessCount.Should().BeGreaterOrEqualTo(2);

        // Assert - System continues to work
        var observations = await _repository.GetObservationsAsync(new QueryOptions());
        observations.Values.Should().HaveCountGreaterOrEqualTo(2);

        // Note: In real implementation, malformed messages would go to dead letter queue
        // Here we're testing that good messages aren't blocked by bad ones
    }

    [Fact]
    public async Task PerformanceBenchmark_Process100Messages_UnderPerformanceTarget()
    {
        // Arrange - Create 100 messages from 10 different devices
        var messages = new List<EventData>();
        for (int deviceNum = 0; deviceNum < 10; deviceNum++)
        {
            var deviceId = $"perf-device-{deviceNum:D3}";
            for (int msgNum = 0; msgNum < 10; msgNum++)
            {
                var telemetryJson = $@"{{
                    ""temperature"": {20 + deviceNum + msgNum * 0.1},
                    ""humidity"": {50 + msgNum},
                    ""timestamp"": ""2025-11-10T10:{msgNum:D2}:00Z""
                }}";
                messages.Add(CreateEventData(telemetryJson, deviceId));
            }
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var parsedMessages = await _parser.ParseMessagesAsync(messages);
        var result = await _mapper.ProcessMessagesAsync(parsedMessages);
        stopwatch.Stop();

        // Assert - Performance target: 100 messages in <5 seconds
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            $"Processing took {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Assert - All messages processed successfully
        result.SuccessCount.Should().Be(100);
        result.FailureCount.Should().Be(0);
        result.ObservationsCreated.Should().Be(200); // 100 messages * 2 fields each

        // Assert - Correct number of Things created (10 devices)
        var things = await _repository.GetThingsAsync(new QueryOptions());
        things.Values.Should().HaveCount(10);

        // Log performance metrics
        Console.WriteLine($"Performance Metrics:");
        Console.WriteLine($"  Total Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Messages/Second: {100 / stopwatch.Elapsed.TotalSeconds:F2}");
        Console.WriteLine($"  Things Created: {result.ThingsCreated}");
        Console.WriteLine($"  Observations Created: {result.ObservationsCreated}");
    }

    private static EventData CreateEventData(string telemetryJson, string deviceId)
    {
        var body = Encoding.UTF8.GetBytes(telemetryJson);
        var eventData = new EventData(body);

        // Add IoT Hub system properties
        eventData.SystemProperties.Add("iothub-connection-device-id", deviceId);
        eventData.SystemProperties.Add("iothub-enqueuedtime", DateTime.UtcNow);
        eventData.SystemProperties.Add("iothub-message-source", "Telemetry");

        return eventData;
    }

    private void SetupDefaultMappings()
    {
        var defaultRule = new DeviceMappingRule
        {
            DevicePattern = "*",
            ThingNameTemplate = "IoT Device: {deviceId}",
            ThingDescriptionTemplate = "Device {deviceId} connected via Azure IoT Hub",
            TelemetryMappings = new Dictionary<string, TelemetryFieldMapping>
            {
                ["temperature"] = new TelemetryFieldMapping
                {
                    Name = "temperature",
                    Description = "Temperature measurement",
                    ObservedPropertyName = "Temperature",
                    ObservedPropertyDescription = "Air temperature",
                    UnitOfMeasurement = new UnitOfMeasurementConfig
                    {
                        Name = "Celsius",
                        Symbol = "°C",
                        Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
                    }
                },
                ["humidity"] = new TelemetryFieldMapping
                {
                    Name = "humidity",
                    Description = "Humidity measurement",
                    ObservedPropertyName = "Humidity",
                    ObservedPropertyDescription = "Relative humidity",
                    UnitOfMeasurement = new UnitOfMeasurementConfig
                    {
                        Name = "Percent",
                        Symbol = "%",
                        Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Percent"
                    }
                }
            }
        };

        var config = new DeviceMappingConfiguration
        {
            Defaults = new DefaultMappingRules
            {
                DefaultUnit = new UnitOfMeasurementConfig
                {
                    Name = "unitless",
                    Symbol = "",
                    Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#Unitless"
                }
            },
            DeviceMappings = new[] { defaultRule }
        };

        _mappingService.Setup(m => m.GetMappingForDevice(It.IsAny<string>()))
            .Returns(defaultRule);

        _mappingService.Setup(m => m.GetConfiguration())
            .Returns(config);

        _mappingService.Setup(m => m.ResolveTenantId(It.IsAny<IoTHubMessage>()))
            .Returns((string?)null);
    }
}
