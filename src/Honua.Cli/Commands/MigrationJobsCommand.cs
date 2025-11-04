// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class MigrationJobsCommand : AsyncCommand<MigrationJobsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IMigrationApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<MigrationJobsCommand> _logger;

    public MigrationJobsCommand(IAnsiConsole console, IMigrationApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<MigrationJobsCommand> logger)
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
                var connection = await _connectionResolver.ResolveAsync(settings.Host, settings.Token, CancellationToken.None).ConfigureAwait(false);

                var jobs = await _apiClient.ListJobsAsync(connection, CancellationToken.None).ConfigureAwait(false);

                if (jobs.Count == 0)
                {
                    _console.MarkupLine("[yellow]No migration jobs found.[/]");
                    return 0;
                }

                var table = new Table();
                table.AddColumn("Job ID");
                table.AddColumn("Service ID");
                table.AddColumn("Data Source");
                table.AddColumn("Status");
                table.AddColumn("Progress");
                table.AddColumn("Created");

                foreach (var job in jobs.OrderByDescending(j => j.CreatedAtUtc))
                {
                    var statusColor = job.Status.ToString() switch
                    {
                        "Completed" => "green",
                        "Failed" => "red",
                        "Cancelled" => "yellow",
                        _ => "cyan"
                    };

                    var progress = $"{(int)Math.Round(job.Progress * 100)}%";

                    table.AddRow(
                        job.JobId.ToString(),
                        job.ServiceId,
                        job.DataSourceId,
                        $"[{statusColor}]{job.Status}[/]",
                        progress,
                        job.CreatedAtUtc.ToLocalTime().ToString("g")
                    );
                }

                _console.Write(table);
                return 0;
            },
            _logger,
            "migration-jobs");
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
