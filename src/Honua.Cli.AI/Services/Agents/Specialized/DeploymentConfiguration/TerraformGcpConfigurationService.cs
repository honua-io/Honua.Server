// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating GCP Terraform configurations.
/// </summary>
public sealed class TerraformGcpConfigurationService
{
    private readonly ILogger<TerraformGcpConfigurationService> _logger;

    public TerraformGcpConfigurationService(ILogger<TerraformGcpConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateCloudRunAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating GCP Terraform configuration");

        var terraformContent = TerraformGcpContent.GenerateCloudRun(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-gcp", fileName);

        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        await File.WriteAllTextAsync(filePath, terraformContent.Trim());

        var workspaceRootPath = Path.Combine(context.WorkspacePath, "main.tf");
        await File.WriteAllTextAsync(workspaceRootPath, terraformContent.Trim());

        return terraformContent.Trim();
    }

    public async Task<string> GenerateCloudFunctionsAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating GCP Cloud Functions Terraform configuration");

        var terraformContent = TerraformGcpContent.GenerateCloudFunctions(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-gcp-functions", fileName);

        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        await File.WriteAllTextAsync(filePath, terraformContent.Trim());

        var workspaceRootPath = Path.Combine(context.WorkspacePath, "main.tf");
        await File.WriteAllTextAsync(workspaceRootPath, terraformContent.Trim());

        return terraformContent.Trim();
    }
}
