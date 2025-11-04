// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class VectorCacheJobsCommand : AsyncCommand<VectorCacheJobsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IVectorTileCacheApiClient _apiClient;

    public VectorCacheJobsCommand(IAnsiConsole console, IVectorTileCacheApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
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

        try
        {
            var jobs = await _apiClient.ListAsync(connection, CancellationToken.None).ConfigureAwait(false);
            if (jobs.Count == 0)
            {
                _console.MarkupLine("[yellow]No vector preseed jobs found.[/]");
                return 0;
            }

            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumn("Job ID");
            table.AddColumn("Status");
            table.AddColumn("Progress");
            table.AddColumn("Layer");
            table.AddColumn("Tiles");
            table.AddColumn("Created (UTC)");

            foreach (var job in jobs.OrderByDescending(j => j.CreatedAtUtc))
            {
                var color = ResolveStatusColor(job.Status);
                var percentage = (int)Math.Round(job.Progress * 100, MidpointRounding.AwayFromZero);
                var layer = $"{job.ServiceId}/{job.LayerId}";
                var tiles = $"{job.TilesProcessed}/{job.TilesTotal}";
                var created = job.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm");

                table.AddRow(
                    job.JobId.ToString(),
                    $"[{color}]{job.Status}[/]",
                    $"{percentage}%",
                    layer,
                    tiles,
                    created);
            }

            _console.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to retrieve jobs: {ex.Message}[/]");
            return 1;
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
        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
