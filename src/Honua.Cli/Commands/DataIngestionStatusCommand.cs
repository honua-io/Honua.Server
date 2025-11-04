// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class DataIngestionStatusCommand : AsyncCommand<DataIngestionStatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IDataIngestionApiClient _apiClient;
    private readonly IControlPlaneConnectionResolver _connectionResolver;

    public DataIngestionStatusCommand(IAnsiConsole console, IDataIngestionApiClient apiClient, IControlPlaneConnectionResolver connectionResolver)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
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

        var snapshot = await _apiClient.GetJobAsync(connection, settings.JobId, CancellationToken.None).ConfigureAwait(false);
        if (snapshot is null)
        {
            _console.MarkupLine("[red]Ingestion job was not found on the control plane.[/]");
            return 1;
        }

        var panel = new Panel($"Status: {snapshot.Status}\nProgress: {Math.Round(snapshot.Progress * 100, 0)}%\nStage: {snapshot.Stage}\nMessage: {snapshot.Message ?? "(none)"}")
        {
            Header = new PanelHeader(snapshot.JobId.ToString(), Justify.Center)
        };

        _console.Write(panel);
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Ingestion job identifier.")]
        public Guid JobId { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
