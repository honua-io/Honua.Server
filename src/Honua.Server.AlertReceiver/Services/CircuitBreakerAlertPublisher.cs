// <copyright file="CircuitBreakerAlertPublisher.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

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
    private readonly IAlertPublisher innerPublisher;
    private readonly AsyncCircuitBreakerPolicy circuitBreaker;
    private readonly ILogger<CircuitBreakerAlertPublisher> logger;
    private readonly string publisherName;

    public CircuitBreakerAlertPublisher(
        IAlertPublisher innerPublisher,
        IConfiguration configuration,
        ILogger<CircuitBreakerAlertPublisher> logger)
    {
        this.innerPublisher = innerPublisher;
        this.logger = logger;
        this.publisherName = innerPublisher.GetType().Name;

        var failureThreshold = configuration.GetValue("Alerts:CircuitBreaker:FailureThreshold", 5);
        var breakDuration = configuration.GetValue("Alerts:CircuitBreaker:BreakDurationSeconds", 60);

        this.circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(breakDuration),
                onBreak: (exception, duration) =>
                {
                    this.logger.LogWarning(
                        "Circuit breaker OPEN for {Publisher}: {Exception}. Breaking for {Duration}s",
                        this.publisherName,
                        exception.GetType().Name,
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    this.logger.LogInformation("Circuit breaker CLOSED for {Publisher}", this.publisherName);
                },
                onHalfOpen: () =>
                {
                    this.logger.LogInformation("Circuit breaker HALF-OPEN for {Publisher}, testing...", this.publisherName);
                });
    }

    public async Task PublishAsync(AlertManagerWebhook webhook, string severity, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.circuitBreaker.ExecuteAsync(async () =>
            {
                await this.innerPublisher.PublishAsync(webhook, severity, cancellationToken);
            });
        }
        catch (BrokenCircuitException)
        {
            this.logger.LogWarning(
                "Alert not sent to {Publisher} - circuit breaker is OPEN",
                this.publisherName);
            throw;
        }
    }
}
