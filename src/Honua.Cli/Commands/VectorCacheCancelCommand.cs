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

public sealed class VectorCacheCancelCommand : AsyncCommand<VectorCacheCancelCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IVectorTileCacheApiClient _apiClient;

    public VectorCacheCancelCommand(IAnsiConsole console, IVectorTileCacheApiClient apiClient)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
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

        try
        {
            var job = await _apiClient.CancelAsync(connection, jobId, CancellationToken.None).ConfigureAwait(false);
            if (job is null)
            {
                _console.MarkupLine("[yellow]Job not found.[/]");
                return 1;
            }

            var color = ResolveStatusColor(job.Status);
            _console.MarkupLine($"Job [{color}]{job.Status}[/]: {job.ServiceId}/{job.LayerId}");
            if (job.Message.HasValue())
            {
                _console.MarkupLine($"[grey]{job.Message}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to cancel job: {ex.Message}[/]");
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
        [CommandArgument(0, "<JOB_ID>")]
        [Description("Identifier of the vector preseed job to cancel.")]
        public string JobId { get; init; } = string.Empty;

        [CommandOption("--host <URI>")]
        [Description("Honua control plane base URI. Defaults to http://localhost:5000.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token for authenticating against the control plane.")]
        public string? Token { get; init; }
    }
}
