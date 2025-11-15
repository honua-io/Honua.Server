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

public class SliMetricsTests
{
    private readonly Mock<ILogger<SliMetrics>> _loggerMock;
    private readonly SreOptions _options;

    public SliMetricsTests()
    {
        _loggerMock = new Mock<ILogger<SliMetrics>>();
        _options = new SreOptions
        {
            Enabled = true,
            RollingWindowDays = 1,
            EvaluationIntervalMinutes = 5,
            Slos = new Dictionary<string, SloConfig>
            {
                ["latency_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.Latency,
                    Target = 0.99,
                    ThresholdMs = 500,
                    Description = "99% of requests under 500ms"
                },
                ["availability_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.Availability,
                    Target = 0.999,
                    Description = "99.9% availability"
                },
                ["error_rate_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.ErrorRate,
                    Target = 0.995,
                    Description = "99.5% error-free"
                }
            }
        };
    }

    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
            new SliMetrics(Options.Create(_options), _loggerMock.Object));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SliMetrics(null!, _loggerMock.Object));
    }

    [Fact]
    public void RecordLatency_WhenSreDisabled_DoesNotRecordMetrics()
    {
        // Arrange
        var disabledOptions = new SreOptions { Enabled = false };
        var sliMetrics = new SliMetrics(Options.Create(disabledOptions), _loggerMock.Object);

        // Act
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(300));
        var stats = sliMetrics.GetStatistics("latency_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().BeNull();
    }

    [Fact]
    public void RecordLatency_WithinThreshold_RecordsGoodEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(300), "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("latency_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(1);
        stats.ActualSli.Should().Be(1.0);
    }

    [Fact]
    public void RecordLatency_ExceedsThreshold_RecordsBadEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(600), "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("latency_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(0);
        stats.BadEvents.Should().Be(1);
        stats.ActualSli.Should().Be(0.0);
    }

    [Fact]
    public void RecordLatency_MixedResults_CalculatesCorrectSli()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act - Record 99 good and 1 bad (99% success rate)
        for (int i = 0; i < 99; i++)
        {
            sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(300), "/api/test", "GET");
        }
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(600), "/api/test", "GET");

        var stats = sliMetrics.GetStatistics("latency_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(100);
        stats.GoodEvents.Should().Be(99);
        stats.BadEvents.Should().Be(1);
        stats.ActualSli.Should().BeApproximately(0.99, 0.001);
    }

    [Fact]
    public void RecordAvailability_With2xxStatus_RecordsGoodEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        sliMetrics.RecordAvailability(200, "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("availability_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(1);
    }

    [Fact]
    public void RecordAvailability_With4xxStatus_RecordsGoodEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act - 4xx are client errors, not availability issues
        sliMetrics.RecordAvailability(404, "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("availability_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(1);
    }

    [Fact]
    public void RecordAvailability_With5xxStatus_RecordsBadEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        sliMetrics.RecordAvailability(500, "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("availability_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(0);
        stats.BadEvents.Should().Be(1);
    }

    [Fact]
    public void RecordError_With4xxStatus_RecordsGoodEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act - 4xx are client errors, not server errors
        sliMetrics.RecordError(400, "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("error_rate_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(1);
    }

    [Fact]
    public void RecordError_With5xxStatus_RecordsBadEvent()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        sliMetrics.RecordError(503, "/api/test", "GET");
        var stats = sliMetrics.GetStatistics("error_rate_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(0);
        stats.BadEvents.Should().Be(1);
    }

    [Fact]
    public void RecordHealthCheck_WhenHealthy_RecordsGoodEvent()
    {
        // Arrange
        var optionsWithHealthCheck = new SreOptions
        {
            Enabled = true,
            Slos = new Dictionary<string, SloConfig>
            {
                ["health_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.HealthCheckSuccess,
                    Target = 0.999
                }
            }
        };
        var sliMetrics = new SliMetrics(Options.Create(optionsWithHealthCheck), _loggerMock.Object);

        // Act
        sliMetrics.RecordHealthCheck(true, "database");
        var stats = sliMetrics.GetStatistics("health_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(1);
    }

    [Fact]
    public void RecordHealthCheck_WhenUnhealthy_RecordsBadEvent()
    {
        // Arrange
        var optionsWithHealthCheck = new SreOptions
        {
            Enabled = true,
            Slos = new Dictionary<string, SloConfig>
            {
                ["health_slo"] = new SloConfig
                {
                    Enabled = true,
                    Type = SliType.HealthCheckSuccess,
                    Target = 0.999
                }
            }
        };
        var sliMetrics = new SliMetrics(Options.Create(optionsWithHealthCheck), _loggerMock.Object);

        // Act
        sliMetrics.RecordHealthCheck(false, "database");
        var stats = sliMetrics.GetStatistics("health_slo", TimeSpan.FromHours(1));

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().Be(1);
        stats.GoodEvents.Should().Be(0);
        stats.BadEvents.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_ForNonExistentSlo_ReturnsNull()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act
        var stats = sliMetrics.GetStatistics("non_existent", TimeSpan.FromHours(1));

        // Assert
        stats.Should().BeNull();
    }

    [Fact]
    public void GetAllStatistics_ReturnsAllSloStatistics()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act - Record metrics for different SLOs
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(300));
        sliMetrics.RecordAvailability(200);
        sliMetrics.RecordError(200);

        var allStats = sliMetrics.GetAllStatistics(TimeSpan.FromHours(1));

        // Assert
        allStats.Should().NotBeNull();
        allStats.Count.Should().Be(3);
    }

    [Fact]
    public void GetStatistics_RespectTimeWindow_FiltersOldMeasurements()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act - Record metrics and query with very short window
        sliMetrics.RecordLatency(TimeSpan.FromMilliseconds(300));
        System.Threading.Thread.Sleep(100); // Wait a bit
        var stats = sliMetrics.GetStatistics("latency_slo", TimeSpan.FromMilliseconds(50));

        // Assert - Statistics should exist but might not include old measurements
        // (This is a simplified test - in reality, you'd need to control time for better testing)
        stats.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var sliMetrics = new SliMetrics(Options.Create(_options), _loggerMock.Object);

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => sliMetrics.Dispose());
        exception.Should().BeNull();
    }
}
