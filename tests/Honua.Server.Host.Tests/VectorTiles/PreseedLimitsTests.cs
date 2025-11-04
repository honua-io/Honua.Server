using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Host.VectorTiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.VectorTiles;

[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class PreseedLimitsTests
{
    [Fact]
    public void VectorTilePreseedLimits_DefaultValues_AreSecure()
    {
        var limits = new VectorTilePreseedLimits();

        limits.MaxTilesPerJob.Should().Be(100_000);
        limits.MaxConcurrentJobs.Should().Be(5);
        limits.MaxJobsPerUser.Should().Be(3);
        limits.JobTimeout.Should().Be(TimeSpan.FromHours(24));
        limits.MaxZoomLevel.Should().Be(18);
        limits.RateLimitWindow.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void VectorTilePreseedLimits_Validate_AcceptsValidConfiguration()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxTilesPerJob = 50000,
            MaxConcurrentJobs = 3,
            MaxJobsPerUser = 2,
            JobTimeout = TimeSpan.FromHours(12),
            MaxZoomLevel = 16,
            RateLimitWindow = TimeSpan.FromSeconds(5)
        };

        var act = () => limits.Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, "MaxTilesPerJob")]
    [InlineData(-1, "MaxTilesPerJob")]
    public void VectorTilePreseedLimits_Validate_RejectsInvalidMaxTilesPerJob(int value, string expectedField)
    {
        var limits = new VectorTilePreseedLimits { MaxTilesPerJob = value };

        var act = () => limits.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedField}*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VectorTilePreseedLimits_Validate_RejectsInvalidMaxConcurrentJobs(int value)
    {
        var limits = new VectorTilePreseedLimits { MaxConcurrentJobs = value };

        var act = () => limits.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxConcurrentJobs*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(23)]
    [InlineData(100)]
    public void VectorTilePreseedLimits_Validate_RejectsInvalidMaxZoomLevel(int value)
    {
        var limits = new VectorTilePreseedLimits { MaxZoomLevel = value };

        var act = () => limits.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxZoomLevel*");
    }

    [Fact]
    public async Task EnqueueAsync_RejectsTileCountAboveLimit()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxTilesPerJob = 1000, // Very small limit for testing
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10, // Would generate ~1.4 million tiles
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tile count*maximum allowed*");
    }

    [Fact]
    public async Task EnqueueAsync_RejectsZoomLevelAboveLimit()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxZoomLevel = 14
        };

        var service = CreateService(limits);

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 20, // Above limit
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum zoom level*exceeds allowed limit*");
    }

    [Fact]
    public async Task EnqueueAsync_RejectsWhenMaxConcurrentJobsReached()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxConcurrentJobs = 2,
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        // Enqueue first two jobs
        var request1 = new VectorTilePreseedRequest
        {
            ServiceId = "service1",
            LayerId = "layer1",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var request2 = new VectorTilePreseedRequest
        {
            ServiceId = "service2",
            LayerId = "layer2",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        await service.EnqueueAsync(request1, CancellationToken.None);
        await service.EnqueueAsync(request2, CancellationToken.None);

        // Third job should be rejected
        var request3 = new VectorTilePreseedRequest
        {
            ServiceId = "service3",
            LayerId = "layer3",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request3, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum concurrent jobs*reached*");
    }

    [Fact]
    public async Task EnqueueAsync_RejectsWhenMaxJobsPerUserReached()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxJobsPerUser = 2,
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        // Enqueue first two jobs for same service/layer
        var request1 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var request2 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 6,
            MaxZoom = 10,
            Overwrite = false
        };

        await service.EnqueueAsync(request1, CancellationToken.None);
        await service.EnqueueAsync(request2, CancellationToken.None);

        // Third job for same service/layer should be rejected
        var request3 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 11,
            MaxZoom = 15,
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request3, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Maximum jobs per service/layer*reached*");
    }

    [Fact]
    public async Task EnqueueAsync_AllowsJobsForDifferentServiceLayers()
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxJobsPerUser = 2,
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        // Enqueue jobs for different service/layer combinations
        var request1 = new VectorTilePreseedRequest
        {
            ServiceId = "service1",
            LayerId = "layer1",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var request2 = new VectorTilePreseedRequest
        {
            ServiceId = "service2",
            LayerId = "layer2",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        var snapshot1 = await service.EnqueueAsync(request1, CancellationToken.None);
        var snapshot2 = await service.EnqueueAsync(request2, CancellationToken.None);

        snapshot1.Should().NotBeNull();
        snapshot2.Should().NotBeNull();
        snapshot1.JobId.Should().NotBe(snapshot2.JobId);
    }

    [Fact]
    public async Task EnqueueAsync_EnforcesRateLimit()
    {
        var limits = new VectorTilePreseedLimits
        {
            RateLimitWindow = TimeSpan.FromSeconds(2),
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        var request1 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // First request should succeed
        await service.EnqueueAsync(request1, CancellationToken.None);

        // Immediate second request should be rate limited
        var request2 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 6,
            MaxZoom = 10,
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request2, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Rate limit exceeded*");
    }

    [Fact]
    public async Task EnqueueAsync_AllowsRequestAfterRateLimitExpires()
    {
        var limits = new VectorTilePreseedLimits
        {
            RateLimitWindow = TimeSpan.FromMilliseconds(100),
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        var request1 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 0,
            MaxZoom = 5,
            Overwrite = false
        };

        // First request
        await service.EnqueueAsync(request1, CancellationToken.None);

        // Wait for rate limit to expire
        await Task.Delay(150);

        // Second request should succeed
        var request2 = new VectorTilePreseedRequest
        {
            ServiceId = "my-service",
            LayerId = "my-layer",
            MinZoom = 6,
            MaxZoom = 10,
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request2, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnqueueAsync_AcceptsValidRequest()
    {
        var limits = new VectorTilePreseedLimits();
        var service = CreateService(limits);

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = 0,
            MaxZoom = 10,
            Overwrite = false
        };

        var snapshot = await service.EnqueueAsync(request, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot.JobId.Should().NotBe(Guid.Empty);
        snapshot.Status.Should().Be(VectorTilePreseedJobStatus.Queued);
        snapshot.ServiceId.Should().Be("test-service");
        snapshot.LayerId.Should().Be("test-layer");
    }

    [Theory]
    [InlineData(0, 22)] // Entire world at all zoom levels - billions of tiles
    [InlineData(15, 22)] // High zoom range
    public async Task EnqueueAsync_RejectsDangerousZoomRanges(int minZoom, int maxZoom)
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxTilesPerJob = 100_000,
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = minZoom,
            MaxZoom = maxZoom,
            Overwrite = false
        };

        var act = async () => await service.EnqueueAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0, 5, 21)]   // 21 tiles total
    [InlineData(0, 10, 1365)] // 1,365 tiles total
    [InlineData(10, 12, 21845)] // 21,845 tiles total
    public async Task EnqueueAsync_AcceptsReasonableZoomRanges(int minZoom, int maxZoom, long expectedTiles)
    {
        var limits = new VectorTilePreseedLimits
        {
            MaxTilesPerJob = 100_000,
            MaxZoomLevel = 22
        };

        var service = CreateService(limits);

        var request = new VectorTilePreseedRequest
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            MinZoom = minZoom,
            MaxZoom = maxZoom,
            Overwrite = false
        };

        var snapshot = await service.EnqueueAsync(request, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot.TilesTotal.Should().Be(expectedTiles);
    }

    private static VectorTilePreseedService CreateService(VectorTilePreseedLimits limits)
    {
        var mockRepo = new Mock<IFeatureRepository>();
        mockRepo.Setup(r => r.GenerateMvtTileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        var mockLogger = new Mock<ILogger<VectorTilePreseedService>>();
        var options = Options.Create(limits);

        return new VectorTilePreseedService(mockRepo.Object, mockLogger.Object, options);
    }
}
