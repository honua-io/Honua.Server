// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents.Specialized;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to create tunnels for exposing local Honua to the internet.
/// </summary>
public sealed class TunnelCommand : AsyncCommand<TunnelCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly NetworkDiagnosticsAgent? _networkAgent;

    public TunnelCommand(
        IAnsiConsole console,
        NetworkDiagnosticsAgent? networkAgent = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _networkAgent = networkAgent;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_networkAgent == null)
        {
            _console.MarkupLine("[red]Network diagnostics agent not available.[/]");
            return 1;
        }

        _console.Write(new FigletText("Honua Tunnel").Color(Color.Blue));
        _console.WriteLine();

        _console.MarkupLine($"[green]Provider:[/] {settings.Provider}");
        _console.MarkupLine($"[green]Local Port:[/] {settings.Port}");
        if (!settings.Subdomain.IsNullOrEmpty())
        {
            _console.MarkupLine($"[green]Subdomain:[/] {settings.Subdomain}");
        }
        _console.WriteLine();

        // Show provider info
        ShowProviderInfo(settings.Provider);
        _console.WriteLine();

        if (!settings.Yes)
        {
            if (!_console.Confirm($"Create {settings.Provider} tunnel for port {settings.Port}?"))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return 1;
            }
        }

        return await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Creating {settings.Provider} tunnel...", async ctx =>
            {
                var request = new TunnelRequest
                {
                    Provider = settings.Provider,
                    LocalPort = settings.Port,
                    AuthToken = settings.AuthToken,
                    Subdomain = settings.Subdomain
                };

                var result = await _networkAgent.CreateTunnelAsync(request, default);

                if (result.Success)
                {
                    _console.MarkupLine("[green]✓ Tunnel created successfully![/]");
                    _console.WriteLine();

                    var panel = new Panel(new Markup($@"[bold]Public URL:[/] [blue]{result.PublicUrl}[/]
[bold]Local Port:[/] {result.LocalPort}
[bold]Provider:[/] {result.Provider}
{(result.ProcessId > 0 ? $"[bold]Process ID:[/] {result.ProcessId}" : "")}

[dim]{result.Message}[/]"))
                    {
                        Header = new PanelHeader("[bold green]Tunnel Active[/]"),
                        Border = BoxBorder.Rounded,
                        Padding = new Padding(2, 1)
                    };

                    _console.Write(panel);
                    _console.WriteLine();

                    _console.MarkupLine("[yellow]Press Ctrl+C to stop the tunnel[/]");
                    _console.WriteLine();

                    // Keep the tunnel process alive by waiting for it to exit
                    // The tunnel will stay active until the user presses Ctrl+C or the process exits
                    if (result.Process != null)
                    {
                        try
                        {
                            await result.Process.WaitForExitAsync(default);
                            _console.MarkupLine("[yellow]Tunnel process exited.[/]");
                        }
                        catch (OperationCanceledException)
                        {
                            _console.MarkupLine("[yellow]Stopping tunnel...[/]");
                            try
                            {
                                result.Process.Kill();
                                _console.MarkupLine("[green]Tunnel stopped.[/]");
                            }
                            catch
                            {
                                _console.MarkupLine("[yellow]Tunnel process may still be running (PID: {0})[/]", result.ProcessId);
                            }
                        }
                        finally
                        {
                            result.Process.Dispose();
                        }
                    }

                    return 0;
                }
                else
                {
                    _console.MarkupLine("[red]✗ Failed to create tunnel[/]");
                    _console.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");

                    if (!result.InstallInstructions.IsNullOrEmpty())
                    {
                        _console.WriteLine();
                        _console.MarkupLine("[yellow]Installation Instructions:[/]");
                        _console.MarkupLine($"[dim]{result.InstallInstructions}[/]");
                    }

                    return 1;
                }
            });
    }

    private void ShowProviderInfo(string provider)
    {
        var info = provider.ToLower() switch
        {
            "ngrok" => new
            {
                Name = "ngrok",
                Description = "Most popular tunnel service with free tier",
                Features = "• HTTP/HTTPS tunnels\n• Web dashboard at localhost:4040\n• Custom subdomains (paid)\n• Auth token required for extended sessions",
                Installation = "brew install ngrok (macOS) | choco install ngrok (Windows) | snap install ngrok (Linux)",
                Website = "https://ngrok.com"
            },
            "cloudflare" => new
            {
                Name = "Cloudflare Tunnel",
                Description = "Free, secure tunnels from Cloudflare",
                Features = "• Completely free\n• No account required for quick tunnels\n• Built-in DDoS protection\n• Fast global network",
                Installation = "https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/",
                Website = "https://developers.cloudflare.com/cloudflare-one/"
            },
            "localtunnel" => new
            {
                Name = "localtunnel",
                Description = "Simple npm-based tunnel service",
                Features = "• Completely free\n• Custom subdomains available\n• No sign-up required\n• Easy to use",
                Installation = "npm install -g localtunnel",
                Website = "https://localtunnel.github.io/www/"
            },
            "localhost.run" => new
            {
                Name = "localhost.run",
                Description = "SSH-based tunnel, no installation needed",
                Features = "• Zero installation (uses SSH)\n• No sign-up\n• Completely free\n• Just run SSH command",
                Installation = "No installation needed (uses SSH)",
                Website = "https://localhost.run"
            },
            _ => null
        };

        if (info != null)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Property[/]")
                .AddColumn("[bold]Details[/]");

            table.AddRow("Description", info.Description);
            table.AddRow("Features", info.Features);
            table.AddRow("Installation", info.Installation);
            table.AddRow("Website", $"[link]{info.Website}[/]");

            _console.Write(new Panel(table)
            {
                Header = new PanelHeader($"[bold]{info.Name}[/]"),
                Border = BoxBorder.Rounded
            });
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--provider <PROVIDER>")]
        [Description("Tunnel provider: ngrok, cloudflare, localtunnel, or localhost.run. Default: ngrok")]
        [DefaultValue("ngrok")]
        public string Provider { get; init; } = "ngrok";

        [CommandOption("--port <PORT>")]
        [Description("Local port to expose. Default: 8080")]
        [DefaultValue(8080)]
        public int Port { get; init; } = 8080;

        [CommandOption("-t|--auth-token <TOKEN>")]
        [Description("Auth token for ngrok (optional, extends session time)")]
        public string? AuthToken { get; init; }

        [CommandOption("-s|--subdomain <SUBDOMAIN>")]
        [Description("Custom subdomain (localtunnel only)")]
        public string? Subdomain { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        public bool Yes { get; init; }
    }
}
