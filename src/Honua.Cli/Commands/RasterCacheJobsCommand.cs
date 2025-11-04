// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class RasterCacheJobsCommand : AsyncCommand<RasterCacheJobsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _apiClient;
    private readonly ILogger<RasterCacheJobsCommand> _logger;

    public RasterCacheJobsCommand(IAnsiConsole console, IRasterTileCacheApiClient apiClient, ILogger<RasterCacheJobsCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(async () =>
        {
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

            var jobs = await _apiClient.ListAsync(connection, CancellationToken.None).ConfigureAwait(false);
            if (jobs.Count == 0)
            {
                _console.MarkupLine("[yellow]No raster preseed jobs found.[/]");
                return 0;
            }

            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumn("Job ID");
            table.AddColumn("Status");
            table.AddColumn("Progress");
            table.AddColumn("Stage");
            table.AddColumn("Datasets");
            table.AddColumn("Created (UTC)");

            foreach (var job in jobs)
            {
                var datasets = job.DatasetIds is { Count: > 0 }
                    ? string.Join(",", job.DatasetIds)
                    : "(none)";
                var color = ResolveStatusColor(job.Status);

                table.AddRow(
                    job.JobId.ToString(),
                    $"[{color}]{job.Status}[/]",
                    $"{Math.Round(job.Progress * 100, MidpointRounding.AwayFromZero)}%",
                    job.Stage,
                    datasets,
                    job.CreatedAtUtc.ToString("u"));
            }

            _console.Write(table);
            return 0;
        }, _logger, "raster-cache-jobs");
    }

    private static string ResolveStatusColor(RasterTilePreseedJobStatus status)
        => status switch
        {
            RasterTilePreseedJobStatus.Completed => "green",
            RasterTilePreseedJobStatus.Failed => "red",
            RasterTilePreseedJobStatus.Cancelled => "yellow",
            _ => "cyan"
        };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
