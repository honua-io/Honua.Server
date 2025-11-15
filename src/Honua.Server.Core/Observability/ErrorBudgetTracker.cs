// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Tracks and manages error budgets for Service Level Objectives.
/// </summary>
/// <remarks>
/// Error budget is the inverse of the SLO target. For example:
/// - 99.9% SLO = 0.1% error budget (100 errors per 100,000 requests)
/// - 99% SLO = 1% error budget (1,000 errors per 100,000 requests)
///
/// Error budgets help teams balance feature velocity with reliability:
/// - Budget available: Deploy freely, take risks, move fast
/// - Budget low: Slow down deployments, focus on reliability
/// - Budget exhausted: Halt non-critical deployments, fix issues
/// </remarks>
public interface IErrorBudgetTracker
{
    /// <summary>
    /// Gets the current error budget for a specific SLO.
    /// </summary>
    ErrorBudget? GetErrorBudget(string sloName);

    /// <summary>
    /// Gets error budgets for all configured SLOs.
    /// </summary>
    IReadOnlyList<ErrorBudget> GetAllErrorBudgets();

    /// <summary>
    /// Gets deployment policy recommendations based on current error budgets.
    /// </summary>
    DeploymentPolicy GetDeploymentPolicy();
}

/// <summary>
/// Implementation of error budget tracking.
/// </summary>
public sealed class ErrorBudgetTracker : IErrorBudgetTracker, IDisposable
{
    private readonly ISliMetrics _sliMetrics;
    private readonly SreOptions _options;
    private readonly ILogger<ErrorBudgetTracker> _logger;
    private readonly Meter _meter;

    // OpenTelemetry metrics
    private readonly Gauge<double> _errorBudgetRemaining;
    private readonly Gauge<long> _allowedErrors;
    private readonly Gauge<long> _remainingErrors;

    public ErrorBudgetTracker(
        ISliMetrics sliMetrics,
        IOptions<SreOptions> options,
        ILogger<ErrorBudgetTracker> logger)
    {
        _sliMetrics = sliMetrics ?? throw new ArgumentNullException(nameof(sliMetrics));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _meter = new Meter("Honua.Server.SRE", "1.0.0");

        // Create OpenTelemetry metrics
        _errorBudgetRemaining = _meter.CreateGauge<double>(
            "honua.slo.error_budget.remaining",
            unit: "{ratio}",
            description: "Remaining error budget as a ratio (0.0-1.0) of the total budget");

        _allowedErrors = _meter.CreateGauge<long>(
            "honua.slo.error_budget.allowed_errors",
            unit: "{error}",
            description: "Total number of errors allowed by the error budget");

        _remainingErrors = _meter.CreateGauge<long>(
            "honua.slo.error_budget.remaining_errors",
            unit: "{error}",
            description: "Number of errors remaining in the error budget");
    }

