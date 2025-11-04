// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to resume a paused Semantic Kernel process.
/// </summary>
[Description("Resume a paused process")]
public sealed class ProcessResumeCommand : AsyncCommand<ProcessResumeCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public ProcessResumeCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[yellow]⚠ This command is not yet fully implemented.[/]");
        _console.WriteLine();
        _console.MarkupLine("[dim]The process framework requires persistent state management to support pause/resume.[/]");
        _console.MarkupLine("[dim]This feature is under active development and will be available in version 2.0.[/]");
        _console.WriteLine();
        _console.MarkupLine("[bold]What you can do now:[/]");
        _console.MarkupLine("  • Start a new process using one of the available process commands");
        _console.MarkupLine("  • Run [cyan]honua process --help[/] to see available process types");
        _console.WriteLine();
        _console.MarkupLine("[dim]Track progress: https://github.com/honuaio/honua/issues[/]");

        return Task.FromResult(1); // Return error code
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<process-id>")]
        [Description("Process ID to resume")]
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
