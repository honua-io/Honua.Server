// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Refines a previous consultant plan based on user feedback.
/// Enables conversational iteration without starting from scratch.
/// </summary>
public sealed class ConsultantRefineCommand : AsyncCommand<ConsultantRefineCommand.Settings>
{
    private readonly IConsultantWorkflow _workflow;
    private readonly IConsultantSessionStore _sessionStore;
    private readonly IHonuaCliEnvironment _environment;

    public ConsultantRefineCommand(
        IConsultantWorkflow workflow,
        IConsultantSessionStore sessionStore,
        IHonuaCliEnvironment environment)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var cancellationToken = CancellationToken.None;

        // If no session ID provided, show recent sessions
        if (settings.SessionId.IsNullOrWhiteSpace())
        {
            return await ShowRecentSessionsAsync(cancellationToken);
        }

        // Retrieve the session
        var session = await _sessionStore.GetSessionAsync(settings.SessionId!, cancellationToken);
        if (session == null)
        {
            AnsiConsole.MarkupLine($"[red]✗ Session '{settings.SessionId}' not found[/]");
            AnsiConsole.MarkupLine("[dim]Use 'honua consultant refine' to see recent sessions[/]");
            return 1;
        }

        var (previousPlan, previousContext) = session.Value;

        // Validate adjustment request
        if (settings.Adjustment.IsNullOrWhiteSpace())
        {
            AnsiConsole.MarkupLine("[red]✗ Refinement adjustment is required (use --adjustment)[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]→ Refining session {settings.SessionId}[/]");
        AnsiConsole.MarkupLine($"[dim]  Adjustment: {settings.Adjustment}[/]");
        AnsiConsole.WriteLine();

        // Build conversation history
        var conversationHistory = new List<string>
        {
            $"Initial request: {previousContext.Request.Prompt}",
            $"Initial plan: {previousPlan.ExecutiveSummary}",
            $"Refinement: {settings.Adjustment}"
        };

        // Create refinement request
        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        var request = new ConsultantRequest(
            Prompt: settings.Adjustment,
            DryRun: settings.DryRun,
            AutoApprove: settings.AutoApprove,
            SuppressLogging: settings.SuppressLogs,
            Verbose: settings.Verbose,
            WorkspacePath: workspace,
            Mode: settings.Mode,
            TrustHighConfidence: settings.TrustHighConfidence,
            PreviousPlan: previousPlan,
            ConversationHistory: conversationHistory);

        var result = await _workflow.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success ? 0 : 1;
    }

    private async Task<int> ShowRecentSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await _sessionStore.GetRecentSessionsAsync(10, cancellationToken);

        if (!sessions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No recent consultant sessions found[/]");
            AnsiConsole.MarkupLine("[dim]Run 'honua consultant' to create a new session[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[bold]Recent Consultant Sessions:[/]");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Session ID");
        table.AddColumn("Status");

        foreach (var sessionId in sessions)
        {
            table.AddRow(sessionId, "[green]Available[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]To refine a session:[/]");
        AnsiConsole.MarkupLine("[dim]  honua consultant refine --session <id> --adjustment \"your refinement\"[/]");

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-s|--session <SESSION_ID>")]
        [Description("Session ID to refine (omit to list recent sessions)")]
        public string? SessionId { get; init; }

        [CommandOption("-a|--adjustment <TEXT>")]
        [Description("Refinement request (e.g., 'make it more secure', 'optimize for cost')")]
        public string? Adjustment { get; init; }

        [CommandOption("--dry-run")]
        [Description("Plan actions without executing deployment steps")]
        public bool DryRun { get; init; }

        [CommandOption("--auto-approve")]
        [Description("Automatically approve the generated plan")]
        public bool AutoApprove { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to the current directory")]
        public string? Workspace { get; init; }

        [CommandOption("--no-log")]
        [Description("Skip writing an audit log entry for this session")]
        public bool SuppressLogs { get; init; }

        [CommandOption("--verbose")]
        [Description("Print additional details (agent transcripts, file locations)")]
        public bool Verbose { get; init; }

        [CommandOption("--mode <MODE>")]
        [Description("Execution mode: auto (default), plan, or multi")]
        [TypeConverter(typeof(ConsultantExecutionModeConverter))]
        public ConsultantExecutionMode Mode { get; init; } = ConsultantExecutionMode.Auto;

        [CommandOption("--trust-high-confidence")]
        [Description("Automatically approve plans when all recommended patterns have High confidence (≥80%)")]
        public bool TrustHighConfidence { get; init; }
    }
}