    public ErrorBudget? GetErrorBudget(string sloName)
    {
        if (!_options.Slos.TryGetValue(sloName, out var sloConfig) || !sloConfig.Enabled)
            return null;

        var window = TimeSpan.FromDays(_options.RollingWindowDays);
        var statistics = _sliMetrics.GetStatistics(sloName, window);

        if (statistics == null || statistics.TotalEvents == 0)
        {
            // No data yet - return full budget
            return new ErrorBudget
            {
                SloName = sloName,
                Target = sloConfig.Target,
                TotalRequests = 0,
                FailedRequests = 0,
                AllowedErrors = 0,
                RemainingErrors = 0,
                BudgetRemaining = 1.0,
                Status = ErrorBudgetStatus.Healthy,
                WindowDays = _options.RollingWindowDays,
                ActualSli = 0.0,
                SloMet = true
            };
        }

        var totalRequests = statistics.TotalEvents;
        var failedRequests = statistics.BadEvents;
        var allowedErrors = (long)((1.0 - sloConfig.Target) * totalRequests);
        var remainingErrors = Math.Max(0, allowedErrors - failedRequests);
        var budgetRemaining = allowedErrors > 0 ? (double)remainingErrors / allowedErrors : 0.0;

        var status = DetermineErrorBudgetStatus(budgetRemaining);
        var actualSli = statistics.ActualSli;
        var sloMet = actualSli >= sloConfig.Target;

        var budget = new ErrorBudget
        {
            SloName = sloName,
            Target = sloConfig.Target,
            TotalRequests = totalRequests,
            FailedRequests = failedRequests,
            AllowedErrors = allowedErrors,
            RemainingErrors = remainingErrors,
            BudgetRemaining = budgetRemaining,
            Status = status,
            WindowDays = _options.RollingWindowDays,
            ActualSli = actualSli,
            SloMet = sloMet
        };

        // Emit OpenTelemetry metrics
        _errorBudgetRemaining.Record(budgetRemaining,
            new("slo.name", sloName),
            new("status", status.ToString().ToLowerInvariant()));

        _allowedErrors.Record(allowedErrors,
            new("slo.name", sloName));

        _remainingErrors.Record(remainingErrors,
            new("slo.name", sloName));

        // Log warnings when budget is low
        if (status == ErrorBudgetStatus.Warning)
        {
            _logger.LogWarning(
                "Error budget warning for SLO '{SloName}': {BudgetRemaining:P1} remaining ({RemainingErrors}/{AllowedErrors} errors). " +
                "Consider reducing deployment velocity.",
                sloName, budgetRemaining, remainingErrors, allowedErrors);
        }
        else if (status == ErrorBudgetStatus.Critical)
        {
            _logger.LogError(
                "Error budget critical for SLO '{SloName}': {BudgetRemaining:P1} remaining ({RemainingErrors}/{AllowedErrors} errors). " +
                "Halt non-essential deployments and focus on reliability.",
                sloName, budgetRemaining, remainingErrors, allowedErrors);
        }
        else if (status == ErrorBudgetStatus.Exhausted)
        {
            _logger.LogCritical(
                "Error budget exhausted for SLO '{SloName}': {FailedRequests} failures exceed allowed {AllowedErrors} errors. " +
                "SLO is violated. Immediate action required.",
                sloName, failedRequests, allowedErrors);
        }

        return budget;
    }

    public IReadOnlyList<ErrorBudget> GetAllErrorBudgets()
    {
        var budgets = new List<ErrorBudget>();

        foreach (var (sloName, _) in _options.Slos)
        {
            var budget = GetErrorBudget(sloName);
            if (budget != null)
            {
                budgets.Add(budget);
            }
        }

        return budgets;
    }

    public DeploymentPolicy GetDeploymentPolicy()
    {
        var budgets = GetAllErrorBudgets();

        if (budgets.Count == 0)
        {
            return new DeploymentPolicy
            {
                CanDeploy = true,
                Recommendation = DeploymentRecommendation.Normal,
                Reason = "No SLOs configured or no data available",
                AffectedSlos = Array.Empty<string>()
            };
        }

        // Find the most restrictive status
        var worstStatus = budgets.Max(b => b.Status);
        var affectedSlos = budgets.Where(b => b.Status == worstStatus).Select(b => b.SloName).ToArray();

        return worstStatus switch
        {
            ErrorBudgetStatus.Exhausted => new DeploymentPolicy
            {
                CanDeploy = false,
                Recommendation = DeploymentRecommendation.Halt,
                Reason = $"Error budget exhausted for {affectedSlos.Length} SLO(s). Focus on reliability and incident response.",
                AffectedSlos = affectedSlos,
                Details = "One or more SLOs have violated their targets. Halt all non-critical deployments until error budgets recover."
            },

            ErrorBudgetStatus.Critical => new DeploymentPolicy
            {
                CanDeploy = true,
                Recommendation = DeploymentRecommendation.Restricted,
                Reason = $"Error budget critical for {affectedSlos.Length} SLO(s). Deploy only critical fixes.",
                AffectedSlos = affectedSlos,
                Details = "Error budgets are critically low. Only deploy urgent fixes and carefully monitor impact."
            },

            ErrorBudgetStatus.Warning => new DeploymentPolicy
            {
                CanDeploy = true,
                Recommendation = DeploymentRecommendation.Cautious,
                Reason = $"Error budget warning for {affectedSlos.Length} SLO(s). Reduce deployment velocity.",
                AffectedSlos = affectedSlos,
                Details = "Error budgets are running low. Consider slowing down feature deployments and increasing testing rigor."
            },

            _ => new DeploymentPolicy
            {
                CanDeploy = true,
                Recommendation = DeploymentRecommendation.Normal,
                Reason = "All error budgets are healthy. Normal deployment velocity approved.",
                AffectedSlos = Array.Empty<string>(),
                Details = "Error budgets are in good shape. Continue normal deployment practices."
            }
        };
    }

