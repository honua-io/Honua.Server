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
/// CLI command to synchronize GitOps configuration using Semantic Kernel Process Framework.
/// Orchestrates GitOps workflow: validate Git config → sync config → monitor drift.
/// </summary>
[Description("Synchronize GitOps configuration from Git repository")]
public sealed class ProcessGitOpsCommand : AsyncCommand<ProcessGitOpsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly Kernel _kernel;

    public ProcessGitOpsCommand(IAnsiConsole console, Kernel kernel)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Honua GitOps Configuration Sync[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.RepoUrl.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Repository URL is required (--repo-url)[/]");
                return 1;
            }

            if (settings.ConfigPath.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Configuration path is required (--config-path)[/]");
                return 1;
            }

            // Build GitOps state from settings
            var gitOpsState = new GitOpsState
            {
                ProcessId = Guid.NewGuid().ToString(),
                GitOpsId = Guid.NewGuid().ToString(),
                RepoUrl = settings.RepoUrl,
                Branch = settings.Branch,
                ConfigPath = settings.ConfigPath,
                AutoSync = settings.AutoSync,
                RequiresApproval = !settings.AutoApprove,
                StartTime = DateTime.UtcNow,
                Status = "Starting"
            };

            // Display GitOps configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Configuration")
                .AddColumn("Value");

            configTable.AddRow("[cyan]Repository URL[/]", gitOpsState.RepoUrl);
            configTable.AddRow("[cyan]Branch[/]", gitOpsState.Branch);
            configTable.AddRow("[cyan]Config Path[/]", gitOpsState.ConfigPath);
            configTable.AddRow("[cyan]GitOps ID[/]", gitOpsState.GitOpsId);
            configTable.AddRow("[cyan]Process ID[/]", gitOpsState.ProcessId);
            configTable.AddRow("[cyan]Auto-Sync[/]", gitOpsState.AutoSync ? "[green]Enabled[/]" : "[yellow]Disabled[/]");
            configTable.AddRow("[cyan]Requires Approval[/]", gitOpsState.RequiresApproval ? "[yellow]Yes[/]" : "[green]No (auto-approve)[/]");

            _console.Write(configTable);
            _console.WriteLine();

            if (gitOpsState.RequiresApproval && !_console.Confirm("Start GitOps sync with these settings?"))
            {
                _console.MarkupLine("[yellow]GitOps sync cancelled by user[/]");
                return 0;
            }

            // Build and start the GitOps process
            var processBuilder = GitOpsProcess.BuildProcess();
            var process = processBuilder.Build();

            // Start process with initial event
            await _console.Status()
                .StartAsync("Initializing GitOps sync process...", async ctx =>
                {
                    ctx.Status("Starting GitOps configuration workflow...");

                    // Start the process with the GitOps state
                    var processHandle = await process.StartAsync(
                        new KernelProcessEvent
                        {
                            Id = "StartGitOpsSync",
                            Data = gitOpsState
                        },
                        Guid.NewGuid().ToString());

                    ctx.Status("GitOps sync process started successfully");

                    _console.MarkupLine($"[green]Process started successfully[/]");
                    _console.MarkupLine($"[dim]Process ID: {processHandle}[/]");
                });

            _console.WriteLine();
            _console.MarkupLine("[bold green]GitOps sync process initiated successfully[/]");
            _console.WriteLine();

            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine($"  1. Monitor sync: [cyan]honua process status {gitOpsState.ProcessId}[/]");
            _console.MarkupLine("  2. View GitOps status: [cyan]honua gitops status[/]");
            _console.MarkupLine("  3. Check configuration: [cyan]honua admin config toggle --protocol wfs[/]");

            if (gitOpsState.AutoSync)
            {
                _console.WriteLine();
                _console.MarkupLine("[yellow]Note: Auto-sync is enabled. Changes will be applied automatically on drift detection.[/]");
            }

            if (gitOpsState.RequiresApproval)
            {
                _console.WriteLine();
                _console.MarkupLine("[yellow]Note: Configuration changes require manual approval.[/]");
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

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--repo-url")]
        [Description("Git repository URL containing configuration (required)")]
        public string RepoUrl { get; set; } = string.Empty;

        [CommandOption("--branch")]
        [Description("Git branch to sync from")]
        [DefaultValue("main")]
        public string Branch { get; set; } = "main";

        [CommandOption("--config-path")]
        [Description("Path to configuration within repository (required)")]
        public string ConfigPath { get; set; } = string.Empty;

        [CommandOption("--auto-sync")]
        [Description("Enable automatic synchronization on drift detection")]
        [DefaultValue(false)]
        public bool AutoSync { get; set; }

        [CommandOption("--auto-approve")]
        [Description("Skip manual approval for configuration changes")]
        [DefaultValue(false)]
        public bool AutoApprove { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
