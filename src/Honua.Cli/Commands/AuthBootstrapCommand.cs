// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

internal sealed class AuthBootstrapCommand : AsyncCommand<AuthBootstrapCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IAuthBootstrapService _bootstrapService;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;

    public AuthBootstrapCommand(
        IAnsiConsole console,
        IAuthBootstrapService bootstrapService,
        IOptionsMonitor<HonuaAuthenticationOptions> options)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _bootstrapService = bootstrapService ?? throw new ArgumentNullException(nameof(bootstrapService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var configuredMode = _options.CurrentValue.Mode.ToString();
        if (settings.Mode.HasValue() && !string.Equals(settings.Mode, configuredMode, StringComparison.OrdinalIgnoreCase))
        {
            var warning = $"Configured authentication mode is '{configuredMode}', which differs from '--mode {settings.Mode}'. Proceeding with configured mode.";
            _console.WriteLine(warning);
        }

        var result = await _bootstrapService.BootstrapAsync().ConfigureAwait(false);

        switch (result.Status)
        {
            case AuthBootstrapStatus.Completed:
                _console.WriteLine(result.Message);
                await HandleSecretOutputAsync(result.GeneratedSecret, settings.OutputPath, settings.PrintSecret).ConfigureAwait(false);
                return 0;
            case AuthBootstrapStatus.Pending:
            case AuthBootstrapStatus.Unknown:
                _console.WriteLine(result.Message);
                return 0;
            default:
                _console.WriteLine(result.Message);
                return 1;
        }
    }

    private async Task HandleSecretOutputAsync(string? secret, string? outputPath, bool printSecret)
    {
        if (secret.IsNullOrEmpty())
        {
            return;
        }

        if (printSecret)
        {
            _console.WriteLine($"Generated administrator password: {secret}");
            _console.WriteLine("Store this password securely and rotate immediately after first login.");
        }
        else if (outputPath.IsNullOrWhiteSpace())
        {
            _console.WriteLine("A bootstrap password was generated. Re-run with --print-secret to display it or supply --output to persist it.");
        }

        if (outputPath.IsNullOrWhiteSpace())
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(outputPath, secret).ConfigureAwait(false);
            FilePermissionHelper.ApplyFilePermissions(outputPath);
            var outputMessage = $"Password written to {outputPath} (ensure file permissions are restricted).";
            _console.WriteLine(outputMessage);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to write secret to {outputPath}: {ex.Message}";
            _console.WriteLine(errorMessage);
        }
    }

    internal sealed class Settings : CommandSettings
    {
        [Description("Authentication mode to bootstrap (Oidc or Local). Optional guard to catch misconfiguration.")]
        [CommandOption("--mode <MODE>")]
        public string? Mode { get; set; }

        [Description("Optional output path for generated bootstrap secrets.")]
        [CommandOption("--output <PATH>")]
        public string? OutputPath { get; set; }

        [Description("Print generated secrets to stdout (use with caution).")]
        [CommandOption("--print-secret")]
        public bool PrintSecret { get; set; }
    }
}
