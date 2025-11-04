// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Server.Core.Migration.GeoservicesRest;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class MigrateEsriServiceCommand : AsyncCommand<MigrateEsriServiceCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IMigrationApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;

    public MigrateEsriServiceCommand(IAnsiConsole console, IMigrationApiClient apiClient, IControlPlaneConnectionResolver connectionResolver)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.SourceServiceUri.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--source is required.[/]");
            return 1;
        }

        if (settings.TargetServiceId.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--target-service is required.[/]");
            return 1;
        }

        if (settings.TargetFolderId.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--target-folder is required.[/]");
            return 1;
        }

        if (settings.TargetDataSourceId.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--target-datasource is required.[/]");
            return 1;
        }

        if (!Uri.TryCreate(settings.SourceServiceUri, UriKind.Absolute, out var sourceUri))
        {
            _console.MarkupLine("[red]--source must be a valid absolute URI.[/]");
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
            _console.MarkupLine("[yellow]Cancellation requested. Attempting to stop migration...[/]");
            cts.Cancel();
        }

        Console.CancelKeyPress += Handler;

        try
        {
            var layerIds = ParseLayerIds(settings.Layers);

            var job = await _apiClient.CreateJobAsync(
                connection,
                sourceUri,
                settings.TargetServiceId!,
                settings.TargetFolderId!,
                settings.TargetDataSourceId!,
                layerIds,
                settings.IncludeData,
                settings.BatchSize,
                cts.Token).ConfigureAwait(false);

            _console.MarkupLine($"Queued migration job [green]{job.JobId}[/].");
            _console.MarkupLine($"Migrating from [cyan]{sourceUri}[/] to service [cyan]{settings.TargetServiceId}[/]");

            var pollInterval = TimeSpan.FromSeconds(Math.Max(1, settings.PollIntervalSeconds));
            return await MonitorJobAsync(connection, job.JobId, pollInterval, cts);
        }
        catch (OperationCanceledException)
        {
            _console.MarkupLine("[yellow]Migration cancelled by user.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to start migration: {ex.Message}[/]");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= Handler;
        }
    }

    private async Task<int> MonitorJobAsync(ControlPlaneConnection connection, Guid jobId, TimeSpan pollInterval, CancellationTokenSource cts)
    {
        GeoservicesRestMigrationJobStatus? lastStatus = null;
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
                    _console.MarkupLine("[red]Migration job was not found on the control plane.[/]");
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

                if (snapshot.Status is GeoservicesRestMigrationJobStatus.Completed)
                {
                    _console.MarkupLine("[green]Migration completed successfully.[/]");
                    return 0;
                }

                if (snapshot.Status is GeoservicesRestMigrationJobStatus.Failed)
                {
                    _console.MarkupLine($"[red]Migration failed: {snapshot.Message ?? "Unknown error"}[/]");
                    return 1;
                }

                if (snapshot.Status is GeoservicesRestMigrationJobStatus.Cancelled)
                {
                    _console.MarkupLine($"[yellow]Migration was cancelled: {snapshot.Message}[/]");
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

    private static string ResolveStatusColor(GeoservicesRestMigrationJobStatus status)
    {
        return status switch
        {
            GeoservicesRestMigrationJobStatus.Completed => "green",
            GeoservicesRestMigrationJobStatus.Failed => "red",
            GeoservicesRestMigrationJobStatus.Cancelled => "yellow",
            _ => "cyan"
        };
    }

    private static int[]? ParseLayerIds(string? layerIdsString)
    {
        if (layerIdsString.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return layerIdsString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--source <URL>")]
        [Description("Source Esri/ArcGIS REST service URL (e.g., https://gis.example.com/arcgis/rest/services/Planning/Zoning/FeatureServer)")]
        public string? SourceServiceUri { get; init; }

        [CommandOption("--target-service <ID>")]
        [Description("Target service identifier in Honua metadata")]
        public string? TargetServiceId { get; init; }

        [CommandOption("--target-folder <ID>")]
        [Description("Target folder identifier in Honua metadata")]
        public string? TargetFolderId { get; init; }

        [CommandOption("--target-datasource <ID>")]
        [Description("Target data source identifier in Honua metadata")]
        public string? TargetDataSourceId { get; init; }

        [CommandOption("--layers <IDS>")]
        [Description("Comma-separated layer IDs to migrate (e.g., 0,1,2). If omitted, all layers are migrated.")]
        public string? Layers { get; init; }

        [CommandOption("--include-data")]
        [Description("Include data migration (features) in addition to metadata. Default is true.")]
        [DefaultValue(true)]
        public bool IncludeData { get; init; } = true;

        [CommandOption("--batch-size <SIZE>")]
        [Description("Number of features to fetch per batch. Default is 10000.")]
        public int? BatchSize { get; init; }

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }

        [CommandOption("--poll-interval <SECONDS>")]
        [Description("Polling interval in seconds when tracking migration progress (default: 2).")]
        [DefaultValue(2)]
        public int PollIntervalSeconds { get; init; } = 2;
    }
}
