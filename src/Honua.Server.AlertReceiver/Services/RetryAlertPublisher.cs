// <copyright file="RetryAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using Honua.Server.AlertReceiver.Models;
using Honua.Server.Core.Resilience;
using Polly;
using Polly.Retry;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Wraps alert publishers with retry logic and exponential backoff.
/// Uses ResiliencePolicies.CreateRetryPolicy for consistent retry behavior.
/// On final failure after all retries, sends alert to Dead Letter Queue for later retry.
/// </summary>
public sealed class RetryAlertPublisher : IAlertPublisher
{
    private readonly IAlertPublisher innerPublisher;
    private readonly ResiliencePipeline retryPolicy;
    private readonly ILogger<RetryAlertPublisher> logger;
    private readonly IAlertDeadLetterQueueService? dlqService;
    private readonly string publisherName;

    public RetryAlertPublisher(
        IAlertPublisher innerPublisher,
        IConfiguration configuration,
        ILogger<RetryAlertPublisher> logger,
        IAlertDeadLetterQueueService? dlqService = null)
    {
        this.innerPublisher = innerPublisher;
        this.logger = logger;
        this.dlqService = dlqService;
        this.publisherName = innerPublisher.GetType().Name;

        var maxRetries = configuration.GetValue("Alerts:Retry:MaxAttempts", 3);
        var baseDelayMs = configuration.GetValue("Alerts:Retry:BaseDelayMs", 1000);

        // Use centralized retry policy builder
        this.retryPolicy = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromMilliseconds(baseDelayMs),
            logger: logger,
            shouldRetry: _ => true); // Retry all exceptions for alert publishing
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.retryPolicy.ExecuteAsync(
                async ct =>
                {
                    await this.innerPublisher.PublishAsync(webhook, severity, ct);
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // All immediate retries exhausted - send to DLQ for background retry
            if (this.dlqService != null)
            {
                try
                {
                    await this.dlqService.EnqueueAsync(webhook, this.publisherName, ex, severity, cancellationToken);
                    this.logger.LogWarning(
                        "Alert delivery to {Publisher} failed after retries. Enqueued to DLQ for background retry. Error: {Error}",
                        this.publisherName,
                        ex.Message);
                }
                catch (Exception dlqEx)
                {
                    this.logger.LogError(
                        dlqEx,
                        "Failed to enqueue alert to DLQ after delivery failure to {Publisher}",
                        this.publisherName);
                }
            }
            else
            {
                this.logger.LogError(
                    ex,
                    "Alert delivery to {Publisher} failed after retries. DLQ not available - alert may be lost!",
                    this.publisherName);
            }

            // Re-throw to maintain existing error handling behavior
            throw;
        }
    }

    public async Task<AlertDeliveryResult> PublishWithResultAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        var result = new AlertDeliveryResult();

        try
        {
            await this.retryPolicy.ExecuteAsync(
                async ct =>
                {
                    await this.innerPublisher.PublishAsync(webhook, severity, ct);
                },
                cancellationToken);

            // Success
            result.SuccessfulChannels.Add(this.publisherName);
        }
        catch (Exception ex)
        {
            // All immediate retries exhausted - send to DLQ for background retry
            result.FailedChannels.Add(this.publisherName);

            if (this.dlqService != null)
            {
                try
                {
                    await this.dlqService.EnqueueAsync(webhook, this.publisherName, ex, severity, cancellationToken);
                    this.logger.LogWarning(
                        "Alert delivery to {Publisher} failed after retries. Enqueued to DLQ for background retry. Error: {Error}",
                        this.publisherName,
                        ex.Message);
                }
                catch (Exception dlqEx)
                {
                    this.logger.LogError(
                        dlqEx,
                        "Failed to enqueue alert to DLQ after delivery failure to {Publisher}",
                        this.publisherName);
                }
            }
            else
            {
                this.logger.LogError(
                    ex,
                    "Alert delivery to {Publisher} failed after retries. DLQ not available - alert may be lost!",
                    this.publisherName);
            }
        }

        return result;
    }
}
