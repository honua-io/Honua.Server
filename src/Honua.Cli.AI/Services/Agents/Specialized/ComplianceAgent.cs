// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for compliance and regulatory requirements (SOC2, HIPAA, GDPR, PCI-DSS, etc.).
/// Analyzes deployments for compliance gaps and generates audit-ready documentation.
/// </summary>
public sealed class ComplianceAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ComplianceAgent> _logger;

    public ComplianceAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<ComplianceAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes compliance assessment request.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing compliance assessment request");

            // Analyze compliance requirements
            var requirements = await AnalyzeComplianceRequirementsAsync(request, context, cancellationToken);

            // Generate compliance assessment
            var assessment = await GenerateComplianceAssessmentAsync(requirements, context, cancellationToken);

            // Build response
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("## Compliance Assessment Report");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Compliance Frameworks:");
            foreach (var framework in requirements.Frameworks)
            {
                responseBuilder.AppendLine($"- {framework}");
            }
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Data Classification:");
            responseBuilder.AppendLine($"- **Data Types**: {string.Join(", ", requirements.DataTypes)}");
            responseBuilder.AppendLine($"- **Geographic Regions**: {string.Join(", ", requirements.GeographicRegions)}");
            responseBuilder.AppendLine();

            if (assessment.ControlsInPlace.Any())
            {
                responseBuilder.AppendLine("### ✅ Controls In Place:");
                foreach (var control in assessment.ControlsInPlace)
                {
                    responseBuilder.AppendLine($"- {control}");
                }
                responseBuilder.AppendLine();
            }

            if (assessment.ComplianceGaps.Any())
            {
                responseBuilder.AppendLine("### ⚠️  Compliance Gaps:");
                foreach (var gap in assessment.ComplianceGaps)
                {
                    responseBuilder.AppendLine($"- **{gap.Control}**: {gap.Description}");
                    responseBuilder.AppendLine($"  - Risk Level: {gap.RiskLevel}");
                    responseBuilder.AppendLine($"  - Remediation: {gap.Remediation}");
                }
                responseBuilder.AppendLine();
            }

            if (assessment.RequiredPolicies.Any())
            {
                responseBuilder.AppendLine("### Required Policies:");
                foreach (var policy in assessment.RequiredPolicies)
                {
                    responseBuilder.AppendLine($"- {policy}");
                }
                responseBuilder.AppendLine();
            }

            if (assessment.AuditRequirements.Any())
            {
                responseBuilder.AppendLine("### Audit Requirements:");
                foreach (var audit in assessment.AuditRequirements)
                {
                    responseBuilder.AppendLine($"- {audit}");
                }
                responseBuilder.AppendLine();
            }

            if (assessment.DataResidencyRecommendations.Any())
            {
                responseBuilder.AppendLine("### Data Residency Recommendations:");
                foreach (var rec in assessment.DataResidencyRecommendations)
                {
                    responseBuilder.AppendLine($"- {rec}");
                }
                responseBuilder.AppendLine();
            }

            if (assessment.EncryptionRequirements.Any())
            {
                responseBuilder.AppendLine("### Encryption Requirements:");
                foreach (var enc in assessment.EncryptionRequirements)
                {
                    responseBuilder.AppendLine($"- {enc}");
                }
                responseBuilder.AppendLine();
            }

            responseBuilder.AppendLine("### Compliance Score:");
            responseBuilder.AppendLine($"**{assessment.ComplianceScore}%** compliant");
            responseBuilder.AppendLine();

            if (assessment.NextSteps.Any())
            {
                responseBuilder.AppendLine("### Next Steps:");
                for (int i = 0; i < assessment.NextSteps.Count; i++)
                {
                    responseBuilder.AppendLine($"{i + 1}. {assessment.NextSteps[i]}");
                }
            }

            return new AgentStepResult
            {
                AgentName = "Compliance",
                Action = "ProcessComplianceAssessment",
                Success = true,
                Message = responseBuilder.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process compliance assessment request");
            return new AgentStepResult
            {
                AgentName = "Compliance",
                Action = "ProcessComplianceAssessment",
                Success = false,
                Message = $"Error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<ComplianceRequirements> AnalyzeComplianceRequirementsAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this compliance request:

User Request: {request}

Identify:
1. Compliance frameworks needed (SOC2, HIPAA, GDPR, PCI-DSS, ISO 27001, FedRAMP, etc.)
2. Types of data being handled (PII, PHI, PCI, financial, etc.)
3. Geographic regions (affects GDPR, data residency laws)
4. Industry (healthcare, finance, government, etc.)

Respond in JSON:
{{
  ""frameworks"": [""SOC2"", ""GDPR""],
  ""dataTypes"": [""PII"", ""Financial Data""],
  ""geographicRegions"": [""EU"", ""US""],
  ""industry"": ""Healthcare""
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 800,
            Temperature = 0.2
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return new ComplianceRequirements
            {
                Frameworks = new List<string> { "SOC2" },
                DataTypes = new List<string> { "PII" }
            };
        }

        var jsonStart = response.Content.IndexOf('{');
        var jsonEnd = response.Content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

            return new ComplianceRequirements
            {
                Frameworks = ExtractStringList(data, "frameworks"),
                DataTypes = ExtractStringList(data, "dataTypes"),
                GeographicRegions = ExtractStringList(data, "geographicRegions"),
                Industry = data.TryGetProperty("industry", out var ind) ? ind.GetString() ?? "General" : "General"
            };
        }

        return new ComplianceRequirements();
    }

    private async Task<ComplianceAssessment> GenerateComplianceAssessmentAsync(
        ComplianceRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate a compliance assessment for:

Frameworks: {string.Join(", ", requirements.Frameworks)}
Data Types: {string.Join(", ", requirements.DataTypes)}
Regions: {string.Join(", ", requirements.GeographicRegions)}
Industry: {requirements.Industry}

Provide:
1. Controls already in place (encryption, access control, logging, etc.)
2. Compliance gaps (missing controls, inadequate implementations)
3. Required policies (privacy policy, data retention, incident response, etc.)
4. Audit requirements (logging, monitoring, alerting)
5. Data residency recommendations
6. Encryption requirements
7. Compliance score (0-100%)
8. Next steps for remediation

Respond in JSON:
{{
  ""controlsInPlace"": [""TLS encryption in transit"", ""At-rest encryption with KMS""],
  ""complianceGaps"": [
    {{""control"": ""Access Logging"", ""description"": ""No centralized access logs"", ""riskLevel"": ""High"", ""remediation"": ""Enable CloudTrail/Activity Log""}}
  ],
  ""requiredPolicies"": [""Privacy Policy"", ""Data Retention Policy""],
  ""auditRequirements"": [""90-day log retention"", ""Real-time security alerting""],
  ""dataResidencyRecommendations"": [""Store EU data in EU regions only""],
  ""encryptionRequirements"": [""AES-256 for data at rest"", ""TLS 1.2+ for transit""],
  ""complianceScore"": 75,
  ""nextSteps"": [""Enable centralized logging"", ""Document incident response plan""]
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 2500,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        var assessment = new ComplianceAssessment();

        if (response.Success)
        {
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

                assessment.ControlsInPlace = ExtractStringList(data, "controlsInPlace");
                assessment.ComplianceGaps = ExtractComplianceGaps(data);
                assessment.RequiredPolicies = ExtractStringList(data, "requiredPolicies");
                assessment.AuditRequirements = ExtractStringList(data, "auditRequirements");
                assessment.DataResidencyRecommendations = ExtractStringList(data, "dataResidencyRecommendations");
                assessment.EncryptionRequirements = ExtractStringList(data, "encryptionRequirements");
                assessment.ComplianceScore = data.TryGetProperty("complianceScore", out var score) ? score.GetInt32() : 0;
                assessment.NextSteps = ExtractStringList(data, "nextSteps");
            }
        }

        return assessment;
    }

    private List<string> ExtractStringList(System.Text.Json.JsonElement data, string propertyName)
    {
        if (data.TryGetProperty(propertyName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return prop.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !s.IsNullOrEmpty())
                .Select(s => s!)
                .ToList();
        }
        return new List<string>();
    }

    private List<ComplianceGap> ExtractComplianceGaps(System.Text.Json.JsonElement data)
    {
        if (data.TryGetProperty("complianceGaps", out var gaps) && gaps.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return gaps.EnumerateArray()
                .Select(g => new ComplianceGap
                {
                    Control = g.TryGetProperty("control", out var c) ? c.GetString() ?? "" : "",
                    Description = g.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    RiskLevel = g.TryGetProperty("riskLevel", out var r) ? r.GetString() ?? "Medium" : "Medium",
                    Remediation = g.TryGetProperty("remediation", out var rem) ? rem.GetString() ?? "" : ""
                })
                .Where(g => !g.Control.IsNullOrEmpty())
                .ToList();
        }
        return new List<ComplianceGap>();
    }
}

public sealed class ComplianceRequirements
{
    public List<string> Frameworks { get; set; } = new();
    public List<string> DataTypes { get; set; } = new();
    public List<string> GeographicRegions { get; set; } = new();
    public string Industry { get; set; } = "General";
}

public sealed class ComplianceAssessment
{
    public List<string> ControlsInPlace { get; set; } = new();
    public List<ComplianceGap> ComplianceGaps { get; set; } = new();
    public List<string> RequiredPolicies { get; set; } = new();
    public List<string> AuditRequirements { get; set; } = new();
    public List<string> DataResidencyRecommendations { get; set; } = new();
    public List<string> EncryptionRequirements { get; set; } = new();
    public int ComplianceScore { get; set; }
    public List<string> NextSteps { get; set; } = new();
}

public sealed class ComplianceGap
{
    public string Control { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Medium";
    public string Remediation { get; set; } = string.Empty;
}
