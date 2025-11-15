// <copyright file="AlertEscalationService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Core service for managing alert escalations.
/// </summary>
public interface IAlertEscalationService
{
    Task<long?> StartEscalationAsync(AlertHistoryEntry alert, CancellationToken cancellationToken = default);

    Task AcknowledgeAlertAsync(long alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default);

    Task ProcessPendingEscalationsAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    Task<AlertEscalationState?> GetEscalationStatusAsync(long alertId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertEscalationEvent>> GetEscalationHistoryAsync(long alertId, CancellationToken cancellationToken = default);

    Task CancelEscalationAsync(long alertId, string reason, CancellationToken cancellationToken = default);
}

public sealed class AlertEscalationService : IAlertEscalationService
{
    private readonly IAlertEscalationStore escalationStore;
    private readonly IAlertHistoryStore alertHistoryStore;
    private readonly IAlertPublisher alertPublisher;
    private readonly ILogger<AlertEscalationService> logger;

    public AlertEscalationService(
        IAlertEscalationStore escalationStore,
        IAlertHistoryStore alertHistoryStore,
        IAlertPublisher alertPublisher,
        ILogger<AlertEscalationService> logger)
    {
        this.escalationStore = escalationStore;
        this.alertHistoryStore = alertHistoryStore;
        this.alertPublisher = alertPublisher;
        this.logger = logger;
    }

    public async Task<long?> StartEscalationAsync(AlertHistoryEntry alert, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if escalation is already active for this alert
            var existingEscalation = await this.escalationStore.GetEscalationStateByAlertIdAsync(alert.Id, cancellationToken);
            if (existingEscalation != null)
            {
                this.logger.LogDebug("Escalation already active for alert {AlertId}", alert.Id);
                return null;
            }

            // Find applicable policy
            var policies = await this.escalationStore.GetActivePoliciesAsync(cancellationToken);
            var applicablePolicy = policies.FirstOrDefault(p => p.AppliesTo(alert.Name, alert.Severity));

            if (applicablePolicy == null)
            {
                this.logger.LogDebug("No escalation policy applies to alert {AlertName} with severity {Severity}", alert.Name, alert.Severity);
                return null;
            }

            if (applicablePolicy.EscalationLevels.Count == 0)
            {
                this.logger.LogWarning("Escalation policy {PolicyName} has no levels configured", applicablePolicy.Name);
                return null;
            }

            // Check for suppression windows
            if (await this.IsEscalationSuppressedAsync(alert.Name, alert.Severity, cancellationToken))
            {
                this.logger.LogInformation("Escalation suppressed for alert {AlertName} due to active maintenance window", alert.Name);
                return null;
            }

            // Create escalation state
            var firstLevel = applicablePolicy.EscalationLevels[0];
            var state = new AlertEscalationState
            {
                AlertId = alert.Id,
                AlertFingerprint = alert.Fingerprint,
                PolicyId = applicablePolicy.Id,
                CurrentLevel = 0,
                NextEscalationTime = applicablePolicy.EscalationLevels.Count > 1
                    ? DateTimeOffset.UtcNow.Add(applicablePolicy.EscalationLevels[1].Delay)
                    : null,
                EscalationStartedAt = DateTimeOffset.UtcNow,
                Status = EscalationStatus.Active,
            };

            var escalationId = await this.escalationStore.InsertEscalationStateAsync(state, cancellationToken);
            state.Id = escalationId;

            // Record escalation started event
            await this.RecordEventAsync(
                escalationId,
                EscalationEventType.Started,
                0,
                firstLevel.NotificationChannels,
                firstLevel.SeverityOverride,
                new Dictionary<string, object?>
                {
                    ["policy_name"] = applicablePolicy.Name,
                    ["alert_name"] = alert.Name,
                    ["original_severity"] = alert.Severity,
                },
                cancellationToken);

            // Send initial notifications (level 0)
            await this.SendEscalationNotificationsAsync(alert, firstLevel, 0, cancellationToken);

            this.logger.LogInformation(
                "Started escalation for alert {AlertId} ({AlertName}) using policy {PolicyName}",
                alert.Id,
                alert.Name,
                applicablePolicy.Name);

            return escalationId;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to start escalation for alert {AlertId}", alert.Id);
            throw;
        }
    }

