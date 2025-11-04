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

public sealed class CachePurgeAllCommand : AsyncCommand<CachePurgeAllCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _rasterApiClient;
    private readonly IVectorTileCacheApiClient _vectorApiClient;
    private readonly ILogger<CachePurgeAllCommand> _logger;

    public CachePurgeAllCommand(
        IAnsiConsole console,
        IRasterTileCacheApiClient rasterApiClient,
        IVectorTileCacheApiClient vectorApiClient,
        ILogger<CachePurgeAllCommand> logger)
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
            var purgeRaster = settings.Type.IsNullOrWhiteSpace() || settings.Type.Equals("raster", StringComparison.OrdinalIgnoreCase);
            var purgeVector = settings.Type.IsNullOrWhiteSpace() || settings.Type.Equals("vector", StringComparison.OrdinalIgnoreCase);

            if (settings.DryRun)
            {
                _console.MarkupLine("[yellow]DRY RUN:[/] Would purge:");
                if (purgeRaster) _console.MarkupLine("  - [cyan]All raster cache[/]");
                if (purgeVector) _console.MarkupLine("  - [green]All vector cache[/]");
                return 0;
            }

            if (!settings.Confirm)
            {
                _console.MarkupLine("[red]This operation will purge ALL cached tiles.[/]");
                _console.MarkupLine("[yellow]Use --confirm to proceed with this destructive operation.[/]");
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

            var hasError = false;

            if (purgeRaster)
            {
                var result = await _rasterApiClient.PurgeAllAsync(connection, CancellationToken.None).ConfigureAwait(false);
                if (result.Success)
                {
                    _console.MarkupLine($"[green]Purged raster cache:[/] {result.TilesPurged:N0} tiles");
                }
                else
                {
                    _console.MarkupLine($"[red]Failed to purge raster cache:[/] {result.Message ?? "Unknown error"}");
                    hasError = true;
                }
            }

            if (purgeVector)
            {
                var result = await _vectorApiClient.PurgeAllAsync(connection, CancellationToken.None).ConfigureAwait(false);
                if (result.Success)
                {
                    _console.MarkupLine($"[green]Purged vector cache:[/] {result.TilesPurged:N0} tiles");
                }
                else
                {
                    _console.MarkupLine($"[red]Failed to purge vector cache:[/] {result.Message ?? "Unknown error"}");
                    hasError = true;
                }
            }

            return hasError ? 1 : 0;
        }, _logger, "cache-purge-all");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--type <TYPE>")]
        [Description("Filter by cache type: raster or vector. If not specified, purges both.")]
        public string? Type { get; init; }

        [CommandOption("--confirm")]
        [Description("Confirm destructive purge operation.")]
        public bool Confirm { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would be purged without actually purging.")]
        public bool DryRun { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
