// <copyright file="AlertEscalationStore.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Data;
using System.Text.Json;
using Dapper;
using Honua.Server.AlertReceiver.Data;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Data access layer for alert escalation functionality.
/// </summary>
public interface IAlertEscalationStore
{
    Task<long> InsertEscalationStateAsync(AlertEscalationState state, CancellationToken cancellationToken = default);

    Task<bool> UpdateEscalationStateAsync(AlertEscalationState state, CancellationToken cancellationToken = default);

    Task<AlertEscalationState?> GetEscalationStateByAlertIdAsync(long alertId, CancellationToken cancellationToken = default);

    Task<AlertEscalationState?> GetEscalationStateByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEscalationState>> GetPendingEscalationsAsync(int limit, CancellationToken cancellationToken = default);

    Task InsertEscalationEventAsync(AlertEscalationEvent escalationEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEscalationEvent>> GetEscalationEventsAsync(long escalationStateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEscalationPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default);

    Task<AlertEscalationPolicy?> GetPolicyByIdAsync(long policyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEscalationSuppression>> GetActiveSuppressionWindowsAsync(CancellationToken cancellationToken = default);

    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}

public sealed class AlertEscalationStore : IAlertEscalationStore
{
    private readonly IAlertReceiverDbConnectionFactory connectionFactory;
    private readonly ILogger<AlertEscalationStore> logger;

    private static readonly object SchemaLock = new();
    private static volatile bool schemaInitialized;

    public AlertEscalationStore(
        IAlertReceiverDbConnectionFactory connectionFactory,
        ILogger<AlertEscalationStore> logger)
    {
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
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

            try
            {
                using var connection = this.connectionFactory.CreateConnection();
                connection.Open();

                // Read and execute migration script
                var migrationPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Migrations",
                    "003_alert_escalation.sql");

                if (File.Exists(migrationPath))
                {
                    var sql = File.ReadAllText(migrationPath);
                    connection.Execute(sql);
                    this.logger.LogInformation("Alert escalation schema initialized successfully");
                }
                else
                {
                    this.logger.LogWarning("Migration file not found: {Path}", migrationPath);
                }

                schemaInitialized = true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize alert escalation schema");
                throw;
            }
        }
    }

    public async Task<long> InsertEscalationStateAsync(AlertEscalationState state, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO alert_escalation_state (
    alert_id,
    alert_fingerprint,
    policy_id,
    current_level,
    is_acknowledged,
    acknowledged_by,
    acknowledged_at,
    acknowledgment_notes,
    next_escalation_time,
    escalation_started_at,
    escalation_completed_at,
    status,
    cancellation_reason,
    row_version
) VALUES (
    @AlertId,
    @AlertFingerprint,
    @PolicyId,
    @CurrentLevel,
    @IsAcknowledged,
    @AcknowledgedBy,
    @AcknowledgedAt,
    @AcknowledgmentNotes,
    @NextEscalationTime,
    @EscalationStartedAt,
    @EscalationCompletedAt,
    @Status,
    @CancellationReason,
    @RowVersion
) RETURNING id;";

        using var connection = this.connectionFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<long>(
            sql,
            new
            {
                state.AlertId,
                state.AlertFingerprint,
                state.PolicyId,
                state.CurrentLevel,
                state.IsAcknowledged,
                state.AcknowledgedBy,
                state.AcknowledgedAt,
                state.AcknowledgmentNotes,
                state.NextEscalationTime,
                state.EscalationStartedAt,
                state.EscalationCompletedAt,
                Status = state.Status.ToString().ToLowerInvariant(),
                state.CancellationReason,
                state.RowVersion,
            });

        return id;
    }

