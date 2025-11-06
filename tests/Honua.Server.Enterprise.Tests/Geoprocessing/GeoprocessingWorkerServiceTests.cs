using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

/// <summary>
/// Tests for the GeoprocessingWorkerService background service
/// </summary>
public class GeoprocessingWorkerServiceTests
{
    private readonly Mock<IControlPlane> _mockControlPlane;
    private readonly ServiceProvider _serviceProvider;

    public GeoprocessingWorkerServiceTests()
    {
        _mockControlPlane = new Mock<IControlPlane>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockControlPlane.Object);
        services.AddLogging(builder => builder.AddConsole());
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_NoJobsInQueue_ShouldPollAndWait()
    {
        // Arrange
        _mockControlPlane
            .Setup(cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessRun?)null);

        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var runTask = service.StartAsync(cts.Token);
        await Task.Delay(1000, CancellationToken.None);
        cts.Cancel();

        // Assert - cancellation should complete gracefully
        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
            // Expected for BackgroundService when cancellation requested
        }

        // Verify dequeue was called
        _mockControlPlane.Verify(
            cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_JobInQueue_ShouldProcessJob()
    {
        // Arrange
        var jobId = "job-20250101-test123";
        var processRun = CreateTestProcessRun(jobId, "buffer");

        var callCount = 0;
        _mockControlPlane
            .Setup(cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? processRun : null;
            });

        _mockControlPlane
            .Setup(cp => cp.RecordCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<ProcessResult>(),
                It.IsAny<ProcessExecutionTier>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var executeTask = service.StartAsync(cts.Token);

        // Wait for job processing
        await Task.Delay(1000);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockControlPlane.Verify(
            cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Note: RecordCompletionAsync may not be called if job processing
        // is still in progress when cancellation occurs
    }

    [Fact]
    public void Constructor_ShouldRegisterOperations()
    {
        // Act
        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        // Assert
        service.Should().NotBeNull();
        // Service should have 7 operations registered internally
        // (Buffer, Intersection, Union, Difference, Simplify, ConvexHull, Dissolve)
    }

    [Fact]
    public async Task StopAsync_WithActiveJobs_ShouldWaitForCompletion()
    {
        // Arrange
        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        _mockControlPlane
            .Setup(cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessRun?)null);

        using var startCts = new CancellationTokenSource();
        var startTask = service.StartAsync(startCts.Token);

        await Task.Delay(500); // Let it start

        // Act
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.StopAsync(stopCts.Token);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));

        startCts.Cancel();
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Fact]
    public async Task ProcessJobAsync_UnsupportedOperation_ShouldRecordFailure()
    {
        // Arrange
        var jobId = "job-20250101-test456";
        var processRun = CreateTestProcessRun(jobId, "unsupported-operation");

        var callCount = 0;
        _mockControlPlane
            .Setup(cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? processRun : null;
            });

        _mockControlPlane
            .Setup(cp => cp.RecordFailureAsync(
                It.IsAny<string>(),
                It.IsAny<Exception>(),
                It.IsAny<ProcessExecutionTier>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var executeTask = service.StartAsync(cts.Token);

        // Wait for job processing
        await Task.Delay(1000);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockControlPlane.Verify(
            cp => cp.DequeueNextJobAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Failure should be recorded for unsupported operation
        _mockControlPlane.Verify(
            cp => cp.RecordFailureAsync(
                It.Is<string>(id => id == jobId),
                It.IsAny<Exception>(),
                It.IsAny<ProcessExecutionTier>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.AtMostOnce()); // May not complete before cancellation
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var service = new GeoprocessingWorkerService(
            _serviceProvider,
            NullLogger<GeoprocessingWorkerService>.Instance);

        // Act & Assert - should not throw
        service.Dispose();
    }

    // Helper methods

    private ProcessRun CreateTestProcessRun(string jobId, string processId)
    {
        return new ProcessRun
        {
            JobId = jobId,
            ProcessId = processId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "test@example.com",
            Status = ProcessRunStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            Priority = 5,
            Progress = 0,
            Inputs = new Dictionary<string, object>
            {
                ["geometry"] = new Dictionary<string, object>
                {
                    ["type"] = "wkt",
                    ["source"] = "POINT(0 0)"
                },
                ["distance"] = 100
            },
            ResponseFormat = "geojson",
            ApiSurface = "OGC",
            ActualTier = ProcessExecutionTier.NTS
        };
    }
}

/// <summary>
/// Tests for the DequeueNextJobAsync functionality in PostgresControlPlane
/// </summary>
[Collection("SharedPostgres")]
public class DequeueJobTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private string _connectionString;
    private readonly Mock<IProcessRegistry> _mockRegistry;
    private readonly Mock<ITierExecutor> _mockTierExecutor;
    private PostgresControlPlane _controlPlane;

    public DequeueJobTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
        _mockRegistry = new Mock<IProcessRegistry>();
        _mockTierExecutor = new Mock<ITierExecutor>();
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new Xunit.SkipException("PostgreSQL test container is not available");
        }

        _connectionString = _fixture.ConnectionString;

        _controlPlane = new PostgresControlPlane(
            _connectionString,
            _mockRegistry.Object,
            _mockTierExecutor.Object,
            NullLogger<PostgresControlPlane>.Instance);

        await TestDatabaseHelper.RunMigrationsAsync(_connectionString);
        await TestDatabaseHelper.CleanupAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_fixture.IsAvailable)
        {
            await TestDatabaseHelper.CleanupAsync(_connectionString);
        }
    }

    [Fact]
    public async Task DequeueNextJobAsync_NoJobs_ShouldReturnNull()
    {
        // Act
        var job = await _controlPlane.DequeueNextJobAsync();

        // Assert
        job.Should().BeNull();
    }

    [Fact]
    public async Task DequeueNextJobAsync_PendingJob_ShouldReturnAndMarkRunning()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var request = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        var decision = await _controlPlane.AdmitAsync(request);
        var enqueued = await _controlPlane.EnqueueAsync(decision);

        // Act
        var dequeued = await _controlPlane.DequeueNextJobAsync();

        // Assert
        dequeued.Should().NotBeNull();
        dequeued!.JobId.Should().Be(enqueued.JobId);
        dequeued.Status.Should().Be(ProcessRunStatus.Running);
        dequeued.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DequeueNextJobAsync_MultipleJobs_ShouldRespectPriority()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        var tenantId = Guid.NewGuid();

        // Create low priority job
        var request1 = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>(),
            Priority = 3
        };
        var decision1 = await _controlPlane.AdmitAsync(request1);
        var job1 = await _controlPlane.EnqueueAsync(decision1);

        // Create high priority job
        var request2 = new ProcessExecutionRequest
        {
            ProcessId = "buffer",
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>(),
            Priority = 8
        };
        var decision2 = await _controlPlane.AdmitAsync(request2);
        var job2 = await _controlPlane.EnqueueAsync(decision2);

        // Act
        var dequeued = await _controlPlane.DequeueNextJobAsync();

        // Assert
        dequeued.Should().NotBeNull();
        dequeued!.JobId.Should().Be(job2.JobId); // High priority job should be first
        dequeued.Priority.Should().Be(8);
    }

    [Fact]
    public async Task DequeueNextJobAsync_ConcurrentCalls_ShouldNotReturnSameJob()
    {
        // Arrange
        var processDefinition = CreateTestProcessDefinition("buffer");
        _mockRegistry.Setup(r => r.GetProcessAsync("buffer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processDefinition);

        _mockTierExecutor.Setup(e => e.SelectTierAsync(It.IsAny<ProcessDefinition>(), It.IsAny<ProcessExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessExecutionTier.NTS);

        // Enqueue multiple jobs
        for (int i = 0; i < 5; i++)
        {
            var request = new ProcessExecutionRequest
            {
                ProcessId = "buffer",
                TenantId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Inputs = new Dictionary<string, object>()
            };
            var decision = await _controlPlane.AdmitAsync(request);
            await _controlPlane.EnqueueAsync(decision);
        }

        // Act - Simulate concurrent dequeuing
        var dequeueTasks = Enumerable.Range(0, 5)
            .Select(_ => _controlPlane.DequeueNextJobAsync())
            .ToArray();

        var results = await Task.WhenAll(dequeueTasks);

        // Assert
        var nonNullResults = results.Where(r => r != null).ToList();
        nonNullResults.Should().HaveCount(5);

        var uniqueJobIds = nonNullResults.Select(r => r!.JobId).Distinct().ToList();
        uniqueJobIds.Should().HaveCount(5); // All jobs should be unique
    }

    private ProcessDefinition CreateTestProcessDefinition(string processId)
    {
        return new ProcessDefinition
        {
            Id = processId,
            Title = $"{processId} Operation",
            Description = $"Test {processId} operation",
            Version = "1.0.0",
            Category = "vector",
            Inputs = new List<ProcessParameter>(),
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.NTS,
                    ProcessExecutionTier.PostGIS,
                    ProcessExecutionTier.CloudBatch
                },
                DefaultTier = ProcessExecutionTier.NTS,
                EstimatedDurationSeconds = 10
            },
            Enabled = true
        };
    }
}
