// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Npgsql;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

[Collection("SharedPostgres")]
public class PostgresGeofenceEventRepositoryTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private readonly GeometryFactory _geometryFactory;
    private PostgresGeofenceEventRepository? _repository;

    public PostgresGeofenceEventRepositoryTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available");
        }

        await CreateSchemaAsync();

        _repository = new PostgresGeofenceEventRepository(
            _fixture.ConnectionString,
            NullLogger<PostgresGeofenceEventRepository>.Instance);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAsync_ValidEvent_ShouldPersist()
    {
        // Arrange
        var geofenceEvent = new GeofenceEvent
        {
            Id = Guid.NewGuid(),
            EventType = GeofenceEventType.Enter,
            EventTime = DateTime.UtcNow,
            GeofenceId = Guid.NewGuid(),
            GeofenceName = "Test Geofence",
            EntityId = "entity-1",
            EntityType = "vehicle",
            Location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8)),
            Properties = new Dictionary<string, object> { ["speed"] = 45.5 },
            TenantId = "tenant-1"
        };

        // Act
        var result = await _repository!.CreateAsync(geofenceEvent);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(geofenceEvent.Id);
        result.EventType.Should().Be(GeofenceEventType.Enter);
        result.EntityId.Should().Be("entity-1");
    }

    [Fact]
    public async Task CreateBatchAsync_MultipleEvents_ShouldPersistAll()
    {
        // Arrange
        var events = new List<GeofenceEvent>
        {
            CreateTestEvent("entity-1", GeofenceEventType.Enter),
            CreateTestEvent("entity-1", GeofenceEventType.Exit),
            CreateTestEvent("entity-2", GeofenceEventType.Enter)
        };

        // Act
        await _repository!.CreateBatchAsync(events);

        // Assert - Query to verify all were persisted
        var retrieved = await _repository.QueryEventsAsync(
            entityIds: new[] { "entity-1", "entity-2" },
            limit: 10);

        retrieved.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingEvent_ShouldReturn()
    {
        // Arrange
        var geofenceEvent = CreateTestEvent("entity-3", GeofenceEventType.Enter);
        await _repository!.CreateAsync(geofenceEvent);

        // Act
        var result = await _repository.GetByIdAsync(geofenceEvent.Id, geofenceEvent.TenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(geofenceEvent.Id);
        result.EventType.Should().Be(GeofenceEventType.Enter);
        result.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ShouldReturnNull()
    {
        // Act
        var result = await _repository!.GetByIdAsync(Guid.NewGuid(), null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryEventsAsync_FilterByEntityId_ShouldReturnMatching()
    {
        // Arrange
        var entityId = "entity-4";
        await _repository!.CreateAsync(CreateTestEvent(entityId, GeofenceEventType.Enter));
        await _repository.CreateAsync(CreateTestEvent(entityId, GeofenceEventType.Exit));
        await _repository.CreateAsync(CreateTestEvent("other-entity", GeofenceEventType.Enter));

        // Act
        var results = await _repository.QueryEventsAsync(entityIds: new[] { entityId }, limit: 100);

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(2);
        results.Should().OnlyContain(e => e.EntityId == entityId);
    }

    [Fact]
    public async Task QueryEventsAsync_FilterByGeofenceId_ShouldReturnMatching()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        await _repository!.CreateAsync(CreateTestEvent("entity-5", GeofenceEventType.Enter, geofenceId));
        await _repository.CreateAsync(CreateTestEvent("entity-6", GeofenceEventType.Exit, geofenceId));
        await _repository.CreateAsync(CreateTestEvent("entity-7", GeofenceEventType.Enter, Guid.NewGuid()));

        // Act
        var results = await _repository.QueryEventsAsync(geofenceIds: new[] { geofenceId }, limit: 100);

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(2);
        results.Should().OnlyContain(e => e.GeofenceId == geofenceId);
    }

    [Fact]
    public async Task QueryEventsAsync_FilterByEventType_ShouldReturnMatching()
    {
        // Arrange
        var tenantId = "event-type-test";
        await _repository!.CreateAsync(CreateTestEvent("entity-8", GeofenceEventType.Enter, tenantId: tenantId));
        await _repository.CreateAsync(CreateTestEvent("entity-9", GeofenceEventType.Enter, tenantId: tenantId));
        await _repository.CreateAsync(CreateTestEvent("entity-10", GeofenceEventType.Exit, tenantId: tenantId));

        // Act
        var results = await _repository.QueryEventsAsync(
            eventTypes: new[] { GeofenceEventType.Enter },
            tenantId: tenantId,
            limit: 100);

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(2);
        results.Should().OnlyContain(e => e.EventType == GeofenceEventType.Enter);
    }

    [Fact]
    public async Task QueryEventsAsync_FilterByTimeRange_ShouldReturnMatching()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldEvent = CreateTestEvent("entity-11", GeofenceEventType.Enter);
        oldEvent.EventTime = now.AddHours(-2);
        var recentEvent = CreateTestEvent("entity-12", GeofenceEventType.Enter);
        recentEvent.EventTime = now.AddMinutes(-10);

        await _repository!.CreateAsync(oldEvent);
        await _repository.CreateAsync(recentEvent);

        // Act - Query for events in last hour
        var results = await _repository.QueryEventsAsync(
            startTime: now.AddHours(-1),
            limit: 100);

        // Assert
        results.Should().Contain(e => e.Id == recentEvent.Id);
        results.Should().NotContain(e => e.Id == oldEvent.Id);
    }

    [Fact]
    public async Task QueryEventsAsync_WithPagination_ShouldRespectLimitAndOffset()
    {
        // Arrange
        var tenantId = "pagination-test";
        for (int i = 0; i < 5; i++)
        {
            await _repository!.CreateAsync(CreateTestEvent($"entity-{i}", GeofenceEventType.Enter, tenantId: tenantId));
        }

        // Act
        var page1 = await _repository!.QueryEventsAsync(tenantId: tenantId, limit: 2, offset: 0);
        var page2 = await _repository.QueryEventsAsync(tenantId: tenantId, limit: 2, offset: 2);

        // Assert
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1[0].Id.Should().NotBe(page2[0].Id);
    }

    [Fact]
    public async Task QueryEventsAsync_OrderByEventTime_ShouldReturnDescending()
    {
        // Arrange
        var tenantId = "order-test";
        var now = DateTime.UtcNow;

        var event1 = CreateTestEvent("entity-13", GeofenceEventType.Enter, tenantId: tenantId);
        event1.EventTime = now.AddMinutes(-30);

        var event2 = CreateTestEvent("entity-14", GeofenceEventType.Enter, tenantId: tenantId);
        event2.EventTime = now.AddMinutes(-20);

        var event3 = CreateTestEvent("entity-15", GeofenceEventType.Enter, tenantId: tenantId);
        event3.EventTime = now.AddMinutes(-10);

        await _repository!.CreateAsync(event1);
        await _repository.CreateAsync(event2);
        await _repository.CreateAsync(event3);

        // Act
        var results = await _repository.QueryEventsAsync(tenantId: tenantId, limit: 3);

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(3);
        var orderedResults = results.Take(3).ToList();
        orderedResults[0].EventTime.Should().BeAfter(orderedResults[1].EventTime);
        orderedResults[1].EventTime.Should().BeAfter(orderedResults[2].EventTime);
    }

    [Fact]
    public async Task QueryEventsAsync_DifferentTenants_ShouldRespectTenancy()
    {
        // Arrange
        var tenant1Event = CreateTestEvent("entity-16", GeofenceEventType.Enter, tenantId: "tenant-1");
        var tenant2Event = CreateTestEvent("entity-17", GeofenceEventType.Enter, tenantId: "tenant-2");

        await _repository!.CreateAsync(tenant1Event);
        await _repository.CreateAsync(tenant2Event);

        // Act
        var tenant1Results = await _repository.QueryEventsAsync(tenantId: "tenant-1", limit: 100);

        // Assert
        tenant1Results.Should().Contain(e => e.Id == tenant1Event.Id);
        tenant1Results.Should().NotContain(e => e.Id == tenant2Event.Id);
    }

    [Fact]
    public async Task CreateAsync_ExitEventWithDwellTime_ShouldPersistDwellTime()
    {
        // Arrange
        var geofenceEvent = CreateTestEvent("entity-18", GeofenceEventType.Exit);
        geofenceEvent.DwellTimeSeconds = 300; // 5 minutes

        // Act
        await _repository!.CreateAsync(geofenceEvent);

        // Assert
        var retrieved = await _repository.GetByIdAsync(geofenceEvent.Id, geofenceEvent.TenantId);
        retrieved!.DwellTimeSeconds.Should().Be(300);
    }

    // Helper methods

    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geofence_events (
                id UUID PRIMARY KEY,
                event_type VARCHAR(20) NOT NULL,
                event_time TIMESTAMPTZ NOT NULL,
                geofence_id UUID NOT NULL,
                geofence_name VARCHAR(255) NOT NULL,
                entity_id VARCHAR(255) NOT NULL,
                entity_type VARCHAR(100),
                location geometry(Point, 4326) NOT NULL,
                properties JSONB,
                dwell_time_seconds INT,
                sensorthings_observation_id UUID,
                tenant_id VARCHAR(100),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_geofence_events_entity ON geofence_events(entity_id);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_geofence ON geofence_events(geofence_id);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_time_brin ON geofence_events USING BRIN(event_time);
            CREATE INDEX IF NOT EXISTS idx_geofence_events_tenant ON geofence_events(tenant_id);
        ");
    }

    private GeofenceEvent CreateTestEvent(
        string entityId,
        GeofenceEventType eventType,
        Guid? geofenceId = null,
        string? tenantId = null)
    {
        return new GeofenceEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EventTime = DateTime.UtcNow,
            GeofenceId = geofenceId ?? Guid.NewGuid(),
            GeofenceName = "Test Geofence",
            EntityId = entityId,
            EntityType = "test",
            Location = _geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8)),
            Properties = new Dictionary<string, object> { ["test"] = "value" },
            TenantId = tenantId
        };
    }
}
