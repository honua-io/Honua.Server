// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Cli.AI.Services.Processes;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to rollback a failed process by undoing completed steps in reverse order.
/// </summary>
[Description("Rollback a failed process by undoing completed steps")]
public sealed class ProcessRollbackCommand : AsyncCommand<ProcessRollbackCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IServiceProvider _serviceProvider;

    public ProcessRollbackCommand(IAnsiConsole console, IServiceProvider serviceProvider)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Process Rollback[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.ProcessId.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Process ID is required[/]");
                _console.MarkupLine("[dim]Usage: honua process rollback <process-id>[/]");
                return 1;
            }

            // Get the rollback orchestrator
            var orchestrator = _serviceProvider.GetService<ProcessRollbackOrchestrator>();
            if (orchestrator == null)
            {
                _console.MarkupLine("[red]Error: Process rollback orchestrator not available[/]");
                _console.MarkupLine("[yellow]Make sure the Process Framework is properly configured[/]");
                return 1;
            }

            // Confirm with user unless --force
            if (!settings.Force)
            {
                _console.MarkupLine("[yellow]This will rollback all completed steps for process:[/] [cyan]{0}[/]", settings.ProcessId);
                _console.MarkupLine("[yellow]Warning: Rollback operations may not be fully reversible.[/]");
                _console.WriteLine();

                if (!_console.Confirm("Are you sure you want to continue?", false))
                {
                    _console.MarkupLine("[dim]Rollback cancelled by user[/]");
                    return 0;
                }
                _console.WriteLine();
            }

            // Execute rollback with status display
            RollbackResult? result = null;

            await _console.Status()
                .StartAsync("Rolling back process...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    ctx.Status($"Rolling back process {settings.ProcessId}...");

                    result = await orchestrator.RollbackProcessAsync(
                        settings.ProcessId,
                        CancellationToken.None);

                    ctx.Status("Rollback completed");
                });

            if (result == null)
            {
                _console.MarkupLine("[red]Error: Rollback failed to complete[/]");
                return 1;
            }

            // Display results
            _console.WriteLine();
            _console.MarkupLine("[bold]Rollback Summary[/]");
            _console.WriteLine();

            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Property")
                .AddColumn("Value");

            summaryTable.AddRow("[cyan]Process ID[/]", result.ProcessId);
            summaryTable.AddRow("[cyan]Workflow Type[/]", result.WorkflowType);
            summaryTable.AddRow("[cyan]Total Steps[/]", result.TotalSteps.ToString());
            summaryTable.AddRow("[cyan]Successful[/]",
                $"[green]{result.SuccessfulRollbacks}[/]");
            summaryTable.AddRow("[cyan]Failed[/]",
                result.FailedRollbacks > 0
                    ? $"[red]{result.FailedRollbacks}[/]"
                    : $"[green]{result.FailedRollbacks}[/]");

            _console.Write(summaryTable);
            _console.WriteLine();

            // Display detailed step results
            if (result.Steps.Count > 0)
            {
                _console.MarkupLine("[bold]Step Details[/]");
                _console.WriteLine();

                var stepTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Step")
                    .AddColumn("Status")
                    .AddColumn("Details");

                foreach (var step in result.Steps)
                {
                    var status = step.IsSuccess ? "[green]Success[/]" : $"[red]Failed[/]";
                    var details = step.IsSuccess
                        ? (step.Details ?? "-")
                        : (step.Error ?? "Unknown error");

                    stepTable.AddRow(
                        step.StepName,
                        status,
                        details.EscapeMarkup()
                    );
                }

                _console.Write(stepTable);
            }

            _console.WriteLine();

            // Final status
            if (result.IsFullySuccessful)
            {
                _console.MarkupLine("[green]✓ Process successfully rolled back[/]");
                return 0;
            }
            else if (result.IsPartiallySuccessful)
            {
                _console.MarkupLine("[yellow]⚠ Process partially rolled back[/]");
                _console.MarkupLine("[yellow]Some steps failed to rollback. Manual intervention may be required.[/]");
                return 2;
            }
            else
            {
                _console.MarkupLine("[red]✗ Rollback failed[/]");
                _console.MarkupLine("[red]All rollback attempts failed. Manual intervention required.[/]");
                return 1;
            }
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
        [CommandArgument(0, "<PROCESS_ID>")]
        [Description("Process ID to rollback")]
        public string ProcessId { get; set; } = string.Empty;

        [CommandOption("--force")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool Force { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
