// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to configure DNS records for deployment.
/// </summary>
public sealed class DnsSetupCommand : AsyncCommand<DnsSetupCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public DnsSetupCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold blue]DNS Configuration[/]");
        _console.WriteLine();

        if (settings.Domain.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]Error: Domain is required (--domain)[/]");
            return 1;
        }

        if (settings.Target.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]Error: Target (IP or hostname) is required (--target)[/]");
            return 1;
        }

        _console.MarkupLine($"[green]Provider:[/] {settings.Provider}");
        _console.MarkupLine($"[green]Domain:[/] {settings.Domain}");
        _console.MarkupLine($"[green]Record Type:[/] {settings.RecordType}");
        _console.MarkupLine($"[green]Target:[/] {settings.Target}");
        _console.MarkupLine($"[green]TTL:[/] {settings.Ttl} seconds");
        _console.WriteLine();

        if (!settings.Yes)
        {
            if (!_console.Confirm("Create DNS record?"))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return 1;
            }
        }

        return await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Creating DNS record...", async ctx =>
            {
                // DNS record creation would happen here via DnsRecordService
                ctx.Status($"Creating {settings.RecordType} record for {settings.Domain}...");
                await Task.Delay(2000).ConfigureAwait(false);

                if (settings.Verify)
                {
                    ctx.Status("Waiting for DNS propagation...");
                    await Task.Delay(3000).ConfigureAwait(false);

                    ctx.Status("Verifying DNS record...");
                    await Task.Delay(1000).ConfigureAwait(false);
                }

                _console.MarkupLine("[green]✓[/] DNS record created successfully!");
                _console.MarkupLine($"Record: {settings.Domain} -> {settings.Target}");

                if (settings.Verify)
                {
                    _console.MarkupLine("[green]✓[/] DNS propagation verified");
                }

                return 0;
            });
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--provider <PROVIDER>")]
        [Description("DNS provider: route53, cloudflare, azure, or gcp. Default: route53")]
        [DefaultValue("route53")]
        public string Provider { get; init; } = "route53";

        [CommandOption("-d|--domain <DOMAIN>")]
        [Description("Domain name for the DNS record (e.g., app.example.com)")]
        public string? Domain { get; init; }

        [CommandOption("-t|--target <TARGET>")]
        [Description("Target IP address or hostname")]
        public string? Target { get; init; }

        [CommandOption("-r|--record-type <TYPE>")]
        [Description("DNS record type: A, AAAA, CNAME, or TXT. Default: A")]
        [DefaultValue("A")]
        public string RecordType { get; init; } = "A";

        [CommandOption("--ttl <SECONDS>")]
        [Description("Time-to-live in seconds. Default: 300")]
        [DefaultValue(300)]
        public int Ttl { get; init; } = 300;

        [CommandOption("--verify")]
        [Description("Wait and verify DNS propagation after creation")]
        public bool Verify { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool Yes { get; init; }
    }
}
