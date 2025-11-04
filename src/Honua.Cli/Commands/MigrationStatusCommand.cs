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
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class MigrationStatusCommand : AsyncCommand<MigrationStatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IMigrationApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<MigrationStatusCommand> _logger;

    public MigrationStatusCommand(IAnsiConsole console, IMigrationApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<MigrationStatusCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                if (!Guid.TryParse(settings.JobId, out var jobId))
                {
                    _console.MarkupLine("[red]Invalid job ID format.[/]");
                    return 1;
                }

                var connection = await _connectionResolver.ResolveAsync(settings.Host, settings.Token, CancellationToken.None).ConfigureAwait(false);

                var job = await _apiClient.GetJobAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);

                if (job is null)
                {
                    _console.MarkupLine($"[red]Migration job {jobId} was not found.[/]");
                    return 1;
                }

                var statusColor = job.Status.ToString() switch
                {
                    "Completed" => "green",
                    "Failed" => "red",
                    "Cancelled" => "yellow",
                    _ => "cyan"
                };

                _console.MarkupLine($"[bold]Job ID:[/] {job.JobId}");
                _console.MarkupLine($"[bold]Service ID:[/] {job.ServiceId}");
                _console.MarkupLine($"[bold]Data Source:[/] {job.DataSourceId}");
                _console.MarkupLine($"[bold]Status:[/] [{statusColor}]{job.Status}[/]");
                _console.MarkupLine($"[bold]Progress:[/] {(int)Math.Round(job.Progress * 100)}%");
                _console.MarkupLine($"[bold]Stage:[/] {job.Stage}");

                if (job.Message.HasValue())
                {
                    _console.MarkupLine($"[bold]Message:[/] {job.Message}");
                }

                _console.MarkupLine($"[bold]Created:[/] {job.CreatedAtUtc.ToLocalTime():g}");

                if (job.StartedAtUtc.HasValue)
                {
                    _console.MarkupLine($"[bold]Started:[/] {job.StartedAtUtc.Value.ToLocalTime():g}");
                }

                if (job.CompletedAtUtc.HasValue)
                {
                    _console.MarkupLine($"[bold]Completed:[/] {job.CompletedAtUtc.Value.ToLocalTime():g}");

                    var duration = job.CompletedAtUtc.Value - (job.StartedAtUtc ?? job.CreatedAtUtc);
                    _console.MarkupLine($"[bold]Duration:[/] {duration:hh\\:mm\\:ss}");
                }

                return 0;
            },
            _logger,
            "migration-status");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Migration job ID")]
        public string? JobId { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
