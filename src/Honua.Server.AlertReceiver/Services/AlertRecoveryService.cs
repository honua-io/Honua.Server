// <copyright file="AlertRecoveryService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Background service that recovers and retries pending/failed alerts on server restart.
/// Implements the recovery mechanism for the durable queue pattern.
/// </summary>
public sealed class AlertRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<AlertRecoveryService> logger;
    private readonly TimeSpan retryInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan minRetryDelay = TimeSpan.FromMinutes(1);

    public AlertRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<AlertRecoveryService> logger)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("Alert Recovery Service starting...");

        // Wait a bit on startup to let other services initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.RecoverPendingAlertsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error during alert recovery cycle");
            }

            // Wait before next recovery cycle
            await Task.Delay(this.retryInterval, stoppingToken).ConfigureAwait(false);
        }

        this.logger.LogInformation("Alert Recovery Service stopped.");
    }

    private async Task RecoverPendingAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = this.scopeFactory.CreateScope();
        var historyStore = scope.ServiceProvider.GetRequiredService<IAlertHistoryStore>();
        var alertPublisher = scope.ServiceProvider.GetRequiredService<IAlertPublisher>();
        var persistenceService = scope.ServiceProvider.GetRequiredService<IAlertPersistenceService>();
        var metricsService = scope.ServiceProvider.GetRequiredService<IAlertMetricsService>();

        // Get alerts that need retry (pending, failed, or partially failed, not retried recently)
        var maxRetryTime = DateTimeOffset.UtcNow.Subtract(this.minRetryDelay);
        var pendingAlerts = await historyStore.GetPendingAlertsForRetryAsync(100, maxRetryTime, cancellationToken)
            .ConfigureAwait(false);

        if (pendingAlerts.Count == 0)
        {
            this.logger.LogDebug("No pending alerts to recover");
            return;
        }

        this.logger.LogInformation(
            "Found {Count} pending alerts to recover/retry",
            pendingAlerts.Count);

        foreach (var alertEntry in pendingAlerts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await this.RetryAlertAsync(
                alertEntry,
                historyStore,
                alertPublisher,
                persistenceService,
                metricsService,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RetryAlertAsync(
        AlertHistoryEntry alertEntry,
        IAlertHistoryStore historyStore,
        IAlertPublisher alertPublisher,
        IAlertPersistenceService persistenceService,
        IAlertMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            this.logger.LogInformation(
                "Retrying alert: ID={Id}, Name={Name}, Fingerprint={Fingerprint}, RetryCount={RetryCount}, Status={Status}",
                alertEntry.Id,
                alertEntry.Name,
                alertEntry.Fingerprint,
                alertEntry.RetryCount,
                alertEntry.DeliveryStatus);

            // Increment retry count
            await historyStore.IncrementRetryCountAsync(alertEntry.Id, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            // Reconstruct GenericAlert from history entry
            var alert = new GenericAlert
            {
                Fingerprint = alertEntry.Fingerprint,
                Name = alertEntry.Name,
                Severity = alertEntry.Severity,
                Status = alertEntry.Status,
                Summary = alertEntry.Summary,
                Description = alertEntry.Description,
                Source = alertEntry.Source,
                Service = alertEntry.Service,
                Environment = alertEntry.Environment,
                Labels = alertEntry.Labels ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Context = alertEntry.Context ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                Timestamp = alertEntry.Timestamp,
            };

            // Convert to AlertManager format
            var webhook = GenericAlertAdapter.ToAlertManagerWebhook(alert);

            // Determine routing severity
            var routingSeverity = MapSeverityToRoute(alert.Severity);

            // Attempt to publish
            var deliveryResult = await alertPublisher.PublishWithResultAsync(webhook, routingSeverity, cancellationToken)
                .ConfigureAwait(false);

            // Update delivery status
            await persistenceService.UpdateDeliveryStatusAsync(alertEntry.Id, deliveryResult, cancellationToken)
                .ConfigureAwait(false);

            if (deliveryResult.AllSucceeded)
            {
                this.logger.LogInformation(
                    "Successfully recovered alert: ID={Id}, Name={Name}, Channels=[{Channels}]",
                    alertEntry.Id,
                    alertEntry.Name,
                    string.Join(", ", deliveryResult.SuccessfulChannels));

                metricsService.RecordAlertReceived("recovery_success", alert.Severity);
            }
            else if (deliveryResult.PartiallyFailed)
            {
                this.logger.LogWarning(
                    "Partially recovered alert: ID={Id}, Name={Name}, Successful=[{Successful}], Failed=[{Failed}]",
                    alertEntry.Id,
                    alertEntry.Name,
                    string.Join(", ", deliveryResult.SuccessfulChannels),
                    string.Join(", ", deliveryResult.FailedChannels));

                metricsService.RecordAlertReceived("recovery_partial", alert.Severity);
            }
            else
            {
                this.logger.LogError(
                    "Failed to recover alert: ID={Id}, Name={Name}, RetryCount={RetryCount}, FailedChannels=[{Failed}]",
                    alertEntry.Id,
                    alertEntry.Name,
                    alertEntry.RetryCount + 1,
                    string.Join(", ", deliveryResult.FailedChannels));

                metricsService.RecordAlertSuppressed("recovery_failed", alert.Severity);

                // TODO: After max retries, send to DLQ (when implemented by another agent)
                if (alertEntry.RetryCount + 1 >= 3)
                {
                    this.logger.LogCritical(
                        "Alert exceeded max retries and will be abandoned: ID={Id}, Name={Name}. " +
                        "TODO: Implement DLQ for permanently failed alerts.",
                        alertEntry.Id,
                        alertEntry.Name);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Exception while retrying alert: ID={Id}, Name={Name}",
                alertEntry.Id,
                alertEntry.Name);
        }
    }

    private static string MapSeverityToRoute(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" or "crit" or "fatal" => "critical",
            "high" or "error" or "err" => "critical",
            "medium" or "warning" or "warn" => "warning",
            "low" or "info" or "information" => "warning",
            _ => "warning",
        };
    }
}