    private ErrorBudgetStatus DetermineErrorBudgetStatus(double budgetRemaining)
    {
        if (budgetRemaining <= 0.0)
            return ErrorBudgetStatus.Exhausted;

        if (budgetRemaining < _options.ErrorBudgetThresholds.CriticalThreshold)
            return ErrorBudgetStatus.Critical;

        if (budgetRemaining < _options.ErrorBudgetThresholds.WarningThreshold)
            return ErrorBudgetStatus.Warning;

        return ErrorBudgetStatus.Healthy;
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

/// <summary>
/// Represents the current error budget for a Service Level Objective.
/// </summary>
public sealed class ErrorBudget
{
    /// <summary>
    /// Gets the name of the SLO.
    /// </summary>
    public required string SloName { get; init; }

    /// <summary>
    /// Gets the SLO target (e.g., 0.999 for 99.9%).
    /// </summary>
    public required double Target { get; init; }

    /// <summary>
    /// Gets the total number of requests in the measurement window.
    /// </summary>
    public required long TotalRequests { get; init; }

    /// <summary>
    /// Gets the number of failed requests (bad events).
    /// </summary>
    public required long FailedRequests { get; init; }

    /// <summary>
    /// Gets the total number of errors allowed by the error budget.
    /// </summary>
    /// <remarks>
    /// Calculated as: (1 - Target) * TotalRequests
    /// Example: For 99.9% SLO with 100,000 requests: (1 - 0.999) * 100,000 = 100 allowed errors
    /// </remarks>
    public required long AllowedErrors { get; init; }

    /// <summary>
    /// Gets the number of errors remaining in the budget.
    /// </summary>
    /// <remarks>
    /// Calculated as: AllowedErrors - FailedRequests
    /// </remarks>
    public required long RemainingErrors { get; init; }

    /// <summary>
    /// Gets the percentage of error budget remaining (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// Calculated as: RemainingErrors / AllowedErrors
    /// Example: 0.50 means 50% of the error budget remains
    /// </remarks>
    public required double BudgetRemaining { get; init; }

    /// <summary>
    /// Gets the error budget status.
    /// </summary>
    public required ErrorBudgetStatus Status { get; init; }

    /// <summary>
    /// Gets the rolling window size in days.
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// Gets the actual SLI achieved (0.0 to 1.0).
    /// </summary>
    public required double ActualSli { get; init; }

    /// <summary>
    /// Gets a value indicating whether the SLO target was met.
    /// </summary>
    public required bool SloMet { get; init; }
}

/// <summary>
/// Status of an error budget.
/// </summary>
public enum ErrorBudgetStatus
{
    /// <summary>
    /// Error budget is healthy with plenty of room remaining.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Error budget is running low (below warning threshold).
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error budget is critically low (below critical threshold).
    /// </summary>
    Critical = 2,

    /// <summary>
    /// Error budget is exhausted or exceeded.
    /// </summary>
    Exhausted = 3
}

/// <summary>
/// Deployment policy recommendation based on error budget status.
/// </summary>
public sealed class DeploymentPolicy
{
    /// <summary>
    /// Gets a value indicating whether deployments can proceed.
    /// </summary>
    public required bool CanDeploy { get; init; }

    /// <summary>
    /// Gets the deployment recommendation.
    /// </summary>
    public required DeploymentRecommendation Recommendation { get; init; }

    /// <summary>
    /// Gets the reason for this recommendation.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the SLOs affecting this policy decision.
    /// </summary>
    public required string[] AffectedSlos { get; init; }

    /// <summary>
    /// Gets additional details about the policy.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// Deployment recommendation levels.
/// </summary>
public enum DeploymentRecommendation
{
    /// <summary>
    /// Normal deployment velocity. All systems healthy.
    /// </summary>
    Normal,

    /// <summary>
    /// Proceed with caution. Reduce deployment velocity.
    /// </summary>
    Cautious,

    /// <summary>
    /// Restricted deployments. Only critical fixes.
    /// </summary>
    Restricted,

    /// <summary>
    /// Halt all non-essential deployments. Focus on reliability.
    /// </summary>
    Halt
}
