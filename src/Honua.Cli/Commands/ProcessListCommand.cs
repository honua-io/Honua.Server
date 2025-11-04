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
/// CLI command to list all running Semantic Kernel processes.
/// </summary>
[Description("List all running processes")]
public sealed class ProcessListCommand : AsyncCommand<ProcessListCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public ProcessListCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[yellow]⚠ This command is not yet fully implemented.[/]");
        _console.WriteLine();
        _console.MarkupLine("[dim]The process framework requires persistent state management to track running processes.[/]");
        _console.MarkupLine("[dim]This feature is under active development and will be available in version 2.0.[/]");
        _console.WriteLine();
        _console.MarkupLine("[bold]What you can do now:[/]");
        _console.MarkupLine("  • Start a deployment: [cyan]honua process deploy --name my-deployment --provider AWS[/]");
        _console.MarkupLine("  • Start an upgrade: [cyan]honua process upgrade --deployment-name my-deployment --target-version 2.0.0[/]");
        _console.MarkupLine("  • Extract metadata: [cyan]honua process metadata --dataset-path ./data/raster.tif[/]");
        _console.MarkupLine("  • Sync GitOps config: [cyan]honua process gitops --repo-url https://github.com/org/config --config-path config/[/]");
        _console.MarkupLine("  • Run a benchmark: [cyan]honua process benchmark --target-endpoint http://localhost:5000[/]");
        _console.WriteLine();
        _console.MarkupLine("[dim]Track progress: https://github.com/honuaio/honua/issues[/]");

        return Task.FromResult(1); // Return error code
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("Show all processes including completed ones")]
        [DefaultValue(false)]
        public bool ShowAll { get; set; }

        [CommandOption("--type")]
        [Description("Filter by process type (deployment, upgrade, metadata, gitops, benchmark)")]
        public string? ProcessType { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
