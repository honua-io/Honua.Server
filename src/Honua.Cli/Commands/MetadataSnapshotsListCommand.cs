// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Metadata;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class MetadataSnapshotsListCommand : AsyncCommand<MetadataSnapshotsListCommand.Settings>
{
    private readonly IMetadataSnapshotService _snapshotService;
    private readonly IAnsiConsole _console;

    public MetadataSnapshotsListCommand(IMetadataSnapshotService snapshotService, IAnsiConsole console)
    {
        _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cancellationToken = CancellationToken.None;
        var snapshots = await _snapshotService.ListSnapshotsAsync(settings.SnapshotsPath, cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
        {
            _console.WriteLine("No snapshots found. Run 'honua metadata snapshot' to capture one.");
            return 0;
        }

        var ordered = snapshots
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .Take(settings.Limit ?? int.MaxValue)
            .ToList();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Label");
        table.AddColumn("Created (UTC)");
        table.AddColumn("Size");
        table.AddColumn("Notes");

        foreach (var snapshot in ordered)
        {
            table.AddRow(
                snapshot.Label,
                snapshot.CreatedAtUtc.ToString("u", CultureInfo.InvariantCulture),
                snapshot.SizeBytes.HasValue ? FormatSize(snapshot.SizeBytes.Value) : "—",
                snapshot.Notes.IsNullOrWhiteSpace() ? "—" : snapshot.Notes);
        }

        _console.Write(table);
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--snapshots-path <PATH>")]
        [Description("Optional override for the snapshots root directory.")]
        public string? SnapshotsPath { get; init; }

        [CommandOption("--limit <COUNT>")]
        [Description("Maximum number of snapshots to display.")]
        public int? Limit { get; init; }
    }
}
