// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Services;

/// <summary>
/// Notification channel entity.
/// </summary>
public sealed class NotificationChannel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty; // sns, slack, email, webhook, etc.
    public Dictionary<string, string> Configuration { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public List<string> SeverityFilter { get; set; } = new(); // Empty = all severities
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Service for managing notification channels.
/// </summary>
public interface INotificationChannelService
{
    Task<long> CreateNotificationChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default);
    Task<NotificationChannel?> GetNotificationChannelAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationChannel>> GetNotificationChannelsAsync(CancellationToken cancellationToken = default);
    Task UpdateNotificationChannelAsync(long id, NotificationChannel channel, CancellationToken cancellationToken = default);
    Task DeleteNotificationChannelAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of notification channel service using PostgreSQL.
/// </summary>
public sealed class NotificationChannelService : INotificationChannelService
{
    private readonly IAlertConfigurationDbConnectionFactory _connectionFactory;
    private readonly ILogger<NotificationChannelService> _logger;

    private static readonly object SchemaLock = new();
    private static volatile bool _schemaInitialized;

    private const string EnsureSchemaSql = @"
CREATE TABLE IF NOT EXISTS notification_channels (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NULL,
    type TEXT NOT NULL,
    configuration JSONB NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    severity_filter JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    modified_at TIMESTAMPTZ NULL,
    created_by TEXT NULL,
    modified_by TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_notification_channels_enabled ON notification_channels(enabled);
CREATE INDEX IF NOT EXISTS idx_notification_channels_type ON notification_channels(type);
";

    public NotificationChannelService(
        IAlertConfigurationDbConnectionFactory connectionFactory,
        ILogger<NotificationChannelService> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<long> CreateNotificationChannelAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
INSERT INTO notification_channels (
    name, description, type, configuration, enabled, severity_filter,
    created_at, created_by)
VALUES (
    @Name, @Description, @Type,
    CAST(@ConfigurationJson AS jsonb),
    @Enabled,
    CAST(@SeverityFilterJson AS jsonb),
    @CreatedAt, @CreatedBy)
RETURNING id;";

        var command = new CommandDefinition(
            sql,
            new
            {
                channel.Name,
                channel.Description,
                channel.Type,
                ConfigurationJson = JsonSerializer.Serialize(channel.Configuration),
                channel.Enabled,
                SeverityFilterJson = JsonSerializer.Serialize(channel.SeverityFilter),
                CreatedAt = DateTimeOffset.UtcNow,
                channel.CreatedBy
            },
            cancellationToken: cancellationToken);

        var id = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
        _logger.LogInformation("Created notification channel {ChannelId}: {ChannelName} ({ChannelType})",
            id, channel.Name, channel.Type);
        return id;
    }

    public async Task<NotificationChannel?> GetNotificationChannelAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, name, description, type,
       configuration AS ConfigurationJson,
       enabled,
       severity_filter AS SeverityFilterJson,
       created_at, modified_at, created_by, modified_by
FROM notification_channels
WHERE id = @Id;";

        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var record = await connection.QuerySingleOrDefaultAsync<NotificationChannelRecord>(command).ConfigureAwait(false);
        return record?.ToEntity();
    }

    public async Task<IReadOnlyList<NotificationChannel>> GetNotificationChannelsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
SELECT id, name, description, type,
       configuration AS ConfigurationJson,
       enabled,
       severity_filter AS SeverityFilterJson,
       created_at, modified_at, created_by, modified_by
FROM notification_channels
ORDER BY created_at DESC;";

        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var records = await connection.QueryAsync<NotificationChannelRecord>(command).ConfigureAwait(false);
        return records.Select(r => r.ToEntity()).ToList();
    }

    public async Task UpdateNotificationChannelAsync(long id, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE notification_channels
SET name = @Name,
    description = @Description,
    configuration = CAST(@ConfigurationJson AS jsonb),
    enabled = @Enabled,
    severity_filter = CAST(@SeverityFilterJson AS jsonb),
    modified_at = @ModifiedAt,
    modified_by = @ModifiedBy
WHERE id = @Id;";

        var command = new CommandDefinition(
            sql,
            new
            {
                Id = id,
                channel.Name,
                channel.Description,
                ConfigurationJson = JsonSerializer.Serialize(channel.Configuration),
                channel.Enabled,
                SeverityFilterJson = JsonSerializer.Serialize(channel.SeverityFilter),
                ModifiedAt = DateTimeOffset.UtcNow,
                channel.ModifiedBy
            },
            cancellationToken: cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new KeyNotFoundException($"Notification channel with ID {id} not found.");
        }

        _logger.LogInformation("Updated notification channel {ChannelId}: {ChannelName}", id, channel.Name);
    }

    public async Task DeleteNotificationChannelAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = "DELETE FROM notification_channels WHERE id = @Id;";
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
        _logger.LogInformation("Deleted notification channel {ChannelId}", id);
    }

    private async Task<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionFactory.CreateConnection();
        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            throw new InvalidOperationException("Notification channel service requires DbConnection-compatible factory.");
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
            _logger.LogInformation("Notification channel schema verified.");
        }
    }

    #region Database Records

    private sealed class NotificationChannelRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public string ConfigurationJson { get; set; } = "{}";
        public bool Enabled { get; set; }
        public string SeverityFilterJson { get; set; } = "[]";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ModifiedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }

        public NotificationChannel ToEntity()
        {
            return new NotificationChannel
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Type = Type,
                Configuration = DeserializeOrDefault<Dictionary<string, string>>(ConfigurationJson) ?? new(),
                Enabled = Enabled,
                SeverityFilter = DeserializeOrDefault<List<string>>(SeverityFilterJson) ?? new(),
                CreatedAt = CreatedAt,
                ModifiedAt = ModifiedAt,
                CreatedBy = CreatedBy,
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
        catch (JsonException ex)
        {
            // Log deserialization failures to detect data corruption
            Debug.WriteLine($"Failed to deserialize {typeof(T).Name}: {ex.Message}");
            return default;
        }
    }

    #endregion
}
