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
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Services;

/// <summary>
/// Alert rule entity.
/// </summary>
public sealed class AlertRule
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty;
    public Dictionary<string, string> Matchers { get; set; } = new();
    public List<long> NotificationChannelIds { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Alert routing configuration entity.
/// </summary>
public sealed class AlertRoutingConfiguration
{
    public long Id { get; set; }
    public List<AlertRoutingRule> Routes { get; set; } = new();
    public AlertRoutingRule? DefaultRoute { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Alert routing rule entity.
/// </summary>
public sealed class AlertRoutingRule
{
    public string? Name { get; set; }
    public Dictionary<string, string> Matchers { get; set; } = new();
    public List<long> NotificationChannelIds { get; set; } = new();
    public bool Continue { get; set; } = false;
}

/// <summary>
/// Connection factory interface for alert configuration database.
/// </summary>
public interface IAlertConfigurationDbConnectionFactory
{
    IDbConnection CreateConnection();
}

/// <summary>
/// Service for managing alert rule configuration.
/// </summary>
public interface IAlertConfigurationService
{
    Task<long> CreateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task<AlertRule?> GetAlertRuleAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default);
    Task UpdateAlertRuleAsync(long id, AlertRule rule, CancellationToken cancellationToken = default);
    Task DeleteAlertRuleAsync(long id, CancellationToken cancellationToken = default);
    Task<AlertRoutingConfiguration?> GetRoutingConfigurationAsync(CancellationToken cancellationToken = default);
    Task UpdateRoutingConfigurationAsync(AlertRoutingConfiguration config, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of alert configuration service using PostgreSQL.
/// </summary>
public sealed class AlertConfigurationService : IAlertConfigurationService
{
    private readonly IAlertConfigurationDbConnectionFactory _connectionFactory;
    private readonly ILogger<AlertConfigurationService> _logger;

    private static readonly object SchemaLock = new();
    private static volatile bool _schemaInitialized;

    private const string EnsureSchemaSql = @"
CREATE TABLE IF NOT EXISTS alert_rules (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NULL,
    severity TEXT NOT NULL,
    matchers JSONB NOT NULL,
    notification_channel_ids JSONB NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    metadata JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_at TIMESTAMPTZ NULL,
    created_by TEXT NULL,
    modified_by TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_alert_rules_enabled ON alert_rules(enabled);
CREATE INDEX IF NOT EXISTS idx_alert_rules_severity ON alert_rules(severity);

CREATE TABLE IF NOT EXISTS alert_routing_configuration (
    id BIGSERIAL PRIMARY KEY,
    routes JSONB NOT NULL,
    default_route JSONB NULL,
    modified_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_by TEXT NULL
);
";

    public AlertConfigurationService(
        IAlertConfigurationDbConnectionFactory connectionFactory,
        ILogger<AlertConfigurationService> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<long> CreateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
INSERT INTO alert_rules (
    name, description, severity, matchers, notification_channel_ids,
    enabled, metadata, created_at, created_by)
VALUES (
    @Name, @Description, @Severity,
    CAST(@MatchersJson AS jsonb),
    CAST(@NotificationChannelIdsJson AS jsonb),
    @Enabled,
    CAST(@MetadataJson AS jsonb),
    @CreatedAt, @CreatedBy)
RETURNING id;";

        var command = new CommandDefinition(
            sql,
            new
            {
                rule.Name,
                rule.Description,
                rule.Severity,
                MatchersJson = JsonSerializer.Serialize(rule.Matchers),
                NotificationChannelIdsJson = JsonSerializer.Serialize(rule.NotificationChannelIds),
                rule.Enabled,
                MetadataJson = rule.Metadata != null ? JsonSerializer.Serialize(rule.Metadata) : null,
                CreatedAt = DateTimeOffset.UtcNow,
                rule.CreatedBy
            },
            cancellationToken: cancellationToken);

        var id = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
        _logger.LogInformation("Created alert rule {RuleId}: {RuleName}", id, rule.Name);
        return id;
    }

    public async Task<AlertRule?> GetAlertRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, name, description, severity,
       matchers AS MatchersJson,
       notification_channel_ids AS NotificationChannelIdsJson,
       enabled,
       metadata AS MetadataJson,
       created_at, modified_at, created_by, modified_by
FROM alert_rules
WHERE id = @Id;";

        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<AlertRuleRecord>(command).ConfigureAwait(false);
        return record?.ToEntity();
    }

    public async Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, name, description, severity,
       matchers AS MatchersJson,
       notification_channel_ids AS NotificationChannelIdsJson,
       enabled,
       metadata AS MetadataJson,
       created_at, modified_at, created_by, modified_by
FROM alert_rules
ORDER BY created_at DESC;";

        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var records = await connection.QueryAsync<AlertRuleRecord>(command).ConfigureAwait(false);
        return records.Select(r => r.ToEntity()).ToList();
    }

    public async Task UpdateAlertRuleAsync(long id, AlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE alert_rules
SET name = @Name,
    description = @Description,
    severity = @Severity,
    matchers = CAST(@MatchersJson AS jsonb),
    notification_channel_ids = CAST(@NotificationChannelIdsJson AS jsonb),
    enabled = @Enabled,
    metadata = CAST(@MetadataJson AS jsonb),
    modified_at = @ModifiedAt,
    modified_by = @ModifiedBy
WHERE id = @Id;";

        var command = new CommandDefinition(
            sql,
            new
            {
                Id = id,
                rule.Name,
                rule.Description,
                rule.Severity,
                MatchersJson = JsonSerializer.Serialize(rule.Matchers),
                NotificationChannelIdsJson = JsonSerializer.Serialize(rule.NotificationChannelIds),
                rule.Enabled,
                MetadataJson = rule.Metadata != null ? JsonSerializer.Serialize(rule.Metadata) : null,
                ModifiedAt = DateTimeOffset.UtcNow,
                rule.ModifiedBy
            },
            cancellationToken: cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new KeyNotFoundException($"Alert rule with ID {id} not found.");
        }

        _logger.LogInformation("Updated alert rule {RuleId}: {RuleName}", id, rule.Name);
    }

    public async Task DeleteAlertRuleAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM alert_rules WHERE id = @Id;";
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new KeyNotFoundException($"Alert rule with ID {id} not found.");
        }

        _logger.LogInformation("Deleted alert rule {RuleId}", id);
    }

    public async Task<AlertRoutingConfiguration?> GetRoutingConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, routes AS RoutesJson, default_route AS DefaultRouteJson, modified_at, modified_by
FROM alert_routing_configuration
ORDER BY id DESC
LIMIT 1;";

        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<AlertRoutingConfigurationRecord>(command).ConfigureAwait(false);
        return record?.ToEntity();
    }

    public async Task UpdateRoutingConfigurationAsync(AlertRoutingConfiguration config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Upsert pattern - insert or update
        const string sql = @"
INSERT INTO alert_routing_configuration (routes, default_route, modified_at, modified_by)
VALUES (CAST(@RoutesJson AS jsonb), CAST(@DefaultRouteJson AS jsonb), @ModifiedAt, @ModifiedBy)
ON CONFLICT (id) DO UPDATE
SET routes = EXCLUDED.routes,
    default_route = EXCLUDED.default_route,
    modified_at = EXCLUDED.modified_at,
    modified_by = EXCLUDED.modified_by
WHERE alert_routing_configuration.id = (SELECT id FROM alert_routing_configuration ORDER BY id DESC LIMIT 1);";

        var command = new CommandDefinition(
            sql,
            new
            {
                RoutesJson = JsonSerializer.Serialize(config.Routes),
                DefaultRouteJson = config.DefaultRoute != null ? JsonSerializer.Serialize(config.DefaultRoute) : null,
                ModifiedAt = DateTimeOffset.UtcNow,
                config.ModifiedBy
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
        _logger.LogInformation("Updated alert routing configuration");
    }

    private async Task<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.CreateConnection();
        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            throw new InvalidOperationException("Alert configuration service requires DbConnection-compatible factory.");
        }

        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        EnsureSchema(dbConnection);
        return dbConnection;
    }

    private void EnsureSchema(IDbConnection connection)
    {
        if (_schemaInitialized)
        {
            return;
        }

        lock (SchemaLock)
        {
            if (_schemaInitialized)
            {
                return;
            }

            connection.Execute(EnsureSchemaSql);
            _schemaInitialized = true;
            _logger.LogInformation("Alert configuration schema verified.");
        }
    }

    #region Database Records

    private sealed class AlertRuleRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string MatchersJson { get; set; } = "{}";
        public string NotificationChannelIdsJson { get; set; } = "[]";
        public bool Enabled { get; set; }
        public string? MetadataJson { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ModifiedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }

        public AlertRule ToEntity()
        {
            return new AlertRule
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Severity = Severity,
                Matchers = DeserializeOrDefault<Dictionary<string, string>>(MatchersJson) ?? new(),
                NotificationChannelIds = DeserializeOrDefault<List<long>>(NotificationChannelIdsJson) ?? new(),
                Enabled = Enabled,
                Metadata = DeserializeOrDefault<Dictionary<string, string>>(MetadataJson),
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                CreatedBy = CreatedBy,
                ModifiedBy = ModifiedBy
            };
        }
    }

    private sealed class AlertRoutingConfigurationRecord
    {
        public long Id { get; set; }
        public string RoutesJson { get; set; } = "[]";
        public string? DefaultRouteJson { get; set; }
        public DateTimeOffset ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }

        public AlertRoutingConfiguration ToEntity()
        {
            return new AlertRoutingConfiguration
            {
                Id = Id,
                Routes = DeserializeOrDefault<List<AlertRoutingRule>>(RoutesJson) ?? new(),
                DefaultRoute = DeserializeOrDefault<AlertRoutingRule>(DefaultRouteJson),
                ModifiedAt = ModifiedAt,
                ModifiedBy = ModifiedBy
            };
        }
    }

    private static T? DeserializeOrDefault<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    #endregion
}
