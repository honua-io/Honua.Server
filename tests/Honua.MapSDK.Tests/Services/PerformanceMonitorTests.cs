// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Logging;
using Honua.MapSDK.Services.Performance;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.Services;

/// <summary>
/// Tests for PerformanceMonitor with focus on interop measurement capabilities.
/// </summary>
public class PerformanceMonitorTests
{
    private readonly Mock<MapSdkLogger> _mockLogger;

    public PerformanceMonitorTests()
    {
        _mockLogger = new Mock<MapSdkLogger>();
    }

    [Fact]
    public async Task MeasureInteropAsync_TracksTimeAndMemory()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);
        var expectedResult = 42;

        // Act
        var result = await monitor.MeasureInteropAsync("TestOperation", async () =>
        {
            await Task.Delay(10);
            return expectedResult;
        });

        // Assert
        Assert.Equal(expectedResult, result);

        // Verify that measurement was recorded
        var stats = monitor.GetStatistics("TestOperation");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Count);
        Assert.True(stats.Min >= 0);
    }

    [Fact]
    public async Task MeasureInteropAsync_VoidOverload_TracksTimeAndMemory()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);
        var called = false;

        // Act
        await monitor.MeasureInteropAsync("TestVoidOperation", async () =>
        {
            await Task.Delay(10);
            called = true;
        });

        // Assert
        Assert.True(called);

        var stats = monitor.GetStatistics("TestVoidOperation");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Count);
    }

    [Fact]
    public async Task MeasureInteropAsync_WhenDisabled_DoesNotTrack()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: false);

        // Act
        var result = await monitor.MeasureInteropAsync("TestOperation", async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        // Assert
        Assert.Equal(42, result);

        var stats = monitor.GetStatistics("TestOperation");
        Assert.Null(stats);
    }

    [Fact]
    public async Task MeasureInteropAsync_OnException_LogsError()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);
        var expectedException = new InvalidOperationException("Test error");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await monitor.MeasureInteropAsync<int>("TestOperation", () => throw expectedException)
        );

        Assert.Equal(expectedException, exception);
        _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task MeasureAsync_MultipleOperations_TracksStatistics()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);

        // Act - Run operation multiple times
        for (int i = 0; i < 10; i++)
        {
            await monitor.MeasureAsync("TestOperation", async () =>
            {
                await Task.Delay(1);
                return i;
            });
        }

        // Assert
        var stats = monitor.GetStatistics("TestOperation");
        Assert.NotNull(stats);
        Assert.Equal(10, stats.Count);
        Assert.True(stats.Average > 0);
        Assert.True(stats.Min <= stats.Average);
        Assert.True(stats.Max >= stats.Average);
    }

    [Fact]
    public void GetAllStatistics_ReturnsAllMeasurements()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);

        monitor.RecordMeasurement("Operation1", 100);
        monitor.RecordMeasurement("Operation2", 200);
        monitor.RecordMeasurement("Operation3", 300);

        // Act
        var allStats = monitor.GetAllStatistics();

        // Assert
        Assert.Equal(3, allStats.Count);
        Assert.Contains("Operation1", allStats.Keys);
        Assert.Contains("Operation2", allStats.Keys);
        Assert.Contains("Operation3", allStats.Keys);
    }

    [Fact]
    public void Clear_RemovesAllMeasurements()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);
        monitor.RecordMeasurement("Operation1", 100);
        monitor.RecordMeasurement("Operation2", 200);

        // Act
        monitor.Clear();

        // Assert
        var allStats = monitor.GetAllStatistics();
        Assert.Empty(allStats);
    }

    [Fact]
    public void Measure_Disposable_RecordsMeasurement()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);

        // Act
        using (monitor.Measure("TestOperation"))
        {
            Thread.Sleep(10);
        }

        // Assert
        var stats = monitor.GetStatistics("TestOperation");
        Assert.NotNull(stats);
        Assert.Equal(1, stats.Count);
        Assert.True(stats.Min >= 10);
    }

    [Fact]
    public void RecordMeasurement_CalculatesCorrectStatistics()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);

        // Act
        monitor.RecordMeasurement("TestOperation", 100);
        monitor.RecordMeasurement("TestOperation", 200);
        monitor.RecordMeasurement("TestOperation", 300);
        monitor.RecordMeasurement("TestOperation", 400);
        monitor.RecordMeasurement("TestOperation", 500);

        // Assert
        var stats = monitor.GetStatistics("TestOperation");
        Assert.NotNull(stats);
        Assert.Equal(5, stats.Count);
        Assert.Equal(100, stats.Min);
        Assert.Equal(500, stats.Max);
        Assert.Equal(300, stats.Average);
        Assert.Equal(300, stats.Median);
        Assert.Equal(1500, stats.Total);
    }

    [Fact]
    public void Dispose_LogsReport()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);
        monitor.RecordMeasurement("TestOperation", 100);

        // Act
        monitor.Dispose();

        // Assert
        _mockLogger.Verify(l => l.Info(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MeasureInteropAsync_WithLargeData_TracksMemoryIncrease()
    {
        // Arrange
        var monitor = new PerformanceMonitor(_mockLogger.Object, enabled: true);

        // Act
        await monitor.MeasureInteropAsync("LargeDataOperation", async () =>
        {
            // Allocate some memory to simulate interop data
            var largeArray = new byte[1024 * 1024]; // 1MB
            await Task.Delay(1);
            GC.KeepAlive(largeArray);
        });

        // Assert
        var stats = monitor.GetStatistics("LargeDataOperation");
        Assert.NotNull(stats);
    }
}
