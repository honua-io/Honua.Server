using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;
using Geometry = NetTopologySuite.Geometries.Geometry;
using StaLocation = Honua.Server.Enterprise.Sensors.Models.Location;
using Honua.Server.Enterprise.Data;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL implementation of the SensorThings repository.
/// Uses Dapper for efficient data access and PostGIS for spatial operations.
/// </summary>
public sealed class PostgresSensorThingsRepository : ISensorThingsRepository
{
    private readonly IDbConnection _connection;
    private readonly SensorThingsServiceDefinition _config;
    private readonly ILogger<PostgresSensorThingsRepository> _logger;
    private readonly GeoJsonReader _geoJsonReader;
    private readonly GeoJsonWriter _geoJsonWriter;

    public PostgresSensorThingsRepository(
        IDbConnection connection,
        SensorThingsServiceDefinition config,
        ILogger<PostgresSensorThingsRepository> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connection = connection;
        _config = config;
        _logger = logger;
        _geoJsonReader = new GeoJsonReader();
        _geoJsonWriter = new GeoJsonWriter();
    }

    // ============================================================================
    // Thing operations
    // ============================================================================

    public async Task<Thing?> GetThingAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text AS Id,
                name AS Name,
                description AS Description,
                properties::text AS PropertiesJson,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM sta_things
            WHERE id = @Id::uuid
            """;

        var row = await _connection.QuerySingleOrDefaultAsync<ThingRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (row == null)
            return null;

        var thing = MapThing(row);
        thing = thing with { SelfLink = $"{_config.BasePath}/Things({thing.Id})" };

        // Handle expansions
        if (expand?.Properties.Contains("Locations") == true)
        {
            thing = thing with { Locations = (await GetThingLocationsAsync(id, new QueryOptions(), ct)).Items };
        }

        if (expand?.Properties.Contains("Datastreams") == true)
        {
            thing = thing with { Datastreams = (await GetThingDatastreamsAsync(id, new QueryOptions(), ct)).Items };
        }

        return thing;
    }

    public async Task<PagedResult<Thing>> GetThingsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = "SELECT id::text AS Id, name AS Name, description AS Description, properties::text AS PropertiesJson, created_at AS CreatedAt, updated_at AS UpdatedAt FROM sta_things";
        var countSql = "SELECT COUNT(*) FROM sta_things";
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

        var rows = await _connection.QueryAsync<ThingRow>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var thingsWithLinks = rows
            .Select(MapThing)
            .Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" })
            .ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        // Generate next link if there are more results
        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Things?$skip={offset + limit}&$top={limit}";
            if (options.Count)
                nextLink += "&$count=true";
        }

        return new PagedResult<Thing>
        {
            Items = thingsWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text AS Id,
                name AS Name,
                description AS Description,
                properties::text AS PropertiesJson,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM sta_things
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            """;

