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
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class AdminConfigToggleCommand : AsyncCommand<AdminConfigToggleCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly ConfigurationApiClient _apiClient;
    private readonly ILogger<AdminConfigToggleCommand> _logger;

    public AdminConfigToggleCommand(IAnsiConsole console, ConfigurationApiClient apiClient, ILogger<AdminConfigToggleCommand> logger)
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
                if (settings.Protocol.IsNullOrWhiteSpace())
                {
                    _console.MarkupLine("[red]--protocol is required.[/]");
                    return 1;
                }

                var connection = ControlPlaneConnection.Create(settings.Host ?? "http://localhost:5000", settings.Token);

                if (settings.ServiceId.IsNullOrWhiteSpace())
                {
                    // Global toggle
                    await _apiClient.ToggleGlobalProtocolAsync(connection, settings.Protocol, settings.Enabled, default).ConfigureAwait(false);
                    var state = settings.Enabled ? "enabled" : "disabled";
                    _console.MarkupLine($"[green]Globally {state} protocol '{settings.Protocol}'[/]");
                }
                else
                {
                    // Service-specific toggle
                    await _apiClient.ToggleServiceProtocolAsync(connection, settings.ServiceId, settings.Protocol, settings.Enabled, default).ConfigureAwait(false);
                    var state = settings.Enabled ? "enabled" : "disabled";
                    _console.MarkupLine($"[green]{state.ToUpper()} protocol '{settings.Protocol}' for service '{settings.ServiceId}'[/]");
                }
                return 0;
            },
            _logger,
            "admin-config-toggle");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--protocol <PROTOCOL>")]
        [Description("Protocol to toggle (e.g., 'wfs', 'wms', 'ogcapi').")]
        public string? Protocol { get; init; }

        [CommandOption("--enabled")]
        [Description("Enable the protocol (omit to disable).")]
        [DefaultValue(true)]
        public bool Enabled { get; init; } = true;

        [CommandOption("--service <SERVICE_ID>")]
        [Description("Service ID for service-specific toggle (omit for global).")]
        public string? ServiceId { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
