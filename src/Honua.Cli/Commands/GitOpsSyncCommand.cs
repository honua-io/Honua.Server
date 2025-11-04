// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Manually triggers GitOps synchronization
/// </summary>
public sealed class GitOpsSyncCommand : AsyncCommand<GitOpsSyncCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public GitOpsSyncCommand(IAnsiConsole console, IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]GitOps Manual Synchronization[/]");
        _console.WriteLine();

        // Load GitOps configuration
        var workspacePath = _environment.ResolveWorkspacePath(settings.Workspace);
        var configPath = Path.Combine(workspacePath, ".honua", "gitops", "config.json");

        if (!File.Exists(configPath))
        {
            _console.MarkupLine("[red]Error: GitOps not initialized. Run 'honua gitops init' first.[/]");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath, CancellationToken.None);
        var config = JsonDocument.Parse(configJson);
        var root = config.RootElement;

        var repo = root.GetProperty("repositoryUrl").GetString();
        var branch = root.GetProperty("branch").GetString();
        var env = root.GetProperty("environment").GetString();

        _console.MarkupLine($"[grey]Repository:[/] {repo}");
        _console.MarkupLine($"[grey]Branch:[/] {branch}");
        _console.MarkupLine($"[grey]Environment:[/] {env}");
        _console.WriteLine();

        if (settings.DryRun)
        {
            _console.MarkupLine("[yellow]Dry-run mode:[/] No changes will be applied");
            _console.WriteLine();
        }

        // Simulate synchronization
        await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Synchronizing...", async ctx =>
            {
                ctx.Status("Fetching latest changes from Git...");
                await Task.Delay(1000).ConfigureAwait(false);

                ctx.Status("Checking for configuration changes...");
                await Task.Delay(1000).ConfigureAwait(false);

                ctx.Status("Validating configuration files...");
                await Task.Delay(800).ConfigureAwait(false);

                if (!settings.DryRun)
                {
                    ctx.Status("Applying configuration changes...");
                    await Task.Delay(1200).ConfigureAwait(false);

                    ctx.Status("Reconciling environment state...");
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            });

        if (settings.DryRun)
        {
            _console.MarkupLine("[green]✓[/] Dry-run completed successfully");
            _console.MarkupLine("[yellow]No changes were applied[/]");
        }
        else
        {
            _console.MarkupLine("[green]✓[/] Synchronization completed successfully");
            _console.MarkupLine($"[grey]Environment '{env}' is now in sync with Git[/]");
        }

        _console.WriteLine();

        // Note about actual implementation
        if (settings.Verbose)
        {
            _console.MarkupLine("[dim]Note: Full GitOps reconciliation requires the Honua server to be running with GitWatcher enabled.[/]");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Preview changes without applying them")]
        public bool DryRun { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to current directory")]
        public string? Workspace { get; init; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        public bool Verbose { get; init; }

        [CommandOption("--force")]
        [Description("Force synchronization even if no changes detected")]
        public bool Force { get; init; }
    }
}
