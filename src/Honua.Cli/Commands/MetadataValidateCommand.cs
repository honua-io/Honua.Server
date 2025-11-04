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

public sealed class MetadataValidateCommand : AsyncCommand<MetadataValidateCommand.Settings>
{
    private readonly IMetadataSnapshotService _snapshotService;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAnsiConsole _console;

    public MetadataValidateCommand(
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
        var workspace = _environment.ResolveWorkspacePath(settings.Workspace);
        var cancellationToken = CancellationToken.None;
        var result = await _snapshotService.ValidateAsync(new MetadataValidationRequest(workspace), cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            _console.WriteLine("Metadata workspace validated successfully.");
        }
        else
        {
            _console.WriteLine("Metadata validation failed.");
            foreach (var error in result.Errors)
            {
                _console.WriteLine($"  • {error}");
            }
        }

        foreach (var warning in result.Warnings)
        {
            _console.WriteLine($"  • {warning}");
        }

        return result.IsSuccess ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--workspace <PATH>")]
        [Description("Metadata workspace to validate; defaults to the current directory.")]
        public string? Workspace { get; init; }
    }
}
