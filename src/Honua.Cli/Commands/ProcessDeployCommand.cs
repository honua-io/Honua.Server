// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to deploy Honua infrastructure using Semantic Kernel Process Framework.
/// Orchestrates deployment workflow: validate → generate → review → deploy infra → configure → deploy app → validate → observability.
/// </summary>
[Description("Deploy Honua infrastructure to cloud provider")]
public sealed class ProcessDeployCommand : AsyncCommand<ProcessDeployCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly Kernel _kernel;
    private readonly IResourceEnvelopeCatalog _envelopeCatalog;

    public ProcessDeployCommand(IAnsiConsole console, Kernel kernel, IResourceEnvelopeCatalog envelopeCatalog)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _envelopeCatalog = envelopeCatalog ?? throw new ArgumentNullException(nameof(envelopeCatalog));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Honua Infrastructure Deployment[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.Name.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Deployment name is required (--name)[/]");
                return 1;
            }

            // Build deployment state from settings
        var deploymentState = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = settings.Provider,
            Region = settings.Region,
            DeploymentName = settings.Name,
            Tier = settings.Tier,
            WorkloadProfile = settings.WorkloadProfile,
            StartTime = DateTime.UtcNow,
            Status = "Starting",
            RequiresApproval = !settings.AutoApprove,
            GuardrailHistory = new()
        };

            // Display deployment configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Configuration")
                .AddColumn("Value");

            configTable.AddRow("[cyan]Deployment Name[/]", deploymentState.DeploymentName);
            configTable.AddRow("[cyan]Cloud Provider[/]", deploymentState.CloudProvider);
            configTable.AddRow("[cyan]Region[/]", deploymentState.Region);
        configTable.AddRow("[cyan]Tier[/]", deploymentState.Tier);
        configTable.AddRow("[cyan]Workload Profile[/]", settings.WorkloadProfile);
        configTable.AddRow("[cyan]Deployment ID[/]", deploymentState.DeploymentId);
            configTable.AddRow("[cyan]Auto-Approve[/]", settings.AutoApprove ? "[green]Yes[/]" : "[yellow]No (manual approval)[/]");

        _console.Write(configTable);
        _console.WriteLine();

        RenderGuardrailSummary(settings);

            if (!settings.AutoApprove && !_console.Confirm("Start deployment with these settings?"))
            {
                _console.MarkupLine("[yellow]Deployment cancelled by user[/]");
                return 0;
            }

            // Build and start the deployment process
            var processBuilder = DeploymentProcess.BuildProcess();
            var process = processBuilder.Build();

            // Start process with initial event
            await _console.Status()
                .StartAsync("Initializing deployment process...", async ctx =>
                {
                    ctx.Status("Starting deployment workflow...");

                    // Start the process with the deployment state
                    var processHandle = await process.StartAsync(
                        new KernelProcessEvent
                        {
                            Id = "StartDeployment",
                            Data = deploymentState
                        },
                        Guid.NewGuid().ToString());

                    ctx.Status("Deployment process started successfully");

                    // In a real implementation, we would monitor the process status here
                    // For now, just display the process ID
                    _console.MarkupLine($"[green]Process started successfully[/]");
                    _console.MarkupLine($"[dim]Process ID: {processHandle}[/]");
                });

            _console.WriteLine();
            _console.MarkupLine("[bold green]Deployment process initiated successfully[/]");
            _console.WriteLine();

            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine($"  1. Monitor deployment: [cyan]honua process status {deploymentState.DeploymentId}[/]");
            _console.MarkupLine("  2. View logs: [cyan]honua admin logging get[/]");
            _console.MarkupLine("  3. List all processes: [cyan]honua process list[/]");

            if (!settings.AutoApprove)
            {
                _console.WriteLine();
                _console.MarkupLine("[yellow]Note: This deployment requires manual approval at the review step.[/]");
            }

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

    private void RenderGuardrailSummary(Settings settings)
    {
        try
        {
            var envelope = _envelopeCatalog.Resolve(settings.Provider, settings.WorkloadProfile);

            var guardrailTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold cyan]Guardrail Summary[/]")
                .AddColumn("Attribute")
                .AddColumn("Value");

            guardrailTable.AddRow("Envelope", envelope.Id);
            guardrailTable.AddRow("Platform", envelope.Platform);
            guardrailTable.AddRow("Min vCPU", envelope.MinVCpu.ToString());
            guardrailTable.AddRow("Min Memory (GiB)", envelope.MinMemoryGb.ToString());
            guardrailTable.AddRow("Min Instances", envelope.MinInstances.ToString());

            if (envelope.MinProvisionedConcurrency is { } minConcurrency)
            {
                guardrailTable.AddRow("Min Provisioned Concurrency", minConcurrency.ToString());
            }

            if (envelope.MinEphemeralGb > 0)
            {
                guardrailTable.AddRow("Min Ephemeral Storage (GiB)", envelope.MinEphemeralGb.ToString());
            }

            _console.Write(guardrailTable);
            _console.WriteLine();
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        {
            _console.MarkupLine("[yellow]No predefined guardrail envelope for this provider/workload combination. Guardrails will be determined during validation.[/]");
            _console.WriteLine();
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--provider")]
        [Description("Cloud provider (AWS, Azure, GCP, kubernetes, docker)")]
        [DefaultValue("AWS")]
        public string Provider { get; set; } = "AWS";

        [CommandOption("--region")]
        [Description("Deployment region")]
        [DefaultValue("us-west-2")]
        public string Region { get; set; } = "us-west-2";

        [CommandOption("--tier")]
        [Description("Deployment tier (Development, Staging, Production)")]
        [DefaultValue("Development")]
        public string Tier { get; set; } = "Development";

        [CommandOption("--workload-profile")]
        [Description("Workload profile for guardrails (e.g., api-small, api-standard, raster-batch)")]
        [DefaultValue("api-standard")]
        public string WorkloadProfile { get; set; } = "api-standard";

        [CommandOption("--name")]
        [Description("Deployment name (required)")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("--auto-approve")]
        [Description("Skip manual approval steps")]
        [DefaultValue(false)]
        public bool AutoApprove { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
