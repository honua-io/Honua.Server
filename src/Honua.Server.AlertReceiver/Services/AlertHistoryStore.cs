// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data;
using System.Linq;
using Dapper;
using Honua.Server.AlertReceiver.Data;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

public interface IAlertHistoryStore
{
    Task<long> InsertAlertAsync(AlertHistoryEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertHistoryEntry>> GetRecentAlertsAsync(int limit, string? severity, CancellationToken cancellationToken = default);
    Task<AlertHistoryEntry?> GetAlertByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task InsertAcknowledgementAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken = default);
    Task<AlertAcknowledgement?> GetLatestAcknowledgementAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task<long> InsertSilencingRuleAsync(AlertSilencingRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertSilencingRule>> GetActiveSilencingRulesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
    Task DeactivateSilencingRuleAsync(long ruleId, CancellationToken cancellationToken = default);
    Task CheckConnectivityAsync(CancellationToken cancellationToken = default);
}

public sealed class AlertHistoryStore : IAlertHistoryStore
{
    private readonly IAlertReceiverDbConnectionFactory _connectionFactory;
    private readonly ILogger<AlertHistoryStore> _logger;

    private static readonly object SchemaLock = new();
    private static volatile bool schemaInitialized;

    private const string EnsureSchemaSql = @"
CREATE TABLE IF NOT EXISTS alert_history (
    id BIGSERIAL PRIMARY KEY,
    fingerprint TEXT NOT NULL,
    name TEXT NOT NULL,
    severity TEXT NOT NULL,
    status TEXT NOT NULL,
    summary TEXT NULL,
    description TEXT NULL,
    source TEXT NOT NULL,
    service TEXT NULL,
    environment TEXT NULL,
    labels JSONB NULL,
    context JSONB NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    published_to JSONB NOT NULL,
    was_suppressed BOOLEAN NOT NULL,
    suppression_reason TEXT NULL
);
CREATE INDEX IF NOT EXISTS idx_alert_history_fingerprint ON alert_history(fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_history_timestamp ON alert_history(timestamp);
CREATE INDEX IF NOT EXISTS idx_alert_history_severity_timestamp ON alert_history(severity, timestamp);

CREATE TABLE IF NOT EXISTS alert_acknowledgements (
    id BIGSERIAL PRIMARY KEY,
    fingerprint TEXT NOT NULL,
    acknowledged_by TEXT NOT NULL,
    acknowledged_at TIMESTAMPTZ NOT NULL,
    comment TEXT NULL,
    expires_at TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS idx_alert_acknowledgements_fingerprint ON alert_acknowledgements(fingerprint);
CREATE INDEX IF NOT EXISTS idx_alert_acknowledgements_acknowledged_at ON alert_acknowledgements(acknowledged_at);

CREATE TABLE IF NOT EXISTS alert_silencing_rules (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    matchers JSONB NOT NULL,
    created_by TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    comment TEXT NULL,
    is_active BOOLEAN NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_alert_silencing_rules_active ON alert_silencing_rules(is_active);
CREATE INDEX IF NOT EXISTS idx_alert_silencing_rules_active_range ON alert_silencing_rules(is_active, ends_at);
";

    private const string InsertAlertSql = @"
INSERT INTO alert_history (
    fingerprint,
    name,
    severity,
    status,
    summary,
    description,
    source,
    service,
    environment,
    labels,
    context,
    timestamp,
    published_to,
    was_suppressed,
    suppression_reason)
VALUES (
    @Fingerprint,
    @Name,
    @Severity,
    @Status,
    @Summary,
    @Description,
    @Source,
    @Service,
    @Environment,
    CAST(@LabelsJson AS jsonb),
    CAST(@ContextJson AS jsonb),
    @Timestamp,
    CAST(@PublishedToJson AS jsonb),
    @WasSuppressed,
    @SuppressionReason)
RETURNING id;";

    private const string SelectRecentAlertsSql = @"
SELECT id,
       fingerprint,
       name,
       severity,
       status,
       summary,
       description,
       source,
       service,
       environment,
       labels AS LabelsJson,
       context AS ContextJson,
       timestamp,
       published_to AS PublishedToJson,
       was_suppressed,
       suppression_reason
FROM alert_history
WHERE (@Severity IS NULL OR severity = @Severity)
ORDER BY timestamp DESC
LIMIT @Limit;";

    private const string SelectAlertByFingerprintSql = @"
SELECT id,
       fingerprint,
       name,
       severity,
       status,
       summary,
       description,
       source,
       service,
       environment,
       labels AS LabelsJson,
       context AS ContextJson,
       timestamp,
       published_to AS PublishedToJson,
       was_suppressed,
       suppression_reason
FROM alert_history
WHERE fingerprint = @Fingerprint
ORDER BY timestamp DESC
LIMIT 1;";

    private const string InsertAcknowledgementSql = @"
INSERT INTO alert_acknowledgements (
    fingerprint,
    acknowledged_by,
    acknowledged_at,
    comment,
    expires_at)
VALUES (
    @Fingerprint,
    @AcknowledgedBy,
    @AcknowledgedAt,
    @Comment,
    @ExpiresAt);";

    private const string SelectLatestAcknowledgementSql = @"
SELECT id,
       fingerprint,
       acknowledged_by AS AcknowledgedBy,
       acknowledged_at AS AcknowledgedAt,
       comment,
       expires_at AS ExpiresAt
FROM alert_acknowledgements
WHERE fingerprint = @Fingerprint
ORDER BY acknowledged_at DESC
LIMIT 1;";

    private const string InsertSilencingRuleSql = @"
INSERT INTO alert_silencing_rules (
    name,
    matchers,
    created_by,
    created_at,
    starts_at,
    ends_at,
    comment,
    is_active)
VALUES (
    @Name,
    CAST(@MatchersJson AS jsonb),
    @CreatedBy,
    @CreatedAt,
    @StartsAt,
    @EndsAt,
    @Comment,
    @IsActive)
RETURNING id;";

    private const string SelectActiveSilencingRulesSql = @"
SELECT id,
       name,
       matchers AS MatchersJson,
       created_by AS CreatedBy,
       created_at AS CreatedAt,
       starts_at AS StartsAt,
       ends_at AS EndsAt,
       comment,
       is_active AS IsActive
FROM alert_silencing_rules
WHERE is_active = TRUE
  AND ends_at > @Now;";

    private const string DeactivateSilencingRuleSql = @"
UPDATE alert_silencing_rules
SET is_active = FALSE
WHERE id = @RuleId;";

    public AlertHistoryStore(IAlertReceiverDbConnectionFactory connectionFactory, ILogger<AlertHistoryStore> logger)
    {
        this._connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<long> InsertAlertAsync(AlertHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var record = entry.ToRecord();
        var command = new CommandDefinition(
            InsertAlertSql,
            new
            {
                record.Fingerprint,
                record.Name,
                record.Severity,
                record.Status,
                record.Summary,
                record.Description,
                record.Source,
                record.Service,
                record.Environment,
                record.LabelsJson,
                record.ContextJson,
                record.Timestamp,
                record.PublishedToJson,
                record.WasSuppressed,
                record.SuppressionReason
            },
            cancellationToken: cancellationToken);

        var id = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
        entry.Id = id;
        return id;
    }

    public async Task<IReadOnlyList<AlertHistoryEntry>> GetRecentAlertsAsync(int limit, string? severity, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            SelectRecentAlertsSql,
            new { Limit = limit, Severity = severity },
            cancellationToken: cancellationToken);

        var records = await connection.QueryAsync<AlertHistoryRecord>(command).ConfigureAwait(false);
        return records.Select(AlertHistoryEntry.FromRecord).ToList();
    }

    public async Task<AlertHistoryEntry?> GetAlertByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            SelectAlertByFingerprintSql,
            new { Fingerprint = fingerprint },
            cancellationToken: cancellationToken);

        var record = await connection.QuerySingleOrDefaultAsync<AlertHistoryRecord>(command).ConfigureAwait(false);
        return record is null ? null : AlertHistoryEntry.FromRecord(record);
    }

    public async Task InsertAcknowledgementAsync(AlertAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            InsertAcknowledgementSql,
            new
            {
                acknowledgement.Fingerprint,
                acknowledgement.AcknowledgedBy,
                acknowledgement.AcknowledgedAt,
                acknowledgement.Comment,
                acknowledgement.ExpiresAt
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task<AlertAcknowledgement?> GetLatestAcknowledgementAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            SelectLatestAcknowledgementSql,
            new { Fingerprint = fingerprint },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<AlertAcknowledgement>(command).ConfigureAwait(false);
    }

    public async Task<long> InsertSilencingRuleAsync(AlertSilencingRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var record = rule.ToRecord();
        var command = new CommandDefinition(
            InsertSilencingRuleSql,
            new
            {
                record.Name,
                record.MatchersJson,
                record.CreatedBy,
                record.CreatedAt,
                record.StartsAt,
                record.EndsAt,
                record.Comment,
                record.IsActive
            },
            cancellationToken: cancellationToken);

        var id = await connection.ExecuteScalarAsync<long>(command).ConfigureAwait(false);
        rule.Id = id;
        return id;
    }

    public async Task<IReadOnlyList<AlertSilencingRule>> GetActiveSilencingRulesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            SelectActiveSilencingRulesSql,
            new { Now = now },
            cancellationToken: cancellationToken);

        var records = await connection.QueryAsync<AlertSilencingRuleRecord>(command).ConfigureAwait(false);
        return records.Select(AlertSilencingRule.FromRecord).ToList();
    }

    public async Task DeactivateSilencingRuleAsync(long ruleId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            DeactivateSilencingRuleSql,
            new { RuleId = ruleId },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task CheckConnectivityAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken);
        await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
    }

    private async Task<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = this._connectionFactory.CreateConnection();
        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            throw new InvalidOperationException("Alert history store requires DbConnection-compatible factory.");
        }

        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        this.EnsureSchema(dbConnection);
        return dbConnection;
    }

    private void EnsureSchema(IDbConnection connection)
    {
        if (schemaInitialized)
        {
            return;
        }

        lock (SchemaLock)
        {
            if (schemaInitialized)
            {
                return;
            }

            connection.Execute(EnsureSchemaSql);
            schemaInitialized = true;
            this._logger.LogInformation("Alert history schema verified.");
        }
    }
}
