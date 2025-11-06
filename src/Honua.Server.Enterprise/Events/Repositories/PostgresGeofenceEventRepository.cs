using Dapper;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Data;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// PostgreSQL implementation of geofence event repository
/// </summary>
public class PostgresGeofenceEventRepository : IGeofenceEventRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresGeofenceEventRepository> _logger;
    private readonly WKBReader _wkbReader;
    private readonly WKBWriter _wkbWriter;

    public PostgresGeofenceEventRepository(
        string connectionString,
        ILogger<PostgresGeofenceEventRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _wkbReader = new WKBReader();
        _wkbWriter = new WKBWriter();
    }

    public async Task<GeofenceEvent> CreateAsync(
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geofence_events (
                id, event_type, event_time, geofence_id, geofence_name,
                entity_id, entity_type, location, boundary_point,
                properties, dwell_time_seconds, sensorthings_observation_id,
                tenant_id, processed_at
            )
            VALUES (
                @Id, @EventType, @EventTime, @GeofenceId, @GeofenceName,
                @EntityId, @EntityType,
                ST_GeomFromWKB(@Location, 4326),
                CASE WHEN @BoundaryPoint IS NOT NULL THEN ST_GeomFromWKB(@BoundaryPoint, 4326) ELSE NULL END,
                @Properties::jsonb, @DwellTimeSeconds, @SensorThingsObservationId,
                @TenantId, @ProcessedAt
            )
            RETURNING id";

        using var connection = new NpgsqlConnection(_connectionString);

        var locationWkb = _wkbWriter.Write(geofenceEvent.Location);
        byte[]? boundaryWkb = geofenceEvent.BoundaryPoint != null
            ? _wkbWriter.Write(geofenceEvent.BoundaryPoint)
            : null;

        await connection.ExecuteAsync(
            sql,
            new
            {
                geofenceEvent.Id,
                EventType = geofenceEvent.EventType.ToString().ToLowerInvariant(),
                geofenceEvent.EventTime,
                geofenceEvent.GeofenceId,
                geofenceEvent.GeofenceName,
                geofenceEvent.EntityId,
                geofenceEvent.EntityType,
                Location = locationWkb,
                BoundaryPoint = boundaryWkb,
                Properties = geofenceEvent.Properties != null
                    ? System.Text.Json.JsonSerializer.Serialize(geofenceEvent.Properties)
                    : null,
                geofenceEvent.DwellTimeSeconds,
                geofenceEvent.SensorThingsObservationId,
                geofenceEvent.TenantId,
                geofenceEvent.ProcessedAt
            });

        return geofenceEvent;
    }

    public async Task<List<GeofenceEvent>> CreateBatchAsync(
        List<GeofenceEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (!events.Any())
        {
            return events;
        }

        const string sql = @"
            INSERT INTO geofence_events (
                id, event_type, event_time, geofence_id, geofence_name,
                entity_id, entity_type, location, boundary_point,
                properties, dwell_time_seconds, sensorthings_observation_id,
                tenant_id, processed_at
            )
            VALUES (
                @Id, @EventType, @EventTime, @GeofenceId, @GeofenceName,
                @EntityId, @EntityType,
                ST_GeomFromWKB(@Location, 4326),
                CASE WHEN @BoundaryPoint IS NOT NULL THEN ST_GeomFromWKB(@BoundaryPoint, 4326) ELSE NULL END,
                @Properties::jsonb, @DwellTimeSeconds, @SensorThingsObservationId,
                @TenantId, @ProcessedAt
            )";

        using var connection = new NpgsqlConnection(_connectionString);

        var parameters = events.Select(evt => new
        {
            evt.Id,
            EventType = evt.EventType.ToString().ToLowerInvariant(),
            evt.EventTime,
            evt.GeofenceId,
            evt.GeofenceName,
            evt.EntityId,
            evt.EntityType,
            Location = _wkbWriter.Write(evt.Location),
            BoundaryPoint = evt.BoundaryPoint != null ? _wkbWriter.Write(evt.BoundaryPoint) : null,
            Properties = evt.Properties != null
                ? System.Text.Json.JsonSerializer.Serialize(evt.Properties)
                : null,
            evt.DwellTimeSeconds,
            evt.SensorThingsObservationId,
            evt.TenantId,
            evt.ProcessedAt
        }).ToList();

        await connection.ExecuteAsync(sql, parameters);

        _logger.LogInformation("Created {Count} geofence events in batch", events.Count);

        return events;
    }

    public async Task<GeofenceEvent?> GetByIdAsync(
        Guid id,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                id, event_type, event_time, geofence_id, geofence_name,
                entity_id, entity_type,
                ST_AsBinary(location) as location_wkb,
                ST_AsBinary(boundary_point) as boundary_point_wkb,
                properties, dwell_time_seconds, sensorthings_observation_id,
                tenant_id, processed_at
            FROM geofence_events
            WHERE id = @Id";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        var dto = await connection.QuerySingleOrDefaultAsync<GeofenceEventDto>(
            sql,
            new { Id = id, TenantId = tenantId });

        return dto != null ? MapToGeofenceEvent(dto) : null;
    }

    public async Task<List<GeofenceEvent>> QueryEventsAsync(
        GeofenceEventQuery query,
        CancellationToken cancellationToken = default)
    {
        var conditions = BuildWhereClause(query, out var parameters);

        var sql = $@"
            SELECT
                id, event_type, event_time, geofence_id, geofence_name,
                entity_id, entity_type,
                ST_AsBinary(location) as location_wkb,
                ST_AsBinary(boundary_point) as boundary_point_wkb,
                properties, dwell_time_seconds, sensorthings_observation_id,
                tenant_id, processed_at
            FROM geofence_events
            {conditions}
            ORDER BY event_time DESC
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", query.Limit);
        parameters.Add("Offset", query.Offset);

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<GeofenceEventDto>(sql, parameters);

        return results.Select(MapToGeofenceEvent).ToList();
    }

    public async Task<int> GetCountAsync(
        GeofenceEventQuery query,
        CancellationToken cancellationToken = default)
    {
        var conditions = BuildWhereClause(query, out var parameters);

        var sql = $"SELECT COUNT(*) FROM geofence_events {conditions}";

        using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    private string BuildWhereClause(GeofenceEventQuery query, out DynamicParameters parameters)
    {
        var conditions = new List<string>();
        parameters = new DynamicParameters();

        if (query.GeofenceId.HasValue)
        {
            conditions.Add("geofence_id = @GeofenceId");
            parameters.Add("GeofenceId", query.GeofenceId.Value);
        }

        if (!string.IsNullOrEmpty(query.EntityId))
        {
            conditions.Add("entity_id = @EntityId");
            parameters.Add("EntityId", query.EntityId);
        }

        if (query.EventType.HasValue)
        {
            conditions.Add("event_type = @EventType");
            parameters.Add("EventType", query.EventType.Value.ToString().ToLowerInvariant());
        }

        if (query.StartTime.HasValue)
        {
            conditions.Add("event_time >= @StartTime");
            parameters.Add("StartTime", query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            conditions.Add("event_time <= @EndTime");
            parameters.Add("EndTime", query.EndTime.Value);
        }

        if (!string.IsNullOrEmpty(query.TenantId))
        {
            conditions.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", query.TenantId);
        }

        return conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
    }

    private GeofenceEvent MapToGeofenceEvent(GeofenceEventDto dto)
    {
        var location = (Point)_wkbReader.Read(dto.LocationWkb);
        Point? boundaryPoint = dto.BoundaryPointWkb != null
            ? (Point)_wkbReader.Read(dto.BoundaryPointWkb)
            : null;

        Dictionary<string, object>? properties = null;
        if (!string.IsNullOrEmpty(dto.Properties))
        {
            properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Properties);
        }

        return new GeofenceEvent
        {
            Id = dto.Id,
            EventType = Enum.Parse<GeofenceEventType>(dto.EventType, ignoreCase: true),
            EventTime = dto.EventTime,
            GeofenceId = dto.GeofenceId,
            GeofenceName = dto.GeofenceName,
            EntityId = dto.EntityId,
            EntityType = dto.EntityType,
            Location = location,
            BoundaryPoint = boundaryPoint,
            Properties = properties,
            DwellTimeSeconds = dto.DwellTimeSeconds,
            SensorThingsObservationId = dto.SensorThingsObservationId,
            TenantId = dto.TenantId,
            ProcessedAt = dto.ProcessedAt
        };
    }

    private class GeofenceEventDto
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime EventTime { get; set; }
        public Guid GeofenceId { get; set; }
        public string GeofenceName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public byte[] LocationWkb { get; set; } = null!;
        public byte[]? BoundaryPointWkb { get; set; }
        public string? Properties { get; set; }
        public int? DwellTimeSeconds { get; set; }
        public Guid? SensorThingsObservationId { get; set; }
        public string? TenantId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
