// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models;

/// <summary>
/// Correlation between a geofence event and an alert.
/// </summary>
public sealed class GeofenceAlertCorrelation
{
    public Guid GeofenceEventId { get; set; }
    public string AlertFingerprint { get; set; } = string.Empty;
    public long? AlertHistoryId { get; set; }
    public DateTimeOffset AlertCreatedAt { get; set; }
    public string? AlertSeverity { get; set; }
    public string? AlertStatus { get; set; }
    public List<long>? NotificationChannelIds { get; set; }
    public bool WasSilenced { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Alert rule specifically for geofence events with advanced matching.
/// </summary>
public sealed class GeofenceAlertRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;

    // Geofence criteria
    public Guid? GeofenceId { get; set; }
    public string? GeofenceNamePattern { get; set; }

    // Event type filter
    public List<string>? EventTypes { get; set; }

    // Entity criteria
    public string? EntityIdPattern { get; set; }
    public string? EntityType { get; set; }

    // Dwell time thresholds
    public int? MinDwellTimeSeconds { get; set; }
    public int? MaxDwellTimeSeconds { get; set; }

    // Alert configuration
    public string AlertSeverity { get; set; } = "medium";
    public string AlertNameTemplate { get; set; } = string.Empty;
    public string? AlertDescriptionTemplate { get; set; }
    public Dictionary<string, string>? AlertLabels { get; set; }

    // Notification channels
    public List<long>? NotificationChannelIds { get; set; }

    // Silencing and deduplication
    public int? SilenceDurationMinutes { get; set; }
    public int DeduplicationWindowMinutes { get; set; } = 60;

    // Multi-tenancy
    public string? TenantId { get; set; }

    // Audit fields
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Silencing rule for geofence alerts.
/// </summary>
public sealed class GeofenceAlertSilencingRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    // Silencing criteria
    public Guid? GeofenceId { get; set; }
    public string? GeofenceNamePattern { get; set; }
    public string? EntityIdPattern { get; set; }
    public List<string>? EventTypes { get; set; }

    // Time-based silencing
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    // Recurring silencing schedule
    public RecurringSchedule? RecurringSchedule { get; set; }

    // Multi-tenancy
    public string? TenantId { get; set; }

    // Audit fields
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Recurring schedule for alert silencing (e.g., maintenance windows).
/// </summary>
public sealed class RecurringSchedule
{
    /// <summary>
    /// Days of week (0=Sunday, 6=Saturday).
    /// </summary>
    [JsonPropertyName("days")]
    public List<int>? Days { get; set; }

    /// <summary>
    /// Start hour (0-23).
    /// </summary>
    [JsonPropertyName("start_hour")]
    public int? StartHour { get; set; }

    /// <summary>
    /// End hour (0-23).
    /// </summary>
    [JsonPropertyName("end_hour")]
    public int? EndHour { get; set; }
}

/// <summary>
/// Request to create or update a geofence alert rule.
/// </summary>
public sealed class CreateGeofenceAlertRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public Guid? GeofenceId { get; set; }
    public string? GeofenceNamePattern { get; set; }
    public List<string>? EventTypes { get; set; }
    public string? EntityIdPattern { get; set; }
    public string? EntityType { get; set; }
    public int? MinDwellTimeSeconds { get; set; }
    public int? MaxDwellTimeSeconds { get; set; }
    public string AlertSeverity { get; set; } = "medium";
    public string AlertNameTemplate { get; set; } = string.Empty;
    public string? AlertDescriptionTemplate { get; set; }
    public Dictionary<string, string>? AlertLabels { get; set; }
    public List<long>? NotificationChannelIds { get; set; }
    public int? SilenceDurationMinutes { get; set; }
    public int DeduplicationWindowMinutes { get; set; } = 60;
}

/// <summary>
/// Active geofence alert view model.
/// </summary>
public sealed class ActiveGeofenceAlert
{
    public Guid GeofenceEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset EventTime { get; set; }
    public Guid GeofenceId { get; set; }
    public string GeofenceName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? DwellTimeSeconds { get; set; }
    public string AlertFingerprint { get; set; } = string.Empty;
    public string? AlertSeverity { get; set; }
    public string? AlertStatus { get; set; }
    public DateTimeOffset AlertCreatedAt { get; set; }
    public List<long>? NotificationChannelIds { get; set; }
    public bool WasSilenced { get; set; }
    public string? TenantId { get; set; }
}
