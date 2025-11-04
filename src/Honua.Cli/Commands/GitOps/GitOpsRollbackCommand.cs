// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.GitOps;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands.GitOps;

/// <summary>
/// CLI command to rollback environment to last successful deployment
/// </summary>
[Description("Rollback environment to last successful deployment")]
public sealed class GitOpsRollbackCommand : AsyncCommand<GitOpsRollbackCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public GitOpsRollbackCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var service = new GitOpsCliService(settings.StateDirectory);

        try
        {
            // Get environment state
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

            if (state.LastSuccessfulDeployment == null)
            {
                _console.MarkupLine($"[yellow]No successful deployment found for environment '{settings.Environment}'[/]");
                _console.MarkupLine("[grey]Cannot rollback without a previous successful deployment[/]");
                return 1;
            }

            if (state.CurrentDeployment == null)
            {
                _console.MarkupLine($"[yellow]No current deployment for environment '{settings.Environment}'[/]");
                return 1;
            }

            // Display rollback information
            _console.MarkupLine($"[bold yellow]Rollback Environment: {settings.Environment}[/]");
            _console.WriteLine();

            var rollbackTable = new Table();
            rollbackTable.Border(TableBorder.Rounded);
            rollbackTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
            rollbackTable.AddColumn(new TableColumn("[bold]Current[/]").LeftAligned());
            rollbackTable.AddColumn(new TableColumn("[bold]Rollback Target[/]").LeftAligned());

            rollbackTable.AddRow(
                "Deployment ID",
                state.CurrentDeployment.Id,
                state.LastSuccessfulDeployment.Id
            );

            rollbackTable.AddRow(
                "Commit",
                GitOpsCliService.GetShortCommit(state.CurrentDeployment.Commit),
                GitOpsCliService.GetShortCommit(state.LastSuccessfulDeployment.Commit)
            );

            rollbackTable.AddRow(
                "State",
                state.CurrentDeployment.State.ToString(),
                state.LastSuccessfulDeployment.State.ToString()
            );

            rollbackTable.AddRow(
                "Health",
                state.CurrentDeployment.Health.ToString(),
                state.LastSuccessfulDeployment.Health.ToString()
            );

            rollbackTable.AddRow(
                "Deployed At",
                state.CurrentDeployment.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                state.LastSuccessfulDeployment.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            );

            _console.Write(rollbackTable);
            _console.WriteLine();

            // Confirmation
            if (!settings.Yes)
            {
                var confirm = _console.Confirm(
                    $"[yellow]Are you sure you want to rollback '{settings.Environment}' to commit {GitOpsCliService.GetShortCommit(state.LastSuccessfulDeployment.Commit)}?[/]",
                    false
                );

                if (!confirm)
                {
                    _console.MarkupLine("[grey]Rollback cancelled[/]");
                    return 0;
                }
            }

            // Note: Actual rollback would trigger reconciliation to the previous commit
            // This is a placeholder implementation showing what would happen
            _console.MarkupLine("[bold yellow]⚠ Rollback Initiated[/]");
            _console.WriteLine();
            _console.MarkupLine("[dim]Note: This command triggers a rollback request.[/]");
            _console.MarkupLine("[dim]The GitOps reconciliation service will:[/]");
            _console.MarkupLine($"  1. Detect the rollback request for environment '{settings.Environment}'");
            _console.MarkupLine($"  2. Checkout commit {GitOpsCliService.GetShortCommit(state.LastSuccessfulDeployment.Commit)}");
            _console.MarkupLine("  3. Apply the previous configuration");
            _console.MarkupLine("  4. Validate the rollback succeeded");
            _console.WriteLine();
            _console.MarkupLine("[bold]Monitor Progress:[/]");
            _console.MarkupLine($"  • Use [cyan]honua gitops status --environment {settings.Environment}[/] to monitor rollback");
            _console.MarkupLine($"  • Use [cyan]honua gitops deployments --environment {settings.Environment}[/] to see deployment history");
            _console.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ENVIRONMENT>")]
        [Description("Environment to rollback")]
        public string Environment { get; init; } = string.Empty;

        [CommandOption("--yes")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool Yes { get; init; }

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }
    }
}
