// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating Azure Terraform configurations.
/// </summary>
public sealed class TerraformAzureConfigurationService
{
    private readonly ILogger<TerraformAzureConfigurationService> _logger;

    public TerraformAzureConfigurationService(ILogger<TerraformAzureConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateContainerAppsAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating Azure Terraform configuration");

        var terraformContent = TerraformAzureContent.GenerateContainerApps(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-azure", fileName);

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

    public async Task<string> GenerateFunctionsAsync(DeploymentAnalysis analysis, AgentExecutionContext context)
    {
        _logger.LogDebug("Generating Azure Functions Terraform configuration");

        var terraformContent = TerraformAzureContent.GenerateFunctions(analysis);
        var fileName = "main.tf";
        var filePath = Path.Combine(context.WorkspacePath, "terraform-azure-functions", fileName);

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
