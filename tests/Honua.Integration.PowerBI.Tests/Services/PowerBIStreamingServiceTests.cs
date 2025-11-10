// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Honua.Integration.PowerBI.Configuration;
using Honua.Integration.PowerBI.Services;
using Honua.Server.Enterprise.Sensors.Models;
using System.Text.Json;
using Xunit;

namespace Honua.Integration.PowerBI.Tests.Services;

public class PowerBIStreamingServiceTests : IDisposable
{
    private readonly Mock<IPowerBIDatasetService> _datasetServiceMock;
    private readonly Mock<ILogger<PowerBIStreamingService>> _loggerMock;
    private readonly PowerBIOptions _options;
    private readonly PowerBIStreamingService _service;

    public PowerBIStreamingServiceTests()
    {
        _datasetServiceMock = new Mock<IPowerBIDatasetService>();
        _loggerMock = new Mock<ILogger<PowerBIStreamingService>>();

        _options = new PowerBIOptions
        {
            TenantId = "test-tenant-id",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            WorkspaceId = "test-workspace-id",
            EnablePushDatasets = true,
            StreamingBatchSize = 100,
            PushDatasetRateLimitPerHour = 10000,
            StreamingDatasets = new List<PowerBIStreamingDatasetConfig>
            {
                new PowerBIStreamingDatasetConfig
                {
                    Name = "Test Observations",
                    DatasetId = "test-dataset-id",
                    SourceType = "Observations",
                    DatastreamIds = new List<string> { "datastream-1", "datastream-2" },
                    AutoStream = true
                },
                new PowerBIStreamingDatasetConfig
                {
                    Name = "Test Alerts",
                    DatasetId = "test-alerts-dataset-id",
                    SourceType = "Alerts",
                    DatastreamIds = new List<string>(),
                    AutoStream = true
                }
            }
        };

        _service = new PowerBIStreamingService(_options, _datasetServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _service.StopAutoStreamingAsync().Wait();
        GC.SuppressFinalize(this);
    }

    #region Streaming Single Observation Tests

    [Fact]
    public async Task StreamObservationAsync_WithValidData_StreamsSuccessfully()
    {
        // Arrange
        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("datastream-1", "Temperature");

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StreamObservationAsync(observation, datastream);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                "test-dataset-id",
                "Observations",
                It.Is<IEnumerable<object>>(rows => rows.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamObservationAsync_WithDisabledPushDatasets_DoesNotStream()
    {
        // Arrange
        var disabledOptions = new PowerBIOptions
        {
            EnablePushDatasets = false,
            StreamingDatasets = _options.StreamingDatasets
        };
        var service = new PowerBIStreamingService(disabledOptions, _datasetServiceMock.Object, _loggerMock.Object);

        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("datastream-1", "Temperature");

        // Act
        await service.StreamObservationAsync(observation, datastream);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamObservationAsync_WithNoMatchingDatastream_DoesNotStream()
    {
        // Arrange
        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("unknown-datastream", "Temperature");

        // Act
        await _service.StreamObservationAsync(observation, datastream);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamObservationAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("datastream-1", "Temperature");

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Power BI API Error"));

        // Act - Should not throw
        await _service.StreamObservationAsync(observation, datastream);

        // Assert - Error should be logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error streaming observation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamObservationAsync_WithDifferentResultTypes_ConvertsCorrectly()
    {
        // Arrange
        var observationDouble = CreateTestObservation("obs-1", 25.5);
        var observationInt = CreateTestObservation("obs-2", 30);
        var observationString = CreateTestObservation("obs-3", "15.7");
        var datastream = CreateTestDatastream("datastream-1", "Temperature");

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StreamObservationAsync(observationDouble, datastream);
        await _service.StreamObservationAsync(observationInt, datastream);
        await _service.StreamObservationAsync(observationString, datastream);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    #endregion

    #region Streaming Batch Observations Tests

    [Fact]
    public async Task StreamObservationsAsync_WithValidData_StreamsBatch()
    {
        // Arrange
        var observations = new List<(Observation Observation, Datastream Datastream)>
        {
            (CreateTestObservation("obs-1", 25.5), CreateTestDatastream("datastream-1", "Temperature")),
            (CreateTestObservation("obs-2", 26.3), CreateTestDatastream("datastream-1", "Temperature")),
            (CreateTestObservation("obs-3", 24.8), CreateTestDatastream("datastream-2", "Humidity"))
        };

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StreamObservationsAsync(observations);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                "test-dataset-id",
                "Observations",
                It.Is<IEnumerable<object>>(rows => rows.Count() == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamObservationsAsync_WithEmptyList_DoesNotStream()
    {
        // Arrange
        var observations = new List<(Observation Observation, Datastream Datastream)>();

        // Act
        await _service.StreamObservationsAsync(observations);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamObservationsAsync_WithMultipleDatasets_GroupsCorrectly()
    {
        // Arrange
        var additionalOptions = new PowerBIOptions
        {
            EnablePushDatasets = true,
            StreamingBatchSize = 100,
            StreamingDatasets = new List<PowerBIStreamingDatasetConfig>
            {
                new PowerBIStreamingDatasetConfig
                {
                    DatasetId = "dataset-1",
                    SourceType = "Observations",
                    DatastreamIds = new List<string> { "datastream-1" }
                },
                new PowerBIStreamingDatasetConfig
                {
                    DatasetId = "dataset-2",
                    SourceType = "Observations",
                    DatastreamIds = new List<string> { "datastream-2" }
                }
            }
        };
        var service = new PowerBIStreamingService(additionalOptions, _datasetServiceMock.Object, _loggerMock.Object);

        var observations = new List<(Observation Observation, Datastream Datastream)>
        {
            (CreateTestObservation("obs-1", 25.5), CreateTestDatastream("datastream-1", "Temperature")),
            (CreateTestObservation("obs-2", 26.3), CreateTestDatastream("datastream-1", "Temperature")),
            (CreateTestObservation("obs-3", 60.0), CreateTestDatastream("datastream-2", "Humidity"))
        };

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.StreamObservationsAsync(observations);

        // Assert - Should call twice, once for each dataset
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                "dataset-1",
                "Observations",
                It.Is<IEnumerable<object>>(rows => rows.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                "dataset-2",
                "Observations",
                It.Is<IEnumerable<object>>(rows => rows.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamObservationsAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var observations = new List<(Observation Observation, Datastream Datastream)>
        {
            (CreateTestObservation("obs-1", 25.5), CreateTestDatastream("datastream-1", "Temperature"))
        };

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Power BI API Error"));

        // Act - Should not throw
        await _service.StreamObservationsAsync(observations);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error streaming observation batch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task StreamObservationAsync_WithMultipleConcurrentCalls_RespectsRateLimit()
    {
        // Arrange
        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("datastream-1", "Temperature");

        var callCount = 0;
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                lock (lockObj)
                {
                    currentConcurrent++;
                    maxConcurrent = Math.Max(maxConcurrent, currentConcurrent);
                    callCount++;
                }
                await Task.Delay(10);
                lock (lockObj)
                {
                    currentConcurrent--;
                }
            });

        // Act - Fire 20 concurrent requests
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => _service.StreamObservationAsync(observation, datastream))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Should have limited concurrent requests (max 15 based on semaphore)
        maxConcurrent.Should().BeLessOrEqualTo(15);
        callCount.Should().Be(20);
    }

    [Fact]
    public async Task StreamObservationsAsync_WithLargeVolume_HandlesRateLimit()
    {
        // Arrange - Create observations that would exceed hourly rate limit
        var observations = Enumerable.Range(0, 100)
            .Select(i => (
                CreateTestObservation($"obs-{i}", i * 1.5),
                CreateTestDatastream("datastream-1", "Temperature")))
            .ToList();

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StreamObservationsAsync(observations);

        // Assert - Should batch and respect limits
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // All grouped into one call
    }

    #endregion

    #region Anomaly Alert Tests

    [Fact]
    public async Task StreamAnomalyAlertAsync_WithValidData_StreamsAlert()
    {
        // Arrange
        var datastreamId = "datastream-1";
        var observedValue = 35.5;
        var expectedValue = 25.0;
        var threshold = 5.0;
        var timestamp = DateTimeOffset.UtcNow;

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StreamAnomalyAlertAsync(datastreamId, observedValue, expectedValue, threshold, timestamp);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                "test-alerts-dataset-id",
                "Alerts",
                It.Is<IEnumerable<object>>(rows => rows.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreamAnomalyAlertAsync_CalculatesSeverityCorrectly()
    {
        // Arrange
        var datastreamId = "datastream-1";
        var timestamp = DateTimeOffset.UtcNow;

        object? capturedRow = null;
        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, IEnumerable<object>, CancellationToken>((_, _, rows, _) =>
            {
                capturedRow = rows.First();
            })
            .Returns(Task.CompletedTask);

        // Act - Critical severity (>= 50% deviation)
        await _service.StreamAnomalyAlertAsync(datastreamId, 150.0, 100.0, 10.0, timestamp);

        // Assert
        capturedRow.Should().NotBeNull();
        var severity = capturedRow!.GetType().GetProperty("Severity")?.GetValue(capturedRow) as string;
        severity.Should().Be("Critical");
    }

    [Fact]
    public async Task StreamAnomalyAlertAsync_WithNoAlertsDataset_DoesNotStream()
    {
        // Arrange
        var optionsWithoutAlerts = new PowerBIOptions
        {
            EnablePushDatasets = true,
            StreamingDatasets = new List<PowerBIStreamingDatasetConfig>
            {
                new PowerBIStreamingDatasetConfig
                {
                    DatasetId = "test-dataset-id",
                    SourceType = "Observations",
                    DatastreamIds = new List<string> { "datastream-1" }
                }
            }
        };
        var service = new PowerBIStreamingService(optionsWithoutAlerts, _datasetServiceMock.Object, _loggerMock.Object);

        // Act
        await service.StreamAnomalyAlertAsync("datastream-1", 35.5, 25.0, 5.0, DateTimeOffset.UtcNow);

        // Assert
        _datasetServiceMock.Verify(
            x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamAnomalyAlertAsync_WithException_DoesNotThrow()
    {
        // Arrange
        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Power BI API Error"));

        // Act - Should not throw
        await _service.StreamAnomalyAlertAsync("datastream-1", 35.5, 25.0, 5.0, DateTimeOffset.UtcNow);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error streaming anomaly alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Auto Streaming Tests

    [Fact]
    public async Task StartAutoStreamingAsync_StartsStreaming()
    {
        // Arrange & Act
        await _service.StartAutoStreamingAsync();

        // Give it a moment to start
        await Task.Delay(100);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Started Power BI auto-streaming")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Cleanup
        await _service.StopAutoStreamingAsync();
    }

    [Fact]
    public async Task StartAutoStreamingAsync_WhenAlreadyRunning_LogsWarning()
    {
        // Arrange
        await _service.StartAutoStreamingAsync();
        await Task.Delay(100);

        // Act - Start again
        await _service.StartAutoStreamingAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Auto-streaming is already running")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Cleanup
        await _service.StopAutoStreamingAsync();
    }

    [Fact]
    public async Task StopAutoStreamingAsync_StopsStreaming()
    {
        // Arrange
        await _service.StartAutoStreamingAsync();
        await Task.Delay(100);

        // Act
        await _service.StopAutoStreamingAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopped Power BI auto-streaming")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAutoStreamingAsync_WhenNotRunning_DoesNotThrow()
    {
        // Act - Stop without starting
        await _service.StopAutoStreamingAsync();

        // Assert - Should not throw
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopped Power BI auto-streaming")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void PowerBIStreamingService_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PowerBIStreamingService(null!, _datasetServiceMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void PowerBIStreamingService_WithNullDatasetService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PowerBIStreamingService(_options, null!, _loggerMock.Object));
    }

    [Fact]
    public void PowerBIStreamingService_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PowerBIStreamingService(_options, _datasetServiceMock.Object, null!));
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task StreamObservationAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var observation = CreateTestObservation("obs-1", 25.5);
        var datastream = CreateTestDatastream("datastream-1", "Temperature");
        var cts = new CancellationTokenSource();

        _datasetServiceMock
            .Setup(x => x.PushRowsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        cts.Cancel();

        // Act - Should handle cancellation gracefully
        await _service.StreamObservationAsync(observation, datastream, cts.Token);

        // Assert - Error should be logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private Observation CreateTestObservation(string id, object result)
    {
        return new Observation
        {
            Id = id,
            Result = result,
            ResultTime = DateTimeOffset.UtcNow,
            PhenomenonTime = DateTimeOffset.UtcNow,
            DatastreamId = "datastream-1"
        };
    }

    private Datastream CreateTestDatastream(string id, string name)
    {
        return new Datastream
        {
            Id = id,
            Name = name,
            Description = $"{name} sensor",
            ObservedPropertyId = "temperature",
            UnitOfMeasurement = new UnitOfMeasurement
            {
                Name = "Celsius",
                Symbol = "°C",
                Definition = "http://example.org/celsius"
            }
        };
    }

    #endregion
}
