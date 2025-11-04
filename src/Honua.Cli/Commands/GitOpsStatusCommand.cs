// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.GitOps;
using Honua.Server.Core.Deployment;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Displays GitOps configuration and deployment status
/// </summary>
public sealed class GitOpsStatusCommand : AsyncCommand<GitOpsStatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public GitOpsStatusCommand(IAnsiConsole console, IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]GitOps Status[/]");
        _console.WriteLine();

        // Load GitOps configuration
        var workspacePath = _environment.ResolveWorkspacePath(settings.Workspace);
        var configPath = Path.Combine(workspacePath, ".honua", "gitops", "config.json");

        if (!File.Exists(configPath))
        {
            _console.MarkupLine("[yellow]GitOps is not initialized[/]");
            _console.MarkupLine("[grey]Run 'honua gitops init' to set up GitOps[/]");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath, CancellationToken.None);
        var config = JsonDocument.Parse(configJson);
        var root = config.RootElement;

        // Display configuration
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]Configuration[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

        table.AddRow("Repository", root.GetProperty("repositoryUrl").GetString() ?? "N/A");
        table.AddRow("Branch", root.GetProperty("branch").GetString() ?? "N/A");
        table.AddRow("Environment", root.GetProperty("environment").GetString() ?? "N/A");
        table.AddRow("Poll Interval", $"{root.GetProperty("pollIntervalSeconds").GetInt32()} seconds");
        table.AddRow("Authentication", root.GetProperty("authenticationMethod").GetString() ?? "N/A");
        table.AddRow("Reconciliation", root.GetProperty("reconciliationStrategy").GetString() ?? "N/A");
        table.AddRow("Enabled", root.GetProperty("enabled").GetBoolean() ? "[green]Yes[/]" : "[red]No[/]");

        if (root.TryGetProperty("createdAt", out var createdAt))
        {
            table.AddRow("Created", createdAt.GetDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }

        _console.Write(table);
        _console.WriteLine();

        // Check environment structure
        var environmentsDir = Path.Combine(workspacePath, "environments");
        var envName = root.GetProperty("environment").GetString();
        var envDir = Path.Combine(environmentsDir, envName ?? "production");

        if (Directory.Exists(envDir))
        {
            _console.MarkupLine("[bold]Environment Files:[/]");
            var filesTable = new Table();
            filesTable.Border(TableBorder.Rounded);
            filesTable.AddColumn("File");
            filesTable.AddColumn("Status");
            filesTable.AddColumn("Last Modified");

            var metadataPath = Path.Combine(envDir, "metadata.json");
            var datasourcesPath = Path.Combine(envDir, "datasources.json");
            var appsettingsPath = Path.Combine(envDir, "appsettings.json");

            AddFileRow(filesTable, "metadata.json", metadataPath);
            AddFileRow(filesTable, "datasources.json", datasourcesPath);
            AddFileRow(filesTable, "appsettings.json", appsettingsPath);

            _console.Write(filesTable);
            _console.WriteLine();
        }

        // Check common directory
        var commonDir = Path.Combine(environmentsDir, "common");
        if (Directory.Exists(commonDir))
        {
            _console.MarkupLine("[bold]Common Configuration:[/]");
            var commonTable = new Table();
            commonTable.Border(TableBorder.Rounded);
            commonTable.AddColumn("File");
            commonTable.AddColumn("Status");
            commonTable.AddColumn("Last Modified");

            var sharedConfigPath = Path.Combine(commonDir, "shared-config.json");
            AddFileRow(commonTable, "shared-config.json", sharedConfigPath);

            _console.Write(commonTable);
            _console.WriteLine();
        }

        // Display reconciliation status
        _console.MarkupLine("[bold]Reconciliation Status:[/]");
        var isAutoReconcile = root.GetProperty("reconciliationStrategy").GetString() == "automatic";
        if (isAutoReconcile)
        {
            _console.MarkupLine("[green]● Automatic reconciliation enabled[/]");
            _console.MarkupLine("[grey]Changes will be applied automatically when detected[/]");
        }
        else
        {
            _console.MarkupLine("[yellow]○ Manual reconciliation mode[/]");
            _console.MarkupLine("[grey]Use 'honua gitops sync' to apply changes[/]");
        }

        // Display deployment status if environment specified
        if (settings.Environment != null)
        {
            _console.WriteLine();
            await DisplayDeploymentStatusAsync(settings.Environment, settings.StateDirectory, CancellationToken.None);
        }
        else
        {
            // Show status for configured environment
            var configuredEnv = root.GetProperty("environment").GetString();
            if (!string.IsNullOrWhiteSpace(configuredEnv))
            {
                _console.WriteLine();
                await DisplayDeploymentStatusAsync(configuredEnv, settings.StateDirectory, CancellationToken.None);
            }
        }

        return 0;
    }

    private async Task DisplayDeploymentStatusAsync(string environment, string? stateDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var gitOpsService = new GitOpsCliService(stateDirectory);
            var state = await gitOpsService.GetEnvironmentStateAsync(environment, cancellationToken);

            if (state == null)
            {
                return;
            }

            _console.MarkupLine($"[bold]Deployment Status: {environment}[/]");

            var statusTable = new Table();
            statusTable.Border(TableBorder.Rounded);
            statusTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
            statusTable.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

            // Sync status
            var syncStatusMarkup = state.SyncStatus switch
            {
                SyncStatus.Synced => "[green]Synced[/]",
                SyncStatus.OutOfSync => "[yellow]OutOfSync[/]",
                SyncStatus.Syncing => "[blue]Syncing[/]",
                _ => "[grey]Unknown[/]"
            };
            statusTable.AddRow("Sync Status", syncStatusMarkup);

            // Health status
            var healthMarkup = state.Health switch
            {
                DeploymentHealth.Healthy => "[green]Healthy[/]",
                DeploymentHealth.Degraded => "[yellow]Degraded[/]",
                DeploymentHealth.Unhealthy => "[red]Unhealthy[/]",
                DeploymentHealth.Progressing => "[blue]Progressing[/]",
                _ => "[grey]Unknown[/]"
            };
            statusTable.AddRow("Health", healthMarkup);

            // Deployed commit
            if (!string.IsNullOrEmpty(state.DeployedCommit))
            {
                statusTable.AddRow("Deployed Commit", GitOpsCliService.GetShortCommit(state.DeployedCommit));
            }

            // Latest commit (would need Git integration)
            if (!string.IsNullOrEmpty(state.LatestCommit))
            {
                statusTable.AddRow("Latest Commit", GitOpsCliService.GetShortCommit(state.LatestCommit));
            }

            statusTable.AddRow("Last Updated", GitOpsCliService.FormatRelativeTime(state.LastUpdated));

            _console.Write(statusTable);
            _console.WriteLine();

            // Current deployment
            if (state.CurrentDeployment != null)
            {
                _console.MarkupLine("[bold]Current Deployment[/]");
                var deploymentTable = new Table();
                deploymentTable.Border(TableBorder.Rounded);
                deploymentTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
                deploymentTable.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

                deploymentTable.AddRow("Deployment ID", state.CurrentDeployment.Id);
                deploymentTable.AddRow("Commit", GitOpsCliService.GetShortCommit(state.CurrentDeployment.Commit));

                var stateMarkup = state.CurrentDeployment.State switch
                {
                    DeploymentState.Completed => "[green]Completed[/]",
                    DeploymentState.Failed => "[red]Failed[/]",
                    DeploymentState.AwaitingApproval => "[yellow]AwaitingApproval[/]",
                    DeploymentState.Applying => "[blue]Applying[/]",
                    _ => state.CurrentDeployment.State.ToString()
                };
                deploymentTable.AddRow("State", stateMarkup);

                deploymentTable.AddRow("Started", GitOpsCliService.FormatRelativeTime(state.CurrentDeployment.StartedAt));

                if (state.CurrentDeployment.Duration.HasValue)
                {
                    deploymentTable.AddRow("Duration", GitOpsCliService.FormatDuration(state.CurrentDeployment.Duration));
                }

                if (!string.IsNullOrEmpty(state.CurrentDeployment.ErrorMessage))
                {
                    deploymentTable.AddRow("Error", $"[red]{state.CurrentDeployment.ErrorMessage}[/]");
                }

                _console.Write(deploymentTable);
                _console.WriteLine();

                // Check for pending approval
                if (state.CurrentDeployment.State == DeploymentState.AwaitingApproval)
                {
                    _console.MarkupLine("[yellow]⚠ Deployment is awaiting approval[/]");
                    _console.MarkupLine($"  • Use [cyan]honua gitops approve {state.CurrentDeployment.Id}[/] to approve");
                    _console.MarkupLine($"  • Use [cyan]honua gitops reject {state.CurrentDeployment.Id}[/] to reject");
                    _console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[dim]Note: Could not load deployment status: {ex.Message}[/]");
        }
    }

    private void AddFileRow(Table table, string fileName, string filePath)
    {
        if (File.Exists(filePath))
        {
            var lastModified = File.GetLastWriteTime(filePath);
            table.AddRow(
                fileName,
                "[green]✓ Found[/]",
                lastModified.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
        else
        {
            table.AddRow(
                fileName,
                "[yellow]○ Missing[/]",
                "N/A"
            );
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to current directory")]
        public string? Workspace { get; init; }

        [CommandOption("--environment <ENV>")]
        [Description("Show deployment status for specific environment")]
        public string? Environment { get; init; }

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed information")]
        public bool Verbose { get; init; }
    }
}
