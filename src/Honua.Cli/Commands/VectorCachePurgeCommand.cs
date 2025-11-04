// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class VectorCachePurgeCommand : AsyncCommand<VectorCachePurgeCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IVectorTileCacheApiClient _apiClient;

    public VectorCachePurgeCommand(IAnsiConsole console, IVectorTileCacheApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.ServiceId.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]--service-id is required.[/]");
            return 1;
        }

        var target = settings.LayerId != null
            ? $"{settings.ServiceId}/{settings.LayerId}"
            : settings.ServiceId;

        if (settings.DryRun)
        {
            _console.MarkupLine($"[yellow]DRY RUN:[/] Would purge vector cache for [cyan]{target}[/]");
            return 0;
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

        try
        {
            var request = new VectorTileCachePurgeRequest(settings.ServiceId, settings.LayerId);
            var result = await _apiClient.PurgeAsync(connection, request, CancellationToken.None).ConfigureAwait(false);

            var purgedCount = result.PurgedTargets.Count;
            var failedCount = result.FailedTargets.Count;

            if (purgedCount > 0)
            {
                _console.MarkupLine($"[green]Purged {purgedCount} target(s):[/] {string.Join(", ", result.PurgedTargets)}");
            }

            if (failedCount > 0)
            {
                _console.MarkupLine($"[yellow]Failed to purge {failedCount} target(s):[/] {string.Join(", ", result.FailedTargets)}");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to purge cache: {ex.Message}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--service-id <ID>")]
        [Description("Vector service identifier to purge from cache.")]
        public string ServiceId { get; init; } = string.Empty;

        [CommandOption("--layer-id <ID>")]
        [Description("Optional layer identifier to purge only a specific layer.")]
        public string? LayerId { get; init; }

        [CommandOption("--dry-run")]
        [Description("Show what would be purged without actually purging.")]
        public bool DryRun { get; init; }

        [CommandOption("--host <URI>")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }
}
