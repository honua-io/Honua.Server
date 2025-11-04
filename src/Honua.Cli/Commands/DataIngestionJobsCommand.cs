// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Honua.Server.Core.Import;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class DataIngestionJobsCommand : AsyncCommand<DataIngestionJobsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IDataIngestionApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<DataIngestionJobsCommand> _logger;

    public DataIngestionJobsCommand(IAnsiConsole console, IDataIngestionApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<DataIngestionJobsCommand> logger)
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

            var jobs = await _apiClient.ListJobsAsync(connection, CancellationToken.None).ConfigureAwait(false);
            if (jobs.Count == 0)
            {
                _console.MarkupLine("[grey]No ingestion jobs were found on the control plane.[/]");
                return 0;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Job Id");
            table.AddColumn("Service/Layer");
            table.AddColumn("Status");
            table.AddColumn("Progress");
            table.AddColumn("Stage");
            table.AddColumn("Created");

            foreach (var snapshot in jobs.OrderByDescending(job => job.CreatedAtUtc))
            {
                table.AddRow(
                    snapshot.JobId.ToString(),
                    $"{snapshot.ServiceId}/{snapshot.LayerId}",
                    snapshot.Status.ToString(),
                    $"{Math.Round(snapshot.Progress * 100, 0)}%",
                    snapshot.Stage,
                    snapshot.CreatedAtUtc.ToString("u"));
            }

            _console.Write(table);
            return 0;
        }, _logger, "data-ingestion-jobs");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
