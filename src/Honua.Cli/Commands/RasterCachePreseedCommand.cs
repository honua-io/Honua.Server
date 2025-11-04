// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class RasterCachePreseedCommand : AsyncCommand<RasterCachePreseedCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _apiClient;

    public RasterCachePreseedCommand(IAnsiConsole console, IRasterTileCacheApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var datasetIds = settings.DatasetIds?.Select(id => id?.Trim())
            .Where(id => id.HasValue())
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (datasetIds.Length == 0)
        {
            _console.MarkupLine("[red]At least one --dataset-id value is required.[/]");
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

        var request = new RasterTilePreseedJobRequest(
            datasetIds,
            settings.TileMatrixSetId.IsNullOrWhiteSpace() ? null : settings.TileMatrixSetId,
            settings.MinZoom,
            settings.MaxZoom,
            settings.StyleId.IsNullOrWhiteSpace() ? null : settings.StyleId,
            settings.Transparent,
            settings.Format.IsNullOrWhiteSpace() ? null : settings.Format,
            settings.Overwrite,
            settings.TileSize);

        using var cts = new CancellationTokenSource();
        void Handler(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            _console.MarkupLine("[yellow]Cancellation requested. Attempting to stop preseed job...[/]");
            cts.Cancel();
        }

        Console.CancelKeyPress += Handler;

        try
        {
            var job = await _apiClient.EnqueueAsync(connection, request, cts.Token).ConfigureAwait(false);
            _console.MarkupLine($"Queued raster preseed job [green]{job.JobId}[/].");

            var pollInterval = TimeSpan.FromSeconds(Math.Max(0, settings.PollIntervalSeconds));
            return await MonitorJobAsync(connection, job.JobId, pollInterval, cts);
        }
        catch (OperationCanceledException)
        {
            _console.MarkupLine("[yellow]Preseed cancelled by user.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to enqueue preseed job: {ex.Message}[/]");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= Handler;
        }
    }

    private async Task<int> MonitorJobAsync(ControlPlaneConnection connection, Guid jobId, TimeSpan pollInterval, CancellationTokenSource cts)
    {
        RasterTilePreseedJobStatus? lastStatus = null;
        string? lastStage = null;
        int lastPercentage = -1;

        try
        {
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                var snapshot = await _apiClient.GetAsync(connection, jobId, cts.Token).ConfigureAwait(false);
                if (snapshot is null)
                {
                    _console.MarkupLine("[red]Preseed job was not found on the control plane.[/]");
                    return 1;
                }

                var percentage = (int)Math.Round(snapshot.Progress * 100, MidpointRounding.AwayFromZero);

                if (snapshot.Status != lastStatus || !string.Equals(snapshot.Stage, lastStage, StringComparison.OrdinalIgnoreCase) || percentage != lastPercentage)
                {
                    var color = ResolveStatusColor(snapshot.Status);
                    var datasets = snapshot.DatasetIds is { Count: > 0 }
                        ? string.Join(",", snapshot.DatasetIds)
                        : "(none)";
                    var message = snapshot.Message.IsNullOrWhiteSpace() ? string.Empty : $" - {snapshot.Message}";
                    _console.MarkupLine($"[{color}]{snapshot.Status}[/] {percentage}% {snapshot.Stage} [grey]datasets:{datasets}[/]{message}");
                    lastStatus = snapshot.Status;
                    lastStage = snapshot.Stage;
                    lastPercentage = percentage;
                }

                if (snapshot.Status is RasterTilePreseedJobStatus.Completed)
                {
                    _console.MarkupLine("[green]Raster cache preseed completed successfully.[/]");
                    return 0;
                }

                if (snapshot.Status is RasterTilePreseedJobStatus.Failed)
                {
                    _console.MarkupLine($"[red]Preseed job failed: {snapshot.Message ?? "Unknown error"}[/]");
                    return 1;
                }

                if (snapshot.Status is RasterTilePreseedJobStatus.Cancelled)
                {
                    _console.MarkupLine($"[yellow]Preseed job was cancelled: {snapshot.Message ?? "No additional details"}[/]");
                    return 1;
                }

                await Task.Delay(pollInterval, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await TryCancelRemoteJobAsync(connection, jobId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task TryCancelRemoteJobAsync(ControlPlaneConnection connection, Guid jobId)
    {
        try
        {
            await _apiClient.CancelAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            _console.MarkupLine("[yellow]Cancellation signal sent to control plane.[/]");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[yellow]Failed to cancel job: {ex.Message}[/]");
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
        [CommandOption("--dataset-id <ID>")]
        [Description("Raster dataset identifier to seed. Provide multiple --dataset-id options to warm several datasets.")]
        public string[]? DatasetIds { get; init; }

        [CommandOption("--matrix-set <ID>")]
        [Description("Tile matrix set identifier (default: WorldWebMercatorQuad).")]
        public string? TileMatrixSetId { get; init; }

        [CommandOption("--min-zoom <LEVEL>")]
        [Description("Optional minimum zoom level override.")]
        public int? MinZoom { get; init; }

        [CommandOption("--max-zoom <LEVEL>")]
        [Description("Optional maximum zoom level override.")]
        public int? MaxZoom { get; init; }

        [CommandOption("--style-id <ID>")]
        [Description("Optional style identifier to apply while rendering.")]
        public string? StyleId { get; init; }

        [CommandOption("--transparent <BOOLEAN>")]
        [Description("Override transparency flag (true/false). Defaults to server configuration.")]
        public bool? Transparent { get; init; }

        [CommandOption("--format <MIME>")]
        [Description("Requested MIME type (default: image/png).")]
        public string? Format { get; init; }

        [CommandOption("--overwrite")]
        [Description("Force regeneration even when cached tiles already exist.")]
        public bool Overwrite { get; init; }

        [CommandOption("--tile-size <PIXELS>")]
        [Description("Tile size in pixels (default: 256).")]
        public int? TileSize { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }

        [CommandOption("--poll-interval <SECONDS>")]
        [Description("Polling interval in seconds while monitoring the job (default: 2). Set to 0 to disable delay.")]
        [DefaultValue(2)]
        public int PollIntervalSeconds { get; init; } = 2;
    }
}
