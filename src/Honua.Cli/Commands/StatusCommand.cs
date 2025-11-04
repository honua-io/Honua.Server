// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Configuration;
using Honua.Cli.Services.ControlPlane;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

public sealed class StatusCommand : AsyncCommand<StatusCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IControlPlaneConnectionResolver _connectionResolver;
    private readonly IHonuaCliConfigStore _configStore;

    public StatusCommand(
        IAnsiConsole console,
        IHttpClientFactory httpClientFactory,
        IControlPlaneConnectionResolver connectionResolver,
        IHonuaCliConfigStore configStore)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _connectionResolver = connectionResolver ?? throw new ArgumentNullException(nameof(connectionResolver));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cancellationToken = CancellationToken.None;
        HonuaCliConfig existing;
        try
        {
            existing = await _configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Failed to load CLI configuration: {ex.Message}[/]");
            return 1;
        }

        ControlPlaneConnection connection;
        try
        {
            connection = await _connectionResolver.ResolveAsync(settings.Host, settings.Token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]{ex.Message}[/]");
            return 1;
        }

        var client = _httpClientFactory.CreateClient("honua-control-plane");
        client.BaseAddress = connection.BaseUri;

        var healthStatus = await ProbeHealthAsync(client, cancellationToken).ConfigureAwait(false);
        var authStatus = await ProbeMetadataAsync(client, connection.BearerToken, cancellationToken).ConfigureAwait(false);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Check");
        table.AddColumn("Result");
        table.AddRow("Configuration host", existing.Host ?? "(none)");
        table.AddRow("Active host", connection.BaseUri.ToString());
        table.AddRow("Stored token", existing.Token.IsNullOrWhiteSpace() ? "no" : "yes");
        table.AddEmptyRow();
        table.AddRow("/healthz/ready", healthStatus);
        table.AddRow("/admin/metadata/snapshots", authStatus);

        _console.Write(table);
        return string.Equals(healthStatus, "ok", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private async Task<string> ProbeHealthAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(new Uri("/healthz/ready", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? "ok" : $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    private async Task<string> ProbeMetadataAsync(HttpClient client, string? bearerToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/admin/metadata/snapshots", UriKind.Relative));
            if (bearerToken.HasValue())
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return $"unauthorized ({(int)response.StatusCode})";
            }

            if (!response.IsSuccessStatusCode)
            {
                return $"{(int)response.StatusCode} {response.ReasonPhrase}";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var count = document.RootElement.TryGetProperty("snapshots", out var snapshotsElement) && snapshotsElement.ValueKind == JsonValueKind.Array
                ? snapshotsElement.GetArrayLength()
                : 0;
            return $"ok ({count} snapshots)";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--host <URI>")]
        [Description("Override the configured host for this status check.")]
        public string? Host { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Override the configured bearer token for authenticated probes.")]
        public string? Token { get; init; }
    }
}
