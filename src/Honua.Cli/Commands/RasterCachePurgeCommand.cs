// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class RasterCachePurgeCommand : AsyncCommand<RasterCachePurgeCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _apiClient;
    private readonly ILogger<RasterCachePurgeCommand> _logger;

    public RasterCachePurgeCommand(IAnsiConsole console, IRasterTileCacheApiClient apiClient, ILogger<RasterCachePurgeCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(async () =>
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

            var result = await _apiClient.PurgeAsync(connection, datasetIds, CancellationToken.None).ConfigureAwait(false);
            var purgedCount = result.PurgedDatasets.Count;
            var failedCount = result.FailedDatasets.Count;

            if (purgedCount > 0)
            {
                _console.MarkupLine($"[green]Purged {purgedCount} dataset(s):[/] {string.Join(",", result.PurgedDatasets)}");
            }

            if (failedCount > 0)
            {
                _console.MarkupLine($"[yellow]Failed to purge {failedCount} dataset(s):[/] {string.Join(",", result.FailedDatasets)}");
                return 1;
            }

            return 0;
        }, _logger, "raster-cache-purge");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dataset-id <ID>")]
        [Description("Raster dataset identifier to purge from cache.")]
        public string[]? DatasetIds { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
