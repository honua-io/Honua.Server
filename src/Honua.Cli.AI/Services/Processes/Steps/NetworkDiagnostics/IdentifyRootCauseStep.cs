// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NetworkDiagnosticsState = Honua.Cli.AI.Services.Processes.State.NetworkDiagnosticsState;
using Honua.Cli.AI.Services.Processes.State;

namespace Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;

/// <summary>
/// Analyzes all diagnostic test results to identify the root cause of the network issue.
/// Classifies the root cause into categories: DNS, Network, Security, SSL, LoadBalancer, or Database.
/// </summary>
public class IdentifyRootCauseStep : KernelProcessStep<NetworkDiagnosticsState>
{
    private readonly ILogger<IdentifyRootCauseStep> _logger;
    private NetworkDiagnosticsState _state = new();

    public IdentifyRootCauseStep(ILogger<IdentifyRootCauseStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<NetworkDiagnosticsState> state)
    {
        _state = state.State ?? new NetworkDiagnosticsState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("IdentifyRootCause")]
    public async Task IdentifyRootCauseAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Identifying root cause for diagnostic {DiagnosticId}", _state.DiagnosticId);
        _state.Status = "Identifying Root Cause";

        // Analyze all findings to determine root cause
        var criticalFindings = _state.Findings.Where(f => f.Severity == "Critical").ToList();
        var highFindings = _state.Findings.Where(f => f.Severity == "High").ToList();
        var mediumFindings = _state.Findings.Where(f => f.Severity == "Medium").ToList();
        var lowFindings = _state.Findings.Where(f => f.Severity == "Low").ToList();

        string rootCause;
        string rootCauseCategory;
        var recommendations = new List<string>();

        // Analyze test results to provide context
        var failedTests = _state.TestsRun.Where(t => !t.Success).ToList();
        var analysisDetails = new List<string>();

        if (failedTests.Any())
        {
            analysisDetails.Add($"{failedTests.Count} test(s) failed: {string.Join(", ", failedTests.Select(t => t.TestName))}");
        }

        if (criticalFindings.Any())
        {
            // Identify the most impactful critical finding based on category priority
            var primaryFinding = PrioritizeFinding(criticalFindings);
            rootCause = BuildRootCauseDescription(primaryFinding, criticalFindings, analysisDetails);
            rootCauseCategory = primaryFinding.Category;

            if (primaryFinding.Recommendation != null)
            {
                recommendations.Add(primaryFinding.Recommendation);
            }

            _logger.LogWarning("Critical root cause identified: {RootCause} (Category: {Category})",
                rootCause, rootCauseCategory);
        }
        else if (highFindings.Any())
        {
            var primaryFinding = PrioritizeFinding(highFindings);
            rootCause = BuildRootCauseDescription(primaryFinding, highFindings, analysisDetails);
            rootCauseCategory = primaryFinding.Category;

            if (primaryFinding.Recommendation != null)
            {
                recommendations.Add(primaryFinding.Recommendation);
            }

            _logger.LogInformation("High-severity root cause identified: {RootCause} (Category: {Category})",
                rootCause, rootCauseCategory);
        }
        else if (mediumFindings.Any())
        {
            var primaryFinding = PrioritizeFinding(mediumFindings);
            rootCause = BuildRootCauseDescription(primaryFinding, mediumFindings, analysisDetails);
            rootCauseCategory = primaryFinding.Category;

            if (primaryFinding.Recommendation != null)
            {
                recommendations.Add(primaryFinding.Recommendation);
            }

            _logger.LogInformation("Medium-severity root cause identified: {RootCause} (Category: {Category})",
                rootCause, rootCauseCategory);
        }
        else if (lowFindings.Any())
        {
            var primaryFinding = PrioritizeFinding(lowFindings);
            rootCause = BuildRootCauseDescription(primaryFinding, lowFindings, analysisDetails);
            rootCauseCategory = primaryFinding.Category;

            if (primaryFinding.Recommendation != null)
            {
                recommendations.Add(primaryFinding.Recommendation);
            }

            _logger.LogInformation("Low-severity root cause identified: {RootCause} (Category: {Category})",
                rootCause, rootCauseCategory);
        }
        else
        {
            // No issues found - all tests passed
            rootCause = "No issues detected. All diagnostic tests passed successfully.";
            rootCauseCategory = "None";

            var passedTests = _state.TestsRun.Where(t => t.Success).Count();
            if (passedTests > 0)
            {
                rootCause += $" ({passedTests} test(s) completed successfully)";
            }

            recommendations.Add("Network diagnostics show no problems. If issues persist, check application-level logs and configurations.");

            _logger.LogInformation("No root cause found - all tests passed");
        }

        _state.RootCause = rootCause;
        _state.RootCauseCategory = rootCauseCategory;
        _state.RecommendedFixes = recommendations;

        // Add additional recommendations from all findings, prioritized by severity
        var orderedFindings = _state.Findings
            .OrderByDescending(f => GetSeverityWeight(f.Severity))
            .Where(f => f.Recommendation != null)
            .ToList();

        foreach (var finding in orderedFindings)
        {
            if (finding.Recommendation != null && !recommendations.Contains(finding.Recommendation))
            {
                _state.RecommendedFixes.Add(finding.Recommendation);
            }
        }

        // Record diagnostic test
        _state.TestsRun.Add(new DiagnosticTest
        {
            TestName = "Root Cause Analysis",
            TestType = "Analysis",
            Timestamp = DateTime.UtcNow,
            Success = true,
            Output = $"Root cause: {rootCause} (Category: {rootCauseCategory})"
        });

        _logger.LogInformation("Root cause analysis complete: {Category} - {RootCause}",
            rootCauseCategory, rootCause);

        await context.EmitEventAsync(new KernelProcessEvent
        {
            Id = "RootCauseIdentified",
            Data = _state
        });
    }

    /// <summary>
    /// Prioritizes findings within the same severity level based on category impact.
    /// DNS and connectivity issues are typically more fundamental than other issues.
    /// </summary>
    private Finding PrioritizeFinding(List<Finding> findings)
    {
        // Priority order: DNS > Network > SSL > Security > LoadBalancer > Database > Others
        var categoryPriority = new Dictionary<string, int>
        {
            { "DNS", 6 },
            { "Network", 5 },
            { "SSL", 4 },
            { "Security", 3 },
            { "LoadBalancer", 2 },
            { "Database", 1 }
        };

        return findings
            .OrderByDescending(f => categoryPriority.GetValueOrDefault(f.Category, 0))
            .First();
    }

    /// <summary>
    /// Builds a comprehensive root cause description with context.
    /// </summary>
    private string BuildRootCauseDescription(Finding primaryFinding, List<Finding> relatedFindings, List<string> analysisDetails)
    {
        var description = primaryFinding.Description;

        // Add context if there are multiple related findings
        if (relatedFindings.Count > 1)
        {
            description += $" (plus {relatedFindings.Count - 1} related issue(s))";
        }

        // Add analysis context if available
        if (analysisDetails.Any())
        {
            description += $". {string.Join("; ", analysisDetails)}";
        }

        return description;
    }

    /// <summary>
    /// Gets numeric weight for severity levels.
    /// </summary>
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