    public async Task<bool> UpdateEscalationStateAsync(AlertEscalationState state, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE alert_escalation_state
SET
    current_level = @CurrentLevel,
    is_acknowledged = @IsAcknowledged,
    acknowledged_by = @AcknowledgedBy,
    acknowledged_at = @AcknowledgedAt,
    acknowledgment_notes = @AcknowledgmentNotes,
    next_escalation_time = @NextEscalationTime,
    escalation_completed_at = @EscalationCompletedAt,
    status = @Status,
    cancellation_reason = @CancellationReason,
    row_version = @NewRowVersion
WHERE id = @Id
  AND row_version = @RowVersion;";

        using var connection = this.connectionFactory.CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                state.Id,
                state.CurrentLevel,
                state.IsAcknowledged,
                state.AcknowledgedBy,
                state.AcknowledgedAt,
                state.AcknowledgmentNotes,
                state.NextEscalationTime,
                state.EscalationCompletedAt,
                Status = state.Status.ToString().ToLowerInvariant(),
                state.CancellationReason,
                state.RowVersion,
                NewRowVersion = state.RowVersion + 1,
            });

        if (rowsAffected > 0)
        {
            state.RowVersion++;
            return true;
        }

        return false; // Optimistic locking conflict
    }

    public async Task<AlertEscalationState?> GetEscalationStateByAlertIdAsync(long alertId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_state
WHERE alert_id = @AlertId
  AND status = 'active'
ORDER BY id DESC
LIMIT 1;";

        using var connection = this.connectionFactory.CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<AlertEscalationStateRecord>(sql, new { AlertId = alertId });
        return record != null ? MapFromRecord(record) : null;
    }

    public async Task<AlertEscalationState?> GetEscalationStateByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_state
WHERE alert_fingerprint = @Fingerprint
ORDER BY id DESC
LIMIT 1;";

        using var connection = this.connectionFactory.CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<AlertEscalationStateRecord>(sql, new { Fingerprint = fingerprint });
        return record != null ? MapFromRecord(record) : null;
    }

    public async Task<IReadOnlyList<AlertEscalationState>> GetPendingEscalationsAsync(int limit, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_state
WHERE status = 'active'
  AND next_escalation_time IS NOT NULL
  AND next_escalation_time <= @Now
ORDER BY next_escalation_time ASC
LIMIT @Limit;";

        using var connection = this.connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AlertEscalationStateRecord>(
            sql,
            new { Now = DateTimeOffset.UtcNow, Limit = limit });

        return records.Select(MapFromRecord).ToList();
    }

    public async Task InsertEscalationEventAsync(AlertEscalationEvent escalationEvent, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO alert_escalation_events (
    escalation_state_id,
    event_type,
    escalation_level,
    notification_channels,
    severity_override,
    event_timestamp,
    event_details
) VALUES (
    @EscalationStateId,
    @EventType,
    @EscalationLevel,
    @NotificationChannels,
    @SeverityOverride,
    @EventTimestamp,
    CAST(@EventDetailsJson AS jsonb)
);";

        using var connection = this.connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            sql,
            new
            {
                escalationEvent.EscalationStateId,
                EventType = escalationEvent.EventType.ToString().ToLowerInvariant(),
                escalationEvent.EscalationLevel,
                NotificationChannels = escalationEvent.NotificationChannels?.ToArray(),
                escalationEvent.SeverityOverride,
                escalationEvent.EventTimestamp,
                EventDetailsJson = escalationEvent.EventDetails != null
                    ? JsonSerializer.Serialize(escalationEvent.EventDetails)
                    : null,
            });
    }

    public async Task<IReadOnlyList<AlertEscalationEvent>> GetEscalationEventsAsync(long escalationStateId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_events
WHERE escalation_state_id = @EscalationStateId
ORDER BY event_timestamp DESC;";

        using var connection = this.connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AlertEscalationEventRecord>(sql, new { EscalationStateId = escalationStateId });
        return records.Select(MapFromRecord).ToList();
    }

    public async Task<IReadOnlyList<AlertEscalationPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_policies
WHERE is_active = true
ORDER BY name;";

        using var connection = this.connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AlertEscalationPolicyRecord>(sql);
        return records.Select(MapFromRecord).ToList();
    }

    public async Task<AlertEscalationPolicy?> GetPolicyByIdAsync(long policyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_policies
WHERE id = @PolicyId;";

        using var connection = this.connectionFactory.CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<AlertEscalationPolicyRecord>(sql, new { PolicyId = policyId });
        return record != null ? MapFromRecord(record) : null;
    }

    public async Task<IReadOnlyList<AlertEscalationSuppression>> GetActiveSuppressionWindowsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM alert_escalation_suppressions
WHERE is_active = true
  AND starts_at <= @Now
  AND ends_at > @Now
