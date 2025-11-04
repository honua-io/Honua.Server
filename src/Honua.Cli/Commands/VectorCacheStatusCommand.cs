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

namespace Honua.Cli.Commands;

public sealed class VectorCacheStatusCommand : AsyncCommand<VectorCacheStatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IVectorTileCacheApiClient _apiClient;
    private readonly ILogger<VectorCacheStatusCommand> _logger;

    public VectorCacheStatusCommand(IAnsiConsole console, IVectorTileCacheApiClient apiClient, ILogger<VectorCacheStatusCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(async () =>
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

            var job = await _apiClient.GetAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            if (job is null)
            {
                _console.MarkupLine("[yellow]Job not found.[/]");
                return 1;
            }

            var color = ResolveStatusColor(job.Status);
            var table = new Table().Border(TableBorder.Minimal);
            table.AddColumn("Field");
            table.AddColumn("Value");
            table.AddRow("Job ID", job.JobId.ToString());
            table.AddRow("Status", $"[{color}]{job.Status}[/]");
            table.AddRow("Progress", $"{Math.Round(job.Progress * 100, MidpointRounding.AwayFromZero)}%");
            table.AddRow("Service ID", job.ServiceId);
            table.AddRow("Layer ID", job.LayerId);
            table.AddRow("Tiles Processed", job.TilesProcessed.ToString());
            table.AddRow("Tiles Total", job.TilesTotal.ToString());
            table.AddRow("Message", job.Message ?? string.Empty);
            table.AddRow("Created (UTC)", job.CreatedAtUtc.ToString("u"));
            table.AddRow("Completed (UTC)", job.CompletedAtUtc?.ToString("u") ?? string.Empty);

            _console.Write(table);
            return 0;
        }, _logger, "vector-cache-status");
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
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Identifier of the vector preseed job.")]
        public string JobId { get; init; } = string.Empty;

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
