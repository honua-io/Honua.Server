// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Honua.Server.Enterprise.Events.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Events;

public class GeofenceManagementServiceTests
{
    private readonly Mock<IGeofenceRepository> _mockGeofenceRepo;
    private readonly GeofenceManagementService _service;
    private readonly GeometryFactory _geometryFactory;

    public GeofenceManagementServiceTests()
    {
        _mockGeofenceRepo = new Mock<IGeofenceRepository>();
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        _service = new GeofenceManagementService(
            _mockGeofenceRepo.Object,
            NullLogger<GeofenceManagementService>.Instance);
    }

    [Fact]
    public async Task CreateGeofenceAsync_ValidRequest_ShouldCreateGeofence()
    {
        // Arrange
        var request = new CreateGeofenceRequest
        {
            Name = "Downtown District",
            Description = "Main downtown area",
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.4, 37.8 },
                        new[] { -122.3, 37.8 },
                        new[] { -122.3, 37.9 },
                        new[] { -122.4, 37.9 },
                        new[] { -122.4, 37.8 }
                    }
                }
            },
            EnabledEventTypes = new[] { "Enter", "Exit" },
            Properties = new Dictionary<string, object>
            {
                ["priority"] = "high",
                ["zone_type"] = "restricted"
            },
            IsActive = true
        };

        var tenantId = "tenant-1";

        _mockGeofenceRepo.Setup(r => r.CreateAsync(
                It.IsAny<Geofence>(), It.IsAny<CancellationToken>()))
            .Returns<Geofence, CancellationToken>((geofence, _) => Task.FromResult(geofence));

        // Act
        var result = await _service.CreateGeofenceAsync(request, tenantId: tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Downtown District");
        result.Description.Should().Be("Main downtown area");
        result.IsActive.Should().BeTrue();
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Enter).Should().BeTrue();
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Exit).Should().BeTrue();
        result.Properties.Should().ContainKey("priority");
        result.Properties.Should().ContainKey("zone_type");
        result.TenantId.Should().Be(tenantId);

        // Verify repository was called
        _mockGeofenceRepo.Verify(r => r.CreateAsync(
            It.Is<Geofence>(g =>
                g.Name == "Downtown District" &&
                g.TenantId == tenantId &&
                g.IsActive == true &&
                (g.EnabledEventTypes & GeofenceEventTypes.Enter) != 0 &&
                (g.EnabledEventTypes & GeofenceEventTypes.Exit) != 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateGeofenceAsync_InvalidPolygon_ShouldThrowArgumentException()
    {
        // Arrange - Not enough coordinates for a valid polygon
        var request = new CreateGeofenceRequest
        {
            Name = "Invalid Fence",
            Geometry = new GeoJsonGeometry
            {
                Type = "Polygon",
                Coordinates = new[]
                {
                    new[]
                    {
                        new[] { -122.4, 37.8 },
                        new[] { -122.3, 37.8 }
                        // Missing closing point and not enough points
                    }
                }
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateGeofenceAsync(request, tenantId: null));
    }

    [Fact]
    public async Task CreateGeofenceAsync_DefaultEventTypes_ShouldUseEnterAndExit()
    {
        // Arrange
        var request = new CreateGeofenceRequest
        {
            Name = "Default Events Fence",
            Geometry = CreateValidGeoJsonPolygon(),
            EnabledEventTypes = null // Use default
        };

        _mockGeofenceRepo.Setup(r => r.CreateAsync(
                It.IsAny<Geofence>(), It.IsAny<CancellationToken>()))
            .Returns<Geofence, CancellationToken>((geofence, _) => Task.FromResult(geofence));

        // Act
        var result = await _service.CreateGeofenceAsync(request, tenantId: null);

        // Assert
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Enter).Should().BeTrue();
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Exit).Should().BeTrue();
        result.EnabledEventTypes.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit);
    }

    [Fact]
    public async Task UpdateGeofenceAsync_ExistingGeofence_ShouldUpdate()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var tenantId = "tenant-1";

        var existingGeofence = new Geofence
        {
            Id = geofenceId,
            Name = "Old Name",
            Geometry = CreateSquarePolygon(-122.4, 37.8, 0.1),
            EnabledEventTypes = GeofenceEventTypes.Enter,
            IsActive = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateRequest = new CreateGeofenceRequest
        {
            Name = "New Name",
            Description = "Updated description",
            Geometry = CreateValidGeoJsonPolygon(),
            EnabledEventTypes = new[] { "Enter", "Exit", "Dwell" },
            IsActive = false
        };

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGeofence);

        Geofence? updatedGeofence = null;

        _mockGeofenceRepo.Setup(r => r.UpdateAsync(
                It.IsAny<Geofence>(), It.IsAny<CancellationToken>()))
            .Callback<Geofence, CancellationToken>((geofence, _) => updatedGeofence = geofence)
            .Returns(() => Task.FromResult(true));

        // Act
        var result = await _service.UpdateGeofenceAsync(geofenceId, updateRequest, tenantId: tenantId);

        // Assert
        updatedGeofence.Should().NotBeNull();
        updatedGeofence!.Name.Should().Be("New Name");
        updatedGeofence.Description.Should().Be("Updated description");
        updatedGeofence.IsActive.Should().BeFalse();
        updatedGeofence.EnabledEventTypes.Should().NotBe(GeofenceEventTypes.None);

        // Verify update was called
        _mockGeofenceRepo.Verify(r => r.UpdateAsync(
            It.Is<Geofence>(g =>
                g.Id == geofenceId &&
                g.Name == "New Name" &&
                g.IsActive == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateGeofenceAsync_NonExistentGeofence_ShouldReturnFalse()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var updateRequest = new CreateGeofenceRequest
        {
            Name = "Updated Name",
            Geometry = CreateValidGeoJsonPolygon()
        };

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Geofence?)null);

        // Act
        var result = await _service.UpdateGeofenceAsync(geofenceId, updateRequest);

        // Assert
        result.Should().BeFalse();
        _mockGeofenceRepo.Verify(r => r.UpdateAsync(
            It.IsAny<Geofence>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGeofenceAsync_ExistingGeofence_ShouldDelete()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var tenantId = "tenant-1";

        _mockGeofenceRepo.Setup(r => r.DeleteAsync(geofenceId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteGeofenceAsync(geofenceId, tenantId);

        // Assert
        result.Should().BeTrue();
        _mockGeofenceRepo.Verify(r => r.DeleteAsync(
            geofenceId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGeofenceAsync_ExistingGeofence_ShouldReturnResponse()
    {
        // Arrange
        var geofenceId = Guid.NewGuid();
        var tenantId = "tenant-1";

        var geofence = new Geofence
        {
            Id = geofenceId,
            Name = "Test Fence",
            Description = "Test Description",
            Geometry = CreateSquarePolygon(-122.4, 37.8, 0.1),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            TenantId = tenantId,
            Properties = new Dictionary<string, object> { ["test"] = "value" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockGeofenceRepo.Setup(r => r.GetByIdAsync(geofenceId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofence);

        // Act
        var result = await _service.GetGeofenceAsync(geofenceId, tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(geofenceId);
        result.Name.Should().Be("Test Fence");
       result.Description.Should().Be("Test Description");
       result.Geometry.Should().NotBeNull();
        result.Geometry.GeometryType.Should().Be("Polygon");
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Enter).Should().BeTrue();
        result.EnabledEventTypes.HasFlag(GeofenceEventTypes.Exit).Should().BeTrue();
    }

    [Fact]
    public async Task ListGeofencesAsync_WithPagination_ShouldReturnList()
    {
        // Arrange
        var tenantId = "tenant-1";
        var geofences = new List<Geofence>
        {
            CreateGeofence(Guid.NewGuid(), "Fence 1"),
            CreateGeofence(Guid.NewGuid(), "Fence 2"),
            CreateGeofence(Guid.NewGuid(), "Fence 3")
        };

        _mockGeofenceRepo.Setup(r => r.GetAllAsync(
                true, tenantId, 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofences);

        _mockGeofenceRepo.Setup(r => r.GetCountAsync(
                true, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geofences.Count);

        // Act
        var result = await _service.ListGeofencesAsync(
            isActive: true, tenantId: tenantId, limit: 10, offset: 0);

        // Assert
        result.Should().NotBeNull();
        result.Geofences.Should().HaveCount(3);
        result.Geofences[0].Name.Should().Be("Fence 1");
        result.Geofences[1].Name.Should().Be("Fence 2");
        result.Geofences[2].Name.Should().Be("Fence 3");
        result.TotalCount.Should().Be(3);
        result.Limit.Should().Be(10);
        result.Offset.Should().Be(0);
    }

    [Fact]
    public void ParseEventTypes_ValidStrings_ShouldReturnCorrectFlags()
    {
        // Arrange & Act
        var result1 = ParseEventTypes(new[] { "Enter" });
        var result2 = ParseEventTypes(new[] { "Enter", "Exit" });
        var result3 = ParseEventTypes(new[] { "Enter", "Exit", "Dwell" });
        var result4 = ParseEventTypes(new[] { "Enter", "Exit", "Dwell", "Approach" });

        // Assert
        result1.Should().Be(GeofenceEventTypes.Enter);
        result2.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit);
        result3.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit | GeofenceEventTypes.Dwell);
        result4.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit | GeofenceEventTypes.Dwell | GeofenceEventTypes.Approach);
    }

    [Fact]
    public void ParseEventTypes_CaseInsensitive_ShouldWork()
    {
        // Arrange & Act
        var result = ParseEventTypes(new[] { "enter", "EXIT", "DwElL" });

        // Assert
        result.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit | GeofenceEventTypes.Dwell);
    }

    [Fact]
    public void ParseEventTypes_InvalidType_ShouldIgnore()
    {
        // Arrange & Act
        var result = ParseEventTypes(new[] { "Enter", "InvalidType", "Exit" });

        // Assert
        result.Should().Be(GeofenceEventTypes.Enter | GeofenceEventTypes.Exit);
    }

    // Helper methods

    private Geofence CreateGeofence(Guid id, string name)
    {
        return new Geofence
        {
            Id = id,
            Name = name,
            Geometry = CreateSquarePolygon(-122.4, 37.8, 0.1),
            EnabledEventTypes = GeofenceEventTypes.Enter | GeofenceEventTypes.Exit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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

    private GeoJsonGeometry CreateValidGeoJsonPolygon()
    {
        return new GeoJsonGeometry
        {
            Type = "Polygon",
            Coordinates = new[]
            {
                new[]
                {
                    new[] { -122.4, 37.8 },
                    new[] { -122.3, 37.8 },
                    new[] { -122.3, 37.9 },
                    new[] { -122.4, 37.9 },
                    new[] { -122.4, 37.8 }
                }
            }
        };
    }

    // Helper to parse event types (mirrors service logic)
    private GeofenceEventTypes ParseEventTypes(string[]? eventTypes)
    {
        if (eventTypes == null || eventTypes.Length == 0)
        {
            return GeofenceEventTypes.Enter | GeofenceEventTypes.Exit;
        }

        var result = GeofenceEventTypes.None;
        foreach (var type in eventTypes)
        {
            if (Enum.TryParse<GeofenceEventTypes>(type, ignoreCase: true, out var parsed))
            {
                result |= parsed;
            }
        }

        return result == GeofenceEventTypes.None
            ? GeofenceEventTypes.Enter | GeofenceEventTypes.Exit
            : result;
    }
}
