// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
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

public class GeofenceEvaluationServiceTests
{
    private readonly Mock<IGeofenceRepository> _mockGeofenceRepo;
    private readonly Mock<IEntityStateRepository> _mockStateRepo;
    private readonly Mock<IGeofenceEventRepository> _mockEventRepo;
    private readonly GeofenceEvaluationService _service;
    private readonly GeometryFactory _geometryFactory;

    public GeofenceEvaluationServiceTests()
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
    public async Task EvaluateLocationAsync_FirstEntry_ShouldGenerateEnterEvent()
    {
        // Arrange
        var entityId = "entity-1";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;
        var tenantId = "tenant-1";

        var geofence = CreateGeofence("geofence-1", "Test Fence", CreateSquarePolygon(-122.5, 37.7, 0.2));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.Events[0].EventType.Should().Be(GeofenceEventType.Enter);
        result.Events[0].GeofenceId.Should().Be(geofence.Id);
        result.Events[0].EntityId.Should().Be(entityId);
        result.CurrentGeofences.Should().ContainSingle()
            .Which.Id.Should().Be(geofence.Id);

        // Verify state was updated
        _mockStateRepo.Verify(r => r.UpsertStateAsync(
            It.Is<EntityGeofenceState>(s =>
                s.EntityId == entityId &&
                s.GeofenceId == geofence.Id &&
                s.IsInside == true),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify event was persisted
        _mockEventRepo.Verify(r => r.CreateBatchAsync(
            It.Is<List<GeofenceEvent>>(events => events.Count == 1 && events[0].EventType == GeofenceEventType.Enter),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateLocationAsync_Exit_ShouldGenerateExitEvent()
    {
        // Arrange
        var entityId = "entity-2";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.0, 37.5)); // Outside geofence
        var eventTime = DateTime.UtcNow;
        var geofenceId = Guid.NewGuid();
        var tenantId = "tenant-1";

        var existingState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = eventTime.AddMinutes(-10),
            LastUpdated = eventTime.AddMinutes(-10)
        };

        var geofence = CreateGeofence(geofenceId, "Test Fence", CreateSquarePolygon(-122.5, 37.7, 0.2));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>()); // No geofences at current location

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { existingState });

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.Events[0].EventType.Should().Be(GeofenceEventType.Exit);
        result.Events[0].GeofenceId.Should().Be(geofenceId);
        result.Events[0].EntityId.Should().Be(entityId);
        result.Events[0].DwellTimeSeconds.Should().BeGreaterThan(0);
        result.CurrentGeofences.Should().BeEmpty();

        // Verify state was deleted
        _mockStateRepo.Verify(r => r.DeleteStateAsync(
            entityId, geofenceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateLocationAsync_MultipleGeofences_ShouldHandleCorrectly()
    {
        // Arrange
        var entityId = "entity-3";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;
        var tenantId = "tenant-1";

        var geofence1 = CreateGeofence("geofence-1", "Fence 1", CreateSquarePolygon(-122.5, 37.7, 0.2));
        var geofence2 = CreateGeofence("geofence-2", "Fence 2", CreateSquarePolygon(-122.45, 37.75, 0.15));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence1, geofence2 });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(2);
        result.Events.Should().OnlyContain(e => e.EventType == GeofenceEventType.Enter);
        result.CurrentGeofences.Should().HaveCount(2);

        // Verify state was updated for both geofences
        _mockStateRepo.Verify(r => r.UpsertStateAsync(
            It.IsAny<EntityGeofenceState>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EvaluateLocationAsync_StillInside_ShouldNotGenerateEvents()
    {
        // Arrange
        var entityId = "entity-4";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;
        var geofenceId = Guid.NewGuid();
        var tenantId = "tenant-1";

        var geofence = CreateGeofence(geofenceId, "Test Fence", CreateSquarePolygon(-122.5, 37.7, 0.2));

        var existingState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = eventTime.AddMinutes(-5),
            LastUpdated = eventTime.AddMinutes(-5)
        };

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { existingState });

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().BeEmpty(); // No events generated
        result.CurrentGeofences.Should().ContainSingle();

        // Verify state was updated (timestamp)
        _mockStateRepo.Verify(r => r.UpsertStateAsync(
            It.IsAny<EntityGeofenceState>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify no events were persisted
        _mockEventRepo.Verify(r => r.CreateBatchAsync(
            It.IsAny<List<GeofenceEvent>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateLocationAsync_InactiveGeofence_ShouldBeIgnored()
    {
        // Arrange
        var entityId = "entity-5";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;
        var tenantId = "tenant-1";

        // FindGeofencesAtPointAsync should only return active geofences
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>()); // Empty - inactive geofences filtered out

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().BeEmpty();
        result.CurrentGeofences.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateLocationAsync_WithProperties_ShouldIncludeInEvent()
    {
        // Arrange
        var entityId = "entity-6";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var eventTime = DateTime.UtcNow;
        var properties = new Dictionary<string, object>
        {
            ["speed"] = 45.5,
            ["heading"] = 180,
            ["vehicle_type"] = "truck"
        };

        var geofence = CreateGeofence("geofence-1", "Test Fence", CreateSquarePolygon(-122.5, 37.7, 0.2));

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, eventTime, properties: properties);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.Events[0].Properties.Should().NotBeNull();
        result.Events[0].Properties.Should().ContainKey("speed");
        result.Events[0].Properties.Should().ContainKey("heading");
        result.Events[0].Properties.Should().ContainKey("vehicle_type");
    }

    [Fact]
    public async Task EvaluateLocationAsync_OnlyEnterEvents_WhenGeofenceDisablesExit()
    {
        // Arrange
        var entityId = "entity-7";
        var locationInside = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var locationOutside = _geometryFactory.CreatePoint(new Coordinate(-122.0, 37.5));
        var eventTime = DateTime.UtcNow;
        var geofenceId = Guid.NewGuid();

        var geofence = CreateGeofence(geofenceId, "Enter Only Fence", CreateSquarePolygon(-122.5, 37.7, 0.2));
        geofence.EnabledEventTypes = GeofenceEventTypes.Enter; // Only enter events

        // First evaluation - enter
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                -122.4, 37.8, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        var enterResult = await _service.EvaluateLocationAsync(
            entityId, locationInside, eventTime);

        // Second evaluation - exit (but exit events disabled)
        var exitState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = eventTime,
            LastUpdated = eventTime
        };

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                -122.0, 37.5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence>());

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { exitState });

        // Act
        var exitResult = await _service.EvaluateLocationAsync(
            entityId, locationOutside, eventTime.AddMinutes(5));

        // Assert
        enterResult.Events.Should().HaveCount(1);
        enterResult.Events[0].EventType.Should().Be(GeofenceEventType.Enter);

        exitResult.Events.Should().BeEmpty(); // No exit event generated
    }

    // Helper methods

    private Geofence CreateGeofence(Guid id, string name, Polygon geometry)
    {
        return new Geofence
        {
            Id = id,
            Name = name,
            Geometry = geometry,
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private Geofence CreateGeofence(string idString, string name, Polygon geometry)
    {
        return CreateGeofence(Guid.Parse(idString.PadRight(32, '0')), name, geometry);
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
            new Coordinate(centerLon - halfSize, centerLat - halfSize) // Close the ring
        };

        return _geometryFactory.CreatePolygon(coordinates);
    }
}
