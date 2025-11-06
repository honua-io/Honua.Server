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

/// <summary>
/// Tests for multi-tenancy data isolation in geofence system
/// Ensures that tenants cannot access or affect each other's data
/// </summary>
public class MultiTenancyIsolationTests
{
    private readonly Mock<IGeofenceRepository> _mockGeofenceRepo;
    private readonly Mock<IEntityStateRepository> _mockStateRepo;
    private readonly Mock<IGeofenceEventRepository> _mockEventRepo;
    private readonly GeofenceEvaluationService _service;
    private readonly GeometryFactory _geometryFactory;

    public MultiTenancyIsolationTests()
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
    public async Task EvaluateLocation_TenantA_ShouldNotSeeTenantBGeofences()
    {
        // Arrange
        var entityId = "entity-tenant-a";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantA = "tenant-a";

        // Only tenant-a's geofences should be returned
        var tenantAGeofence = CreateGeofence("fence-a", tenantA);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantAGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantA);

        // Assert
        result.Should().NotBeNull();
        result.CurrentGeofences.Should().ContainSingle();
        result.CurrentGeofences[0].TenantId.Should().Be(tenantA);

        // Verify repository was called with correct tenant filter
        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), tenantA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateLocation_TenantB_ShouldNotSeeTenantAGeofences()
    {
        // Arrange
        var entityId = "entity-tenant-b";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantB = "tenant-b";

        var tenantBGeofence = CreateGeofence("fence-b", tenantB);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantBGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantB);

        // Assert
        result.CurrentGeofences.Should().ContainSingle();
        result.CurrentGeofences[0].TenantId.Should().Be(tenantB);

        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), tenantB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateLocation_EntityStateIsolation_BetweenTenants()
    {
        // Arrange - Tenant A has existing state
        var entityId = "shared-entity-id"; // Same entity ID across tenants
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantA = "tenant-a";
        var tenantB = "tenant-b";

        var tenantAGeofence = CreateGeofence(Guid.NewGuid(), "fence-a", tenantA);
        var tenantBGeofence = CreateGeofence(Guid.NewGuid(), "fence-b", tenantB);

        // Tenant A has state
        var tenantAState = new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = tenantAGeofence.Id,
            IsInside = true,
            TenantId = tenantA,
            EnteredAt = DateTime.UtcNow.AddMinutes(-10),
            LastUpdated = DateTime.UtcNow.AddMinutes(-10)
        };

        // Setup for Tenant A
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantAGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState> { tenantAState });

        // Setup for Tenant B - Should not see Tenant A's state
        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantBGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>()); // No state for tenant B

        // Act - Evaluate for Tenant A
        var resultA = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantA);

        // Act - Evaluate for Tenant B (same entity ID, different tenant)
        var resultB = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantB);

        // Assert - Tenant A should see existing state (no new event)
        resultA.Events.Should().BeEmpty(); // Still inside

        // Assert - Tenant B should generate new enter event (no prior state)
        resultB.Events.Should().ContainSingle();
        resultB.Events[0].EventType.Should().Be(GeofenceEventType.Enter);

        // Verify isolation - each tenant queried their own state
        _mockStateRepo.Verify(r => r.GetEntityStatesAsync(entityId, tenantA, It.IsAny<CancellationToken>()), Times.Once);
        _mockStateRepo.Verify(r => r.GetEntityStatesAsync(entityId, tenantB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateLocation_NullTenantId_ShouldWorkWithoutIsolation()
    {
        // Arrange - Single-tenant mode (tenant ID is null)
        var entityId = "entity-no-tenant";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var geofence = CreateGeofence("shared-fence", tenantId: null);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act
        var result = await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: null);

        // Assert
        result.Should().NotBeNull();
        result.CurrentGeofences.Should().ContainSingle();

        // Verify repository was called with null tenant
        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EventPersistence_ShouldIncludeTenantId()
    {
        // Arrange
        var entityId = "entity-with-tenant";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantId = "tenant-xyz";
        var geofence = CreateGeofence("fence-xyz", tenantId);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        GeofenceEvent? capturedEvent = null;
        _mockEventRepo.Setup(r => r.CreateBatchAsync(
                It.IsAny<List<GeofenceEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<List<GeofenceEvent>, CancellationToken>((events, _) =>
            {
                capturedEvent = events[0];
            })
            .ReturnsAsync(1);

        // Act
        await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantId);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.TenantId.Should().Be(tenantId);

        _mockEventRepo.Verify(r => r.CreateBatchAsync(
            It.Is<List<GeofenceEvent>>(events => events[0].TenantId == tenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatePersistence_ShouldIncludeTenantId()
    {
        // Arrange
        var entityId = "entity-state-tenant";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var tenantId = "tenant-state";
        var geofence = CreateGeofence("fence-state", tenantId);

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { geofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(entityId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        EntityGeofenceState? capturedState = null;
        _mockStateRepo.Setup(r => r.UpsertStateAsync(
                It.IsAny<EntityGeofenceState>(), It.IsAny<CancellationToken>()))
            .Callback<EntityGeofenceState, CancellationToken>((state, _) =>
            {
                capturedState = state;
            })
            .Returns(Task.CompletedTask);

        // Act
        await _service.EvaluateLocationAsync(
            entityId, location, DateTime.UtcNow, tenantId: tenantId);

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.TenantId.Should().Be(tenantId);

        _mockStateRepo.Verify(r => r.UpsertStateAsync(
            It.Is<EntityGeofenceState>(s => s.TenantId == tenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MultipleTenantsSimultaneous_ShouldMaintainIsolation()
    {
        // Arrange - Simulate concurrent requests from different tenants
        var entityId = "concurrent-entity";
        var location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));

        var tenantAGeofence = CreateGeofence("fence-a", "tenant-a");
        var tenantBGeofence = CreateGeofence("fence-b", "tenant-b");
        var tenantCGeofence = CreateGeofence("fence-c", "tenant-c");

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantAGeofence });

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), "tenant-b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantBGeofence });

        _mockGeofenceRepo.Setup(r => r.FindGeofencesAtPointAsync(
                It.IsAny<double>(), It.IsAny<double>(), "tenant-c", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Geofence> { tenantCGeofence });

        _mockStateRepo.Setup(r => r.GetEntityStatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntityGeofenceState>());

        // Act - Run concurrent evaluations
        var tasks = new[]
        {
            _service.EvaluateLocationAsync(entityId, location, DateTime.UtcNow, tenantId: "tenant-a"),
            _service.EvaluateLocationAsync(entityId, location, DateTime.UtcNow, tenantId: "tenant-b"),
            _service.EvaluateLocationAsync(entityId, location, DateTime.UtcNow, tenantId: "tenant-c")
        };

        var results = await Task.WhenAll(tasks);

        // Assert - Each tenant should only see their own geofences
        results[0].CurrentGeofences.Should().ContainSingle();
        results[0].CurrentGeofences[0].TenantId.Should().Be("tenant-a");

        results[1].CurrentGeofences.Should().ContainSingle();
        results[1].CurrentGeofences[0].TenantId.Should().Be("tenant-b");

        results[2].CurrentGeofences.Should().ContainSingle();
        results[2].CurrentGeofences[0].TenantId.Should().Be("tenant-c");

        // Verify each tenant's repository calls were isolated
        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), "tenant-a", It.IsAny<CancellationToken>()), Times.Once);
        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), "tenant-b", It.IsAny<CancellationToken>()), Times.Once);
        _mockGeofenceRepo.Verify(r => r.FindGeofencesAtPointAsync(
            It.IsAny<double>(), It.IsAny<double>(), "tenant-c", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper methods

    private Geofence CreateGeofence(string name, string? tenantId)
    {
        return CreateGeofence(Guid.NewGuid(), name, tenantId);
    }

    private Geofence CreateGeofence(Guid id, string name, string? tenantId)
    {
        var coordinates = new[]
        {
            new Coordinate(-122.5, 37.7),
            new Coordinate(-122.3, 37.7),
            new Coordinate(-122.3, 37.9),
            new Coordinate(-122.5, 37.9),
            new Coordinate(-122.5, 37.7)
        };

        return new Geofence
        {
            Id = id,
            Name = name,
            Geometry = _geometryFactory.CreatePolygon(coordinates),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
