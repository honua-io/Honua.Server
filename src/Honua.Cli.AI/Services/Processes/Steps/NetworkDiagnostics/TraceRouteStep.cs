// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Performs traceroute to identify the network path and potential bottlenecks.
/// Shows the route packets take to reach the destination.
/// </summary>
public class TraceRouteStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<TraceRouteStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public TraceRouteStep(ILogger<TraceRouteStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("TraceRoute")]
    public async Task TraceRouteAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Running traceroute to {Host}", _state.TargetHost);
        _state.Status = "Running Traceroute";

        var traceRouteHops = new List<string>();
        bool success = true;
        string? errorMessage = null;

        try
        {
            // Determine OS and use appropriate traceroute command
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = isWindows ? "tracert" : "traceroute",
                Arguments = isWindows ? $"-h 30 -w 2000 {_state.TargetHost}" : $"-m 30 -w 2 {_state.TargetHost}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
            process.Start();

            // Read output line by line
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    traceRouteHops.Add(line.Trim());
                }
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                errorMessage = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("Traceroute command failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, errorMessage);
                success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute traceroute command");
            errorMessage = ex.Message;
            success = false;

            // Fallback: at least indicate the failure
            traceRouteHops.Add($"Error executing traceroute: {ex.Message}");
        }

        _state.TraceRoute = traceRouteHops;

        var hopCount = traceRouteHops.Count;

        // Record diagnostic test
        _state.TestsRun.Add(new DiagnosticTest
        {
            TestName = "Traceroute",
            TestType = "Network",
            Timestamp = DateTime.UtcNow,
            Success = success,
            Output = success ? $"Completed in {hopCount} hops:\n{string.Join("\n", traceRouteHops)}" : null,
            ErrorMessage = errorMessage
        });

        // Check for excessive hops (> 15)
        if (hopCount > 15)
        {
            _logger.LogWarning("Excessive hop count detected: {HopCount} hops", hopCount);

            _state.Findings.Add(new Finding
            {
                Category = "Network",
                Severity = "Medium",
                Description = $"Network path has {hopCount} hops (expected < 15)",
                Recommendation = "Review network routing, consider using a more direct network path",
                Evidence = traceRouteHops
            });
        }

        _logger.LogInformation("Traceroute completed: {HopCount} hops to {Host}", hopCount, _state.TargetHost);

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "TraceRouteComplete",
            Data = _state
        });
    }
}
