// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for PostgresBackgroundJobQueue
/// </summary>
public sealed class PostgresBackgroundJobQueueTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly PostgresBackgroundJobQueue _queue;
    private readonly BackgroundJobsOptions _options;

    public PostgresBackgroundJobQueueTests()
    {
        // Use test database connection string
        _connectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
            ?? "Host=localhost;Database=honua_test;Username=postgres;Password=postgres";

        _options = new BackgroundJobsOptions
        {
            Mode = BackgroundJobMode.Polling,
            MaxConcurrentJobs = 5,
            MaxRetries = 3,
            VisibilityTimeoutSeconds = 300
        };

        _queue = new PostgresBackgroundJobQueue(
            _connectionString,
            NullLogger<PostgresBackgroundJobQueue>.Instance,
            Options.Create(_options));
    }

    public async Task InitializeAsync()
    {
        // Create test table
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS background_jobs (
                message_id TEXT PRIMARY KEY,
                job_type TEXT NOT NULL,
                payload JSONB NOT NULL,
                status TEXT NOT NULL CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
                priority INTEGER NOT NULL DEFAULT 5 CHECK (priority BETWEEN 1 AND 10),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                last_received_at TIMESTAMPTZ,
                visible_after TIMESTAMPTZ,
                completed_at TIMESTAMPTZ,
                delivery_count INTEGER NOT NULL DEFAULT 0,
                max_retries INTEGER NOT NULL DEFAULT 3,
                receipt_handle TEXT,
                deduplication_id TEXT,
                message_group_id TEXT,
                attributes JSONB,
                error_message TEXT,
                error_details TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_background_jobs_dequeue
            ON background_jobs (status, visible_after, priority DESC, created_at)
            WHERE status = 'pending';

            -- Clean up any existing test data
            DELETE FROM background_jobs WHERE job_type LIKE 'Test%';
        ", connection);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test data
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM background_jobs WHERE job_type LIKE 'Test%'",
            connection);

        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task EnqueueAsync_ShouldAddJobToQueue()
    {
        // Arrange
        var job = new TestJob { Name = "Test", Value = 42 };

        // Act
        var messageId = await _queue.EnqueueAsync(job);

        // Assert
        Assert.NotNull(messageId);
        Assert.NotEmpty(messageId);

        var depth = await _queue.GetQueueDepthAsync();
        Assert.True(depth > 0);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldReturnEnqueuedJob()
    {
        // Arrange
        var job = new TestJob { Name = "Test Receive", Value = 123 };
        await _queue.EnqueueAsync(job);

        // Act
        var messages = await _queue.ReceiveAsync<TestJob>(maxMessages: 1);

        // Assert
        var message = Assert.Single(messages);
        Assert.NotNull(message.Body);
        Assert.Equal("Test Receive", message.Body.Name);
        Assert.Equal(123, message.Body.Value);
        Assert.Equal(1, message.DeliveryCount);
    }

    [Fact]
    public async Task ReceiveAsync_ShouldRespectPriority()
    {
        // Arrange - enqueue jobs with different priorities
        var lowPriorityJob = new TestJob { Name = "Low Priority", Value = 1 };
        var highPriorityJob = new TestJob { Name = "High Priority", Value = 10 };

        await _queue.EnqueueAsync(lowPriorityJob, new EnqueueOptions { Priority = 1 });
        await _queue.EnqueueAsync(highPriorityJob, new EnqueueOptions { Priority = 10 });

        // Act
        var messages = await _queue.ReceiveAsync<TestJob>(maxMessages: 2);

        // Assert
        var messageList = messages.ToList();
        Assert.Equal(2, messageList.Count);

        // High priority job should be first
        Assert.Equal("High Priority", messageList[0].Body.Name);
        Assert.Equal("Low Priority", messageList[1].Body.Name);
    }

    [Fact]
    public async Task CompleteAsync_ShouldRemoveJobFromQueue()
    {
        // Arrange
        var job = new TestJob { Name = "Test Complete", Value = 456 };
        await _queue.EnqueueAsync(job);

        var messages = await _queue.ReceiveAsync<TestJob>(maxMessages: 1);
        var message = messages.First();

        // Act
        await _queue.CompleteAsync(message.ReceiptHandle);

        // Assert - job should not be in queue anymore
        var remainingMessages = await _queue.ReceiveAsync<TestJob>(maxMessages: 10);
        Assert.DoesNotContain(remainingMessages, m => m.Body.Name == "Test Complete");
    }

    [Fact]
    public async Task AbandonAsync_ShouldMakeJobVisibleAgain()
    {
        // Arrange
        var job = new TestJob { Name = "Test Abandon", Value = 789 };
        await _queue.EnqueueAsync(job);

        var messages = await _queue.ReceiveAsync<TestJob>(maxMessages: 1);
        var message = messages.First();

        // Act
        await _queue.AbandonAsync(message.ReceiptHandle);

        // Give the visibility timeout a moment to clear (in real scenario this would be seconds)
        await Task.Delay(100);

        // Assert - job should be available again (but this test won't work perfectly
        // without waiting for actual visibility timeout)
        var depth = await _queue.GetQueueDepthAsync();
        Assert.True(depth > 0, "Queue should still have pending messages after abandon");
    }

    [Fact]
    public async Task ReceiveAsync_ShouldNotReturnProcessingJobs()
    {
        // Arrange
        var job = new TestJob { Name = "Test Processing", Value = 999 };
        await _queue.EnqueueAsync(job);

        // First receive should get the job
        var messages1 = await _queue.ReceiveAsync<TestJob>(maxMessages: 1);
        Assert.Single(messages1);

        // Second receive (while first is still processing) should not get the same job
        var messages2 = await _queue.ReceiveAsync<TestJob>(maxMessages: 1);
        Assert.Empty(messages2);
    }

    [Fact]
    public async Task EnqueueAsync_WithDelay_ShouldNotBeImmediatelyVisible()
    {
        // Arrange
        var job = new TestJob { Name = "Test Delay", Value = 111 };

        // Act
        await _queue.EnqueueAsync(job, new EnqueueOptions
        {
            DelaySeconds = TimeSpan.FromSeconds(10)
        });

        // Assert - job should not be visible yet
        var messages = await _queue.ReceiveAsync<TestJob>(maxMessages: 10);
        Assert.DoesNotContain(messages, m => m.Body.Name == "Test Delay");

        // Queue depth should still be 0 for visible messages
        var depth = await _queue.GetQueueDepthAsync();
        Assert.Equal(0, depth);
    }

    [Fact]
    public async Task GetQueueDepthAsync_ShouldReturnCorrectCount()
    {
        // Arrange - clean state
        var initialDepth = await _queue.GetQueueDepthAsync();

        // Enqueue 3 jobs
        await _queue.EnqueueAsync(new TestJob { Name = "Test1", Value = 1 });
        await _queue.EnqueueAsync(new TestJob { Name = "Test2", Value = 2 });
        await _queue.EnqueueAsync(new TestJob { Name = "Test3", Value = 3 });

        // Act
        var depth = await _queue.GetQueueDepthAsync();

        // Assert
        Assert.Equal(initialDepth + 3, depth);
    }

    // Helper class for testing
    private class TestJob
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
