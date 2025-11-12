// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Data;
using System.Text.Json;
using Dapper;
using Honua.Server.Core.Models.Dashboard;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.Data.Dashboard;

/// <summary>
/// Repository for managing dashboard persistence.
/// </summary>
public interface IDashboardRepository
{
    Task<DashboardDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DashboardDefinition>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
    Task<List<DashboardDefinition>> GetPublicDashboardsAsync(CancellationToken cancellationToken = default);
    Task<List<DashboardDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<List<DashboardDefinition>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(DashboardDefinition dashboard, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(DashboardDefinition dashboard, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ShareAsync(Guid id, bool isPublic, CancellationToken cancellationToken = default);
    Task<DashboardDefinition?> CloneAsync(Guid sourceId, string newOwnerId, string? newName = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL implementation of dashboard repository.
/// </summary>
public class PostgresDashboardRepository : IDashboardRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresDashboardRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PostgresDashboardRepository(
        string connectionString,
        ILogger<PostgresDashboardRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<DashboardDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, owner_id, tags, definition, is_public, is_template,
                   created_at, updated_at, schema_version
            FROM dashboards
            WHERE id = @Id AND deleted_at IS NULL";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DashboardRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return row != null ? MapRowToDashboard(row) : null;
    }

    public async Task<List<DashboardDefinition>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, owner_id, tags, definition, is_public, is_template,
                   created_at, updated_at, schema_version
            FROM dashboards
            WHERE owner_id = @OwnerId AND deleted_at IS NULL
            ORDER BY updated_at DESC";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DashboardRow>(
            new CommandDefinition(sql, new { OwnerId = ownerId }, cancellationToken: cancellationToken));

        return rows.Select(MapRowToDashboard).ToList();
    }

    public async Task<List<DashboardDefinition>> GetPublicDashboardsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, owner_id, tags, definition, is_public, is_template,
                   created_at, updated_at, schema_version
            FROM dashboards
            WHERE is_public = true AND deleted_at IS NULL
            ORDER BY updated_at DESC";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DashboardRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(MapRowToDashboard).ToList();
    }

