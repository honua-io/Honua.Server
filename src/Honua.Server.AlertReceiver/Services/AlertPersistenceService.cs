// <copyright file="AlertPersistenceService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Honua.Server.AlertReceiver.Extensions;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Persists alerts to database for audit trail and history.
/// Supports durable queue pattern by persisting BEFORE sending.
/// </summary>
public interface IAlertPersistenceService
{
    Task SaveAlertAsync(GenericAlert alert, string[] publishedTo, bool wasSuppressed, string? suppressionReason = null);

    Task<long> PersistAlertBeforeSendAsync(GenericAlert alert, CancellationToken cancellationToken = default);

    Task UpdateDeliveryStatusAsync(long alertId, AlertDeliveryResult deliveryResult, CancellationToken cancellationToken = default);

    Task UpdateDeliveryStatusAndStartEscalationAsync(long alertId, AlertDeliveryResult deliveryResult, CancellationToken cancellationToken = default);

    Task<List<AlertHistoryEntry>> GetRecentAlertsAsync(int limit = 100, string? severity = null);

    Task<AlertHistoryEntry?> GetAlertByFingerprintAsync(string fingerprint);
}

public sealed class AlertPersistenceService : IAlertPersistenceService
{
    private readonly IAlertHistoryStore historyStore;
    private readonly IAlertMetricsService metricsService;
    private readonly ILogger<AlertPersistenceService> logger;
    private readonly IAlertEscalationService? escalationService;

    public AlertPersistenceService(
        IAlertHistoryStore historyStore,
        IAlertMetricsService metricsService,
        ILogger<AlertPersistenceService> logger,
        IAlertEscalationService? escalationService = null)
    {
        this.historyStore = historyStore;
        this.metricsService = metricsService;
        this.logger = logger;
        this.escalationService = escalationService;
    }

    public async Task SaveAlertAsync(GenericAlert alert, string[] publishedTo, bool wasSuppressed, string? suppressionReason = null)
    {
        try
        {
            var entry = new AlertHistoryEntry
            {
                Fingerprint = alert.Fingerprint ?? GenerateFingerprint(alert),
                Name = alert.Name,
                Severity = alert.Severity,
                Status = alert.Status,
                Summary = alert.Summary,
                Description = alert.Description,
                Source = alert.Source,
                Service = alert.Service,
                Environment = alert.Environment,
                Labels = alert.Labels.Count > 0 ? new Dictionary<string, string>(alert.Labels, StringComparer.OrdinalIgnoreCase) : null,
                Context = alert.Context != null ? new Dictionary<string, object?>(alert.Context, StringComparer.OrdinalIgnoreCase) : null,
                Timestamp = alert.Timestamp,
                PublishedTo = publishedTo,
                WasSuppressed = wasSuppressed,
                SuppressionReason = suppressionReason,
            };

            await this.historyStore.InsertAlertAsync(entry).ConfigureAwait(false);

            this.logger.LogDebug("Persisted alert: {Name} ({Fingerprint})", alert.Name, entry.Fingerprint);
        }
        catch (Exception ex)
        {
            this.metricsService.RecordAlertPersistenceFailure("save");

            // Log the full exception with all details for internal debugging
            this.logger.LogCritical(ex, "Failed to persist alert: {Name}", alert.Name);

            // Throw sanitized exception without inner exception to prevent leaking connection strings
            // The full details are already logged above for debugging purposes
            throw new AlertPersistenceException($"Failed to persist alert history entry: {GetSafeErrorMessage(ex)}");
        }
    }

    public async Task<List<AlertHistoryEntry>> GetRecentAlertsAsync(int limit = 100, string? severity = null)
    {
        var results = await this.historyStore.GetRecentAlertsAsync(limit, severity).ConfigureAwait(false);
        return results.ToList();
    }

    public async Task<AlertHistoryEntry?> GetAlertByFingerprintAsync(string fingerprint)
    {
        return await this.historyStore.GetAlertByFingerprintAsync(fingerprint).ConfigureAwait(false);
    }

