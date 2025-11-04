// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// PostgreSQL implementation of Process Registry
/// Catalog of available geoprocessing operations
/// </summary>
public class PostgresProcessRegistry : IProcessRegistry
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresProcessRegistry> _logger;
    private readonly Dictionary<string, ProcessDefinition> _cache = new();
    private DateTimeOffset _lastReload = DateTimeOffset.MinValue;

    public PostgresProcessRegistry(
        string connectionString,
        ILogger<PostgresProcessRegistry> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessDefinition?> GetProcessAsync(string processId, CancellationToken ct = default)
    {
        // Check cache first
        if (_cache.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT * FROM process_catalog
            WHERE process_id = @ProcessId AND enabled = true";

        var row = await connection.QuerySingleOrDefaultAsync(sql, new { ProcessId = processId });

        if (row == null)
            return null;

        var process = MapToProcessDefinition(row);

        // Cache it
        _cache[processId] = process;

        return process;
    }

    public async Task<IReadOnlyList<ProcessDefinition>> ListProcessesAsync(CancellationToken ct = default)
    {
        // Reload cache if stale (> 5 minutes)
        if (DateTimeOffset.UtcNow - _lastReload > TimeSpan.FromMinutes(5))
        {
            await ReloadAsync(ct);
        }

        return _cache.Values.ToList();
    }

    public async Task RegisterProcessAsync(ProcessDefinition process, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            INSERT INTO process_catalog (
                process_id, title, description, version, category, keywords,
                inputs_schema, output_schema, output_formats, execution_config,
                links, metadata, enabled, registered_at, updated_at, implementation_class
            ) VALUES (
                @ProcessId, @Title, @Description, @Version, @Category, @Keywords,
                @InputsSchema::jsonb, @OutputSchema::jsonb, @OutputFormats, @ExecutionConfig::jsonb,
                @Links::jsonb, @Metadata::jsonb, @Enabled, @RegisteredAt, @UpdatedAt, @ImplementationClass
            )
            ON CONFLICT (process_id) DO UPDATE SET
                title = EXCLUDED.title,
                description = EXCLUDED.description,
                version = EXCLUDED.version,
                category = EXCLUDED.category,
                keywords = EXCLUDED.keywords,
                inputs_schema = EXCLUDED.inputs_schema,
                output_schema = EXCLUDED.output_schema,
                output_formats = EXCLUDED.output_formats,
                execution_config = EXCLUDED.execution_config,
                links = EXCLUDED.links,
                metadata = EXCLUDED.metadata,
                enabled = EXCLUDED.enabled,
                updated_at = EXCLUDED.updated_at,
                implementation_class = EXCLUDED.implementation_class";

        await connection.ExecuteAsync(sql, new
        {
            ProcessId = process.Id,
            process.Title,
            process.Description,
            process.Version,
            process.Category,
            Keywords = process.Keywords.ToArray(),
            InputsSchema = JsonSerializer.Serialize(process.Inputs),
            OutputSchema = process.Output != null ? JsonSerializer.Serialize(process.Output) : null,
            OutputFormats = process.OutputFormats.ToArray(),
            ExecutionConfig = JsonSerializer.Serialize(process.ExecutionConfig),
            Links = JsonSerializer.Serialize(process.Links),
            Metadata = process.Metadata != null ? JsonSerializer.Serialize(process.Metadata) : null,
            process.Enabled,
            RegisteredAt = process.RegisteredAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            process.ImplementationClass
        });

        // Update cache
        _cache[process.Id] = process;

        _logger.LogInformation("Registered process {ProcessId} v{Version}", process.Id, process.Version);
    }

    public async Task UnregisterProcessAsync(string processId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = "DELETE FROM process_catalog WHERE process_id = @ProcessId";

        await connection.ExecuteAsync(sql, new { ProcessId = processId });

        // Remove from cache
        _cache.Remove(processId);

        _logger.LogInformation("Unregistered process {ProcessId}", processId);
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Reloading process catalog from database");

        await using var connection = new NpgsqlConnection(_connectionString);

        const string sql = "SELECT * FROM process_catalog WHERE enabled = true ORDER BY category, process_id";

        var rows = await connection.QueryAsync(sql);

        _cache.Clear();

        foreach (var row in rows)
        {
            var process = MapToProcessDefinition(row);
            _cache[process.Id] = process;
        }

        _lastReload = DateTimeOffset.UtcNow;

        _logger.LogInformation("Loaded {Count} processes from catalog", _cache.Count);
    }

    public Task<bool> IsAvailableAsync(string processId, CancellationToken ct = default)
    {
        return Task.FromResult(_cache.ContainsKey(processId));
    }

    private static ProcessDefinition MapToProcessDefinition(dynamic row)
    {
        return new ProcessDefinition
        {
            Id = row.process_id,
            Title = row.title,
            Description = row.description,
            Version = row.version,
            Category = row.category,
            Keywords = row.keywords != null ? new List<string>(row.keywords) : new List<string>(),
            Inputs = JsonSerializer.Deserialize<List<ProcessParameter>>(row.inputs_schema) ?? new List<ProcessParameter>(),
            Output = row.output_schema != null ? JsonSerializer.Deserialize<ProcessOutput>(row.output_schema) : null,
            OutputFormats = row.output_formats != null ? new List<string>(row.output_formats) : new List<string> { "geojson" },
            ExecutionConfig = JsonSerializer.Deserialize<ProcessExecutionConfig>(row.execution_config) ?? new ProcessExecutionConfig(),
            Links = row.links != null ? JsonSerializer.Deserialize<List<ProcessLink>>(row.links) : new List<ProcessLink>(),
            RegisteredAt = row.registered_at,
            Enabled = row.enabled,
            ImplementationClass = row.implementation_class,
            Metadata = row.metadata != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.metadata) : null
        };
    }
}
