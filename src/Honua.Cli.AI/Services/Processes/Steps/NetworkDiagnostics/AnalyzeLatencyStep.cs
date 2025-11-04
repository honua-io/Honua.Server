// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Analyzes network latency and performance metrics.
/// Identifies high latency or packet loss issues.
/// </summary>
public class AnalyzeLatencyStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<AnalyzeLatencyStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public AnalyzeLatencyStep(ILogger<AnalyzeLatencyStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("AnalyzeLatency")]
    public async Task AnalyzeLatencyAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Analyzing network latency for {Host}", _state.TargetHost);
        _state.Status = "Analyzing Latency";

        // Analyze latency from previous network tests
        var avgLatency = _state.NetworkTests
            .Where(t => t.LatencyMs.HasValue)
            .Average(t => t.LatencyMs ?? 0);

        _state.LatencyMs = avgLatency;

        // Record diagnostic test
        _state.TestsRun.Add(new DiagnosticTest
        {
            TestName = "Latency Analysis",
            TestType = "Network",
            Timestamp = DateTime.UtcNow,
            Success = true,
            Output = $"Average latency: {avgLatency:F2}ms"
        });

        // Check for high latency (> 100ms)
        if (avgLatency > 100)
        {
            _logger.LogWarning("High latency detected: {Latency}ms for {Host}", avgLatency, _state.TargetHost);

            _state.Findings.Add(new Finding
            {
                Category = "Network",
                Severity = avgLatency > 500 ? "High" : "Medium",
                Description = $"High network latency detected: {avgLatency:F2}ms",
                Recommendation = "Check network path, consider using a closer region, or investigate network congestion",
                Evidence = new List<string>
                {
                    $"Average latency: {avgLatency:F2}ms",
                    $"Expected latency: < 100ms"
                }
            });
        }
        else
        {
            _logger.LogInformation("Network latency is acceptable: {Latency}ms", avgLatency);
        }

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "LatencyAnalyzed",
            Data = _state
        });
    }
}