    public async Task<long> PersistAlertBeforeSendAsync(GenericAlert alert, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new AlertHistoryEntry
            {
                Fingerprint = alert.Fingerprint ?? GenerateFingerprint(alert),
                Name = alert.Name,
                Severity = alert.Severity,
                Status = alert.Status,
                Summary = alert.Summary,
                Description = alert.Description,
                Source = alert.Source,
                Service = alert.Service,
                Environment = alert.Environment,
                Labels = alert.Labels.Count > 0 ? new Dictionary<string, string>(alert.Labels, StringComparer.OrdinalIgnoreCase) : null,
                Context = alert.Context != null ? new Dictionary<string, object?>(alert.Context, StringComparer.OrdinalIgnoreCase) : null,
                Timestamp = alert.Timestamp,
                PublishedTo = Array.Empty<string>(),
                WasSuppressed = false,
                SuppressionReason = null,
                DeliveryStatus = Data.AlertDeliveryStatus.Pending,
                FailedChannels = Array.Empty<string>(),
                RetryCount = 0,
                LastRetryAttempt = null,
            };

            var id = await this.historyStore.InsertAlertAsync(entry, cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug(
                "Persisted alert BEFORE sending (durable queue pattern): {Name} ({Fingerprint}), ID: {Id}",
                alert.Name,
                entry.Fingerprint,
                id);

            return id;
        }
        catch (Exception ex)
        {
            this.metricsService.RecordAlertPersistenceFailure("persist_before_send");

            // Log the full exception with all details for internal debugging
            this.logger.LogCritical(ex, "Failed to persist alert before sending: {Name}", alert.Name);

            // Throw sanitized exception without inner exception to prevent leaking connection strings
            // The full details are already logged above for debugging purposes
            throw new AlertPersistenceException($"Failed to persist alert before sending: {GetSafeErrorMessage(ex)}");
        }
    }

    public async Task UpdateDeliveryStatusAsync(long alertId, AlertDeliveryResult deliveryResult, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = deliveryResult.AllSucceeded ? Data.AlertDeliveryStatus.Sent :
                         deliveryResult.AllFailed ? Data.AlertDeliveryStatus.Failed :
                         Data.AlertDeliveryStatus.PartiallyFailed;

            await this.historyStore.UpdateAlertDeliveryStatusAsync(
                alertId,
                status,
                deliveryResult.SuccessfulChannels.ToArray(),
                deliveryResult.FailedChannels.ToArray(),
                cancellationToken).ConfigureAwait(false);

            this.logger.LogDebug(
                "Updated alert delivery status: ID={Id}, Status={Status}, Successful=[{Successful}], Failed=[{Failed}]",
                alertId,
                status,
                string.Join(", ", deliveryResult.SuccessfulChannels),
                string.Join(", ", deliveryResult.FailedChannels));
        }
        catch (Exception ex)
        {
            this.metricsService.RecordAlertPersistenceFailure("update_delivery_status");

            // Log the full exception with all details for internal debugging
            this.logger.LogError(ex, "Failed to update delivery status for alert ID: {AlertId}", alertId);

            // Throw sanitized exception without inner exception to prevent leaking connection strings
            // The full details are already logged above for debugging purposes
            throw new AlertPersistenceException($"Failed to update alert delivery status: {GetSafeErrorMessage(ex)}");
        }
    }

    public async Task UpdateDeliveryStatusAndStartEscalationAsync(long alertId, AlertDeliveryResult deliveryResult, CancellationToken cancellationToken = default)
    {
        // First update delivery status
        await this.UpdateDeliveryStatusAsync(alertId, deliveryResult, cancellationToken).ConfigureAwait(false);

        // Then start escalation if service is available
        if (this.escalationService != null)
        {
            try
            {
                // Get the alert details
                var alert = await this.historyStore.GetAlertByIdAsync(alertId, cancellationToken).ConfigureAwait(false);
                if (alert != null)
                {
                    // Start escalation (it will check if a policy applies)
                    await this.escalationService.StartEscalationAsync(alert, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw - escalation failure shouldn't fail the alert delivery
                this.logger.LogError(ex, "Failed to start escalation for alert ID: {AlertId}", alertId);
            }
        }
    }

    /// <summary>
    /// Extracts a safe error message from an exception, avoiding sensitive information leakage.
    /// Returns only generic error type information, never connection strings or internal details.
    /// </summary>
    private static string GetSafeErrorMessage(Exception ex)
    {
        // For database exceptions, never include the message as it may contain connection strings
        if (ex is Npgsql.NpgsqlException)
        {
            return "Database operation error";
        }

        if (ex is System.Data.Common.DbException)
        {
            return "Database connection error";
        }

        // For other exceptions, use only the exception type
        // This provides useful debugging info without exposing internals
        return ex.GetType().Name;
    }

    private static string GenerateFingerprint(GenericAlert alert)
    {
        var key = $"{alert.Source}:{alert.Name}:{alert.Service ?? "default"}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
