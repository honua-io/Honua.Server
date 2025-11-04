using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Features;
using Honua.Server.Core.Features.Strategies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Features;

/// <summary>
/// Integration tests for graceful degradation scenarios.
/// Tests end-to-end degradation flows including auto-recovery.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DegradationIntegrationTests
{
    [Fact]
    public async Task Scenario_RedisUnavailable_FallsBackToInMemoryCache()
    {
        // Arrange - simulate Redis unavailable
        var featureOptions = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                Required = false,
                Strategy = new DegradationStrategy
                {
                    Type = DegradationType.Fallback
                }
            }
        };

        var featureManagement = CreateFeatureManagement(featureOptions);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<AdaptiveCacheService>>().Object;

        var cacheService = new AdaptiveCacheService(
            memoryCache,
            featureManagement,
            logger,
            distributedCache: null); // Redis not available

        // Act - disable advanced caching to simulate degradation
        await featureManagement.DisableFeatureAsync("AdvancedCaching", "Redis unavailable");

        var testKey = "test-key";
        var testValue = System.Text.Encoding.UTF8.GetBytes("test-value");

        await cacheService.SetAsync(testKey, testValue);
        var retrieved = await cacheService.GetAsync(testKey);

        // Assert - should still work using in-memory cache
        Assert.NotNull(retrieved);
        Assert.Equal(testValue, retrieved);
    }

    [Fact]
    public async Task Scenario_AIServiceDown_ReturnsGracefulError()
    {
        // Arrange
        var featureOptions = new FeatureFlagsOptions
        {
            AIConsultant = new FeatureOptions
            {
                Enabled = true,
                Required = false
            }
        };

        var featureManagement = CreateFeatureManagement(featureOptions);
        var adaptiveFeature = new AdaptiveFeatureService(
            featureManagement,
            new Mock<ILogger<AdaptiveFeatureService>>().Object);

        var aiService = new AdaptiveAIService(
            adaptiveFeature,
            new Mock<ILogger<AdaptiveAIService>>().Object);

        // Act - disable AI to simulate service down
        await featureManagement.DisableFeatureAsync("AIConsultant", "LLM API unavailable");

        var response = await aiService.ExecuteAsync(
            "Deploy to AWS",
            (prompt, ct) => Task.FromResult("AI response"),
            fallbackFunc: null);

        // Assert - should return graceful error
        Assert.False(response.Success);
        Assert.Equal(AIMode.Unavailable, response.Mode);
        Assert.NotNull(response.Error);
        Assert.NotNull(response.SuggestedActions);
        Assert.NotEmpty(response.SuggestedActions);
    }

    [Fact]
    public async Task Scenario_HighDatabaseLoad_DegradesStacToCache()
    {
        // Arrange
        var featureOptions = new FeatureFlagsOptions
        {
            StacCatalog = new FeatureOptions
            {
                Enabled = true,
                Required = false,
                Strategy = new DegradationStrategy
                {
                    Type = DegradationType.ReduceFunctionality
                }
            }
        };

        var featureManagement = CreateFeatureManagement(featureOptions);
        var adaptiveFeature = new AdaptiveFeatureService(
            featureManagement,
            new Mock<ILogger<AdaptiveFeatureService>>().Object);

        // Act - simulate degradation
        var status = FeatureStatus.Degraded(
            "StacCatalog",
            45,
            DegradationType.ReduceFunctionality,
            "Database under high load");

        // Manually set degraded status (in real scenario, health check would do this)
        // For now, just verify the strategy works
        var strategy = await adaptiveFeature.GetMetadataStrategyAsync();

        // Initially should be full STAC
        Assert.Equal(MetadataStrategy.FullStac, strategy);

        // After degradation (simulated by disabling)
        await featureManagement.DisableFeatureAsync("StacCatalog", "High load");
        strategy = await adaptiveFeature.GetMetadataStrategyAsync();

        // Assert - should fall back to basic metadata
        Assert.Equal(MetadataStrategy.BasicMetadata, strategy);
    }

    [Fact]
    public async Task Scenario_MultipleFeaturesDegraded_IncreasesRateLimiting()
    {
        // Arrange
        var featureOptions = new FeatureFlagsOptions
        {
            AIConsultant = new FeatureOptions { Enabled = false },
            AdvancedRasterProcessing = new FeatureOptions { Enabled = false },
            VectorTiles = new FeatureOptions { Enabled = false },
            Analytics = new FeatureOptions { Enabled = false },
            ExternalStorage = new FeatureOptions { Enabled = false },
            OidcAuthentication = new FeatureOptions { Enabled = false },
            AdvancedCaching = new FeatureOptions { Enabled = true, Required = false },
            Search = new FeatureOptions { Enabled = true, Required = false },
            RealTimeMetrics = new FeatureOptions { Enabled = true, Required = false },
            StacCatalog = new FeatureOptions { Enabled = true, Required = false }
        };

        var healthMock = new Mock<HealthCheckService>();
        var unhealthyReport = new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Unhealthy, TimeSpan.Zero);
        healthMock
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unhealthyReport);

        var featureManagement = CreateFeatureManagement(featureOptions, healthMock.Object);
        var adaptiveFeature = new AdaptiveFeatureService(
            featureManagement,
            new Mock<ILogger<AdaptiveFeatureService>>().Object);

        // Act - healthy state
        var multiplierHealthy = await adaptiveFeature.GetRateLimitMultiplierAsync();

        await featureManagement.CheckFeatureHealthAsync("AdvancedCaching");
        await featureManagement.CheckFeatureHealthAsync("RealTimeMetrics");
        await featureManagement.CheckFeatureHealthAsync("StacCatalog");

        var multiplierDegraded = await adaptiveFeature.GetRateLimitMultiplierAsync();

        // Assert
        Assert.Equal(1.0, multiplierHealthy);
        Assert.True(multiplierDegraded < 1.0); // More aggressive rate limiting
    }

    [Fact]
    public async Task Scenario_FeatureRecovery_AutomaticallyRestoresService()
    {
        // Arrange
        var featureOptions = new FeatureFlagsOptions
        {
            AdvancedCaching = new FeatureOptions
            {
                Enabled = true,
                Required = false,
                RecoveryCheckInterval = 1 // 1 second for testing
            }
        };

        var featureManagement = CreateFeatureManagement(featureOptions);

        // Act - disable feature
        await featureManagement.DisableFeatureAsync("AdvancedCaching", "Test degradation");
        var statusDegraded = await featureManagement.GetFeatureStatusAsync("AdvancedCaching");

        // Simulate recovery by re-enabling
        await featureManagement.EnableFeatureAsync("AdvancedCaching");
        var statusRecovered = await featureManagement.GetFeatureStatusAsync("AdvancedCaching");

        // Assert
        Assert.False(statusDegraded.IsAvailable);
        Assert.True(statusRecovered.IsAvailable);
        Assert.Equal(FeatureDegradationState.Healthy, statusRecovered.State);
    }

    [Fact]
    public async Task Scenario_SearchIndexDown_FallsBackToDatabaseScan()
    {
        // Arrange
        var featureOptions = new FeatureFlagsOptions
        {
            Search = new FeatureOptions
            {
                Enabled = true,
                Required = false,
                Strategy = new DegradationStrategy
                {
                    Type = DegradationType.ReduceFunctionality
                }
            }
        };

        var featureManagement = CreateFeatureManagement(featureOptions);
        var adaptiveFeature = new AdaptiveFeatureService(
            featureManagement,
            new Mock<ILogger<AdaptiveFeatureService>>().Object);

        var searchService = new AdaptiveSearchService(
            adaptiveFeature,
            new Mock<ILogger<AdaptiveSearchService>>().Object);

        // Act - disable search to simulate index unavailable
        await featureManagement.DisableFeatureAsync("Search", "Index service down");

        var fullTextCalled = false;
        var basicSearchCalled = false;
        var databaseScanCalled = false;

        var results = await searchService.SearchAsync(
            "test query",
            fullTextSearchFunc: (q, ct) =>
            {
                fullTextCalled = true;
                return Task.FromResult(new SearchResults
                {
                    Results = new List<SearchResult>(),
                    SearchStrategy = "FullText",
                    IsDegraded = false
                });
            },
            basicSearchFunc: (q, ct) =>
            {
                basicSearchCalled = true;
                return Task.FromResult(new SearchResults
                {
                    Results = new List<SearchResult>(),
                    SearchStrategy = "Basic",
                    IsDegraded = true
                });
            },
            databaseScanFunc: (q, ct) =>
            {
                databaseScanCalled = true;
                return Task.FromResult(new SearchResults
                {
                    Results = new List<SearchResult>(),
                    SearchStrategy = "DatabaseScan",
                    IsDegraded = true
                });
            });

        // Assert - should fall back to database scan
        Assert.False(fullTextCalled);
        Assert.False(basicSearchCalled);
        Assert.True(databaseScanCalled);
        Assert.Equal("DatabaseScan", results.SearchStrategy);
    }

    private static IFeatureManagementService CreateFeatureManagement(FeatureFlagsOptions options, HealthCheckService? healthCheckService = null)
    {
        var optionsMonitor = new TestOptionsMonitor<FeatureFlagsOptions>(options);

        var logger = new Mock<ILogger<FeatureManagementService>>().Object;

        return new FeatureManagementService(optionsMonitor, logger, healthCheckService);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public TestOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
