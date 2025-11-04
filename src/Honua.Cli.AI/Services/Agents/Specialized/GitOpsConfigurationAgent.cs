// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for GitOps configuration and management.
/// Guides users through setting up Git-based configuration management with automatic reconciliation.
/// </summary>
public sealed class GitOpsConfigurationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<GitOpsConfigurationAgent> _logger;

    public GitOpsConfigurationAgent(Kernel kernel, ILlmProvider? llmProvider = null, ILogger<GitOpsConfigurationAgent>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitOpsConfigurationAgent>.Instance;
    }

    /// <summary>
    /// Processes a GitOps configuration request by analyzing requirements and generating configuration.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Analyze GitOps requirements
            var analysis = await AnalyzeGitOpsRequirementsAsync(request, context, cancellationToken);

            // Generate GitOps configuration based on analysis
            var configuration = await GenerateGitOpsConfigurationAsync(analysis, context, cancellationToken);

            // Validate configuration
            var validation = await ValidateGitOpsConfigurationAsync(configuration, context, cancellationToken);

            if (!validation.IsValid)
            {
                return new AgentStepResult
                {
                    AgentName = "GitOpsConfiguration",
                    Action = "ProcessGitOpsRequest",
                    Success = false,
                    Message = $"GitOps configuration validation failed: {validation.ErrorMessage}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Save GitOps configuration
            await SaveGitOpsConfigurationAsync(configuration, context, cancellationToken);

            // Generate environment directory structure
            await GenerateEnvironmentStructureAsync(analysis, context, cancellationToken);

            // Generate reconciliation configuration
            await GenerateReconciliationConfigAsync(analysis, context, cancellationToken);

            var message = context.DryRun
                ? $"Generated GitOps configuration (dry-run): {configuration.Summary}. Repository: {configuration.RepositoryUrl}"
                : $"Applied GitOps configuration: {configuration.Summary}. Repository: {configuration.RepositoryUrl}";

            return new AgentStepResult
            {
                AgentName = "GitOpsConfiguration",
                Action = "ProcessGitOpsRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "GitOpsConfiguration",
                Action = "ProcessGitOpsRequest",
                Success = false,
                Message = $"Error processing GitOps request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Analyzes GitOps requirements from user request using LLM inference")]
    public async Task<GitOpsAnalysis> AnalyzeGitOpsRequirementsAsync(
        [Description("User's GitOps configuration request")] string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("AnalyzeGitOpsRequirementsAsync called with request: {Request}", request);

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("You are an expert in GitOps, Git-based configuration management, and continuous deployment.");
        promptBuilder.AppendLine("Analyze the following GitOps configuration request and provide structured analysis.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User Request:");
        promptBuilder.AppendLine(request);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Provide your analysis in the following JSON structure:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"repositoryUrl\": \"<Git repository URL>\",");
        promptBuilder.AppendLine("  \"branch\": \"<branch name to watch (e.g., main, production)>\",");
        promptBuilder.AppendLine("  \"environments\": [\"<list of environments (e.g., production, staging, development)>\"],");
        promptBuilder.AppendLine("  \"pollIntervalSeconds\": <polling interval in seconds (default: 30)>,");
        promptBuilder.AppendLine("  \"authenticationMethod\": \"<ssh-key|https|none>\",");
        promptBuilder.AppendLine("  \"reconciliationStrategy\": \"<automatic|manual|approval-required>\",");
        promptBuilder.AppendLine("  \"requiresSecrets\": <true|false>,");
        promptBuilder.AppendLine("  \"metadataFiles\": [\"<list of metadata files to watch>\"],");
        promptBuilder.AppendLine("  \"datasourceFiles\": [\"<list of datasource configuration files>\"],");
        promptBuilder.AppendLine("  \"summary\": \"<brief summary of the GitOps setup>\"");
        promptBuilder.AppendLine("}");

        var prompt = promptBuilder.ToString();

        if (_llmProvider != null)
        {
            // Use LLM provider directly for more control
            var llmRequest = new LlmRequest
            {
                UserPrompt = prompt,
                MaxTokens = 1024,
                Temperature = 0.3
            };

            var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);
            _logger.LogDebug("LLM response: {Response}", response.Content);

            // Extract JSON from response
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var analysis = JsonSerializer.Deserialize<GitOpsAnalysis>(jsonStr, CliJsonOptions.DevTooling) ?? new GitOpsAnalysis();

                return analysis;
            }
        }

        // Fallback: Parse request for keywords and infer configuration
        return InferGitOpsConfiguration(request);
    }

    private GitOpsAnalysis InferGitOpsConfiguration(string request)
    {
        var analysis = new GitOpsAnalysis
        {
            Branch = "main",
            Environments = new List<string> { "production" },
            PollIntervalSeconds = 30,
            AuthenticationMethod = "ssh-key",
            ReconciliationStrategy = "automatic",
            RequiresSecrets = true,
            MetadataFiles = new List<string> { "metadata.yaml" },
            DatasourceFiles = new List<string> { "datasources.json" },
            Summary = "GitOps setup with automatic reconciliation"
        };

        // Extract repository URL - check longer patterns first to avoid false matches
        var urlPatterns = new[] { "ssh://", "https://", "git@" };
        foreach (var pattern in urlPatterns)
        {
            var idx = request.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var endIdx = request.IndexOfAny(new[] { ' ', '\n', '\r' }, idx);
                analysis.RepositoryUrl = endIdx > idx
                    ? request.Substring(idx, endIdx - idx).Trim()
                    : request.Substring(idx).Trim();
                break;
            }
        }

        // Detect environments
        var envKeywords = new Dictionary<string, string>
        {
            { "production", "production" },
            { "prod", "production" },
            { "staging", "staging" },
            { "stage", "staging" },
            { "development", "development" },
            { "dev", "development" },
            { "test", "test" }
        };

        var detectedEnvs = new HashSet<string>();
        foreach (var kvp in envKeywords)
        {
            if (request.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                detectedEnvs.Add(kvp.Value);
            }
        }

        if (detectedEnvs.Count > 0)
        {
            analysis.Environments = new List<string>(detectedEnvs);
        }

        // Detect branch
        if (request.Contains("branch", StringComparison.OrdinalIgnoreCase))
        {
            var branchKeywords = new[] { "main", "master", "production", "develop" };
            foreach (var branch in branchKeywords)
            {
                if (request.Contains(branch, StringComparison.OrdinalIgnoreCase))
                {
                    analysis.Branch = branch;
                    break;
                }
            }
        }

        // Detect manual reconciliation
        if (request.Contains("manual", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("approval", StringComparison.OrdinalIgnoreCase))
        {
            analysis.ReconciliationStrategy = request.Contains("approval", StringComparison.OrdinalIgnoreCase)
                ? "approval-required"
                : "manual";
        }

        return analysis;
    }

    [KernelFunction, Description("Generates GitOps configuration files")]
    public async Task<GitOpsConfiguration> GenerateGitOpsConfigurationAsync(
        [Description("GitOps analysis results")] GitOpsAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var config = new GitOpsConfiguration
        {
            RepositoryUrl = analysis.RepositoryUrl,
            Branch = analysis.Branch,
            PollIntervalSeconds = analysis.PollIntervalSeconds,
            AuthenticationMethod = analysis.AuthenticationMethod,
            Environments = analysis.Environments,
            ReconciliationStrategy = analysis.ReconciliationStrategy,
            Summary = analysis.Summary
        };

        return config;
    }

    [KernelFunction, Description("Validates GitOps configuration")]
    public async Task<GitOpsValidationResult> ValidateGitOpsConfigurationAsync(
        [Description("GitOps configuration to validate")] GitOpsConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var result = new GitOpsValidationResult { IsValid = true };

        if (configuration.RepositoryUrl.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Repository URL is required";
            return result;
        }

        if (configuration.Branch.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Branch name is required";
            return result;
        }

        if (configuration.Environments == null || configuration.Environments.Count == 0)
        {
            result.IsValid = false;
            result.ErrorMessage = "At least one environment must be specified";
            return result;
        }

        if (configuration.PollIntervalSeconds < 5)
        {
            result.IsValid = false;
            result.ErrorMessage = "Poll interval must be at least 5 seconds";
            return result;
        }

        return result;
    }

    private async Task SaveGitOpsConfigurationAsync(
        GitOpsConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(context.WorkspacePath, "gitops-config.json");
        var json = JsonSerializer.Serialize(configuration, CliJsonOptions.Indented);

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
        _logger.LogInformation("Saved GitOps configuration to {ConfigPath}", configPath);
    }

    private async Task GenerateEnvironmentStructureAsync(
        GitOpsAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var environment in analysis.Environments)
        {
            var envPath = Path.Combine(context.WorkspacePath, "environments", environment);
            Directory.CreateDirectory(envPath);

            // Create sample metadata.yaml
            var metadataPath = Path.Combine(envPath, "metadata.yaml");
            var metadata = new
            {
                environment,
                version = "1.0.0",
                services = new[]
                {
                    new
                    {
                        name = $"{environment}-geospatial-service",
                        endpoint = $"/{environment}/geoserver",
                        layers = Array.Empty<string>()
                    }
                },
                updated = DateTime.UtcNow
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(metadata);
            await File.WriteAllTextAsync(metadataPath, yaml, cancellationToken);

            // Create sample datasources.json
            var datasourcesPath = Path.Combine(envPath, "datasources.json");
            var datasources = new
            {
                environment,
                datasources = Array.Empty<object>()
            };

            var datasourcesJson = JsonSerializer.Serialize(datasources, CliJsonOptions.Indented);
            await File.WriteAllTextAsync(datasourcesPath, datasourcesJson, cancellationToken);

            _logger.LogDebug("Created environment structure for: {Environment}", environment);
        }

        // Create common directory
        var commonPath = Path.Combine(context.WorkspacePath, "environments", "common");
        Directory.CreateDirectory(commonPath);

        var commonConfigPath = Path.Combine(commonPath, "shared-config.json");
        var commonConfig = new
        {
            shared_settings = new
            {
                cache_enabled = true,
                logging_level = "info"
            }
        };

        var commonJson = JsonSerializer.Serialize(commonConfig, CliJsonOptions.Indented);
        await File.WriteAllTextAsync(commonConfigPath, commonJson, cancellationToken);
    }

    private async Task GenerateReconciliationConfigAsync(
        GitOpsAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var reconciliationConfigPath = Path.Combine(context.WorkspacePath, "reconciliation-config.json");
        var config = new
        {
            strategy = analysis.ReconciliationStrategy,
            autoApply = analysis.ReconciliationStrategy == "automatic",
            requiresApproval = analysis.ReconciliationStrategy == "approval-required",
            notifications = new
            {
                onSync = true,
                onError = true
            }
        };

        var json = JsonSerializer.Serialize(config, CliJsonOptions.Indented);
        await File.WriteAllTextAsync(reconciliationConfigPath, json, cancellationToken);

        _logger.LogInformation("Created reconciliation configuration");
    }
}

/// <summary>
/// Analysis results from GitOps requirements
/// </summary>
public class GitOpsAnalysis
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public List<string> Environments { get; set; } = new();
    public int PollIntervalSeconds { get; set; } = 30;
    public string AuthenticationMethod { get; set; } = "ssh-key";
    public string ReconciliationStrategy { get; set; } = "automatic";
    public bool RequiresSecrets { get; set; }
    public List<string> MetadataFiles { get; set; } = new();
    public List<string> DatasourceFiles { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Generated GitOps configuration
/// </summary>
public class GitOpsConfiguration
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public int PollIntervalSeconds { get; set; } = 30;
    public string AuthenticationMethod { get; set; } = "ssh-key";
    public List<string> Environments { get; set; } = new();
    public string ReconciliationStrategy { get; set; } = "automatic";
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// GitOps configuration validation result
/// </summary>
public class GitOpsValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
