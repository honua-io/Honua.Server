using System.Data;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;

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
                id::text,
                name,
                description,
                properties,
                created_at,
                updated_at,
                self_link
            FROM sta_things
            WHERE id = @Id::uuid
            """;

        var thing = await _connection.QuerySingleOrDefaultAsync<Thing>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (thing == null)
            return null;

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
        var sql = "SELECT id::text, name, description, properties, created_at, updated_at, self_link FROM sta_things";
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

        var things = await _connection.QueryAsync<Thing>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

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
            Items = things.ToList(),
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<IReadOnlyList<Thing>> GetThingsByUserAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                id::text,
                name,
                description,
                properties,
                created_at,
                updated_at,
                self_link
            FROM sta_things
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            """;

        var things = await _connection.QueryAsync<Thing>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));

        return things.ToList();
    }

    public async Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_things (name, description, properties)
            VALUES (@Name, @Description, @Properties::jsonb)
            RETURNING id::text, name, description, properties, created_at, updated_at, self_link
            """;

        var created = await _connection.QuerySingleAsync<Thing>(
            new CommandDefinition(sql, new
            {
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

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
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING id::text, name, description, properties, created_at, updated_at, self_link
            """;

        var updated = await _connection.QuerySingleAsync<Thing>(
            new CommandDefinition(sql, new
            {
                Id = id,
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

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
                created_at,
                self_link
            FROM sta_observations
            WHERE id = @Id::uuid
            """;

        var observation = await _connection.QuerySingleOrDefaultAsync<Observation>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

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
                server_timestamp,
                self_link
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
            Items = observations.ToList(),
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
                server_timestamp,
                self_link
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
            await writer.WriteAsync(obs.PhenomenonTime, ct);
            await writer.WriteAsync(obs.ResultTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(JsonSerializer.Serialize(obs.Result), NpgsqlTypes.NpgsqlDbType.Jsonb, ct);
            await writer.WriteAsync(obs.ResultQuality, ct);
            await writer.WriteAsync(Guid.Parse(obs.DatastreamId), ct);
            await writer.WriteAsync(obs.FeatureOfInterestId != null ? Guid.Parse(obs.FeatureOfInterestId) : DBNull.Value, ct);
            await writer.WriteAsync(obs.ClientTimestamp, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(obs.SyncBatchId != null ? Guid.Parse(obs.SyncBatchId) : DBNull.Value, ct);
            await writer.WriteAsync(
                obs.Parameters != null ? JsonSerializer.Serialize(obs.Parameters) : DBNull.Value,
                NpgsqlTypes.NpgsqlDbType.Jsonb,
                ct);
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

    public async Task<PagedResult<Location>> GetThingLocationsAsync(
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

        var result = locations.Select(l => new Location
        {
            Id = l.id,
            Name = l.name,
            Description = l.description,
            EncodingType = l.encoding_type,
            Location = _geoJsonReader.Read<Geometry>(l.location_geojson.ToString()),
            Properties = l.properties,
            CreatedAt = l.created_at,
            UpdatedAt = l.updated_at,
            SelfLink = l.self_link
        }).ToList();

        return new PagedResult<Location>
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
                created_at,
                updated_at,
                self_link
            FROM sta_datastreams
            WHERE thing_id = @ThingId::uuid
            ORDER BY created_at DESC
            """;

        var datastreams = await _connection.QueryAsync<Datastream>(
            new CommandDefinition(sql, new { ThingId = thingId }, cancellationToken: ct));

        return new PagedResult<Datastream>
        {
            Items = datastreams.ToList()
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
                server_timestamp,
                self_link
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

        return new PagedResult<Observation>
        {
            Items = observations.ToList()
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
            "eq" => "=",
            "ne" => "!=",
            "gt" => ">",
            "ge" => ">=",
            "lt" => "<",
            "le" => "<=",
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
    // Stub implementations (to be completed)
    // ============================================================================

    // Location operations
    public Task<Location?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<Location>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Location> CreateLocationAsync(Location location, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Location> UpdateLocationAsync(string id, Location location, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteLocationAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();

    // HistoricalLocation operations
    public Task<HistoricalLocation?> GetHistoricalLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<HistoricalLocation>> GetHistoricalLocationsAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    // Sensor operations
    public Task<Sensor?> GetSensorAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<Sensor>> GetSensorsAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Sensor> CreateSensorAsync(Sensor sensor, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Sensor> UpdateSensorAsync(string id, Sensor sensor, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteSensorAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();

    // ObservedProperty operations
    public Task<ObservedProperty?> GetObservedPropertyAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<ObservedProperty>> GetObservedPropertiesAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<ObservedProperty> CreateObservedPropertyAsync(ObservedProperty observedProperty, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<ObservedProperty> UpdateObservedPropertyAsync(string id, ObservedProperty observedProperty, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteObservedPropertyAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();

    // Datastream operations
    public Task<Datastream?> GetDatastreamAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<Datastream>> GetDatastreamsAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Datastream> CreateDatastreamAsync(Datastream datastream, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<Datastream> UpdateDatastreamAsync(string id, Datastream datastream, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteDatastreamAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();

    // FeatureOfInterest operations
    public Task<FeatureOfInterest?> GetFeatureOfInterestAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<FeatureOfInterest>> GetFeaturesOfInterestAsync(QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<FeatureOfInterest> CreateFeatureOfInterestAsync(FeatureOfInterest featureOfInterest, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<FeatureOfInterest> UpdateFeatureOfInterestAsync(string id, FeatureOfInterest featureOfInterest, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteFeatureOfInterestAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<FeatureOfInterest> GetOrCreateFeatureOfInterestAsync(
        string name,
        string description,
        Geometry geometry,
        CancellationToken ct = default)
        => throw new NotImplementedException();

    // Additional navigation properties
    public Task<PagedResult<Datastream>> GetSensorDatastreamsAsync(string sensorId, QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<PagedResult<Datastream>> GetObservedPropertyDatastreamsAsync(string observedPropertyId, QueryOptions options, CancellationToken ct = default)
        => throw new NotImplementedException();
}
