// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Text.Json;
using Dapper;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// Partial class containing Thing entity operations.
/// Handles CRUD operations for Thing entities in the SensorThings API.
/// </summary>
public sealed partial class PostgresSensorThingsRepository
{
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

        // Use deferred execution for projection to avoid unnecessary materialization
        var thingsWithLinks = rows
            .Select(MapThing)
            .Select(t => t with { SelfLink = $"{_config.BasePath}/Things({t.Id})" });

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
            Items = thingsWithLinks.ToList(),
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

        // Return as IReadOnlyList to maintain contract while avoiding double allocation
        // Dapper already materializes, so we materialize the projection efficiently
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

    // ============================================================================
    // Thing helper methods
    // ============================================================================

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
}
