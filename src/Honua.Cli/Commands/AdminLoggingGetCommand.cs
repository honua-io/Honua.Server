// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class AdminLoggingGetCommand : AsyncCommand<AdminLoggingGetCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly LoggingApiClient _apiClient;
    private readonly ILogger<AdminLoggingGetCommand> _logger;

    public AdminLoggingGetCommand(IAnsiConsole console, LoggingApiClient apiClient, ILogger<AdminLoggingGetCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                var connection = ControlPlaneConnection.Create(settings.Host ?? "http://localhost:5000", settings.Token);

                using var levelsDoc = await _apiClient.GetLogLevelsAsync(connection, default).ConfigureAwait(false);

                if (levelsDoc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                    !levelsDoc.RootElement.EnumerateObject().Any())
                {
                    _console.MarkupLine("[yellow]No log level overrides configured[/]");
                    return 0;
                }

                var table = new Table();
                table.AddColumn("Category");
                table.AddColumn("Level");

                foreach (var prop in levelsDoc.RootElement.EnumerateObject().OrderBy(p => p.Name))
                {
                    var category = prop.Name;
                    var level = prop.Value.GetString() ?? "Unknown";

                    var color = level.ToLowerInvariant() switch
                    {
                        "error" or "critical" => "red",
                        "warning" => "yellow",
                        "information" => "green",
                        "debug" or "trace" => "grey",
                        _ => "white"
                    };
                    table.AddRow(category, $"[{color}]{level}[/]");
                }

                _console.Write(table);
                return 0;
            },
            _logger,
            "admin-logging-get");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
