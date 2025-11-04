// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class MigrationCancelCommand : AsyncCommand<MigrationCancelCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IMigrationApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<MigrationCancelCommand> _logger;

    public MigrationCancelCommand(IAnsiConsole console, IMigrationApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<MigrationCancelCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(async () =>
        {
            if (!Guid.TryParse(settings.JobId, out var jobId))
            {
                _console.MarkupLine("[red]Invalid job ID format.[/]");
                return 1;
            }

            ControlPlaneConnection connection;
            try
            {
                connection = await _connectionResolver.ResolveAsync(settings.Host, settings.Token, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]{ex.Message}[/]");
                return 1;
            }

            var job = await _apiClient.CancelJobAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);

            if (job is null)
            {
                _console.MarkupLine($"[red]Migration job {jobId} was not found.[/]");
                return 1;
            }

            _console.MarkupLine($"[green]Migration job {jobId} cancellation requested.[/]");
            _console.MarkupLine($"[bold]Current Status:[/] [yellow]{job.Status}[/]");

            return 0;
        }, _logger, "migration-cancel");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Migration job ID to cancel")]
        public string? JobId { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
