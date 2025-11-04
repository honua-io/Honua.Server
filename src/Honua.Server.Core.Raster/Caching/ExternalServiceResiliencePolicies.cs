// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

/// <summary>
/// Provides circuit breaker and resilience policies for external service calls (S3, Azure Blob, HTTP).
/// </summary>
public static class ExternalServiceResiliencePolicies
{
    /// <summary>
    /// Creates a circuit breaker resilience pipeline for external storage services.
    /// Configuration:
    /// - 5 consecutive failures opens the circuit
    /// - 30 second timeout when circuit is open
    /// - 50% failure rate threshold over a sampling duration
    /// </summary>
    public static ResiliencePipeline CreateCircuitBreakerPipeline(
        string serviceName,
        ILogger logger,
        ICircuitBreakerMetrics? metrics = null)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Circuit opens after 5 consecutive failures
                FailureRatio = 0.5, // 50% failure rate threshold
                MinimumThroughput = 10, // Minimum 10 actions before circuit can open
                SamplingDuration = TimeSpan.FromSeconds(30), // Sample window for failure rate
                BreakDuration = TimeSpan.FromSeconds(30), // Circuit stays open for 30 seconds

                // Only handle exceptions that indicate service failures
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsTransientException(ex)),

                // Log circuit state changes and record metrics
                OnOpened = args =>
                {
                    var outcome = args.Outcome.Exception?.GetType().Name ?? "Unknown";

                    logger.LogWarning(
                        "Circuit breaker OPENED for {ServiceName}. Outcome: {Outcome}, Break duration: {BreakDuration}s",
                        serviceName,
                        outcome,
                        args.BreakDuration.TotalSeconds);

                    metrics?.RecordCircuitOpened(serviceName, outcome);
                    return default;
                },
                OnClosed = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker CLOSED for {ServiceName}. Service is healthy again.",
                        serviceName);

                    metrics?.RecordCircuitClosed(serviceName);
                    return default;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker HALF-OPEN for {ServiceName}. Testing if service has recovered.",
                        serviceName);

                    metrics?.RecordCircuitHalfOpened(serviceName);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Determines if an exception is transient and should trigger circuit breaker logic.
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        // Network and timeout exceptions
        if (ex is TimeoutException or
            System.Net.Http.HttpRequestException or
            System.Net.Sockets.SocketException or
            System.IO.IOException)
        {
            return true;
        }

        // AWS SDK exceptions
        if (ex.GetType().FullName?.StartsWith("Amazon.") == true)
        {
            // Only transient S3 exceptions, not authorization failures
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("timeout") ||
                   message.Contains("throttl") ||
                   message.Contains("service unavailable") ||
                   message.Contains("internal error") ||
                   message.Contains("slow down");
        }

        // Azure SDK exceptions
        if (ex.GetType().FullName?.StartsWith("Azure.") == true)
        {
            // Only transient Azure exceptions
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("timeout") ||
                   message.Contains("throttl") ||
                   message.Contains("service unavailable") ||
                   message.Contains("server busy") ||
                   message.Contains("too many requests");
        }

        return false;
    }
}
