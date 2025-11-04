using System;
using System.Collections.Generic;
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
public sealed class RasterCacheCommandsTests
{
    [Fact]
    public async Task PreseedCommand_ShouldEnqueueAndMonitorUntilCompleted()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient();
        var jobId = Guid.NewGuid();

        api.EnqueueResponses.Enqueue(CreateJob(jobId, RasterTilePreseedJobStatus.Queued, 0d, "Queued"));
        api.GetResponses.Enqueue(CreateJob(jobId, RasterTilePreseedJobStatus.Queued, 0d, "Queued"));
        api.GetResponses.Enqueue(CreateJob(jobId, RasterTilePreseedJobStatus.Running, 0.5d, "z1"));
        api.GetResponses.Enqueue(CreateJob(jobId, RasterTilePreseedJobStatus.Completed, 1d, "Completed"));

        var command = new RasterCachePreseedCommand(console, api);
        var settings = new RasterCachePreseedCommand.Settings
        {
            DatasetIds = new[] { "basemap" },
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
    public async Task PreseedCommand_ShouldRequireDatasetIds()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient();
        var command = new RasterCachePreseedCommand(console, api);
        var settings = new RasterCachePreseedCommand.Settings
        {
            DatasetIds = Array.Empty<string>()
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("dataset-id");
    }

    [Fact]
    public async Task PurgeCommand_ShouldReportResults()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient
        {
            PurgeResponses = new Queue<RasterTileCachePurgeResult>(new[]
            {
                new RasterTileCachePurgeResult(new[] { "basemap" }, Array.Empty<string>())
            })
        };

        var command = new RasterCachePurgeCommand(console, api, NullLogger<RasterCachePurgeCommand>.Instance);
        var settings = new RasterCachePurgeCommand.Settings
        {
            DatasetIds = new[] { "basemap" },
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Purged 1 dataset");
    }

    [Fact]
    public async Task JobsCommand_ShouldRenderTable()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient();
        var jobId = Guid.NewGuid();
        api.ListResponses.Add(CreateJob(jobId, RasterTilePreseedJobStatus.Running, 0.25d, "z0"));

        var command = new RasterCacheJobsCommand(console, api, NullLogger<RasterCacheJobsCommand>.Instance);
        var settings = new RasterCacheJobsCommand.Settings
        {
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain(jobId.ToString()[..8]);
    }

    [Fact]
    public async Task StatusCommand_ShouldDisplayJobDetails()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient();
        var jobId = Guid.NewGuid();
        api.GetResponses.Enqueue(CreateJob(jobId, RasterTilePreseedJobStatus.Running, 0.4d, "z2"));

        var command = new RasterCacheStatusCommand(console, api);
        var settings = new RasterCacheStatusCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain(jobId.ToString());
        console.Output.Should().Contain("Running");
    }

    [Fact]
    public async Task CancelCommand_ShouldReportCancellation()
    {
        var console = new TestConsole();
        var api = new FakeRasterTileCacheApiClient();
        var jobId = Guid.NewGuid();
        api.CancelResponse = CreateJob(jobId, RasterTilePreseedJobStatus.Cancelled, 0.6d, "Cancelled");

        var command = new RasterCacheCancelCommand(console, api, NullLogger<RasterCacheCancelCommand>.Instance);
        var settings = new RasterCacheCancelCommand.Settings
        {
            JobId = jobId.ToString(),
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Cancelled");
    }

    private static RasterTilePreseedJob CreateJob(Guid jobId, RasterTilePreseedJobStatus status, double progress, string stage)
    {
        return new RasterTilePreseedJob(
            jobId,
            status,
            progress,
            stage,
            null,
            DateTimeOffset.UtcNow,
            status is RasterTilePreseedJobStatus.Completed ? DateTimeOffset.UtcNow : null,
            new[] { "basemap" },
            "WorldWebMercatorQuad",
            256,
            true,
            "image/png",
            false,
            10,
            100);
    }

    private sealed class FakeRasterTileCacheApiClient : IRasterTileCacheApiClient
    {
        public Queue<RasterTilePreseedJob> EnqueueResponses { get; } = new();
        public Queue<RasterTilePreseedJob> GetResponses { get; } = new();
        public List<RasterTilePreseedJobRequest> Requests { get; } = new();
        public List<RasterTilePreseedJob> ListResponses { get; } = new();
        public RasterTilePreseedJob? CancelResponse { get; set; }
        public Queue<RasterTileCachePurgeResult> PurgeResponses { get; set; } = new();
        private RasterTilePreseedJob? _last;

        public Task<RasterTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, RasterTilePreseedJobRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (EnqueueResponses.Count > 0)
            {
                _last = EnqueueResponses.Dequeue();
            }

            _last ??= CreateJob(Guid.NewGuid(), RasterTilePreseedJobStatus.Queued, 0, "Queued");
            return Task.FromResult(_last);
        }

        public Task<RasterTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        {
            if (GetResponses.Count > 0)
            {
                _last = GetResponses.Dequeue();
            }

            return Task.FromResult(_last);
        }

        public Task<IReadOnlyList<RasterTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RasterTilePreseedJob>>(ListResponses.ToArray());
        }

        public Task<RasterTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, IReadOnlyList<string> datasetIds, CancellationToken cancellationToken)
        {
            if (PurgeResponses.Count > 0)
            {
                return Task.FromResult(PurgeResponses.Dequeue());
            }

            return Task.FromResult(new RasterTileCachePurgeResult(datasetIds, Array.Empty<string>()));
        }

        public Task<RasterTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        {
            if (CancelResponse is not null)
            {
                _last = CancelResponse;
                return Task.FromResult<RasterTilePreseedJob?>(_last);
            }

            if (_last is null)
            {
                return Task.FromResult<RasterTilePreseedJob?>(null);
            }

            _last = _last with { Status = RasterTilePreseedJobStatus.Cancelled, Stage = "Cancelled" };
            return Task.FromResult<RasterTilePreseedJob?>(_last);
        }

        public Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
        {
            var stats = new CacheStats(1000, 500000000, 5, 10);
            return Task.FromResult(stats);
        }

        public Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
        {
            var result = new CachePurgeAllResult(true, 100, "Purged successfully");
            return Task.FromResult(result);
        }
    }
}
