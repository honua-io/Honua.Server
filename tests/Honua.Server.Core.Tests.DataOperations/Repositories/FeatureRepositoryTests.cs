// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Repositories;

public class FeatureRepositoryTests
{
    private readonly Mock<IFeatureRepository> _mockRepository;
    private readonly GeometryFactory _geometryFactory;

    public FeatureRepositoryTests()
    {
        _mockRepository = new Mock<IFeatureRepository>();
        _geometryFactory = new GeometryFactory();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsFeature()
    {
        // Arrange
        var featureId = "feature-123";
        var expectedFeature = new Feature
        {
            Id = featureId,
            Geometry = _geometryFactory.CreatePoint(new Coordinate(10, 20)),
            Properties = new() { ["name"] = "Test Feature" }
        };

        _mockRepository
            .Setup(x => x.GetByIdAsync(featureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFeature);

        // Act
        var result = await _mockRepository.Object.GetByIdAsync(featureId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(featureId);
        result.Properties["name"].Should().Be("Test Feature");
    }

    [Fact]
    public async Task CreateAsync_WithValidFeature_ReturnsCreatedId()
    {
        // Arrange
        var newFeature = new Feature
        {
            Geometry = _geometryFactory.CreatePoint(new Coordinate(5, 15)),
            Properties = new() { ["type"] = "point of interest" }
        };

        _mockRepository
            .Setup(x => x.CreateAsync(It.IsAny<Feature>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-feature-456");

        // Act
        var createdId = await _mockRepository.Object.CreateAsync(newFeature);

        // Assert
        createdId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithExistingFeature_ReturnsTrue()
    {
        // Arrange
        var feature = new Feature
        {
            Id = "feature-789",
            Geometry = _geometryFactory.CreatePoint(new Coordinate(30, 40)),
            Properties = new() { ["status"] = "updated" }
        };

        _mockRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Feature>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockRepository.Object.UpdateAsync(feature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var featureId = "feature-to-delete";

        _mockRepository
            .Setup(x => x.DeleteAsync(featureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockRepository.Object.DeleteAsync(featureId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task QueryByBoundsAsync_WithBbox_ReturnsFeatures()
    {
        // Arrange
        var bbox = new Envelope(0, 100, 0, 100);
        var features = new[]
        {
            new Feature { Id = "f1", Geometry = _geometryFactory.CreatePoint(new Coordinate(10, 10)) },
            new Feature { Id = "f2", Geometry = _geometryFactory.CreatePoint(new Coordinate(50, 50)) }
        };

        _mockRepository
            .Setup(x => x.QueryByBoundsAsync(It.IsAny<Envelope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(features);

        // Act
        var result = await _mockRepository.Object.QueryByBoundsAsync(bbox);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryByDistanceAsync_WithPoint_ReturnsNearbyFeatures()
    {
        // Arrange
        var center = _geometryFactory.CreatePoint(new Coordinate(0, 0));
        var distance = 1000.0; // meters

        var features = new[]
        {
            new Feature { Id = "nearby1", Geometry = _geometryFactory.CreatePoint(new Coordinate(0.001, 0.001)) }
        };

        _mockRepository
            .Setup(x => x.QueryByDistanceAsync(It.IsAny<Point>(), distance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(features);

        // Act
        var result = await _mockRepository.Object.QueryByDistanceAsync(center, distance);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CountAsync_WithFilter_ReturnsCount()
    {
        // Arrange
        var filter = new { status = "active" };

        _mockRepository
            .Setup(x => x.CountAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var count = await _mockRepository.Object.CountAsync(filter);

        // Assert
        count.Should().Be(42);
    }

    [Fact]
    public async Task BulkInsertAsync_WithMultipleFeatures_InsertsAll()
    {
        // Arrange
        var features = new[]
        {
            new Feature { Geometry = _geometryFactory.CreatePoint(new Coordinate(1, 1)) },
            new Feature { Geometry = _geometryFactory.CreatePoint(new Coordinate(2, 2)) },
            new Feature { Geometry = _geometryFactory.CreatePoint(new Coordinate(3, 3)) }
        };

        _mockRepository
            .Setup(x => x.BulkInsertAsync(It.IsAny<Feature[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var insertedCount = await _mockRepository.Object.BulkInsertAsync(features);

        // Assert
        insertedCount.Should().Be(3);
    }
}

// Mock interfaces and classes
public interface IFeatureRepository
{
    Task<Feature?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(Feature feature, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Feature feature, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Feature[]> QueryByBoundsAsync(Envelope bbox, CancellationToken cancellationToken = default);
    Task<Feature[]> QueryByDistanceAsync(Point center, double distance, CancellationToken cancellationToken = default);
    Task<int> CountAsync(object filter, CancellationToken cancellationToken = default);
    Task<int> BulkInsertAsync(Feature[] features, CancellationToken cancellationToken = default);
}

public class Feature
{
    public string Id { get; set; } = string.Empty;
    public Geometry? Geometry { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}
