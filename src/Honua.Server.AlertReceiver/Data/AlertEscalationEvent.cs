// <copyright file="AlertEscalationEvent.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Represents an event in the alert escalation lifecycle.
/// Provides an audit trail for compliance and debugging.
/// </summary>
public sealed class AlertEscalationEvent
{
    public long Id { get; set; }

    /// <summary>
    /// ID of the escalation state this event belongs to.
    /// </summary>
    public long EscalationStateId { get; set; }

    /// <summary>
    /// Type of escalation event.
    /// </summary>
    public EscalationEventType EventType { get; set; }

    /// <summary>
    /// Escalation level this event occurred at.
    /// </summary>
    public int? EscalationLevel { get; set; }

    /// <summary>
    /// Notification channels used at this level.
    /// </summary>
    public List<string>? NotificationChannels { get; set; }

    /// <summary>
    /// Severity override applied at this level.
    /// </summary>
    public string? SeverityOverride { get; set; }

    /// <summary>
    /// When this event occurred.
    /// </summary>
    public DateTimeOffset EventTimestamp { get; set; }

    /// <summary>
    /// Additional event details (JSON).
    /// Can include delivery results, user info, etc.
    /// </summary>
    public Dictionary<string, object?>? EventDetails { get; set; }
}

/// <summary>
/// Types of escalation events.
/// </summary>
public enum EscalationEventType
{
    /// <summary>
    /// Escalation was started for an alert.
    /// </summary>
    Started,

    /// <summary>
    /// Alert was escalated to the next level.
    /// </summary>
    Escalated,

    /// <summary>
    /// Alert was acknowledged by a user.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Escalation was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Escalation reached the final level.
    /// </summary>
    Completed,

    /// <summary>
    /// Escalation was suppressed (e.g., during maintenance window).
    /// </summary>
    Suppressed,
}
