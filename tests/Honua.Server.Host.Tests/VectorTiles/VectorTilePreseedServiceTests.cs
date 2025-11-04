using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.VectorTiles;
using Xunit;

namespace Honua.Server.Host.Tests.VectorTiles;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class VectorTilePreseedServiceTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldCreateJobWithQueuedStatus()
    {
        var service = new FakeVectorTilePreseedService();
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var snapshot = await service.EnqueueAsync(request, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot.JobId.Should().NotBe(Guid.Empty);
        snapshot.Status.Should().Be(VectorTilePreseedJobStatus.Queued);
        snapshot.ServiceId.Should().Be("my-service");
        snapshot.LayerId.Should().Be("cities");
        snapshot.Progress.Should().Be(0.0);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldCalculateTotalTiles()
    {
        var service = new FakeVectorTilePreseedService();
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 0,
            MaxZoom = 2,
            Overwrite = false
        };

        var snapshot = await service.EnqueueAsync(request, CancellationToken.None);

        // Zoom 0: 1 tile (1x1)
        // Zoom 1: 4 tiles (2x2)
        // Zoom 2: 16 tiles (4x4)
        // Total: 21 tiles
        snapshot.TilesTotal.Should().Be(21);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldRejectInvalidZoomRange()
    {
        var service = new FakeVectorTilePreseedService();
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 10,
            MaxZoom = 5, // Invalid: max < min
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maxZoom*minZoom*");
    }

    [Fact]
    public async Task TryGetJob_ShouldReturnSnapshot_WhenJobExists()
    {
        var service = new FakeVectorTilePreseedService();
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var enqueued = await service.EnqueueAsync(request, CancellationToken.None);
        var snapshot = await service.TryGetJobAsync(enqueued.JobId, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.JobId.Should().Be(enqueued.JobId);
    }

    [Fact]
    public async Task TryGetJob_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var service = new FakeVectorTilePreseedService();
        var randomJobId = Guid.NewGuid();

        var snapshot = await service.TryGetJobAsync(randomJobId, CancellationToken.None);

        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task ListJobs_ShouldReturnAllJobs()
    {
        var service = new FakeVectorTilePreseedService();

        var request1 = new VectorTilePreseedRequest { ServiceId = "svc1", LayerId = "layer1", MinZoom = 0, MaxZoom = 5 };
        var request2 = new VectorTilePreseedRequest { ServiceId = "svc2", LayerId = "layer2", MinZoom = 0, MaxZoom = 5 };

        await service.EnqueueAsync(request1, CancellationToken.None);
        await service.EnqueueAsync(request2, CancellationToken.None);

        var jobs = await service.ListJobsAsync(CancellationToken.None);

        jobs.Should().HaveCount(2);
        jobs.Select(j => j.ServiceId).Should().Contain(new[] { "svc1", "svc2" });
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelRunningJob()
    {
        var service = new FakeVectorTilePreseedService();
        var request = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "cities",
            MinZoom = 0,
            MaxZoom = 10,
            Overwrite = false
        };

        var enqueued = await service.EnqueueAsync(request, CancellationToken.None);
        var cancelled = await service.CancelAsync(enqueued.JobId, "User requested");

        cancelled.Should().NotBeNull();
        cancelled!.Status.Should().Be(VectorTilePreseedJobStatus.Cancelled);
        cancelled.Message.Should().Be("User requested");
    }

    [Fact]
    public async Task CancelAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var service = new FakeVectorTilePreseedService();
        var randomJobId = Guid.NewGuid();

        var result = await service.CancelAsync(randomJobId, "Test");

        result.Should().BeNull();
    }
}

// Fake implementation for testing without background processing
internal sealed class FakeVectorTilePreseedService : IVectorTilePreseedService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, VectorTilePreseedJobSnapshot> _jobs = new();

    public Task<VectorTilePreseedJobSnapshot> EnqueueAsync(VectorTilePreseedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaxZoom < request.MinZoom)
        {
            throw new InvalidOperationException("maxZoom must be greater than or equal to minZoom");
        }

        var jobId = Guid.NewGuid();
        var totalTiles = CalculateTotalTiles(request.MinZoom, request.MaxZoom);

        var snapshot = new VectorTilePreseedJobSnapshot
        {
            JobId = jobId,
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
            Status = VectorTilePreseedJobStatus.Queued,
            Progress = 0.0,
            TilesProcessed = 0,
            TilesTotal = totalTiles,
            Message = null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = null
        };

        _jobs[jobId] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<VectorTilePreseedJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<VectorTilePreseedJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VectorTilePreseedJobSnapshot> jobs = _jobs.Values.ToList();
        return Task.FromResult(jobs);
    }

    public Task<VectorTilePreseedJobSnapshot?> CancelAsync(Guid jobId, string? reason = null)
    {
        if (_jobs.TryGetValue(jobId, out var snapshot))
        {
            var cancelled = snapshot with
            {
                Status = VectorTilePreseedJobStatus.Cancelled,
                Message = reason,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            _jobs[jobId] = cancelled;
            return Task.FromResult<VectorTilePreseedJobSnapshot?>(cancelled);
        }

        return Task.FromResult<VectorTilePreseedJobSnapshot?>(null);
    }

    private static long CalculateTotalTiles(int minZoom, int maxZoom)
    {
        long total = 0;
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var tilesAtZoom = 1L << zoom;
            total += tilesAtZoom * tilesAtZoom;
        }
        return total;
    }
}
