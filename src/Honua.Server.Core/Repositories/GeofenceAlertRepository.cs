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
using Honua.Server.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.Repositories;

/// <summary>
/// Repository for geofence alert correlation and rule management.
/// </summary>
public interface IGeofenceAlertRepository
{
    // Correlation tracking
    Task<Guid> CreateCorrelationAsync(GeofenceAlertCorrelation correlation, CancellationToken cancellationToken = default);
    Task<GeofenceAlertCorrelation?> GetCorrelationAsync(Guid geofenceEventId, CancellationToken cancellationToken = default);
    Task UpdateCorrelationStatusAsync(Guid geofenceEventId, string status, long? alertHistoryId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActiveGeofenceAlert>> GetActiveAlertsAsync(string? tenantId = null, CancellationToken cancellationToken = default);

    // Alert rules
    Task<Guid> CreateAlertRuleAsync(GeofenceAlertRule rule, CancellationToken cancellationToken = default);
    Task<GeofenceAlertRule?> GetAlertRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeofenceAlertRule>> GetAlertRulesAsync(string? tenantId = null, bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task UpdateAlertRuleAsync(Guid id, GeofenceAlertRule rule, CancellationToken cancellationToken = default);
    Task DeleteAlertRuleAsync(Guid id, CancellationToken cancellationToken = default);

    // Silencing rules
    Task<Guid> CreateSilencingRuleAsync(GeofenceAlertSilencingRule rule, CancellationToken cancellationToken = default);
    Task<GeofenceAlertSilencingRule?> GetSilencingRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeofenceAlertSilencingRule>> GetSilencingRulesAsync(string? tenantId = null, bool enabledOnly = false, CancellationToken cancellationToken = default);
    Task UpdateSilencingRuleAsync(Guid id, GeofenceAlertSilencingRule rule, CancellationToken cancellationToken = default);
    Task DeleteSilencingRuleAsync(Guid id, CancellationToken cancellationToken = default);

    // Query operations
    Task<bool> ShouldSilenceAlertAsync(Guid geofenceId, string geofenceName, string entityId, string eventType, DateTimeOffset eventTime, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeofenceAlertRule>> FindMatchingRulesAsync(Guid geofenceId, string geofenceName, string entityId, string? entityType, string eventType, int? dwellTimeSeconds, string? tenantId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL implementation of geofence alert repository.
/// </summary>
public sealed class GeofenceAlertRepository : IGeofenceAlertRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<GeofenceAlertRepository> _logger;

    public GeofenceAlertRepository(IDbConnection connection, ILogger<GeofenceAlertRepository> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // === Correlation Tracking ===

    public async Task<Guid> CreateCorrelationAsync(GeofenceAlertCorrelation correlation, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO geofence_alert_correlation (
    geofence_event_id, alert_fingerprint, alert_history_id, alert_created_at,
    alert_severity, alert_status, notification_channel_ids, was_silenced, tenant_id, updated_at
)
VALUES (
    @GeofenceEventId, @AlertFingerprint, @AlertHistoryId, @AlertCreatedAt,
    @AlertSeverity, @AlertStatus, @NotificationChannelIdsJson::jsonb, @WasSilenced, @TenantId, @UpdatedAt
)
RETURNING geofence_event_id;";

        var parameters = new
        {
            correlation.GeofenceEventId,
            correlation.AlertFingerprint,
            correlation.AlertHistoryId,
            correlation.AlertCreatedAt,
            correlation.AlertSeverity,
            AlertStatus = correlation.AlertStatus ?? "active",
            NotificationChannelIdsJson = correlation.NotificationChannelIds != null
                ? JsonSerializer.Serialize(correlation.NotificationChannelIds)
                : null,
            correlation.WasSilenced,
            correlation.TenantId,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var id = await _connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Created geofence alert correlation for event {GeofenceEventId}", correlation.GeofenceEventId);
        return id;
    }

    public async Task<GeofenceAlertCorrelation?> GetCorrelationAsync(Guid geofenceEventId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT geofence_event_id AS GeofenceEventId, alert_fingerprint AS AlertFingerprint,
       alert_history_id AS AlertHistoryId, alert_created_at AS AlertCreatedAt,
       alert_severity AS AlertSeverity, alert_status AS AlertStatus,
       notification_channel_ids AS NotificationChannelIdsJson,
       was_silenced AS WasSilenced, tenant_id AS TenantId, updated_at AS UpdatedAt
FROM geofence_alert_correlation
WHERE geofence_event_id = @GeofenceEventId;";

        var record = await _connection.QuerySingleOrDefaultAsync<GeofenceAlertCorrelationRecord>(
            new CommandDefinition(sql, new { GeofenceEventId = geofenceEventId }, cancellationToken: cancellationToken));

        return record?.ToEntity();
    }

    public async Task UpdateCorrelationStatusAsync(Guid geofenceEventId, string status, long? alertHistoryId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE geofence_alert_correlation
SET alert_status = @Status,
    alert_history_id = COALESCE(@AlertHistoryId, alert_history_id),
    updated_at = @UpdatedAt
WHERE geofence_event_id = @GeofenceEventId;";

        var parameters = new
        {
            GeofenceEventId = geofenceEventId,
            Status = status,
            AlertHistoryId = alertHistoryId,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Updated correlation status for event {GeofenceEventId} to {Status}", geofenceEventId, status);
    }

    public async Task<IReadOnlyList<ActiveGeofenceAlert>> GetActiveAlertsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM v_active_geofence_alerts
WHERE (@TenantId IS NULL OR tenant_id = @TenantId)
ORDER BY alert_created_at DESC
LIMIT 1000;";

        var results = await _connection.QueryAsync<ActiveGeofenceAlert>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return results.ToList();
    }

    // === Alert Rules ===

    public async Task<Guid> CreateAlertRuleAsync(GeofenceAlertRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO geofence_alert_rules (
    id, name, description, enabled, geofence_id, geofence_name_pattern,
    event_types, entity_id_pattern, entity_type, min_dwell_time_seconds, max_dwell_time_seconds,
    alert_severity, alert_name_template, alert_description_template, alert_labels,
    notification_channel_ids, silence_duration_minutes, deduplication_window_minutes,
    tenant_id, created_at, created_by
)
VALUES (
    @Id, @Name, @Description, @Enabled, @GeofenceId, @GeofenceNamePattern,
    @EventTypes, @EntityIdPattern, @EntityType, @MinDwellTimeSeconds, @MaxDwellTimeSeconds,
    @AlertSeverity, @AlertNameTemplate, @AlertDescriptionTemplate, @AlertLabelsJson::jsonb,
    @NotificationChannelIdsJson::jsonb, @SilenceDurationMinutes, @DeduplicationWindowMinutes,
    @TenantId, @CreatedAt, @CreatedBy
)
RETURNING id;";

        var id = Guid.NewGuid();
        var parameters = new
        {
            Id = id,
            rule.Name,
            rule.Description,
            rule.Enabled,
            rule.GeofenceId,
            rule.GeofenceNamePattern,
            EventTypes = rule.EventTypes?.ToArray(),
            rule.EntityIdPattern,
            rule.EntityType,
            rule.MinDwellTimeSeconds,
            rule.MaxDwellTimeSeconds,
            rule.AlertSeverity,
            rule.AlertNameTemplate,
            rule.AlertDescriptionTemplate,
            AlertLabelsJson = rule.AlertLabels != null ? JsonSerializer.Serialize(rule.AlertLabels) : null,
            NotificationChannelIdsJson = rule.NotificationChannelIds != null ? JsonSerializer.Serialize(rule.NotificationChannelIds) : null,
            rule.SilenceDurationMinutes,
            rule.DeduplicationWindowMinutes,
            rule.TenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            rule.CreatedBy
        };

        await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Created geofence alert rule {RuleId}: {RuleName}", id, rule.Name);
        return id;
    }

    public async Task<GeofenceAlertRule?> GetAlertRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM geofence_alert_rules WHERE id = @Id;";

        var record = await _connection.QuerySingleOrDefaultAsync<GeofenceAlertRuleRecord>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return record?.ToEntity();
    }

    public async Task<IReadOnlyList<GeofenceAlertRule>> GetAlertRulesAsync(string? tenantId = null, bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM geofence_alert_rules
WHERE (@TenantId IS NULL OR tenant_id = @TenantId OR tenant_id IS NULL)
  AND (@EnabledOnly = false OR enabled = true)
ORDER BY created_at DESC;";

        var records = await _connection.QueryAsync<GeofenceAlertRuleRecord>(
            new CommandDefinition(sql, new { TenantId = tenantId, EnabledOnly = enabledOnly }, cancellationToken: cancellationToken));

        return records.Select(r => r.ToEntity()).ToList();
    }

    public async Task UpdateAlertRuleAsync(Guid id, GeofenceAlertRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE geofence_alert_rules
SET name = @Name, description = @Description, enabled = @Enabled,
    geofence_id = @GeofenceId, geofence_name_pattern = @GeofenceNamePattern,
    event_types = @EventTypes, entity_id_pattern = @EntityIdPattern, entity_type = @EntityType,
    min_dwell_time_seconds = @MinDwellTimeSeconds, max_dwell_time_seconds = @MaxDwellTimeSeconds,
    alert_severity = @AlertSeverity, alert_name_template = @AlertNameTemplate,
    alert_description_template = @AlertDescriptionTemplate, alert_labels = @AlertLabelsJson::jsonb,
    notification_channel_ids = @NotificationChannelIdsJson::jsonb,
    silence_duration_minutes = @SilenceDurationMinutes,
    deduplication_window_minutes = @DeduplicationWindowMinutes,
    updated_at = @UpdatedAt, updated_by = @UpdatedBy
WHERE id = @Id;";

        var parameters = new
        {
            Id = id,
            rule.Name,
            rule.Description,
            rule.Enabled,
            rule.GeofenceId,
            rule.GeofenceNamePattern,
            EventTypes = rule.EventTypes?.ToArray(),
            rule.EntityIdPattern,
            rule.EntityType,
            rule.MinDwellTimeSeconds,
            rule.MaxDwellTimeSeconds,
            rule.AlertSeverity,
            rule.AlertNameTemplate,
            rule.AlertDescriptionTemplate,
            AlertLabelsJson = rule.AlertLabels != null ? JsonSerializer.Serialize(rule.AlertLabels) : null,
            NotificationChannelIdsJson = rule.NotificationChannelIds != null ? JsonSerializer.Serialize(rule.NotificationChannelIds) : null,
            rule.SilenceDurationMinutes,
            rule.DeduplicationWindowMinutes,
            UpdatedAt = DateTimeOffset.UtcNow,
            rule.UpdatedBy
        };

        await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Updated geofence alert rule {RuleId}", id);
    }

    public async Task DeleteAlertRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM geofence_alert_rules WHERE id = @Id;";
        await _connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        _logger.LogInformation("Deleted geofence alert rule {RuleId}", id);
    }

    // === Silencing Rules ===

    public async Task<Guid> CreateSilencingRuleAsync(GeofenceAlertSilencingRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO geofence_alert_silencing (
    id, name, enabled, geofence_id, geofence_name_pattern, entity_id_pattern,
    event_types, start_time, end_time, recurring_schedule, tenant_id, created_at, created_by
)
VALUES (
    @Id, @Name, @Enabled, @GeofenceId, @GeofenceNamePattern, @EntityIdPattern,
    @EventTypes, @StartTime, @EndTime, @RecurringScheduleJson::jsonb, @TenantId, @CreatedAt, @CreatedBy
)
RETURNING id;";

        var id = Guid.NewGuid();
        var parameters = new
        {
            Id = id,
            rule.Name,
            rule.Enabled,
            rule.GeofenceId,
            rule.GeofenceNamePattern,
            rule.EntityIdPattern,
            EventTypes = rule.EventTypes?.ToArray(),
            rule.StartTime,
            rule.EndTime,
            RecurringScheduleJson = rule.RecurringSchedule != null ? JsonSerializer.Serialize(rule.RecurringSchedule) : null,
            rule.TenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            rule.CreatedBy
        };

        await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Created geofence alert silencing rule {RuleId}: {RuleName}", id, rule.Name);
        return id;
    }

    public async Task<GeofenceAlertSilencingRule?> GetSilencingRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM geofence_alert_silencing WHERE id = @Id;";
        var record = await _connection.QuerySingleOrDefaultAsync<GeofenceAlertSilencingRuleRecord>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return record?.ToEntity();
    }

    public async Task<IReadOnlyList<GeofenceAlertSilencingRule>> GetSilencingRulesAsync(string? tenantId = null, bool enabledOnly = false, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM geofence_alert_silencing
WHERE (@TenantId IS NULL OR tenant_id = @TenantId OR tenant_id IS NULL)
  AND (@EnabledOnly = false OR enabled = true)
ORDER BY created_at DESC;";

        var records = await _connection.QueryAsync<GeofenceAlertSilencingRuleRecord>(
            new CommandDefinition(sql, new { TenantId = tenantId, EnabledOnly = enabledOnly }, cancellationToken: cancellationToken));

        return records.Select(r => r.ToEntity()).ToList();
    }

    public async Task UpdateSilencingRuleAsync(Guid id, GeofenceAlertSilencingRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE geofence_alert_silencing
SET name = @Name, enabled = @Enabled, geofence_id = @GeofenceId,
    geofence_name_pattern = @GeofenceNamePattern, entity_id_pattern = @EntityIdPattern,
    event_types = @EventTypes, start_time = @StartTime, end_time = @EndTime,
    recurring_schedule = @RecurringScheduleJson::jsonb,
    updated_at = @UpdatedAt, updated_by = @UpdatedBy
WHERE id = @Id;";

        var parameters = new
        {
            Id = id,
            rule.Name,
            rule.Enabled,
            rule.GeofenceId,
            rule.GeofenceNamePattern,
            rule.EntityIdPattern,
            EventTypes = rule.EventTypes?.ToArray(),
            rule.StartTime,
            rule.EndTime,
            RecurringScheduleJson = rule.RecurringSchedule != null ? JsonSerializer.Serialize(rule.RecurringSchedule) : null,
            UpdatedAt = DateTimeOffset.UtcNow,
            rule.UpdatedBy
        };

        await _connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        _logger.LogInformation("Updated geofence alert silencing rule {RuleId}", id);
    }

    public async Task DeleteSilencingRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM geofence_alert_silencing WHERE id = @Id;";
        await _connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        _logger.LogInformation("Deleted geofence alert silencing rule {RuleId}", id);
    }

    // === Query Operations ===

    public async Task<bool> ShouldSilenceAlertAsync(Guid geofenceId, string geofenceName, string entityId, string eventType, DateTimeOffset eventTime, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT honua_should_silence_geofence_alert(@GeofenceId, @GeofenceName, @EntityId, @EventType, @EventTime, @TenantId);";

        var parameters = new
        {
            GeofenceId = geofenceId,
            GeofenceName = geofenceName,
            EntityId = entityId,
            EventType = eventType,
            EventTime = eventTime,
            TenantId = tenantId
        };

        var result = await _connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result;
    }

    public async Task<IReadOnlyList<GeofenceAlertRule>> FindMatchingRulesAsync(Guid geofenceId, string geofenceName, string entityId, string? entityType, string eventType, int? dwellTimeSeconds, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT * FROM honua_find_matching_geofence_alert_rules(
    @GeofenceId, @GeofenceName, @EntityId, @EntityType, @EventType, @DwellTimeSeconds, @TenantId
);";

        var parameters = new
        {
            GeofenceId = geofenceId,
            GeofenceName = geofenceName,
            EntityId = entityId,
            EntityType = entityType,
            EventType = eventType,
            DwellTimeSeconds = dwellTimeSeconds,
            TenantId = tenantId
        };

        var records = await _connection.QueryAsync<GeofenceAlertRuleRecord>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return records.Select(r => r.ToEntity()).ToList();
    }
}

// === Database Record DTOs ===

internal sealed class GeofenceAlertCorrelationRecord
{
    public Guid GeofenceEventId { get; set; }
    public string AlertFingerprint { get; set; } = string.Empty;
    public long? AlertHistoryId { get; set; }
    public DateTimeOffset AlertCreatedAt { get; set; }
    public string? AlertSeverity { get; set; }
    public string? AlertStatus { get; set; }
    public string? NotificationChannelIdsJson { get; set; }
    public bool WasSilenced { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public GeofenceAlertCorrelation ToEntity()
    {
        return new GeofenceAlertCorrelation
        {
            GeofenceEventId = GeofenceEventId,
            AlertFingerprint = AlertFingerprint,
            AlertHistoryId = AlertHistoryId,
            AlertCreatedAt = AlertCreatedAt,
            AlertSeverity = AlertSeverity,
            AlertStatus = AlertStatus,
            NotificationChannelIds = !string.IsNullOrEmpty(NotificationChannelIdsJson)
                ? JsonSerializer.Deserialize<List<long>>(NotificationChannelIdsJson)
                : null,
            WasSilenced = WasSilenced,
            TenantId = TenantId,
            UpdatedAt = UpdatedAt
        };
    }
}

internal sealed class GeofenceAlertRuleRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public Guid? GeofenceId { get; set; }
    public string? GeofenceNamePattern { get; set; }
    public string[]? EventTypes { get; set; }
    public string? EntityIdPattern { get; set; }
    public string? EntityType { get; set; }
    public int? MinDwellTimeSeconds { get; set; }
    public int? MaxDwellTimeSeconds { get; set; }
    public string AlertSeverity { get; set; } = string.Empty;
    public string AlertNameTemplate { get; set; } = string.Empty;
    public string? AlertDescriptionTemplate { get; set; }
    public string? AlertLabelsJson { get; set; }
    public string? NotificationChannelIdsJson { get; set; }
    public int? SilenceDurationMinutes { get; set; }
    public int DeduplicationWindowMinutes { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public GeofenceAlertRule ToEntity()
    {
        return new GeofenceAlertRule
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Enabled = Enabled,
            GeofenceId = GeofenceId,
            GeofenceNamePattern = GeofenceNamePattern,
            EventTypes = EventTypes?.ToList(),
            EntityIdPattern = EntityIdPattern,
            EntityType = EntityType,
            MinDwellTimeSeconds = MinDwellTimeSeconds,
            MaxDwellTimeSeconds = MaxDwellTimeSeconds,
            AlertSeverity = AlertSeverity,
            AlertNameTemplate = AlertNameTemplate,
            AlertDescriptionTemplate = AlertDescriptionTemplate,
            AlertLabels = !string.IsNullOrEmpty(AlertLabelsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(AlertLabelsJson)
                : null,
            NotificationChannelIds = !string.IsNullOrEmpty(NotificationChannelIdsJson)
                ? JsonSerializer.Deserialize<List<long>>(NotificationChannelIdsJson)
                : null,
            SilenceDurationMinutes = SilenceDurationMinutes,
            DeduplicationWindowMinutes = DeduplicationWindowMinutes,
            TenantId = TenantId,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            CreatedBy = CreatedBy,
            UpdatedBy = UpdatedBy
        };
    }
}

internal sealed class GeofenceAlertSilencingRuleRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Guid? GeofenceId { get; set; }
    public string? GeofenceNamePattern { get; set; }
    public string? EntityIdPattern { get; set; }
    public string[]? EventTypes { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? RecurringScheduleJson { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public GeofenceAlertSilencingRule ToEntity()
    {
        return new GeofenceAlertSilencingRule
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            GeofenceId = GeofenceId,
            GeofenceNamePattern = GeofenceNamePattern,
            EntityIdPattern = EntityIdPattern,
            EventTypes = EventTypes?.ToList(),
            StartTime = StartTime,
            EndTime = EndTime,
            RecurringSchedule = !string.IsNullOrEmpty(RecurringScheduleJson)
                ? JsonSerializer.Deserialize<RecurringSchedule>(RecurringScheduleJson)
                : null,
            TenantId = TenantId,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            CreatedBy = CreatedBy,
            UpdatedBy = UpdatedBy
        };
    }
}
