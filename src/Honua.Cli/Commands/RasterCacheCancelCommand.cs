// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class RasterCacheCancelCommand : AsyncCommand<RasterCacheCancelCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IRasterTileCacheApiClient _apiClient;
    private readonly ILogger<RasterCacheCancelCommand> _logger;

    public RasterCacheCancelCommand(IAnsiConsole console, IRasterTileCacheApiClient apiClient, ILogger<RasterCacheCancelCommand> logger)
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

            var job = await _apiClient.CancelAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            if (job is null)
            {
                _console.MarkupLine("[yellow]Job not found or already completed.[/]");
                return 1;
            }

            var color = ResolveStatusColor(job.Status);
            _console.MarkupLine($"[{color}]Job {job.JobId} marked {job.Status}.[/]");
            return 0;
        }, _logger, "raster-cache-cancel");
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
        [Description("Identifier of the raster preseed job to cancel.")]
        public string JobId { get; init; } = string.Empty;

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
