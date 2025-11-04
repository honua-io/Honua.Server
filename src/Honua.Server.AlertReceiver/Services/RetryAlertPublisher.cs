// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
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
    private readonly IAlertPublisher _innerPublisher;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly ILogger<RetryAlertPublisher> _logger;
    private readonly string _publisherName;

    public RetryAlertPublisher(
        IAlertPublisher innerPublisher,
        IConfiguration configuration,
        ILogger<RetryAlertPublisher> logger)
    {
        _innerPublisher = innerPublisher;
        _logger = logger;
        _publisherName = innerPublisher.GetType().Name;

        var maxRetries = configuration.GetValue("Alerts:Retry:MaxAttempts", 3);
        var baseDelayMs = configuration.GetValue("Alerts:Retry:BaseDelayMs", 1000);

        // Use centralized retry policy builder
        _retryPolicy = ResiliencePolicies.CreateRetryPolicy(
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromMilliseconds(baseDelayMs),
            logger: logger,
            shouldRetry: _ => true); // Retry all exceptions for alert publishing
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        await _retryPolicy.ExecuteAsync(async ct =>
        {
            await _innerPublisher.PublishAsync(webhook, severity, ct);
        }, cancellationToken);
    }
}

/// <summary>
/// Factory for creating retry wrapped publishers.
/// </summary>
public static class RetryPublisherFactory
{
    public static IAlertPublisher Wrap(
        IAlertPublisher publisher,
        IConfiguration configuration,
        ILogger<RetryAlertPublisher> logger)
    {
        return new RetryAlertPublisher(publisher, configuration, logger);
    }
}
