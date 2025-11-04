// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class ConsultantCommand : AsyncCommand<ConsultantCommand.Settings>
{
    private readonly IConsultantWorkflow _workflow;
    private readonly IHonuaCliEnvironment _environment;
    private readonly Spectre.Console.IAnsiConsole _console;
    private readonly Honua.Cli.AI.Services.Agents.IAgentCoordinator? _agentCoordinator;

    public ConsultantCommand(
        IConsultantWorkflow workflow,
        IHonuaCliEnvironment environment,
        Spectre.Console.IAnsiConsole console,
        Honua.Cli.AI.Services.Agents.IAgentCoordinator? agentCoordinator = null)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _agentCoordinator = agentCoordinator;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);

        // If no prompt provided, enter interactive chat mode
        if (settings.Prompt.IsNullOrWhiteSpace() && !settings.NoInteractive)
        {
            return await EnterInteractiveModeAsync(workspace, settings);
        }

        // Otherwise, run single-turn mode
        var request = new ConsultantRequest(
            Prompt: settings.Prompt,
            DryRun: settings.DryRun,
            AutoApprove: settings.AutoApprove,
            SuppressLogging: settings.SuppressLogs,
            Verbose: settings.Verbose,
            WorkspacePath: workspace,
            Mode: settings.Mode,
            TrustHighConfidence: settings.TrustHighConfidence);

        var cancellationToken = CancellationToken.None;
        var result = await _workflow.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success ? 0 : 1;
    }

    private async Task<int> EnterInteractiveModeAsync(string workspace, Settings settings)
    {
        // For now, just direct users to use consultant-chat command
        // Interactive mode integration can be completed later
        Console.WriteLine("Interactive chat mode is available via the consultant-chat command.");
        Console.WriteLine("Please run: honua consultant-chat");

        return await Task.FromResult(0);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--prompt <PROMPT>")]
        [Description("Optional natural language prompt to seed the consultant.")]
        public string? Prompt { get; init; }

        [CommandOption("--dry-run")]
        [Description("Plan actions without executing deployment steps.")]
        public bool DryRun { get; init; }

        [CommandOption("--auto-approve")]
        [Description("Automatically approve the generated plan (reserved for future automation).")]
        public bool AutoApprove { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to the current directory.")]
        public string? Workspace { get; init; }

        [CommandOption("--no-log")]
        [Description("Skip writing an audit log entry for this session.")]
        public bool SuppressLogs { get; init; }

        [CommandOption("--verbose")]
        [Description("Print additional details (agent transcripts, file locations).")]
        public bool Verbose { get; init; }

        [CommandOption("--mode <MODE>")]
        [Description("Execution mode: auto (default), plan, or multi")]
        [TypeConverter(typeof(ConsultantExecutionModeConverter))]
        public ConsultantExecutionMode Mode { get; init; } = ConsultantExecutionMode.Auto;

        [CommandOption("--trust-high-confidence")]
        [Description("Automatically approve plans when all recommended patterns have High confidence (≥80%)")]
        public bool TrustHighConfidence { get; init; }

        [CommandOption("--no-interactive")]
        [Description("Disable interactive mode (useful for CI/CD or scripting)")]
        public bool NoInteractive { get; init; }
    }
}
