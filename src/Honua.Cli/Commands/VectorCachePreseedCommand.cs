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

public sealed class VectorCachePreseedCommand : AsyncCommand<VectorCachePreseedCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IVectorTileCacheApiClient _apiClient;
    private readonly ILogger<VectorCachePreseedCommand> _logger;

    public VectorCachePreseedCommand(IAnsiConsole console, IVectorTileCacheApiClient apiClient, ILogger<VectorCachePreseedCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(async () =>
        {
            if (settings.ServiceId.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]--service-id is required.[/]");
                return 1;
            }

            if (settings.LayerId.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]--layer-id is required.[/]");
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

            var request = new VectorTilePreseedJobRequest(
                settings.ServiceId,
                settings.LayerId,
                settings.MinZoom,
                settings.MaxZoom,
                settings.Datetime.IsNullOrWhiteSpace() ? null : settings.Datetime,
                settings.Overwrite);

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
                _console.MarkupLine($"Queued vector preseed job [green]{job.JobId}[/].");

                var pollInterval = TimeSpan.FromSeconds(Math.Max(0, settings.PollIntervalSeconds));
                return await MonitorJobAsync(connection, job.JobId, pollInterval, cts);
            }
            finally
            {
                Console.CancelKeyPress -= Handler;
            }
        }, _logger, "vector-cache-preseed");
    }

    private async Task<int> MonitorJobAsync(ControlPlaneConnection connection, Guid jobId, TimeSpan pollInterval, CancellationTokenSource cts)
    {
        VectorTilePreseedJobStatus? lastStatus = null;
        int lastPercentage = -1;
        long lastProcessed = -1;

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

                if (snapshot.Status != lastStatus || percentage != lastPercentage || snapshot.TilesProcessed != lastProcessed)
                {
                    var color = ResolveStatusColor(snapshot.Status);
                    var layer = $"{snapshot.ServiceId}/{snapshot.LayerId}";
                    var message = snapshot.Message.IsNullOrWhiteSpace() ? string.Empty : $" - {snapshot.Message}";
                    _console.MarkupLine($"[{color}]{snapshot.Status}[/] {percentage}% ({snapshot.TilesProcessed}/{snapshot.TilesTotal} tiles) [grey]{layer}[/]{message}");
                    lastStatus = snapshot.Status;
                    lastPercentage = percentage;
                    lastProcessed = snapshot.TilesProcessed;
                }

                if (snapshot.Status is VectorTilePreseedJobStatus.Completed)
                {
                    _console.MarkupLine("[green]Vector cache preseed completed successfully.[/]");
                    return 0;
                }

                if (snapshot.Status is VectorTilePreseedJobStatus.Failed)
                {
                    _console.MarkupLine($"[red]Preseed job failed: {snapshot.Message ?? "Unknown error"}[/]");
                    return 1;
                }

                if (snapshot.Status is VectorTilePreseedJobStatus.Cancelled)
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

    private static string ResolveStatusColor(VectorTilePreseedJobStatus status)
        => status switch
        {
            VectorTilePreseedJobStatus.Completed => "green",
            VectorTilePreseedJobStatus.Failed => "red",
            VectorTilePreseedJobStatus.Cancelled => "yellow",
            _ => "cyan"
        };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--service-id <ID>")]
        [Description("Vector service identifier.")]
        public string ServiceId { get; init; } = string.Empty;

        [CommandOption("--layer-id <ID>")]
        [Description("Vector layer identifier.")]
        public string LayerId { get; init; } = string.Empty;

        [CommandOption("--min-zoom <LEVEL>")]
        [Description("Minimum zoom level (default: 0).")]
        [DefaultValue(0)]
        public int MinZoom { get; init; }

        [CommandOption("--max-zoom <LEVEL>")]
        [Description("Maximum zoom level (default: 14).")]
        [DefaultValue(14)]
        public int MaxZoom { get; init; } = 14;

        [CommandOption("--datetime <ISO8601>")]
        [Description("Optional datetime filter for temporal layers (ISO 8601 format).")]
        public string? Datetime { get; init; }

        [CommandOption("--overwrite")]
        [Description("Force regeneration even when cached tiles already exist.")]
        public bool Overwrite { get; init; }

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
