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
/// PostgreSQL implementation for ObservedProperty entity operations.
/// </summary>
internal sealed class PostgresObservedPropertyRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly string _basePath;

    public PostgresObservedPropertyRepository(string connectionString, string basePath, ILogger logger)
    {
        _connectionString = connectionString;
        _basePath = basePath;
        _logger = logger;
    }

    public async Task<ObservedProperty?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id::text, name, description, definition, properties, created_at, updated_at
            FROM sta_observed_properties
            WHERE id = @Id::uuid
            """;

        var observedProperty = await conn.QuerySingleOrDefaultAsync<ObservedProperty>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (observedProperty != null)
        {
            observedProperty = observedProperty with { SelfLink = $"{_basePath}/ObservedProperties({observedProperty.Id})" };
        }

        return observedProperty;
    }

    public async Task<PagedResult<ObservedProperty>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = "SELECT id::text, name, description, definition, properties, created_at, updated_at FROM sta_observed_properties";
        var countSql = "SELECT COUNT(*) FROM sta_observed_properties";
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
            sql += " ORDER BY name";
        }

        var limit = Math.Min(options.Top ?? 100, 10000);
        var offset = options.Skip ?? 0;
        sql += $" LIMIT {limit} OFFSET {offset}";

        var observedProperties = await conn.QueryAsync<ObservedProperty>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var observedPropertiesWithLinks = observedProperties.Select(op => op with { SelfLink = $"{_basePath}/ObservedProperties({op.Id})" }).ToList();

        long? totalCount = null;
        if (options.Count)
        {
            totalCount = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(countSql, parameters, cancellationToken: ct));
        }

        string? nextLink = null;
        if (totalCount.HasValue && offset + limit < totalCount.Value)
        {
            nextLink = $"{_basePath}/ObservedProperties?$skip={offset + limit}&$top={limit}";
        }

        return new PagedResult<ObservedProperty>
        {
            Items = observedPropertiesWithLinks,
            TotalCount = totalCount,
            NextLink = nextLink
        };
    }

    public async Task<ObservedProperty> CreateAsync(ObservedProperty observedProperty, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO sta_observed_properties (name, description, definition, properties)
            VALUES (@Name, @Description, @Definition, @Properties::jsonb)
            RETURNING id::text, name, description, definition, properties, created_at, updated_at
            """;

        var created = await conn.QuerySingleAsync<ObservedProperty>(
            new CommandDefinition(sql, new
            {
                observedProperty.Name,
                observedProperty.Description,
                observedProperty.Definition,
                Properties = observedProperty.Properties != null ? JsonSerializer.Serialize(observedProperty.Properties) : null
            }, cancellationToken: ct));

        created = created with { SelfLink = $"{_basePath}/ObservedProperties({created.Id})" };

        _logger.LogInformation("Created ObservedProperty {ObservedPropertyId} with definition '{Definition}'", created.Id, created.Definition);

        return created;
    }

    public async Task<ObservedProperty> UpdateAsync(string id, ObservedProperty observedProperty, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

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

        var updated = await conn.QuerySingleAsync<ObservedProperty>(
            new CommandDefinition(sql, new
            {
                Id = id,
                observedProperty.Name,
                observedProperty.Description,
                observedProperty.Definition,
                Properties = observedProperty.Properties != null ? JsonSerializer.Serialize(observedProperty.Properties) : null
            }, cancellationToken: ct));

        updated = updated with { SelfLink = $"{_basePath}/ObservedProperties({updated.Id})" };

        _logger.LogInformation("Updated ObservedProperty {ObservedPropertyId}", id);

        return updated;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM sta_observed_properties WHERE id = @Id::uuid";

        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        _logger.LogInformation("Deleted ObservedProperty {ObservedPropertyId}", id);
    }
}
