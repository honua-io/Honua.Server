// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// Repository for entity geofence state tracking
/// </summary>
public interface IEntityStateRepository
{
    /// <summary>
    /// Get current state of an entity relative to a geofence
    /// </summary>
    Task<EntityGeofenceState?> GetStateAsync(
        string entityId,
        Guid geofenceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all geofences an entity is currently inside
    /// </summary>
    Task<List<EntityGeofenceState>> GetEntityStatesAsync(
        string entityId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update or create entity state
    /// </summary>
    Task UpsertStateAsync(
        EntityGeofenceState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete state (entity no longer being tracked)
    /// </summary>
    Task DeleteStateAsync(
        string entityId,
        Guid geofenceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup stale states (not updated recently)
    /// </summary>
    Task<int> CleanupStaleStatesAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default);
}
