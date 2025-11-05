// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

[Collection("SharedPostgres")]
public class PostgresEntityStateRepositoryTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private PostgresEntityStateRepository? _repository;

    public PostgresEntityStateRepositoryTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new SkipException("PostgreSQL test container is not available");
        }

        await CreateSchemaAsync();

        _repository = new PostgresEntityStateRepository(
            _fixture.ConnectionString,
            NullLogger<PostgresEntityStateRepository>.Instance);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpsertStateAsync_NewState_ShouldInsert()
    {
        // Arrange
        var state = new EntityGeofenceState
        {
            EntityId = "entity-1",
            GeofenceId = Guid.NewGuid(),
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // Act
        await _repository!.UpsertStateAsync(state);

        // Assert - Retrieve and verify
        var states = await _repository.GetEntityStatesAsync(state.EntityId, null);
        states.Should().ContainSingle();
        states[0].EntityId.Should().Be(state.EntityId);
        states[0].GeofenceId.Should().Be(state.GeofenceId);
        states[0].IsInside.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertStateAsync_ExistingState_ShouldUpdate()
    {
        // Arrange - Insert initial state
        var state = new EntityGeofenceState
        {
            EntityId = "entity-2",
            GeofenceId = Guid.NewGuid(),
            IsInside = true,
            EnteredAt = DateTime.UtcNow.AddMinutes(-10),
            LastUpdated = DateTime.UtcNow.AddMinutes(-10)
        };
        await _repository!.UpsertStateAsync(state);

        // Act - Update state
        await Task.Delay(100); // Ensure LastUpdated will be different
        state.IsInside = false;
        state.LastUpdated = DateTime.UtcNow;
        await _repository.UpsertStateAsync(state);

        // Assert
        var states = await _repository.GetEntityStatesAsync(state.EntityId, null);
        states.Should().ContainSingle();
        states[0].IsInside.Should().BeFalse();
        states[0].LastUpdated.Should().BeAfter(state.EnteredAt!.Value);
    }

    [Fact]
    public async Task GetEntityStatesAsync_MultipleGeofences_ShouldReturnAll()
    {
        // Arrange
        var entityId = "entity-3";
        var geofence1 = Guid.NewGuid();
        var geofence2 = Guid.NewGuid();

        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofence1,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        });

        await _repository.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofence2,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        });

        // Act
        var states = await _repository.GetEntityStatesAsync(entityId, null);

        // Assert
        states.Should().HaveCount(2);
        states.Should().Contain(s => s.GeofenceId == geofence1);
        states.Should().Contain(s => s.GeofenceId == geofence2);
    }

    [Fact]
    public async Task GetEntityStatesAsync_DifferentTenants_ShouldRespectTenancy()
    {
        // Arrange
        var entityId = "entity-4";
        var geofenceId = Guid.NewGuid();

        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            TenantId = "tenant-1"
        });

        await _repository.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = Guid.NewGuid(),
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            TenantId = "tenant-2"
        });

        // Act
        var tenant1States = await _repository.GetEntityStatesAsync(entityId, "tenant-1");

        // Assert
        tenant1States.Should().ContainSingle();
        tenant1States[0].TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task GetStateAsync_ExistingState_ShouldReturn()
    {
        // Arrange
        var entityId = "entity-5";
        var geofenceId = Guid.NewGuid();

        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        });

        // Act
        var state = await _repository.GetStateAsync(entityId, geofenceId, null);

        // Assert
        state.Should().NotBeNull();
        state!.EntityId.Should().Be(entityId);
        state.GeofenceId.Should().Be(geofenceId);
        state.IsInside.Should().BeTrue();
    }

    [Fact]
    public async Task GetStateAsync_NonExistent_ShouldReturnNull()
    {
        // Act
        var state = await _repository!.GetStateAsync("nonexistent", Guid.NewGuid(), null);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task DeleteStateAsync_ExistingState_ShouldDelete()
    {
        // Arrange
        var entityId = "entity-6";
        var geofenceId = Guid.NewGuid();

        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        });

        // Act
        await _repository.DeleteStateAsync(entityId, geofenceId, null);

        // Assert
        var state = await _repository.GetStateAsync(entityId, geofenceId, null);
        state.Should().BeNull();
    }

    [Fact]
    public async Task CleanupStaleStatesAsync_OldStates_ShouldDelete()
    {
        // Arrange
        var entityId = "entity-7";
        var geofenceId = Guid.NewGuid();

        // Insert a state with old LastUpdated timestamp
        var oldTimestamp = DateTime.UtcNow.AddDays(-8); // Older than 7 days
        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = oldTimestamp,
            LastUpdated = oldTimestamp
        });

        // Act
        var deletedCount = await _repository.CleanupStaleStatesAsync(days: 7);

        // Assert
        deletedCount.Should().BeGreaterOrEqualTo(1);

        // Verify state was deleted
        var state = await _repository.GetStateAsync(entityId, geofenceId, null);
        state.Should().BeNull();
    }

    [Fact]
    public async Task CleanupStaleStatesAsync_RecentStates_ShouldNotDelete()
    {
        // Arrange
        var entityId = "entity-8";
        var geofenceId = Guid.NewGuid();

        // Insert a recent state
        await _repository!.UpsertStateAsync(new EntityGeofenceState
        {
            EntityId = entityId,
            GeofenceId = geofenceId,
            IsInside = true,
            EnteredAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        });

        // Act
        await _repository.CleanupStaleStatesAsync(days: 7);

        // Assert - State should still exist
        var state = await _repository.GetStateAsync(entityId, geofenceId, null);
        state.Should().NotBeNull();
    }

    // Helper methods

    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS entity_geofence_state (
                entity_id VARCHAR(255) NOT NULL,
                geofence_id UUID NOT NULL,
                is_inside BOOLEAN NOT NULL,
                entered_at TIMESTAMPTZ,
                last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                tenant_id VARCHAR(100),
                PRIMARY KEY (entity_id, geofence_id)
            );

            CREATE INDEX IF NOT EXISTS idx_entity_state_tenant ON entity_geofence_state(tenant_id);
            CREATE INDEX IF NOT EXISTS idx_entity_state_updated ON entity_geofence_state(last_updated);
        ");
    }
}
