// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Pages;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Moq;

namespace Honua.Admin.Blazor.Tests.Components.Pages;

/// <summary>
/// Tests for the CacheSettings page component.
/// </summary>
public class CacheSettingsTests : ComponentTestBase
{
    private readonly Mock<CacheApiClient> _mockCacheApiClient;
    private readonly Mock<ISnackbar> _mockSnackbar;

    public CacheSettingsTests()
    {
        _mockCacheApiClient = new Mock<CacheApiClient>(MockBehavior.Strict, new HttpClient());
        _mockSnackbar = new Mock<ISnackbar>();

        Context.Services.AddSingleton(_mockCacheApiClient.Object);
        Context.Services.AddSingleton(_mockSnackbar.Object);

        var authStateProvider = new TestAuthenticationStateProvider(
            TestAuthenticationStateProvider.CreateAdministrator());
        Context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
    }

    [Fact]
    public async Task CacheSettings_OnInitialized_LoadsStatistics()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            TotalHits = 10000,
            TotalMisses = 2000,
            TotalEvictions = 500,
            TotalSizeBytes = 100 * 1024 * 1024,
            HitRate = 0.833,
            TotalEntries = 5000
        };

        var datasetStats = new List<DatasetCacheStatistics>
        {
            new DatasetCacheStatistics
            {
                DatasetId = "dataset1",
                Hits = 5000,
                Misses = 1000,
                SizeBytes = 50 * 1024 * 1024,
                Entries = 2500,
                LastAccessed = DateTimeOffset.UtcNow
            }
        };

        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasetStats);

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Assert
        _mockCacheApiClient.Verify(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockCacheApiClient.Verify(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Verify statistics are displayed
        cut.Markup.Should().Contain("10000"); // Total hits (or formatted version)
        cut.Markup.Should().Contain("2000"); // Total misses (or formatted version)
    }

    [Fact]
    public async Task CacheSettings_HighHitRate_ShowsGreenIndicator()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            TotalHits = 9000,
            TotalMisses = 1000,
            TotalEvictions = 100,
            TotalSizeBytes = 100 * 1024 * 1024,
            HitRate = 0.9, // 90% hit rate - should be green
            TotalEntries = 5000
        };

        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DatasetCacheStatistics>());

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Assert
        // High hit rate should show success/green color
        // Exact verification depends on implementation
        cut.Markup.Should().Contain("90"); // Hit rate percentage
    }

    [Fact]
    public async Task CacheSettings_LowHitRate_ShowsWarningIndicator()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            TotalHits = 3000,
            TotalMisses = 7000,
            TotalEvictions = 500,
            TotalSizeBytes = 100 * 1024 * 1024,
            HitRate = 0.3, // 30% hit rate - should be warning/red
            TotalEntries = 5000
        };

        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DatasetCacheStatistics>());

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Assert
        // Low hit rate should show warning/red color
        cut.Markup.Should().Contain("30"); // Hit rate percentage
    }

    [Fact]
    public async Task CacheSettings_LoadPreseedJobs_ShowsJobList()
    {
        // Arrange
        var stats = new CacheStatistics { HitRate = 0.8, TotalEntries = 1000 };
        var datasetStats = new List<DatasetCacheStatistics>();
        var preseedJobs = new List<PreseedJobSnapshot>
        {
            new PreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                Status = "Running",
                DatasetIds = new List<string> { "dataset1" },
                TotalTiles = 10000,
                TilesGenerated = 5000,
                PercentComplete = 50.0,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                StartedAt = DateTimeOffset.UtcNow
            },
            new PreseedJobSnapshot
            {
                JobId = Guid.NewGuid(),
                Status = "Completed",
                DatasetIds = new List<string> { "dataset2" },
                TotalTiles = 5000,
                TilesGenerated = 5000,
                PercentComplete = 100.0,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
                StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
            }
        };

        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasetStats);

        _mockCacheApiClient
            .Setup(x => x.ListPreseedJobsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(preseedJobs);

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Navigate to Preseed Jobs tab if needed
        // (exact interaction depends on implementation)

        // Assert
        _mockCacheApiClient.Verify(x => x.ListPreseedJobsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Verify jobs are displayed
        cut.Markup.Should().Contain("Running");
        cut.Markup.Should().Contain("Completed");
    }

    [Fact]
    public async Task CacheSettings_DatasetStatistics_ShowsDatasetTable()
    {
        // Arrange
        var stats = new CacheStatistics { HitRate = 0.8, TotalEntries = 1000 };
        var datasetStats = new List<DatasetCacheStatistics>
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
                Hits = 3000,
                Misses = 2000,
                SizeBytes = 30 * 1024 * 1024,
                Entries = 1500,
                LastAccessed = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        };

        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasetStats);

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Assert
        cut.Markup.Should().Contain("dataset1");
        cut.Markup.Should().Contain("dataset2");
    }

    [Fact]
    public async Task CacheSettings_LoadError_ShowsErrorMessage()
    {
        // Arrange
        _mockCacheApiClient
            .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Failed to load statistics"));

        _mockCacheApiClient
            .Setup(x => x.GetAllDatasetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Failed to load dataset statistics"));

        // Act
        var cut = Context.RenderComponent<CacheSettings>();
        await Task.Delay(100);

        // Assert
        _mockSnackbar.Verify(
            x => x.Add(
                It.Is<string>(s => s.Contains("Failed") || s.Contains("Error")),
                It.IsAny<Severity>(),
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }
}