    public async Task<List<DashboardDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, owner_id, tags, definition, is_public, is_template,
                   created_at, updated_at, schema_version
            FROM dashboards
            WHERE is_template = true AND deleted_at IS NULL
            ORDER BY name";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DashboardRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(MapRowToDashboard).ToList();
    }

    public async Task<List<DashboardDefinition>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, name, description, owner_id, tags, definition, is_public, is_template,
                   created_at, updated_at, schema_version
            FROM dashboards
            WHERE deleted_at IS NULL
              AND (
                name ILIKE @SearchTerm
                OR description ILIKE @SearchTerm
                OR @SearchTerm = ANY(tags)
              )
            ORDER BY updated_at DESC
            LIMIT 100";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var searchPattern = $"%{searchTerm}%";
        var rows = await connection.QueryAsync<DashboardRow>(
            new CommandDefinition(sql, new { SearchTerm = searchPattern }, cancellationToken: cancellationToken));

        return rows.Select(MapRowToDashboard).ToList();
    }

    public async Task<Guid> CreateAsync(DashboardDefinition dashboard, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO dashboards (id, name, description, owner_id, tags, definition, is_public, is_template, schema_version)
            VALUES (@Id, @Name, @Description, @OwnerId, @Tags, @Definition::jsonb, @IsPublic, @IsTemplate, @SchemaVersion)
            RETURNING id";

        dashboard.Id = dashboard.Id == Guid.Empty ? Guid.NewGuid() : dashboard.Id;
        dashboard.CreatedAt = DateTime.UtcNow;
        dashboard.UpdatedAt = DateTime.UtcNow;

        var definition = SerializeDashboardDefinition(dashboard);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var id = await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(sql, new
            {
                dashboard.Id,
                dashboard.Name,
                dashboard.Description,
                dashboard.OwnerId,
                Tags = dashboard.Tags.ToArray(),
                Definition = definition,
                dashboard.IsPublic,
                dashboard.IsTemplate,
                dashboard.SchemaVersion
            }, cancellationToken: cancellationToken));

        _logger.LogInformation("Created dashboard {DashboardId} for user {UserId}", id, dashboard.OwnerId);
        return id;
    }

    public async Task<bool> UpdateAsync(DashboardDefinition dashboard, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE dashboards
            SET name = @Name,
                description = @Description,
                tags = @Tags,
                definition = @Definition::jsonb,
                is_public = @IsPublic,
                is_template = @IsTemplate,
                updated_at = @UpdatedAt,
                schema_version = @SchemaVersion
            WHERE id = @Id AND deleted_at IS NULL";

        dashboard.UpdatedAt = DateTime.UtcNow;
        var definition = SerializeDashboardDefinition(dashboard);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                dashboard.Id,
                dashboard.Name,
                dashboard.Description,
                Tags = dashboard.Tags.ToArray(),
                Definition = definition,
                dashboard.IsPublic,
                dashboard.IsTemplate,
                dashboard.UpdatedAt,
                dashboard.SchemaVersion
            }, cancellationToken: cancellationToken));

        if (affected > 0)
        {
            _logger.LogInformation("Updated dashboard {DashboardId}", dashboard.Id);
        }

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Soft delete
        const string sql = @"
            UPDATE dashboards
            SET deleted_at = @DeletedAt
            WHERE id = @Id AND deleted_at IS NULL";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, DeletedAt = DateTime.UtcNow }, cancellationToken: cancellationToken));

        if (affected > 0)
        {
            _logger.LogInformation("Deleted dashboard {DashboardId}", id);
        }

        return affected > 0;
    }

    public async Task<bool> ShareAsync(Guid id, bool isPublic, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE dashboards
            SET is_public = @IsPublic,
                updated_at = @UpdatedAt
            WHERE id = @Id AND deleted_at IS NULL";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, IsPublic = isPublic, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<DashboardDefinition?> CloneAsync(Guid sourceId, string newOwnerId, string? newName = null,
        CancellationToken cancellationToken = default)
    {
        var source = await GetByIdAsync(sourceId, cancellationToken);
        if (source == null)
        {
            return null;
        }

        var cloned = new DashboardDefinition
        {
            Id = Guid.NewGuid(),
            Name = newName ?? $"{source.Name} (Copy)",
            Description = source.Description,
            OwnerId = newOwnerId,
            Tags = new List<string>(source.Tags),
            Layout = source.Layout,
            Widgets = source.Widgets,
            Connections = source.Connections,
            IsPublic = false,
            IsTemplate = false,
            RefreshInterval = source.RefreshInterval,
            Theme = source.Theme,
            SchemaVersion = source.SchemaVersion
        };

        await CreateAsync(cloned, cancellationToken);
        return cloned;
    }

    private DashboardDefinition MapRowToDashboard(DashboardRow row)
    {
        var dashboard = JsonSerializer.Deserialize<DashboardDefinition>(row.Definition, _jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize dashboard {row.Id}");

        // Ensure top-level properties are set correctly
        dashboard.Id = row.Id;
        dashboard.Name = row.Name;
        dashboard.Description = row.Description;
        dashboard.OwnerId = row.OwnerId;
        dashboard.Tags = row.Tags?.ToList() ?? new List<string>();
        dashboard.IsPublic = row.IsPublic;
        dashboard.IsTemplate = row.IsTemplate;
        dashboard.CreatedAt = row.CreatedAt;
        dashboard.UpdatedAt = row.UpdatedAt;
        dashboard.SchemaVersion = row.SchemaVersion;

        return dashboard;
    }

    private string SerializeDashboardDefinition(DashboardDefinition dashboard)
    {
        return JsonSerializer.Serialize(dashboard, _jsonOptions);
    }

    private class DashboardRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string[]? Tags { get; set; }
        public string Definition { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public bool IsTemplate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SchemaVersion { get; set; } = "1.0";
    }
}
