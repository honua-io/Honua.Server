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
/// </summary>
public sealed class RetryAlertPublisher : IAlertPublisher
{
    private readonly IAlertPublisher innerPublisher;
    private readonly ResiliencePipeline retryPolicy;
    private readonly ILogger<RetryAlertPublisher> logger;
    private readonly string publisherName;

    public RetryAlertPublisher(
        IAlertPublisher innerPublisher,
        IConfiguration configuration,
        ILogger<RetryAlertPublisher> logger)
    {
        this.innerPublisher = innerPublisher;
        this.logger = logger;
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
        await this.retryPolicy.ExecuteAsync(
            async ct =>
            {
                await this.innerPublisher.PublishAsync(webhook, severity, ct);
            },
            cancellationToken);
    }
}
