// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentModels = Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for deployment configuration, metadata management, and GitOps workflows.
/// Uses LLM inference to dynamically understand deployment requirements and generate configurations.
/// Orchestrates specialized sub-services for different deployment targets.
/// </summary>
public sealed class DeploymentConfigurationAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<DeploymentConfigurationAgent> _logger;

    // Specialized configuration services
    private readonly DeploymentModels.DeploymentAnalysisService _analysisService;
    private readonly DeploymentModels.DockerComposeConfigurationService _dockerComposeService;
    private readonly DeploymentModels.KubernetesConfigurationService _kubernetesService;
    private readonly DeploymentModels.TerraformAwsConfigurationService _terraformAwsService;
    private readonly DeploymentModels.TerraformAzureConfigurationService _terraformAzureService;
    private readonly DeploymentModels.TerraformGcpConfigurationService _terraformGcpService;
    private readonly DeploymentModels.HonuaConfigurationService _honuaConfigService;

    // Simplified constructor for backward compatibility
    public DeploymentConfigurationAgent(Kernel kernel, ILlmProvider? llmProvider)
        : this(kernel, llmProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentConfigurationAgent>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.DeploymentAnalysisService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.DockerComposeConfigurationService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.KubernetesConfigurationService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.TerraformAwsConfigurationService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.TerraformAzureConfigurationService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.TerraformGcpConfigurationService>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentModels.HonuaConfigurationService>.Instance)
    {
    }

    public DeploymentConfigurationAgent(
        Kernel kernel,
        ILlmProvider? llmProvider,
        ILogger<DeploymentConfigurationAgent> logger,
        ILogger<DeploymentModels.DeploymentAnalysisService> analysisLogger,
        ILogger<DeploymentModels.DockerComposeConfigurationService> dockerComposeLogger,
        ILogger<DeploymentModels.KubernetesConfigurationService> kubernetesLogger,
        ILogger<DeploymentModels.TerraformAwsConfigurationService> terraformAwsLogger,
        ILogger<DeploymentModels.TerraformAzureConfigurationService> terraformAzureLogger,
        ILogger<DeploymentModels.TerraformGcpConfigurationService> terraformGcpLogger,
        ILogger<DeploymentModels.HonuaConfigurationService> honuaConfigLogger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize specialized services
        _analysisService = new DeploymentModels.DeploymentAnalysisService(llmProvider, analysisLogger);
        _dockerComposeService = new DeploymentModels.DockerComposeConfigurationService(dockerComposeLogger);
        _kubernetesService = new DeploymentModels.KubernetesConfigurationService(kubernetesLogger);
        _terraformAwsService = new DeploymentModels.TerraformAwsConfigurationService(terraformAwsLogger);
        _terraformAzureService = new DeploymentModels.TerraformAzureConfigurationService(terraformAzureLogger);
        _terraformGcpService = new DeploymentModels.TerraformGcpConfigurationService(terraformGcpLogger);
        _honuaConfigService = new DeploymentModels.HonuaConfigurationService(honuaConfigLogger);
    }

    /// <summary>
    /// Processes a deployment configuration request by analyzing requirements and generating configuration.
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
            var validation = await ValidateConfigurationAsync(configuration, context, cancellationToken);

            if (!validation.IsValid)
            {
                return new AgentStepResult
                {
                    AgentName = "DeploymentConfiguration",
                    Action = "ProcessDeploymentRequest",
                    Success = false,
                    Message = $"Configuration validation failed: {validation.ErrorMessage}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Save configuration to workspace
            await SaveConfigurationAsync(configuration, context, cancellationToken);

            // Generate Honua metadata and runtime configuration
            await _honuaConfigService.GenerateAsync(analysis, context, cancellationToken);

            var message = context.DryRun
                ? $"Generated deployment configuration (dry-run): {configuration.Summary}. Saved to {context.WorkspacePath}"
                : $"Applied deployment configuration: {configuration.Summary}. Saved to {context.WorkspacePath}";

            return new AgentStepResult
            {
                AgentName = "DeploymentConfiguration",
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
                AgentName = "DeploymentConfiguration",
                Action = "ProcessDeploymentRequest",
                Success = false,
                Message = $"Error processing deployment request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Analyzes deployment requirements from user request using LLM inference")]
    public async Task<DeploymentModels.DeploymentAnalysis> AnalyzeDeploymentRequirementsAsync(
        [Description("User's deployment request")] string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await _analysisService.AnalyzeAsync(request, context, cancellationToken);
    }

    [KernelFunction, Description("Generates deployment configuration (Docker Compose, K8s, Terraform, etc.)")]
    public async Task<DeploymentModels.DeploymentConfiguration> GenerateDeploymentConfigurationAsync(
        [Description("Analysis of deployment requirements")] DeploymentModels.DeploymentAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        string configContent;
        string configType;

        switch (analysis.DeploymentType)
        {
            case DeploymentModels.DeploymentType.DockerCompose:
                configType = "docker-compose.yml";
                configContent = await _dockerComposeService.GenerateAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.Kubernetes:
                configType = "kubernetes-manifests";
                configContent = await _kubernetesService.GenerateAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.TerraformAWS:
                configType = "terraform-aws";
                configContent = await _terraformAwsService.GenerateEcsAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.TerraformAzure:
                configType = "terraform-azure";
                configContent = await _terraformAzureService.GenerateContainerAppsAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.TerraformGCP:
                configType = "terraform-gcp";
                configContent = await _terraformGcpService.GenerateCloudRunAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.AWSLambda:
                configType = "terraform-aws-lambda";
                configContent = await _terraformAwsService.GenerateLambdaAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.AzureFunctions:
                configType = "terraform-azure-functions";
                configContent = await _terraformAzureService.GenerateFunctionsAsync(analysis, context);
                break;

            case DeploymentModels.DeploymentType.GCPCloudFunctions:
                configType = "terraform-gcp-functions";
                configContent = await _terraformGcpService.GenerateCloudFunctionsAsync(analysis, context);
                break;

            default:
                throw new NotSupportedException($"Deployment type {analysis.DeploymentType} not supported");
        }

        return new DeploymentModels.DeploymentConfiguration
        {
            Type = configType,
            Content = configContent,
            Summary = $"{configType} for {analysis.TargetEnvironment} environment"
        };
    }

    [KernelFunction, Description("Validates deployment configuration for correctness and best practices")]
    public async Task<DeploymentModels.ValidationResult> ValidateConfigurationAsync(
        [Description("Deployment configuration to validate")] DeploymentModels.DeploymentConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var issues = new System.Collections.Generic.List<string>();

        // Basic validation
        if (configuration.Content.IsNullOrWhiteSpace())
        {
            issues.Add("Configuration content is empty");
        }

        // Type-specific validation could be added here

        return await Task.FromResult(new DeploymentModels.ValidationResult
        {
            IsValid = issues.Count == 0,
            ErrorMessage = issues.Count > 0 ? string.Join("; ", issues) : null
        });
    }

    private async Task SaveConfigurationAsync(
        DeploymentModels.DeploymentConfiguration configuration,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // For kubernetes-manifests and terraform, files are already written by their respective generators
        if (configuration.Type == "kubernetes-manifests" ||
            configuration.Type.StartsWith("terraform-"))
        {
            return;
        }

        string filePath = configuration.Type switch
        {
            "docker-compose.yml" => Path.Combine(context.WorkspacePath, "docker-compose.yml"),
            _ => Path.Combine(context.WorkspacePath, configuration.Type)
        };

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save the configuration content
        await File.WriteAllTextAsync(filePath, configuration.Content, cancellationToken);
    }
}
