using System;
using System.Collections.Generic;
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
/// PostgreSQL repository for Thing entities.
/// Handles CRUD operations for Thing entities with optimized queries.
/// </summary>
internal sealed class PostgresThingRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public PostgresThingRepository(string connectionString, ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Thing?> GetByIdAsync(string id, ExpandOptions? expand, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, name, description, properties, user_id, created_at, updated_at
            FROM things
            WHERE id = @Id";

        var thing = await conn.QuerySingleOrDefaultAsync<ThingDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (thing == null)
            return null;

        return MapToModel(thing);
    }

    public async Task<PagedResult<Thing>> GetPagedAsync(QueryOptions options, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var parameters = new DynamicParameters();
        var whereClause = string.Empty;

        if (options.Filter != null)
        {
            whereClause = "WHERE " + PostgresQueryHelper.TranslateFilter(options.Filter, parameters);
        }

        // Build ORDER BY clause
        var orderBy = "ORDER BY created_at DESC";
        if (options.OrderBy?.Any() == true)
        {
            var orderClauses = options.OrderBy.Select(o =>
                $"{o.Property} {(o.Descending ? "DESC" : "ASC")}");
            orderBy = "ORDER BY " + string.Join(", ", orderClauses);
        }

        // Get total count
        var countSql = $"SELECT COUNT(*) FROM things {whereClause}";
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: ct));

        // Get paged results
        parameters.Add("Skip", options.Skip);
        parameters.Add("Top", options.Top);

        var dataSql = $@"
            SELECT id, name, description, properties, user_id, created_at, updated_at
            FROM things
            {whereClause}
            {orderBy}
            OFFSET @Skip ROWS
            FETCH NEXT @Top ROWS ONLY";

        var things = await conn.QueryAsync<ThingDto>(
            new CommandDefinition(dataSql, parameters, cancellationToken: ct));

        var items = things.Select(MapToModel).ToList();

        return new PagedResult<Thing>(items, total, options.Skip, options.Top);
    }

    public async Task<IReadOnlyList<Thing>> GetByUserAsync(string userId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, name, description, properties, user_id, created_at, updated_at
            FROM things
            WHERE user_id = @UserId
            ORDER BY created_at DESC";

        var things = await conn.QueryAsync<ThingDto>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));

        return things.Select(MapToModel).ToList();
    }

    public async Task<Thing> CreateAsync(Thing thing, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        thing.Id = Guid.NewGuid().ToString();
        thing.CreatedAt = DateTimeOffset.UtcNow;
        thing.UpdatedAt = thing.CreatedAt;

        const string sql = @"
            INSERT INTO things (id, name, description, properties, user_id, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @Properties::jsonb, @UserId, @CreatedAt, @UpdatedAt)";

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            thing.Id,
            thing.Name,
            thing.Description,
            Properties = thing.Properties != null
                ? JsonSerializer.Serialize(thing.Properties)
                : null,
            thing.UserId,
            thing.CreatedAt,
            thing.UpdatedAt
        }, cancellationToken: ct));

        _logger.LogInformation("Created Thing {ThingId} for user {UserId}", thing.Id, thing.UserId);

        return thing;
    }

    public async Task<Thing> UpdateAsync(string id, Thing thing, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        thing.Id = id;
        thing.UpdatedAt = DateTimeOffset.UtcNow;

        const string sql = @"
            UPDATE things
            SET name = COALESCE(@Name, name),
                description = COALESCE(@Description, description),
                properties = COALESCE(@Properties::jsonb, properties),
                updated_at = @UpdatedAt
            WHERE id = @Id
            RETURNING id, name, description, properties, user_id, created_at, updated_at";

        var updated = await conn.QuerySingleAsync<ThingDto>(new CommandDefinition(sql, new
        {
            Id = id,
            thing.Name,
            thing.Description,
            Properties = thing.Properties != null
                ? JsonSerializer.Serialize(thing.Properties)
                : null,
            thing.UpdatedAt
        }, cancellationToken: ct));

        _logger.LogInformation("Updated Thing {ThingId}", id);

        return MapToModel(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM things WHERE id = @Id";

        var affected = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

        if (affected == 0)
        {
            throw new InvalidOperationException($"Thing {id} not found");
        }

        _logger.LogInformation("Deleted Thing {ThingId}", id);
    }

    private static Thing MapToModel(ThingDto dto)
    {
        return new Thing
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Properties = PostgresQueryHelper.ParseProperties(dto.Properties),
            UserId = dto.UserId,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    private sealed class ThingDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Properties { get; init; }
        public string? UserId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
