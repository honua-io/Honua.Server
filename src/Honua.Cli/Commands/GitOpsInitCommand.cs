// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Initializes GitOps configuration for a Honua environment
/// </summary>
public sealed class GitOpsInitCommand : AsyncCommand<GitOpsInitCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;

    public GitOpsInitCommand(IAnsiConsole console, IHonuaCliEnvironment environment)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold cyan]GitOps Configuration Initialization[/]");
        _console.WriteLine();

        // Validate repository URL
        if (settings.RepositoryUrl.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]Error: Repository URL is required (--repo)[/]");
            return 1;
        }

        // Default values
        var branch = settings.Branch ?? "main";
        var environment = settings.Environment ?? "production";
        var pollInterval = settings.PollInterval ?? 30;
        var authMethod = settings.AuthMethod ?? "ssh-key";

        // Create GitOps configuration directory
        var workspacePath = _environment.ResolveWorkspacePath(settings.Workspace);
        var gitOpsDir = Path.Combine(workspacePath, ".honua", "gitops");
        Directory.CreateDirectory(gitOpsDir);

        // Create configuration file
        var configPath = Path.Combine(gitOpsDir, "config.json");
        var config = new
        {
            repositoryUrl = settings.RepositoryUrl,
            branch,
            environment,
            pollIntervalSeconds = pollInterval,
            authenticationMethod = authMethod,
            reconciliationStrategy = settings.AutoReconcile ? "automatic" : "manual",
            enabled = true,
            createdAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json, CancellationToken.None);

        // Create environment directory structure
        var environmentsDir = Path.Combine(workspacePath, "environments");
        var envDir = Path.Combine(environmentsDir, environment);
        Directory.CreateDirectory(envDir);

        // Create sample metadata.json
        var metadataPath = Path.Combine(envDir, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            var metadata = new
            {
                environment,
                version = "1.0.0",
                services = Array.Empty<object>(),
                updatedAt = DateTime.UtcNow
            };
            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(metadataPath, metadataJson, CancellationToken.None);
        }

        // Create sample datasources.json
        var datasourcesPath = Path.Combine(envDir, "datasources.json");
        if (!File.Exists(datasourcesPath))
        {
            var datasources = new
            {
                environment,
                datasources = Array.Empty<object>()
            };
            var datasourcesJson = System.Text.Json.JsonSerializer.Serialize(datasources, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(datasourcesPath, datasourcesJson, CancellationToken.None);
        }

        // Create common directory
        var commonDir = Path.Combine(environmentsDir, "common");
        Directory.CreateDirectory(commonDir);

        var sharedConfigPath = Path.Combine(commonDir, "shared-config.json");
        if (!File.Exists(sharedConfigPath))
        {
            var sharedConfig = new
            {
                shared_settings = new
                {
                    cache_enabled = true,
                    logging_level = "info"
                }
            };
            var sharedJson = System.Text.Json.JsonSerializer.Serialize(sharedConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(sharedConfigPath, sharedJson, CancellationToken.None);
        }

        // Display summary
        _console.MarkupLine("[green]✓[/] GitOps configuration initialized successfully");
        _console.WriteLine();
        _console.MarkupLine($"[grey]Repository:[/] {settings.RepositoryUrl}");
        _console.MarkupLine($"[grey]Branch:[/] {branch}");
        _console.MarkupLine($"[grey]Environment:[/] {environment}");
        _console.MarkupLine($"[grey]Poll Interval:[/] {pollInterval} seconds");
        _console.MarkupLine($"[grey]Authentication:[/] {authMethod}");
        _console.MarkupLine($"[grey]Reconciliation:[/] {(settings.AutoReconcile ? "automatic" : "manual")}");
        _console.WriteLine();
        _console.MarkupLine($"[grey]Configuration saved to:[/] {configPath}");
        _console.MarkupLine($"[grey]Environment directory:[/] {envDir}");
        _console.WriteLine();

        if (!settings.AutoReconcile)
        {
            _console.MarkupLine("[yellow]Note:[/] Manual reconciliation mode enabled. Use 'honua gitops sync' to apply changes.");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--repo <URL>")]
        [Description("Git repository URL (required)")]
        public string? RepositoryUrl { get; init; }

        [CommandOption("--branch <NAME>")]
        [Description("Git branch to watch (default: main)")]
        public string? Branch { get; init; }

        [CommandOption("--environment <NAME>")]
        [Description("Environment name to manage (default: production)")]
        public string? Environment { get; init; }

        [CommandOption("--poll-interval <SECONDS>")]
        [Description("Polling interval in seconds (default: 30)")]
        public int? PollInterval { get; init; }

        [CommandOption("--auth <METHOD>")]
        [Description("Authentication method: ssh-key, https, or none (default: ssh-key)")]
        public string? AuthMethod { get; init; }

        [CommandOption("--auto-reconcile")]
        [Description("Enable automatic reconciliation when changes are detected")]
        public bool AutoReconcile { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Path to the Honua workspace; defaults to current directory")]
        public string? Workspace { get; init; }
    }
}
