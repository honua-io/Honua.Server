using System.Data;
using Dapper;
using Honua.Server.Enterprise.Events.Models;
using Honua.Server.Enterprise.Data;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Events.Repositories;

/// <summary>
/// PostgreSQL implementation of geofence repository using Dapper
/// </summary>
public class PostgresGeofenceRepository : IGeofenceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresGeofenceRepository> _logger;
    private readonly WKBReader _wkbReader;
    private readonly WKBWriter _wkbWriter;

    public PostgresGeofenceRepository(
        string connectionString,
        ILogger<PostgresGeofenceRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _wkbReader = new WKBReader();
        _wkbWriter = new WKBWriter();
    }

    public async Task<Geofence> CreateAsync(Geofence geofence, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geofences (
                id, name, description, geometry, properties,
                enabled_event_types, is_active, tenant_id,
                created_by, created_at
            )
            VALUES (
                @Id, @Name, @Description, ST_GeomFromWKB(@Geometry, 4326), @Properties::jsonb,
                @EnabledEventTypes, @IsActive, @TenantId,
                @CreatedBy, @CreatedAt
            )
            RETURNING id, name, description,
                      ST_AsBinary(geometry) as geometry_wkb,
                      properties, enabled_event_types, is_active, tenant_id,
                      created_at, updated_at, created_by, updated_by";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var geometryWkb = _wkbWriter.Write(geofence.Geometry);

        var result = await connection.QuerySingleAsync<GeofenceDto>(
            sql,
            new
            {
                geofence.Id,
                geofence.Name,
                geofence.Description,
                Geometry = geometryWkb,
                Properties = geofence.Properties != null
                    ? System.Text.Json.JsonSerializer.Serialize(geofence.Properties)
                    : null,
                EnabledEventTypes = (int)geofence.EnabledEventTypes,
                geofence.IsActive,
                geofence.TenantId,
                geofence.CreatedBy,
                geofence.CreatedAt
            });

        return MapToGeofence(result);
    }

    public async Task<Geofence?> GetByIdAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                id, name, description,
                ST_AsBinary(geometry) as geometry_wkb,
                properties, enabled_event_types, is_active, tenant_id,
                created_at, updated_at, created_by, updated_by
            FROM geofences
            WHERE id = @Id";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        var result = await connection.QuerySingleOrDefaultAsync<GeofenceDto>(
            sql,
            new { Id = id, TenantId = tenantId });

        return result != null ? MapToGeofence(result) : null;
    }

    public async Task<List<Geofence>> GetAllAsync(
        bool? isActive = null,
        string? tenantId = null,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (isActive.HasValue)
        {
            conditions.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrEmpty(tenantId))
        {
            conditions.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", tenantId);
        }

        var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $@"
            SELECT
                id, name, description,
                ST_AsBinary(geometry) as geometry_wkb,
                properties, enabled_event_types, is_active, tenant_id,
                created_at, updated_at, created_by, updated_by
            FROM geofences
            {whereClause}
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<GeofenceDto>(sql, parameters);

        return results.Select(MapToGeofence).ToList();
    }

    public async Task<bool> UpdateAsync(Geofence geofence, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geofences
            SET
                name = @Name,
                description = @Description,
                geometry = ST_GeomFromWKB(@Geometry, 4326),
                properties = @Properties::jsonb,
                enabled_event_types = @EnabledEventTypes,
                is_active = @IsActive,
                updated_at = @UpdatedAt,
                updated_by = @UpdatedBy
            WHERE id = @Id
            AND (@TenantId IS NULL OR tenant_id = @TenantId)";

        using var connection = new NpgsqlConnection(_connectionString);

        var geometryWkb = _wkbWriter.Write(geofence.Geometry);

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                geofence.Id,
                geofence.Name,
                geofence.Description,
                Geometry = geometryWkb,
                Properties = geofence.Properties != null
                    ? System.Text.Json.JsonSerializer.Serialize(geofence.Properties)
                    : null,
                EnabledEventTypes = (int)geofence.EnabledEventTypes,
                geofence.IsActive,
                geofence.TenantId,
                UpdatedAt = DateTime.UtcNow,
                geofence.UpdatedBy
            });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var sql = "DELETE FROM geofences WHERE id = @Id";

        if (!string.IsNullOrEmpty(tenantId))
        {
            sql += " AND tenant_id = @TenantId";
        }

        using var connection = new NpgsqlConnection(_connectionString);

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { Id = id, TenantId = tenantId });

        return rowsAffected > 0;
    }

    public async Task<int> GetCountAsync(bool? isActive = null, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (isActive.HasValue)
        {
            conditions.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        if (!string.IsNullOrEmpty(tenantId))
        {
            conditions.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", tenantId);
        }

        var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"SELECT COUNT(*) FROM geofences {whereClause}";

        using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public async Task<List<Geofence>> FindGeofencesAtPointAsync(
        double longitude,
        double latitude,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT
                id, name, description,
                ST_AsBinary(geometry) as geometry_wkb,
                properties, enabled_event_types, is_active, tenant_id,
                created_at, updated_at, created_by, updated_by
            FROM geofences
            WHERE is_active = true
            AND (@TenantId IS NULL OR tenant_id = @TenantId)
            AND ST_Contains(geometry, ST_SetSRID(ST_MakePoint(@Longitude, @Latitude), 4326))";

        using var connection = new NpgsqlConnection(_connectionString);

        var results = await connection.QueryAsync<GeofenceDto>(
            sql,
            new { Longitude = longitude, Latitude = latitude, TenantId = tenantId });

        return results.Select(MapToGeofence).ToList();
    }

    private Geofence MapToGeofence(GeofenceDto dto)
    {
        var geometry = (Polygon)_wkbReader.Read(dto.GeometryWkb);
        geometry.SRID = 4326;

        Dictionary<string, object>? properties = null;
        if (!string.IsNullOrEmpty(dto.Properties))
        {
            properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Properties);
        }

        return new Geofence
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Geometry = geometry,
            Properties = properties,
            EnabledEventTypes = (GeofenceEventTypes)dto.EnabledEventTypes,
            IsActive = dto.IsActive,
            TenantId = dto.TenantId,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            CreatedBy = dto.CreatedBy,
            UpdatedBy = dto.UpdatedBy
        };
    }

    private class GeofenceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte[] GeometryWkb { get; set; } = null!;
        public string? Properties { get; set; }
        public int EnabledEventTypes { get; set; }
        public bool IsActive { get; set; }
        public string? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
