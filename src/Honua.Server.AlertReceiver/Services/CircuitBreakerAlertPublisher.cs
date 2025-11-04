// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.AlertReceiver.Models;
using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;

namespace Honua.Server.AlertReceiver.Services;

/// <summary>
/// Wraps alert publishers with circuit breakers to prevent cascading failures.
/// </summary>
public sealed class CircuitBreakerAlertPublisher : IAlertPublisher
{
    private readonly IAlertPublisher _innerPublisher;
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<CircuitBreakerAlertPublisher> _logger;
    private readonly string _publisherName;

    public CircuitBreakerAlertPublisher(
        IAlertPublisher innerPublisher,
        IConfiguration configuration,
        ILogger<CircuitBreakerAlertPublisher> logger)
    {
        _innerPublisher = innerPublisher;
        _logger = logger;
        _publisherName = innerPublisher.GetType().Name;

        var failureThreshold = configuration.GetValue("Alerts:CircuitBreaker:FailureThreshold", 5);
        var breakDuration = configuration.GetValue("Alerts:CircuitBreaker:BreakDurationSeconds", 60);

        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(breakDuration),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning(
                        "Circuit breaker OPEN for {Publisher}: {Exception}. Breaking for {Duration}s",
                        _publisherName, exception.GetType().Name, duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker CLOSED for {Publisher}", _publisherName);
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for {Publisher}, testing...", _publisherName);
                });
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                await _innerPublisher.PublishAsync(webhook, severity, cancellationToken);
            });
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "Alert not sent to {Publisher} - circuit breaker is OPEN",
                _publisherName);
            throw;
        }
    }
}

/// <summary>
/// Factory for creating circuit breaker wrapped publishers.
/// </summary>
public static class CircuitBreakerPublisherFactory
{
    public static IAlertPublisher Wrap(
        IAlertPublisher publisher,
        IConfiguration configuration,
        ILogger<CircuitBreakerAlertPublisher> logger)
    {
        return new CircuitBreakerAlertPublisher(publisher, configuration, logger);
    }
}
