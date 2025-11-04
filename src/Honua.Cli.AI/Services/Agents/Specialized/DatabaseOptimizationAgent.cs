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
/// Specialized agent for database optimization, performance tuning, and best practices.
/// Analyzes database configurations, query patterns, indexing strategies, and suggests optimizations.
/// </summary>
public sealed class DatabaseOptimizationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<DatabaseOptimizationAgent> _logger;

    public DatabaseOptimizationAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<DatabaseOptimizationAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes database optimization request and generates recommendations.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing database optimization request");

            // Analyze database configuration
            var analysis = await AnalyzeDatabaseConfigurationAsync(request, context, cancellationToken);

            // Generate optimization recommendations
            var recommendations = await GenerateOptimizationRecommendationsAsync(analysis, context, cancellationToken);

            // Build response
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("## Database Optimization Analysis");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Database Configuration:");
            responseBuilder.AppendLine($"- **Database Type**: {analysis.DatabaseType}");
            responseBuilder.AppendLine($"- **Instance Size**: {analysis.InstanceSize}");
            responseBuilder.AppendLine($"- **Storage Type**: {analysis.StorageType}");
            responseBuilder.AppendLine($"- **Expected Load**: {analysis.ExpectedLoad}");
            responseBuilder.AppendLine();

            if (recommendations.PerformanceOptimizations.Any())
            {
                responseBuilder.AppendLine("### Performance Optimizations:");
                foreach (var opt in recommendations.PerformanceOptimizations)
                {
                    responseBuilder.AppendLine($"- {opt}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.IndexingRecommendations.Any())
            {
                responseBuilder.AppendLine("### Indexing Recommendations:");
                foreach (var rec in recommendations.IndexingRecommendations)
                {
                    responseBuilder.AppendLine($"- {rec}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.ConnectionPoolingSettings.Any())
            {
                responseBuilder.AppendLine("### Connection Pooling:");
                foreach (var setting in recommendations.ConnectionPoolingSettings)
                {
                    responseBuilder.AppendLine($"- {setting}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.BackupStrategy.Any())
            {
                responseBuilder.AppendLine("### Backup Strategy:");
                foreach (var strategy in recommendations.BackupStrategy)
                {
                    responseBuilder.AppendLine($"- {strategy}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.CostOptimizations.Any())
            {
                responseBuilder.AppendLine("### Cost Optimizations:");
                foreach (var cost in recommendations.CostOptimizations)
                {
                    responseBuilder.AppendLine($"- {cost}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.Warnings.Any())
            {
                responseBuilder.AppendLine("### ⚠️  Warnings:");
                foreach (var warning in recommendations.Warnings)
                {
                    responseBuilder.AppendLine($"- {warning}");
                }
            }

            return new AgentStepResult
            {
                AgentName = "DatabaseOptimization",
                Action = "ProcessDatabaseOptimization",
                Success = true,
                Message = responseBuilder.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process database optimization request");
            return new AgentStepResult
            {
                AgentName = "DatabaseOptimization",
                Action = "ProcessDatabaseOptimization",
                Success = false,
                Message = $"Error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<DatabaseAnalysis> AnalyzeDatabaseConfigurationAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this database configuration request and extract key information:

User Request: {request}

Determine:
1. Database type (PostgreSQL, MySQL, SQL Server, MongoDB, etc.)
2. Instance size (db.t3.micro, Standard_D2s_v3, etc.)
3. Storage type (gp3, Premium SSD, etc.)
4. Expected load (transactions/second, connections, data size)
5. Primary use case (OLTP, OLAP, hybrid, time-series, spatial)
6. High availability requirements
7. Backup requirements

Respond in JSON:
{{
  ""databaseType"": ""PostgreSQL"",
  ""instanceSize"": ""db.r5.xlarge"",
  ""storageType"": ""gp3"",
  ""expectedLoad"": ""1000 TPS"",
  ""useCase"": ""OLTP with spatial data"",
  ""highAvailability"": true,
  ""backupRetention"": 30
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
            throw new InvalidOperationException("LLM analysis failed");
        }

        // Parse JSON response
        var jsonStart = response.Content.IndexOf('{');
        var jsonEnd = response.Content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

            return new DatabaseAnalysis
            {
                DatabaseType = data.TryGetProperty("databaseType", out var dbType) ? dbType.GetString() ?? "PostgreSQL" : "PostgreSQL",
                InstanceSize = data.TryGetProperty("instanceSize", out var size) ? size.GetString() ?? "db.t3.medium" : "db.t3.medium",
                StorageType = data.TryGetProperty("storageType", out var storage) ? storage.GetString() ?? "gp3" : "gp3",
                ExpectedLoad = data.TryGetProperty("expectedLoad", out var load) ? load.GetString() ?? "unknown" : "unknown",
                UseCase = data.TryGetProperty("useCase", out var useCase) ? useCase.GetString() ?? "OLTP" : "OLTP",
                HighAvailability = data.TryGetProperty("highAvailability", out var ha) && ha.GetBoolean(),
                BackupRetention = data.TryGetProperty("backupRetention", out var retention) ? retention.GetInt32() : 7
            };
        }

        return new DatabaseAnalysis { DatabaseType = "PostgreSQL" };
    }

    private async Task<DatabaseOptimizationRecommendations> GenerateOptimizationRecommendationsAsync(
        DatabaseAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate database optimization recommendations:

Database Type: {analysis.DatabaseType}
Instance Size: {analysis.InstanceSize}
Storage Type: {analysis.StorageType}
Expected Load: {analysis.ExpectedLoad}
Use Case: {analysis.UseCase}
High Availability: {analysis.HighAvailability}

Provide recommendations for:
1. Performance optimizations (query tuning, caching, read replicas)
2. Indexing strategies
3. Connection pooling settings
4. Backup strategy
5. Cost optimizations
6. Potential issues/warnings

Respond in JSON:
{{
  ""performanceOptimizations"": [""Enable read replicas for read-heavy workloads""],
  ""indexingRecommendations"": [""Create GIST index on spatial columns""],
  ""connectionPoolingSettings"": [""Set max_connections=200, pool size 50""],
  ""backupStrategy"": [""Automated daily backups with 30-day retention""],
  ""costOptimizations"": [""Use Reserved Instances for 40% savings""],
  ""warnings"": [""Instance may be oversized for current load""]
}}";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        var recommendations = new DatabaseOptimizationRecommendations();

        if (response.Success)
        {
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(jsonStr);

                recommendations.PerformanceOptimizations = ExtractStringList(data, "performanceOptimizations");
                recommendations.IndexingRecommendations = ExtractStringList(data, "indexingRecommendations");
                recommendations.ConnectionPoolingSettings = ExtractStringList(data, "connectionPoolingSettings");
                recommendations.BackupStrategy = ExtractStringList(data, "backupStrategy");
                recommendations.CostOptimizations = ExtractStringList(data, "costOptimizations");
                recommendations.Warnings = ExtractStringList(data, "warnings");
            }
        }

        return recommendations;
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

public sealed class DatabaseAnalysis
{
    public string DatabaseType { get; set; } = "PostgreSQL";
    public string InstanceSize { get; set; } = "db.t3.medium";
    public string StorageType { get; set; } = "gp3";
    public string ExpectedLoad { get; set; } = "unknown";
    public string UseCase { get; set; } = "OLTP";
    public bool HighAvailability { get; set; }
    public int BackupRetention { get; set; } = 7;
}

public sealed class DatabaseOptimizationRecommendations
{
    public List<string> PerformanceOptimizations { get; set; } = new();
    public List<string> IndexingRecommendations { get; set; } = new();
    public List<string> ConnectionPoolingSettings { get; set; } = new();
    public List<string> BackupStrategy { get; set; } = new();
    public List<string> CostOptimizations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
