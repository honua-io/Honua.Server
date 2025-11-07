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
    private readonly string _basePath;
    private readonly int _maxObservationsPerRequest;
    private readonly ILogger _logger;

    public PostgresObservationRepository(
        string connectionString,
        string basePath,
        int maxObservationsPerRequest,
        ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _maxObservationsPerRequest = maxObservationsPerRequest;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Observation?> GetByIdAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, phenomenon_time, result_time, result, result_quality,
                   valid_time_start, valid_time_end, parameters,
                   datastream_id, feature_of_interest_id, client_timestamp, server_timestamp,
                   sync_batch_id, created_at
            FROM sta_observations
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

        var orderBy = "ORDER BY phenomenon_time DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        long? totalCount = null;
        if (options.Count)
        {
            var countSql = $"SELECT COUNT(*) FROM sta_observations {whereClause}";
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;
        parameters.Add("Skip", offset);
        parameters.Add("Top", limit);

        var dataSql = $@"
            SELECT id, phenomenon_time, result_time, result, result_quality,
                   valid_time_start, valid_time_end, parameters,
                   datastream_id, feature_of_interest_id, client_timestamp, server_timestamp,
                   sync_batch_id, created_at
            FROM sta_observations
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var observations = await conn.QueryAsync<ObservationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = observations.Select(MapToModel).ToList();

        return new PagedResult<Observation>
        {
            Items = items,
            TotalCount = totalCount ?? -1
        };
    }

    public async Task<Observation> CreateAsync(Observation observation, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var id = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;
        var serverTimestamp = DateTime.UtcNow;

        const string sql = @"
            INSERT INTO sta_observations (
                id, phenomenon_time, result_time, result, result_quality,
                valid_time_start, valid_time_end, parameters,
                datastream_id, feature_of_interest_id,
                client_timestamp, server_timestamp, sync_batch_id, created_at
            )
            VALUES (
                @Id::uuid, @PhenomenonTime, @ResultTime, @Result::jsonb, @ResultQuality,
                @ValidTimeStart, @ValidTimeEnd, @Parameters::jsonb,
                @DatastreamId::uuid, @FeatureOfInterestId::uuid,
                @ClientTimestamp, @ServerTimestamp, @SyncBatchId::uuid, @CreatedAt
            )";

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            observation.PhenomenonTime,
            observation.ResultTime,
            Result = SerializeResult(observation.Result),
            observation.ResultQuality,
            observation.ValidTimeStart,
            observation.ValidTimeEnd,
            Parameters = observation.Parameters != null
                ? JsonSerializer.Serialize(observation.Parameters)
                : null,
            observation.DatastreamId,
            observation.FeatureOfInterestId,
            observation.ClientTimestamp,
            ServerTimestamp = serverTimestamp,
            observation.SyncBatchId,
            CreatedAt = createdAt
        }, cancellationToken: ct));

        _logger.LogDebug("Created Observation {ObservationId}", id);

        return observation with
        {
            Id = id,
            CreatedAt = createdAt,
            ServerTimestamp = serverTimestamp
        };
    }

    public async Task<IReadOnlyList<Observation>> CreateBatchAsync(
        IReadOnlyList<Observation> observations,
        CancellationToken ct)
    {
        if (!observations.Any())
            return observations;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var now = DateTime.UtcNow;

        // Assign IDs and timestamps, converting to mutable list
        var updatedObservations = new List<Observation>();
        foreach (var obs in observations)
        {
            var updated = obs with
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = now,
                ServerTimestamp = now
            };
            updatedObservations.Add(updated);
        }

        // Use PostgreSQL COPY for bulk insert (most efficient)
        await using var writer = conn.BeginBinaryImport(@"
            COPY sta_observations (
                id, phenomenon_time, result_time, result, result_quality,
                valid_time_start, valid_time_end, parameters,
                datastream_id, feature_of_interest_id,
                client_timestamp, server_timestamp, sync_batch_id, created_at
            ) FROM STDIN (FORMAT BINARY)");

        foreach (var obs in updatedObservations)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(Guid.Parse(obs.Id), ct);
            await writer.WriteAsync(obs.PhenomenonTime, ct);
            await writer.WriteAsync<object>(obs.ResultTime ?? (object)DBNull.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(SerializeResult(obs.Result), ct);
            await writer.WriteAsync<object>(obs.ResultQuality ?? (object)DBNull.Value, NpgsqlTypes.NpgsqlDbType.Text, ct);
            await writer.WriteAsync<object>(obs.ValidTimeStart ?? (object)DBNull.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync<object>(obs.ValidTimeEnd ?? (object)DBNull.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync<object>(
                obs.Parameters != null ? JsonSerializer.Serialize(obs.Parameters) : DBNull.Value,
                NpgsqlTypes.NpgsqlDbType.Jsonb,
                ct);
            await writer.WriteAsync(Guid.Parse(obs.DatastreamId), ct);
            await writer.WriteAsync<object>(
                obs.FeatureOfInterestId != null ? (object)Guid.Parse(obs.FeatureOfInterestId) : DBNull.Value,
                NpgsqlTypes.NpgsqlDbType.Uuid,
                ct);
            await writer.WriteAsync<object>(obs.ClientTimestamp ?? (object)DBNull.Value, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(obs.ServerTimestamp, ct);
            await writer.WriteAsync<object>(
                obs.SyncBatchId != null ? (object)Guid.Parse(obs.SyncBatchId) : DBNull.Value,
                NpgsqlTypes.NpgsqlDbType.Uuid,
                ct);
            await writer.WriteAsync(obs.CreatedAt, ct);
        }

        await writer.CompleteAsync(ct);

        _logger.LogInformation("Created {Count} observations via batch insert", updatedObservations.Count);

        return updatedObservations;
    }

    public async Task<IReadOnlyList<Observation>> CreateDataArrayAsync(
        DataArrayRequest request,
        CancellationToken ct)
    {
        // Use the built-in ToObservations method from DataArrayRequest
        var observations = request.ToObservations();

        if (observations.Count == 0)
        {
            return Array.Empty<Observation>();
        }

        // Batch insert all observations
        return await CreateBatchAsync(observations.ToList(), ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM sta_observations WHERE id = @Id::uuid";

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

        var whereClause = "WHERE datastream_id = @DatastreamId::uuid";

        if (options.Filter != null)
        {
            var filterClause = PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
            whereClause += $" AND ({filterClause})";
        }

        var orderBy = "ORDER BY phenomenon_time DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Direction == SortDirection.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        long? totalCount = null;
        if (options.Count)
        {
            var countSql = $"SELECT COUNT(*) FROM sta_observations {whereClause}";
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        var limit = options.Top ?? 100;
        var offset = options.Skip ?? 0;
        parameters.Add("Skip", offset);
        parameters.Add("Top", limit);

        var dataSql = $@"
            SELECT id, phenomenon_time, result_time, result, result_quality,
                   valid_time_start, valid_time_end, parameters,
                   datastream_id, feature_of_interest_id, client_timestamp, server_timestamp,
                   sync_batch_id, created_at
            FROM sta_observations
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var observations = await conn.QueryAsync<ObservationDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = observations.Select(MapToModel).ToList();

        return new PagedResult<Observation>
        {
            Items = items,
            TotalCount = totalCount ?? -1
        };
    }

    private static Observation MapToModel(ObservationDto dto)
    {
        return new Observation
        {
            Id = dto.Id,
            PhenomenonTime = dto.PhenomenonTime,
            ResultTime = dto.ResultTime,
            Result = DeserializeResult(dto.Result),
            ResultQuality = dto.ResultQuality,
            ValidTimeStart = dto.ValidTimeStart,
            ValidTimeEnd = dto.ValidTimeEnd,
            Parameters = PostgresQueryHelper.ParseProperties(dto.Parameters),
            DatastreamId = dto.DatastreamId,
            FeatureOfInterestId = dto.FeatureOfInterestId,
            ClientTimestamp = dto.ClientTimestamp,
            ServerTimestamp = dto.ServerTimestamp,
            SyncBatchId = dto.SyncBatchId,
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

    private sealed class ObservationDto
    {
        public string Id { get; init; } = string.Empty;
        public DateTime PhenomenonTime { get; init; }
        public DateTime? ResultTime { get; init; }
        public string? Result { get; init; }
        public string? ResultQuality { get; init; }
        public DateTime? ValidTimeStart { get; init; }
        public DateTime? ValidTimeEnd { get; init; }
        public string? Parameters { get; init; }
        public string DatastreamId { get; init; } = string.Empty;
        public string? FeatureOfInterestId { get; init; }
        public DateTime? ClientTimestamp { get; init; }
        public DateTime ServerTimestamp { get; init; }
        public string? SyncBatchId { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
