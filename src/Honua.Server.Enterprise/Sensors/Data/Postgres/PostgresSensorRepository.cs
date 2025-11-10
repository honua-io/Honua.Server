// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
/// PostgreSQL implementation for Sensor entity operations.
/// </summary>
internal sealed class PostgresSensorRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly string _basePath;

    public PostgresSensorRepository(string connectionString, string basePath, ILogger logger)
    {
        _connectionString = connectionString;
        _basePath = basePath;
        _logger = logger;
    }

    public async Task<Sensor?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id::text, name, description, encoding_type, metadata, properties, created_at, updated_at
            FROM sta_sensors
            WHERE id = @Id::uuid
            """;

        var sensor = await conn.QuerySingleOrDefaultAsync<Sensor>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (sensor != null)
        {
            sensor = sensor with { SelfLink = $"{_basePath}/Sensors({sensor.Id})" };
        }

        return sensor;
    }

    public async Task<PagedResult<Sensor>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = "SELECT id::text, name, description, encoding_type, metadata, properties, created_at, updated_at FROM sta_sensors";
        var countSql = "SELECT COUNT(*) FROM sta_sensors";
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

        var sensors = await conn.QueryAsync<Sensor>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var sensorsWithLinks = sensors.Select(s => s with { SelfLink = $"{_basePath}/Sensors({s.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_basePath}/Sensors?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<Sensor>
        {
            Items = sensorsWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<Sensor> CreateAsync(Sensor sensor, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO sta_sensors (name, description, encoding_type, metadata, properties)
            VALUES (@Name, @Description, @EncodingType, @Metadata, @Properties::jsonb)
            RETURNING id::text, name, description, encoding_type, metadata, properties, created_at, updated_at
            """;

        var created = await conn.QuerySingleAsync<Sensor>(
            new CommandDefinition(sql, new
            {
                sensor.Name,
                sensor.Description,
                sensor.EncodingType,
                sensor.Metadata,
                Properties = sensor.Properties != null ? JsonSerializer.Serialize(sensor.Properties) : null
            }, cancellationToken: ct));

        created = created with { SelfLink = $"{_basePath}/Sensors({created.Id})" };

        _logger.LogInformation("Created Sensor {SensorId} with name '{Name}'", created.Id, created.Name);

        return created;
    }

    public async Task<Sensor> UpdateAsync(string id, Sensor sensor, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

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

        var updated = await conn.QuerySingleAsync<Sensor>(
            new CommandDefinition(sql, new
            {
                Id = id,
                sensor.Name,
                sensor.Description,
                sensor.EncodingType,
                sensor.Metadata,
                Properties = sensor.Properties != null ? JsonSerializer.Serialize(sensor.Properties) : null
            }, cancellationToken: ct));

        updated = updated with { SelfLink = $"{_basePath}/Sensors({updated.Id})" };

        _logger.LogInformation("Updated Sensor {SensorId}", id);

        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM sta_sensors WHERE id = @Id::uuid";

        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted Sensor {SensorId}", id);
    }
}
