// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.Sensors.AnomalyDetection.Configuration;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Data;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Models;
using Honua.Server.Enterprise.Sensors.AnomalyDetection.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Sensors.AnomalyDetection;

public sealed class StaleSensorDetectionServiceTests
{
    private readonly Mock<IAnomalyDetectionRepository> _mockRepository;
    private readonly Mock<ILogger<StaleSensorDetectionService>> _mockLogger;
    private readonly StaleSensorDetectionService _service;

    public StaleSensorDetectionServiceTests()
    {
        _mockRepository = new Mock<IAnomalyDetectionRepository>();
        _mockLogger = new Mock<ILogger<StaleSensorDetectionService>>();

        var options = Options.Create(new AnomalyDetectionOptions
        {
            Enabled = true,
            StaleSensorDetection = new StaleSensorDetectionOptions
            {
                Enabled = true,
                StaleThreshold = TimeSpan.FromHours(1)
            }
        });

        _service = new StaleSensorDetectionService(
            _mockRepository.Object,
            Options.Create(options.Value),
            _mockLogger.Object);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_WhenDisabled_ReturnsEmpty()
    {
        // Arrange
        var options = Options.Create(new AnomalyDetectionOptions
        {
            Enabled = true,
            StaleSensorDetection = new StaleSensorDetectionOptions
            {
                Enabled = false
            }
        });

        var service = new StaleSensorDetectionService(
            _mockRepository.Object,
            Options.Create(options.Value),
            _mockLogger.Object);

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Empty(result);
        _mockRepository.Verify(
            r => r.GetStaleDatastreamsAsync(It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_WhenNoStaleDatastreams_ReturnsEmpty()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetStaleDatastreamsAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StaleDatastreamInfo>());

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_DetectsStaleDatastreams()
    {
        // Arrange
        var staleDatastreams = new List<StaleDatastreamInfo>
        {
            new()
            {
                DatastreamId = "ds-1",
                DatastreamName = "Temperature Sensor 1",
                ThingId = "thing-1",
                ThingName = "Weather Station A",
                SensorId = "sensor-1",
                SensorName = "Temp Sensor",
                ObservedPropertyName = "temperature",
                LastObservationTime = DateTime.UtcNow - TimeSpan.FromHours(2),
                TimeSinceLastObservation = TimeSpan.FromHours(2)
            }
        };

        _mockRepository
            .Setup(r => r.GetStaleDatastreamsAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDatastreams);

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Single(result);
        var anomaly = result[0];
        Assert.Equal(AnomalyType.StaleSensor, anomaly.Type);
        Assert.Equal("ds-1", anomaly.DatastreamId);
        Assert.Equal("Temperature Sensor 1", anomaly.DatastreamName);
        Assert.NotNull(anomaly.LastObservationTime);
        Assert.Equal(TimeSpan.FromHours(2), anomaly.TimeSinceLastObservation);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_DetectsOfflineSensors()
    {
        // Arrange
        var staleDatastreams = new List<StaleDatastreamInfo>
        {
            new()
            {
                DatastreamId = "ds-1",
                DatastreamName = "Temperature Sensor 1",
                ThingId = "thing-1",
                ThingName = "Weather Station A",
                SensorId = "sensor-1",
                SensorName = "Temp Sensor",
                ObservedPropertyName = "temperature",
                LastObservationTime = null, // Never reported
                TimeSinceLastObservation = TimeSpan.MaxValue
            }
        };

        _mockRepository
            .Setup(r => r.GetStaleDatastreamsAsync(
                It.IsAny<TimeSpan>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDatastreams);

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Single(result);
        var anomaly = result[0];
        Assert.Equal(AnomalyType.SensorOffline, anomaly.Type);
        Assert.Null(anomaly.LastObservationTime);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_AppliesCorrectSeverity()
    {
        // Arrange
        var threshold = TimeSpan.FromHours(1);
        var staleDatastreams = new List<StaleDatastreamInfo>
        {
            // Critical: > 2x threshold
            new()
            {
                DatastreamId = "ds-1",
                DatastreamName = "Sensor 1",
                ThingId = "thing-1",
                ThingName = "Thing 1",
                SensorId = "sensor-1",
                SensorName = "S1",
                ObservedPropertyName = "temperature",
                LastObservationTime = DateTime.UtcNow - TimeSpan.FromHours(3),
                TimeSinceLastObservation = TimeSpan.FromHours(3)
            },
            // Warning: > 1.5x threshold
            new()
            {
                DatastreamId = "ds-2",
                DatastreamName = "Sensor 2",
                ThingId = "thing-2",
                ThingName = "Thing 2",
                SensorId = "sensor-2",
                SensorName = "S2",
                ObservedPropertyName = "temperature",
                LastObservationTime = DateTime.UtcNow - TimeSpan.FromHours(1.6),
                TimeSinceLastObservation = TimeSpan.FromHours(1.6)
            },
            // Info: just past threshold
            new()
            {
                DatastreamId = "ds-3",
                DatastreamName = "Sensor 3",
                ThingId = "thing-3",
                ThingName = "Thing 3",
                SensorId = "sensor-3",
                SensorName = "S3",
                ObservedPropertyName = "temperature",
                LastObservationTime = DateTime.UtcNow - TimeSpan.FromHours(1.1),
                TimeSinceLastObservation = TimeSpan.FromHours(1.1)
            }
        };

        _mockRepository
            .Setup(r => r.GetStaleDatastreamsAsync(threshold, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDatastreams);

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(AnomalySeverity.Critical, result[0].Severity);
        Assert.Equal(AnomalySeverity.Warning, result[1].Severity);
        Assert.Equal(AnomalySeverity.Info, result[2].Severity);
    }

    [Fact]
    public async Task DetectStaleDatastreamsAsync_RespectsThresholdOverrides()
    {
        // Arrange
        var options = Options.Create(new AnomalyDetectionOptions
        {
            Enabled = true,
            StaleSensorDetection = new StaleSensorDetectionOptions
            {
                Enabled = true,
                StaleThreshold = TimeSpan.FromHours(1),
                ThresholdOverrides = new Dictionary<string, TimeSpan>
                {
                    ["traffic_count"] = TimeSpan.FromMinutes(30)
                }
            }
        });

        var service = new StaleSensorDetectionService(
            _mockRepository.Object,
            Options.Create(options.Value),
            _mockLogger.Object);

        var staleDatastreams = new List<StaleDatastreamInfo>
        {
            new()
            {
                DatastreamId = "ds-1",
                DatastreamName = "Traffic Counter",
                ThingId = "thing-1",
                ThingName = "Traffic Station",
                SensorId = "sensor-1",
                SensorName = "Counter",
                ObservedPropertyName = "traffic_count",
                LastObservationTime = DateTime.UtcNow - TimeSpan.FromMinutes(45),
                TimeSinceLastObservation = TimeSpan.FromMinutes(45)
            }
        };

        _mockRepository
            .Setup(r => r.GetStaleDatastreamsAsync(
                TimeSpan.FromHours(1),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleDatastreams);

        // Act
        var result = await service.DetectStaleDatastreamsAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("traffic_count", result[0].Metadata!["threshold"].ToString()!);
    }
}
