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
/// CLI command to show detailed deployment information
/// </summary>
[Description("Show detailed deployment information")]
public sealed class GitOpsDeploymentCommand : AsyncCommand<GitOpsDeploymentCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public GitOpsDeploymentCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var service = new GitOpsCliService(settings.StateDirectory);

        try
        {
            var deployment = await service.GetDeploymentAsync(settings.DeploymentId, CancellationToken.None);

            if (deployment == null)
            {
                _console.MarkupLine($"[red]Deployment '{settings.DeploymentId}' not found[/]");
                return 1;
            }

            // Header
            _console.MarkupLine($"[bold cyan]Deployment Details: {deployment.Id}[/]");
            _console.WriteLine();

            // Basic Information
            var infoTable = new Table();
            infoTable.Border(TableBorder.Rounded);
            infoTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
            infoTable.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

            infoTable.AddRow("Environment", deployment.Environment);
            infoTable.AddRow("State", GetStateMarkup(deployment.State));
            infoTable.AddRow("Health", GetHealthMarkup(deployment.Health));
            infoTable.AddRow("Sync Status", GetSyncStatusMarkup(deployment.SyncStatus));
            infoTable.AddRow("Commit (SHA)", deployment.Commit);
            infoTable.AddRow("Branch", deployment.Branch);
            infoTable.AddRow("Initiated By", deployment.InitiatedBy);
            infoTable.AddRow("Started", deployment.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            if (deployment.CompletedAt.HasValue)
            {
                infoTable.AddRow("Completed", deployment.CompletedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                infoTable.AddRow("Duration", GitOpsCliService.FormatDuration(deployment.Duration));
            }

            if (!deployment.BackupId.IsNullOrEmpty())
            {
                infoTable.AddRow("Backup ID", deployment.BackupId);
            }

            if (!deployment.ErrorMessage.IsNullOrEmpty())
            {
                infoTable.AddRow("Error", $"[red]{deployment.ErrorMessage}[/]");
            }

            _console.Write(infoTable);
            _console.WriteLine();

            // Approval Status
            var approvalStatus = await service.GetApprovalStatusAsync(settings.DeploymentId, CancellationToken.None);
            if (approvalStatus != null)
            {
                _console.MarkupLine("[bold]Approval Status[/]");
                var approvalTable = new Table();
                approvalTable.Border(TableBorder.Rounded);
                approvalTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
                approvalTable.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

                approvalTable.AddRow("State", GetApprovalStateMarkup(approvalStatus.State));
                approvalTable.AddRow("Requested", approvalStatus.RequestedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

                if (approvalStatus.ExpiresAt.HasValue)
                {
                    var expiryStatus = approvalStatus.IsExpired ? "[red]Expired[/]" : "[green]Valid[/]";
                    approvalTable.AddRow("Expires", $"{approvalStatus.ExpiresAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({expiryStatus})");
                }

                if (approvalStatus.RespondedAt.HasValue)
                {
                    approvalTable.AddRow("Responded", approvalStatus.RespondedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                }

                if (!approvalStatus.Responder.IsNullOrEmpty())
                {
                    approvalTable.AddRow("Responder", approvalStatus.Responder);
                }

                if (!approvalStatus.Reason.IsNullOrEmpty())
                {
                    approvalTable.AddRow("Reason", approvalStatus.Reason);
                }

                _console.Write(approvalTable);
                _console.WriteLine();
            }

            // Deployment Plan
            if (deployment.Plan != null)
            {
                _console.MarkupLine("[bold]Deployment Plan[/]");
                var planTable = new Table();
                planTable.Border(TableBorder.Rounded);
                planTable.AddColumn(new TableColumn("[bold]Type[/]").LeftAligned());
                planTable.AddColumn(new TableColumn("[bold]Count[/]").RightAligned());

                planTable.AddRow("Added Resources", $"[green]{deployment.Plan.Added.Count}[/]");
                planTable.AddRow("Modified Resources", $"[yellow]{deployment.Plan.Modified.Count}[/]");
                planTable.AddRow("Removed Resources", $"[red]{deployment.Plan.Removed.Count}[/]");
                planTable.AddRow("Migrations", deployment.Plan.Migrations.Count.ToString());
                planTable.AddRow("Risk Level", GetRiskLevelMarkup(deployment.Plan.RiskLevel));
                planTable.AddRow("Breaking Changes", deployment.Plan.HasBreakingChanges ? "[red]Yes[/]" : "[green]No[/]");

                _console.Write(planTable);
                _console.WriteLine();

                // Show resource details if verbose
                if (settings.Verbose)
                {
                    if (deployment.Plan.Added.Count > 0)
                    {
                        _console.MarkupLine("[bold green]Added Resources:[/]");
                        foreach (var resource in deployment.Plan.Added)
                        {
                            _console.MarkupLine($"  [green]+[/] {resource.Type}: {resource.Name} ([grey]{resource.Path}[/])");
                        }
                        _console.WriteLine();
                    }

                    if (deployment.Plan.Modified.Count > 0)
                    {
                        _console.MarkupLine("[bold yellow]Modified Resources:[/]");
                        foreach (var resource in deployment.Plan.Modified)
                        {
                            var breaking = resource.IsBreaking ? " [red](BREAKING)[/]" : "";
                            _console.MarkupLine($"  [yellow]~[/] {resource.Type}: {resource.Name} ([grey]{resource.Path}[/]){breaking}");
                        }
                        _console.WriteLine();
                    }

                    if (deployment.Plan.Removed.Count > 0)
                    {
                        _console.MarkupLine("[bold red]Removed Resources:[/]");
                        foreach (var resource in deployment.Plan.Removed)
                        {
                            _console.MarkupLine($"  [red]-[/] {resource.Type}: {resource.Name} ([grey]{resource.Path}[/])");
                        }
                        _console.WriteLine();
                    }

                    if (deployment.Plan.Migrations.Count > 0)
                    {
                        _console.MarkupLine("[bold]Migrations:[/]");
                        foreach (var migration in deployment.Plan.Migrations)
                        {
                            _console.MarkupLine($"  • {migration.Id}: {migration.Description}");
                        }
                        _console.WriteLine();
                    }
                }
            }

            // Validation Results
            if (deployment.ValidationResults.Count > 0)
            {
                _console.MarkupLine("[bold]Validation Results[/]");
                var validationTable = new Table();
                validationTable.Border(TableBorder.Rounded);
                validationTable.AddColumn(new TableColumn("[bold]Type[/]").LeftAligned());
                validationTable.AddColumn(new TableColumn("[bold]Status[/]").LeftAligned());
                validationTable.AddColumn(new TableColumn("[bold]Message[/]").LeftAligned());

                foreach (var result in deployment.ValidationResults)
                {
                    var statusMarkup = result.Success ? "[green]Passed[/]" : "[red]Failed[/]";
                    validationTable.AddRow(result.Type, statusMarkup, result.Message);
                }

                _console.Write(validationTable);
                _console.WriteLine();
            }

            // State History
            if (deployment.StateHistory.Count > 0 && settings.Verbose)
            {
                _console.MarkupLine("[bold]State History[/]");
                var historyTable = new Table();
                historyTable.Border(TableBorder.Rounded);
                historyTable.AddColumn(new TableColumn("[bold]Timestamp[/]").LeftAligned());
                historyTable.AddColumn(new TableColumn("[bold]From[/]").LeftAligned());
                historyTable.AddColumn(new TableColumn("[bold]To[/]").LeftAligned());
                historyTable.AddColumn(new TableColumn("[bold]Message[/]").LeftAligned());

                foreach (var transition in deployment.StateHistory.OrderBy(h => h.Timestamp))
                {
                    var fromState = transition.From.HasValue ? transition.From.Value.ToString() : "N/A";
                    var toState = transition.To.ToString();
                    var message = transition.Message ?? "";

                    historyTable.AddRow(
                        transition.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                        fromState,
                        GetStateMarkup(transition.To),
                        message
                    );
                }

                _console.Write(historyTable);
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

    private static string GetHealthMarkup(DeploymentHealth health)
    {
        return health switch
        {
            DeploymentHealth.Healthy => "[green]Healthy[/]",
            DeploymentHealth.Degraded => "[yellow]Degraded[/]",
            DeploymentHealth.Unhealthy => "[red]Unhealthy[/]",
            DeploymentHealth.Progressing => "[blue]Progressing[/]",
            DeploymentHealth.Unknown => "[grey]Unknown[/]",
            _ => health.ToString()
        };
    }

    private static string GetSyncStatusMarkup(SyncStatus status)
    {
        return status switch
        {
            SyncStatus.Synced => "[green]Synced[/]",
            SyncStatus.OutOfSync => "[yellow]OutOfSync[/]",
            SyncStatus.Syncing => "[blue]Syncing[/]",
            SyncStatus.Unknown => "[grey]Unknown[/]",
            _ => status.ToString()
        };
    }

    private static string GetApprovalStateMarkup(ApprovalState state)
    {
        return state switch
        {
            ApprovalState.Pending => "[yellow]Pending[/]",
            ApprovalState.Approved => "[green]Approved[/]",
            ApprovalState.Rejected => "[red]Rejected[/]",
            _ => state.ToString()
        };
    }

    private static string GetRiskLevelMarkup(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => "[green]Low[/]",
            RiskLevel.Medium => "[yellow]Medium[/]",
            RiskLevel.High => "[orange1]High[/]",
            RiskLevel.Critical => "[red]Critical[/]",
            _ => level.ToString()
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<DEPLOYMENT_ID>")]
        [Description("Deployment ID to show details for")]
        public string DeploymentId { get; init; } = string.Empty;

        [CommandOption("--verbose")]
        [Description("Show detailed information including resource changes and state history")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }
    }
}
