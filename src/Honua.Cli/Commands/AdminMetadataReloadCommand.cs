// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class AdminMetadataReloadCommand : AsyncCommand<AdminMetadataReloadCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly MetadataApiClient _apiClient;
    private readonly ILogger<AdminMetadataReloadCommand> _logger;

    public AdminMetadataReloadCommand(IAnsiConsole console, MetadataApiClient apiClient, ILogger<AdminMetadataReloadCommand> logger)
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

                _console.MarkupLine("[cyan]Reloading metadata from source...[/]");
                await _apiClient.ReloadMetadataAsync(connection, default).ConfigureAwait(false);
                _console.MarkupLine("[green]Metadata reloaded successfully[/]");
                return 0;
            },
            _logger,
            "admin-metadata-reload");
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
