using System.Threading.Tasks;
using Honua.Server.Core.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Features;

[Trait("Category", "Unit")]
public sealed class AdaptiveFeatureServiceTests
{
    private readonly Mock<IFeatureManagementService> _featureManagementMock;
    private readonly Mock<ILogger<AdaptiveFeatureService>> _loggerMock;

    public AdaptiveFeatureServiceTests()
    {
        _featureManagementMock = new Mock<IFeatureManagementService>();
        _loggerMock = new Mock<ILogger<AdaptiveFeatureService>>();
    }

    [Fact]
    public async Task GetCachingModeAsync_AdvancedCachingAvailable_ReturnsDistributed()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.IsFeatureAvailableAsync("AdvancedCaching", default))
            .ReturnsAsync(true);

        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("AdvancedCaching", default))
            .ReturnsAsync(FeatureStatus.Healthy("AdvancedCaching"));

        var service = CreateService();

        // Act
        var mode = await service.GetCachingModeAsync();

        // Assert
        Assert.Equal(CachingMode.Distributed, mode);
    }

    [Fact]
    public async Task GetCachingModeAsync_AdvancedCachingUnavailable_ReturnsInMemory()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.IsFeatureAvailableAsync("AdvancedCaching", default))
            .ReturnsAsync(false);

        var service = CreateService();

        // Act
        var mode = await service.GetCachingModeAsync();

        // Assert
        Assert.Equal(CachingMode.InMemory, mode);
    }

    [Fact]
    public async Task GetCachingModeAsync_AdvancedCachingDegraded_ReturnsInMemory()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.IsFeatureAvailableAsync("AdvancedCaching", default))
            .ReturnsAsync(true);

        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("AdvancedCaching", default))
            .ReturnsAsync(FeatureStatus.Degraded(
                "AdvancedCaching",
                40,
                DegradationType.Fallback,
                "Redis unavailable"));

        var service = CreateService();

        // Act
        var mode = await service.GetCachingModeAsync();

        // Assert
        Assert.Equal(CachingMode.InMemory, mode);
    }

    [Fact]
    public async Task GetRecommendedTileResolutionAsync_HealthyRasterProcessing_ReturnsSameResolution()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("AdvancedRasterProcessing", default))
            .ReturnsAsync(FeatureStatus.Healthy("AdvancedRasterProcessing"));

        var service = CreateService();

        // Act
        var resolution = await service.GetRecommendedTileResolutionAsync(TileResolution.High);

        // Assert
        Assert.Equal(TileResolution.High, resolution);
    }

    [Fact]
    public async Task GetRecommendedTileResolutionAsync_DegradedRasterProcessing_ReturnsLowerResolution()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("AdvancedRasterProcessing", default))
            .ReturnsAsync(FeatureStatus.Degraded(
                "AdvancedRasterProcessing",
                30,
                DegradationType.ReduceQuality,
                "High load"));

        var service = CreateService();

        // Act
        var resolution = await service.GetRecommendedTileResolutionAsync(TileResolution.High);

        // Assert
        Assert.Equal(TileResolution.Medium, resolution);
    }

    [Fact]
    public async Task GetSearchStrategyAsync_HealthySearch_ReturnsFullTextSearch()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("Search", default))
            .ReturnsAsync(FeatureStatus.Healthy("Search"));

        var service = CreateService();

        // Act
        var strategy = await service.GetSearchStrategyAsync();

        // Assert
        Assert.Equal(SearchStrategy.FullTextSearch, strategy);
    }

    [Fact]
    public async Task GetSearchStrategyAsync_UnavailableSearch_ReturnsDatabaseScan()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("Search", default))
            .ReturnsAsync(FeatureStatus.Unavailable("Search", "Index service down"));

        var service = CreateService();

        // Act
        var strategy = await service.GetSearchStrategyAsync();

        // Assert
        Assert.Equal(SearchStrategy.DatabaseScan, strategy);
    }

    [Fact]
    public async Task GetMetadataStrategyAsync_HealthyStac_ReturnsFullStac()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("StacCatalog", default))
            .ReturnsAsync(FeatureStatus.Healthy("StacCatalog"));

        var service = CreateService();

        // Act
        var strategy = await service.GetMetadataStrategyAsync();

        // Assert
        Assert.Equal(MetadataStrategy.FullStac, strategy);
    }

    [Fact]
    public async Task GetMetadataStrategyAsync_DegradedStac_ReturnsCachedStac()
    {
        // Arrange
        _featureManagementMock
            .Setup(x => x.GetFeatureStatusAsync("StacCatalog", default))
            .ReturnsAsync(FeatureStatus.Degraded(
                "StacCatalog",
                45,
                DegradationType.ReduceFunctionality,
                "Database slow"));

        var service = CreateService();

        // Act
        var strategy = await service.GetMetadataStrategyAsync();

        // Assert
        Assert.Equal(MetadataStrategy.CachedStac, strategy);
    }

    [Fact]
    public async Task GetRateLimitMultiplierAsync_AllHealthy_ReturnsOne()
    {
        // Arrange
        var statuses = new Dictionary<string, FeatureStatus>
        {
            ["Feature1"] = FeatureStatus.Healthy("Feature1"),
            ["Feature2"] = FeatureStatus.Healthy("Feature2"),
            ["Feature3"] = FeatureStatus.Healthy("Feature3")
        };

        _featureManagementMock
            .Setup(x => x.GetAllFeatureStatusesAsync(default))
            .ReturnsAsync(statuses);

        var service = CreateService();

        // Act
        var multiplier = await service.GetRateLimitMultiplierAsync();

        // Assert
        Assert.Equal(1.0, multiplier);
    }

    [Fact]
    public async Task GetRateLimitMultiplierAsync_HalfDegraded_ReturnsQuarter()
    {
        // Arrange
        var statuses = new Dictionary<string, FeatureStatus>
        {
            ["Feature1"] = FeatureStatus.Healthy("Feature1"),
            ["Feature2"] = FeatureStatus.Degraded("Feature2", 30, DegradationType.ReducePerformance, "Test"),
            ["Feature3"] = FeatureStatus.Unavailable("Feature3", "Test")
        };

        _featureManagementMock
            .Setup(x => x.GetAllFeatureStatusesAsync(default))
            .ReturnsAsync(statuses);

        var service = CreateService();

        // Act
        var multiplier = await service.GetRateLimitMultiplierAsync();

        // Assert
        Assert.Equal(0.25, multiplier); // >= 50% degraded
    }

    private AdaptiveFeatureService CreateService()
    {
        return new AdaptiveFeatureService(
            _featureManagementMock.Object,
            _loggerMock.Object);
    }
}
