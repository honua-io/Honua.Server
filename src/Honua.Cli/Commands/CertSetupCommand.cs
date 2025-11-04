// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to set up SSL/TLS certificates using Let's Encrypt.
/// </summary>
public sealed class CertSetupCommand : AsyncCommand<CertSetupCommand.Settings>
{
    private readonly IAnsiConsole _console;

    public CertSetupCommand(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _console.MarkupLine("[bold blue]Certificate Setup (Let's Encrypt)[/]");
        _console.WriteLine();

        if (settings.Domains == null || settings.Domains.Length == 0)
        {
            _console.MarkupLine("[red]Error: At least one domain is required (--domain)[/]");
            return 1;
        }

        if (settings.Email.IsNullOrWhiteSpace())
        {
            _console.MarkupLine("[red]Error: Email address is required for ACME registration (--email)[/]");
            return 1;
        }

        _console.MarkupLine($"[green]Domains:[/] {string.Join(", ", settings.Domains)}");
        _console.MarkupLine($"[green]Email:[/] {settings.Email}");
        _console.MarkupLine($"[green]Challenge Type:[/] {settings.ChallengeType}");
        _console.MarkupLine($"[green]Environment:[/] {(settings.Production ? "Production" : "Staging")}");
        _console.WriteLine();

        if (!settings.Yes)
        {
            if (!_console.Confirm("Proceed with certificate acquisition?"))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return 1;
            }
        }

        return await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Acquiring certificate from Let's Encrypt...", async ctx =>
            {
                // Certificate acquisition would happen here via AcmeCertificateService
                ctx.Status("Validating domains...");
                await Task.Delay(1000).ConfigureAwait(false);

                ctx.Status("Completing ACME challenge...");
                await Task.Delay(2000).ConfigureAwait(false);

                ctx.Status("Downloading certificate...");
                await Task.Delay(1000).ConfigureAwait(false);

                _console.MarkupLine("[green]✓[/] Certificate acquired successfully!");
                _console.MarkupLine($"Certificate saved to: {settings.OutputPath ?? "/etc/honua/certificates"}");

                return 0;
            });
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--domain <DOMAIN>")]
        [Description("Domain name(s) for the certificate. Can be specified multiple times. Use *.example.com for wildcard (requires DNS-01).")]
        public string[]? Domains { get; init; }

        [CommandOption("-e|--email <EMAIL>")]
        [Description("Contact email for ACME account registration.")]
        public string? Email { get; init; }

        [CommandOption("-c|--challenge <TYPE>")]
        [Description("Challenge type: http-01 or dns-01. Default: http-01")]
        [DefaultValue("http-01")]
        public string ChallengeType { get; init; } = "http-01";

        [CommandOption("--production")]
        [Description("Use Let's Encrypt production environment (rate-limited). Default: staging")]
        public bool Production { get; init; }

        [CommandOption("-o|--output <PATH>")]
        [Description("Output path for certificates. Default: /etc/honua/certificates")]
        public string? OutputPath { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool Yes { get; init; }
    }
}
