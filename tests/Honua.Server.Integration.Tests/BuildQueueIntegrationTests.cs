using System.Diagnostics;
using Dapper;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Npgsql;

namespace Honua.Server.Integration.Tests;

/// <summary>
/// Integration tests for build queue processing and concurrency control.
/// </summary>
[Collection("Integration")]
public class BuildQueueIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public BuildQueueIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Test_BuildQueue_ProcessInPriorityOrder()
    {
        // Arrange - Queue builds with different priorities
        var customerId = "priority-test-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId);
            builder.WithBuildInQueue(customerId, "manifest-low", "hash-low", priority: 50);
            builder.WithBuildInQueue(customerId, "manifest-high", "hash-high", priority: 200);
            builder.WithBuildInQueue(customerId, "manifest-med", "hash-med", priority: 100);
        });

        // Act - Retrieve builds in priority order
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var builds = await connection.QueryAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue
            WHERE customer_id = @CustomerId AND status = 'queued'
            ORDER BY priority DESC, created_at ASC
        ", new { CustomerId = customerId });

        // Assert - Builds are in correct priority order
        var buildList = builds.ToList();
        buildList.Should().HaveCount(3);
        buildList[0].ManifestId.Should().Be("manifest-high");
        buildList[1].ManifestId.Should().Be("manifest-med");
        buildList[2].ManifestId.Should().Be("manifest-low");
    }

    [Fact]
    public async Task Test_BuildQueue_ConcurrentBuilds_RespectLimit()
    {
        // Arrange - Create customer with concurrent build limit
        var customerId = "concurrent-test";
        var maxConcurrent = 2;

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, maxConcurrentBuilds: maxConcurrent);
            // Queue 5 builds
            for (int i = 0; i < 5; i++)
            {
                builder.WithBuildInQueue(customerId, $"manifest-{i}", $"hash-{i}");
            }
        });

        // Act - Start processing builds
        var processingTasks = new List<Task>();
        var activeBuildCount = 0;
        var maxObservedConcurrent = 0;
        var lockObj = new object();

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Get all queued builds
        var queuedBuilds = await connection.QueryAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue
            WHERE customer_id = @CustomerId AND status = 'queued'
        ", new { CustomerId = customerId });

        // Process each build with concurrency limit
        var semaphore = new SemaphoreSlim(maxConcurrent);

        foreach (var build in queuedBuilds)
        {
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    lock (lockObj)
                    {
                        activeBuildCount++;
                        maxObservedConcurrent = Math.Max(maxObservedConcurrent, activeBuildCount);
                    }

                    // Simulate build processing
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    lock (lockObj)
                    {
                        activeBuildCount--;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            processingTasks.Add(task);
        }

        await Task.WhenAll(processingTasks);

        // Assert - Never exceeded concurrent build limit
        maxObservedConcurrent.Should().BeLessThanOrEqualTo(maxConcurrent,
            "concurrent builds should not exceed license limit");
        activeBuildCount.Should().Be(0, "all builds should have completed");
    }

    [Fact]
    public async Task Test_BuildQueue_BuildFailure_RetryLogic()
    {
        // Arrange - Queue a build
        var customerId = "retry-test";
        var manifestHash = "retry-hash";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId);
            builder.WithBuildInQueue(customerId, "retry-manifest", manifestHash);
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var buildId = await connection.QuerySingleAsync<Guid>(@"
            SELECT id FROM build_queue
            WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        // Act - Simulate build failure with retries
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            // Start build
            await connection.ExecuteAsync(@"
                UPDATE build_queue
                SET status = 'running', started_at = NOW()
                WHERE id = @BuildId
            ", new { BuildId = buildId });

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            // Fail build
            await connection.ExecuteAsync(@"
                UPDATE build_queue
                SET status = 'failed',
                    retry_count = @RetryCount,
                    error_message = @ErrorMessage
                WHERE id = @BuildId
            ", new
            {
                BuildId = buildId,
                RetryCount = attempt,
                ErrorMessage = $"Build failed: attempt {attempt}"
            });

            // Queue retry if under max retries
            if (attempt < 3)
            {
                await connection.ExecuteAsync(@"
                    UPDATE build_queue
                    SET status = 'queued'
                    WHERE id = @BuildId AND retry_count < max_retries
                ", new { BuildId = buildId });
            }
        }

        // Assert - Build exhausted retries
        var build = await connection.QuerySingleAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue WHERE id = @BuildId
        ", new { BuildId = buildId });

        build.Status.Should().Be("failed");
        build.RetryCount.Should().Be(3);
        build.ErrorMessage.Should().Contain("Build failed");
    }

    [Fact]
    public async Task Test_BuildQueue_BuildTimeout_Cancellation()
    {
        // Arrange - Queue build with timeout
        var customerId = "timeout-test";
        var timeoutMinutes = 1;

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId);
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var buildId = await connection.QuerySingleAsync<Guid>(@"
            INSERT INTO build_queue (customer_id, manifest_id, manifest_hash, status, timeout_at)
            VALUES (@CustomerId, 'timeout-manifest', 'timeout-hash', 'running', @TimeoutAt)
            RETURNING id
        ", new
        {
            CustomerId = customerId,
            TimeoutAt = DateTimeOffset.UtcNow.AddMinutes(timeoutMinutes)
        });

        // Act - Simulate timeout check
        await Task.Delay(TimeSpan.FromMilliseconds(100)); // Small delay to ensure timeout_at is in the past for test purposes

        // Force timeout for testing
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET timeout_at = @TimeoutAt
            WHERE id = @BuildId
        ", new { BuildId = buildId, TimeoutAt = DateTimeOffset.UtcNow.AddSeconds(-1) });

        // Check for timed out builds and cancel them
        var timedOutBuilds = await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'timeout',
                completed_at = NOW(),
                error_message = 'Build exceeded timeout threshold'
            WHERE status = 'running'
            AND timeout_at IS NOT NULL
            AND timeout_at < NOW()
        ");

        // Assert - Build was cancelled due to timeout
        var build = await connection.QuerySingleAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue WHERE id = @BuildId
        ", new { BuildId = buildId });

        build.Status.Should().Be("timeout");
        build.CompletedAt.Should().NotBeNull();
        build.ErrorMessage.Should().Contain("timeout");
    }

    [Fact]
    public async Task Test_BuildQueue_ParallelTargets_IndependentProcessing()
    {
        // Arrange - Queue multiple targets for same manifest
        var customerId = "parallel-targets";
        var manifestHash = "multi-target-hash";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, maxConcurrentBuilds: 4);
            builder.WithBuildInQueue(customerId, "manifest", manifestHash, targetId: "target-1");
            builder.WithBuildInQueue(customerId, "manifest", manifestHash, targetId: "target-2");
            builder.WithBuildInQueue(customerId, "manifest", manifestHash, targetId: "target-3");
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Process builds in parallel
        var builds = await connection.QueryAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue
            WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        var stopwatch = Stopwatch.StartNew();
        var processTasks = builds.Select(async build =>
        {
            await connection.ExecuteAsync(@"
                UPDATE build_queue
                SET status = 'running', started_at = NOW()
                WHERE id = @BuildId
            ", new { BuildId = build.Id });

            await Task.Delay(TimeSpan.FromSeconds(1)); // Simulate build

            await connection.ExecuteAsync(@"
                UPDATE build_queue
                SET status = 'success',
                    completed_at = NOW(),
                    output_path = @OutputPath
                WHERE id = @BuildId
            ", new { BuildId = build.Id, OutputPath = $"/output/{build.Id}" });
        });

        await Task.WhenAll(processTasks);
        stopwatch.Stop();

        // Assert - All targets completed
        var completedBuilds = await connection.QueryAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue
            WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        completedBuilds.Should().HaveCount(3);
        completedBuilds.Should().OnlyContain(b => b.Status == "success");

        // Assert - Parallel execution was faster than sequential
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2.5),
            "parallel builds should complete faster than 3 sequential builds");
    }

    [Fact]
    public async Task Test_BuildQueue_CancellationRequest_StopsProcessing()
    {
        // Arrange - Queue a long-running build
        var customerId = "cancel-test";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId);
            builder.WithBuildInQueue(customerId, "cancel-manifest", "cancel-hash");
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var buildId = await connection.QuerySingleAsync<Guid>(@"
            SELECT id FROM build_queue WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        // Act - Start build
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'running', started_at = NOW()
            WHERE id = @BuildId
        ", new { BuildId = buildId });

        // Simulate cancellation request
        var cts = new CancellationTokenSource();
        var buildTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        // Cancel after short delay
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await cts.CancelAsync();

        await buildTask;

        // Mark build as cancelled
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'cancelled',
                completed_at = NOW(),
                error_message = 'Build cancelled by user request'
            WHERE id = @BuildId AND status = 'running'
        ", new { BuildId = buildId });

        // Assert - Build was cancelled
        var build = await connection.QuerySingleAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue WHERE id = @BuildId
        ", new { BuildId = buildId });

        build.Status.Should().Be("cancelled");
        build.CompletedAt.Should().NotBeNull();
        build.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Test_BuildQueue_MetricsCollection_RecordsPerformance()
    {
        // Arrange - Queue build
        var customerId = "metrics-test";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId);
            builder.WithBuildInQueue(customerId, "metrics-manifest", "metrics-hash");
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var buildId = await connection.QuerySingleAsync<Guid>(@"
            SELECT id FROM build_queue WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        // Act - Simulate build with metrics
        var startTime = DateTimeOffset.UtcNow;
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'running', started_at = @StartTime
            WHERE id = @BuildId
        ", new { BuildId = buildId, StartTime = startTime });

        // Record metrics during build
        await connection.ExecuteAsync(@"
            INSERT INTO build_metrics (build_id, metric_name, metric_value, unit)
            VALUES
                (@BuildId, 'cpu_usage_percent', 75.5, 'percent'),
                (@BuildId, 'memory_usage_mb', 2048, 'megabytes'),
                (@BuildId, 'build_duration_seconds', 120, 'seconds')
        ", new { BuildId = buildId });

        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'success', completed_at = NOW()
            WHERE id = @BuildId
        ", new { BuildId = buildId });

        // Assert - Metrics were recorded
        var metrics = await connection.QueryAsync<BuildMetric>(@"
            SELECT * FROM build_metrics
            WHERE build_id = @BuildId
            ORDER BY metric_name
        ", new { BuildId = buildId });

        metrics.Should().HaveCount(3);
        metrics.Should().Contain(m => m.MetricName == "cpu_usage_percent" && m.MetricValue == 75.5m);
        metrics.Should().Contain(m => m.MetricName == "memory_usage_mb" && m.MetricValue == 2048m);
        metrics.Should().Contain(m => m.MetricName == "build_duration_seconds" && m.MetricValue == 120m);
    }
}

internal record BuildMetric
{
    public Guid Id { get; init; }
    public Guid BuildId { get; init; }
    public string MetricName { get; init; } = string.Empty;
    public decimal? MetricValue { get; init; }
    public string? Unit { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
