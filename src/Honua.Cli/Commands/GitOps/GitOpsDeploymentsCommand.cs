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
/// CLI command to list recent GitOps deployments
/// </summary>
[Description("List recent GitOps deployments")]
public sealed class GitOpsDeploymentsCommand : AsyncCommand<GitOpsDeploymentsCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public GitOpsDeploymentsCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]GitOps Deployments[/]");
        _console.WriteLine();

        var service = new GitOpsCliService(settings.StateDirectory);

        try
        {
            List<string> environments;

            if (settings.Environment.HasValue())
            {
                // Single environment
                environments = new List<string> { settings.Environment };
            }
            else
            {
                // All environments
                environments = await service.ListEnvironmentsAsync(CancellationToken.None);

                if (environments.Count == 0)
                {
                    _console.MarkupLine("[yellow]No environments found[/]");
                    _console.MarkupLine("[grey]Run 'honua gitops init' to set up GitOps[/]");
                    return 1;
                }
            }

            var allDeployments = new List<(string Environment, DeploymentSummary Summary)>();

            foreach (var env in environments)
            {
                var history = await service.GetDeploymentHistoryAsync(env, settings.Limit, CancellationToken.None);
                foreach (var deployment in history)
                {
                    allDeployments.Add((env, deployment));
                }
            }

            if (allDeployments.Count == 0)
            {
                _console.MarkupLine("[yellow]No deployments found[/]");
                return 0;
            }

            // Sort by started time (most recent first)
            allDeployments = allDeployments
                .OrderByDescending(d => d.Summary.StartedAt)
                .Take(settings.Limit)
                .ToList();

            // Create table
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[bold]Environment[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]Deployment ID[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]Commit[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]State[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]Started[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]Duration[/]").RightAligned());
            table.AddColumn(new TableColumn("[bold]Initiated By[/]").LeftAligned());

            foreach (var (environment, deployment) in allDeployments)
            {
                var stateMarkup = GetStateMarkup(deployment.State);
                var shortCommit = GitOpsCliService.GetShortCommit(deployment.Commit);
                var relativeTime = GitOpsCliService.FormatRelativeTime(deployment.StartedAt);
                var duration = GitOpsCliService.FormatDuration(deployment.Duration);

                table.AddRow(
                    environment,
                    deployment.Id,
                    shortCommit,
                    stateMarkup,
                    relativeTime,
                    duration,
                    deployment.InitiatedBy
                );
            }

            _console.Write(table);
            _console.WriteLine();

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

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--environment <ENV>")]
        [Description("Filter by environment name")]
        public string? Environment { get; init; }

        [CommandOption("--limit <N>")]
        [Description("Maximum number of deployments to show (default: 10)")]
        [DefaultValue(10)]
        public int Limit { get; init; } = 10;

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }
    }
}
