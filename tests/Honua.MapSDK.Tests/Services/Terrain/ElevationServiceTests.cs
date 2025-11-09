// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Services.Terrain;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.MapSDK.Tests.Services.Terrain;

/// <summary>
/// Unit tests for ElevationService.
/// </summary>
public class ElevationServiceTests
{
    private readonly Mock<ILogger<ElevationService>> _loggerMock;
    private readonly ElevationService _service;

    public ElevationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ElevationService>>();
        _service = new ElevationService(_loggerMock.Object);
    }

    [Fact]
    public void RegisterDataSource_ShouldStoreDataSource()
    {
        // Arrange
        var source = new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/path/to/dem.tif"
        };

        // Act
        _service.RegisterDataSource("test", source);

        // Assert - no exception thrown
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("test")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryElevationAsync_WithNoDataSource_ReturnsNull()
    {
        // Act
        var result = await _service.QueryElevationAsync(-122.5, 37.8);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryPathElevationAsync_WithValidPath_ReturnsProfile()
    {
        // Arrange
        var coordinates = new[]
        {
            new[] { -122.5, 37.8 },
            new[] { -122.6, 37.9 }
        };

        // Register a test data source
        var source = new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/test/path"
        };
        _service.RegisterDataSource("test", source);

        // Act
        var profile = await _service.QueryPathElevationAsync(coordinates, samplePoints: 10);

        // Assert
        profile.Should().NotBeNull();
        profile.Points.Should().HaveCountGreaterOrEqualTo(10);
        profile.TotalDistance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task QueryPathElevationAsync_WithSinglePoint_ThrowsException()
    {
        // Arrange
        var coordinates = new[]
        {
            new[] { -122.5, 37.8 }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.QueryPathElevationAsync(coordinates));
    }

    [Fact]
    public async Task QueryElevationBatchAsync_WithMultiplePoints_ReturnsArray()
    {
        // Arrange
        var points = new[]
        {
            new[] { -122.5, 37.8 },
            new[] { -122.6, 37.9 },
            new[] { -122.7, 38.0 }
        };

        var source = new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/test/path"
        };
        _service.RegisterDataSource("test", source);

        // Act
        var results = await _service.QueryElevationBatchAsync(points);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAreaElevationAsync_WithValidBounds_ReturnsGrid()
    {
        // Arrange
        var source = new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/test/path"
        };
        _service.RegisterDataSource("test", source);

        // Act
        var grid = await _service.QueryAreaElevationAsync(
            minLon: -122.6,
            minLat: 37.8,
            maxLon: -122.5,
            maxLat: 37.9,
            resolution: 10);

        // Assert
        grid.Should().NotBeNull();
        grid.Width.Should().BeGreaterThan(0);
        grid.Height.Should().BeGreaterThan(0);
        grid.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryPathElevationAsync_CalculatesCorrectStatistics()
    {
        // Arrange
        var coordinates = new[]
        {
            new[] { -122.5, 37.8 },
            new[] { -122.6, 37.9 },
            new[] { -122.7, 38.0 }
        };

        var source = new ElevationDataSource
        {
            Type = ElevationSourceType.LocalRaster,
            Path = "/test/path"
        };
        _service.RegisterDataSource("test", source);

        // Act
        var profile = await _service.QueryPathElevationAsync(coordinates, samplePoints: 20);

        // Assert
        profile.MaxElevation.Should().BeGreaterOrEqualTo(profile.MinElevation);
        profile.TotalDistance.Should().BeGreaterThan(0);
        profile.Points.Should().NotBeEmpty();

        // Check that distances are monotonically increasing
        for (int i = 1; i < profile.Points.Length; i++)
        {
            profile.Points[i].Distance.Should().BeGreaterOrEqualTo(profile.Points[i - 1].Distance);
        }
    }
}
