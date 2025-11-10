// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Elevation;
using Xunit;

namespace Honua.Server.Core.Tests.Elevation;

/// <summary>
/// Unit tests for AttributeElevationService.
/// Tests elevation retrieval from feature attributes.
/// </summary>
public class AttributeElevationServiceTests
{
    [Fact]
    public async Task GetElevationAsync_WithValidElevationAttribute_ReturnsElevation()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "elevation", 100.5 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elevation"
            }
        };

        // Act
        var elevation = await service.GetElevationAsync(-122.4194, 37.7749, context);

        // Assert
        Assert.NotNull(elevation);
        Assert.Equal(100.5, elevation.Value, precision: 2);
    }

    [Fact]
    public async Task GetElevationAsync_WithVerticalOffset_AppliesOffset()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "elev", 50.0 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elev",
                VerticalOffset = 10.0
            }
        };

        // Act
        var elevation = await service.GetElevationAsync(-122.4194, 37.7749, context);

        // Assert
        Assert.NotNull(elevation);
        Assert.Equal(60.0, elevation.Value, precision: 2);
    }

    [Fact]
    public async Task GetElevationAsync_WithDefaultElevation_ReturnsDefault()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "name", "test" }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "missing_attribute",
                DefaultElevation = 25.0
            }
        };

        // Act
        var elevation = await service.GetElevationAsync(-122.4194, 37.7749, context);

        // Assert
        Assert.NotNull(elevation);
        Assert.Equal(25.0, elevation.Value, precision: 2);
    }

    [Fact]
    public async Task GetElevationAsync_StringElevation_ParsesCorrectly()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "height", "123.45" }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "height"
            }
        };

        // Act
        var elevation = await service.GetElevationAsync(-122.4194, 37.7749, context);

        // Assert
        Assert.NotNull(elevation);
        Assert.Equal(123.45, elevation.Value, precision: 2);
    }

    [Fact]
    public async Task GetElevationsAsync_MultiplePoints_ReturnsSameElevation()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "elevation", 200.0 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elevation"
            }
        };

        var coordinates = new List<(double, double)>
        {
            (-122.4194, 37.7749),
            (-122.4184, 37.7759),
            (-122.4174, 37.7769)
        };

        // Act
        var elevations = await service.GetElevationsAsync(coordinates, context);

        // Assert
        Assert.Equal(3, elevations.Length);
        Assert.All(elevations, e => Assert.Equal(200.0, e!.Value, precision: 2));
    }

    [Fact]
    public void CanProvideElevation_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "elevation", 100.0 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elevation"
            }
        };

        // Act
        var canProvide = service.CanProvideElevation(context);

        // Assert
        Assert.True(canProvide);
    }

    [Fact]
    public void CanProvideElevation_WithoutAttributes_ReturnsFalse()
    {
        // Arrange
        var service = new AttributeElevationService();
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = null,
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                ElevationAttribute = "elevation"
            }
        };

        // Act
        var canProvide = service.CanProvideElevation(context);

        // Assert
        Assert.False(canProvide);
    }

    [Fact]
    public void GetBuildingHeight_WithValidHeightAttribute_ReturnsHeight()
    {
        // Arrange
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "building_height", 45.5 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                HeightAttribute = "building_height",
                IncludeHeight = true
            }
        };

        // Act
        var height = AttributeElevationService.GetBuildingHeight(context);

        // Assert
        Assert.NotNull(height);
        Assert.Equal(45.5, height.Value, precision: 2);
    }

    [Fact]
    public void GetBuildingHeight_WithoutIncludeHeight_ReturnsNull()
    {
        // Arrange
        var context = new ElevationContext
        {
            ServiceId = "test-service",
            LayerId = "test-layer",
            FeatureAttributes = new Dictionary<string, object?>
            {
                { "height", 30.0 }
            },
            Configuration = new ElevationConfiguration
            {
                Source = "attribute",
                HeightAttribute = "height",
                IncludeHeight = false
            }
        };

        // Act
        var height = AttributeElevationService.GetBuildingHeight(context);

        // Assert
        Assert.Null(height);
    }
}
