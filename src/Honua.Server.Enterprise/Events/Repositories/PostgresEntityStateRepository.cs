using Dapper;
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// PostgreSQL implementation of entity state repository
/// </summary>
public class PostgresEntityStateRepository : IEntityStateRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresEntityStateRepository> _logger;

    public PostgresEntityStateRepository(
        string connectionString,
        ILogger<PostgresEntityStateRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EntityGeofenceState?> GetStateAsync(
        string entityId,
        Guid geofenceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT entity_id, geofence_id, is_inside, entered_at, last_updated, tenant_id
            FROM entity_geofence_state
            WHERE entity_id = @EntityId
            AND geofence_id = @GeofenceId";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        return await connection.QuerySingleOrDefaultAsync<EntityGeofenceState>(
            sql,
            new { EntityId = entityId, GeofenceId = geofenceId, TenantId = tenantId });
    }

    public async Task<List<EntityGeofenceState>> GetEntityStatesAsync(
        string entityId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT entity_id, geofence_id, is_inside, entered_at, last_updated, tenant_id
            FROM entity_geofence_state
            WHERE entity_id = @EntityId
            AND is_inside = true";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<EntityGeofenceState>(
            sql,
            new { EntityId = entityId, TenantId = tenantId });

        return results.ToList();
    }

    public async Task UpsertStateAsync(
        EntityGeofenceState state,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO entity_geofence_state (
                entity_id, geofence_id, is_inside, entered_at, last_updated, tenant_id
            )
            VALUES (
                @EntityId, @GeofenceId, @IsInside, @EnteredAt, @LastUpdated, @TenantId
            )
            ON CONFLICT (entity_id, geofence_id)
            DO UPDATE SET
                is_inside = EXCLUDED.is_inside,
                entered_at = EXCLUDED.entered_at,
                last_updated = EXCLUDED.last_updated";

        using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(sql, state);
    }

    public async Task DeleteStateAsync(
        string entityId,
        Guid geofenceId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            DELETE FROM entity_geofence_state
            WHERE entity_id = @EntityId
            AND geofence_id = @GeofenceId";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            sql,
            new { EntityId = entityId, GeofenceId = geofenceId, TenantId = tenantId });
    }

    public async Task<int> CleanupStaleStatesAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            DELETE FROM entity_geofence_state
            WHERE last_updated < @CutoffTime";

        using var connection = new NpgsqlConnection(_connectionString);

        var cutoffTime = DateTime.UtcNow - maxAge;

        var rowsDeleted = await connection.ExecuteAsync(
            sql,
            new { CutoffTime = cutoffTime });

        _logger.LogInformation(
            "Cleaned up {Count} stale entity states older than {MaxAge}",
            rowsDeleted,
            maxAge);

        return rowsDeleted;
    }
}
