// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using Geometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Partial class containing Datastream entity operations.
/// Handles CRUD operations for Datastream entities which link Things to Sensors
/// and ObservedProperties, and contain Observations.
/// </summary>
public sealed partial class PostgresSensorThingsRepository
{
    // ============================================================================
    // Datastream operations
    // ============================================================================

    public async Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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

        var result = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (result == null)
            return null;

        var datastream = new Datastream
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            ObservationType = result.observation_type,
            UnitOfMeasurement = JsonSerializer.Deserialize<UnitOfMeasurement>(result.unit_of_measurement.ToString()),
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
            SelfLink = $"{_config.BasePath}/Datastreams({result.id})"
        };

        // Handle expansions
        if (expand?.Properties.Contains("Thing") == true)
        {
            datastream = datastream with { Thing = await GetThingAsync(datastream.ThingId, ct: ct) };
        }

        if (expand?.Properties.Contains("Sensor") == true)
        {
            datastream = datastream with { Sensor = await GetSensorAsync(datastream.SensorId, ct: ct) };
        }

        if (expand?.Properties.Contains("ObservedProperty") == true)
        {
            datastream = datastream with { ObservedProperty = await GetObservedPropertyAsync(datastream.ObservedPropertyId, ct: ct) };
        }

        return datastream;
    }
    }

    public async Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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
            var whereClause = TranslateFilter(options.Filter, parameters);
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

        var results = await connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        // Use deferred execution for projection to avoid unnecessary materialization
        var datastreams = results.Select(r => new Datastream
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            ObservationType = r.observation_type,
            UnitOfMeasurement = JsonSerializer.Deserialize<UnitOfMeasurement>(r.unit_of_measurement.ToString()),
            ThingId = r.thing_id,
            SensorId = r.sensor_id,
            ObservedPropertyId = r.observed_property_id,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            Properties = r.properties,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_config.BasePath}/Datastreams({r.id})"
        });

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Datastreams?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Datastream>
        {
            Items = datastreams.ToList(),
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }
    }

    public async Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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

        var result = await connection.QuerySingleAsync<dynamic>(
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
            SelfLink = $"{_config.BasePath}/Datastreams({result.id})"
        };

        _logger.LogInformation("Created Datastream {DatastreamId} for Thing {ThingId}", created.Id, created.ThingId);

        return created;
    }
    }

    public async Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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

        var result = await connection.QuerySingleAsync<dynamic>(
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
            SelfLink = $"{_config.BasePath}/Datastreams({result.id})"
        };

        _logger.LogInformation("Updated Datastream {DatastreamId}", id);

        return updated;
    }
    }

    public async Task DeleteDatastreamAsync(string id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM sta_datastreams WHERE id = @Id::uuid";

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Datastream {DatastreamId}", id);
    }
    }
}
