using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Honua.Cli.Services.ControlPlane;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class VectorCacheCommandsTests
{
    [Fact]
    public async Task PreseedCommand_ShouldEnqueueAndMonitorUntilCompleted()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.EnqueueResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Queued, 0d, 0, 1000));
        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Queued, 0d, 0, 1000));
        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Running, 0.5d, 500, 1000));
        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Completed, 1d, 1000, 1000));

        var command = new VectorCachePreseedCommand(console, api, NullLogger<VectorCachePreseedCommand>.Instance);
        var settings = new VectorCachePreseedCommand.Settings
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 0,
            MaxZoom = 10,
            Host = "http://localhost:5000",
            PollIntervalSeconds = 0
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        api.Requests.Should().HaveCount(1);
        console.Output.Should().Contain("Queued");
        console.Output.Should().Contain("Running");
        console.Output.Should().Contain("Completed");
    }

    [Fact]
    public async Task PreseedCommand_ShouldRequireServiceId()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var command = new VectorCachePreseedCommand(console, api, NullLogger<VectorCachePreseedCommand>.Instance);
        var settings = new VectorCachePreseedCommand.Settings
        {
            ServiceId = "",
            LayerId = "cities",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("service-id is required");
    }

    [Fact]
    public async Task PreseedCommand_ShouldRequireLayerId()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var command = new VectorCachePreseedCommand(console, api, NullLogger<VectorCachePreseedCommand>.Instance);
        var settings = new VectorCachePreseedCommand.Settings
        {
            ServiceId = "my-service",
            LayerId = "",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("layer-id is required");
    }

    [Fact]
    public async Task PreseedCommand_ShouldHandleFailedJob()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.EnqueueResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Queued, 0d, 0, 1000));
        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Running, 0.3d, 300, 1000));
        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Failed, 0.3d, 300, 1000, "Database connection failed"));

        var command = new VectorCachePreseedCommand(console, api, NullLogger<VectorCachePreseedCommand>.Instance);
        var settings = new VectorCachePreseedCommand.Settings
        {
            ServiceId = "my-service",
            LayerId = "cities",
            Host = "http://localhost:5000",
            PollIntervalSeconds = 0
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("Failed");
        console.Output.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task StatusCommand_ShouldDisplayJobDetails()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.GetResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Running, 0.75d, 750, 1000));

        var command = new VectorCacheStatusCommand(console, api, NullLogger<VectorCacheStatusCommand>.Instance);
        var settings = new VectorCacheStatusCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain(jobId.ToString());
        console.Output.Should().Contain("Running");
        console.Output.Should().Contain("75%");
        console.Output.Should().Contain("750");
        console.Output.Should().Contain("1000");
    }

    [Fact]
    public async Task StatusCommand_ShouldHandleNotFound()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.GetResponses.Enqueue(null);

        var command = new VectorCacheStatusCommand(console, api, NullLogger<VectorCacheStatusCommand>.Instance);
        var settings = new VectorCacheStatusCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("not found");
    }

    [Fact]
    public async Task JobsCommand_ShouldListAllJobs()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        var jobs = new List<VectorTilePreseedJob>
        {
            CreateJob(Guid.NewGuid(), VectorTilePreseedJobStatus.Running, 0.5d, 500, 1000),
            CreateJob(Guid.NewGuid(), VectorTilePreseedJobStatus.Completed, 1d, 1000, 1000),
            CreateJob(Guid.NewGuid(), VectorTilePreseedJobStatus.Failed, 0.2d, 200, 1000, "Error")
        };

        api.ListResponses.Enqueue(jobs);

        var command = new VectorCacheJobsCommand(console, api);
        var settings = new VectorCacheJobsCommand.Settings
        {
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Running");
        console.Output.Should().Contain("Completed");
        console.Output.Should().Contain("Failed");
        Regex.IsMatch(console.Output, @"my-service/c.*ities", RegexOptions.Singleline)
            .Should().BeTrue("the layer ID should render as my-service/cities even when wrapped");
    }

    [Fact]
    public async Task JobsCommand_ShouldHandleEmptyList()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        api.ListResponses.Enqueue(new List<VectorTilePreseedJob>());

        var command = new VectorCacheJobsCommand(console, api);
        var settings = new VectorCacheJobsCommand.Settings
        {
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("No vector preseed jobs found");
    }

    [Fact]
    public async Task CancelCommand_ShouldCancelJob()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.CancelResponses.Enqueue(CreateJob(jobId, VectorTilePreseedJobStatus.Cancelled, 0.5d, 500, 1000, "User requested cancellation"));

        var command = new VectorCacheCancelCommand(console, api);
        var settings = new VectorCacheCancelCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Cancelled");
        Regex.IsMatch(console.Output, @"my-service/c.*ities", RegexOptions.Singleline)
            .Should().BeTrue("the layer ID should render as my-service/cities even when wrapped");
    }

    [Fact]
    public async Task CancelCommand_ShouldHandleNotFound()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.CancelResponses.Enqueue(null);

        var command = new VectorCacheCancelCommand(console, api);
        var settings = new VectorCacheCancelCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("not found");
    }

    [Fact]
    public async Task PurgeCommand_ShouldPurgeServiceSuccessfully()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        api.PurgeResponses.Enqueue(new VectorTileCachePurgeResult(
            new[] { "my-service" },
            Array.Empty<string>()));

        var command = new VectorCachePurgeCommand(console, api);
        var settings = new VectorCachePurgeCommand.Settings
        {
            ServiceId = "my-service",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Purged");
        console.Output.Should().Contain("my-service");
    }

    [Fact]
    public async Task PurgeCommand_ShouldPurgeLayerSuccessfully()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        api.PurgeResponses.Enqueue(new VectorTileCachePurgeResult(
            new[] { "my-service/cities" },
            Array.Empty<string>()));

        var command = new VectorCachePurgeCommand(console, api);
        var settings = new VectorCachePurgeCommand.Settings
        {
            ServiceId = "my-service",
            LayerId = "cities",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Purged");
        Regex.IsMatch(console.Output, @"my-service/c.*ities", RegexOptions.Singleline)
            .Should().BeTrue("the layer ID should render as my-service/cities even when wrapped");
    }

    [Fact]
    public async Task PurgeCommand_ShouldRequireServiceId()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        var command = new VectorCachePurgeCommand(console, api);
        var settings = new VectorCachePurgeCommand.Settings
        {
            ServiceId = "",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("service-id is required");
    }

    [Fact]
    public async Task PurgeCommand_ShouldHandleFailures()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        api.PurgeResponses.Enqueue(new VectorTileCachePurgeResult(
            new[] { "my-service/cities" },
            new[] { "my-service/roads" }));

        var command = new VectorCachePurgeCommand(console, api);
        var settings = new VectorCachePurgeCommand.Settings
        {
            ServiceId = "my-service",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("Failed");
    }

    [Fact]
    public async Task PurgeCommand_ShouldSupportDryRun()
    {
        var console = new TestConsole();
        var api = new FakeVectorTileCacheApiClient();

        var command = new VectorCachePurgeCommand(console, api);
        var settings = new VectorCachePurgeCommand.Settings
        {
            ServiceId = "my-service",
            LayerId = "cities",
            DryRun = true,
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("DRY RUN");
        Regex.IsMatch(console.Output, @"my-service/c.*ities", RegexOptions.Singleline)
            .Should().BeTrue("the layer ID should render as my-service/cities even when wrapped");
        api.PurgeRequests.Should().BeEmpty();
    }

    private static VectorTilePreseedJob CreateJob(
        Guid jobId,
        VectorTilePreseedJobStatus status,
        double progress,
        long tilesProcessed,
        long tilesTotal,
        string? message = null)
    {
        return new VectorTilePreseedJob(
            jobId,
            "my-service",
            "cities",
            status,
            progress,
            tilesProcessed,
            tilesTotal,
            message,
            DateTimeOffset.UtcNow,
            status == VectorTilePreseedJobStatus.Completed ? DateTimeOffset.UtcNow : null);
    }
}

