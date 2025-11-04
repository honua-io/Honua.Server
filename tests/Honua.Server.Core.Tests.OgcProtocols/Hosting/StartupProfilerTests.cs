// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Honua.Server.Core.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class StartupProfilerTests
{
    [Fact]
    public void Checkpoint_RecordsCheckpoint()
    {
        // Arrange
        var profiler = new StartupProfiler();

        // Act
        profiler.Checkpoint("First checkpoint");

        // Assert
        var checkpoints = profiler.Checkpoints;
        checkpoints.Should().HaveCount(1);
        checkpoints[0].Name.Should().Be("First checkpoint");
    }

    [Fact]
    public void Checkpoint_RecordsMultipleCheckpoints()
    {
        // Arrange
        var profiler = new StartupProfiler();

        // Act
        profiler.Checkpoint("Step 1");
        profiler.Checkpoint("Step 2");
        profiler.Checkpoint("Step 3");

        // Assert
        var checkpoints = profiler.Checkpoints;
        checkpoints.Should().HaveCount(3);
        checkpoints[0].Name.Should().Be("Step 1");
        checkpoints[1].Name.Should().Be("Step 2");
        checkpoints[2].Name.Should().Be("Step 3");
    }

    [Fact]
    public void Checkpoint_RecordsIncreasingTimestamps()
    {
        // Arrange
        var profiler = new StartupProfiler();

        // Act
        profiler.Checkpoint("First");
        Thread.Sleep(50);
        profiler.Checkpoint("Second");
        Thread.Sleep(50);
        profiler.Checkpoint("Third");

        // Assert
        var checkpoints = profiler.Checkpoints;
        checkpoints.Should().HaveCount(3);
        checkpoints[0].ElapsedMs.Should().BeLessThan(checkpoints[1].ElapsedMs);
        checkpoints[1].ElapsedMs.Should().BeLessThan(checkpoints[2].ElapsedMs);
    }

    [Fact]
    public void ElapsedMilliseconds_ReturnsCurrentElapsedTime()
    {
        // Arrange
        var profiler = new StartupProfiler();
        Thread.Sleep(10);

        // Act
        var elapsed = profiler.ElapsedMilliseconds;

        // Assert
        elapsed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Checkpoints_ReturnsOrderedList()
    {
        // Arrange
        var profiler = new StartupProfiler();

        // Act - Add checkpoints with delays to ensure different timestamps
        profiler.Checkpoint("First");
        Thread.Sleep(10);
        profiler.Checkpoint("Second");
        Thread.Sleep(10);
        profiler.Checkpoint("Third");

        // Assert
        var checkpoints = profiler.Checkpoints;
        checkpoints.Should().BeInAscendingOrder(c => c.ElapsedMs);
    }

    [Fact]
    public void LogResults_LogsAllCheckpoints()
    {
        // Arrange
        var profiler = new StartupProfiler();
        profiler.Checkpoint("Step 1");
        Thread.Sleep(20);
        profiler.Checkpoint("Step 2");

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert - Should log multiple times (header, total, checkpoints, slowest)
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public void LogResults_LogsTotalStartupTime()
    {
        // Arrange
        var profiler = new StartupProfiler();
        Thread.Sleep(50);

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total startup time")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogResults_LogsProcessRuntime()
    {
        // Arrange
        var profiler = new StartupProfiler();

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Process runtime")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogResults_LogsCheckpointTimings()
    {
        // Arrange
        var profiler = new StartupProfiler();
        profiler.Checkpoint("Test checkpoint");

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Checkpoint timings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogResults_LogsSlowestOperations()
    {
        // Arrange
        var profiler = new StartupProfiler();
        profiler.Checkpoint("Fast");
        Thread.Sleep(10);
        profiler.Checkpoint("Slow");
        Thread.Sleep(100);
        profiler.Checkpoint("Medium");
        Thread.Sleep(50);

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slowest operations")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogResults_WithNoCheckpoints_DoesNotLogCheckpointTimings()
    {
        // Arrange
        var profiler = new StartupProfiler();

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert - Should not log checkpoint timings when there are none
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Checkpoint timings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void LogResults_WithOneCheckpoint_DoesNotLogSlowestOperations()
    {
        // Arrange
        var profiler = new StartupProfiler();
        profiler.Checkpoint("Only checkpoint");

        var loggerMock = new Mock<ILogger>();

        // Act
        profiler.LogResults(loggerMock.Object);

        // Assert - Need at least 2 checkpoints to calculate deltas
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Slowest operations")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void CheckpointTiming_RecordConstructorValues()
    {
        // Arrange & Act
        var timing = new StartupProfiler.CheckpointTiming("Test", 123);

        // Assert
        timing.Name.Should().Be("Test");
        timing.ElapsedMs.Should().Be(123);
    }

    [Fact]
    public void Checkpoints_IsThreadSafe()
    {
        // Arrange
        var profiler = new StartupProfiler();
        const int threadCount = 10;
        const int checkpointsPerThread = 100;

        // Act - Create checkpoints from multiple threads
        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            var threadIndex = i;
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < checkpointsPerThread; j++)
                {
                    profiler.Checkpoint($"Thread{threadIndex}-Checkpoint{j}");
                }
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        // Assert
        var checkpoints = profiler.Checkpoints;
        checkpoints.Should().HaveCount(threadCount * checkpointsPerThread);
    }
}

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class StartupMetricsServiceTests
{
    [Fact]
    public void RecordStartupComplete_LogsStartupTime()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        StartupMetricsService.RecordStartupComplete(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Application startup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordStartupComplete_LogsMemoryUsage()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        StartupMetricsService.RecordStartupComplete(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Memory usage")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordStartupComplete_LogsGCCollections()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        StartupMetricsService.RecordStartupComplete(loggerMock.Object);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GC collections")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordStartupComplete_OnlyLogsOnce()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();

        // Act
        StartupMetricsService.RecordStartupComplete(loggerMock.Object);
        StartupMetricsService.RecordStartupComplete(loggerMock.Object); // Second call

        // Assert - Should only log once
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Application startup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ElapsedMilliseconds_ReturnsPositiveValue()
    {
        // Act
        var elapsed = StartupMetricsService.ElapsedMilliseconds;

        // Assert
        elapsed.Should().BeGreaterThan(0);
    }
}
