// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Collects symptoms and initial information about the network issue.
/// Gathers error messages, affected endpoints, and timestamps.
/// </summary>
public class CollectSymptomsStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<CollectSymptomsStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public CollectSymptomsStep(ILogger<CollectSymptomsStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("CollectSymptoms")]
    public async Task CollectSymptomsAsync(
        KernelProcessStepContext context,
        NetworkDiagnosticsRequest request)
    {
        _logger.LogInformation("Collecting symptoms for network diagnostic: {Issue}", request.ReportedIssue);

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    // Initialize state
                    _state.DiagnosticId = Guid.NewGuid().ToString();
                    _state.ReportedIssue = request.ReportedIssue;
                    _state.IssueTimestamp = DateTime.UtcNow;
                    _state.TargetHost = request.TargetHost;
                    _state.TargetPort = request.TargetPort;
                    _state.AffectedEndpoints = request.AffectedEndpoints ?? new List<string>();
                    _state.DiagnosticStartTime = DateTime.UtcNow;
                    _state.Status = "Collecting Symptoms";

                    // Parse symptoms from reported issue
                    _state.Symptoms = new List<string>
                    {
                        $"Reported issue: {request.ReportedIssue}",
                        $"Target: {request.TargetHost}" + (request.TargetPort.HasValue ? $":{request.TargetPort}" : ""),
                        $"Issue timestamp: {_state.IssueTimestamp:yyyy-MM-dd HH:mm:ss} UTC"
                    };

                    if (_state.AffectedEndpoints.Any())
                    {
                        _state.Symptoms.Add($"Affected endpoints: {string.Join(", ", _state.AffectedEndpoints)}");
                    }

                    _logger.LogInformation("Collected {Count} symptoms for diagnostic {DiagnosticId}",
                        _state.Symptoms.Count, _state.DiagnosticId);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "SymptomsCollected",
                        Data = _state
                    });
                },
                _logger,
                "CollectSymptoms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect symptoms after retries");
            _state.Status = "Symptom Collection Failed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "SymptomCollectionFailed",
                Data = new { Error = ex.Message }
            });
        }
    }
}

/// <summary>
/// Request object for network diagnostics.
/// </summary>
public record NetworkDiagnosticsRequest(
    string ReportedIssue,
    string TargetHost,
    int? TargetPort = null,
    List<string>? AffectedEndpoints = null);
