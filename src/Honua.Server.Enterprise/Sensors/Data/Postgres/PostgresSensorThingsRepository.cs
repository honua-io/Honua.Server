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
                updated_at
            FROM sta_things
            WHERE id = @Id::uuid
            """;

        var thing = await _connection.QuerySingleOrDefaultAsync<Thing>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (thing == null)
            return null;

        // Generate self-link dynamically
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
        var sql = "SELECT id::text, name, description, properties, created_at, updated_at FROM sta_things";
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

        // Generate self-links dynamically
        var thingsWithLinks = things.Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" }).ToList();

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
                id::text,
                name,
                description,
                properties,
                created_at,
                updated_at
            FROM sta_things
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            """;

        var things = await _connection.QueryAsync<Thing>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));

        // Generate self-links dynamically
        return things.Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" }).ToList();
    }

    public async Task<Thing> CreateThingAsync(Thing thing, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO sta_things (name, description, properties)
            VALUES (@Name, @Description, @Properties::jsonb)
            RETURNING id::text, name, description, properties, created_at, updated_at
            """;

        var created = await _connection.QuerySingleAsync<Thing>(
            new CommandDefinition(sql, new
            {
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

        // Generate self-link dynamically
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
                properties = COALESCE(@Properties::jsonb, properties)
            WHERE id = @Id::uuid
            RETURNING id::text, name, description, properties, created_at, updated_at
            """;

        var updated = await _connection.QuerySingleAsync<Thing>(
            new CommandDefinition(sql, new
            {
                Id = id,
                thing.Name,
                thing.Description,
                Properties = thing.Properties != null ? JsonSerializer.Serialize(thing.Properties) : null
            }, cancellationToken: ct));

        // Generate self-link dynamically
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
            SelfLink = $"{_config.BasePath}/Locations({l.id})"
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
                updated_at
            FROM sta_datastreams
            WHERE thing_id = @ThingId::uuid
            ORDER BY created_at DESC
            """;

        var datastreams = await _connection.QueryAsync<Datastream>(
            new CommandDefinition(sql, new { ThingId = thingId }, cancellationToken: ct));

        // Generate self-links dynamically
        var datastreamsWithLinks = datastreams.Select(d => d with { SelfLink = $"{_config.BasePath}/Datastreams({d.Id})" }).ToList();

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
    // Location operations
    // ============================================================================

    public async Task<Location?> GetLocationAsync(string id, ExpandOptions? expand = null, CancellationToken ct = default)
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

        var location = new Location
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Location = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
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

    public async Task<PagedResult<Location>> GetLocationsAsync(QueryOptions options, CancellationToken ct = default)
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

        var locations = results.Select(r => new Location
        {
            Id = r.id,
            Name = r.name,
            Description = r.description,
            EncodingType = r.encoding_type,
            Location = _geoJsonReader.Read<Geometry>(r.location_geojson.ToString()),
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

        return new PagedResult<Location>
        {
            Items = locations,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Location> CreateLocationAsync(Location location, CancellationToken ct = default)
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
                Location = _geoJsonWriter.Write(location.Location),
                Properties = location.Properties != null ? JsonSerializer.Serialize(location.Properties) : null
            }, cancellationToken: ct));

        var created = new Location
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Location = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
            Properties = result.properties,
            CreatedAt = result.created_at,
            UpdatedAt = result.updated_at,
            SelfLink = $"{_config.BasePath}/Locations({result.id})"
        };

        _logger.LogInformation("Created Location {LocationId} with name '{Name}'", created.Id, created.Name);

        return created;
    }

    public async Task<Location> UpdateLocationAsync(string id, Location location, CancellationToken ct = default)
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
                Location = location.Location != null ? _geoJsonWriter.Write(location.Location) : null,
                Properties = location.Properties != null ? JsonSerializer.Serialize(location.Properties) : null
            }, cancellationToken: ct));

        var updated = new Location
        {
            Id = result.id,
            Name = result.name,
            Description = result.description,
            EncodingType = result.encoding_type,
            Location = _geoJsonReader.Read<Geometry>(result.location_geojson.ToString()),
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

                var locations = results.Select(r => new Location
                {
                    Id = r.id,
                    Name = r.name,
                    Description = r.description,
                    EncodingType = r.encoding_type,
                    Location = _geoJsonReader.Read<Geometry>(r.location_geojson.ToString()),
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
