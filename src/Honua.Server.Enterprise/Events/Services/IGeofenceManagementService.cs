// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Dto;
using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Services;

/// <summary>
/// Service for managing geofences (CRUD operations)
/// </summary>
public interface IGeofenceManagementService
{
    /// <summary>
    /// Create a new geofence
    /// </summary>
    Task<Geofence> CreateGeofenceAsync(
        CreateGeofenceRequest request,
        string? createdBy = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get geofence by ID
    /// </summary>
    Task<Geofence?> GetGeofenceAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List geofences
    /// </summary>
    Task<GeofenceListResult> ListGeofencesAsync(
        bool? isActive = null,
        string? tenantId = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update geofence
    /// </summary>
    Task<bool> UpdateGeofenceAsync(
        Guid id,
        CreateGeofenceRequest request,
        string? updatedBy = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete geofence
    /// </summary>
    Task<bool> DeleteGeofenceAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of listing geofences
/// </summary>
public class GeofenceListResult
{
    public List<Geofence> Geofences { get; set; } = new();
    public int TotalCount { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
