using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL repository for Location entities.
/// Handles CRUD operations with PostGIS spatial support.
/// </summary>
internal sealed class PostgresLocationRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly GeoJsonReader _geoJsonReader;

    public PostgresLocationRepository(string connectionString, ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _geoJsonReader = new GeoJsonReader();
    }

    public async Task<Location?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, name, description, encoding_type,
                   ST_AsGeoJSON(location)::text as location,
                   properties, created_at, updated_at
            FROM locations
            WHERE id = @Id";

        var loc = await conn.QuerySingleOrDefaultAsync<LocationDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        return loc != null ? MapToModel(loc) : null;
    }

    public async Task<PagedResult<Location>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        var whereClause = string.Empty;

        if (options.Filter != null)
        {
            whereClause = "WHERE " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
        }

        var orderBy = "ORDER BY created_at DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        var countSql = $"SELECT COUNT(*) FROM locations {whereClause}";
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        parameters.Add("Skip", options.Skip);
        parameters.Add("Top", options.Top);

        var dataSql = $@"
            SELECT id, name, description, encoding_type,
                   ST_AsGeoJSON(location)::text as location,
                   properties, created_at, updated_at
            FROM locations
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var locations = await conn.QueryAsync<LocationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = locations.Select(MapToModel).ToList();

        return new PagedResult<Location>(items, total, options.Skip, options.Top);
    }

    public async Task<PagedResult<Location>> GetByThingAsync(
        string thingId,
        QueryOptions options,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("ThingId", thingId);

        var whereClause = @"
            WHERE l.id IN (
                SELECT location_id FROM thing_locations WHERE thing_id = @ThingId
            )";

        if (options.Filter != null)
        {
            var filterClause = PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
            whereClause += $" AND ({filterClause})";
        }

        var orderBy = "ORDER BY l.created_at DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"l.{o.Property} {(o.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        var countSql = $"SELECT COUNT(*) FROM locations l {whereClause}";
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        parameters.Add("Skip", options.Skip);
        parameters.Add("Top", options.Top);

        var dataSql = $@"
            SELECT l.id, l.name, l.description, l.encoding_type,
                   ST_AsGeoJSON(l.location)::text as location,
                   l.properties, l.created_at, l.updated_at
            FROM locations l
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var locations = await conn.QueryAsync<LocationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = locations.Select(MapToModel).ToList();

        return new PagedResult<Location>(items, total, options.Skip, options.Top);
    }

    public async Task<Location> CreateAsync(Location location, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        location.Id = Guid.NewGuid().ToString();
        location.CreatedAt = DateTimeOffset.UtcNow;
        location.UpdatedAt = location.CreatedAt;

        const string sql = @"
            INSERT INTO locations (
                id, name, description, encoding_type, location, properties,
                created_at, updated_at
            )
            VALUES (
                @Id, @Name, @Description, @EncodingType,
                ST_GeomFromGeoJSON(@Location), @Properties::jsonb,
                @CreatedAt, @UpdatedAt
            )";

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            location.Id,
            location.Name,
            location.Description,
            location.EncodingType,
            Location = SerializeGeometry(location.Location),
            Properties = location.Properties != null
                ? JsonSerializer.Serialize(location.Properties)
                : null,
            location.CreatedAt,
            location.UpdatedAt
        }, cancellationToken: ct));

        _logger.LogInformation("Created Location {LocationId}", location.Id);

        return location;
    }

    public async Task<Location> UpdateAsync(string id, Location location, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        location.Id = id;
        location.UpdatedAt = DateTimeOffset.UtcNow;

        const string sql = @"
            UPDATE locations
            SET name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                encoding_type = COALESCE(@EncodingType, encoding_type),
                location = COALESCE(ST_GeomFromGeoJSON(@Location), location),
                properties = COALESCE(@Properties::jsonb, properties),
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, name, description, encoding_type,
                      ST_AsGeoJSON(location)::text as location,
                      properties, created_at, updated_at";

        var updated = await conn.QuerySingleAsync<LocationDto>(new CommandDefinition(sql, new
        {
            Id = id,
            location.Name,
            location.Description,
            location.EncodingType,
            Location = location.Location != null ? SerializeGeometry(location.Location) : null,
            Properties = location.Properties != null
                ? JsonSerializer.Serialize(location.Properties)
                : null,
            location.UpdatedAt
        }, cancellationToken: ct));

        _logger.LogInformation("Updated Location {LocationId}", id);

        return MapToModel(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM locations WHERE id = @Id";

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (affected == 0)
        {
            throw new InvalidOperationException($"Location {id} not found");
        }

        _logger.LogInformation("Deleted Location {LocationId}", id);
    }

    private Location MapToModel(LocationDto dto)
    {
        return new Location
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            EncodingType = dto.EncodingType,
            Location = DeserializeGeometry(dto.Location),
            Properties = PostgresQueryHelper.ParseProperties(dto.Properties),
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    private string? SerializeGeometry(Geometry? geometry)
    {
        if (geometry == null)
            return null;

        var writer = new GeoJsonWriter();
        return writer.Write(geometry);
    }

    private Geometry? DeserializeGeometry(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
            return null;

        try
        {
            return _geoJsonReader.Read<Geometry>(geoJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GeoJSON: {GeoJson}", geoJson);
            return null;
        }
    }

    private sealed class LocationDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string EncodingType { get; init; } = string.Empty;
        public string? Location { get; init; }
        public string? Properties { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
