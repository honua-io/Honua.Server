// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Partial class containing Observation entity operations.
/// Handles CRUD operations for Observation entities, including bulk insert operations
/// critical for mobile sensor data synchronization.
/// </summary>
public sealed partial class PostgresSensorThingsRepository
{
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

        using var connection = _connectionFactory.CreateConnection();
        var observation = await connection.QuerySingleOrDefaultAsync<Observation>(
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
        var whereClauses = new List<string>();
        if (options.Filter != null)
        {
            whereClauses.Add(TranslateFilter(options.Filter, parameters));
        }

        // CURSOR-BASED PAGINATION: Use phenomenon_time cursor for efficient pagination
        // Supports both cursor-based (preferred) and offset-based (legacy) pagination
        string? cursor = options.Cursor;
        var offset = options.Skip ?? 0;
        var limit = Math.Min(options.Top ?? 100, 10000);

        // Warn about inefficient offset pagination for large offsets
        if (offset > 1000 && string.IsNullOrEmpty(cursor))
        {
            _logger.LogWarning(
                "Inefficient OFFSET pagination detected: OFFSET={Offset}. " +
                "Consider using cursor-based pagination with phenomenon_time cursor for better performance.",
                offset);
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            // Cursor-based pagination: phenomenon_time < cursor
            // Cursor format: ISO 8601 timestamp (e.g., "2024-11-14T10:30:00Z")
            if (DateTime.TryParse(cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cursorTime))
            {
                whereClauses.Add("phenomenon_time < @cursor");
                parameters.Add("@cursor", cursorTime);
            }
            else
            {
                _logger.LogWarning("Invalid cursor format: {Cursor}. Expected ISO 8601 timestamp.", cursor);
            }
        }

        // Build WHERE clause
        if (whereClauses.Count > 0)
        {
            var whereClause = string.Join(" AND ", whereClauses);
            sql += $" WHERE {whereClause}";

            // For count, only include filter (not cursor)
            if (options.Filter != null)
            {
                countSql += $" WHERE {TranslateFilter(options.Filter, new DynamicParameters())}";
            }
        }

        // Default ordering by phenomenon time descending (required for cursor pagination)
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

        // Apply pagination - use OFFSET only if no cursor provided (backward compatibility)
        if (string.IsNullOrEmpty(cursor))
        {
            sql += $" LIMIT {limit} OFFSET {offset}";
        }
        else
        {
            sql += $" LIMIT {limit}";
        }

        using var connection = _connectionFactory.CreateConnection();
        var observations = await connection.QueryAsync<Observation>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        // Generate self-links dynamically - Use deferred execution to avoid materializing collection
        var observationsWithLinks = observations.Select(o => o with { SelfLink = $"{_config.BasePath}/Observations({o.Id})" });
        var observationsList = observationsWithLinks.ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        // Generate next link with cursor if available
        string? nextLink = null;
        if (observationsList.Count == limit)
        {
            // Use cursor-based pagination for next link
            var lastObservation = observationsList.Last();
            var nextCursor = lastObservation.PhenomenonTime.ToString("O"); // ISO 8601 format
            nextLink = $"{_config.BasePath}/Observations?cursor={Uri.EscapeDataString(nextCursor)}&$top={limit}";
        }
        else if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            // Fallback to offset-based for backward compatibility
            nextLink = $"{_config.BasePath}/Observations?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Observation>
        {
            Items = observationsList,
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

        using var connection = _connectionFactory.CreateConnection();
        var created = await connection.QuerySingleAsync<Observation>(
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
        // Create connection from factory and cast to NpgsqlConnection
        await using var connection = _connectionFactory.CreateConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("PostgreSQL connection required for bulk operations");

        await connection.OpenAsync(ct);

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

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Observation {ObservationId}", id);
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
}