internal sealed class FakeVectorTileCacheApiClient : IVectorTileCacheApiClient
{
    public Queue<VectorTilePreseedJob> EnqueueResponses { get; } = new();
    public Queue<VectorTilePreseedJob?> GetResponses { get; } = new();
    public Queue<IReadOnlyList<VectorTilePreseedJob>> ListResponses { get; } = new();
    public Queue<VectorTilePreseedJob?> CancelResponses { get; } = new();
    public Queue<VectorTileCachePurgeResult> PurgeResponses { get; } = new();
    public List<VectorTilePreseedJobRequest> Requests { get; } = new();
    public List<VectorTileCachePurgeRequest> PurgeRequests { get; } = new();
    public CacheStats? StatsResponse { get; set; }
    public CachePurgeAllResult? PurgeAllResponse { get; set; }
    public bool PurgeAllCalled { get; set; }

    public Task<VectorTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, VectorTilePreseedJobRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(EnqueueResponses.Dequeue());
    }

    public Task<VectorTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        return Task.FromResult(GetResponses.Dequeue());
    }

    public Task<IReadOnlyList<VectorTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        return Task.FromResult(ListResponses.Dequeue());
    }

    public Task<VectorTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        return Task.FromResult(CancelResponses.Dequeue());
    }

    public Task<VectorTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, VectorTileCachePurgeRequest request, CancellationToken cancellationToken)
    {
        PurgeRequests.Add(request);
        if (PurgeResponses.Count > 0)
        {
            return Task.FromResult(PurgeResponses.Dequeue());
        }

        // Default response - purge successful
        var purged = request.LayerId != null
            ? new[] { $"{request.ServiceId}/{request.LayerId}" }
            : new[] { request.ServiceId };
        return Task.FromResult(new VectorTileCachePurgeResult(purged, Array.Empty<string>()));
    }

    public Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        return Task.FromResult(StatsResponse ?? new CacheStats(0, 0, 0, 0));
    }

    public Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        PurgeAllCalled = true;
        return Task.FromResult(PurgeAllResponse ?? new CachePurgeAllResult(true, 0, null));
    }
}
