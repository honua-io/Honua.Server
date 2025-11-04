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
/// Specialized agent for disaster recovery (DR) planning and business continuity.
/// Designs multi-region failover strategies, backup procedures, and recovery testing plans.
/// </summary>
public sealed class DisasterRecoveryAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<DisasterRecoveryAgent> _logger;

    public DisasterRecoveryAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<DisasterRecoveryAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes disaster recovery planning request.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing disaster recovery planning request");

            // Analyze DR requirements
            var requirements = await AnalyzeDisasterRecoveryRequirementsAsync(request, context, cancellationToken);

            // Generate DR strategy
            var strategy = await GenerateDisasterRecoveryStrategyAsync(requirements, context, cancellationToken);

            // Build response
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("## Disaster Recovery Plan");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Requirements:");
            responseBuilder.AppendLine($"- **RTO (Recovery Time Objective)**: {requirements.Rto}");
            responseBuilder.AppendLine($"- **RPO (Recovery Point Objective)**: {requirements.Rpo}");
            responseBuilder.AppendLine($"- **Primary Region**: {requirements.PrimaryRegion}");
            responseBuilder.AppendLine($"- **DR Region(s)**: {string.Join(", ", requirements.DrRegions)}");
            responseBuilder.AppendLine($"- **Critical Services**: {string.Join(", ", requirements.CriticalServices)}");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### DR Strategy:");
            responseBuilder.AppendLine($"**Strategy Type**: {strategy.StrategyType}");
            responseBuilder.AppendLine();

            if (strategy.BackupProcedures.Any())
            {
                responseBuilder.AppendLine("### Backup Procedures:");
                foreach (var procedure in strategy.BackupProcedures)
                {
                    responseBuilder.AppendLine($"- {procedure}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.FailoverSteps.Any())
            {
                responseBuilder.AppendLine("### Failover Steps:");
                for (int i = 0; i < strategy.FailoverSteps.Count; i++)
                {
                    responseBuilder.AppendLine($"{i + 1}. {strategy.FailoverSteps[i]}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.DataReplicationStrategy.Any())
            {
                responseBuilder.AppendLine("### Data Replication:");
                foreach (var replication in strategy.DataReplicationStrategy)
                {
                    responseBuilder.AppendLine($"- {replication}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.TestingPlan.Any())
            {
                responseBuilder.AppendLine("### DR Testing Plan:");
                foreach (var test in strategy.TestingPlan)
                {
                    responseBuilder.AppendLine($"- {test}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.MonitoringAndAlerts.Any())
            {
                responseBuilder.AppendLine("### Monitoring & Alerts:");
                foreach (var monitor in strategy.MonitoringAndAlerts)
                {
                    responseBuilder.AppendLine($"- {monitor}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.CostEstimate.Any())
            {
                responseBuilder.AppendLine("### Cost Estimate:");
                foreach (var cost in strategy.CostEstimate)
                {
                    responseBuilder.AppendLine($"- {cost}");
                }
                responseBuilder.AppendLine();
            }

            if (strategy.Warnings.Any())
            {
                responseBuilder.AppendLine("### ⚠️  Important Considerations:");
                foreach (var warning in strategy.Warnings)
                {
                    responseBuilder.AppendLine($"- {warning}");
                }
            }

            return new AgentStepResult
            {
                AgentName = "DisasterRecovery",
                Action = "ProcessDisasterRecoveryPlan",
                Success = true,
                Message = responseBuilder.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process disaster recovery planning request");
            return new AgentStepResult
            {
                AgentName = "DisasterRecovery",
                Action = "ProcessDisasterRecoveryPlan",
                Success = false,
                Message = $"Error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<DisasterRecoveryRequirements> AnalyzeDisasterRecoveryRequirementsAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this disaster recovery planning request:

User Request: {request}

Extract:
1. RTO (Recovery Time Objective) - how quickly must service be restored
2. RPO (Recovery Point Objective) - acceptable data loss window
3. Primary region/location
4. Desired DR region(s)
5. Critical services that must be protected
6. Compliance requirements (if any)

Respond in JSON:
{{
  ""rto"": ""4 hours"",
  ""rpo"": ""15 minutes"",
  ""primaryRegion"": ""us-east-1"",
  ""drRegions"": [""us-west-2""],
  ""criticalServices"": [""Database"", ""API"", ""Authentication""],
  ""complianceRequirements"": [""SOC2"", ""HIPAA""]
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.2
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return new DisasterRecoveryRequirements
            {
                Rto = "4 hours",
                Rpo = "1 hour",
                PrimaryRegion = "us-east-1",
                DrRegions = new List<string> { "us-west-2" }
            };
        }

        var jsonStart = response.Content.IndexOf('{');
        var jsonEnd = response.Content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

            return new DisasterRecoveryRequirements
            {
                Rto = data.TryGetProperty("rto", out var rto) ? rto.GetString() ?? "4 hours" : "4 hours",
                Rpo = data.TryGetProperty("rpo", out var rpo) ? rpo.GetString() ?? "1 hour" : "1 hour",
                PrimaryRegion = data.TryGetProperty("primaryRegion", out var primary) ? primary.GetString() ?? "us-east-1" : "us-east-1",
                DrRegions = ExtractStringList(data, "drRegions"),
                CriticalServices = ExtractStringList(data, "criticalServices"),
                ComplianceRequirements = ExtractStringList(data, "complianceRequirements")
            };
        }

        return new DisasterRecoveryRequirements();
    }

    private async Task<DisasterRecoveryStrategy> GenerateDisasterRecoveryStrategyAsync(
        DisasterRecoveryRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate a comprehensive disaster recovery strategy:

Requirements:
- RTO: {requirements.Rto}
- RPO: {requirements.Rpo}
- Primary Region: {requirements.PrimaryRegion}
- DR Regions: {string.Join(", ", requirements.DrRegions)}
- Critical Services: {string.Join(", ", requirements.CriticalServices)}

Provide:
1. Strategy type (Hot Standby, Warm Standby, Cold Standby, Pilot Light, Multi-Region Active-Active)
2. Backup procedures
3. Step-by-step failover process
4. Data replication strategy
5. DR testing plan
6. Monitoring and alerting
7. Cost estimate (rough monthly cost)
8. Important warnings/considerations

Respond in JSON:
{{
  ""strategyType"": ""Warm Standby"",
  ""backupProcedures"": [""Automated daily database snapshots""],
  ""failoverSteps"": [""1. Detect outage"", ""2. Update DNS to DR region""],
  ""dataReplicationStrategy"": [""PostgreSQL streaming replication to DR region""],
  ""testingPlan"": [""Quarterly DR drills""],
  ""monitoringAndAlerts"": [""Health check monitoring with 5-min intervals""],
  ""costEstimate"": [""DR infrastructure: $500/month""],
  ""warnings"": [""DNS TTL should be 60s for faster failover""]
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 2000,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        var strategy = new DisasterRecoveryStrategy();

        if (response.Success)
        {
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

                strategy.StrategyType = data.TryGetProperty("strategyType", out var type) ? type.GetString() ?? "Warm Standby" : "Warm Standby";
                strategy.BackupProcedures = ExtractStringList(data, "backupProcedures");
                strategy.FailoverSteps = ExtractStringList(data, "failoverSteps");
                strategy.DataReplicationStrategy = ExtractStringList(data, "dataReplicationStrategy");
                strategy.TestingPlan = ExtractStringList(data, "testingPlan");
                strategy.MonitoringAndAlerts = ExtractStringList(data, "monitoringAndAlerts");
                strategy.CostEstimate = ExtractStringList(data, "costEstimate");
                strategy.Warnings = ExtractStringList(data, "warnings");
            }
        }

        return strategy;
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
}

public sealed class DisasterRecoveryRequirements
{
    public string Rto { get; set; } = "4 hours";
    public string Rpo { get; set; } = "1 hour";
    public string PrimaryRegion { get; set; } = "us-east-1";
    public List<string> DrRegions { get; set; } = new();
    public List<string> CriticalServices { get; set; } = new();
    public List<string> ComplianceRequirements { get; set; } = new();
}

public sealed class DisasterRecoveryStrategy
{
    public string StrategyType { get; set; } = "Warm Standby";
    public List<string> BackupProcedures { get; set; } = new();
    public List<string> FailoverSteps { get; set; } = new();
    public List<string> DataReplicationStrategy { get; set; } = new();
    public List<string> TestingPlan { get; set; } = new();
    public List<string> MonitoringAndAlerts { get; set; } = new();
    public List<string> CostEstimate { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
