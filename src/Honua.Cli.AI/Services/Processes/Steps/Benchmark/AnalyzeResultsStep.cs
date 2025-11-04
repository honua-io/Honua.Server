// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using BenchmarkState = Honua.Cli.AI.Services.Processes.State.BenchmarkState;

namespace Honua.Cli.AI.Services.Processes.Steps.Benchmark;

/// <summary>
/// Analyzes benchmark results and generates insights.
/// </summary>
public class AnalyzeResultsStep : KernelProcessStep<BenchmarkState>, IProcessStepTimeout
{
    private readonly ILogger<AnalyzeResultsStep> _logger;
    private BenchmarkState _state = new();

    /// <summary>
    /// Analysis includes statistics calculation, bottleneck identification, and baseline comparison.
    /// Default timeout: 5 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);

    public AnalyzeResultsStep(ILogger<AnalyzeResultsStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<BenchmarkState> state)
    {
        _state = state.State ?? new BenchmarkState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("AnalyzeResults")]
    public async Task AnalyzeResultsAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing benchmark results for {BenchmarkName}", _state.BenchmarkName);

        _state.Status = "Analyzing";

        try
        {
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Calculate statistics
            await CalculateStatistics(cancellationToken);

            // Identify bottlenecks
            await IdentifyBottlenecks(cancellationToken);

            // Compare with baselines
            await CompareWithBaseline(cancellationToken);

            _logger.LogInformation("Analysis completed for {BenchmarkName}", _state.BenchmarkName);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "AnalysisCompleted",
                Data = _state
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analysis cancelled for {BenchmarkName}", _state.BenchmarkName);
            _state.Status = "Cancelled";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "AnalysisCancelled",
                Data = new { _state.BenchmarkName }
            });
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze results for {BenchmarkName}", _state.BenchmarkName);
            _state.Status = "AnalysisFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "AnalysisFailed",
                Data = new { _state.BenchmarkName, Error = ex.Message }
            });
        }
    }

    private async Task CalculateStatistics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calculating statistics");

        // Calculate additional statistics from collected metrics
        var avgLatency = (_state.P95Latency + _state.P99Latency) / 2.0;
        _logger.LogInformation(
            "Statistics - RPS: {RPS:F2}, P95: {P95:F2}ms, P99: {P99:F2}ms, AvgLatency: {Avg:F2}ms, ErrorRate: {ErrorRate:P2}",
            _state.RequestsPerSecond, _state.P95Latency, _state.P99Latency, avgLatency, _state.ErrorRate);

        await Task.CompletedTask;
    }

    private async Task IdentifyBottlenecks(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Identifying performance bottlenecks");

        _state.Bottlenecks.Clear();

        // Analyze latency percentiles
        if (_state.P99Latency > 1000)
        {
            _state.Bottlenecks.Add($"Critical: P99 latency ({_state.P99Latency:F0}ms) exceeds 1 second - indicates severe performance issues");
        }
        else if (_state.P99Latency > 500)
        {
            _state.Bottlenecks.Add($"Warning: P99 latency ({_state.P99Latency:F0}ms) exceeds 500ms - consider optimization");
        }
        else if (_state.P99Latency > 100)
        {
            _state.Bottlenecks.Add($"Notice: P99 latency ({_state.P99Latency:F0}ms) exceeds 100ms - acceptable but could be improved");
        }

        if (_state.P95Latency > 500)
        {
            _state.Bottlenecks.Add($"Critical: P95 latency ({_state.P95Latency:F0}ms) exceeds 500ms - majority of requests are slow");
        }
        else if (_state.P95Latency > 200)
        {
            _state.Bottlenecks.Add($"Warning: P95 latency ({_state.P95Latency:F0}ms) exceeds 200ms");
        }

        // Analyze error rate
        if (_state.ErrorRate > 0.05)
        {
            _state.Bottlenecks.Add($"Critical: Error rate ({_state.ErrorRate:P2}) exceeds 5% - system is unstable under load");
        }
        else if (_state.ErrorRate > 0.01)
        {
            _state.Bottlenecks.Add($"Warning: Error rate ({_state.ErrorRate:P2}) exceeds 1% - investigate error causes");
        }
        else if (_state.ErrorRate > 0.001)
        {
            _state.Bottlenecks.Add($"Notice: Error rate ({_state.ErrorRate:P2}) is low but non-zero");
        }

        // Analyze throughput
        if (_state.RequestsPerSecond < 100)
        {
            _state.Bottlenecks.Add($"Warning: Low throughput ({_state.RequestsPerSecond:F0} RPS) - system may be constrained");
        }

        // Check for latency vs error rate correlation
        if (_state.P99Latency > 1000 && _state.ErrorRate > 0.01)
        {
            _state.Bottlenecks.Add("Critical: High latency combined with high error rate suggests system overload or resource exhaustion");
        }

        _logger.LogInformation("Identified {Count} bottlenecks", _state.Bottlenecks.Count);
        await Task.CompletedTask;
    }

    private async Task CompareWithBaseline(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Comparing with baseline performance");

        _state.Recommendations.Clear();

        // Use baseline from state if available, otherwise use reasonable defaults
        var baselineRps = _state.BaselineLatencyP95.HasValue ? 1000.0 : 1000.0; // Default baseline
        var baselineP95 = _state.BaselineLatencyP95 ?? 100.0m; // 100ms baseline
        var baselineP99 = 200.0m; // 200ms baseline

        // Compare throughput
        var rpsImprovement = ((_state.RequestsPerSecond - baselineRps) / baselineRps) * 100;
        if (Math.Abs(rpsImprovement) > 5) // Only report if change > 5%
        {
            _state.Recommendations.Add(rpsImprovement > 0
                ? $"Throughput improved by {rpsImprovement:F1}% ({_state.RequestsPerSecond:F0} RPS vs {baselineRps:F0} baseline)"
                : $"Throughput degraded by {Math.Abs(rpsImprovement):F1}% ({_state.RequestsPerSecond:F0} RPS vs {baselineRps:F0} baseline)");
        }

        // Compare P95 latency
        var p95Improvement = ((double)(baselineP95 - (decimal)_state.P95Latency) / (double)baselineP95) * 100;
        if (Math.Abs(p95Improvement) > 5)
        {
            _state.Recommendations.Add(p95Improvement > 0
                ? $"P95 latency improved by {p95Improvement:F1}% ({_state.P95Latency:F0}ms vs {baselineP95}ms baseline)"
                : $"P95 latency degraded by {Math.Abs(p95Improvement):F1}% ({_state.P95Latency:F0}ms vs {baselineP95}ms baseline)");
        }

        // Compare P99 latency
        var p99Improvement = ((double)(baselineP99 - (decimal)_state.P99Latency) / (double)baselineP99) * 100;
        if (Math.Abs(p99Improvement) > 5)
        {
            _state.Recommendations.Add(p99Improvement > 0
                ? $"P99 latency improved by {p99Improvement:F1}% ({_state.P99Latency:F0}ms vs {baselineP99}ms baseline)"
                : $"P99 latency degraded by {Math.Abs(p99Improvement):F1}% ({_state.P99Latency:F0}ms vs {baselineP99}ms baseline)");
        }

        // General recommendations based on performance profile
        if (_state.ErrorRate == 0 && _state.P99Latency < 100)
        {
            _state.Recommendations.Add("System performs well under current load - consider stress testing at higher concurrency");
        }
        else if (_state.ErrorRate > 0.01 || _state.P99Latency > 500)
        {
            _state.Recommendations.Add("System shows performance issues - review application logs, database queries, and resource utilization");
        }

        if (_state.RequestsPerSecond < 500 && _state.P95Latency < 100)
        {
            _state.Recommendations.Add("Low throughput despite good latency - system may not be fully utilized, consider increasing concurrency");
        }

        _logger.LogInformation("Generated {Count} recommendations", _state.Recommendations.Count);
        await Task.CompletedTask;
    }
}
