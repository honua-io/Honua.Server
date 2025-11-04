// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating AWS Terraform configurations (ECS and Lambda).
/// Implementation extracted from DeploymentConfigurationAgent for separation of concerns.
/// </summary>
public sealed class TerraformAwsConfigurationService
{
    private readonly ILogger<TerraformAwsConfigurationService> _logger;

    public TerraformAwsConfigurationService(ILogger<TerraformAwsConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates AWS ECS Terraform configuration.
    /// </summary>
    public async Task<string> GenerateEcsAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating AWS Terraform configuration");

        var terraformContent = GenerateEcsContent(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-aws", fileName);

        _logger.LogDebug("Will write Terraform to: {FilePath}", filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            _logger.LogDebug("Creating directory: {Directory}", directory);
            Directory.CreateDirectory(directory!);
        }

        _logger.LogDebug("Writing {ByteCount} bytes to Terraform file", terraformContent.Length);
        await File.WriteAllTextAsync(filePath, terraformContent.Trim());
        _logger.LogDebug("Terraform file written successfully");

        // Also copy to workspace root for compatibility with tests
        var workspaceRootPath = Path.Combine(context.WorkspacePath, "main.tf");
        await File.WriteAllTextAsync(workspaceRootPath, terraformContent.Trim());
        _logger.LogDebug("Also copied to workspace root: {WorkspaceRootPath}", workspaceRootPath);

        return terraformContent.Trim();
    }

    /// <summary>
    /// Generates AWS Lambda Terraform configuration.
    /// </summary>
    public async Task<string> GenerateLambdaAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating AWS Lambda Terraform configuration");

        var terraformContent = GenerateLambdaContent(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-aws-lambda", fileName);

        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        await File.WriteAllTextAsync(filePath, terraformContent.Trim());

        // Also copy to workspace root for compatibility with tests
        var workspaceRootPath = Path.Combine(context.WorkspacePath, "main.tf");
        await File.WriteAllTextAsync(workspaceRootPath, terraformContent.Trim());

        return terraformContent.Trim();
    }

    // NOTE: Full implementation moved to separate file due to size (600+ lines)
    // See TerraformAwsContent.cs for the complete Terraform generation logic
    private string GenerateEcsContent(DeploymentAnalysis analysis)
    {
        return TerraformAwsContent.GenerateEcs(analysis);
    }

    private string GenerateLambdaContent(DeploymentAnalysis analysis)
    {
        return TerraformAwsContent.GenerateLambda(analysis);
    }
}
