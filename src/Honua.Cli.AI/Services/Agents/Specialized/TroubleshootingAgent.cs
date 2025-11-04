// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for diagnostics, error analysis, log analysis, and health check failures.
/// </summary>
public sealed class TroubleshootingAgent
{
    private readonly Kernel _kernel;

    public TroubleshootingAgent(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var diagnosticsPlugin = _kernel.Plugins["Diagnostics"];

            // Analyze the issue
            var analysis = await AnalyzeIssueAsync(request, context, cancellationToken);

            // Generate remediation steps
            var remediation = GenerateRemediationSteps(analysis);

            var message = $"Troubleshooting complete. Root cause: {analysis}. " +
                         $"Remediation: {remediation}";

            return new AgentStepResult
            {
                AgentName = "Troubleshooting",
                Action = "ProcessTroubleshootingRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "Troubleshooting",
                Action = "ProcessTroubleshootingRequest",
                Success = false,
                Message = $"Error during troubleshooting: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<string> AnalyzeIssueAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var diagnosticsPlugin = _kernel.Plugins["Diagnostics"];

        // Call diagnostics plugin to diagnose server issue
        var result = await _kernel.InvokeAsync(
            diagnosticsPlugin["DiagnoseServerIssue"],
            new KernelArguments
            {
                ["symptoms"] = request,
                ["recentLogs"] = "No recent logs provided"
            },
            cancellationToken);

        // Parse the diagnostic results
        var analysis = result.ToString();

        if (analysis.Contains("OutOfMemory") || analysis.Contains("memory"))
            return "Memory exhaustion detected";

        if (analysis.Contains("connection") || analysis.Contains("timeout"))
            return "Database connection pool exhausted";

        if (analysis.Contains("disk") || analysis.Contains("space"))
            return "Insufficient disk space";

        return "Performance degradation - requires deeper analysis";
    }

    private string GenerateRemediationSteps(string rootCause)
    {
        if (rootCause.Contains("Memory"))
            return "Increase memory allocation, enable memory profiling, check for leaks";

        if (rootCause.Contains("connection"))
            return "Increase connection pool size, add connection timeout monitoring";

        if (rootCause.Contains("disk"))
            return "Clean up old logs, increase disk capacity, enable log rotation";

        return "Review logs, monitor metrics, escalate if needed";
    }
}
