// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Honua.Server.Core.Import;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class DataIngestionCommand : AsyncCommand<DataIngestionCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IDataIngestionApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<DataIngestionCommand> _logger;

    public DataIngestionCommand(IAnsiConsole console, IDataIngestionApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<DataIngestionCommand> logger)
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

            if (settings.DatasetPath.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Dataset path is required.[/]");
                return 1;
            }

            if (!File.Exists(settings.DatasetPath!))
            {
                _console.MarkupLine($"[red]Dataset '{settings.DatasetPath}' could not be found.[/]");
                return 1;
            }

            if (settings.Overwrite)
            {
                _console.MarkupLine("[red]Overwrite imports are not supported yet. Remove --overwrite and retry after clearing the destination manually.[/]");
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

            using var cts = new CancellationTokenSource();
            void Handler(object? sender, ConsoleCancelEventArgs eventArgs)
            {
                eventArgs.Cancel = true;
                _console.MarkupLine("[yellow]Cancellation requested. Attempting to stop ingestion...[/]");
                cts.Cancel();
            }

            Console.CancelKeyPress += Handler;

            try
            {
                var job = await _apiClient.CreateJobAsync(
                    connection,
                    settings.ServiceId!,
                    settings.LayerId!,
                    settings.DatasetPath!,
                    settings.Overwrite,
                    cts.Token).ConfigureAwait(false);

                _console.MarkupLine($"Queued ingestion job [green]{job.JobId}[/].");

                var pollInterval = TimeSpan.FromSeconds(Math.Max(1, settings.PollIntervalSeconds));
                return await MonitorJobAsync(connection, job.JobId, pollInterval, cts);
            }
            finally
            {
                Console.CancelKeyPress -= Handler;
            }
        }, _logger, "data-ingestion");
    }

    private async Task<int> MonitorJobAsync(ControlPlaneConnection connection, Guid jobId, TimeSpan pollInterval, CancellationTokenSource cts)
    {
        DataIngestionJobStatus? lastStatus = null;
        string? lastStage = null;
        int lastPercentage = -1;

        try
        {
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                var snapshot = await _apiClient.GetJobAsync(connection, jobId, cts.Token).ConfigureAwait(false);
                if (snapshot is null)
                {
                    _console.MarkupLine("[red]Ingestion job was not found on the control plane.[/]");
                    return 1;
                }

                var percentage = (int)Math.Round(snapshot.Progress * 100, MidpointRounding.AwayFromZero);

                if (snapshot.Status != lastStatus || !string.Equals(snapshot.Stage, lastStage, StringComparison.OrdinalIgnoreCase) || percentage != lastPercentage)
                {
                    var color = ResolveStatusColor(snapshot.Status);
                    var message = snapshot.Message.IsNullOrWhiteSpace() ? string.Empty : $" - {snapshot.Message}";
                    _console.MarkupLine($"[{color}]{snapshot.Status}[/] {percentage}% {snapshot.Stage}{message}");
                    lastStatus = snapshot.Status;
                    lastStage = snapshot.Stage;
                    lastPercentage = percentage;
                }

                if (snapshot.Status is DataIngestionJobStatus.Completed)
                {
                    _console.MarkupLine("[green]Data ingestion completed successfully.[/]");
                    return 0;
                }

                if (snapshot.Status is DataIngestionJobStatus.Failed)
                {
                    _console.MarkupLine($"[red]Data ingestion failed: {snapshot.Message ?? "Unknown error"}[/]");
                    return 1;
                }

                if (snapshot.Status is DataIngestionJobStatus.Cancelled)
                {
                    _console.MarkupLine($"[yellow]Data ingestion was cancelled: {snapshot.Message}[/]");
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
            await _apiClient.CancelJobAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            _console.MarkupLine("[yellow]Cancellation signal sent to control plane.[/]");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[yellow]Failed to cancel job: {ex.Message}[/]");
        }
    }

    private static string ResolveStatusColor(DataIngestionJobStatus status)
    {
        return status switch
        {
            DataIngestionJobStatus.Completed => "green",
            DataIngestionJobStatus.Failed => "red",
            DataIngestionJobStatus.Cancelled => "yellow",
            _ => "cyan"
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<DATASET>")]
        [Description("Path to the dataset to ingest (GeoPackage, GeoJSON, zipped Shapefile).")]
        public string? DatasetPath { get; init; }

        [CommandOption("--service-id <ID>")]
        [Description("Service identifier in Honua metadata.")]
        public string? ServiceId { get; init; }

        [CommandOption("--layer-id <ID>")]
        [Description("Layer identifier to ingest into.")]
        public string? LayerId { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }

        [CommandOption("--overwrite")]
        [Description("Reserved for future use; currently rejects the command when supplied.")]
        public bool Overwrite { get; init; }

        [CommandOption("--poll-interval <SECONDS>")]
        [Description("Polling interval in seconds when tracking ingestion progress (default: 2).")]
        [DefaultValue(2)]
        public int PollIntervalSeconds { get; init; } = 2;
    }
}
