// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Generates a comprehensive diagnostic report with test results, findings, and remediation steps.
/// Produces a markdown-formatted report for documentation and troubleshooting.
/// </summary>
public class GenerateReportStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<GenerateReportStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public GenerateReportStep(ILogger<GenerateReportStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("GenerateReport")]
    public async Task GenerateReportAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Generating diagnostic report for {DiagnosticId}", _state.DiagnosticId);
        _state.Status = "Generating Report";

        var report = new StringBuilder();

        // Header
        report.AppendLine("# Network Diagnostics Report");
        report.AppendLine();
        report.AppendLine($"**Diagnostic ID:** {_state.DiagnosticId}");
        report.AppendLine($"**Target:** {_state.TargetHost}" + (_state.TargetPort.HasValue ? $":{_state.TargetPort}" : ""));
        report.AppendLine($"**Issue:** {_state.ReportedIssue}");
        report.AppendLine($"**Timestamp:** {_state.IssueTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"**Diagnostic Duration:** {(_state.DiagnosticCompleteTime ?? DateTime.UtcNow) - _state.DiagnosticStartTime:mm\\:ss}");
        report.AppendLine();

        // Root Cause
        report.AppendLine("## Root Cause Analysis");
        report.AppendLine();
        report.AppendLine($"**Category:** {_state.RootCauseCategory ?? "Unknown"}");
        report.AppendLine($"**Root Cause:** {_state.RootCause ?? "Not determined"}");
        report.AppendLine();

        // Recommended Fixes
        if (_state.RecommendedFixes.Any())
        {
            report.AppendLine("## Recommended Fixes");
            report.AppendLine();
            foreach (var fix in _state.RecommendedFixes)
            {
                report.AppendLine($"- {fix}");
            }
            report.AppendLine();
        }

        // Test Results Summary
        report.AppendLine("## Test Results Summary");
        report.AppendLine();
        var passedTests = _state.TestsRun.Count(t => t.Success);
        var failedTests = _state.TestsRun.Count(t => !t.Success);
        report.AppendLine($"- **Total Tests:** {_state.TestsRun.Count}");
        report.AppendLine($"- **Passed:** {passedTests}");
        report.AppendLine($"- **Failed:** {failedTests}");
        report.AppendLine();

        // Detailed Test Results
        report.AppendLine("## Detailed Test Results");
        report.AppendLine();
        foreach (var test in _state.TestsRun)
        {
            var status = test.Success ? "✓ PASS" : "✗ FAIL";
            report.AppendLine($"### {test.TestName} - {status}");
            report.AppendLine($"**Type:** {test.TestType}  ");
            report.AppendLine($"**Time:** {test.Timestamp:HH:mm:ss}  ");
            if (!test.Output.IsNullOrEmpty())
            {
                report.AppendLine($"**Output:** {test.Output}  ");
            }
            if (!test.ErrorMessage.IsNullOrEmpty())
            {
                report.AppendLine($"**Error:** {test.ErrorMessage}  ");
            }
            report.AppendLine();
        }

        // Findings
        if (_state.Findings.Any())
        {
            report.AppendLine("## Findings");
            report.AppendLine();
            foreach (var finding in _state.Findings.OrderByDescending(f => GetSeverityWeight(f.Severity)))
            {
                report.AppendLine($"### {finding.Severity}: {finding.Description}");
                report.AppendLine($"**Category:** {finding.Category}  ");
                if (!finding.Recommendation.IsNullOrEmpty())
                {
                    report.AppendLine($"**Recommendation:** {finding.Recommendation}  ");
                }
                if (finding.Evidence.Any())
                {
                    report.AppendLine("**Evidence:**");
                    foreach (var evidence in finding.Evidence)
                    {
                        report.AppendLine($"  - {evidence}");
                    }
                }
                report.AppendLine();
            }
        }

        // Network Path (if traceroute available)
        if (_state.TraceRoute.Any())
        {
            report.AppendLine("## Network Path (Traceroute)");
            report.AppendLine();
            report.AppendLine("```");
            foreach (var hop in _state.TraceRoute)
            {
                report.AppendLine(hop);
            }
            report.AppendLine("```");
            report.AppendLine();
        }

        // Performance Metrics
        if (_state.LatencyMs.HasValue || _state.TraceRoute.Any() || _state.NetworkTests.Any())
        {
            report.AppendLine("## Performance Metrics");
            report.AppendLine();

            if (_state.LatencyMs.HasValue)
            {
                report.AppendLine($"- **Average Latency:** {_state.LatencyMs:F2}ms");
            }

            if (_state.TraceRoute.Any())
            {
                report.AppendLine($"- **Hop Count:** {_state.TraceRoute.Count}");
            }

            // Include additional network test metrics
            var pingTests = _state.NetworkTests.Where(t => t.TestType == "Ping" && t.Success && t.LatencyMs.HasValue).ToList();
            if (pingTests.Any())
            {
                var avgPing = pingTests.Average(t => t.LatencyMs!.Value);
                var minPing = pingTests.Min(t => t.LatencyMs!.Value);
                var maxPing = pingTests.Max(t => t.LatencyMs!.Value);
                report.AppendLine($"- **Ping Statistics:** Min={minPing:F2}ms, Avg={avgPing:F2}ms, Max={maxPing:F2}ms");
            }

            report.AppendLine();
        }

        // DNS Information
        if (_state.DNSResults.Any())
        {
            report.AppendLine("## DNS Resolution");
            report.AppendLine();
            foreach (var dnsEntry in _state.DNSResults)
            {
                var result = dnsEntry.Value;
                var statusIcon = result.Resolved ? "✓" : "✗";
                report.AppendLine($"### {statusIcon} {dnsEntry.Key}");

                if (result.Resolved)
                {
                    report.AppendLine($"**IP Addresses:** {string.Join(", ", result.IpAddresses)}  ");
                    if (result.CnameChain.Any())
                    {
                        report.AppendLine($"**CNAME Chain:** {string.Join(" → ", result.CnameChain)}  ");
                    }
                    report.AppendLine($"**TTL:** {result.TtlSeconds}s  ");
                }
                else
                {
                    report.AppendLine($"**Error:** {result.ErrorMessage ?? "DNS resolution failed"}  ");
                }
                report.AppendLine();
            }
        }

        // SSL/TLS Information
        if (_state.SslTestResult != null)
        {
            report.AppendLine("## SSL/TLS Certificate");
            report.AppendLine();
            var ssl = _state.SslTestResult;

            if (ssl.CertificateValid)
            {
                report.AppendLine($"✓ **Certificate Status:** Valid  ");
                report.AppendLine($"**Issuer:** {ssl.Issuer}  ");
                report.AppendLine($"**Expiry Date:** {ssl.ExpiryDate:yyyy-MM-dd}  ");

                if (ssl.ExpiryDate.HasValue)
                {
                    var daysUntilExpiry = (ssl.ExpiryDate.Value - DateTime.UtcNow).Days;
                    report.AppendLine($"**Days Until Expiry:** {daysUntilExpiry}  ");
                }

                if (ssl.SubjectAlternativeNames.Any())
                {
                    report.AppendLine($"**SANs:** {string.Join(", ", ssl.SubjectAlternativeNames)}  ");
                }

                if (!string.IsNullOrEmpty(ssl.Protocol))
                {
                    report.AppendLine($"**Protocol:** {ssl.Protocol}  ");
                }

                if (!string.IsNullOrEmpty(ssl.CipherSuite))
                {
                    report.AppendLine($"**Cipher Suite:** {ssl.CipherSuite}  ");
                }
            }
            else
            {
                report.AppendLine($"✗ **Certificate Status:** Invalid  ");
                if (ssl.ValidationErrors.Any())
                {
                    report.AppendLine("**Validation Errors:**");
                    foreach (var error in ssl.ValidationErrors)
                    {
                        report.AppendLine($"  - {error}");
                    }
                }
            }
            report.AppendLine();
        }

        var reportText = report.ToString();

        _logger.LogInformation("Diagnostic report generated: {LineCount} lines, {FindingCount} findings",
            reportText.Split('\n').Length, _state.Findings.Count);

        // Update state
        _state.DiagnosticCompleteTime = DateTime.UtcNow;
        _state.Status = "Completed";

        // Emit completion event with report
        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "ReportGenerated",
            Data = new DiagnosticReport(
                _state.DiagnosticId,
                reportText,
                _state)
        });

        _logger.LogInformation("Network diagnostics process completed for {DiagnosticId}", _state.DiagnosticId);
    }

    private static int GetSeverityWeight(string severity)
    {
        return severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Medium" => 2,
            "Low" => 1,
            _ => 0
        };
    }
}

/// <summary>
/// Diagnostic report output containing the full report text and state.
/// </summary>
public record DiagnosticReport(
    string DiagnosticId,
    string ReportText,
    NetworkDiagnosticsState State);