    public async Task AcknowledgeAlertAsync(long alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await this.escalationStore.GetEscalationStateByAlertIdAsync(alertId, cancellationToken);
            if (state == null)
            {
                this.logger.LogWarning("No active escalation found for alert {AlertId}", alertId);
                return;
            }

            if (state.IsAcknowledged)
            {
                this.logger.LogDebug("Alert {AlertId} already acknowledged", alertId);
                return;
            }

            // Update escalation state
            state.IsAcknowledged = true;
            state.AcknowledgedBy = acknowledgedBy;
            state.AcknowledgedAt = DateTimeOffset.UtcNow;
            state.AcknowledgmentNotes = notes;
            state.Status = EscalationStatus.Acknowledged;
            state.EscalationCompletedAt = DateTimeOffset.UtcNow;
            state.NextEscalationTime = null; // Stop further escalations

            var updated = await this.escalationStore.UpdateEscalationStateAsync(state, cancellationToken);
            if (!updated)
            {
                // Optimistic locking conflict - retry once
                this.logger.LogWarning("Optimistic locking conflict when acknowledging alert {AlertId}, retrying", alertId);
                state = await this.escalationStore.GetEscalationStateByAlertIdAsync(alertId, cancellationToken);
                if (state != null && !state.IsAcknowledged)
                {
                    state.IsAcknowledged = true;
                    state.AcknowledgedBy = acknowledgedBy;
                    state.AcknowledgedAt = DateTimeOffset.UtcNow;
                    state.AcknowledgmentNotes = notes;
                    state.Status = EscalationStatus.Acknowledged;
                    state.EscalationCompletedAt = DateTimeOffset.UtcNow;
                    state.NextEscalationTime = null;
                    await this.escalationStore.UpdateEscalationStateAsync(state, cancellationToken);
                }
            }

            // Record acknowledgment event
            await this.RecordEventAsync(
                state.Id,
                EscalationEventType.Acknowledged,
                state.CurrentLevel,
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["acknowledged_by"] = acknowledgedBy,
                    ["notes"] = notes ?? string.Empty,
                },
                cancellationToken);

