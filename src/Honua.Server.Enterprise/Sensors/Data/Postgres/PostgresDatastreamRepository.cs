// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
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
/// PostgreSQL implementation for Datastream entity operations.
/// </summary>
internal sealed class PostgresDatastreamRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly string _basePath;
    private readonly GeoJsonReader _geoJsonReader;

    public PostgresDatastreamRepository(string connectionString, string basePath, ILogger logger)
    {
        _connectionString = connectionString;
        _basePath = basePath;
        _logger = logger;
        _geoJsonReader = new GeoJsonReader();
    }

    public async Task<Datastream?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                id::text,
                name,
                description,
                observation_type,
                unit_of_measurement,
                thing_id::text,
                sensor_id::text,
                observed_property_id::text,
                ST_AsGeoJSON(observed_area)::jsonb as observed_area_geojson,
                phenomenon_time_start,
                phenomenon_time_end,
                result_time_start,
                result_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE id = @Id::uuid
            """;

        var result = await conn.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (result == null)
            return null;

        var datastream = new Datastream
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            ObservationType = result.observation_type,
            UnitOfMeasurement = result.unit_of_measurement != null
                ? JsonSerializer.Deserialize<UnitOfMeasurement>(result.unit_of_measurement.ToString())
                : null,
            ThingId = result.thing_id,
            SensorId = result.sensor_id,
            ObservedPropertyId = result.observed_property_id,
            ObservedArea = result.observed_area_geojson != null ? _geoJsonReader.Read<Geometry>(result.observed_area_geojson.ToString()) : null,
            PhenomenonTimeStart = result.phenomenon_time_start,
            PhenomenonTimeEnd = result.phenomenon_time_end,
            ResultTimeStart = result.result_time_start,
            ResultTimeEnd = result.result_time_end,
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/Datastreams({result.id})"
        };

        return datastream;
    }

    public async Task<PagedResult<Datastream>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = """
            SELECT
                id::text,
                name,
                description,
                observation_type,
                unit_of_measurement,
                thing_id::text,
                sensor_id::text,
                observed_property_id::text,
                phenomenon_time_start,
                phenomenon_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            """;
        var countSql = "SELECT COUNT(*) FROM sta_datastreams";
        var parameters = new DynamicParameters();

        if (options.Filter != null)
        {
            var whereClause = PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
            sql += $" WHERE {whereClause}";
            countSql += $" WHERE {whereClause}";
        }

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

        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var results = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var datastreams = results.Select(r => new Datastream
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            ObservationType = r.observation_type,
            UnitOfMeasurement = r.unit_of_measurement != null
                ? JsonSerializer.Deserialize<UnitOfMeasurement>(r.unit_of_measurement.ToString())
                : null,
            ThingId = r.thing_id,
            SensorId = r.sensor_id,
            ObservedPropertyId = r.observed_property_id,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            Properties = r.properties,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_basePath}/Datastreams({r.id})"
        }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_basePath}/Datastreams?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Datastream>
        {
            Items = datastreams,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Datastream> CreateAsync(Datastream datastream, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO sta_datastreams (
                name, description, observation_type, unit_of_measurement,
                thing_id, sensor_id, observed_property_id, properties
            )
            VALUES (
                @Name, @Description, @ObservationType, @UnitOfMeasurement::jsonb,
                @ThingId::uuid, @SensorId::uuid, @ObservedPropertyId::uuid, @Properties::jsonb
            )
            RETURNING
                id::text, name, description, observation_type, unit_of_measurement,
                thing_id::text, sensor_id::text, observed_property_id::text,
                properties, created_at, updated_at
            """;

        var result = await conn.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                datastream.Name,
                datastream.Description,
                datastream.ObservationType,
                UnitOfMeasurement = JsonSerializer.Serialize(datastream.UnitOfMeasurement),
                datastream.ThingId,
                datastream.SensorId,
                datastream.ObservedPropertyId,
                Properties = datastream.Properties != null ? JsonSerializer.Serialize(datastream.Properties) : null
            }, cancellationToken: ct));

        var created = new Datastream
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            ObservationType = result.observation_type,
            UnitOfMeasurement = JsonSerializer.Deserialize<UnitOfMeasurement>(result.unit_of_measurement.ToString()),
            ThingId = result.thing_id,
            SensorId = result.sensor_id,
            ObservedPropertyId = result.observed_property_id,
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/Datastreams({result.id})"
        };

        _logger.LogInformation("Created Datastream {DatastreamId} for Thing {ThingId}", created.Id, created.ThingId);

        return created;
    }

    public async Task<Datastream> UpdateAsync(string id, Datastream datastream, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            UPDATE sta_datastreams
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                observation_type = COALESCE(@ObservationType, observation_type),
                unit_of_measurement = COALESCE(@UnitOfMeasurement::jsonb, unit_of_measurement),
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING
                id::text, name, description, observation_type, unit_of_measurement,
                thing_id::text, sensor_id::text, observed_property_id::text,
                properties, created_at, updated_at
            """;

        var result = await conn.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Id = id,
                datastream.Name,
                datastream.Description,
                datastream.ObservationType,
                UnitOfMeasurement = datastream.UnitOfMeasurement != null ? JsonSerializer.Serialize(datastream.UnitOfMeasurement) : null,
                Properties = datastream.Properties != null ? JsonSerializer.Serialize(datastream.Properties) : null
            }, cancellationToken: ct));

        var updated = new Datastream
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            ObservationType = result.observation_type,
            UnitOfMeasurement = JsonSerializer.Deserialize<UnitOfMeasurement>(result.unit_of_measurement.ToString()),
            ThingId = result.thing_id,
            SensorId = result.sensor_id,
            ObservedPropertyId = result.observed_property_id,
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_basePath}/Datastreams({result.id})"
        };

        _logger.LogInformation("Updated Datastream {DatastreamId}", id);

        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM sta_datastreams WHERE id = @Id::uuid";

        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Datastream {DatastreamId}", id);
    }

    public async Task<PagedResult<Datastream>> GetByThingIdAsync(string thingId, QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT
                id::text,
                name,
                description,
                observation_type,
                unit_of_measurement,
                thing_id::text,
                sensor_id::text,
                observed_property_id::text,
                phenomenon_time_start,
                phenomenon_time_end,
                result_time_start,
                result_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE thing_id = @ThingId::uuid
            ORDER BY created_at DESC
            """;

        var rows = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { ThingId = thingId }, cancellationToken: ct));

        var datastreamsWithLinks = rows.Select(row =>
        {
            var unit = row.unit_of_measurement != null
                ? JsonSerializer.Deserialize<UnitOfMeasurement>(row.unit_of_measurement.ToString())!
                : throw new InvalidOperationException("Datastream missing unit_of_measurement");

            Dictionary<string, object>? properties = null;
            if (row.properties != null)
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, object>>(row.properties.ToString());
            }

            return new Datastream
            {
                Id = row.id,
                Name = row.name,
                Description = row.description,
                ObservationType = row.observation_type,
                UnitOfMeasurement = unit,
                ThingId = row.thing_id,
                SensorId = row.sensor_id,
                ObservedPropertyId = row.observed_property_id,
                PhenomenonTimeStart = row.phenomenon_time_start,
                PhenomenonTimeEnd = row.phenomenon_time_end,
                ResultTimeStart = row.result_time_start,
                ResultTimeEnd = row.result_time_end,
                Properties = properties,
                CreatedAt = row.created_at,
                UpdatedAt = row.updated_at,
                SelfLink = $"{_basePath}/Datastreams({row.id})"
            };
        }).ToList();

        return new PagedResult<Datastream>
        {
            Items = datastreamsWithLinks
        };
    }

    public async Task<PagedResult<Datastream>> GetBySensorIdAsync(string sensorId, QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sqlBuilder = new StringBuilder("""
            SELECT
                id::text,
                name,
                description,
                observation_type,
                unit_of_measurement,
                thing_id::text,
                sensor_id::text,
                observed_property_id::text,
                phenomenon_time_start,
                phenomenon_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE sensor_id = @SensorId::uuid
            """);

        var parameters = new DynamicParameters();
        parameters.Add("SensorId", sensorId);

        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" AND " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters));
        }

        if (options.OrderBy?.Any() == true)
        {
            sqlBuilder.AppendLine(" ORDER BY " + string.Join(", ",
                options.OrderBy.Select(o => $"{o.Property} {(o.Direction == SortDirection.Ascending ? "ASC" : "DESC")}")));
        }
        else
        {
            sqlBuilder.AppendLine(" ORDER BY created_at DESC");
        }

        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;
        sqlBuilder.AppendLine(" LIMIT @Limit OFFSET @Offset");
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var results = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlBuilder.ToString(), parameters, cancellationToken: ct));

        var items = results.Select(r => new Datastream
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            ObservationType = r.observation_type,
            UnitOfMeasurement = r.unit_of_measurement != null
                ? JsonSerializer.Deserialize<UnitOfMeasurement>(r.unit_of_measurement.ToString())
                : null!,
            ThingId = r.thing_id,
            SensorId = r.sensor_id,
            ObservedPropertyId = r.observed_property_id,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_basePath}/Datastreams({r.id})"
        }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_datastreams WHERE sensor_id = @SensorId::uuid";
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, new { SensorId = sensorId }, cancellationToken: ct));
        }

        return new PagedResult<Datastream>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<Datastream>> GetByObservedPropertyIdAsync(string observedPropertyId, QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sqlBuilder = new StringBuilder("""
            SELECT
                id::text,
                name,
                description,
                observation_type,
                unit_of_measurement,
                thing_id::text,
                sensor_id::text,
                observed_property_id::text,
                phenomenon_time_start,
                phenomenon_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE observed_property_id = @ObservedPropertyId::uuid
            """);

        var parameters = new DynamicParameters();
        parameters.Add("ObservedPropertyId", observedPropertyId);

        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" AND " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters));
        }

        if (options.OrderBy?.Any() == true)
        {
            sqlBuilder.AppendLine(" ORDER BY " + string.Join(", ",
                options.OrderBy.Select(o => $"{o.Property} {(o.Direction == SortDirection.Ascending ? "ASC" : "DESC")}")));
        }
        else
        {
            sqlBuilder.AppendLine(" ORDER BY created_at DESC");
        }

        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;
        sqlBuilder.AppendLine(" LIMIT @Limit OFFSET @Offset");
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var results = await conn.QueryAsync<dynamic>(
            new CommandDefinition(sqlBuilder.ToString(), parameters, cancellationToken: ct));

        var items = results.Select(r => new Datastream
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            ObservationType = r.observation_type,
            UnitOfMeasurement = r.unit_of_measurement != null
                ? JsonSerializer.Deserialize<UnitOfMeasurement>(r.unit_of_measurement.ToString())
                : null!,
            ThingId = r.thing_id,
            SensorId = r.sensor_id,
            ObservedPropertyId = r.observed_property_id,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_basePath}/Datastreams({r.id})"
        }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_datastreams WHERE observed_property_id = @ObservedPropertyId::uuid";
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, new { ObservedPropertyId = observedPropertyId }, cancellationToken: ct));
        }

        return new PagedResult<Datastream>
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
