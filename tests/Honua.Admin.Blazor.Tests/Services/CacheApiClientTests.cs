// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;

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
            CurrentSize = 1024 * 1024 * 100, // 100MB
            HitRate = 0.833,
            EntryCount = 5000
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/statistics", expectedStats);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var result = await apiClient.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalHits.Should().Be(10000);
        result.HitRate.Should().Be(0.833);
        result.CurrentSize.Should().Be(1024 * 1024 * 100);
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
                Size = 50 * 1024 * 1024,
                EntryCount = 2500,
                LastAccessed = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new DatasetCacheStatistics
            {
                DatasetId = "dataset2",
                Hits = 5000,
                Misses = 1000,
                Size = 50 * 1024 * 1024,
                EntryCount = 2500,
                LastAccessed = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/datasets/statistics", expectedStats);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

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
            TileMatrixSet = "WorldWebMercatorQuad",
            MinZoom = 0,
            MaxZoom = 10,
            Format = "PNG",
            Transparent = true,
            Overwrite = false
        };

        var expectedJob = new PreseedJobSnapshot
        {
            JobId = Guid.NewGuid(),
            Status = "Running",
            DatasetIds = new List<string> { "dataset1", "dataset2" },
            TotalTiles = 10000,
            ProcessedTiles = 0,
            FailedTiles = 0,
            StartedAt = DateTimeOffset.UtcNow
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/preseed", expectedJob, HttpStatusCode.Created);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

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
                ProcessedTiles = 5000,
                FailedTiles = 0
            },
            new PreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                Status = "Running",
                TotalTiles = 10000,
                ProcessedTiles = 3000,
                FailedTiles = 10
            }
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/raster-cache/preseed", expectedJobs);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var result = await apiClient.ListPreseedJobsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Status.Should().Be("Completed");
        result[1].ProcessedTiles.Should().Be(3000);
    }

    [Fact]
    public async Task CancelPreseedJobAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson($"/admin/raster-cache/preseed/{jobId}/cancel", new { success = true });

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var act = async () => await apiClient.CancelPreseedJobAsync(jobId);

        // Assert
        await act.Should().NotThrowAsync();
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
            PurgedEntries = 2500,
            FreedBytes = 50 * 1024 * 1024
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/purge", expectedResult);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var result = await apiClient.PurgeCacheAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PurgedEntries.Should().Be(2500);
        result.FreedBytes.Should().Be(50 * 1024 * 1024);
    }

    [Fact]
    public async Task ResetStatisticsAsync_Success_CompletesWithoutException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/raster-cache/statistics/reset", new { success = true });

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

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

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

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
            TileMatrixSet = "WorldWebMercatorQuad"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/raster-cache/preseed",
                HttpStatusCode.BadRequest, "Invalid zoom range");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var act = async () => await apiClient.CreatePreseedJobAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CancelPreseedJobAsync_JobNotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, $"/admin/raster-cache/preseed/{jobId}/cancel",
                HttpStatusCode.NotFound, "Job not found");

        var httpClient = mockFactory.CreateClient();
        var apiClient = new CacheApiClient(httpClient);

        // Act
        var act = async () => await apiClient.CancelPreseedJobAsync(jobId);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
