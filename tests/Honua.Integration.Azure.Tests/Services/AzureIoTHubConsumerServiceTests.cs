// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Processor;
using FluentAssertions;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.ErrorHandling;
using Honua.Integration.Azure.Models;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Integration.Azure.Tests.Services;

public class AzureIoTHubConsumerServiceTests
{
    private readonly Mock<IOptionsMonitor<AzureIoTHubOptions>> _optionsMock;
    private readonly Mock<IIoTHubMessageParser> _messageParserMock;
    private readonly Mock<ISensorThingsMapper> _sensorThingsMapperMock;
    private readonly Mock<IDeadLetterQueueService> _deadLetterQueueMock;
    private readonly Mock<ILogger<AzureIoTHubConsumerService>> _loggerMock;
    private readonly AzureIoTHubConsumerService _service;
    private readonly AzureIoTHubOptions _options;

    public AzureIoTHubConsumerServiceTests()
    {
        _optionsMock = new Mock<IOptionsMonitor<AzureIoTHubOptions>>();
        _messageParserMock = new Mock<IIoTHubMessageParser>();
        _sensorThingsMapperMock = new Mock<ISensorThingsMapper>();
        _deadLetterQueueMock = new Mock<IDeadLetterQueueService>();
        _loggerMock = new Mock<ILogger<AzureIoTHubConsumerService>>();

        _options = new AzureIoTHubOptions
        {
            Enabled = false, // Disabled by default to avoid connection attempts
            ConsumerGroup = "$Default",
            CheckpointStorageConnectionString = "UseDevelopmentStorage=true",
            CheckpointContainerName = "test-checkpoints",
            ErrorHandling = new ErrorHandlingOptions
            {
                EnableDeadLetterQueue = true,
                MaxConsecutiveErrors = 10
            }
        };

        _optionsMock.Setup(x => x.CurrentValue).Returns(_options);

        _service = new AzureIoTHubConsumerService(
            _optionsMock.Object,
            _messageParserMock.Object,
            _sensorThingsMapperMock.Object,
            _deadLetterQueueMock.Object,
            _loggerMock.Object);
    }

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_WithDisabledConfiguration_DoesNotStartProcessor()
    {
        // Arrange
        _options.Enabled = false;
        using var cts = new CancellationTokenSource();

        // Act
        var startTask = _service.StartAsync(cts.Token);
        cts.CancelAfter(100); // Cancel quickly since service is disabled
        await Task.WhenAny(startTask, Task.Delay(200));

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.LastStartTime.Should().BeNull();

        // Verify no processing occurred
        _messageParserMock.Verify(
            x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithInvalidConfiguration_UpdatesHealthStatusToUnhealthy()
    {
        // Arrange
        _options.Enabled = true;
        _options.EventHubConnectionString = null; // Invalid config
        _options.EventHubNamespace = null;
        _options.EventHubName = null;

        using var cts = new CancellationTokenSource();

        // Act
        var startTask = _service.StartAsync(cts.Token);
        cts.CancelAfter(100);
        await Task.WhenAny(startTask, Task.Delay(200));

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.IsHealthy.Should().BeFalse();
        healthStatus.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StopAsync_CallsBaseStopAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _service.StopAsync(cts.Token);

        // Assert - Should complete without throwing
        cts.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WaitsPendingOperations_BeforeShuttingDown()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _service.StopAsync(cts.Token);
        stopwatch.Stop();

        // Assert - Should complete quickly when no operations pending
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    #endregion

    #region Message Processing Tests

    [Fact]
    public async Task ProcessEventAsync_WithValidMessage_ParsesAndMapsToSensorThings()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temperature"": 25.5}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            Telemetry = new Dictionary<string, object> { ["temperature"] = 25.5 },
            EnqueuedTime = DateTime.UtcNow
        };

        var processingResult = new MessageProcessingResult
        {
            SuccessCount = 1,
            ObservationsCreated = 1,
            Errors = new List<ProcessingError>()
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesReceived.Should().Be(1);
        healthStatus.TotalMessagesProcessed.Should().Be(1);
        healthStatus.TotalObservationsCreated.Should().Be(1);
        healthStatus.ConsecutiveErrors.Should().Be(0);
        healthStatus.LastMessageTime.Should().NotBeNull();

        _messageParserMock.Verify(
            x => x.ParseMessageAsync(eventData, It.IsAny<CancellationToken>()),
            Times.Once);

        _sensorThingsMapperMock.Verify(
            x => x.ProcessMessagesAsync(
                It.Is<IReadOnlyList<IoTHubMessage>>(m => m.Count == 1 && m[0].DeviceId == "device-001"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_WithBatchMessages_ProcessesAllSuccessfully()
    {
        // Arrange
        var events = new[]
        {
            CreateEventData(@"{""temp"": 20}", "device-001"),
            CreateEventData(@"{""temp"": 21}", "device-002"),
            CreateEventData(@"{""temp"": 22}", "device-003")
        };

        var messages = events.Select((e, i) => new IoTHubMessage
        {
            DeviceId = $"device-{i + 1:D3}",
            Telemetry = new Dictionary<string, object> { ["temp"] = 20 + i },
            EnqueuedTime = DateTime.UtcNow
        }).ToArray();

        var processingResult = new MessageProcessingResult
        {
            SuccessCount = 1,
            ObservationsCreated = 1,
            Errors = new List<ProcessingError>()
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventData e, CancellationToken ct) =>
            {
                var deviceId = e.SystemProperties["iothub-connection-device-id"]?.ToString() ?? "";
                return new IoTHubMessage { DeviceId = deviceId, EnqueuedTime = DateTime.UtcNow };
            });

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act
        foreach (var evt in events)
        {
            var args = CreateProcessEventArgs(evt);
            await InvokeProcessEventHandler(args);
        }

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesReceived.Should().Be(3);
        healthStatus.TotalMessagesProcessed.Should().Be(3);
    }

    [Fact]
    public async Task ProcessEventAsync_WithMalformedMessage_LogsErrorAndContinues()
    {
        // Arrange
        var eventData = CreateEventData(@"{invalid json", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid JSON format"));

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesFailed.Should().Be(1);
        healthStatus.ConsecutiveErrors.Should().Be(1);
        healthStatus.LastError.Should().Contain("Invalid JSON format");

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_WithNullEventData_ReturnsEarly()
    {
        // Arrange
        var processEventArgs = CreateProcessEventArgs(null);

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesReceived.Should().Be(0);

        _messageParserMock.Verify(
            x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessEventAsync_WithTransientError_IncreasesConsecutiveErrorCount()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Transient network error"));

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.ConsecutiveErrors.Should().Be(1);
        healthStatus.TotalMessagesFailed.Should().Be(1);
        healthStatus.LastError.Should().Contain("Transient network error");
    }

    [Fact]
    public async Task ProcessEventAsync_WithPermanentError_SendsToDeadLetterQueue()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        var processingResult = new MessageProcessingResult
        {
            FailureCount = 1,
            Errors = new List<ProcessingError>
            {
                new ProcessingError
                {
                    DeviceId = "device-001",
                    Message = "Device not found in SensorThings API"
                }
            }
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesFailed.Should().Be(1);

        _deadLetterQueueMock.Verify(
            x => x.AddToDeadLetterQueueAsync(
                It.Is<DeadLetterMessage>(m => m.OriginalMessage.DeviceId == "device-001"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessEventAsync_WithConsecutiveFailures_UpdatesHealthStatus()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Processing error"));

        // Act - Simulate MaxConsecutiveErrors + 1 failures
        for (int i = 0; i < _options.ErrorHandling.MaxConsecutiveErrors + 1; i++)
        {
            var args = CreateProcessEventArgs(eventData);
            await InvokeProcessEventHandler(args);
        }

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.IsHealthy.Should().BeFalse();
        healthStatus.ConsecutiveErrors.Should().BeGreaterThan(_options.ErrorHandling.MaxConsecutiveErrors);
    }

    [Fact]
    public async Task ProcessErrorAsync_WithPartitionError_LogsErrorAndUpdatesHealth()
    {
        // Arrange
        var exception = new Exception("Partition processing error");
        var errorArgs = CreateProcessErrorEventArgs("0", "ProcessEvents", exception);

        // Act
        await InvokeProcessErrorHandler(errorArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.ConsecutiveErrors.Should().Be(1);
        healthStatus.LastError.Should().Contain("Partition processing error");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Checkpointing Tests

    [Fact]
    public async Task ProcessEventAsync_AfterBatch_CreatesCheckpoint()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);
        var checkpointCalled = false;

        // Mock UpdateCheckpointAsync
        var updateCheckpointMock = new Mock<Func<CancellationToken, Task>>();
        updateCheckpointMock
            .Setup(x => x(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => checkpointCalled = true);

        processEventArgs.UpdateCheckpointAsync = updateCheckpointMock.Object;

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        var processingResult = new MessageProcessingResult
        {
            SuccessCount = 1,
            ObservationsCreated = 1,
            Errors = new List<ProcessingError>()
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        checkpointCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessEventAsync_WithCheckpointFailure_ContinuesProcessing()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        var updateCheckpointMock = new Mock<Func<CancellationToken, Task>>();
        updateCheckpointMock
            .Setup(x => x(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Checkpoint storage unavailable"));

        processEventArgs.UpdateCheckpointAsync = updateCheckpointMock.Object;

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        var processingResult = new MessageProcessingResult
        {
            SuccessCount = 1,
            ObservationsCreated = 1,
            Errors = new List<ProcessingError>()
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act & Assert - Should not throw
        await InvokeProcessEventHandler(processEventArgs);

        var healthStatus = _service.GetHealthStatus();
        healthStatus.TotalMessagesProcessed.Should().Be(1);
    }

    #endregion

    #region Health Monitoring Tests

    [Fact]
    public async Task HealthCheck_WithRecentMessages_ReturnsHealthy()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");
        var processEventArgs = CreateProcessEventArgs(eventData);

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        var processingResult = new MessageProcessingResult
        {
            SuccessCount = 1,
            ObservationsCreated = 1,
            Errors = new List<ProcessingError>()
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        // Act
        await InvokeProcessEventHandler(processEventArgs);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.IsHealthy.Should().BeTrue();
        healthStatus.LastMessageTime.Should().NotBeNull();
        healthStatus.TimeSinceLastMessage.Should().NotBeNull();
        healthStatus.TimeSinceLastMessage.Value.TotalSeconds.Should().BeLessThan(1);
    }

    [Fact]
    public void HealthCheck_WithNoRecentMessages_ShowsNoMessageTime()
    {
        // Arrange & Act
        var healthStatus = _service.GetHealthStatus();

        // Assert
        healthStatus.LastMessageTime.Should().BeNull();
        healthStatus.TimeSinceLastMessage.Should().BeNull();
    }

    [Fact]
    public async Task HealthCheck_WithConsecutiveErrors_ReturnsDegraded()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        _sensorThingsMapperMock
            .Setup(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Processing error"));

        // Act - Simulate errors but less than max
        for (int i = 0; i < 5; i++)
        {
            var args = CreateProcessEventArgs(eventData);
            await InvokeProcessEventHandler(args);
        }

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.ConsecutiveErrors.Should().Be(5);
        healthStatus.IsHealthy.Should().BeTrue(); // Still healthy, under threshold
        healthStatus.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetHealthStatus_CalculatesSuccessRate_Correctly()
    {
        // Arrange & Act
        var healthStatus = _service.GetHealthStatus();

        // Assert - No messages processed yet
        healthStatus.SuccessRate.Should().Be(0);
    }

    [Fact]
    public async Task HealthCheck_SuccessResetsConsecutiveErrors()
    {
        // Arrange
        var eventData = CreateEventData(@"{""temp"": 25}", "device-001");

        var iotHubMessage = new IoTHubMessage
        {
            DeviceId = "device-001",
            EnqueuedTime = DateTime.UtcNow
        };

        _messageParserMock
            .Setup(x => x.ParseMessageAsync(It.IsAny<EventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iotHubMessage);

        // First call fails
        _sensorThingsMapperMock
            .SetupSequence(x => x.ProcessMessagesAsync(It.IsAny<IReadOnlyList<IoTHubMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Error"))
            .ReturnsAsync(new MessageProcessingResult
            {
                SuccessCount = 1,
                ObservationsCreated = 1,
                Errors = new List<ProcessingError>()
            });

        // Act
        var args1 = CreateProcessEventArgs(eventData);
        await InvokeProcessEventHandler(args1);

        var healthStatusAfterError = _service.GetHealthStatus();
        healthStatusAfterError.ConsecutiveErrors.Should().Be(1);

        var args2 = CreateProcessEventArgs(eventData);
        await InvokeProcessEventHandler(args2);

        // Assert
        var healthStatus = _service.GetHealthStatus();
        healthStatus.ConsecutiveErrors.Should().Be(0); // Reset after success
        healthStatus.TotalMessagesProcessed.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static EventData CreateEventData(string telemetryJson, string deviceId)
    {
        var body = Encoding.UTF8.GetBytes(telemetryJson);
        var eventData = new EventData(body);

        eventData.SystemProperties.Add("iothub-connection-device-id", deviceId);
        eventData.SystemProperties.Add("iothub-enqueuedtime", DateTime.UtcNow);

        return eventData;
    }

    private static ProcessEventArgs CreateProcessEventArgs(EventData? eventData)
    {
        var partitionContext = Mock.Of<PartitionContext>(p =>
            p.PartitionId == "0");

        var args = new ProcessEventArgs(
            partitionContext,
            eventData,
            ct => Task.CompletedTask,
            CancellationToken.None);

        return args;
    }

    private static ProcessErrorEventArgs CreateProcessErrorEventArgs(
        string partitionId,
        string operation,
        Exception exception)
    {
        var args = new ProcessErrorEventArgs(
            partitionId,
            operation,
            exception,
            CancellationToken.None);

        return args;
    }

    private async Task InvokeProcessEventHandler(ProcessEventArgs args)
    {
        // Use reflection to invoke the private ProcessEventHandler method
        var method = typeof(AzureIoTHubConsumerService).GetMethod(
            "ProcessEventHandler",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull("ProcessEventHandler method should exist");

        var task = (Task)method!.Invoke(_service, new object[] { args })!;
        await task;
    }

    private async Task InvokeProcessErrorHandler(ProcessErrorEventArgs args)
    {
        // Use reflection to invoke the private ProcessErrorHandler method
        var method = typeof(AzureIoTHubConsumerService).GetMethod(
            "ProcessErrorHandler",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull("ProcessErrorHandler method should exist");

        var task = (Task)method!.Invoke(_service, new object[] { args })!;
        await task;
    }

    #endregion
}
