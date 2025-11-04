// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.GitOps;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands.GitOps;

/// <summary>
/// CLI command to reject a deployment awaiting approval
/// </summary>
[Description("Reject a deployment awaiting approval")]
public sealed class GitOpsRejectCommand : AsyncCommand<GitOpsRejectCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public GitOpsRejectCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var service = new GitOpsCliService(settings.StateDirectory);

        try
        {
            // Get deployment details
            var deployment = await service.GetDeploymentAsync(settings.DeploymentId, CancellationToken.None);

            if (deployment == null)
            {
                _console.MarkupLine($"[red]Deployment '{settings.DeploymentId}' not found[/]");
                return 1;
            }

            // Check if deployment is awaiting approval
            if (deployment.State != Server.Core.Deployment.DeploymentState.AwaitingApproval)
            {
                _console.MarkupLine($"[yellow]Deployment '{settings.DeploymentId}' is not awaiting approval (current state: {deployment.State})[/]");
                return 1;
            }

            // Get approval status
            var approvalStatus = await service.GetApprovalStatusAsync(settings.DeploymentId, CancellationToken.None);

            if (approvalStatus == null)
            {
                _console.MarkupLine($"[red]Approval record not found for deployment '{settings.DeploymentId}'[/]");
                return 1;
            }

            // Display deployment information
            _console.MarkupLine($"[bold yellow]Rejecting Deployment: {settings.DeploymentId}[/]");
            _console.WriteLine();

            var infoTable = new Table();
            infoTable.Border(TableBorder.Rounded);
            infoTable.AddColumn(new TableColumn("[bold]Property[/]").LeftAligned());
            infoTable.AddColumn(new TableColumn("[bold]Value[/]").LeftAligned());

            infoTable.AddRow("Environment", deployment.Environment);
            infoTable.AddRow("Commit", Honua.Cli.Services.GitOps.GitOpsCliService.GetShortCommit(deployment.Commit));
            infoTable.AddRow("Branch", deployment.Branch);
            infoTable.AddRow("Initiated By", deployment.InitiatedBy);
            infoTable.AddRow("Started", deployment.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            if (deployment.Plan != null)
            {
                infoTable.AddRow("Added Resources", deployment.Plan.Added.Count.ToString());
                infoTable.AddRow("Modified Resources", deployment.Plan.Modified.Count.ToString());
                infoTable.AddRow("Removed Resources", deployment.Plan.Removed.Count.ToString());
                infoTable.AddRow("Risk Level", deployment.Plan.RiskLevel.ToString());
            }

            _console.Write(infoTable);
            _console.WriteLine();

            // Validate reason is provided
            var reason = settings.Reason;
            if (reason.IsNullOrWhiteSpace())
            {
                reason = _console.Ask<string>("[yellow]Reason for rejection:[/]");
            }

            if (reason.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Rejection reason is required[/]");
                return 1;
            }

            // Determine rejecter name
            var rejecter = settings.Rejecter;
            if (rejecter.IsNullOrWhiteSpace())
            {
                rejecter = Environment.UserName ?? "cli-user";
            }

            // Reject deployment
            await service.RejectDeploymentAsync(settings.DeploymentId, rejecter, reason, CancellationToken.None);

            _console.MarkupLine($"[yellow]✗[/] Deployment '{settings.DeploymentId}' rejected by '{rejecter}'");
            _console.MarkupLine($"[grey]Reason: {reason}[/]");
            _console.WriteLine();

            // Next steps
            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine("  • The deployment will transition to Failed state");
            _console.MarkupLine("  • You may need to create a new deployment with corrected changes");
            _console.WriteLine();

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Unexpected error: {ex.Message}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<DEPLOYMENT_ID>")]
        [Description("Deployment ID to reject")]
        public string DeploymentId { get; init; } = string.Empty;

        [CommandOption("--reason <TEXT>")]
        [Description("Reason for rejection (will prompt if not provided)")]
        public string? Reason { get; init; }

        [CommandOption("--rejecter <NAME>")]
        [Description("Name of rejecter (defaults to current user)")]
        public string? Rejecter { get; init; }

        [CommandOption("--state-directory <PATH>")]
        [Description("Path to GitOps state directory (default: ./data/gitops-state/)")]
        public string? StateDirectory { get; init; }
    }
}
