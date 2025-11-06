// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
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
public class PostgresGeofenceRepositoryTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private readonly GeometryFactory _geometryFactory;
    private PostgresGeofenceRepository? _repository;

    public PostgresGeofenceRepositoryTests(SharedPostgresFixture fixture)
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

        _repository = new PostgresGeofenceRepository(
            _fixture.ConnectionString,
            NullLogger<PostgresGeofenceRepository>.Instance);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAsync_ValidGeofence_ShouldPersist()
    {
        // Arrange
        var geofence = new Geofence
        {
            Id = Guid.NewGuid(),
            Name = "Test Geofence",
            Description = "A test geofence",
            Geometry = CreateSquarePolygon(-122.4, 37.8, 0.1),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            Properties = new Dictionary<string, object> { ["test"] = "value" },
            TenantId = "tenant-1"
        };

        // Act
        var result = await _repository!.CreateAsync(geofence);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(geofence.Id);
        result.Name.Should().Be("Test Geofence");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify it was actually persisted
        var retrieved = await _repository.GetByIdAsync(geofence.Id, "tenant-1");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Geofence");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGeofence_ShouldReturn()
    {
        // Arrange
        var geofence = await CreateTestGeofenceAsync("Get By ID Test");

        // Act
        var result = await _repository!.GetByIdAsync(geofence.Id, geofence.TenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(geofence.Id);
        result.Name.Should().Be("Get By ID Test");
        result.Geometry.Should().NotBeNull();
        result.Geometry.SRID.Should().Be(4326);
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
    public async Task UpdateAsync_ExistingGeofence_ShouldUpdate()
    {
        // Arrange
        var geofence = await CreateTestGeofenceAsync("Original Name");
        await Task.Delay(100); // Ensure UpdatedAt will be different

        geofence.Name = "Updated Name";
        geofence.Description = "Updated Description";
        geofence.IsActive = false;

        // Act
        var updated = await _repository!.UpdateAsync(geofence);

        // Assert
        updated.Should().BeTrue();

        // Verify persistence
        var retrieved = await _repository.GetByIdAsync(geofence.Id, geofence.TenantId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Description.Should().Be("Updated Description");
        retrieved.IsActive.Should().BeFalse();
        retrieved.UpdatedAt.Should().BeAfter(retrieved.CreatedAt);
    }

    [Fact]
    public async Task DeleteAsync_ExistingGeofence_ShouldDelete()
    {
        // Arrange
        var geofence = await CreateTestGeofenceAsync("To Be Deleted");

        // Act
        var result = await _repository!.DeleteAsync(geofence.Id, geofence.TenantId);

        // Assert
        result.Should().BeTrue();

        // Verify it's gone
        var retrieved = await _repository.GetByIdAsync(geofence.Id, geofence.TenantId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ShouldReturnFalse()
    {
        // Act
        var result = await _repository!.DeleteAsync(Guid.NewGuid(), null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_WithFilters_ShouldReturnFiltered()
    {
        // Arrange
        await CreateTestGeofenceAsync("Active 1", isActive: true, tenantId: "tenant-1");
        await CreateTestGeofenceAsync("Active 2", isActive: true, tenantId: "tenant-1");
        await CreateTestGeofenceAsync("Inactive", isActive: false, tenantId: "tenant-1");
        await CreateTestGeofenceAsync("Other Tenant", isActive: true, tenantId: "tenant-2");

        // Act - Get active geofences for tenant-1
        var result = await _repository!.GetAllAsync(isActive: true, tenantId: "tenant-1", limit: 100, offset: 0);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Should().OnlyContain(g => g.IsActive && g.TenantId == "tenant-1");
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ShouldRespectLimitAndOffset()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await CreateTestGeofenceAsync($"Fence {i}", tenantId: "page-test");
        }

        // Act - Get page 1 (first 2)
        var page1 = await _repository!.GetAllAsync(null, "page-test", limit: 2, offset: 0);

        // Act - Get page 2 (next 2)
        var page2 = await _repository.GetAllAsync(null, "page-test", limit: 2, offset: 2);

        // Assert
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1[0].Id.Should().NotBe(page2[0].Id);
    }

    [Fact]
    public async Task FindGeofencesAtPointAsync_PointInside_ShouldReturnGeofence()
    {
        // Arrange
        var geofence = await CreateTestGeofenceAsync("Contains Point",
            geometry: CreateSquarePolygon(-122.4, 37.8, 0.2)); // 0.1 degree radius

        // Act - Test point inside the geofence
        var result = await _repository!.FindGeofencesAtPointAsync(
            longitude: -122.4,
            latitude: 37.8,
            tenantId: geofence.TenantId);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(g => g.Id == geofence.Id);
    }

    [Fact]
    public async Task FindGeofencesAtPointAsync_PointOutside_ShouldNotReturnGeofence()
    {
        // Arrange
        var geofence = await CreateTestGeofenceAsync("Point Outside",
            geometry: CreateSquarePolygon(-122.4, 37.8, 0.2));

        // Act - Test point far outside the geofence
        var result = await _repository!.FindGeofencesAtPointAsync(
            longitude: -122.0,
            latitude: 37.5,
            tenantId: geofence.TenantId);

        // Assert
        result.Should().NotContain(g => g.Id == geofence.Id);
    }

    [Fact]
    public async Task FindGeofencesAtPointAsync_MultipleOverlapping_ShouldReturnAll()
    {
        // Arrange
        var tenantId = "multi-test";
        var geofence1 = await CreateTestGeofenceAsync("Large Fence",
            geometry: CreateSquarePolygon(-122.4, 37.8, 0.2),
            tenantId: tenantId);
        var geofence2 = await CreateTestGeofenceAsync("Small Fence",
            geometry: CreateSquarePolygon(-122.4, 37.8, 0.1),
            tenantId: tenantId);

        // Act - Point inside both geofences
        var result = await _repository!.FindGeofencesAtPointAsync(-122.4, 37.8, tenantId);

        // Assert
        result.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Should().Contain(g => g.Id == geofence1.Id);
        result.Should().Contain(g => g.Id == geofence2.Id);
    }

    [Fact]
    public async Task FindGeofencesAtPointAsync_OnlyActiveGeofences_ShouldFilter()
    {
        // Arrange
        var tenantId = "active-test";
        var activeGeofence = await CreateTestGeofenceAsync("Active",
            geometry: CreateSquarePolygon(-122.5, 37.9, 0.2),
            isActive: true,
            tenantId: tenantId);
        var inactiveGeofence = await CreateTestGeofenceAsync("Inactive",
            geometry: CreateSquarePolygon(-122.5, 37.9, 0.2),
            isActive: false,
            tenantId: tenantId);

        // Act
        var result = await _repository!.FindGeofencesAtPointAsync(-122.5, 37.9, tenantId);

        // Assert
        result.Should().Contain(g => g.Id == activeGeofence.Id);
        result.Should().NotContain(g => g.Id == inactiveGeofence.Id);
    }

    [Fact]
    public async Task FindGeofencesAtPointAsync_DifferentTenants_ShouldRespectTenancy()
    {
        // Arrange
        var tenant1Geofence = await CreateTestGeofenceAsync("Tenant 1",
            geometry: CreateSquarePolygon(-122.6, 38.0, 0.2),
            tenantId: "tenant-1");
        var tenant2Geofence = await CreateTestGeofenceAsync("Tenant 2",
            geometry: CreateSquarePolygon(-122.6, 38.0, 0.2),
            tenantId: "tenant-2");

        // Act - Query for tenant-1
        var result = await _repository!.FindGeofencesAtPointAsync(-122.6, 38.0, "tenant-1");

        // Assert
        result.Should().Contain(g => g.Id == tenant1Geofence.Id);
        result.Should().NotContain(g => g.Id == tenant2Geofence.Id);
    }

    // Helper methods

    private async Task CreateSchemaAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        // Create tables (simplified version - production uses migration file)
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS geofences (
                id UUID PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                geometry geometry(Polygon, 4326) NOT NULL,
                properties JSONB,
                enabled_event_types INT NOT NULL DEFAULT 3,
                is_active BOOLEAN NOT NULL DEFAULT true,
                tenant_id VARCHAR(100),
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                created_by VARCHAR(255),
                updated_by VARCHAR(255)
            );

            CREATE INDEX IF NOT EXISTS idx_geofences_geometry ON geofences USING GIST(geometry);
            CREATE INDEX IF NOT EXISTS idx_geofences_tenant ON geofences(tenant_id);
            CREATE INDEX IF NOT EXISTS idx_geofences_active ON geofences(is_active);
        ");
    }

    private async Task<Geofence> CreateTestGeofenceAsync(
        string name,
        Polygon? geometry = null,
        bool isActive = true,
        string? tenantId = null)
    {
        var geofence = new Geofence
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Test geofence: {name}",
            Geometry = geometry ?? CreateSquarePolygon(-122.4, 37.8, 0.1),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = isActive,
            TenantId = tenantId,
            Properties = new Dictionary<string, object> { ["test"] = name }
        };

        return await _repository!.CreateAsync(geofence);
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
