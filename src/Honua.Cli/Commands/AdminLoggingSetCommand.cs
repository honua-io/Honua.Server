// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class AdminLoggingSetCommand : AsyncCommand<AdminLoggingSetCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly LoggingApiClient _apiClient;

    public AdminLoggingSetCommand(IAnsiConsole console, LoggingApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.Category.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--category is required.[/]");
            return 1;
        }

        if (settings.Level.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--level is required.[/]");
            return 1;
        }

        ControlPlaneConnection connection;
        try
        {
            connection = ControlPlaneConnection.Create(settings.Host ?? "http://localhost:5000", settings.Token);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        try
        {
            await _apiClient.SetLogLevelAsync(connection, settings.Category, settings.Level, default).ConfigureAwait(false);
            _console.MarkupLine($"[green]Set log level for '{settings.Category}' to '{settings.Level}'[/]");
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to set log level: {ex.Message}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--category <CATEGORY>")]
        [Description("Logging category (e.g., 'Honua.Server.Core.Data', 'Microsoft.AspNetCore').")]
        public string? Category { get; init; }

        [CommandOption("--level <LEVEL>")]
        [Description("Log level: Trace, Debug, Information, Warning, Error, Critical, or None.")]
        public string? Level { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
