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
public sealed class UnifiedCacheCommandsTests
{
    [Fact]
    public async Task StatsCommand_ShouldDisplayRasterAndVectorStats()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient
        {
            TileCount = 15000,
            TotalSizeBytes = 250_000_000,
            DatasetCount = 3
        };
        var vectorApi = new FakeVectorStatsApiClient
        {
            TileCount = 8500,
            TotalSizeBytes = 125_000_000,
            ServiceCount = 2,
            LayerCount = 5
        };

        var command = new CacheStatsCommand(console, rasterApi, vectorApi, NullLogger<CacheStatsCommand>.Instance);
        var settings = new CacheStatsCommand.Settings
        {
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Raster");
        console.Output.Should().Contain("Vector");
        console.Output.Should().Contain("15,000");
        console.Output.Should().Contain("8,500");
    }

    [Fact]
    public async Task StatsCommand_ShouldFilterByType()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient { TileCount = 15000 };
        var vectorApi = new FakeVectorStatsApiClient { TileCount = 8500 };

        var command = new CacheStatsCommand(console, rasterApi, vectorApi, NullLogger<CacheStatsCommand>.Instance);
        var settings = new CacheStatsCommand.Settings
        {
            Type = "raster",
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Raster");
        console.Output.Should().NotContain("Vector");
    }

    [Fact]
    public async Task PurgeAllCommand_ShouldRequireConfirmation()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient();
        var vectorApi = new FakeVectorStatsApiClient();

        var command = new CachePurgeAllCommand(console, rasterApi, vectorApi, NullLogger<CachePurgeAllCommand>.Instance);
        var settings = new CachePurgeAllCommand.Settings
        {
            Host = "http://localhost:5000",
            Confirm = false
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("--confirm");
    }

    [Fact]
    public async Task PurgeAllCommand_ShouldPurgeBothCaches()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient();
        var vectorApi = new FakeVectorStatsApiClient();

        rasterApi.PurgeAllResponse = new CachePurgeAllResult(true, 15000, null);
        vectorApi.PurgeAllResponse = new CachePurgeAllResult(true, 8500, null);

        var command = new CachePurgeAllCommand(console, rasterApi, vectorApi, NullLogger<CachePurgeAllCommand>.Instance);
        var settings = new CachePurgeAllCommand.Settings
        {
            Host = "http://localhost:5000",
            Confirm = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Purged");
        console.Output.Should().Contain("15,000");
        console.Output.Should().Contain("8,500");
    }

    [Fact]
    public async Task PurgeAllCommand_ShouldFilterByType()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient();
        var vectorApi = new FakeVectorStatsApiClient();

        rasterApi.PurgeAllResponse = new CachePurgeAllResult(true, 15000, null);

        var command = new CachePurgeAllCommand(console, rasterApi, vectorApi, NullLogger<CachePurgeAllCommand>.Instance);
        var settings = new CachePurgeAllCommand.Settings
        {
            Type = "raster",
            Host = "http://localhost:5000",
            Confirm = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("raster");
        rasterApi.PurgeAllCalled.Should().BeTrue();
        vectorApi.PurgeAllCalled.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeAllCommand_ShouldSupportDryRun()
    {
        var console = new TestConsole();
        var rasterApi = new FakeRasterStatsApiClient();
        var vectorApi = new FakeVectorStatsApiClient();

        var command = new CachePurgeAllCommand(console, rasterApi, vectorApi, NullLogger<CachePurgeAllCommand>.Instance);
        var settings = new CachePurgeAllCommand.Settings
        {
            DryRun = true,
            Host = "http://localhost:5000"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("DRY RUN");
        rasterApi.PurgeAllCalled.Should().BeFalse();
        vectorApi.PurgeAllCalled.Should().BeFalse();
    }
}

internal sealed class FakeRasterStatsApiClient : IRasterTileCacheApiClient
{
    public long TileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public int DatasetCount { get; set; }
    public bool PurgeAllCalled { get; private set; }
    public CachePurgeAllResult? PurgeAllResponse { get; set; }

    public Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var stats = new CacheStats(TileCount, TotalSizeBytes, DatasetCount, 0);
        return Task.FromResult(stats);
    }

    public Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        PurgeAllCalled = true;
        return Task.FromResult(PurgeAllResponse ?? new CachePurgeAllResult(true, TileCount, null));
    }

    // Existing methods from interface
    public Task<RasterTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, RasterTilePreseedJobRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<RasterTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<RasterTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<RasterTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<RasterTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, IReadOnlyList<string> datasetIds, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

internal sealed class FakeVectorStatsApiClient : IVectorTileCacheApiClient
{
    public long TileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public int ServiceCount { get; set; }
    public int LayerCount { get; set; }
    public bool PurgeAllCalled { get; private set; }
    public CachePurgeAllResult? PurgeAllResponse { get; set; }

    public Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var stats = new CacheStats(TileCount, TotalSizeBytes, ServiceCount, LayerCount);
        return Task.FromResult(stats);
    }

    public Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        PurgeAllCalled = true;
        return Task.FromResult(PurgeAllResponse ?? new CachePurgeAllResult(true, TileCount, null));
    }

    // Existing methods from interface
    public Task<VectorTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, VectorTilePreseedJobRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<VectorTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<VectorTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<VectorTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<VectorTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, VectorTileCachePurgeRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
