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

public sealed class DataIngestionCancelCommand : AsyncCommand<DataIngestionCancelCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IDataIngestionApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly ILogger<DataIngestionCancelCommand> _logger;

    public DataIngestionCancelCommand(IAnsiConsole console, IDataIngestionApiClient apiClient, IControlPlaneConnectionResolver connectionResolver, ILogger<DataIngestionCancelCommand> logger)
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

            var snapshot = await _apiClient.CancelJobAsync(connection, settings.JobId, CancellationToken.None).ConfigureAwait(false);
            if (snapshot is null)
            {
                _console.MarkupLine("[red]Ingestion job was not found on the control plane.[/]");
                return 1;
            }

            _console.MarkupLine($"[yellow]Cancellation requested for job {snapshot.JobId}. Current status: {snapshot.Status}[/]");
            return 0;
        }, _logger, "data-ingestion-cancel");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Ingestion job identifier to cancel.")]
        public Guid JobId { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
