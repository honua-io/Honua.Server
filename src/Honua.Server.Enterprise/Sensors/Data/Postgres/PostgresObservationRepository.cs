using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL repository for Observation entities.
/// Handles CRUD operations with optimized batch insert capabilities for mobile devices.
/// </summary>
internal sealed class PostgresObservationRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public PostgresObservationRepository(string connectionString, ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Observation?> GetByIdAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, result_time, result, result_quality, valid_time, parameters,
                   datastream_id, feature_of_interest_id, created_at
            FROM observations
            WHERE id = @Id";

        var obs = await conn.QuerySingleOrDefaultAsync<ObservationDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        return obs != null ? MapToModel(obs) : null;
    }

    public async Task<PagedResult<Observation>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        var whereClause = string.Empty;

        if (options.Filter != null)
        {
            whereClause = "WHERE " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
        }

        var orderBy = "ORDER BY result_time DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        var countSql = $"SELECT COUNT(*) FROM observations {whereClause}";
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        parameters.Add("Skip", options.Skip);
        parameters.Add("Top", options.Top);

        var dataSql = $@"
            SELECT id, result_time, result, result_quality, valid_time, parameters,
                   datastream_id, feature_of_interest_id, created_at
            FROM observations
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var observations = await conn.QueryAsync<ObservationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = observations.Select(MapToModel).ToList();

        return new PagedResult<Observation>(items, total, options.Skip, options.Top);
    }

    public async Task<Observation> CreateAsync(Observation observation, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        observation.Id = Guid.NewGuid().ToString();
        observation.CreatedAt = DateTimeOffset.UtcNow;

        const string sql = @"
            INSERT INTO observations (
                id, result_time, result, result_quality, valid_time, parameters,
                datastream_id, feature_of_interest_id, created_at
            )
            VALUES (
                @Id, @ResultTime, @Result, @ResultQuality, @ValidTime, @Parameters::jsonb,
                @DatastreamId, @FeatureOfInterestId, @CreatedAt
            )";

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            observation.Id,
            observation.ResultTime,
            Result = SerializeResult(observation.Result),
            observation.ResultQuality,
            observation.ValidTime,
            Parameters = observation.Parameters != null
                ? JsonSerializer.Serialize(observation.Parameters)
                : null,
            observation.DatastreamId,
            observation.FeatureOfInterestId,
            observation.CreatedAt
        }, cancellationToken: ct));

        _logger.LogDebug("Created Observation {ObservationId}", observation.Id);

        return observation;
    }

    public async Task<IReadOnlyList<Observation>> CreateBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct)
    {
        if (!observations.Any())
            return observations;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var now = DateTimeOffset.UtcNow;

        // Assign IDs and timestamps
        foreach (var obs in observations)
        {
            obs.Id = Guid.NewGuid().ToString();
            obs.CreatedAt = now;
        }

        // Use PostgreSQL COPY for bulk insert (most efficient)
        await using var writer = conn.BeginBinaryImport(@"
            COPY observations (
                id, result_time, result, result_quality, valid_time, parameters,
                datastream_id, feature_of_interest_id, created_at
            ) FROM STDIN (FORMAT BINARY)");

        foreach (var obs in observations)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(obs.Id, ct);
            await writer.WriteAsync(obs.ResultTime, ct);
            await writer.WriteAsync(SerializeResult(obs.Result), ct);
            await writer.WriteAsync(obs.ResultQuality, ct);
            await writer.WriteAsync(obs.ValidTime, ct);
            await writer.WriteAsync(
                obs.Parameters != null ? JsonSerializer.Serialize(obs.Parameters) : DBNull.Value,
                ct);
            await writer.WriteAsync(obs.DatastreamId, ct);
            await writer.WriteAsync(obs.FeatureOfInterestId ?? (object)DBNull.Value, ct);
            await writer.WriteAsync(obs.CreatedAt, ct);
        }

        await writer.CompleteAsync(ct);

        _logger.LogInformation("Created {Count} observations via batch insert", observations.Count);

        return observations;
    }

    public async Task<IReadOnlyList<Observation>> CreateDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct)
    {
        if (request.Datastreams.Count == 0 || request.Data.Count == 0)
        {
            return Array.Empty<Observation>();
        }

        var observations = new List<Observation>();

        // Parse component indices
        var resultTimeIdx = request.Components.IndexOf("resultTime");
        var resultIdx = request.Components.IndexOf("result");
        var datastreamIdx = request.Components.IndexOf("Datastream");

        if (resultTimeIdx < 0 || resultIdx < 0 || datastreamIdx < 0)
        {
            throw new InvalidOperationException(
                "DataArray must include resultTime, result, and Datastream components");
        }

        // Convert data array to observations
        foreach (var row in request.Data)
        {
            if (row.Count != request.Components.Count)
            {
                _logger.LogWarning("DataArray row has {ActualCount} elements but expected {ExpectedCount}",
                    row.Count, request.Components.Count);
                continue;
            }

            var datastreamRef = row[datastreamIdx]?.ToString();
            if (string.IsNullOrEmpty(datastreamRef))
            {
                _logger.LogWarning("DataArray row missing datastream reference");
                continue;
            }

            // Extract datastream ID from reference (format: "Datastreams('id')")
            var datastreamId = ExtractDatastreamId(datastreamRef);

            var observation = new Observation
            {
                ResultTime = ParseDateTime(row[resultTimeIdx]),
                Result = row[resultIdx],
                DatastreamId = datastreamId,
                ValidTime = null,
                ResultQuality = null,
                Parameters = null
            };

            observations.Add(observation);
        }

        // Batch insert all observations
        return await CreateBatchAsync(observations, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM observations WHERE id = @Id";

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (affected == 0)
        {
            throw new InvalidOperationException($"Observation {id} not found");
        }

        _logger.LogDebug("Deleted Observation {ObservationId}", id);
    }

    public async Task<PagedResult<Observation>> GetByDatastreamAsync(
        string datastreamId,
        QueryOptions options,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("DatastreamId", datastreamId);

        var whereClause = "WHERE datastream_id = @DatastreamId";

        if (options.Filter != null)
        {
            var filterClause = PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
            whereClause += $" AND ({filterClause})";
        }

        var orderBy = "ORDER BY result_time DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        var countSql = $"SELECT COUNT(*) FROM observations {whereClause}";
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        parameters.Add("Skip", options.Skip);
        parameters.Add("Top", options.Top);

        var dataSql = $@"
            SELECT id, result_time, result, result_quality, valid_time, parameters,
                   datastream_id, feature_of_interest_id, created_at
            FROM observations
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var observations = await conn.QueryAsync<ObservationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = observations.Select(MapToModel).ToList();

        return new PagedResult<Observation>(items, total, options.Skip, options.Top);
    }

    private static Observation MapToModel(ObservationDto dto)
    {
        return new Observation
        {
            Id = dto.Id,
            ResultTime = dto.ResultTime,
            Result = DeserializeResult(dto.Result),
            ResultQuality = dto.ResultQuality,
            ValidTime = dto.ValidTime,
            Parameters = PostgresQueryHelper.ParseProperties(dto.Parameters),
            DatastreamId = dto.DatastreamId,
            FeatureOfInterestId = dto.FeatureOfInterestId,
            CreatedAt = dto.CreatedAt
        };
    }

    private static string SerializeResult(object? result)
    {
        if (result == null)
            return "null";

        return result is string str ? str : JsonSerializer.Serialize(result);
    }

    private static object? DeserializeResult(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
            return null;

        try
        {
            return JsonSerializer.Deserialize<object>(result);
        }
        catch
        {
            return result; // Return as string if not valid JSON
        }
    }

    private static DateTimeOffset ParseDateTime(object? value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            string str => DateTimeOffset.Parse(str),
            _ => DateTimeOffset.UtcNow
        };
    }

    private static string ExtractDatastreamId(string reference)
    {
        // Format: "Datastreams('id')" or just "id"
        var start = reference.IndexOf('\'');
        if (start < 0)
            return reference;

        var end = reference.LastIndexOf('\'');
        if (end <= start)
            return reference;

        return reference.Substring(start + 1, end - start - 1);
    }

    private sealed class ObservationDto
    {
        public string Id { get; init; } = string.Empty;
        public DateTimeOffset ResultTime { get; init; }
        public string? Result { get; init; }
        public string? ResultQuality { get; init; }
        public DateTimeOffset? ValidTime { get; init; }
        public string? Parameters { get; init; }
        public string DatastreamId { get; init; } = string.Empty;
        public string? FeatureOfInterestId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
