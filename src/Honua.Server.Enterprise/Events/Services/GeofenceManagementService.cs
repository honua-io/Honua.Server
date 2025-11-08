using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Events.Repositories;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Events.Services;

/// <summary>
/// Service for managing geofences
/// </summary>
public class GeofenceManagementService : IGeofenceManagementService
{
    private readonly IGeofenceRepository _repository;
    private readonly ILogger<GeofenceManagementService> _logger;
    private readonly GeometryFactory _geometryFactory;

    public GeofenceManagementService(
        IGeofenceRepository repository,
        ILogger<GeofenceManagementService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
    }

    public async Task<Geofence> CreateGeofenceAsync(
        CreateGeofenceRequest request,
        string? createdBy = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Convert GeoJSON to NetTopologySuite Polygon
        var polygon = ConvertToPolygon(request.Geometry);

        // Parse enabled event types
        var enabledEventTypes = ParseEventTypes(request.EnabledEventTypes);

        var geofence = new Geofence
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Geometry = polygon,
            Properties = request.Properties,
            EnabledEventTypes = enabledEventTypes,
            IsActive = request.IsActive,
            TenantId = tenantId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(geofence, cancellationToken);

        _logger.LogInformation(
            "Created geofence {GeofenceId} '{Name}' with {PointCount} points",
            created.Id,
            created.Name,
            polygon.NumPoints);

        return created;
    }

    public async Task<Geofence?> GetGeofenceAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<GeofenceListResult> ListGeofencesAsync(
        bool? isActive = null,
        string? tenantId = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var geofences = await _repository.GetAllAsync(
            isActive,
            tenantId,
            limit,
            offset,
            cancellationToken);

        var totalCount = await _repository.GetCountAsync(
            isActive,
            tenantId,
            cancellationToken);

        return new GeofenceListResult
        {
            Geofences = geofences,
            TotalCount = totalCount,
            Limit = limit,
            Offset = offset
        };
    }

    public async Task<bool> UpdateGeofenceAsync(
        Guid id,
        CreateGeofenceRequest request,
        string? updatedBy = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _repository.GetByIdAsync(id, tenantId, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        // Update fields
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Geometry = ConvertToPolygon(request.Geometry);
        existing.Properties = request.Properties;
        existing.EnabledEventTypes = ParseEventTypes(request.EnabledEventTypes);
        existing.IsActive = request.IsActive;
        existing.UpdatedBy = updatedBy;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);

        if (updated)
        {
            _logger.LogInformation(
                "Updated geofence {GeofenceId} '{Name}'",
                id,
                request.Name);
        }

        return updated;
    }

    public async Task<bool> DeleteGeofenceAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(id, tenantId, cancellationToken);

        if (deleted)
        {
            _logger.LogInformation(
                "Deleted geofence {GeofenceId}",
                id);
        }

        return deleted;
    }

    private Polygon ConvertToPolygon(GeoJsonGeometry geoJson)
    {
        if (geoJson.Type != "Polygon")
        {
            throw new ArgumentException($"Expected Polygon geometry, got {geoJson.Type}");
        }

        // GeoJSON coordinates: [[[lon, lat], [lon, lat], ...]]
        // First array is exterior ring, subsequent arrays are holes (not supported in MVP)
        if (geoJson.Coordinates.Length == 0)
        {
            throw new ArgumentException("Polygon must have at least one ring");
        }

        var exteriorRing = geoJson.Coordinates[0];
        var coordinates = exteriorRing.Select(coord =>
            new Coordinate(coord[0], coord[1]) // lon, lat
        ).ToArray();

        var linearRing = _geometryFactory.CreateLinearRing(coordinates);
        return _geometryFactory.CreatePolygon(linearRing);
    }

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
