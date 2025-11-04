// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for blue-green and canary deployment strategies.
/// Supports zero-downtime deployments with traffic shifting using YARP reverse proxy.
/// </summary>
public sealed class BlueGreenDeploymentAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<BlueGreenDeploymentAgent> _logger;

    public BlueGreenDeploymentAgent(Kernel kernel, ILlmProvider? llmProvider = null, ILogger<BlueGreenDeploymentAgent>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BlueGreenDeploymentAgent>.Instance;
    }

    /// <summary>
    /// Processes a blue-green deployment request by analyzing requirements and generating configuration.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Analyze deployment requirements
            var analysis = await AnalyzeDeploymentRequirementsAsync(request, context, cancellationToken);

            // Generate deployment configuration based on analysis
            var configuration = await GenerateDeploymentConfigurationAsync(analysis, context, cancellationToken);

            // Validate configuration
            var validation = await ValidateDeploymentConfigurationAsync(configuration, context, cancellationToken);

            if (!validation.IsValid)
            {
                return new AgentStepResult
                {
                    AgentName = "BlueGreenDeployment",
                    Action = "ProcessDeploymentRequest",
                    Success = false,
                    Message = $"Deployment configuration validation failed: {validation.ErrorMessage}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Save deployment configuration
            await SaveDeploymentConfigurationAsync(configuration, context, cancellationToken);

            var message = context.DryRun
                ? $"Generated deployment configuration (dry-run): {configuration.Summary}. Strategy: {configuration.Strategy}"
                : $"Applied deployment configuration: {configuration.Summary}. Strategy: {configuration.Strategy}";

            return new AgentStepResult
            {
                AgentName = "BlueGreenDeployment",
                Action = "ProcessDeploymentRequest",
                Success = true,
                Message = message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "BlueGreenDeployment",
                Action = "ProcessDeploymentRequest",
                Success = false,
                Message = $"Error processing deployment request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Analyzes deployment requirements from user request using LLM inference")]
    public async Task<BlueGreenAnalysis> AnalyzeDeploymentRequirementsAsync(
        [Description("User's deployment request")] string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("AnalyzeDeploymentRequirementsAsync called with request: {Request}", request);

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("You are an expert in deployment strategies, blue-green deployments, canary releases, and traffic management.");
        promptBuilder.AppendLine("Analyze the following deployment request and provide structured analysis.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User Request:");
        promptBuilder.AppendLine(request);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Provide your analysis in the following JSON structure:");
        promptBuilder.AppendLine("{");
        promptBuilder.AppendLine("  \"strategy\": \"<blue-green|canary|rolling>\",");
        promptBuilder.AppendLine("  \"platform\": \"<kubernetes|docker|azure-container-apps|aws-ecs|gcp-cloud-run>\",");
        promptBuilder.AppendLine("  \"serviceName\": \"<name of the service being deployed>\",");
        promptBuilder.AppendLine("  \"blueEnvironment\": {");
        promptBuilder.AppendLine("    \"name\": \"<blue environment name>\",");
        promptBuilder.AppendLine("    \"endpoint\": \"<blue environment endpoint>\"");
        promptBuilder.AppendLine("  },");
        promptBuilder.AppendLine("  \"greenEnvironment\": {");
        promptBuilder.AppendLine("    \"name\": \"<green environment name>\",");
        promptBuilder.AppendLine("    \"endpoint\": \"<green environment endpoint>\"");
        promptBuilder.AppendLine("  },");
        promptBuilder.AppendLine("  \"trafficSplitPercentage\": <percentage of traffic to green (0-100)>,");
        promptBuilder.AppendLine("  \"healthCheckPath\": \"<health check endpoint path>\",");
        promptBuilder.AppendLine("  \"rollbackOnFailure\": <true|false>,");
        promptBuilder.AppendLine("  \"summary\": \"<brief summary of the deployment strategy>\"");
        promptBuilder.AppendLine("}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Notes:");
        promptBuilder.AppendLine("- Blue-green: instant switchover (0% or 100%)");
        promptBuilder.AppendLine("- Canary: gradual rollout (5%, 25%, 50%, 100%)");
        promptBuilder.AppendLine("- Rolling: sequential update");

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
                var analysis = JsonSerializer.Deserialize<BlueGreenAnalysis>(jsonStr,
                    CliJsonOptions.DevTooling) ?? new BlueGreenAnalysis();

                return analysis;
            }
        }

        // Fallback: Parse request for keywords and infer configuration
        return InferDeploymentConfiguration(request);
    }

    private BlueGreenAnalysis InferDeploymentConfiguration(string request)
    {
        var analysis = new BlueGreenAnalysis
        {
            Strategy = "blue-green",
            Platform = "kubernetes",
            ServiceName = "honua-service",
            BlueEnvironment = new BlueGreenEnvironment
            {
                Name = "blue",
                Endpoint = "http://blue-service:8080"
            },
            GreenEnvironment = new BlueGreenEnvironment
            {
                Name = "green",
                Endpoint = "http://green-service:8080"
            },
            TrafficSplitPercentage = 0,
            HealthCheckPath = "/health",
            RollbackOnFailure = true,
            Summary = "Blue-green deployment with zero downtime"
        };

        // Detect strategy
        if (request.Contains("canary", StringComparison.OrdinalIgnoreCase))
        {
            analysis.Strategy = "canary";
            analysis.TrafficSplitPercentage = 10; // Start with 10% for canary
            analysis.Summary = "Canary deployment with gradual rollout";
        }
        else if (request.Contains("rolling", StringComparison.OrdinalIgnoreCase))
        {
            analysis.Strategy = "rolling";
            analysis.Summary = "Rolling deployment with sequential updates";
        }

        // Detect platform
        if (request.Contains("docker", StringComparison.OrdinalIgnoreCase))
            analysis.Platform = "docker";
        else if (request.Contains("azure", StringComparison.OrdinalIgnoreCase) ||
                 request.Contains("container apps", StringComparison.OrdinalIgnoreCase))
            analysis.Platform = "azure-container-apps";
        else if (request.Contains("aws", StringComparison.OrdinalIgnoreCase) ||
                 request.Contains("ecs", StringComparison.OrdinalIgnoreCase))
            analysis.Platform = "aws-ecs";
        else if (request.Contains("gcp", StringComparison.OrdinalIgnoreCase) ||
                 request.Contains("cloud run", StringComparison.OrdinalIgnoreCase))
            analysis.Platform = "gcp-cloud-run";

        // Extract service name
        var serviceKeywords = new[] { "service", "app", "application" };
        var words = request.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length - 1; i++)
        {
            if (serviceKeywords.Any(k => words[i].Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                var potentialName = words[i + 1].Trim('.', '/', ':', ';', '"', '\'');
                if (potentialName.HasValue() && potentialName.Length > 2)
                {
                    analysis.ServiceName = potentialName.ToLowerInvariant();
                    break;
                }
            }
        }

        // Detect traffic split percentage for canary
        var percentMatch = System.Text.RegularExpressions.Regex.Match(request, @"(\d+)%");
        if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var percent))
        {
            analysis.TrafficSplitPercentage = Math.Clamp(percent, 0, 100);
        }

        return analysis;
    }

    [KernelFunction, Description("Generates deployment configuration")]
    public async Task<BlueGreenConfiguration> GenerateDeploymentConfigurationAsync(
        [Description("Deployment analysis results")] BlueGreenAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var config = new BlueGreenConfiguration
        {
            Strategy = analysis.Strategy,
            Platform = analysis.Platform,
            ServiceName = analysis.ServiceName,
            BlueEnvironment = analysis.BlueEnvironment,
            GreenEnvironment = analysis.GreenEnvironment,
            TrafficSplitPercentage = analysis.TrafficSplitPercentage,
            HealthCheckPath = analysis.HealthCheckPath,
            RollbackOnFailure = analysis.RollbackOnFailure,
            Summary = analysis.Summary
        };

        return config;
    }

    [KernelFunction, Description("Validates deployment configuration")]
    public async Task<BlueGreenValidationResult> ValidateDeploymentConfigurationAsync(
        [Description("Deployment configuration to validate")] BlueGreenConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var result = new BlueGreenValidationResult { IsValid = true };

        if (configuration.Strategy.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Deployment strategy is required";
            return result;
        }

        var validStrategies = new[] { "blue-green", "canary", "rolling" };
        if (!validStrategies.Contains(configuration.Strategy, StringComparer.OrdinalIgnoreCase))
        {
            result.IsValid = false;
            result.ErrorMessage = $"Invalid deployment strategy: {configuration.Strategy}";
            return result;
        }

        if (configuration.ServiceName.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Service name is required";
            return result;
        }

        if (configuration.BlueEnvironment == null || configuration.BlueEnvironment.Endpoint.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Blue environment configuration is required";
            return result;
        }

        if (configuration.GreenEnvironment == null || configuration.GreenEnvironment.Endpoint.IsNullOrWhiteSpace())
        {
            result.IsValid = false;
            result.ErrorMessage = "Green environment configuration is required";
            return result;
        }

        if (configuration.TrafficSplitPercentage < 0 || configuration.TrafficSplitPercentage > 100)
        {
            result.IsValid = false;
            result.ErrorMessage = "Traffic split percentage must be between 0 and 100";
            return result;
        }

        return result;
    }

    private async Task SaveDeploymentConfigurationAsync(
        BlueGreenConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(context.WorkspacePath, "deployment-config.json");
        var json = JsonSerializer.Serialize(configuration, CliJsonOptions.Indented);

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
        _logger.LogInformation("Saved deployment configuration to {ConfigPath}", configPath);

        // Generate YARP configuration for traffic management
        await GenerateYarpConfigurationAsync(configuration, context, cancellationToken);
    }

    private async Task GenerateYarpConfigurationAsync(
        BlueGreenConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var yarpConfig = new
        {
            ReverseProxy = new
            {
                Routes = new
                {
                    route1 = new
                    {
                        ClusterId = configuration.ServiceName,
                        Match = new
                        {
                            Path = "/{**catch-all}"
                        }
                    }
                },
                Clusters = new Dictionary<string, object>
                {
                    {
                        configuration.ServiceName, new
                        {
                            Destinations = new Dictionary<string, object>
                            {
                                {
                                    "blue", new
                                    {
                                        Address = configuration.BlueEnvironment.Endpoint,
                                        Health = configuration.HealthCheckPath,
                                        Metadata = new
                                        {
                                            Weight = 100 - configuration.TrafficSplitPercentage
                                        }
                                    }
                                },
                                {
                                    "green", new
                                    {
                                        Address = configuration.GreenEnvironment.Endpoint,
                                        Health = configuration.HealthCheckPath,
                                        Metadata = new
                                        {
                                            Weight = configuration.TrafficSplitPercentage
                                        }
                                    }
                                }
                            },
                            HealthCheck = new
                            {
                                Active = new
                                {
                                    Enabled = true,
                                    Interval = TimeSpan.FromSeconds(10),
                                    Timeout = TimeSpan.FromSeconds(5),
                                    Policy = "ConsecutiveFailures",
                                    Path = configuration.HealthCheckPath
                                }
                            }
                        }
                    }
                }
            }
        };

        var yarpConfigPath = Path.Combine(context.WorkspacePath, "yarp-config.json");
        var yarpJson = JsonSerializer.Serialize(yarpConfig, CliJsonOptions.Indented);

        await File.WriteAllTextAsync(yarpConfigPath, yarpJson, cancellationToken);
        _logger.LogInformation("Generated YARP configuration at {YarpConfigPath}", yarpConfigPath);
    }
}

