// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class RasterCacheStatusCommand : AsyncCommand<RasterCacheStatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _apiClient;

    public RasterCacheStatusCommand(IAnsiConsole console, IRasterTileCacheApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!Guid.TryParse(settings.JobId, out var jobId))
        {
            _console.MarkupLine("[red]A valid job identifier is required.[/]");
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
            var job = await _apiClient.GetAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            if (job is null)
            {
                _console.MarkupLine("[yellow]Job not found.[/]");
                return 1;
            }

            var datasets = job.DatasetIds is { Count: > 0 }
                ? string.Join(",", job.DatasetIds)
                : "(none)";

            var color = ResolveStatusColor(job.Status);
            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumn("Field");
            table.AddColumn("Value");
            table.AddRow("Job ID", job.JobId.ToString());
            table.AddRow("Status", $"[{color}]{job.Status}[/]");
            table.AddRow("Progress", $"{Math.Round(job.Progress * 100, MidpointRounding.AwayFromZero)}%");
            table.AddRow("Stage", job.Stage);
            table.AddRow("Message", job.Message ?? string.Empty);
            table.AddRow("Datasets", datasets);
            table.AddRow("Tile Matrix Set", job.TileMatrixSetId);
            table.AddRow("Tile Size", job.TileSize.ToString());
            table.AddRow("Format", job.Format);
            table.AddRow("Transparent", job.Transparent ? "true" : "false");
            table.AddRow("Overwrite", job.Overwrite ? "true" : "false");
            table.AddRow("Tiles Completed", job.TilesCompleted.ToString());
            table.AddRow("Tiles Total", job.TilesTotal.ToString());
            table.AddRow("Created (UTC)", job.CreatedAtUtc.ToString("u"));
            table.AddRow("Completed (UTC)", job.CompletedAtUtc?.ToString("u") ?? string.Empty);

            _console.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to retrieve job: {ex.Message}[/]");
            return 1;
        }
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
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Identifier of the raster preseed job.")]
        public string JobId { get; init; } = string.Empty;

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
