// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.Metadata;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

public sealed class MetadataSnapshotCommand : AsyncCommand<MetadataSnapshotCommand.Settings>
{
    private readonly IMetadataSnapshotService _snapshotService;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAnsiConsole _console;

    public MetadataSnapshotCommand(
        IMetadataSnapshotService snapshotService,
        IHonuaCliEnvironment environment,
        IAnsiConsole console)
    {
        _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        var request = new MetadataSnapshotRequest(workspace, settings.Label, settings.Notes, settings.SnapshotsPath);

        var cancellationToken = CancellationToken.None;
        var result = await _snapshotService.CreateSnapshotAsync(request, cancellationToken).ConfigureAwait(false);

        _console.WriteLine($"Snapshot '{result.Label}' captured at {result.CreatedAtUtc:O}.");
        _console.WriteLine($"Stored at: {result.SnapshotPath}");

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--label <LABEL>")]
        [Description("Optional snapshot label; defaults to a timestamp-generated name.")]
        public string? Label { get; init; }

        [CommandOption("--notes <TEXT>")]
        [Description("Optional notes to add to the snapshot manifest.")]
        public string? Notes { get; init; }

        [CommandOption("--workspace <PATH>")]
        [Description("Metadata workspace path; defaults to the current directory.")]
        public string? Workspace { get; init; }

        [CommandOption("--snapshots-path <PATH>")]
        [Description("Override the snapshots root directory.")]
        public string? SnapshotsPath { get; init; }
    }
}
