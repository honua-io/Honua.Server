// <copyright file="AlertEscalationState.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Tracks the current escalation state for an alert.
/// </summary>
public sealed class AlertEscalationState
{
    public long Id { get; set; }

    /// <summary>
    /// ID of the alert in alert_history table.
    /// </summary>
    public long AlertId { get; set; }

    /// <summary>
    /// Fingerprint of the alert for quick lookups.
    /// </summary>
    public string AlertFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// ID of the escalation policy being followed.
    /// </summary>
    public long PolicyId { get; set; }

    /// <summary>
    /// Current escalation level (0-based index).
    /// </summary>
    public int CurrentLevel { get; set; } = 0;

    /// <summary>
    /// Whether the alert has been acknowledged.
    /// </summary>
    public bool IsAcknowledged { get; set; } = false;

    /// <summary>
    /// User who acknowledged the alert.
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// When the alert was acknowledged.
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>
    /// Optional notes from the acknowledgment.
    /// </summary>
    public string? AcknowledgmentNotes { get; set; }

    /// <summary>
    /// When to escalate to the next level.
    /// NULL if at the final level or escalation is complete.
    /// </summary>
    public DateTimeOffset? NextEscalationTime { get; set; }

    /// <summary>
    /// When escalation was started for this alert.
    /// </summary>
    public DateTimeOffset EscalationStartedAt { get; set; }

    /// <summary>
    /// When escalation was completed (reached final level or acknowledged).
    /// </summary>
    public DateTimeOffset? EscalationCompletedAt { get; set; }

    /// <summary>
    /// Current status of the escalation.
    /// </summary>
    public EscalationStatus Status { get; set; } = EscalationStatus.Active;

    /// <summary>
    /// Reason for cancellation (if cancelled).
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Row version for optimistic locking.
    /// </summary>
    public int RowVersion { get; set; } = 1;
}

/// <summary>
/// Status of an alert escalation.
/// </summary>
public enum EscalationStatus
{
    /// <summary>
    /// Escalation is active and may progress to next levels.
    /// </summary>
    Active,

    /// <summary>
    /// Alert has been acknowledged and escalation stopped.
    /// </summary>
    Acknowledged,

    /// <summary>
    /// Escalation completed (reached final level).
    /// </summary>
    Completed,

    /// <summary>
    /// Escalation was cancelled (e.g., alert auto-resolved).
    /// </summary>
    Cancelled,
}
