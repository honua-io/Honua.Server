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
/// </summary>
public interface IAlertPersistenceService
{
    Task SaveAlertAsync(GenericAlert alert, string[] publishedTo, bool wasSuppressed, string? suppressionReason = null);

    Task<List<AlertHistoryEntry>> GetRecentAlertsAsync(int limit = 100, string? severity = null);

    Task<AlertHistoryEntry?> GetAlertByFingerprintAsync(string fingerprint);
}

public sealed class AlertPersistenceService : IAlertPersistenceService
{
    private readonly IAlertHistoryStore historyStore;
    private readonly IAlertMetricsService metricsService;
    private readonly ILogger<AlertPersistenceService> logger;

    public AlertPersistenceService(
        IAlertHistoryStore historyStore,
        IAlertMetricsService metricsService,
        ILogger<AlertPersistenceService> logger)
    {
        this.historyStore = historyStore;
        this.metricsService = metricsService;
        this.logger = logger;
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
            this.logger.LogCritical(ex, "Failed to persist alert: {Name}", alert.Name);
            throw new AlertPersistenceException("Failed to persist alert history entry.", ex);
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

    private static string GenerateFingerprint(GenericAlert alert)
    {
        var key = $"{alert.Source}:{alert.Name}:{alert.Service ?? "default"}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
