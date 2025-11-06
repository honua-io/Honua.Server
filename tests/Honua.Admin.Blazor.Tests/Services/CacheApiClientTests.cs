// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;
using Moq;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for CacheApiClient.
/// </summary>
public class CacheApiClientTests
{
    [Fact]
    public async Task GetStatisticsAsync_Success_ReturnsStatistics()
    {
        // Arrange
        var expectedStats = new CacheStatistics
        {
            TotalHits = 10000,
            TotalMisses = 2000,
            TotalEvictions = 500,
            TotalSizeBytes = 1024 * 1024 * 100, // 100MB
            HitRate = 0.833,
            TotalEntries = 5000
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/statistics", expectedStats);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalHits.Should().Be(10000);
        result.HitRate.Should().Be(0.833);
        result.TotalSizeBytes.Should().Be(1024 * 1024 * 100);
    }

    [Fact]
    public async Task GetAllDatasetStatisticsAsync_Success_ReturnsDatasetStatistics()
    {
        // Arrange
        var expectedStats = new List<DatasetCacheStatistics>
        {
            new DatasetCacheStatistics
            {
                DatasetId = "dataset1",
                Hits = 5000,
                Misses = 1000,
                SizeBytes = 50 * 1024 * 1024,
                Entries = 2500,
                LastAccessed = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new DatasetCacheStatistics
            {
                DatasetId = "dataset2",
                Hits = 5000,
                Misses = 1000,
                SizeBytes = 50 * 1024 * 1024,
                Entries = 2500,
                LastAccessed = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/statistics/datasets", expectedStats);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetAllDatasetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].DatasetId.Should().Be("dataset1");
        result[1].DatasetId.Should().Be("dataset2");
    }

    [Fact]
    public async Task CreatePreseedJobAsync_Success_ReturnsJobSnapshot()
    {
        // Arrange
        var request = new CreatePreseedJobRequest
        {
            DatasetIds = new List<string> { "dataset1", "dataset2" },
            TileMatrixSetId = "WorldWebMercatorQuad",
            MinZoom = 0,
            MaxZoom = 10,
            Format = "image/png",
            Transparent = true,
            Overwrite = false
        };

        var expectedJob = new PreseedJobSnapshot
        {
            JobId = Guid.NewGuid(),
            Status = "Running",
            DatasetIds = new List<string> { "dataset1", "dataset2" },
            TotalTiles = 10000,
            TilesGenerated = 0,
            StartedAt = DateTimeOffset.UtcNow
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/jobs", new { job = expectedJob }, HttpStatusCode.OK);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CreatePreseedJobAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Running");
        result.DatasetIds.Should().HaveCount(2);
        result.TotalTiles.Should().Be(10000);
    }

    [Fact]
    public async Task ListPreseedJobsAsync_Success_ReturnsJobList()
    {
        // Arrange
        var expectedJobs = new List<PreseedJobSnapshot>
        {
            new PreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                Status = "Completed",
                TotalTiles = 5000,
                TilesGenerated = 5000
            },
            new PreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                Status = "Running",
                TotalTiles = 10000,
                TilesGenerated = 3000
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/jobs", new { jobs = expectedJobs });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.ListPreseedJobsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Status.Should().Be("Completed");
        result[1].TilesGenerated.Should().Be(3000);
    }

    [Fact]
    public async Task CancelPreseedJobAsync_Success_ReturnsTrue()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var mockFactory = new MockHttpClientFactory()
            .MockDelete($"/admin/raster-cache/jobs/{jobId}", HttpStatusCode.OK);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CancelPreseedJobAsync(jobId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PurgeCacheAsync_Success_ReturnsPurgeResult()
    {
        // Arrange
        var request = new PurgeCacheRequest
        {
            DatasetIds = new List<string> { "dataset1" }
        };

        var expectedResult = new PurgeCacheResult
        {
            Purged = new List<string> { "dataset1" },
            Failed = new List<string>()
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/datasets/purge", expectedResult);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.PurgeCacheAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Purged.Should().Contain("dataset1");
    }

    [Fact]
    public async Task ResetStatisticsAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/statistics/reset", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.ResetStatisticsAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetStatisticsAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/raster-cache/statistics", HttpStatusCode.InternalServerError);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.GetStatisticsAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreatePreseedJobAsync_InvalidZoomRange_ThrowsHttpRequestException()
    {
        // Arrange
        var request = new CreatePreseedJobRequest
        {
            DatasetIds = new List<string> { "dataset1" },
            MinZoom = 10,
            MaxZoom = 5, // Invalid: max < min
            TileMatrixSetId = "WorldWebMercatorQuad"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/raster-cache/jobs",
                HttpStatusCode.BadRequest, "Invalid zoom range");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.CreatePreseedJobAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CancelPreseedJobAsync_JobNotFound_ReturnsFalse()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Delete, $"/admin/raster-cache/jobs/{jobId}",
                HttpStatusCode.NotFound, "Job not found");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<CacheApiClient>>();
        var apiClient = new CacheApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CancelPreseedJobAsync(jobId);

        // Assert
        result.Should().BeFalse();
    }
}
