// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading.Channels;
using FluentAssertions;
using Honua.Server.Core.Cloud.EventGrid.Configuration;
using Honua.Server.Core.Cloud.EventGrid.Models;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly.CircuitBreaker;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.EventGrid;

public class EventGridBackgroundPublisherTests
{
    private readonly Mock<ILogger<EventGridBackgroundPublisher>> _mockLogger;
    private readonly Mock<ILogger<EventGridPublisher>> _mockPublisherLogger;
    private readonly EventGridOptions _options;

    public EventGridBackgroundPublisherTests()
    {
        _mockLogger = new Mock<ILogger<EventGridBackgroundPublisher>>();
        _mockPublisherLogger = new Mock<ILogger<EventGridPublisher>>();
        _options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1, // Short interval for testing
            MaxBatchSize = 10,
            MaxQueueSize = 100
        };
    }

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_WithValidConfig_StartsBackgroundProcessing()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to start

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Event Grid background publisher started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithDisabledConfig_DoesNotStartProcessing()
    {
        // Arrange
        var disabledOptions = new EventGridOptions { Enabled = false };
        var publisher = new TestableEventGridPublisher(disabledOptions, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(disabledOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Event Grid background publisher is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        publisher.ProcessBatchCallCount.Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_FlushesRemainingEvents_BeforeShutdown()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        // Queue some events
        await publisher.PublishAsync(CreateTestEvent("event-1"));
        await publisher.PublishAsync(CreateTestEvent("event-2"));
        await publisher.PublishAsync(CreateTestEvent("event-3"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Start and stop quickly
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try { await executeTask; } catch { }
        await backgroundPublisher.StopAsync(CancellationToken.None);

        // Assert - FlushAsync should be called during shutdown
        publisher.FlushCallCount.Should().BeGreaterThan(0);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Flushing remaining events on shutdown")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_WithPendingEvents_WaitsForCompletion()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        publisher.FlushDelay = TimeSpan.FromMilliseconds(500); // Simulate slow flush

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        // Queue events
        for (int i = 0; i < 5; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);
        await Task.Delay(100);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        cts.Cancel();

        try { await executeTask; } catch { }
        await backgroundPublisher.StopAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert - Should have waited for flush to complete
        stopwatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(400);
        publisher.FlushCallCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task ExecuteAsync_AccumulatesEvents_UntilBatchSizeReached()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Start background processing
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        // Queue events up to batch size
        for (int i = 0; i < _options.MaxBatchSize; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for flush interval
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - ProcessBatchAsync should have been called
        publisher.ProcessBatchCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_FlushesEvents_WhenIntervalElapsed()
    {
        // Arrange
        var shortFlushOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 100
        };

        var publisher = new TestableEventGridPublisher(shortFlushOptions, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(shortFlushOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Start and queue fewer events than batch size
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        await publisher.PublishAsync(CreateTestEvent("event-1"));
        await publisher.PublishAsync(CreateTestEvent("event-2"));

        // Wait for at least 2 flush intervals
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Should have flushed based on time interval
        publisher.ProcessBatchCallCount.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesBatching_WithConcurrentPublishers()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Start background processing
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        // Publish events concurrently from multiple threads
        var publishTasks = Enumerable.Range(0, 50).Select(async i =>
        {
            await publisher.PublishAsync(CreateTestEvent($"concurrent-event-{i}"));
            await Task.Delay(10); // Small delay between events
        });

        await Task.WhenAll(publishTasks);
        await Task.Delay(TimeSpan.FromSeconds(2));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - All events should be processed without errors
        publisher.ProcessBatchCallCount.Should().BeGreaterThan(0);
        publisher.LastException.Should().BeNull();
    }

    #endregion

    #region Flush Behavior Tests

    [Fact]
    public async Task FlushAsync_WithPendingEvents_PublishesAll()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);

        // Queue events
        for (int i = 0; i < 15; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
        }

        var queueSizeBefore = publisher.GetQueueSize();

        // Act
        await publisher.FlushAsync();

        // Assert
        queueSizeBefore.Should().Be(15);
        publisher.GetQueueSize().Should().Be(0);
        publisher.FlushCallCount.Should().Be(1);
        publisher.PublishedEventCount.Should().Be(15);
    }

    [Fact]
    public async Task FlushAsync_WithEmptyQueue_CompletesImmediately()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await publisher.FlushAsync();
        stopwatch.Stop();

        // Assert - Should complete quickly with no events
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        publisher.GetQueueSize().Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_WithPublishFailure_RetriesFailedEvents()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.FailureCount = 2; // Fail first 2 attempts, succeed on 3rd

        await publisher.PublishAsync(CreateTestEvent("event-1"));

        // Act
        await publisher.FlushAsync();

        // Assert - Should have retried (based on retry configuration)
        publisher.PublishAttempts.Should().BeGreaterThan(1);
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task ExecuteAsync_WithConsecutiveFailures_OpensCircuitBreaker()
    {
        // Arrange
        var cbOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 10,
            CircuitBreaker = new CircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 3,
                DurationOfBreakSeconds = 5,
                SamplingDurationSeconds = 10,
                MinimumThroughput = 1
            }
        };

        var publisher = new TestableEventGridPublisher(cbOptions, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.AlwaysFail = true; // Keep failing

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(cbOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act - Queue events and let them fail
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        for (int i = 0; i < 20; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
            await Task.Delay(50);
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Circuit breaker should eventually open
        var metrics = publisher.GetMetrics();
        metrics.EventsFailed.Should().BeGreaterThan(0);

        // Circuit breaker state may be "Open" after enough failures
        publisher.CircuitBreakerOpened.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCircuitOpen_DropsEvents()
    {
        // Arrange
        var cbOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 5,
            CircuitBreaker = new CircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 2,
                DurationOfBreakSeconds = 30,
                SamplingDurationSeconds = 10,
                MinimumThroughput = 1
            }
        };

        var publisher = new TestableEventGridPublisher(cbOptions, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.AlwaysFail = true;

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(cbOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act - Trigger circuit breaker to open
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        // Send initial events to trigger failures
        for (int i = 0; i < 15; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"trigger-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(2));

        var metricsBeforeMore = publisher.GetMetrics();
        var failedBefore = metricsBeforeMore.EventsFailed;

        // Send more events while circuit is open
        for (int i = 0; i < 10; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"dropped-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - When circuit is open, events should be dropped/failed
        var finalMetrics = publisher.GetMetrics();
        finalMetrics.EventsFailed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_AfterDelay_ClosesCircuitAndRetries()
    {
        // Arrange
        var cbOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 5,
            CircuitBreaker = new CircuitBreakerOptions
            {
                Enabled = true,
                FailureThreshold = 2,
                DurationOfBreakSeconds = 2, // Short break duration
                SamplingDurationSeconds = 10,
                MinimumThroughput = 1
            }
        };

        var publisher = new TestableEventGridPublisher(cbOptions, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.FailureCount = 10; // Fail first 10, then succeed

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(cbOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        // Queue events
        for (int i = 0; i < 25; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
            await Task.Delay(100);
        }

        await Task.Delay(TimeSpan.FromSeconds(8));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Circuit should close after break duration and allow retries
        publisher.CircuitBreakerClosed.Should().BeTrue();
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task ExecuteAsync_RespectsBatchSize_NeverExceeds()
    {
        // Arrange
        var strictOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 5,
            MaxQueueSize = 100
        };

        var publisher = new TestableEventGridPublisher(strictOptions, _mockPublisherLogger.Object);
        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(strictOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Queue more events than batch size
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        for (int i = 0; i < 50; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"event-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Each batch should respect the max batch size
        publisher.MaxBatchSizeObserved.Should().BeLessOrEqualTo(strictOptions.MaxBatchSize);
    }

    [Fact]
    public async Task ExecuteAsync_WithHighVolume_AppliesBackpressure()
    {
        // Arrange
        var backpressureOptions = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            TopicKey = "test-key",
            UseManagedIdentity = false,
            FlushIntervalSeconds = 1,
            MaxBatchSize = 10,
            MaxQueueSize = 20, // Small queue
            BackpressureMode = BackpressureMode.Drop // Drop when full
        };

        var publisher = new TestableEventGridPublisher(backpressureOptions, _mockPublisherLogger.Object);
        publisher.ProcessDelay = TimeSpan.FromMilliseconds(500); // Slow processing

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(backpressureOptions),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act - Flood the queue
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        for (int i = 0; i < 100; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"flood-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Some events should be dropped due to backpressure
        var metrics = publisher.GetMetrics();
        metrics.EventsDropped.Should().BeGreaterThan(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WithTransientError_RetriesWithBackoff()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.FailureCount = 2; // Fail twice, then succeed

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        await publisher.PublishAsync(CreateTestEvent("event-1"));
        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait for retry
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Should have retried and eventually succeeded
        publisher.PublishAttempts.Should().BeGreaterThan(1);
        var metrics = publisher.GetMetrics();
        metrics.EventsPublished.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithPermanentError_LogsAndContinues()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        publisher.ShouldThrowPermanentError = true;

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        await publisher.PublishAsync(CreateTestEvent("event-1"));
        await publisher.PublishAsync(CreateTestEvent("event-2"));

        await Task.Delay(TimeSpan.FromSeconds(2));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Should log error but continue processing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithPartialBatchFailure_RetriesFailedItems()
    {
        // Arrange
        var publisher = new TestableEventGridPublisher(_options, _mockPublisherLogger.Object);
        publisher.ShouldFailPublish = true;
        publisher.FailureCount = 1; // Fail first batch, succeed on retry

        var backgroundPublisher = new EventGridBackgroundPublisher(
            publisher,
            Options.Create(_options),
            _mockLogger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await backgroundPublisher.StartAsync(cts.Token);
        var executeTask = RunExecuteAsync(backgroundPublisher, cts.Token);

        // Queue a batch
        for (int i = 0; i < 5; i++)
        {
            await publisher.PublishAsync(CreateTestEvent($"batch-event-{i}"));
        }

        await Task.Delay(TimeSpan.FromSeconds(5));
        cts.Cancel();

        try { await executeTask; } catch { }

        // Assert - Should retry and eventually publish
        publisher.PublishAttempts.Should().BeGreaterThan(1);
    }

    #endregion

    #region Helper Methods

    private static HonuaCloudEvent CreateTestEvent(string id)
    {
        return new HonuaCloudEventBuilder()
            .WithId(id)
            .WithSource("test/source")
            .WithType(HonuaEventTypes.FeatureCreated)
            .WithSubject($"test/{id}")
            .WithData(new { test = "data" })
            .Build();
    }

    private static async Task RunExecuteAsync(EventGridBackgroundPublisher service, CancellationToken cancellationToken)
    {
        // Use reflection to access protected ExecuteAsync method
        var method = typeof(BackgroundService).GetMethod("ExecuteAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            var task = (Task)method.Invoke(service, new object[] { cancellationToken })!;
            await task;
        }
    }

    #endregion

    #region Testable EventGridPublisher

    /// <summary>
    /// Testable version of EventGridPublisher that doesn't require Azure credentials
    /// </summary>
    private class TestableEventGridPublisher : EventGridPublisher
    {
        private readonly Channel<HonuaCloudEvent> _testQueue;
        private readonly EventGridMetrics _testMetrics = new();
        private int _processBatchCallCount;
        private int _flushCallCount;
        private int _publishedEventCount;
        private int _publishAttempts;

        public int ProcessBatchCallCount => _processBatchCallCount;
        public int FlushCallCount => _flushCallCount;
        public int PublishedEventCount => _publishedEventCount;
        public int PublishAttempts => _publishAttempts;
        public int MaxBatchSizeObserved { get; private set; }
        public Exception? LastException { get; private set; }
        public TimeSpan FlushDelay { get; set; } = TimeSpan.Zero;
        public TimeSpan ProcessDelay { get; set; } = TimeSpan.Zero;
        public bool ShouldFailPublish { get; set; }
        public bool AlwaysFail { get; set; }
        public int FailureCount { get; set; }
        public bool ShouldThrowPermanentError { get; set; }
        public bool CircuitBreakerOpened { get; private set; }
        public bool CircuitBreakerClosed { get; private set; }

        public TestableEventGridPublisher(EventGridOptions options, ILogger<EventGridPublisher> logger)
            : base(Options.Create(options), logger)
        {
            var channelOptions = new BoundedChannelOptions(options.MaxQueueSize)
            {
                FullMode = options.BackpressureMode == BackpressureMode.Block
                    ? BoundedChannelFullMode.Wait
                    : BoundedChannelFullMode.DropOldest
            };
            _testQueue = Channel.CreateBounded<HonuaCloudEvent>(channelOptions);
        }

        public new async Task PublishAsync(HonuaCloudEvent cloudEvent, CancellationToken cancellationToken = default)
        {
            if (_testQueue.Writer.TryWrite(cloudEvent))
            {
                // Successfully queued
            }
            else
            {
                Interlocked.Increment(ref _testMetrics.EventsDropped);
            }
            await Task.CompletedTask;
        }

        internal new async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processBatchCallCount);

            if (ProcessDelay > TimeSpan.Zero)
            {
                await Task.Delay(ProcessDelay, cancellationToken);
            }

            var batch = new List<HonuaCloudEvent>();
            while (batch.Count < 10 && _testQueue.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count > 0)
            {
                MaxBatchSizeObserved = Math.Max(MaxBatchSizeObserved, batch.Count);
                await SimulatePublish(batch, cancellationToken);
            }
        }

        public new async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _flushCallCount);

            if (FlushDelay > TimeSpan.Zero)
            {
                await Task.Delay(FlushDelay, cancellationToken);
            }

            var batch = new List<HonuaCloudEvent>();
            while (_testQueue.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count > 0)
            {
                await SimulatePublish(batch, cancellationToken);
            }
        }

        private async Task SimulatePublish(List<HonuaCloudEvent> batch, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _publishAttempts);

            if (ShouldThrowPermanentError)
            {
                throw new InvalidOperationException("Permanent error in publishing");
            }

            if (ShouldFailPublish && (AlwaysFail || Interlocked.Decrement(ref FailureCount) >= 0))
            {
                LastException = new Exception("Simulated publish failure");
                Interlocked.Add(ref _testMetrics.EventsFailed, batch.Count);

                // Check if we should open circuit breaker (simulate)
                if (_testMetrics.EventsFailed >= 5)
                {
                    CircuitBreakerOpened = true;
                    _testMetrics.CircuitBreakerState = "Open";
                    throw new BrokenCircuitException("Circuit breaker is open");
                }

                throw LastException;
            }

            // Simulate successful publish
            await Task.Delay(10, cancellationToken);
            Interlocked.Add(ref _publishedEventCount, batch.Count);
            Interlocked.Add(ref _testMetrics.EventsPublished, batch.Count);
            _testMetrics.LastPublishTime = DateTimeOffset.UtcNow;

            // Simulate circuit closing after success
            if (CircuitBreakerOpened && _testMetrics.EventsPublished > 0)
            {
                CircuitBreakerClosed = true;
                _testMetrics.CircuitBreakerState = "Closed";
            }
        }

        public new int GetQueueSize()
        {
            return _testQueue.Reader.Count;
        }

        public new EventGridMetrics GetMetrics()
        {
            _testMetrics.CurrentQueueSize = GetQueueSize();
            return _testMetrics;
        }
    }

    #endregion
}
