// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Geometry = NetTopologySuite.Geometries.Geometry;
using StaLocation = Honua.Server.Enterprise.Sensors.Models.Location;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Partial class containing Location entity operations.
/// Handles CRUD operations for Location entities with PostGIS spatial support.
/// </summary>
public sealed partial class PostgresSensorThingsRepository
{
    // ============================================================================
    // Location operations
    // ============================================================================

    public async Task<StaLocation?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(location)::jsonb as location_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_locations
            WHERE id = @Id::uuid
            """;

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (result == null)
            return null;

        var location = new StaLocation
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Geometry = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({result.id})"
        };

        // Handle expansions
        if (expand?.Properties.Contains("Things") == true)
        {
            // Query Things associated with this Location
            const string thingsSql = """
                SELECT t.id::text, t.name, t.description, t.properties, t.created_at, t.updated_at
                FROM sta_things t
                JOIN sta_thing_location tl ON t.id = tl.thing_id
                WHERE tl.location_id = @LocationId::uuid
                """;

            var things = await _connection.QueryAsync<Thing>(
                new CommandDefinition(thingsSql, new { LocationId = id }, cancellationToken: ct));

            // Use deferred execution to avoid materializing collection until accessed
            location = location with { Things = things.Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" }) };
        }

        return location;
    }

    public async Task<PagedResult<StaLocation>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(location)::jsonb as location_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_locations
            """;
        var countSql = "SELECT COUNT(*) FROM sta_locations";
        var parameters = new DynamicParameters();

        // Apply filters
        if (options.Filter != null)
        {
            var whereClause = TranslateFilter(options.Filter, parameters);
            sql += $" WHERE {whereClause}";
            countSql += $" WHERE {whereClause}";
        }

        // Apply ordering
        if (options.OrderBy?.Count > 0)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            sql += $" ORDER BY {string.Join(", ", orderClauses)}";
        }
        else
        {
            sql += " ORDER BY created_at DESC";
        }

        // Apply pagination
        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var results = await _connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        // Use deferred execution for projection to avoid unnecessary materialization
        var locations = results.Select(r => new StaLocation
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            EncodingType = r.encoding_type,
            Geometry = _geoJsonReader.Read<Geometry>(r.location_geojson.ToString()),
            Properties = r.properties,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({r.id})"
        });

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Locations?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<StaLocation>
        {
            Items = locations,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<StaLocation> CreateLocationAsync(StaLocation location, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_locations (name, description, encoding_type, location, properties)
            VALUES (@Name, @Description, @EncodingType, ST_GeomFromGeoJSON(@Location), @Properties::jsonb)
            RETURNING id::text, name, description, encoding_type, ST_AsGeoJSON(location)::jsonb as location_geojson, properties, created_at, updated_at
            """;

        var result = await _connection.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                location.Name,
                location.Description,
                location.EncodingType,
                Location = _geoJsonWriter.Write(location.Geometry),
                Properties = location.Properties != null ? JsonSerializer.Serialize(location.Properties) : null
            }, cancellationToken: ct));

        var created = new StaLocation
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Geometry = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({result.id})"
        };

        _logger.LogInformation("Created Location {LocationId} with name '{Name}'", created.Id, created.Name);

        return created;
    }

    public async Task<StaLocation> UpdateLocationAsync(string id, StaLocation location, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE sta_locations
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                encoding_type = COALESCE(@EncodingType, encoding_type),
                location = COALESCE(ST_GeomFromGeoJSON(@Location), location),
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING id::text, name, description, encoding_type, ST_AsGeoJSON(location)::jsonb as location_geojson, properties, created_at, updated_at
            """;

        var result = await _connection.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Id = id,
                location.Name,
                location.Description,
                location.EncodingType,
                Location = location.Geometry != null ? _geoJsonWriter.Write(location.Geometry) : null,
                Properties = location.Properties != null ? JsonSerializer.Serialize(location.Properties) : null
            }, cancellationToken: ct));

        var updated = new StaLocation
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Geometry = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({result.id})"
        };

        _logger.LogInformation("Updated Location {LocationId}", id);

        return updated;
    }

    public async Task DeleteLocationAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_locations WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Location {LocationId}", id);
    }
}
