// <copyright file="AlertRetryWorkerService.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Background service that periodically retries failed alert deliveries from the DLQ.
/// Runs every 5 minutes to process pending retries with exponential backoff.
/// </summary>
public sealed class AlertRetryWorkerService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<AlertRetryWorkerService> logger;
    private readonly TimeSpan retryInterval = TimeSpan.FromMinutes(5);
    private readonly int maxRetryCount = 5;
    private readonly int batchSize = 50;

    public AlertRetryWorkerService(
        IServiceProvider serviceProvider,
        ILogger<AlertRetryWorkerService> logger)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("Alert Retry Worker Service starting up");

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.ProcessPendingRetriesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing alert delivery retries");
            }

            // Wait for next cycle
            try
            {
                await Task.Delay(this.retryInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
        }

        this.logger.LogInformation("Alert Retry Worker Service shutting down");
    }

    private async Task ProcessPendingRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var dlqService = scope.ServiceProvider.GetRequiredService<IAlertDeadLetterQueueService>();

        var pendingRetries = await dlqService.GetPendingRetriesAsync(
            this.batchSize,
            this.maxRetryCount,
            cancellationToken).ConfigureAwait(false);

        if (pendingRetries.Count == 0)
        {
            this.logger.LogDebug("No pending alert delivery retries found");
            return;
        }

        this.logger.LogInformation("Processing {Count} pending alert delivery retries", pendingRetries.Count);

        var successCount = 0;
        var failureCount = 0;
        var abandonedCount = 0;

        foreach (var failure in pendingRetries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await this.RetryAlertDeliveryAsync(failure, scope.ServiceProvider, cancellationToken)
                    .ConfigureAwait(false);

                if (result)
                {
                    // Success - mark as resolved
                    await dlqService.MarkResolvedAsync(failure.Id, cancellationToken).ConfigureAwait(false);
                    successCount++;
                    this.logger.LogInformation(
                        "Successfully retried alert delivery {Id} to {Channel} after {RetryCount} attempts",
                        failure.Id,
                        failure.TargetChannel,
                        failure.RetryCount + 1);
                }
                else
                {
                    // Still failing - check if we should abandon
                    if (failure.RetryCount + 1 >= this.maxRetryCount)
                    {
                        await dlqService.AbandonAsync(
                            failure.Id,
                            $"Max retries ({this.maxRetryCount}) exceeded",
                            cancellationToken).ConfigureAwait(false);
                        abandonedCount++;
                        this.logger.LogWarning(
                            "Abandoning alert delivery {Id} to {Channel} after {RetryCount} failed attempts",
                            failure.Id,
                            failure.TargetChannel,
                            failure.RetryCount + 1);
                    }
                    else
                    {
                        // Update retry state with backoff
                        await dlqService.UpdateRetryStateAsync(failure.Id, cancellationToken).ConfigureAwait(false);
                        failureCount++;
                        this.logger.LogWarning(
                            "Alert delivery {Id} to {Channel} still failing. Will retry again. Attempts: {Attempts}/{Max}",
                            failure.Id,
                            failure.TargetChannel,
                            failure.RetryCount + 1,
                            this.maxRetryCount);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Error processing retry for alert delivery {Id} to {Channel}",
                    failure.Id,
                    failure.TargetChannel);

                // Update retry state on exception
                try
                {
                    if (failure.RetryCount + 1 >= this.maxRetryCount)
                    {
                        await dlqService.AbandonAsync(failure.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                        abandonedCount++;
                    }
                    else
                    {
                        await dlqService.UpdateRetryStateAsync(failure.Id, cancellationToken).ConfigureAwait(false);
                        failureCount++;
                    }
                }
                catch (Exception updateEx)
                {
                    this.logger.LogError(updateEx, "Failed to update retry state for {Id}", failure.Id);
                }
            }
        }

        this.logger.LogInformation(
            "Alert retry batch complete. Success: {Success}, Failed: {Failed}, Abandoned: {Abandoned}",
            successCount,
            failureCount,
            abandonedCount);
    }

    private async Task<bool> RetryAlertDeliveryAsync(
        AlertDeliveryFailure failure,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Deserialize the alert payload
        AlertManagerWebhook webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<AlertManagerWebhook>(failure.AlertPayloadJson)
                ?? throw new InvalidOperationException("Failed to deserialize alert payload");
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to deserialize alert payload for delivery {Id}. Payload may be corrupted.",
                failure.Id);
            return false;
        }

        // Get the appropriate publisher for the target channel
        var publisher = this.GetPublisherForChannel(failure.TargetChannel, serviceProvider);
        if (publisher == null)
        {
            this.logger.LogWarning(
                "No publisher found for channel {Channel}. Delivery {Id} cannot be retried.",
                failure.TargetChannel,
                failure.Id);
            return false;
        }

        // Attempt delivery
        try
        {
            await publisher.PublishAsync(webhook, failure.Severity, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "Retry attempt failed for delivery {Id} to {Channel}",
                failure.Id,
                failure.TargetChannel);
            return false;
        }
    }

    private IAlertPublisher? GetPublisherForChannel(string channel, IServiceProvider serviceProvider)
    {
        // Map channel names to publisher types
        // This assumes publishers are registered with their specific types
        return channel.ToLowerInvariant() switch
        {
            "slack" => serviceProvider.GetService<SlackWebhookAlertPublisher>(),
            "teams" => serviceProvider.GetService<TeamsWebhookAlertPublisher>(),
            "pagerduty" => serviceProvider.GetService<PagerDutyAlertPublisher>(),
            "opsgenie" => serviceProvider.GetService<OpsgenieAlertPublisher>(),
            "sns" => serviceProvider.GetService<SnsAlertPublisher>(),
            "eventgrid" => serviceProvider.GetService<AzureEventGridAlertPublisher>(),
            _ => null,
        };
    }
}