/// <summary>
/// Analysis results from blue-green deployment requirements
/// </summary>
public class BlueGreenAnalysis
{
    public string Strategy { get; set; } = "blue-green";
    public string Platform { get; set; } = "kubernetes";
    public string ServiceName { get; set; } = string.Empty;
    public BlueGreenEnvironment BlueEnvironment { get; set; } = new();
    public BlueGreenEnvironment GreenEnvironment { get; set; } = new();
    public int TrafficSplitPercentage { get; set; }
    public string HealthCheckPath { get; set; } = "/health";
    public bool RollbackOnFailure { get; set; } = true;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Blue-green deployment environment configuration
/// </summary>
public class BlueGreenEnvironment
{
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Generated blue-green deployment configuration
/// </summary>
public class BlueGreenConfiguration
{
    public string Strategy { get; set; } = "blue-green";
    public string Platform { get; set; } = "kubernetes";
    public string ServiceName { get; set; } = string.Empty;
    public BlueGreenEnvironment BlueEnvironment { get; set; } = new();
    public BlueGreenEnvironment GreenEnvironment { get; set; } = new();
    public int TrafficSplitPercentage { get; set; }
    public string HealthCheckPath { get; set; } = "/health";
    public bool RollbackOnFailure { get; set; } = true;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Blue-green deployment configuration validation result
/// </summary>
public class BlueGreenValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
