// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Events.CEP.Models;

/// <summary>
/// Defines a complex event pattern for geofence events
/// </summary>
public class GeofenceEventPattern
{
    /// <summary>
    /// Unique pattern identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of pattern
    /// </summary>
    public PatternType PatternType { get; set; }

    /// <summary>
    /// Whether this pattern is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pattern conditions that must be satisfied
    /// </summary>
    public List<EventCondition> Conditions { get; set; } = new();

    /// <summary>
    /// Temporal window duration in seconds
    /// </summary>
    public int WindowDurationSeconds { get; set; }

    /// <summary>
    /// Type of temporal window
    /// </summary>
    public WindowType WindowType { get; set; } = WindowType.Sliding;

    /// <summary>
    /// For session windows: inactivity gap in seconds before session ends
    /// </summary>
    public int? SessionGapSeconds { get; set; }

    /// <summary>
    /// Alert name to generate when pattern matches
    /// </summary>
    public string AlertName { get; set; } = string.Empty;

    /// <summary>
    /// Alert severity
    /// </summary>
    public string AlertSeverity { get; set; } = "medium";

    /// <summary>
    /// Alert description (template with placeholders)
    /// </summary>
    public string? AlertDescription { get; set; }

    /// <summary>
    /// Additional labels to attach to generated alerts
    /// </summary>
    public Dictionary<string, string>? AlertLabels { get; set; }

    /// <summary>
    /// Notification channel IDs to use for alerts
    /// </summary>
    public List<string>? NotificationChannelIds { get; set; }

    /// <summary>
    /// Pattern priority (higher = evaluated first)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated timestamp
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Created by user
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Updated by user
    /// </summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Type of CEP pattern
/// </summary>
public enum PatternType
{
    /// <summary>
    /// Sequence pattern: ordered events (A then B within time window)
    /// </summary>
    Sequence,

    /// <summary>
    /// Count pattern: N occurrences within window
    /// </summary>
    Count,

    /// <summary>
    /// Correlation pattern: multiple entities performing same action
    /// </summary>
    Correlation,

    /// <summary>
    /// Absence pattern: expected event didn't occur
    /// </summary>
    Absence
}

/// <summary>
/// Type of temporal window
/// </summary>
public enum WindowType
{
    /// <summary>
    /// Sliding window: continuous window that slides with each event
    /// </summary>
    Sliding,

    /// <summary>
    /// Tumbling window: fixed, non-overlapping windows (e.g., every hour on the hour)
    /// </summary>
    Tumbling,

    /// <summary>
    /// Session window: activity-based windows with gap timeout
    /// </summary>
    Session
}

/// <summary>
/// Condition within a pattern
/// </summary>
public class EventCondition
{
    /// <summary>
    /// Unique identifier for this condition (for sequencing)
    /// </summary>
    public string? ConditionId { get; set; }

    /// <summary>
    /// Event type to match (Enter, Exit, Dwell, Approach)
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Specific geofence ID to match (optional)
    /// </summary>
    public Guid? GeofenceId { get; set; }

    /// <summary>
    /// Regex pattern to match geofence name (optional)
    /// </summary>
    public string? GeofenceNamePattern { get; set; }

    /// <summary>
    /// Specific entity ID to match (optional)
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Regex pattern to match entity ID (optional)
    /// </summary>
    public string? EntityIdPattern { get; set; }

    /// <summary>
    /// Entity type to match (optional)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Minimum number of occurrences (for count patterns)
    /// </summary>
    public int? MinOccurrences { get; set; }

    /// <summary>
    /// Maximum number of occurrences (for count patterns)
    /// </summary>
    public int? MaxOccurrences { get; set; }

    /// <summary>
    /// Minimum dwell time in seconds
    /// </summary>
    public int? MinDwellTimeSeconds { get; set; }

    /// <summary>
    /// Maximum dwell time in seconds
    /// </summary>
    public int? MaxDwellTimeSeconds { get; set; }

    /// <summary>
    /// For sequence patterns: ID of previous condition that must be satisfied first
    /// </summary>
    public string? PreviousConditionId { get; set; }

    /// <summary>
    /// For sequence patterns: maximum time in seconds since previous condition
    /// </summary>
    public int? MaxTimeSincePreviousSeconds { get; set; }

    /// <summary>
    /// For correlation patterns: require unique entities
    /// </summary>
    public bool UniqueEntities { get; set; } = false;

    /// <summary>
    /// For absence patterns: this event should NOT occur (negative condition)
    /// </summary>
    public bool Expected { get; set; } = true;

    /// <summary>
    /// Custom properties to match (JSONB query)
    /// </summary>
    public Dictionary<string, object>? PropertyMatchers { get; set; }
}
