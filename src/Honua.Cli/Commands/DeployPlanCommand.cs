// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// CLI command to create a deployment plan for HonuaIO infrastructure.
/// Similar to 'terraform plan' - shows what will be deployed without executing.
/// </summary>
public sealed class DeployPlanCommand : AsyncCommand<DeployPlanCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAgentCoordinator? _agentCoordinator;
    private readonly ILlmProvider? _llmProvider;

    public DeployPlanCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        IAgentCoordinator? agentCoordinator = null,
        ILlmProvider? llmProvider = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _agentCoordinator = agentCoordinator;
        _llmProvider = llmProvider;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_agentCoordinator == null || _llmProvider == null)
        {
            _console.MarkupLine("[red]Error: AI coordinator not configured. Deployment planning requires AI services.[/]");
            _console.MarkupLine("[yellow]Run 'honua setup-wizard' to configure AI services.[/]");
            return 1;
        }

        try
        {
            _console.MarkupLine("[bold cyan]HonuaIO Deployment Planner[/]");
            _console.WriteLine();

            // Load or create topology
            DeploymentTopology topology;
            if (settings.ConfigFile.HasValue())
            {
                topology = await LoadTopologyFromConfigAsync(
                    settings.ConfigFile,
                    settings.CloudProvider ?? "aws",
                    settings.Region ?? "us-east-1",
                    settings.Environment ?? "dev");
            }
            else if (settings.CloudProvider.HasValue() &&
                     settings.Region.HasValue() &&
                     settings.Environment.HasValue())
            {
                // All required parameters provided - create topology directly
                topology = CreateDefaultTopology(settings.CloudProvider, settings.Region, settings.Environment);
            }
            else
            {
                // Interactive mode - prompt for missing parameters
                topology = await PromptForTopologyAsync(settings);
            }

            // Generate deployment plan
            ExecutionPlan plan = await _console.Status()
                .StartAsync("Analyzing deployment requirements...", ctx =>
                {
                    ctx.Status("Creating deployment plan...");

                    var agentContext = new AgentExecutionContext
                    {
                        WorkspacePath = settings.Workspace.IsNullOrWhiteSpace()
                            ? Directory.GetCurrentDirectory()
                            : _environment.ResolveWorkspacePath(settings.Workspace),
                        DryRun = true // Always dry-run for planning
                    };

                    // Create deployment plan using architecture consultant
                    // TODO: Integrate ArchitectureConsultingAgent (requires Kernel, not ILlmProvider)
                    // var architectAgent = new ArchitectureConsultingAgent(kernel);
                    // var deploymentRequest = BuildDeploymentRequest(topology);

                    // For now, create a basic plan structure
                    // In production, this would call the architecture agent to generate full plan
                    return Task.FromResult(CreateDeploymentPlan(topology));
                });

            // Display plan summary
            DisplayPlanSummary(plan, topology);

            // Save plan if output specified
            if (settings.Output.HasValue())
            {
                await SavePlanAsync(plan, topology, settings.Output);
                _console.MarkupLine($"[green]✓ Plan saved to {settings.Output}[/]");
            }

            _console.WriteLine();
            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine("  1. Review the plan above");
            _console.MarkupLine($"  2. Generate IAM permissions: [cyan]honua deploy generate-iam --from-plan {settings.Output ?? "plan.json"}[/]");
            _console.MarkupLine($"  3. Execute deployment: [cyan]honua deploy execute --plan {settings.Output ?? "plan.json"}[/]");

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

    private async Task<DeploymentTopology> LoadTopologyFromConfigAsync(string configFile, string cloudProvider, string region, string environment)
    {
        _console.MarkupLine($"[dim]Loading configuration from {configFile}...[/]");
        var configContent = await File.ReadAllTextAsync(configFile);

        var analyzer = new DeploymentTopologyAnalyzer(_llmProvider!, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeploymentTopologyAnalyzer>.Instance);
        return await analyzer.AnalyzeFromConfigAsync(configContent, cloudProvider, region, environment, CancellationToken.None);
    }

    private DeploymentTopology CreateDefaultTopology(string cloudProvider, string region, string environment)
    {
        // Create a default topology with standard settings
        // IMPORTANT: Always include Storage, Database, Compute, Networking, and Monitoring
        // to ensure tests and commands have complete topology data
        return new DeploymentTopology
        {
            CloudProvider = cloudProvider,
            Region = region,
            Environment = environment,
            Database = new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = environment == "prod" ? "db.r6g.xlarge" : "db.t4g.micro",
                StorageGB = environment == "prod" ? 100 : 20,
                HighAvailability = environment == "prod"
            },
            Compute = new ComputeConfig
            {
                Type = cloudProvider == "kubernetes" ? "container" : "vm",
                InstanceSize = environment == "prod" ? "c6i.2xlarge" : "t3.medium",
                InstanceCount = environment == "prod" ? 3 : 1,
                AutoScaling = environment == "prod"
            },
            Storage = new StorageConfig
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
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = true,
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
        };
    }

    private Task<DeploymentTopology> PromptForTopologyAsync(Settings settings)
    {
        _console.MarkupLine("[bold]Interactive Deployment Configuration[/]");
        _console.WriteLine();

        var cloudProvider = settings.CloudProvider.IsNullOrWhiteSpace()
            ? _console.Prompt(new SelectionPrompt<string>()
                .Title("Select cloud provider:")
                .AddChoices("aws", "azure", "gcp", "kubernetes", "docker"))
            : settings.CloudProvider;

        var region = settings.Region.IsNullOrWhiteSpace()
            ? _console.Prompt(new TextPrompt<string>("Enter region:").DefaultValue("us-east-1"))
            : settings.Region;

        var environment = settings.Environment.IsNullOrWhiteSpace()
            ? _console.Prompt(new SelectionPrompt<string>()
                .Title("Select environment:")
                .AddChoices("dev", "staging", "prod"))
            : settings.Environment;

        // Prompt for components
        var hasDatabase = _console.Confirm("Include PostgreSQL database?", defaultValue: true);
        var hasStorage = _console.Confirm("Include object storage?", defaultValue: true);
        var hasLoadBalancer = _console.Confirm("Include load balancer?", defaultValue: true);
        var enableMonitoring = _console.Confirm("Enable monitoring and logging?", defaultValue: true);

        // IMPORTANT: Always create Storage, Networking, and Monitoring to avoid null reference issues
        // Only Database can be optional
        return Task.FromResult(new DeploymentTopology
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
                Type = cloudProvider == "kubernetes" ? "container" : "vm",
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
            Monitoring = enableMonitoring ? new MonitoringConfig
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
            } : new MonitoringConfig  // Always create Monitoring with defaults
            {
                Provider = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "cloudwatch",
                    "azure" => "application-insights",
                    "gcp" => "cloud-monitoring",
                    _ => "prometheus"
                },
                EnableMetrics = false,
                EnableLogs = false,
                EnableTracing = false
            }
        });
    }

    private string BuildDeploymentRequest(DeploymentTopology topology)
    {
        return $"Deploy HonuaIO to {topology.CloudProvider} {topology.Region} for {topology.Environment} environment";
    }

    private ExecutionPlan CreateDeploymentPlan(DeploymentTopology topology)
    {
        var steps = new System.Collections.Generic.List<PlanStep>();
        var stepNumber = 1;

        // Infrastructure setup steps
        if (topology.Networking != null)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.Custom,
                Description = "Create virtual network and subnets",
                Operation = $"Create VPC/VNet in {topology.Region}",
                ExpectedOutcome = "Isolated network environment for HonuaIO",
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresDowntime = false,
                IsReversible = true
            });
        }

        // Database setup
        if (topology.Database != null)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.Custom,
                Description = $"Create {topology.Database.Engine} database",
                Operation = $"Provision {topology.Database.InstanceSize} instance with {topology.Database.StorageGB}GB storage",
                ExpectedOutcome = "PostgreSQL database ready for HonuaIO",
                EstimatedDuration = TimeSpan.FromMinutes(15),
                RequiresDowntime = false,
                IsReversible = true
            });

            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.CreateTable,
                Description = "Initialize HonuaIO database schema",
                Operation = "Run database migrations",
                ExpectedOutcome = "Database schema configured",
                EstimatedDuration = TimeSpan.FromMinutes(2),
                RequiresDowntime = false,
                IsReversible = true,
                DependsOn = new System.Collections.Generic.List<int> { stepNumber - 2 }
            });
        }

        // Storage setup
        if (topology.Storage != null)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.Custom,
                Description = $"Create {topology.Storage.Type} buckets",
                Operation = "Create attachments and raster cache buckets",
                ExpectedOutcome = "Object storage configured",
                EstimatedDuration = TimeSpan.FromMinutes(2),
                RequiresDowntime = false,
                IsReversible = true
            });
        }

        // Compute setup
        if (topology.Compute != null)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.Custom,
                Description = $"Deploy HonuaIO server ({topology.Compute.Type})",
                Operation = $"Deploy {topology.Compute.InstanceCount} instance(s) of {topology.Compute.InstanceSize}",
                ExpectedOutcome = "HonuaIO server running",
                EstimatedDuration = TimeSpan.FromMinutes(10),
                RequiresDowntime = false,
                IsReversible = true
            });
        }

        // Load balancer
        if (topology.Networking?.LoadBalancer == true)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.Custom,
                Description = "Configure load balancer",
                Operation = "Create and configure ALB/Load Balancer",
                ExpectedOutcome = "Load balancer distributing traffic",
                EstimatedDuration = TimeSpan.FromMinutes(5),
                RequiresDowntime = false,
                IsReversible = true,
                DependsOn = new System.Collections.Generic.List<int> { stepNumber - 2 }
            });
        }

        // Monitoring
        if (topology.Monitoring != null)
        {
            steps.Add(new PlanStep
            {
                StepNumber = stepNumber++,
                Type = StepType.UpdateConfig,
                Description = "Configure monitoring and logging",
                Operation = $"Setup {topology.Monitoring.Provider}",
                ExpectedOutcome = "Metrics and logs being collected",
                EstimatedDuration = TimeSpan.FromMinutes(3),
                RequiresDowntime = false,
                IsReversible = true
            });
        }

        return new ExecutionPlan
        {
            Id = Guid.NewGuid().ToString(),
            Title = $"Deploy HonuaIO to {topology.CloudProvider}",
            Description = $"Deploy HonuaIO geospatial server to {topology.CloudProvider} {topology.Region} ({topology.Environment})",
            Type = PlanType.Deployment,
            Steps = steps,
            CredentialsRequired = new System.Collections.Generic.List<Honua.Cli.AI.Services.Planning.CredentialRequirement>
            {
                new()
                {
                    SecretRef = $"{topology.CloudProvider}-deployer",
                    Scope = new Honua.Cli.AI.Secrets.AccessScope
                    {
                        Level = Honua.Cli.AI.Secrets.AccessLevel.Admin,
                        AllowedOperations = new System.Collections.Generic.List<string>
                        {
                            "CreateVPC", "CreateSubnet", "CreateDatabase", "CreateStorageBucket",
                            "CreateLoadBalancer", "CreateComputeInstance", "ConfigureMonitoring"
                        }
                    },
                    Duration = TimeSpan.FromHours(2),
                    Purpose = "Infrastructure deployment",
                    Operations = new System.Collections.Generic.List<string>
                    {
                        "Create networking resources",
                        "Provision database",
                        "Setup storage buckets",
                        "Deploy compute instances",
                        "Configure load balancer",
                        "Setup monitoring"
                    }
                }
            },
            Risk = new Honua.Cli.AI.Services.Planning.RiskAssessment
            {
                Level = topology.Environment == "prod" ? RiskLevel.Medium : RiskLevel.Low,
                RiskFactors = new System.Collections.Generic.List<string>
                {
                    topology.Environment == "prod" ? "Production environment" : "Non-production environment",
                    "Cloud infrastructure provisioning",
                    topology.Environment == "prod" ? "Multiple instances with auto-scaling" : "Single instance deployment"
                },
                Mitigations = new System.Collections.Generic.List<string>
                {
                    "Automated deployment with rollback support",
                    "No data modification during deployment",
                    "All steps are reversible",
                    "Incremental deployment with health checks"
                }
            }
        };
    }

    private void DisplayPlanSummary(ExecutionPlan plan, DeploymentTopology topology)
    {
        _console.Write(new Rule($"[bold cyan]{plan.Title}[/]").LeftJustified());
        _console.WriteLine();

        // Topology summary
        var topologyTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Component")
            .AddColumn("Configuration");

        topologyTable.AddRow("[cyan]Cloud[/]", $"{topology.CloudProvider} ({topology.Region})");
        topologyTable.AddRow("[cyan]Environment[/]", topology.Environment);

        if (topology.Database != null)
            topologyTable.AddRow("[green]Database[/]", $"{topology.Database.Engine} {topology.Database.Version} ({topology.Database.InstanceSize})");
        if (topology.Compute != null)
            topologyTable.AddRow("[green]Compute[/]", $"{topology.Compute.InstanceCount}x {topology.Compute.InstanceSize}");
        if (topology.Storage != null)
            topologyTable.AddRow("[green]Storage[/]", $"{topology.Storage.Type}");

        _console.Write(topologyTable);
        _console.WriteLine();

        // Execution steps
        _console.MarkupLine("[bold]Deployment Steps:[/]");
        var stepsTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Step")
            .AddColumn("Duration")
            .AddColumn("Downtime");

        foreach (var step in plan.Steps)
        {
            var duration = step.EstimatedDuration?.ToString(@"mm\:ss") ?? "unknown";
            var downtime = step.RequiresDowntime ? "[red]Yes[/]" : "[green]No[/]";
            stepsTable.AddRow(
                step.StepNumber.ToString(),
                step.Description ?? "Unknown",
                duration,
                downtime);
        }

        _console.Write(stepsTable);
        _console.WriteLine();

        // Summary
        var totalDuration = plan.Steps
            .Where(s => s.EstimatedDuration.HasValue)
            .Sum(s => s.EstimatedDuration!.Value.TotalMinutes);

        _console.MarkupLine($"[bold]Total Estimated Duration:[/] ~{totalDuration:F0} minutes");
        _console.MarkupLine($"[bold]Risk Level:[/] {FormatRiskLevel(plan.Risk.Level)}");
    }

    private string FormatRiskLevel(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => "[green]Low[/]",
            RiskLevel.Medium => "[yellow]Medium[/]",
            RiskLevel.High => "[red]High[/]",
            _ => "[dim]Unknown[/]"
        };
    }

    private async Task SavePlanAsync(ExecutionPlan plan, DeploymentTopology topology, string output)
    {
        var planData = new
        {
            Plan = plan,
            Topology = topology,
            GeneratedAt = DateTime.UtcNow
        };

        // Use custom options for anonymous objects - source generation doesn't support them
        // Create indented JSON with flexible serialization for anonymous types
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(planData, options);
        await File.WriteAllTextAsync(output, json);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--cloud <PROVIDER>")]
        [Description("Cloud provider: aws, azure, gcp, kubernetes, docker")]
        public string? CloudProvider { get; init; }

        [CommandOption("--region <REGION>")]
        [Description("Cloud region")]
        public string? Region { get; init; }

        [CommandOption("--environment <ENV>")]
        [Description("Environment: dev, staging, prod")]
        public string? Environment { get; init; } = "dev";

        [CommandOption("--config <FILE>")]
        [Description("Path to HonuaIO configuration file")]
        public string? ConfigFile { get; init; }

        [CommandOption("-o|--output <FILE>")]
        [Description("Output file for deployment plan")]
        public string? Output { get; init; } = "deployment-plan.json";

        [CommandOption("--workspace <PATH>")]
        [Description("Workspace directory")]
        public string? Workspace { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        public bool Verbose { get; init; }
    }
}
