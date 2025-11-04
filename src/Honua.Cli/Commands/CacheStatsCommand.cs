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

public sealed class CacheStatsCommand : AsyncCommand<CacheStatsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _rasterApiClient;
    private readonly IVectorTileCacheApiClient _vectorApiClient;
    private readonly ILogger<CacheStatsCommand> _logger;

    public CacheStatsCommand(
        IAnsiConsole console,
        IRasterTileCacheApiClient rasterApiClient,
        IVectorTileCacheApiClient vectorApiClient,
        ILogger<CacheStatsCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _rasterApiClient = rasterApiClient ?? throw new ArgumentNullException(nameof(rasterApiClient));
        _vectorApiClient = vectorApiClient ?? throw new ArgumentNullException(nameof(vectorApiClient));
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

            var showRaster = settings.Type.IsNullOrWhiteSpace() || settings.Type.Equals("raster", StringComparison.OrdinalIgnoreCase);
            var showVector = settings.Type.IsNullOrWhiteSpace() || settings.Type.Equals("vector", StringComparison.OrdinalIgnoreCase);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Cache Type");
            table.AddColumn("Tile Count");
            table.AddColumn("Total Size");
            table.AddColumn("Datasets/Services");
            table.AddColumn("Layers");

            if (showRaster)
            {
                var rasterStats = await _rasterApiClient.GetStatsAsync(connection, CancellationToken.None).ConfigureAwait(false);
                table.AddRow(
                    "[cyan]Raster[/]",
                    rasterStats.TileCount.ToString("N0"),
                    FormatBytes(rasterStats.TotalSizeBytes),
                    rasterStats.DatasetOrServiceCount.ToString(),
                    "-");
            }

            if (showVector)
            {
                var vectorStats = await _vectorApiClient.GetStatsAsync(connection, CancellationToken.None).ConfigureAwait(false);
                table.AddRow(
                    "[green]Vector[/]",
                    vectorStats.TileCount.ToString("N0"),
                    FormatBytes(vectorStats.TotalSizeBytes),
                    vectorStats.DatasetOrServiceCount.ToString(),
                    vectorStats.LayerCount.ToString());
            }

            _console.Write(table);
            return 0;
        }, _logger, "cache-stats");
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--type <TYPE>")]
        [Description("Filter by cache type: raster or vector. If not specified, shows both.")]
        public string? Type { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
