// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.Services.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class ConfigInitCommand : AsyncCommand<ConfigInitCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IHonuaCliConfigStore _configStore;

    public ConfigInitCommand(IAnsiConsole console, IHonuaCliEnvironment environment, IHonuaCliConfigStore configStore)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _environment.EnsureInitialized();
        var cancellationToken = CancellationToken.None;

        var existing = await _configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Overwrite && (existing.Host.HasValue() || existing.Token.HasValue()))
        {
            _console.MarkupLine("[yellow]Configuration already exists. Use --overwrite to replace it.[/]");
            return 1;
        }

        var host = settings.Host;
        if (host.IsNullOrWhiteSpace())
        {
            host = existing.Host;
        }

        host = ResolveHostPrompt(host);
        if (host.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]A server host is required.[/]");
            return 1;
        }

        string? token = settings.Token;
        if (token.IsNullOrEmpty())
        {
            token = existing.Token;
        }

        if (settings.ResetToken)
        {
            token = null;
        }

        var config = new HonuaCliConfig(host, token.IsNullOrWhiteSpace() ? null : token);
        await _configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);

        _console.MarkupLine("[green]Configuration saved.[/]");
        _console.MarkupLine($"Host: [bold]{host}[/]");
        if (config.Token.HasValue())
        {
            _console.MarkupLine("Token: [grey]stored (hidden)[/]");
        }
        else
        {
            _console.MarkupLine("Token: [grey]none configured[/]");
        }

        return 0;
    }

    private string? ResolveHostPrompt(string? current)
    {
        if (current.HasValue())
        {
            return current;
        }

        var prompt = new TextPrompt<string>("Enter Honua server base URL (e.g. http://localhost:5000)")
            .DefaultValue("http://localhost:5000");

        var response = _console.Prompt(prompt);
        return response.IsNullOrWhiteSpace() ? null : response.Trim();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <URI>")]
        [Description("Honua server base URL (e.g. http://localhost:5000). Prompted if omitted.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Bearer token to store for authenticated control plane calls.")]
        public string? Token { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing configuration if present.")]
        public bool Overwrite { get; init; }

        [CommandOption("--reset-token")]
        [Description("Remove any stored bearer token without changing the host.")]
        public bool ResetToken { get; init; }
    }
}