            this.logger.LogInformation(
                "Alert {AlertId} acknowledged by {User}, escalation stopped",
                alertId,
                acknowledgedBy);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to acknowledge alert {AlertId}", alertId);
            throw;
        }
    }

    public async Task ProcessPendingEscalationsAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingEscalations = await this.escalationStore.GetPendingEscalationsAsync(batchSize, cancellationToken);

            if (pendingEscalations.Count == 0)
            {
                return;
            }

            this.logger.LogInformation("Processing {Count} pending escalations", pendingEscalations.Count);

            foreach (var state in pendingEscalations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await this.ProcessSingleEscalationAsync(state, cancellationToken);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(
                        ex,
                        "Failed to process escalation {EscalationId} for alert {AlertId}",
                        state.Id,
                        state.AlertId);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process pending escalations");
            throw;
        }
    }

    public async Task<AlertEscalationState?> GetEscalationStatusAsync(long alertId, CancellationToken cancellationToken = default)
    {
        return await this.escalationStore.GetEscalationStateByAlertIdAsync(alertId, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertEscalationEvent>> GetEscalationHistoryAsync(long alertId, CancellationToken cancellationToken = default)
    {
        var state = await this.escalationStore.GetEscalationStateByAlertIdAsync(alertId, cancellationToken);
        if (state == null)
        {
            return Array.Empty<AlertEscalationEvent>();
        }

        return await this.escalationStore.GetEscalationEventsAsync(state.Id, cancellationToken);
    }

    public async Task CancelEscalationAsync(long alertId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await this.escalationStore.GetEscalationStateByAlertIdAsync(alertId, cancellationToken);
            if (state == null || state.Status != EscalationStatus.Active)
            {
                return;
            }

            state.Status = EscalationStatus.Cancelled;
            state.CancellationReason = reason;
            state.EscalationCompletedAt = DateTimeOffset.UtcNow;
            state.NextEscalationTime = null;

            await this.escalationStore.UpdateEscalationStateAsync(state, cancellationToken);

            await this.RecordEventAsync(
                state.Id,
                EscalationEventType.Cancelled,
                state.CurrentLevel,
                null,
                null,
                new Dictionary<string, object?> { ["reason"] = reason },
                cancellationToken);

            this.logger.LogInformation("Escalation cancelled for alert {AlertId}: {Reason}", alertId, reason);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to cancel escalation for alert {AlertId}", alertId);
            throw;
        }
    }

    private async Task ProcessSingleEscalationAsync(AlertEscalationState state, CancellationToken cancellationToken)
    {
        // Get policy and alert details
        var policy = await this.escalationStore.GetPolicyByIdAsync(state.PolicyId, cancellationToken);
        if (policy == null)
        {
            this.logger.LogWarning("Policy {PolicyId} not found for escalation {EscalationId}", state.PolicyId, state.Id);
            return;
        }

        var alert = await this.alertHistoryStore.GetAlertByIdAsync(state.AlertId, cancellationToken);
        if (alert == null)
        {
            this.logger.LogWarning("Alert {AlertId} not found for escalation {EscalationId}", state.AlertId, state.Id);
            return;
        }

        // Check if escalation is suppressed
        if (await this.IsEscalationSuppressedAsync(alert.Name, alert.Severity, cancellationToken))
        {
            this.logger.LogInformation("Escalation {EscalationId} suppressed by maintenance window", state.Id);
            await this.RecordEventAsync(
                state.Id,
                EscalationEventType.Suppressed,
                state.CurrentLevel,
                null,
                null,
                new Dictionary<string, object?> { ["reason"] = "maintenance_window" },
                cancellationToken);
            return;
        }

        // Move to next level
        var nextLevel = state.CurrentLevel + 1;
        if (nextLevel >= policy.EscalationLevels.Count)
        {
            // Reached final level
            state.Status = EscalationStatus.Completed;
            state.EscalationCompletedAt = DateTimeOffset.UtcNow;
            state.NextEscalationTime = null;

            await this.escalationStore.UpdateEscalationStateAsync(state, cancellationToken);

            await this.RecordEventAsync(
                state.Id,
                EscalationEventType.Completed,
                state.CurrentLevel,
                null,
                null,
                new Dictionary<string, object?> { ["reason"] = "reached_final_level" },
                cancellationToken);

            this.logger.LogInformation("Escalation {EscalationId} completed (reached final level)", state.Id);
            return;
        }

        var escalationLevel = policy.EscalationLevels[nextLevel];

        // Update state
        state.CurrentLevel = nextLevel;
        state.NextEscalationTime = nextLevel + 1 < policy.EscalationLevels.Count
            ? DateTimeOffset.UtcNow.Add(policy.EscalationLevels[nextLevel + 1].Delay)
            : null;

        var updated = await this.escalationStore.UpdateEscalationStateAsync(state, cancellationToken);
        if (!updated)
        {
            this.logger.LogWarning("Optimistic locking conflict when escalating {EscalationId}", state.Id);
            return; // Will be retried on next cycle
        }

        // Record escalation event
        await this.RecordEventAsync(
            state.Id,
            EscalationEventType.Escalated,
            nextLevel,
            escalationLevel.NotificationChannels,
            escalationLevel.SeverityOverride,
            new Dictionary<string, object?>
            {
                ["previous_level"] = state.CurrentLevel - 1,
                ["next_level"] = state.CurrentLevel,
            },
            cancellationToken);

        // Send notifications
        await this.SendEscalationNotificationsAsync(alert, escalationLevel, nextLevel, cancellationToken);

        this.logger.LogInformation(
            "Escalated alert {AlertId} to level {Level} with channels: {Channels}",
            state.AlertId,
            nextLevel,
            string.Join(", ", escalationLevel.NotificationChannels));
    }

    private async Task SendEscalationNotificationsAsync(
        AlertHistoryEntry alert,
        EscalationLevel level,
        int levelNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a modified alert with severity override if specified
            var effectiveSeverity = level.GetEffectiveSeverity(alert.Severity);

            // Convert alert to webhook format for publishing
            var webhook = this.ConvertToWebhook(alert, effectiveSeverity, levelNumber);

            // Publish to specified channels (the CompositeAlertPublisher will handle channel routing)
            await this.alertPublisher.PublishWithResultAsync(webhook, effectiveSeverity, cancellationToken);

            this.logger.LogDebug(
                "Sent escalation notifications for alert {AlertId} at level {Level}",
                alert.Id,
                levelNumber);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to send escalation notifications for alert {AlertId} at level {Level}",
                alert.Id,
                levelNumber);

            // Don't throw - we've already recorded the escalation event
        }
    }

    private AlertManagerWebhook ConvertToWebhook(AlertHistoryEntry alert, string severity, int escalationLevel)
    {
        // Create a webhook representation of the alert for publishing
        var annotations = new Dictionary<string, string>
        {
            ["summary"] = alert.Summary ?? string.Empty,
            ["description"] = alert.Description ?? string.Empty,
            ["escalation_level"] = escalationLevel.ToString(),
        };

        var labels = new Dictionary<string, string>(alert.Labels ?? new Dictionary<string, string>())
        {
            ["alertname"] = alert.Name,
            ["severity"] = severity,
        };

        if (!string.IsNullOrWhiteSpace(alert.Service))
        {
            labels["service"] = alert.Service;
        }

        if (!string.IsNullOrWhiteSpace(alert.Environment))
        {
            labels["environment"] = alert.Environment;
        }

        return new AlertManagerWebhook
        {
            Receiver = "escalation",
            Status = alert.Status,
            Alerts = new List<AlertManagerAlert>
            {
                new()
                {
                    Status = alert.Status,
                    Labels = labels,
                    Annotations = annotations,
                    StartsAt = alert.Timestamp,
                    GeneratorURL = alert.Source,
                },
            },
            GroupLabels = new Dictionary<string, string> { ["alertname"] = alert.Name },
            CommonLabels = labels,
            CommonAnnotations = annotations,
        };
    }

    private async Task<bool> IsEscalationSuppressedAsync(string alertName, string severity, CancellationToken cancellationToken)
    {
        var suppressions = await this.escalationStore.GetActiveSuppressionWindowsAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return suppressions.Any(s => s.AppliesTo(alertName, severity, now));
    }

    private async Task RecordEventAsync(
        long escalationStateId,
        EscalationEventType eventType,
        int? level,
        List<string>? channels,
        string? severityOverride,
        Dictionary<string, object?> details,
        CancellationToken cancellationToken)
    {
        var escalationEvent = new AlertEscalationEvent
        {
            EscalationStateId = escalationStateId,
            EventType = eventType,
            EscalationLevel = level,
            NotificationChannels = channels,
            SeverityOverride = severityOverride,
            EventTimestamp = DateTimeOffset.UtcNow,
            EventDetails = details,
        };

        await this.escalationStore.InsertEscalationEventAsync(escalationEvent, cancellationToken);
    }
}
