// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Manages GitOps configuration settings
/// </summary>
public sealed class GitOpsConfigCommand : AsyncCommand<GitOpsConfigCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public GitOpsConfigCommand(IAnsiConsole console, IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]GitOps Configuration Management[/]");
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
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        // Extract current values
        var config = new
        {
            repositoryUrl = root.GetProperty("repositoryUrl").GetString(),
            branch = root.GetProperty("branch").GetString(),
            environment = root.GetProperty("environment").GetString(),
            pollIntervalSeconds = root.GetProperty("pollIntervalSeconds").GetInt32(),
            authenticationMethod = root.GetProperty("authenticationMethod").GetString(),
            reconciliationStrategy = root.GetProperty("reconciliationStrategy").GetString(),
            enabled = root.GetProperty("enabled").GetBoolean(),
            createdAt = root.TryGetProperty("createdAt", out var ca) ? ca.GetDateTime() : DateTime.UtcNow
        };

        // Update configuration based on settings
        var updated = false;

        var newBranch = settings.Branch ?? config.branch;
        var newEnvironment = settings.Environment ?? config.environment;
        var newPollInterval = settings.PollInterval ?? config.pollIntervalSeconds;
        var newAuthMethod = settings.AuthMethod ?? config.authenticationMethod;
        var newReconciliation = config.reconciliationStrategy;
        var newEnabled = config.enabled;

        if (settings.EnableAutoReconcile)
        {
            newReconciliation = "automatic";
            updated = true;
        }
        else if (settings.DisableAutoReconcile)
        {
            newReconciliation = "manual";
            updated = true;
        }

        if (settings.Enable)
        {
            newEnabled = true;
            updated = true;
        }
        else if (settings.Disable)
        {
            newEnabled = false;
            updated = true;
        }

        if (settings.Branch != null || settings.Environment != null || settings.PollInterval != null || settings.AuthMethod != null)
        {
            updated = true;
        }

        if (!updated)
        {
            _console.MarkupLine("[yellow]No configuration changes specified[/]");
            _console.MarkupLine("[grey]Use options like --branch, --environment, --poll-interval, etc.[/]");
            return 1;
        }

        // Create updated configuration
        var updatedConfig = new
        {
            repositoryUrl = config.repositoryUrl,
            branch = newBranch,
            environment = newEnvironment,
            pollIntervalSeconds = newPollInterval,
            authenticationMethod = newAuthMethod,
            reconciliationStrategy = newReconciliation,
            enabled = newEnabled,
            createdAt = config.createdAt,
            updatedAt = DateTime.UtcNow
        };

        var updatedJson = JsonSerializer.Serialize(updatedConfig, JsonSerializerOptionsRegistry.WebIndented);

        await File.WriteAllTextAsync(configPath, updatedJson, CancellationToken.None);

        _console.MarkupLine("[green]✓[/] Configuration updated successfully");
        _console.WriteLine();

        // Display updated values
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");

        if (settings.Branch != null)
            table.AddRow("Branch", newBranch ?? "N/A");
        if (settings.Environment != null)
            table.AddRow("Environment", newEnvironment ?? "N/A");
        if (settings.PollInterval != null)
            table.AddRow("Poll Interval", $"{newPollInterval} seconds");
        if (settings.AuthMethod != null)
            table.AddRow("Authentication", newAuthMethod ?? "N/A");
        if (settings.EnableAutoReconcile || settings.DisableAutoReconcile)
            table.AddRow("Reconciliation", newReconciliation ?? "N/A");
        if (settings.Enable || settings.Disable)
            table.AddRow("Enabled", newEnabled ? "[green]Yes[/]" : "[red]No[/]");

        _console.Write(table);

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--branch <NAME>")]
        [Description("Update the Git branch to watch")]
        public string? Branch { get; init; }

        [CommandOption("--environment <NAME>")]
        [Description("Update the environment name")]
        public string? Environment { get; init; }

        [CommandOption("--poll-interval <SECONDS>")]
        [Description("Update the polling interval in seconds")]
        public int? PollInterval { get; init; }

        [CommandOption("--auth <METHOD>")]
        [Description("Update authentication method: ssh-key, https, or none")]
        public string? AuthMethod { get; init; }

        [CommandOption("--enable-auto-reconcile")]
        [Description("Enable automatic reconciliation")]
        public bool EnableAutoReconcile { get; init; }

        [CommandOption("--disable-auto-reconcile")]
        [Description("Disable automatic reconciliation")]
        public bool DisableAutoReconcile { get; init; }

        [CommandOption("--enable")]
        [Description("Enable GitOps watching")]
        public bool Enable { get; init; }

        [CommandOption("--disable")]
        [Description("Disable GitOps watching")]
        public bool Disable { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to current directory")]
        public string? Workspace { get; init; }
    }
}
