// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Planning;
using Honua.Cli.Services;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to generate least-privilege IAM/RBAC policies with Terraform configuration
/// for deploying HonuaIO to cloud providers.
/// </summary>
public sealed class DeployGenerateIamCommand : AsyncCommand<DeployGenerateIamCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly ILlmProvider? _llmProvider;
    private readonly IAgentCoordinator? _agentCoordinator;

    public DeployGenerateIamCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        ILlmProvider? llmProvider = null,
        IAgentCoordinator? agentCoordinator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _llmProvider = llmProvider;
        _agentCoordinator = agentCoordinator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_llmProvider == null)
        {
            _console.MarkupLine("[red]Error: AI provider not configured. IAM generation requires LLM access.[/]");
            _console.MarkupLine("[yellow]Run 'honua setup-wizard' to configure AI services.[/]");
            return 1;
        }

        try
        {
            _console.MarkupLine("[bold cyan]HonuaIO IAM Permission Generator[/]");
            _console.WriteLine();

            // Parse deployment topology
            DeploymentTopology topology;
            if (settings.TopologyFile.HasValue())
            {
                topology = await LoadTopologyFromFileAsync(settings.TopologyFile);
            }
            else if (settings.FromPlan.HasValue())
            {
                topology = await LoadTopologyFromPlanAsync(settings.FromPlan, settings.CloudProvider, settings.Region);
            }
            else if (settings.ConfigFile.HasValue())
            {
                topology = await LoadTopologyFromConfigAsync(
                    settings.ConfigFile,
                    settings.CloudProvider ?? "aws",
                    settings.Region ?? "us-east-1",
                    settings.Environment ?? "dev");
            }
            else
            {
                // Interactive mode - prompt for details
                topology = await PromptForTopologyAsync(settings);
            }

            // Display topology summary
            DisplayTopologySummary(topology);

            if (!settings.AutoApprove)
            {
                if (!_console.Confirm("Generate IAM permissions for this deployment?", defaultValue: true))
                {
                    _console.MarkupLine("[yellow]Cancelled.[/]");
                    return 0;
                }
            }

            // Generate permissions
            await _console.Status()
                .StartAsync("Analyzing deployment requirements...", async ctx =>
                {
                    var agent = new CloudPermissionGeneratorAgent(_llmProvider, Microsoft.Extensions.Logging.Abstractions.NullLogger<CloudPermissionGeneratorAgent>.Instance);

                    // Ensure output directory exists before creating workspace path
                    Directory.CreateDirectory(settings.Output);

                    var agentContext = new AgentExecutionContext
                    {
                        WorkspacePath = Path.GetFullPath(settings.Output),
                        DryRun = false
                    };

                    ctx.Status("Generating least-privilege IAM policies...");
                    var result = await agent.GeneratePermissionsAsync(topology, agentContext, CancellationToken.None);

                    if (!result.Success)
                    {
                        throw new InvalidOperationException($"IAM generation failed: {result.ErrorMessage}");
                    }

                    ctx.Status("Writing Terraform configuration...");
                    await SaveTerraformConfigAsync(result, settings.Output);

                    _console.MarkupLine("[green]✓ IAM permissions generated successfully![/]");
                    _console.WriteLine();
                    DisplayGenerationResult(result, settings.Output);
                });

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    private async Task<DeploymentTopology> LoadTopologyFromFileAsync(string topologyFile)
    {
        _console.MarkupLine($"[dim]Loading topology from {topologyFile}...[/]");
        var json = await File.ReadAllTextAsync(topologyFile);
        var topology = JsonSerializer.Deserialize<DeploymentTopology>(json, JsonSerializerOptionsRegistry.DevTooling);
        if (topology == null)
        {
            throw new InvalidOperationException("Failed to deserialize topology file");
        }
        return topology;
    }

    private async Task<DeploymentTopology> LoadTopologyFromPlanAsync(string planFile, string? cloudProvider, string? region)
    {
        _console.MarkupLine($"[dim]Loading deployment plan from {planFile}...[/]");
        var json = await File.ReadAllTextAsync(planFile);
        var doc = JsonDocument.Parse(json);

        // Extract topology from plan file (saved as {Plan, Topology, GeneratedAt})
        if (doc.RootElement.TryGetProperty("Topology", out var topologyElement))
        {
            var topology = JsonSerializer.Deserialize<DeploymentTopology>(
                topologyElement.GetRawText(),
                JsonSerializerOptionsRegistry.DevTooling);

            if (topology != null)
            {
                return topology;
            }
        }

        throw new InvalidOperationException("Plan file does not contain a valid Topology section");
    }

    private async Task<DeploymentTopology> LoadTopologyFromConfigAsync(string configFile, string cloudProvider, string region, string environment)
    {
        _console.MarkupLine($"[dim]Loading HonuaIO configuration from {configFile}...[/]");
        var configContent = await File.ReadAllTextAsync(configFile);

        var analyzer = new DeploymentTopologyAnalyzer(_llmProvider!, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentTopologyAnalyzer>.Instance);
        return await analyzer.AnalyzeFromConfigAsync(configContent, cloudProvider, region, environment, CancellationToken.None);
    }

    private async Task<DeploymentTopology> PromptForTopologyAsync(Settings settings)
    {
        if (!settings.AutoApprove)
        {
            _console.MarkupLine("[bold]Interactive Topology Configuration[/]");
            _console.WriteLine();
        }

        var cloudProvider = settings.CloudProvider.IsNullOrWhiteSpace()
            ? _console.Prompt(new SelectionPrompt<string>()
                .Title("Select cloud provider:")
                .AddChoices("aws", "azure", "gcp"))
            : settings.CloudProvider;

        var region = settings.Region.IsNullOrWhiteSpace()
            ? _console.Prompt(new TextPrompt<string>("Enter region:").DefaultValue("us-east-1"))
            : settings.Region;

        var environment = settings.Environment.IsNullOrWhiteSpace()
            ? _console.Prompt(new SelectionPrompt<string>()
                .Title("Select environment:")
                .AddChoices("dev", "staging", "prod"))
            : settings.Environment;

        // When AutoApprove is true, include all components by default
        var hasDatabase = settings.AutoApprove ? true : _console.Confirm("Include database (PostgreSQL)?", defaultValue: true);
        var hasStorage = settings.AutoApprove ? true : _console.Confirm("Include object storage (S3/Blob/GCS)?", defaultValue: true);
        var hasLoadBalancer = settings.AutoApprove ? true : _console.Confirm("Include load balancer?", defaultValue: true);

        // IMPORTANT: Always create Storage, Networking, and Monitoring to avoid null reference issues
        // Only Database can be optional
        return await Task.FromResult(new DeploymentTopology
        {
            CloudProvider = cloudProvider,
            Region = region,
            Environment = environment,
            Database = hasDatabase ? new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = environment == "prod" ? "db.r6g.xlarge" : "db.t4g.micro",
                StorageGB = environment == "prod" ? 100 : 20,
                HighAvailability = environment == "prod"
            } : null,
            Compute = new ComputeConfig
            {
                Type = "container",
                InstanceSize = environment == "prod" ? "c6i.2xlarge" : "t3.medium",
                InstanceCount = environment == "prod" ? 3 : 1,
                AutoScaling = environment == "prod"
            },
            Storage = hasStorage ? new StorageConfig
            {
                Type = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "s3",
                    "azure" => "blob",
                    "gcp" => "gcs",
                    _ => "filesystem"
                },
                AttachmentStorageGB = 100,
                RasterCacheGB = 500,
                Replication = environment == "prod" ? "cross-region" : "single-region"
            } : new StorageConfig  // Always create Storage with defaults
            {
                Type = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "s3",
                    "azure" => "blob",
                    "gcp" => "gcs",
                    _ => "filesystem"
                },
                AttachmentStorageGB = 50,
                RasterCacheGB = 100,
                Replication = "single-region"
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = hasLoadBalancer,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = new MonitoringConfig
            {
                Provider = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "cloudwatch",
                    "azure" => "application-insights",
                    "gcp" => "cloud-monitoring",
                    _ => "prometheus"
                },
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = environment == "prod"
            }
        });
    }

    private void DisplayTopologySummary(DeploymentTopology topology)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Component")
            .AddColumn("Configuration");

        table.AddRow("[cyan]Cloud Provider[/]", topology.CloudProvider);
        table.AddRow("[cyan]Region[/]", topology.Region);
        table.AddRow("[cyan]Environment[/]", topology.Environment);

        if (topology.Database != null)
        {
            table.AddRow("[green]Database[/]", $"{topology.Database.Engine} {topology.Database.Version} ({topology.Database.InstanceSize})");
        }

        if (topology.Compute != null)
        {
            table.AddRow("[green]Compute[/]", $"{topology.Compute.Type} - {topology.Compute.InstanceCount}x {topology.Compute.InstanceSize}");
        }

        if (topology.Storage != null)
        {
            table.AddRow("[green]Storage[/]", $"{topology.Storage.Type} - {topology.Storage.AttachmentStorageGB + topology.Storage.RasterCacheGB}GB");
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private void DisplayGenerationResult(PermissionGenerationResult result, string outputPath)
    {
        _console.MarkupLine($"[bold]Generated Files:[/]");
        _console.MarkupLine($"  📄 [cyan]{Path.Combine(outputPath, "main.tf")}[/]");
        _console.MarkupLine($"  📄 [cyan]{Path.Combine(outputPath, "variables.tf")}[/]");
        _console.MarkupLine($"  📄 [cyan]{Path.Combine(outputPath, "outputs.tf")}[/]");
        _console.MarkupLine($"  📄 [cyan]{Path.Combine(outputPath, "README.md")}[/]");
        _console.WriteLine();

        _console.MarkupLine("[bold]Required Cloud Services:[/]");
        if (result.RequiredServices != null)
        {
            foreach (var service in result.RequiredServices)
            {
                _console.MarkupLine($"  • [yellow]{service.Service}[/]: {service.Actions.Count} actions");
            }
        }
        _console.WriteLine();

        _console.MarkupLine("[bold]Next Steps:[/]");
        _console.MarkupLine($"  1. Review generated Terraform: [cyan]cd {outputPath}[/]");
        _console.MarkupLine($"  2. Initialize Terraform: [cyan]terraform init[/]");
        _console.MarkupLine($"  3. Review changes: [cyan]terraform plan[/]");
        _console.MarkupLine($"  4. Apply configuration: [cyan]terraform apply[/]");
        _console.MarkupLine($"  5. Save credentials securely: [cyan]terraform output -json > credentials.json[/]");
        _console.WriteLine();

        _console.MarkupLine("[yellow]⚠ Remember to:[/]");
        _console.MarkupLine("  • Store credentials in a secure password manager");
        _console.MarkupLine("  • Never commit credentials to version control");
        _console.MarkupLine("  • Rotate access keys/passwords every 90 days");
    }

    private async Task SaveTerraformConfigAsync(PermissionGenerationResult result, string outputPath)
    {
        Directory.CreateDirectory(outputPath);

        // Write main.tf
        var deploymentTerraform = result.DeploymentIamTerraform ?? "# No Terraform configuration generated";
        var mainTfContent =
            $"# Generated by HonuaIO CLI{Environment.NewLine}" +
            $"# Provider: {result.CloudProvider}{Environment.NewLine}" +
            "# Role: deployment" + Environment.NewLine + Environment.NewLine +
            deploymentTerraform;

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "main.tf"),
            mainTfContent);

        // Write variables.tf
        var variables = GenerateVariablesFile(result);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "variables.tf"), variables);

        // Write outputs.tf
        var outputs = GenerateOutputsFile(result);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "outputs.tf"), outputs);

        // Write README.md
        var readme = GenerateReadme(result);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "README.md"), readme);
    }

    private string GenerateVariablesFile(PermissionGenerationResult result)
    {
        return $@"# Variables for HonuaIO IAM Deployment
# Generated by HonuaIO AI Agent

variable ""region"" {{
  description = ""Cloud region for deployment""
  type        = string
  default     = ""{result.CloudProvider}""
}}

variable ""environment"" {{
  description = ""Environment name (dev, staging, prod)""
  type        = string
}}

variable ""project_name"" {{
  description = ""Project name for resource naming""
  type        = string
  default     = ""honua""
}}
";
    }

    private string GenerateOutputsFile(PermissionGenerationResult result)
    {
        return @"# Outputs for generated IAM resources
# Credentials will be displayed here after terraform apply

output ""instructions"" {
  value       = ""Run 'terraform output -json' to view all outputs""
  description = ""Usage instructions""
}
";
    }

    private string GenerateReadme(PermissionGenerationResult result)
    {
        return $@"# HonuaIO IAM Deployment Configuration

Generated by HonuaIO AI Agent on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## Cloud Provider

{result.CloudProvider}

## Required Services

{string.Join("\n", result.RequiredServices?.Select(s => $"- **{s.Service}**: {string.Join(", ", s.Actions)}") ?? Array.Empty<string>())}

## Deployment

1. Initialize Terraform:
   ```bash
   terraform init
   ```

2. Review the execution plan:
   ```bash
   terraform plan
   ```

3. Apply the configuration:
   ```bash
   terraform apply
   ```

4. Save credentials securely:
   ```bash
   terraform output -json > credentials.json
   # Encrypt this file or store in password manager
   ```

## Security Notes

- **Never commit credentials to version control**
- Store credentials in a secure password manager (1Password, LastPass, Azure Key Vault)
- Rotate access keys/passwords every 90 days
- Use temporary credentials (STS, Managed Identity) when possible
- Enable MFA for production service principals
- Monitor credential usage in cloud provider audit logs

## Next Steps

Use the generated credentials to deploy HonuaIO:

```bash
# Export credentials (AWS example)
export AWS_ACCESS_KEY_ID=$(terraform output -raw access_key_id)
export AWS_SECRET_ACCESS_KEY=$(terraform output -raw secret_access_key)

# Deploy HonuaIO
honua deploy execute --config honua-config.json
```
";
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--cloud <PROVIDER>")]
        [Description("Cloud provider: aws, azure, or gcp")]
        public string? CloudProvider { get; init; }

        [CommandOption("--region <REGION>")]
        [Description("Cloud region (e.g., us-east-1, eastus, us-central1)")]
        public string? Region { get; init; }

        [CommandOption("--environment <ENV>")]
        [Description("Environment: dev, staging, or prod")]
        public string? Environment { get; init; } = "dev";

        [CommandOption("--topology <FILE>")]
        [Description("Path to topology JSON file")]
        public string? TopologyFile { get; init; }

        [CommandOption("--from-plan <FILE>")]
        [Description("Generate from existing deployment plan JSON")]
        public string? FromPlan { get; init; }

        [CommandOption("--config <FILE>")]
        [Description("Path to HonuaIO config file")]
        public string? ConfigFile { get; init; }

        [CommandOption("-o|--output <DIR>")]
        [Description("Output directory for Terraform files")]
        public string Output { get; init; } = "./iam-terraform";

        [CommandOption("--auto-approve")]
        [Description("Skip confirmation prompts")]
        public bool AutoApprove { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        public bool Verbose { get; init; }
    }
}
