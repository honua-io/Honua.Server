// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check for circuit breaker states.
/// Reports degraded health if any circuit breakers are open, indicating service degradation.
/// </summary>
public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly ICircuitBreakerService _circuitBreakerService;
    private readonly ILogger<CircuitBreakerHealthCheck> _logger;

    public CircuitBreakerHealthCheck(
        ICircuitBreakerService circuitBreakerService,
        ILogger<CircuitBreakerHealthCheck> logger)
    {
        _circuitBreakerService = circuitBreakerService ?? throw new ArgumentNullException(nameof(circuitBreakerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();
            var openCircuits = new List<string>();
            var halfOpenCircuits = new List<string>();

            // Check each circuit breaker state
            var services = new[] { "database", "externalapi", "storage" };

            foreach (var service in services)
            {
                var state = _circuitBreakerService.GetCircuitState(service);
                data[$"{service}_circuit_state"] = state.ToString();

                if (state == CircuitState.Open)
                {
                    openCircuits.Add(service);
                }
                else if (state == CircuitState.HalfOpen)
                {
                    halfOpenCircuits.Add(service);
                }
            }

            // Determine overall health status
            if (openCircuits.Count > 0)
            {
                data["open_circuits"] = openCircuits;
                data["open_circuit_count"] = openCircuits.Count;

                _logger.LogWarning(
                    "Circuit breakers are open for: {OpenCircuits}. This indicates service degradation.",
                    string.Join(", ", openCircuits));

                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Circuit breaker(s) open: {string.Join(", ", openCircuits)}. Service is degraded but operational with fallbacks.",
                    data: data));
            }

            if (halfOpenCircuits.Count > 0)
            {
                data["half_open_circuits"] = halfOpenCircuits;
                data["half_open_circuit_count"] = halfOpenCircuits.Count;

                _logger.LogInformation(
                    "Circuit breakers are testing recovery for: {HalfOpenCircuits}",
                    string.Join(", ", halfOpenCircuits));

                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Circuit breaker(s) testing recovery: {string.Join(", ", halfOpenCircuits)}",
                    data: data));
            }

            // All circuits are closed - healthy
            return Task.FromResult(HealthCheckResult.Healthy(
                "All circuit breakers are closed. Services are operating normally.",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circuit breaker health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Circuit breaker health check failed: " + ex.Message,
                exception: ex));
        }
    }
}
