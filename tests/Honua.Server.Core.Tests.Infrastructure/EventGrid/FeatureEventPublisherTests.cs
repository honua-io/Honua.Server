// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Hooks;
using Honua.Server.Core.Cloud.EventGrid.Models;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.EventGrid;

public class FeatureEventPublisherTests
{
    private readonly Mock<IEventGridPublisher> _mockPublisher;
    private readonly Mock<ILogger<FeatureEventPublisher>> _mockLogger;
    private readonly FeatureEventPublisher _publisher;

    public FeatureEventPublisherTests()
    {
        _mockPublisher = new Mock<IEventGridPublisher>();
        _mockLogger = new Mock<ILogger<FeatureEventPublisher>>();
        _publisher = new FeatureEventPublisher(_mockPublisher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task PublishFeatureCreatedAsync_PublishesCorrectEvent()
    {
        // Arrange
        var collectionId = "parcels";
        var featureId = "feature-123";
        var properties = new Dictionary<string, object?> { { "owner", "John Doe" } };
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var geometry = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantId = "tenant-abc";

        HonuaCloudEvent? capturedEvent = null;
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<HonuaCloudEvent>(), It.IsAny<CancellationToken>()))
            .Callback<HonuaCloudEvent, CancellationToken>((e, ct) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.PublishFeatureCreatedAsync(
            collectionId, featureId, properties, geometry, tenantId);

        // Assert
        _mockPublisher.Verify(
            p => p.PublishAsync(It.IsAny<HonuaCloudEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedEvent);
        Assert.Equal(HonuaEventTypes.FeatureCreated, capturedEvent.Type);
        Assert.Equal($"honua.io/features/{collectionId}", capturedEvent.Source);
        Assert.Equal(featureId, capturedEvent.Subject);
        Assert.Equal(tenantId, capturedEvent.TenantId);
        Assert.Equal(collectionId, capturedEvent.Collection);
        Assert.Equal("EPSG:4326", capturedEvent.Crs);
        Assert.NotNull(capturedEvent.BoundingBox);
    }

    [Fact]
    public async Task PublishFeatureUpdatedAsync_PublishesCorrectEvent()
    {
        // Arrange
        var collectionId = "parcels";
        var featureId = "feature-123";
        var properties = new Dictionary<string, object?> { { "owner", "Jane Doe" } };
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var geometry = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));

        HonuaCloudEvent? capturedEvent = null;
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<HonuaCloudEvent>(), It.IsAny<CancellationToken>()))
            .Callback<HonuaCloudEvent, CancellationToken>((e, ct) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.PublishFeatureUpdatedAsync(collectionId, featureId, properties, geometry);

        // Assert
        Assert.NotNull(capturedEvent);
        Assert.Equal(HonuaEventTypes.FeatureUpdated, capturedEvent.Type);
        Assert.Equal(featureId, capturedEvent.Subject);
    }

    [Fact]
    public async Task PublishFeatureDeletedAsync_PublishesCorrectEvent()
    {
        // Arrange
        var collectionId = "parcels";
        var featureId = "feature-123";

        HonuaCloudEvent? capturedEvent = null;
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<HonuaCloudEvent>(), It.IsAny<CancellationToken>()))
            .Callback<HonuaCloudEvent, CancellationToken>((e, ct) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.PublishFeatureDeletedAsync(collectionId, featureId);

        // Assert
        Assert.NotNull(capturedEvent);
        Assert.Equal(HonuaEventTypes.FeatureDeleted, capturedEvent.Type);
        Assert.Equal(featureId, capturedEvent.Subject);
        Assert.Null(capturedEvent.BoundingBox); // No geometry for delete
    }

    [Fact]
    public async Task PublishFeatureBatchCreatedAsync_PublishesMultipleEvents()
    {
        // Arrange
        var collectionId = "parcels";
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var features = new[]
        {
            ("feature-1", new Dictionary<string, object?> { { "owner", "John" } },
                geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8)) as Geometry),
            ("feature-2", new Dictionary<string, object?> { { "owner", "Jane" } },
                geometryFactory.CreatePoint(new Coordinate(-122.5, 37.9)) as Geometry)
        };

        var capturedEvents = new List<HonuaCloudEvent>();
        _mockPublisher
            .Setup(p => p.PublishBatchAsync(It.IsAny<IEnumerable<HonuaCloudEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<HonuaCloudEvent>, CancellationToken>((events, ct) =>
                capturedEvents.AddRange(events))
            .Returns(Task.CompletedTask);

        // Act
        await _publisher.PublishFeatureBatchCreatedAsync(collectionId, features);

        // Assert
        _mockPublisher.Verify(
            p => p.PublishBatchAsync(It.IsAny<IEnumerable<HonuaCloudEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(2, capturedEvents.Count);
        Assert.All(capturedEvents, e => Assert.Equal(HonuaEventTypes.FeatureCreated, e.Type));
    }

    [Fact]
    public async Task PublishFeatureCreatedAsync_WhenPublisherThrows_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        _mockPublisher
            .Setup(p => p.PublishAsync(It.IsAny<HonuaCloudEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Event Grid is down"));

        // Act & Assert (should not throw)
        await _publisher.PublishFeatureCreatedAsync(
            "parcels", "feature-123", new Dictionary<string, object?>(), null);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