ORDER BY starts_at;";

        using var connection = this.connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AlertEscalationSuppressionRecord>(
            sql,
            new { Now = DateTimeOffset.UtcNow });
        return records.Select(MapFromRecord).ToList();
    }

    private static AlertEscalationState MapFromRecord(AlertEscalationStateRecord record)
    {
        return new AlertEscalationState
        {
            Id = record.Id,
            AlertId = record.AlertId,
            AlertFingerprint = record.AlertFingerprint,
            PolicyId = record.PolicyId,
            CurrentLevel = record.CurrentLevel,
            IsAcknowledged = record.IsAcknowledged,
            AcknowledgedBy = record.AcknowledgedBy,
            AcknowledgedAt = record.AcknowledgedAt,
            AcknowledgmentNotes = record.AcknowledgmentNotes,
            NextEscalationTime = record.NextEscalationTime,
            EscalationStartedAt = record.EscalationStartedAt,
            EscalationCompletedAt = record.EscalationCompletedAt,
            Status = Enum.Parse<EscalationStatus>(record.Status, ignoreCase: true),
            CancellationReason = record.CancellationReason,
            RowVersion = record.RowVersion,
        };
    }

    private static AlertEscalationEvent MapFromRecord(AlertEscalationEventRecord record)
    {
        return new AlertEscalationEvent
        {
            Id = record.Id,
            EscalationStateId = record.EscalationStateId,
            EventType = Enum.Parse<EscalationEventType>(record.EventType, ignoreCase: true),
            EscalationLevel = record.EscalationLevel,
            NotificationChannels = record.NotificationChannels?.ToList(),
            SeverityOverride = record.SeverityOverride,
            EventTimestamp = record.EventTimestamp,
            EventDetails = !string.IsNullOrWhiteSpace(record.EventDetailsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(record.EventDetailsJson)
                : null,
        };
    }

    private static AlertEscalationPolicy MapFromRecord(AlertEscalationPolicyRecord record)
    {
        return new AlertEscalationPolicy
        {
            Id = record.Id,
            Name = record.Name,
            Description = record.Description,
            AppliesToPatterns = !string.IsNullOrWhiteSpace(record.AppliesToPatternsJson)
                ? JsonSerializer.Deserialize<List<string>>(record.AppliesToPatternsJson)
                : null,
            AppliesToSeverities = record.AppliesToSeverities?.ToList(),
            EscalationLevels = JsonSerializer.Deserialize<List<EscalationLevel>>(record.EscalationLevelsJson) ?? new List<EscalationLevel>(),
            RequiresAcknowledgment = record.RequiresAcknowledgment,
            IsActive = record.IsActive,
            TenantId = record.TenantId,
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }

    private static AlertEscalationSuppression MapFromRecord(AlertEscalationSuppressionRecord record)
    {
        return new AlertEscalationSuppression
        {
            Id = record.Id,
            Name = record.Name,
            Reason = record.Reason,
            AppliesToPatterns = !string.IsNullOrWhiteSpace(record.AppliesToPatternsJson)
                ? JsonSerializer.Deserialize<List<string>>(record.AppliesToPatternsJson)
                : null,
            AppliesToSeverities = record.AppliesToSeverities?.ToList(),
            StartsAt = record.StartsAt,
            EndsAt = record.EndsAt,
            IsActive = record.IsActive,
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAt,
        };
    }

    // Internal database record types
    private sealed class AlertEscalationStateRecord
    {
        public long Id { get; set; }
        public long AlertId { get; set; }
        public string AlertFingerprint { get; set; } = string.Empty;
        public long PolicyId { get; set; }
        public int CurrentLevel { get; set; }
        public bool IsAcknowledged { get; set; }
        public string? AcknowledgedBy { get; set; }
        public DateTimeOffset? AcknowledgedAt { get; set; }
        public string? AcknowledgmentNotes { get; set; }
        public DateTimeOffset? NextEscalationTime { get; set; }
        public DateTimeOffset EscalationStartedAt { get; set; }
        public DateTimeOffset? EscalationCompletedAt { get; set; }
        public string Status { get; set; } = "active";
        public string? CancellationReason { get; set; }
        public int RowVersion { get; set; }
    }

    private sealed class AlertEscalationEventRecord
    {
        public long Id { get; set; }
        public long EscalationStateId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int? EscalationLevel { get; set; }
        public string[]? NotificationChannels { get; set; }
        public string? SeverityOverride { get; set; }
        public DateTimeOffset EventTimestamp { get; set; }
        public string? EventDetailsJson { get; set; }
    }

    private sealed class AlertEscalationPolicyRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AppliesToPatternsJson { get; set; }
        public string[]? AppliesToSeverities { get; set; }
        public string EscalationLevelsJson { get; set; } = "[]";
        public bool RequiresAcknowledgment { get; set; }
        public bool IsActive { get; set; }
        public string? TenantId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class AlertEscalationSuppressionRecord
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? AppliesToPatternsJson { get; set; }
        public string[]? AppliesToSeverities { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
