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
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class MetadataRestoreCommand : AsyncCommand<MetadataRestoreCommand.Settings>
{
    private readonly IMetadataSnapshotService _snapshotService;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAnsiConsole _console;

    public MetadataRestoreCommand(
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

        if (settings.Label.IsNullOrWhiteSpace())
        {
            _console.WriteLine("Snapshot label is required.");
            return 1;
        }

        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        var request = new MetadataRestoreRequest(workspace, settings.Label, settings.SnapshotsPath, settings.Overwrite);
        var cancellationToken = CancellationToken.None;

        await _snapshotService.RestoreSnapshotAsync(request, cancellationToken).ConfigureAwait(false);
        _console.WriteLine($"Snapshot '{settings.Label}' restored into {workspace}.");

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<LABEL>")]
        [Description("Label of the snapshot to restore.")]
        public string Label { get; init; } = string.Empty;

        [CommandOption("--workspace <PATH>")]
        [Description("Metadata workspace path; defaults to the current directory.")]
        public string? Workspace { get; init; }

        [CommandOption("--snapshots-path <PATH>")]
        [Description("Override the snapshots root directory.")]
        public string? SnapshotsPath { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing files when restoring the snapshot.")]
        public bool Overwrite { get; init; }
    }
}
