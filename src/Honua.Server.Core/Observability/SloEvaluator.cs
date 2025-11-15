// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Background service that periodically evaluates SLO compliance and emits metrics.
/// </summary>
/// <remarks>
/// This service runs at a configurable interval (default: 5 minutes) to:
/// 1. Calculate SLO compliance over the rolling window
/// 2. Emit compliance metrics to OpenTelemetry
/// 3. Update error budget status
/// 4. Log warnings when SLOs are at risk
/// </remarks>
public sealed class SloEvaluator : BackgroundService
{
    private readonly ISliMetrics _sliMetrics;
    private readonly IErrorBudgetTracker _errorBudgetTracker;
    private readonly SreOptions _options;
    private readonly ILogger<SloEvaluator> _logger;
    private readonly Meter _meter;

    // OpenTelemetry metrics
    private readonly Gauge<double> _sloCompliance;
    private readonly Gauge<double> _sloTarget;
    private readonly Gauge<long> _sloTotalEvents;
    private readonly Gauge<long> _sloGoodEvents;
    private readonly Gauge<long> _sloBadEvents;

    public SloEvaluator(
        ISliMetrics sliMetrics,
        IErrorBudgetTracker errorBudgetTracker,
        IOptions<SreOptions> options,
        ILogger<SloEvaluator> logger)
    {
        _sliMetrics = sliMetrics ?? throw new ArgumentNullException(nameof(sliMetrics));
        _errorBudgetTracker = errorBudgetTracker ?? throw new ArgumentNullException(nameof(errorBudgetTracker));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _meter = new Meter("Honua.Server.SRE", "1.0.0");

        // Create OpenTelemetry metrics
        _sloCompliance = _meter.CreateGauge<double>(
            "honua.slo.compliance",
            unit: "{ratio}",
            description: "Current SLO compliance (0.0-1.0) over the rolling window");

        _sloTarget = _meter.CreateGauge<double>(
            "honua.slo.target",
            unit: "{ratio}",
            description: "Configured SLO target (0.0-1.0)");

        _sloTotalEvents = _meter.CreateGauge<long>(
            "honua.slo.total_events",
            unit: "{event}",
            description: "Total events in the SLO measurement window");

        _sloGoodEvents = _meter.CreateGauge<long>(
            "honua.slo.good_events",
            unit: "{event}",
            description: "Good events in the SLO measurement window");

        _sloBadEvents = _meter.CreateGauge<long>(
            "honua.slo.bad_events",
            unit: "{event}",
            description: "Bad events in the SLO measurement window");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SRE/SLO evaluation is disabled");
            return;
        }

        _logger.LogInformation(
            "SLO evaluator started. Evaluation interval: {IntervalMinutes} minutes, Rolling window: {WindowDays} days",
            _options.EvaluationIntervalMinutes,
            _options.RollingWindowDays);

        var interval = TimeSpan.FromMinutes(_options.EvaluationIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                await EvaluateSlosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SLO evaluation");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("SLO evaluator stopped");
    }

    private async Task EvaluateSlosAsync(CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromDays(_options.RollingWindowDays);
        var statistics = _sliMetrics.GetAllStatistics(window);

        if (statistics.Count == 0)
        {
            _logger.LogDebug("No SLI statistics available for evaluation");
            return;
        }

        var evaluationTime = DateTimeOffset.UtcNow;
        var sloResults = new List<SloEvaluationResult>();

        foreach (var stat in statistics)
        {
            if (!_options.Slos.TryGetValue(stat.Name, out var sloConfig) || !sloConfig.Enabled)
                continue;

            var actualSli = stat.ActualSli;
            var target = sloConfig.Target;
            var isMet = actualSli >= target;
            var margin = actualSli - target;

            var result = new SloEvaluationResult
            {
                SloName = stat.Name,
                Type = stat.Type,
                Target = target,
                ActualSli = actualSli,
                IsMet = isMet,
                Margin = margin,
                TotalEvents = stat.TotalEvents,
                GoodEvents = stat.GoodEvents,
                BadEvents = stat.BadEvents,
                EvaluationTime = evaluationTime,
                WindowDays = _options.RollingWindowDays
            };

            sloResults.Add(result);

            // Emit OpenTelemetry metrics
            _sloCompliance.Record(actualSli,
                new("slo.name", stat.Name),
                new("sli.type", stat.Type.ToString().ToLowerInvariant()),
                new("is_met", isMet.ToString().ToLowerInvariant()));

            _sloTarget.Record(target,
                new("slo.name", stat.Name),
                new("sli.type", stat.Type.ToString().ToLowerInvariant()));

            _sloTotalEvents.Record(stat.TotalEvents,
                new("slo.name", stat.Name),
                new("sli.type", stat.Type.ToString().ToLowerInvariant()));

            _sloGoodEvents.Record(stat.GoodEvents,
                new("slo.name", stat.Name),
                new("sli.type", stat.Type.ToString().ToLowerInvariant()));

            _sloBadEvents.Record(stat.BadEvents,
                new("slo.name", stat.Name),
                new("sli.type", stat.Type.ToString().ToLowerInvariant()));

            // Log warnings for at-risk SLOs
            if (!isMet)
            {
                _logger.LogWarning(
                    "SLO violation: '{SloName}' ({Type}) is at {ActualSli:P3} (target: {Target:P3}). " +
                    "{BadEvents}/{TotalEvents} events failed over {WindowDays} days.",
                    stat.Name, stat.Type, actualSli, target, stat.BadEvents, stat.TotalEvents, _options.RollingWindowDays);
            }
            else if (margin < 0.001) // Within 0.1% of target
            {
                _logger.LogWarning(
                    "SLO at risk: '{SloName}' ({Type}) is at {ActualSli:P3} (target: {Target:P3}), " +
                    "margin: {Margin:P3}. Close to violation.",
                    stat.Name, stat.Type, actualSli, target, margin);
            }
        }

        // Evaluate error budgets and deployment policy
        var deploymentPolicy = _errorBudgetTracker.GetDeploymentPolicy();

        if (deploymentPolicy.Recommendation != DeploymentRecommendation.Normal)
        {
            _logger.LogWarning(
                "Deployment policy: {Recommendation} - {Reason}",
                deploymentPolicy.Recommendation,
                deploymentPolicy.Reason);
        }

        _logger.LogInformation(
            "SLO evaluation complete: {TotalSlos} SLOs evaluated, {MetCount} met, {ViolatedCount} violated",
            sloResults.Count,
            sloResults.Count(r => r.IsMet),
            sloResults.Count(r => !r.IsMet));

        // Allow async work to proceed
        await Task.CompletedTask;
    }

    public override void Dispose()
    {
        _meter.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Result of an SLO evaluation.
/// </summary>
internal sealed class SloEvaluationResult
{
    public required string SloName { get; init; }
    public required SliType Type { get; init; }
    public required double Target { get; init; }
    public required double ActualSli { get; init; }
    public required bool IsMet { get; init; }
    public required double Margin { get; init; }
    public required long TotalEvents { get; init; }
    public required long GoodEvents { get; init; }
    public required long BadEvents { get; init; }
    public required DateTimeOffset EvaluationTime { get; init; }
    public required int WindowDays { get; init; }
}
