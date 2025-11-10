// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

/// <summary>
/// Edge case tests for geofence evaluation
/// </summary>
public class GeofenceEdgeCaseTests
{
    private readonly Mock<IGeofenceRepository> _mockGeofenceRepo;
    private readonly Mock<IEntityStateRepository> _mockStateRepo;
    private readonly Mock<IGeofenceEventRepository> _mockEventRepo;
    private readonly GeofenceEvaluationService _service;
    private readonly GeometryFactory _geometryFactory;

    public GeofenceEdgeCaseTests()
    {
        _mockGeofenceRepo = new Mock<IGeofenceRepository>();
        _mockStateRepo = new Mock<IEntityStateRepository>();
        _mockEventRepo = new Mock<IGeofenceEventRepository>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        _service = new GeofenceEvaluationService(
            _mockGeofenceRepo.Object,
            _mockStateRepo.Object,
            _mockEventRepo.Object,
            NullLogger<GeofenceEvaluationService>.Instance);
    }

    [Fact]
    public async Task EvaluateLocation_PointOnBoundary_ShouldHandleGracefully()
    {
        // Arrange - Point exactly on polygon edge
        var entityId = "entity-boundary";
        var polygon = CreateSquarePolygon(-122.4, 37.8, 0.2);

        // Get a point exactly on the boundary (first coordinate)
        var boundaryPoint = polygon.Coordinates[0];
        var location = _geometryFactory.CreatePoint(boundaryPoint);

        var geofence = CreateGeofence("boundary-test", polygon);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert - Should handle boundary case without errors
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateLocation_VeryLargePolygon_ShouldProcess()
    {
        // Arrange - Create polygon with 1000 vertices
        var vertices = new List<Coordinate>();
        double angleIncrement = (2 * Math.PI) / 1000;
        double radius = 1.0;
        double centerLon = -122.4;
        double centerLat = 37.8;

        for (int i = 0; i < 1000; i++)
        {
            double angle = i * angleIncrement;
            vertices.Add(new Coordinate(
                centerLon + radius * Math.Cos(angle),
                centerLat + radius * Math.Sin(angle)
            ));
        }
        vertices.Add(vertices[0]); // Close the ring

        var largePolygon = _geometryFactory.CreatePolygon(vertices.ToArray());
        var geofence = CreateGeofence("large-polygon", largePolygon);

        var entityId = "entity-large";
        var location = _geometryFactory.CreatePoint(new Coordinate(centerLon, centerLat));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateLocation_ManyOverlappingGeofences_ShouldHandleAll()
    {
        // Arrange - 50 overlapping geofences at same location
        var entityId = "entity-overlap";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));

        var geofences = new List<Geofence>();
        for (int i = 0; i < 50; i++)
        {
            var size = 0.1 + (i * 0.01); // Gradually increasing sizes
            geofences.Add(CreateGeofence($"fence-{i}", CreateSquarePolygon(-122.4, 37.8, size)));
        }

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofences);

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(50);
        result.CurrentGeofences.Should().HaveCount(50);
    }

    [Fact]
    public async Task EvaluateLocation_RapidEntryExit_ShouldTrackCorrectly()
    {
        // Arrange - Simulate rapid entry and exit within seconds
        var entityId = "entity-rapid";
        var geofenceId = Guid.NewGuid();
        var geofence = CreateGeofence(geofenceId, CreateSquarePolygon(-122.4, 37.8, 0.2));

        var insideLocation = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var outsideLocation = _geometryFactory.CreatePoint(new Coordinate(-121.0, 38.5));

        var baseTime = DateTime.UtcNow;

        // Entry
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                -122.4, 37.8, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        var entryResult = await _service.EvaluateLocationAsync(
            entityId, insideLocation, baseTime);

        // Exit after 2 seconds
        var exitState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = baseTime,
            LastUpdated = baseTime
        };

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                -121.0, 38.5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>());

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        _mockGeofenceRepo.Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, string _, CancellationToken __) =>
                ids.Contains(geofenceId) ? new List<Geofence> { geofence } : new List<Geofence>());

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { exitState });

        // Act
        var exitResult = await _service.EvaluateLocationAsync(
            entityId, outsideLocation, baseTime.AddSeconds(2));

        // Assert
        entryResult.Events.Should().ContainSingle();
        entryResult.Events[0].EventType.Should().Be(GeofenceEventType.Enter);

        exitResult.Events.Should().ContainSingle();
        exitResult.Events[0].EventType.Should().Be(GeofenceEventType.Exit);
        exitResult.Events[0].DwellTimeSeconds.Should().Be(2);
    }

    [Fact]
    public async Task EvaluateLocation_NearPoles_ShouldHandleExtremeLatitudes()
    {
        // Arrange - Near North Pole
        var entityId = "entity-pole";
        var location = _geometryFactory.CreatePoint(new Coordinate(0.0, 89.9));

        var polarGeofence = CreateGeofence("polar-fence",
            CreateSquarePolygon(0.0, 89.5, 1.0));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                0.0, 89.9, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { polarGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert - Should handle without errors
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateLocation_DateLineOverlap_ShouldHandleGracefully()
    {
        // Arrange - Near international date line
        var entityId = "entity-dateline";
        var location = _geometryFactory.CreatePoint(new Coordinate(179.9, 0.0));

        var datelineGeofence = CreateGeofence("dateline-fence",
            CreateSquarePolygon(179.5, 0.0, 1.0));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                179.9, 0.0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { datelineGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateLocation_EmptyProperties_ShouldHandleGracefully()
    {
        // Arrange
        var entityId = "entity-no-props";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var geofence = CreateGeofence("test", CreateSquarePolygon(-122.5, 37.7, 0.2));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act - No properties provided
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, properties: null);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.Events[0].Properties.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateLocation_VeryLongEntityId_ShouldProcess()
    {
        // Arrange - Entity ID with 255 characters (max typical varchar length)
        var entityId = new string('a', 255);
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var geofence = CreateGeofence("test", CreateSquarePolygon(-122.5, 37.7, 0.2));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow);

        // Assert
        result.Should().NotBeNull();
        result.Events[0].EntityId.Should().Be(entityId);
    }

    [Fact]
    public async Task EvaluateLocation_PastEventTime_ShouldStillProcess()
    {
        // Arrange - Event time in the past
        var entityId = "entity-past";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var geofence = CreateGeofence("test", CreateSquarePolygon(-122.5, 37.7, 0.2));
        var pastTime = DateTime.UtcNow.AddDays(-1);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, pastTime);

        // Assert
        result.Should().NotBeNull();
        result.Events[0].EventTime.Should().Be(pastTime);
    }

    [Fact]
    public async Task EvaluateLocation_CancellationRequested_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var entityId = "entity-cancel";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.EvaluateLocationAsync(
                entityId, location, DateTime.UtcNow, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task EvaluateLocation_ExtremelyLongDwellTime_ShouldCalculateCorrectly()
    {
        // Arrange - Entity inside for 30 days
        var entityId = "entity-long-dwell";
        var geofenceId = Guid.NewGuid();
        var geofence = CreateGeofence(geofenceId, CreateSquarePolygon(-122.4, 37.8, 0.2));
        var outsideLocation = _geometryFactory.CreatePoint(new Coordinate(-121.0, 38.5));

        var enteredAt = DateTime.UtcNow.AddDays(-30);
        var exitTime = DateTime.UtcNow;

        var existingState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = enteredAt,
            LastUpdated = enteredAt
        };

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>());

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        _mockGeofenceRepo.Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, string _, CancellationToken __) =>
                ids.Contains(geofenceId) ? new List<Geofence> { geofence } : new List<Geofence>());

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { existingState });

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, outsideLocation, exitTime);

        // Assert
        result.Events.Should().ContainSingle();
        result.Events[0].DwellTimeSeconds.Should().BeGreaterThanOrEqualTo(2592000); // 30 days in seconds
    }

    // Helper methods

    private Geofence CreateGeofence(Guid id, Polygon geometry)
    {
        return new Geofence
        {
            Id = id,
            Name = "Test Geofence",
            Geometry = geometry,
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private Geofence CreateGeofence(string idString, Polygon geometry)
    {
        var fullHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idString));
        Span<byte> guidBytes = stackalloc byte[16];
        fullHash.AsSpan(0, 16).CopyTo(guidBytes);
        var guid = new Guid(guidBytes);
        return CreateGeofence(guid, geometry);
    }

    private Polygon CreateSquarePolygon(double centerLon, double centerLat, double size)
    {
        var halfSize = size / 2;
        var coordinates = new[]
        {
            new Coordinate(centerLon - halfSize, centerLat - halfSize),
            new Coordinate(centerLon + halfSize, centerLat - halfSize),
            new Coordinate(centerLon + halfSize, centerLat + halfSize),
            new Coordinate(centerLon - halfSize, centerLat + halfSize),
            new Coordinate(centerLon - halfSize, centerLat - halfSize)
        };

        return _geometryFactory.CreatePolygon(coordinates);
    }
}
