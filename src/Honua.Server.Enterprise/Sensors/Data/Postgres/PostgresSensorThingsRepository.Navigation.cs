using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Geometry = NetTopologySuite.Geometries.Geometry;
using StaLocation = Honua.Server.Enterprise.Sensors.Models.Location;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Partial class containing navigation property operations.
/// Handles queries for related entities through navigation properties
/// (e.g., getting all Datastreams for a Thing, all Observations for a Datastream, etc.).
/// </summary>
public sealed partial class PostgresSensorThingsRepository
{
    // ============================================================================
    // Navigation property queries
    // ============================================================================

    public async Task<PagedResult<StaLocation>> GetThingLocationsAsync(
        string thingId,
        QueryOptions options,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                l.id::text,
                l.name,
                l.description,
                l.encoding_type,
                ST_AsGeoJSON(l.location)::jsonb as location_geojson,
                l.properties,
                l.created_at,
                l.updated_at,
                l.self_link
            FROM sta_locations l
            JOIN sta_thing_location tl ON l.id = tl.location_id
            WHERE tl.thing_id = @ThingId::uuid
            """;

        var locations = await _connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { ThingId = thingId }, cancellationToken: ct));

        var result = locations.Select(l => new StaLocation
        {
            Id = l.id,
            Name = l.name,
            Description = l.description,
            EncodingType = l.encoding_type,
            Geometry = _geoJsonReader.Read<Geometry>(l.location_geojson.ToString()),
            Properties = l.properties,
            CreatedAt = l.created_at,
            UpdatedAt = l.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({l.id})"
        }).ToList();

        return new PagedResult<StaLocation>
        {
            Items = result
        };
    }

    public async Task<PagedResult<Datastream>> GetThingDatastreamsAsync(
        string thingId,
        QueryOptions options,
        CancellationToken ct = default)
    {
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

        var rows = await _connection.QueryAsync<dynamic>(
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
                SelfLink = $"{_config.BasePath}/Datastreams({row.id})"
            };
        }).ToList();

        return new PagedResult<Datastream>
        {
            Items = datastreamsWithLinks
        };
    }

    public async Task<PagedResult<Observation>> GetDatastreamObservationsAsync(
        string datastreamId,
        QueryOptions options,
        CancellationToken ct = default)
    {
        var sql = """
            SELECT
                id::text,
                phenomenon_time,
                result_time,
                result,
                datastream_id::text,
                feature_of_interest_id::text,
                server_timestamp
            FROM sta_observations
            WHERE datastream_id = @DatastreamId::uuid
            """;

        // Apply ordering
        if (options.OrderBy?.Count > 0)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            sql += $" ORDER BY {string.Join(", ", orderClauses)}";
        }
        else
        {
            sql += " ORDER BY phenomenon_time DESC";
        }

        // Apply pagination
        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var observations = await _connection.QueryAsync<Observation>(
            new CommandDefinition(sql, new { DatastreamId = datastreamId }, cancellationToken: ct));

        // Generate self-links dynamically
        var observationsWithLinks = observations.Select(o => o with { SelfLink = $"{_config.BasePath}/Observations({o.Id})" }).ToList();

        return new PagedResult<Observation>
        {
            Items = observationsWithLinks
        };
    }

    public async Task<PagedResult<Datastream>> GetSensorDatastreamsAsync(string sensorId, QueryOptions options, CancellationToken ct = default)
    {
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
                ST_AsGeoJSON(observed_area)::jsonb as observed_area_geojson,
                phenomenon_time_start,
                phenomenon_time_end,
                result_time_start,
                result_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE sensor_id = @SensorId::uuid
            """);

        var parameters = new DynamicParameters();
        parameters.Add("SensorId", sensorId);

        // Apply additional filtering
        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" AND " + TranslateFilter(options.Filter, parameters));
        }

        // Apply ordering
        if (options.OrderBy?.Any() == true)
        {
            sqlBuilder.AppendLine(" ORDER BY " + string.Join(", ",
                options.OrderBy.Select(o => $"{o.Property} {(o.Direction == SortDirection.Ascending ? "ASC" : "DESC")}")));
        }
        else
        {
            sqlBuilder.AppendLine(" ORDER BY created_at DESC");
        }

        // Apply pagination
        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;

        sqlBuilder.AppendLine(" LIMIT @Limit OFFSET @Offset");
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var results = await _connection.QueryAsync<dynamic>(
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
            ObservedArea = r.observed_area_geojson != null
                ? _geoJsonReader.Read<Geometry>(r.observed_area_geojson.ToString())
                : null,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            ResultTimeStart = r.result_time_start,
            ResultTimeEnd = r.result_time_end,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_config.BasePath}/Datastreams({r.id})"
        }).ToList();

        // Get total count if requested
        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_datastreams WHERE sensor_id = @SensorId::uuid";
            if (options.Filter != null)
            {
                var countParams = new DynamicParameters();
                countParams.Add("SensorId", sensorId);
                countSql += " AND " + TranslateFilter(options.Filter, countParams);
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, countParams, cancellationToken: ct));
            }
            else
            {
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, new { SensorId = sensorId }, cancellationToken: ct));
            }
        }

        return new PagedResult<Datastream>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<Datastream>> GetObservedPropertyDatastreamsAsync(string observedPropertyId, QueryOptions options, CancellationToken ct = default)
    {
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
                ST_AsGeoJSON(observed_area)::jsonb as observed_area_geojson,
                phenomenon_time_start,
                phenomenon_time_end,
                result_time_start,
                result_time_end,
                properties,
                created_at,
                updated_at
            FROM sta_datastreams
            WHERE observed_property_id = @ObservedPropertyId::uuid
            """);

        var parameters = new DynamicParameters();
        parameters.Add("ObservedPropertyId", observedPropertyId);

        // Apply additional filtering
        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" AND " + TranslateFilter(options.Filter, parameters));
        }

        // Apply ordering
        if (options.OrderBy?.Any() == true)
        {
            sqlBuilder.AppendLine(" ORDER BY " + string.Join(", ",
                options.OrderBy.Select(o => $"{o.Property} {(o.Direction == SortDirection.Ascending ? "ASC" : "DESC")}")));
        }
        else
        {
            sqlBuilder.AppendLine(" ORDER BY created_at DESC");
        }

        // Apply pagination
        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;

        sqlBuilder.AppendLine(" LIMIT @Limit OFFSET @Offset");
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var results = await _connection.QueryAsync<dynamic>(
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
            ObservedArea = r.observed_area_geojson != null
                ? _geoJsonReader.Read<Geometry>(r.observed_area_geojson.ToString())
                : null,
            PhenomenonTimeStart = r.phenomenon_time_start,
            PhenomenonTimeEnd = r.phenomenon_time_end,
            ResultTimeStart = r.result_time_start,
            ResultTimeEnd = r.result_time_end,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_config.BasePath}/Datastreams({r.id})"
        }).ToList();

        // Get total count if requested
        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_datastreams WHERE observed_property_id = @ObservedPropertyId::uuid";
            if (options.Filter != null)
            {
                var countParams = new DynamicParameters();
                countParams.Add("ObservedPropertyId", observedPropertyId);
                countSql += " AND " + TranslateFilter(options.Filter, countParams);
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, countParams, cancellationToken: ct));
            }
            else
            {
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, new { ObservedPropertyId = observedPropertyId }, cancellationToken: ct));
            }
        }

        return new PagedResult<Datastream>
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