        var rows = await _connection.QueryAsync<ThingRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));

        return rows
            .Select(MapThing)
            .Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" })
            .ToList();
    }

    public async Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_things (name, description, properties)
            VALUES (@Name, @Description, @Properties::jsonb)
            RETURNING
                id::text AS Id,
                name AS Name,
                description AS Description,
                properties::text AS PropertiesJson,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        var createdRow = await _connection.QuerySingleAsync<ThingRow>(
            new CommandDefinition(sql, new
            {
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

        var created = MapThing(createdRow);
        created = created with { SelfLink = $"{_config.BasePath}/Things({created.Id})" };

        _logger.LogInformation("Created Thing {ThingId} with name '{Name}'", created.Id, created.Name);

        return created;
    }

    public async Task<Thing> UpdateThingAsync(string id, Thing thing, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE sta_things
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                properties = COALESCE(@Properties::jsonb, properties),
                updated_at = NOW()
            WHERE id = @Id::uuid
            RETURNING
                id::text AS Id,
                name AS Name,
                description AS Description,
                properties::text AS PropertiesJson,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        var updatedRow = await _connection.QuerySingleAsync<ThingRow>(
            new CommandDefinition(sql, new
            {
                Id = id,
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

        var updated = MapThing(updatedRow);
        updated = updated with { SelfLink = $"{_config.BasePath}/Things({updated.Id})" };

        _logger.LogInformation("Updated Thing {ThingId}", id);

        return updated;
    }

    public async Task DeleteThingAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_things WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Thing {ThingId}", id);
    }

    private Thing MapThing(ThingRow row)
    {
        return new Thing
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description ?? string.Empty,
            Properties = ParseProperties(row.PropertiesJson),
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    private static Dictionary<string, object>? ParseProperties(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, object>>(value);
    }

    private sealed class ThingRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? PropertiesJson { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    // ============================================================================
    // Observation operations (critical for mobile)
    // ============================================================================

    public async Task<Observation?> GetObservationAsync(string id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text,
                phenomenon_time,
                result_time,
                result,
                result_quality,
                valid_time_start,
                valid_time_end,
                parameters,
                datastream_id::text,
                feature_of_interest_id::text,
                client_timestamp,
                server_timestamp,
                sync_batch_id::text,
                created_at
            FROM sta_observations
            WHERE id = @Id::uuid
            """;

        var observation = await _connection.QuerySingleOrDefaultAsync<Observation>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (observation != null)
        {
            observation = observation with { SelfLink = $"{_config.BasePath}/Observations({observation.Id})" };
        }

        return observation;
    }

    public async Task<PagedResult<Observation>> GetObservationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                id::text,
                phenomenon_time,
                result_time,
                result,
                result_quality,
                datastream_id::text,
                feature_of_interest_id::text,
                client_timestamp,
                server_timestamp
            FROM sta_observations
            """;
        var countSql = "SELECT COUNT(*) FROM sta_observations";
        var parameters = new DynamicParameters();

        // Apply filters
        if (options.Filter != null)
        {
            var whereClause = TranslateFilter(options.Filter, parameters);
            sql += $" WHERE {whereClause}";
            countSql += $" WHERE {whereClause}";
        }

        // Default ordering by phenomenon time descending
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
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        // Generate self-links dynamically
        var observationsWithLinks = observations.Select(o => o with { SelfLink = $"{_config.BasePath}/Observations({o.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Observations?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Observation>
        {
            Items = observationsWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Observation> CreateObservationAsync(Observation observation, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_observations (
                phenomenon_time,
                result_time,
                result,
                result_quality,
                valid_time_start,
                valid_time_end,
                parameters,
                datastream_id,
                feature_of_interest_id,
                client_timestamp,
                sync_batch_id
            )
            VALUES (
                @PhenomenonTime,
                @ResultTime,
                @Result::jsonb,
                @ResultQuality,
                @ValidTimeStart,
                @ValidTimeEnd,
                @Parameters::jsonb,
                @DatastreamId::uuid,
                @FeatureOfInterestId::uuid,
                @ClientTimestamp,
                @SyncBatchId::uuid
            )
            RETURNING
                id::text,
                phenomenon_time,
                result_time,
                result,
                datastream_id::text,
                feature_of_interest_id::text,
                server_timestamp
            """;

        var created = await _connection.QuerySingleAsync<Observation>(
            new CommandDefinition(sql, new
            {
                observation.PhenomenonTime,
                observation.ResultTime,
                Result = JsonSerializer.Serialize(observation.Result),
                observation.ResultQuality,
                observation.ValidTimeStart,
                observation.ValidTimeEnd,
                Parameters = observation.Parameters != null ? JsonSerializer.Serialize(observation.Parameters) : null,
                observation.DatastreamId,
                observation.FeatureOfInterestId,
                observation.ClientTimestamp,
                observation.SyncBatchId
            }, cancellationToken: ct));

        // Generate self-link dynamically
        created = created with { SelfLink = $"{_config.BasePath}/Observations({created.Id})" };

        _logger.LogDebug("Created Observation {ObservationId} for Datastream {DatastreamId}",
            created.Id, created.DatastreamId);

        return created;
    }

    public async Task<IReadOnlyList<Observation>> CreateObservationsBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct = default)
    {
        if (observations.Count == 0)
            return Array.Empty<Observation>();

        if (observations.Count > _config.MaxObservationsPerRequest)
        {
            throw new ArgumentException(
                $"Batch size {observations.Count} exceeds maximum of {_config.MaxObservationsPerRequest}");
        }

        // For PostgreSQL, use COPY for maximum performance
        var connection = _connection as NpgsqlConnection
            ?? throw new InvalidOperationException("PostgreSQL connection required for bulk operations");

        const string copyCommand = """
            COPY sta_observations (
                phenomenon_time, result_time, result, result_quality,
                datastream_id, feature_of_interest_id, client_timestamp, sync_batch_id, parameters
            )
            FROM STDIN (FORMAT BINARY)
            """;

        await using var writer = await connection.BeginBinaryImportAsync(copyCommand, ct);

        foreach (var obs in observations)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(obs.PhenomenonTime, NpgsqlDbType.TimestampTz, ct);

            if (obs.ResultTime.HasValue)
                await writer.WriteAsync(obs.ResultTime.Value, NpgsqlDbType.TimestampTz, ct);
            else
                await writer.WriteNullAsync(ct);

            var resultJson = JsonSerializer.Serialize(obs.Result);
            await writer.WriteAsync(resultJson, NpgsqlDbType.Jsonb, ct);

            if (!string.IsNullOrWhiteSpace(obs.ResultQuality))
                await writer.WriteAsync(obs.ResultQuality, NpgsqlDbType.Text, ct);
            else
                await writer.WriteNullAsync(ct);

            await writer.WriteAsync(Guid.Parse(obs.DatastreamId), NpgsqlDbType.Uuid, ct);

            if (!string.IsNullOrWhiteSpace(obs.FeatureOfInterestId))
                await writer.WriteAsync(Guid.Parse(obs.FeatureOfInterestId), NpgsqlDbType.Uuid, ct);
            else
                await writer.WriteNullAsync(ct);

            if (obs.ClientTimestamp.HasValue)
                await writer.WriteAsync(obs.ClientTimestamp.Value, NpgsqlDbType.TimestampTz, ct);
            else
                await writer.WriteNullAsync(ct);

            if (!string.IsNullOrWhiteSpace(obs.SyncBatchId))
                await writer.WriteAsync(Guid.Parse(obs.SyncBatchId), NpgsqlDbType.Uuid, ct);
            else
                await writer.WriteNullAsync(ct);

            if (obs.Parameters != null && obs.Parameters.Count > 0)
                await writer.WriteAsync(JsonSerializer.Serialize(obs.Parameters), NpgsqlDbType.Jsonb, ct);
            else
                await writer.WriteNullAsync(ct);
        }

        await writer.CompleteAsync(ct);

        _logger.LogInformation("Bulk inserted {Count} observations", observations.Count);

        return observations;
    }

    public async Task<IReadOnlyList<Observation>> CreateObservationsDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct = default)
    {
        var observations = request.ToObservations();
        return await CreateObservationsBatchAsync(observations, ct);
    }

    public async Task DeleteObservationAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_observations WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Observation {ObservationId}", id);
    }

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

    // ============================================================================
    // Mobile-specific operations
    // ============================================================================

    public async Task<SyncResponse> SyncObservationsAsync(SyncRequest request, CancellationToken ct = default)
    {
        var errors = new List<SyncError>();
        var created = 0;

        try
        {
            // Batch create all observations
            await CreateObservationsBatchAsync(request.Observations, ct);
            created = request.Observations.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing observations for Thing {ThingId}", request.ThingId);
            errors.Add(new SyncError
            {
                Code = "SYNC_ERROR",
                Message = "Failed to sync observations",
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            });
        }

        return new SyncResponse
        {
            ServerTimestamp = DateTime.UtcNow,
            ObservationsCreated = created,
            ObservationsUpdated = 0,
            Errors = errors
        };
    }

    // ============================================================================
    // Helper methods
    // ============================================================================

    private string TranslateFilter(FilterExpression filter, DynamicParameters parameters)
    {
        return filter switch
        {
            ComparisonExpression comparison => TranslateComparison(comparison, parameters),
            LogicalExpression logical => TranslateLogical(logical, parameters),
            FunctionExpression function => TranslateFunction(function, parameters),
            _ => throw new NotSupportedException($"Filter expression type {filter.GetType().Name} not supported")
        };
    }

    private string TranslateComparison(ComparisonExpression expr, DynamicParameters parameters)
    {
        var paramName = $"p{parameters.ParameterNames.Count()}";
        parameters.Add(paramName, expr.Value);

        var op = expr.Operator switch
        {
            ComparisonOperator.Equals => "=",
            ComparisonOperator.NotEquals => "!=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Operator {expr.Operator} not supported")
        };

        return $"{expr.Property} {op} @{paramName}";
    }

    private string TranslateLogical(LogicalExpression expr, DynamicParameters parameters)
    {
        if (expr.Operator == "not" && expr.Left != null)
        {
            return $"NOT ({TranslateFilter(expr.Left, parameters)})";
        }

        if (expr.Left == null || expr.Right == null)
            throw new ArgumentException("Logical expression requires both left and right operands");

        var left = TranslateFilter(expr.Left, parameters);
        var right = TranslateFilter(expr.Right, parameters);

        return expr.Operator.ToUpper() switch
        {
            "AND" => $"({left} AND {right})",
            "OR" => $"({left} OR {right})",
            _ => throw new NotSupportedException($"Logical operator {expr.Operator} not supported")
        };
    }

    private string TranslateFunction(FunctionExpression expr, DynamicParameters parameters)
    {
        return expr.Name switch
        {
            "geo.intersects" => TranslateGeoIntersects(expr, parameters),
            "substringof" => TranslateSubstringOf(expr, parameters),
            _ => throw new NotSupportedException($"Function {expr.Name} not supported")
        };
    }

    private string TranslateGeoIntersects(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("geo.intersects requires 2 arguments");

        var property = expr.Arguments[0].ToString();
        var geometry = expr.Arguments[1];
        var paramName = $"geom{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, geometry);

        return $"ST_Intersects({property}, ST_GeomFromGeoJSON(@{paramName}))";
    }

    private string TranslateSubstringOf(FunctionExpression expr, DynamicParameters parameters)
    {
        if (expr.Arguments.Count < 2)
            throw new ArgumentException("substringof requires 2 arguments");

        var substring = expr.Arguments[0].ToString();
        var property = expr.Arguments[1].ToString();
        var paramName = $"str{parameters.ParameterNames.Count()}";

        parameters.Add(paramName, $"%{substring}%");

        return $"{property} LIKE @{paramName}";
    }

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

            location = location with { Things = things.Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" }).ToList() };
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
        }).ToList();

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

    // ============================================================================
    // HistoricalLocation operations (read-only - auto-generated by trigger)
    // ============================================================================

    public async Task<HistoricalLocation?> GetHistoricalLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text,
                thing_id::text,
                time,
                created_at
            FROM sta_historical_locations
            WHERE id = @Id::uuid
            """;

        var historicalLocation = await _connection.QuerySingleOrDefaultAsync<HistoricalLocation>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (historicalLocation != null)
        {
            historicalLocation = historicalLocation with { SelfLink = $"{_config.BasePath}/HistoricalLocations({historicalLocation.Id})" };

            // Handle expansions
            if (expand?.Properties.Contains("Thing") == true)
            {
                historicalLocation = historicalLocation with { Thing = await GetThingAsync(historicalLocation.ThingId, ct: ct) };
            }

            if (expand?.Properties.Contains("Locations") == true)
            {
                const string locationsSql = """
                    SELECT l.id::text, l.name, l.description, l.encoding_type,
                           ST_AsGeoJSON(l.location)::jsonb as location_geojson,
                           l.properties, l.created_at, l.updated_at
                    FROM sta_locations l
                    JOIN sta_historical_location_location hll ON l.id = hll.location_id
                    WHERE hll.historical_location_id = @HistoricalLocationId::uuid
                    """;

                var results = await _connection.QueryAsync<dynamic>(
                    new CommandDefinition(locationsSql, new { HistoricalLocationId = id }, cancellationToken: ct));

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
                }).ToList();

                historicalLocation = historicalLocation with { Locations = locations };
            }
        }

        return historicalLocation;
    }

    public async Task<PagedResult<HistoricalLocation>> GetHistoricalLocationsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = "SELECT id::text, thing_id::text, time, created_at FROM sta_historical_locations";
        var countSql = "SELECT COUNT(*) FROM sta_historical_locations";
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
            sql += " ORDER BY time DESC";
        }

        // Apply pagination
        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var historicalLocations = await _connection.QueryAsync<HistoricalLocation>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var historicalLocationsWithLinks = historicalLocations.Select(hl => hl with { SelfLink = $"{_config.BasePath}/HistoricalLocations({hl.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/HistoricalLocations?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<HistoricalLocation>
        {
            Items = historicalLocationsWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    // ============================================================================
    // Sensor operations
    // ============================================================================

    public async Task<Sensor?> GetSensorAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id::text, name, description, encoding_type, metadata, properties, created_at, updated_at
            FROM sta_sensors
            WHERE id = @Id::uuid
            """;

        var sensor = await _connection.QuerySingleOrDefaultAsync<Sensor>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (sensor != null)
        {
            sensor = sensor with { SelfLink = $"{_config.BasePath}/Sensors({sensor.Id})" };
        }

        return sensor;
    }

    public async Task<PagedResult<Sensor>> GetSensorsAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = "SELECT id::text, name, description, encoding_type, metadata, properties, created_at, updated_at FROM sta_sensors";
        var countSql = "SELECT COUNT(*) FROM sta_sensors";
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

        var sensors = await _connection.QueryAsync<Sensor>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var sensorsWithLinks = sensors.Select(s => s with { SelfLink = $"{_config.BasePath}/Sensors({s.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Sensors?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Sensor>
        {
            Items = sensorsWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_sensors (name, description, encoding_type, metadata, properties)
            VALUES (@Name, @Description, @EncodingType, @Metadata, @Properties::jsonb)
            RETURNING id::text, name, description, encoding_type, metadata, properties, created_at, updated_at
            """;

        var created = await _connection.QuerySingleAsync<Sensor>(
            new CommandDefinition(sql, new
            {
                sensor.Name,
                sensor.Description,
                sensor.EncodingType,
                sensor.Metadata,
                Properties = sensor.Properties != null ? JsonSerializer.Serialize(sensor.Properties) : null
            }, cancellationToken: ct));

        created = created with { SelfLink = $"{_config.BasePath}/Sensors({created.Id})" };

        _logger.LogInformation("Created Sensor {SensorId} with name '{Name}'", created.Id, created.Name);

        return created;
    }

    public async Task<Sensor> UpdateSensorAsync(string id, Sensor sensor, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE sta_sensors
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                encoding_type = COALESCE(@EncodingType, encoding_type),
                metadata = COALESCE(@Metadata, metadata),
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING id::text, name, description, encoding_type, metadata, properties, created_at, updated_at
            """;

        var updated = await _connection.QuerySingleAsync<Sensor>(
            new CommandDefinition(sql, new
            {
                Id = id,
                sensor.Name,
                sensor.Description,
                sensor.EncodingType,
                sensor.Metadata,
                Properties = sensor.Properties != null ? JsonSerializer.Serialize(sensor.Properties) : null
            }, cancellationToken: ct));

        updated = updated with { SelfLink = $"{_config.BasePath}/Sensors({updated.Id})" };

        _logger.LogInformation("Updated Sensor {SensorId}", id);

        return updated;
    }

    public async Task DeleteSensorAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_sensors WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Sensor {SensorId}", id);
    }

    // ============================================================================
    // ObservedProperty operations
    // ============================================================================

    public async Task<ObservedProperty?> GetObservedPropertyAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id::text, name, description, definition, properties, created_at, updated_at
            FROM sta_observed_properties
            WHERE id = @Id::uuid
            """;

        var observedProperty = await _connection.QuerySingleOrDefaultAsync<ObservedProperty>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (observedProperty != null)
        {
            observedProperty = observedProperty with { SelfLink = $"{_config.BasePath}/ObservedProperties({observedProperty.Id})" };
        }

        return observedProperty;
    }

    public async Task<PagedResult<ObservedProperty>> GetObservedPropertiesAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sql = "SELECT id::text, name, description, definition, properties, created_at, updated_at FROM sta_observed_properties";
        var countSql = "SELECT COUNT(*) FROM sta_observed_properties";
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
            sql += " ORDER BY name";
        }

        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var observedProperties = await _connection.QueryAsync<ObservedProperty>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var observedPropertiesWithLinks = observedProperties.Select(op => op with { SelfLink = $"{_config.BasePath}/ObservedProperties({op.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/ObservedProperties?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<ObservedProperty>
        {
            Items = observedPropertiesWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<ObservedProperty> CreateObservedPropertyAsync(ObservedProperty observedProperty, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_observed_properties (name, description, definition, properties)
            VALUES (@Name, @Description, @Definition, @Properties::jsonb)
            RETURNING id::text, name, description, definition, properties, created_at, updated_at
            """;

        var created = await _connection.QuerySingleAsync<ObservedProperty>(
            new CommandDefinition(sql, new
            {
                observedProperty.Name,
                observedProperty.Description,
                observedProperty.Definition,
                Properties = observedProperty.Properties != null ? JsonSerializer.Serialize(observedProperty.Properties) : null
            }, cancellationToken: ct));

        created = created with { SelfLink = $"{_config.BasePath}/ObservedProperties({created.Id})" };

        _logger.LogInformation("Created ObservedProperty {ObservedPropertyId} with definition '{Definition}'", created.Id, created.Definition);

        return created;
    }

    public async Task<ObservedProperty> UpdateObservedPropertyAsync(string id, ObservedProperty observedProperty, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE sta_observed_properties
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                definition = COALESCE(@Definition, definition),
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING id::text, name, description, definition, properties, created_at, updated_at
            """;

        var updated = await _connection.QuerySingleAsync<ObservedProperty>(
            new CommandDefinition(sql, new
            {
                Id = id,
                observedProperty.Name,
                observedProperty.Description,
                observedProperty.Definition,
                Properties = observedProperty.Properties != null ? JsonSerializer.Serialize(observedProperty.Properties) : null
            }, cancellationToken: ct));

        updated = updated with { SelfLink = $"{_config.BasePath}/ObservedProperties({updated.Id})" };

        _logger.LogInformation("Updated ObservedProperty {ObservedPropertyId}", id);

        return updated;
    }

    public async Task DeleteObservedPropertyAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_observed_properties WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted ObservedProperty {ObservedPropertyId}", id);
    }

    // ============================================================================
    // Datastream operations
    // ============================================================================

    public async Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
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

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(
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

    public async Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default)
    {
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

        var results = await _connection.QueryAsync<dynamic>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

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
        }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await _connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_config.BasePath}/Datastreams?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Datastream>
        {
            Items = datastreams,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default)
    {
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

        var result = await _connection.QuerySingleAsync<dynamic>(
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

    public async Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default)
    {
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

        var result = await _connection.QuerySingleAsync<dynamic>(
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

    public async Task DeleteDatastreamAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sta_datastreams WHERE id = @Id::uuid";

        await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Datastream {DatastreamId}", id);
    }

    // FeatureOfInterest operations
    public async Task<FeatureOfInterest?> GetFeatureOfInterestAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            WHERE id = @Id::uuid
            """;

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (result == null)
            return null;

        var featureOfInterest = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/FeaturesOfInterest({result.id})"
        };

        // Handle expansions
        if (expand?.Properties.Contains("Observations") == true)
        {
            var observations = await GetObservationsAsync(
                new QueryOptions
                {
                    Filter = new ComparisonExpression
                    {
                        Property = "FeatureOfInterestId",
                        Operator = ComparisonOperator.Equals,
                        Value = id
                    }
                },
                ct);
            featureOfInterest = featureOfInterest with { Observations = observations.Items };
        }

        return featureOfInterest;
    }

    public async Task<PagedResult<FeatureOfInterest>> GetFeaturesOfInterestAsync(QueryOptions options, CancellationToken ct = default)
    {
        var sqlBuilder = new StringBuilder("""
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            """);

        var parameters = new DynamicParameters();

        // Apply filtering
        if (options.Filter != null)
        {
            sqlBuilder.AppendLine(" WHERE " + TranslateFilter(options.Filter, parameters));
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

        var items = results.Select(r => new FeatureOfInterest
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            EncodingType = r.encoding_type,
            Feature = r.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(r.feature_geojson.ToString())
                : null!,
            Properties = r.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.properties.ToString())
                : null,
            CreatedAt = r.created_at,
            UpdatedAt = r.updated_at,
            SelfLink = $"{_config.BasePath}/FeaturesOfInterest({r.id})"
        }).ToList();

        // Get total count if requested
        long? totalCount = null;
        if (options.Count)
        {
            var countSql = "SELECT COUNT(*) FROM sta_features_of_interest";
            if (options.Filter != null)
            {
                var countParams = new DynamicParameters();
                countSql += " WHERE " + TranslateFilter(options.Filter, countParams);
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, countParams, cancellationToken: ct));
            }
            else
            {
                totalCount = await _connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(countSql, cancellationToken: ct));
            }
        }

        return new PagedResult<FeatureOfInterest>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<FeatureOfInterest> CreateFeatureOfInterestAsync(FeatureOfInterest featureOfInterest, CancellationToken ct = default)
    {
        var featureGeoJson = featureOfInterest.Feature != null
            ? _geoJsonWriter.Write(featureOfInterest.Feature)
            : null;

        const string sql = """
            INSERT INTO sta_features_of_interest (
                name,
                description,
                encoding_type,
                feature,
                properties
            )
            VALUES (
                @Name,
                @Description,
                @EncodingType,
                ST_GeomFromGeoJSON(@FeatureGeoJson),
                @Properties::jsonb
            )
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var result = await _connection.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                featureOfInterest.Name,
                featureOfInterest.Description,
                featureOfInterest.EncodingType,
                FeatureGeoJson = featureGeoJson,
                Properties = featureOfInterest.Properties != null
                    ? JsonSerializer.Serialize(featureOfInterest.Properties)
                    : null
            },
            cancellationToken: ct));

        var created = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/FeaturesOfInterest({result.id})"
        };

        _logger.LogInformation("Created FeatureOfInterest {FeatureOfInterestId}", created.Id);

        return created;
    }

    public async Task<FeatureOfInterest> UpdateFeatureOfInterestAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct = default)
    {
        var featureGeoJson = featureOfInterest.Feature != null
            ? _geoJsonWriter.Write(featureOfInterest.Feature)
            : null;

        const string sql = """
            UPDATE sta_features_of_interest
            SET
                name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                encoding_type = COALESCE(@EncodingType, encoding_type),
                feature = COALESCE(ST_GeomFromGeoJSON(@FeatureGeoJson), feature),
                properties = COALESCE(@Properties::jsonb, properties),
                updated_at = now()
            WHERE id = @Id::uuid
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(sql, new
            {
                Id = id,
                featureOfInterest.Name,
                featureOfInterest.Description,
                featureOfInterest.EncodingType,
                FeatureGeoJson = featureGeoJson,
                Properties = featureOfInterest.Properties != null
                    ? JsonSerializer.Serialize(featureOfInterest.Properties)
                    : null
            },
            cancellationToken: ct));

        if (result == null)
            throw new InvalidOperationException($"FeatureOfInterest {id} not found");

        var updated = new FeatureOfInterest
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Feature = result.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(result.feature_geojson.ToString())
                : null!,
            Properties = result.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(result.properties.ToString())
                : null,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/FeaturesOfInterest({result.id})"
        };

        _logger.LogInformation("Updated FeatureOfInterest {FeatureOfInterestId}", id);

        return updated;
    }

    public async Task DeleteFeatureOfInterestAsync(string id, CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM sta_features_of_interest
            WHERE id = @Id::uuid
            """;

        var rowsAffected = await _connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (rowsAffected == 0)
            throw new InvalidOperationException($"FeatureOfInterest {id} not found");

        _logger.LogInformation("Deleted FeatureOfInterest {FeatureOfInterestId}", id);
    }

    public async Task<FeatureOfInterest> GetOrCreateFeatureOfInterestAsync(
        string name,
        string description,
        Geometry geometry,
        CancellationToken ct = default)
    {
        var featureGeoJson = geometry != null ? _geoJsonWriter.Write(geometry) : null;

        // First, try to find an existing FeatureOfInterest with the same geometry
        const string findSql = """
            SELECT
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            FROM sta_features_of_interest
            WHERE ST_Equals(feature, ST_GeomFromGeoJSON(@FeatureGeoJson))
            LIMIT 1
            """;

        var existing = await _connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(findSql, new { FeatureGeoJson = featureGeoJson }, cancellationToken: ct));

        if (existing != null)
        {
            var existingId = (string)existing.id;
            _logger.LogDebug("Found existing FeatureOfInterest {FeatureOfInterestId} with matching geometry", existingId);

            return new FeatureOfInterest
            {
                Id = existingId,
                Name = existing.name,
                Description = existing.description,
                EncodingType = existing.encoding_type,
                Feature = existing.feature_geojson != null
                    ? _geoJsonReader.Read<Geometry>(existing.feature_geojson.ToString())
                    : null!,
                Properties = existing.properties != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(existing.properties.ToString())
                    : null,
                CreatedAt = existing.created_at,
                UpdatedAt = existing.updated_at,
                SelfLink = $"{_config.BasePath}/FeaturesOfInterest({existingId})"
            };
        }

        // If not found, create a new FeatureOfInterest
        const string createSql = """
            INSERT INTO sta_features_of_interest (
                name,
                description,
                encoding_type,
                feature
            )
            VALUES (
                @Name,
                @Description,
                'application/geo+json',
                ST_GeomFromGeoJSON(@FeatureGeoJson)
            )
            RETURNING
                id::text,
                name,
                description,
                encoding_type,
                ST_AsGeoJSON(feature)::jsonb as feature_geojson,
                properties,
                created_at,
                updated_at
            """;

        var created = await _connection.QuerySingleAsync<dynamic>(
            new CommandDefinition(createSql, new
            {
                Name = name,
                Description = description,
                FeatureGeoJson = featureGeoJson
            },
            cancellationToken: ct));

        var createdId = (string)created.id;
        _logger.LogInformation("Created new FeatureOfInterest {FeatureOfInterestId}", createdId);

        return new FeatureOfInterest
        {
            Id = createdId,
            Name = created.name,
            Description = created.description,
            EncodingType = created.encoding_type,
            Feature = created.feature_geojson != null
                ? _geoJsonReader.Read<Geometry>(created.feature_geojson.ToString())
                : null!,
            Properties = created.properties != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(created.properties.ToString())
                : null,
            CreatedAt = created.created_at,
            UpdatedAt = created.updated_at,
            SelfLink = $"{_config.BasePath}/FeaturesOfInterest({createdId})"
        };
    }

    // Additional navigation properties
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
