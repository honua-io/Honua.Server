// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to upgrade Honua deployment using blue-green deployment strategy.
/// Orchestrates upgrade workflow: detect version → backup DB → create blue → switch traffic.
/// </summary>
[Description("Upgrade Honua deployment using blue-green strategy")]
public sealed class ProcessUpgradeCommand : AsyncCommand<ProcessUpgradeCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly Kernel _kernel;

    public ProcessUpgradeCommand(IAnsiConsole console, Kernel kernel)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Honua Deployment Upgrade[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.DeploymentName.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Deployment name is required (--deployment-name)[/]");
                return 1;
            }

            if (settings.TargetVersion.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Target version is required (--target-version)[/]");
                return 1;
            }

            // Build upgrade state from settings
            var upgradeState = new UpgradeState
            {
                UpgradeId = Guid.NewGuid().ToString(),
                DeploymentName = settings.DeploymentName,
                TargetVersion = settings.TargetVersion,
                StartTime = DateTime.UtcNow,
                Status = "Starting",
                CanRollback = true
            };

            // Display upgrade configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Configuration")
                .AddColumn("Value");

            configTable.AddRow("[cyan]Deployment Name[/]", upgradeState.DeploymentName);
            configTable.AddRow("[cyan]Target Version[/]", upgradeState.TargetVersion);
            configTable.AddRow("[cyan]Upgrade ID[/]", upgradeState.UpgradeId);
            configTable.AddRow("[cyan]Strategy[/]", "[green]Blue-Green Deployment[/]");
            configTable.AddRow("[cyan]Rollback Support[/]", upgradeState.CanRollback ? "[green]Yes[/]" : "[red]No[/]");
            configTable.AddRow("[cyan]Skip Backup[/]", settings.SkipBackup ? "[yellow]Yes[/]" : "[green]No[/]");

            _console.Write(configTable);
            _console.WriteLine();

            if (settings.SkipBackup)
            {
                _console.MarkupLine("[yellow]Warning: Database backup will be skipped. This is not recommended for production.[/]");
            }

            if (!_console.Confirm("Start upgrade with these settings?"))
            {
                _console.MarkupLine("[yellow]Upgrade cancelled by user[/]");
                return 0;
            }

            // Build and start the upgrade process
            var processBuilder = UpgradeProcess.BuildProcess();
            var process = processBuilder.Build();

            // Start process with initial event
            await _console.Status()
                .StartAsync("Initializing upgrade process...", async ctx =>
                {
                    ctx.Status("Starting blue-green upgrade workflow...");

                    // Start the process with the upgrade state
                    var processHandle = await process.StartAsync(
                        new KernelProcessEvent
                        {
                            Id = "StartUpgrade",
                            Data = upgradeState
                        },
                        Guid.NewGuid().ToString());

                    ctx.Status("Upgrade process started successfully");

                    _console.MarkupLine($"[green]Process started successfully[/]");
                    _console.MarkupLine($"[dim]Process ID: {processHandle}[/]");
                });

            _console.WriteLine();
            _console.MarkupLine("[bold green]Upgrade process initiated successfully[/]");
            _console.WriteLine();

            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine($"  1. Monitor upgrade: [cyan]honua process status {upgradeState.UpgradeId}[/]");
            _console.MarkupLine("  2. View deployment status: [cyan]honua status[/]");
            _console.MarkupLine("  3. If needed, rollback: [cyan]honua deploy blue-green rollback --deployment {settings.DeploymentName}[/]");

            _console.WriteLine();
            _console.MarkupLine("[yellow]Note: Traffic will be gradually switched to the new version after validation.[/]");

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

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--deployment-name")]
        [Description("Name of the deployment to upgrade (required)")]
        public string DeploymentName { get; set; } = string.Empty;

        [CommandOption("--target-version")]
        [Description("Target Honua version to upgrade to (required)")]
        public string TargetVersion { get; set; } = string.Empty;

        [CommandOption("--skip-backup")]
        [Description("Skip database backup (not recommended for production)")]
        [DefaultValue(false)]
        public bool SkipBackup { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
