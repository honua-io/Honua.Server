// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// Repository for geofence data access
/// </summary>
public interface IGeofenceRepository
{
    /// <summary>
    /// Create a new geofence
    /// </summary>
    Task<Geofence> CreateAsync(Geofence geofence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get geofence by ID
    /// </summary>
    Task<Geofence?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get multiple geofences by IDs (batch query to avoid N+1 problem)
    /// </summary>
    Task<List<Geofence>> GetByIdsAsync(IEnumerable<Guid> ids, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all geofences (with optional filters)
    /// </summary>
    Task<List<Geofence>> GetAllAsync(
        bool? isActive = null,
        string? tenantId = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update geofence
    /// </summary>
    Task<bool> UpdateAsync(Geofence geofence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete geofence
    /// </summary>
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of geofences (for pagination)
    /// </summary>
    Task<int> GetCountAsync(bool? isActive = null, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all active geofences that contain a point
    /// </summary>
    Task<List<Geofence>> FindGeofencesAtPointAsync(
        double longitude,
        double latitude,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
