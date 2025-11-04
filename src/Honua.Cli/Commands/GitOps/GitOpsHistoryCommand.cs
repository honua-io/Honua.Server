// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.GitOps;
using Honua.Server.Core.Deployment;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands.GitOps;

/// <summary>
/// CLI command to show deployment history for an environment
/// </summary>
[Description("Show deployment history for an environment")]
public sealed class GitOpsHistoryCommand : AsyncCommand<GitOpsHistoryCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public GitOpsHistoryCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var service = new GitOpsCliService(settings.StateDirectory);

        try
        {
            // Verify environment exists
            var state = await service.GetEnvironmentStateAsync(settings.Environment, CancellationToken.None);

            if (state == null)
            {
                _console.MarkupLine($"[red]Environment '{settings.Environment}' not found[/]");
                _console.MarkupLine("[grey]Available environments:[/]");
                var environments = await service.ListEnvironmentsAsync(CancellationToken.None);
                foreach (var env in environments)
                {
                    _console.MarkupLine($"  • {env}");
                }
                return 1;
            }

            // Get deployment history
            var history = await service.GetDeploymentHistoryAsync(settings.Environment, settings.Limit, CancellationToken.None);

            if (history.Count == 0)
            {
                _console.MarkupLine($"[yellow]No deployment history found for environment '{settings.Environment}'[/]");
                return 0;
            }

            // Header
            _console.MarkupLine($"[bold cyan]Deployment History: {settings.Environment}[/]");
            _console.WriteLine();

            if (settings.Timeline)
            {
                // Timeline format
                foreach (var deployment in history.OrderBy(d => d.StartedAt))
                {
                    var stateIcon = GetStateIcon(deployment.State);
                    var stateColor = GetStateColor(deployment.State);
                    var timestamp = deployment.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    var shortCommit = GitOpsCliService.GetShortCommit(deployment.Commit);
                    var duration = deployment.Duration.HasValue
                        ? $" ([grey]{GitOpsCliService.FormatDuration(deployment.Duration)}[/])"
                        : "";

                    _console.MarkupLine($"[{stateColor}]{stateIcon}[/] [bold]{timestamp}[/] - [{stateColor}]{deployment.State}[/] - Commit {shortCommit}{duration}");

                    if (!deployment.InitiatedBy.IsNullOrEmpty())
                    {
                        _console.MarkupLine($"    [grey]Initiated by: {deployment.InitiatedBy}[/]");
                    }

                    _console.WriteLine();
                }
            }
            else
            {
                // Table format
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn(new TableColumn("[bold]Deployment ID[/]").LeftAligned());
                table.AddColumn(new TableColumn("[bold]Commit[/]").LeftAligned());
                table.AddColumn(new TableColumn("[bold]State[/]").LeftAligned());
                table.AddColumn(new TableColumn("[bold]Started[/]").LeftAligned());
                table.AddColumn(new TableColumn("[bold]Completed[/]").LeftAligned());
                table.AddColumn(new TableColumn("[bold]Duration[/]").RightAligned());
                table.AddColumn(new TableColumn("[bold]Initiated By[/]").LeftAligned());

                foreach (var deployment in history)
                {
                    var stateMarkup = GetStateMarkup(deployment.State);
                    var shortCommit = GitOpsCliService.GetShortCommit(deployment.Commit);
                    var startedTime = GitOpsCliService.FormatRelativeTime(deployment.StartedAt);
                    var completedTime = deployment.CompletedAt.HasValue
                        ? GitOpsCliService.FormatRelativeTime(deployment.CompletedAt.Value)
                        : "N/A";
                    var duration = GitOpsCliService.FormatDuration(deployment.Duration);

                    table.AddRow(
                        deployment.Id,
                        shortCommit,
                        stateMarkup,
                        startedTime,
                        completedTime,
                        duration,
                        deployment.InitiatedBy
                    );
                }

                _console.Write(table);
                _console.WriteLine();
            }

            // Summary statistics
            if (settings.Verbose)
            {
                var totalDeployments = history.Count;
                var completedDeployments = history.Count(d => d.State == DeploymentState.Completed);
                var failedDeployments = history.Count(d => d.State == DeploymentState.Failed);
                var rolledBackDeployments = history.Count(d => d.State == DeploymentState.RolledBack);
                var successRate = totalDeployments > 0
                    ? (completedDeployments * 100.0 / totalDeployments)
                    : 0.0;

                _console.MarkupLine("[bold]Summary Statistics[/]");
                var statsTable = new Table();
                statsTable.Border(TableBorder.Rounded);
                statsTable.AddColumn(new TableColumn("[bold]Metric[/]").LeftAligned());
                statsTable.AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

                statsTable.AddRow("Total Deployments", totalDeployments.ToString());
                statsTable.AddRow("Completed", $"[green]{completedDeployments}[/]");
                statsTable.AddRow("Failed", $"[red]{failedDeployments}[/]");
                statsTable.AddRow("Rolled Back", $"[yellow]{rolledBackDeployments}[/]");
                statsTable.AddRow("Success Rate", $"{successRate:F1}%");

                var avgDuration = history
                    .Where(d => d.Duration.HasValue)
                    .Select(d => d.Duration!.Value)
                    .DefaultIfEmpty()
                    .Average(d => d.TotalSeconds);

                if (avgDuration > 0)
                {
                    statsTable.AddRow("Avg Duration", GitOpsCliService.FormatDuration(TimeSpan.FromSeconds(avgDuration)));
                }

                _console.Write(statsTable);
                _console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static string GetStateMarkup(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Completed => "[green]Completed[/]",
            DeploymentState.Failed => "[red]Failed[/]",
            DeploymentState.RolledBack => "[yellow]RolledBack[/]",
            DeploymentState.AwaitingApproval => "[yellow]AwaitingApproval[/]",
            DeploymentState.Applying => "[blue]Applying[/]",
            DeploymentState.Validating => "[blue]Validating[/]",
            DeploymentState.Planning => "[blue]Planning[/]",
            DeploymentState.BackingUp => "[blue]BackingUp[/]",
            DeploymentState.PostValidating => "[blue]PostValidating[/]",
            DeploymentState.RollingBack => "[yellow]RollingBack[/]",
            DeploymentState.Pending => "[grey]Pending[/]",
            _ => state.ToString()
        };
    }

    private static string GetStateIcon(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Completed => "✓",
            DeploymentState.Failed => "✗",
            DeploymentState.RolledBack => "↶",
            DeploymentState.AwaitingApproval => "⏸",
            DeploymentState.Applying => "→",
            DeploymentState.Validating => "⊙",
            DeploymentState.Planning => "⊙",
            DeploymentState.BackingUp => "↓",
            DeploymentState.PostValidating => "⊙",
            DeploymentState.RollingBack => "↶",
            DeploymentState.Pending => "○",
            _ => "•"
        };
    }

    private static string GetStateColor(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Completed => "green",
            DeploymentState.Failed => "red",
            DeploymentState.RolledBack => "yellow",
            DeploymentState.AwaitingApproval => "yellow",
            DeploymentState.Applying => "blue",
            DeploymentState.Validating => "blue",
            DeploymentState.Planning => "blue",
            DeploymentState.BackingUp => "blue",
            DeploymentState.PostValidating => "blue",
            DeploymentState.RollingBack => "yellow",
            DeploymentState.Pending => "grey",
            _ => "white"
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ENVIRONMENT>")]
        [Description("Environment to show history for")]
        public string Environment { get; init; } = string.Empty;

        [CommandOption("--limit <N>")]
        [Description("Maximum number of deployments to show (default: 20)")]
        [DefaultValue(20)]
        public int Limit { get; init; } = 20;

        [CommandOption("--timeline")]
        [Description("Show history in timeline format")]
        [DefaultValue(false)]
        public bool Timeline { get; init; }

        [CommandOption("--verbose")]
        [Description("Show summary statistics")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }
    }
}
