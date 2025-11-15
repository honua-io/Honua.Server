// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Configuration options for Site Reliability Engineering (SRE) features including
/// Service Level Indicators (SLIs), Service Level Objectives (SLOs), and error budget tracking.
/// </summary>
/// <remarks>
/// SRE features are designed for Tier 3 enterprise deployments with SLA commitments.
/// They provide formal tracking of service reliability targets and error budgets.
///
/// Configuration example:
/// <code>
/// SRE__ENABLED=true
/// SRE__SLOS__LATENCY_SLO__ENABLED=true
/// SRE__SLOS__LATENCY_SLO__TARGET=0.99
/// SRE__SLOS__LATENCY_SLO__THRESHOLD_MS=500
/// SRE__SLOS__AVAILABILITY_SLO__ENABLED=true
/// SRE__SLOS__AVAILABILITY_SLO__TARGET=0.999
/// </code>
/// </remarks>
public sealed class SreOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether SRE features are enabled.
    /// Default is false. Only enable for Tier 3 deployments or when formal SLO tracking is required.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets the time window (in days) for SLO compliance calculations.
    /// Default is 28 days (4 weeks).
    /// </summary>
    /// <remarks>
    /// Common values:
    /// - 1 day: For testing or very aggressive monitoring
    /// - 7 days: Weekly tracking
    /// - 28 days: Monthly tracking (default)
    /// - 90 days: Quarterly tracking
    /// </remarks>
    public int RollingWindowDays { get; init; } = 28;

    /// <summary>
    /// Gets or sets the evaluation interval (in minutes) for SLO compliance checks.
    /// Default is 5 minutes.
    /// </summary>
    /// <remarks>
    /// This controls how often the SLO evaluator runs to calculate compliance metrics.
    /// Lower values provide more real-time visibility but increase CPU usage.
    /// Recommended values: 1-15 minutes.
    /// </remarks>
    public int EvaluationIntervalMinutes { get; init; } = 5;

    /// <summary>
    /// Gets or sets the SLO definitions for this deployment.
    /// </summary>
    public Dictionary<string, SloConfig> Slos { get; init; } = new();

    /// <summary>
    /// Gets or sets thresholds for error budget status warnings.
    /// </summary>
    public ErrorBudgetThresholds ErrorBudgetThresholds { get; init; } = new();
}

/// <summary>
/// Configuration for a single Service Level Objective (SLO).
/// </summary>
public sealed class SloConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether this SLO is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets the SLO target as a decimal (e.g., 0.99 for 99%, 0.999 for 99.9%).
    /// </summary>
    /// <remarks>
    /// Common SLO targets:
    /// - 0.90 (90%): Low criticality services
    /// - 0.95 (95%): Standard services
    /// - 0.99 (99%): High availability services (43.8 minutes downtime/month)
    /// - 0.999 (99.9%): Mission critical services (4.38 minutes downtime/month)
    /// - 0.9999 (99.99%): Ultra-critical services (26.3 seconds downtime/month)
    /// </remarks>
    public double Target { get; init; } = 0.99;

    /// <summary>
    /// Gets or sets the SLI type for this SLO.
    /// </summary>
    public SliType Type { get; init; }

    /// <summary>
    /// Gets or sets the threshold value for latency-based SLIs (in milliseconds).
    /// Only applicable when Type is Latency.
    /// </summary>
    /// <remarks>
    /// Example: If ThresholdMs = 500, then requests completing in &lt;500ms count as "good",
    /// and the SLO tracks the percentage of requests meeting this threshold.
    /// </remarks>
    public double? ThresholdMs { get; init; }

    /// <summary>
    /// Gets or sets a human-readable description of this SLO.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the endpoints to include in this SLO measurement.
    /// If null or empty, all endpoints are included.
    /// </summary>
    /// <remarks>
    /// Example: ["/api/features", "/api/tiles"] to track only specific API endpoints.
    /// </remarks>
    public string[]? IncludeEndpoints { get; init; }

    /// <summary>
    /// Gets or sets the endpoints to exclude from this SLO measurement.
    /// </summary>
    /// <remarks>
    /// Example: ["/health", "/metrics"] to exclude non-business endpoints.
    /// </remarks>
    public string[]? ExcludeEndpoints { get; init; }
}

/// <summary>
/// Defines the type of Service Level Indicator being measured.
/// </summary>
public enum SliType
{
    /// <summary>
    /// Measures the percentage of requests completing within a latency threshold.
    /// </summary>
    /// <remarks>
    /// Example: 99% of requests must complete in &lt;500ms.
    /// </remarks>
    Latency,

    /// <summary>
    /// Measures the percentage of successful requests (non-5xx responses).
    /// </summary>
    /// <remarks>
    /// Example: 99.9% of requests must return non-5xx status codes.
    /// </remarks>
    Availability,

    /// <summary>
    /// Measures the percentage of requests without errors (5xx only, not 4xx).
    /// </summary>
    /// <remarks>
    /// Example: 99.95% of requests must not result in server errors.
    /// 4xx client errors are excluded as they're typically user/client issues.
    /// </remarks>
    ErrorRate,

    /// <summary>
    /// Measures the success rate of health check probes.
    /// </summary>
    /// <remarks>
    /// Example: 99.99% of health check probes must succeed.
    /// </remarks>
    HealthCheckSuccess
}

/// <summary>
/// Thresholds for error budget status warnings.
/// </summary>
public sealed class ErrorBudgetThresholds
{
    /// <summary>
    /// Gets or sets the threshold (0.0-1.0) at which error budget is considered "Warning".
    /// Default is 0.25 (25% remaining).
    /// </summary>
    /// <remarks>
    /// When error budget drops below this threshold, warnings are logged and operators
    /// should consider reducing deployment velocity or implementing additional safeguards.
    /// </remarks>
    public double WarningThreshold { get; init; } = 0.25;

    /// <summary>
    /// Gets or sets the threshold (0.0-1.0) at which error budget is considered "Critical".
    /// Default is 0.10 (10% remaining).
    /// </summary>
    /// <remarks>
    /// When error budget drops below this threshold, critical alerts are triggered and
    /// non-essential deployments should be halted until the error budget recovers.
    /// </remarks>
    public double CriticalThreshold { get; init; } = 0.10;
}
