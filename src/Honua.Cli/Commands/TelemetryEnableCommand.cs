// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to enable privacy-first telemetry.
/// </summary>
public sealed class TelemetryEnableCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            // Show privacy information
            var panel = new Panel(
                new Markup(
                    "[yellow]Privacy-First Telemetry[/]\n\n" +
                    "Honua telemetry is [green]opt-in only[/] and [green]privacy-focused[/]:\n\n" +
                    "• [cyan]✓[/] Anonymous user IDs (no personal information)\n" +
                    "• [cyan]✓[/] Local storage (~/.honua/telemetry/*.jsonl)\n" +
                    "• [cyan]✓[/] You can review/delete data anytime\n" +
                    "• [cyan]✓[/] Tracks: commands, LLM usage, errors (not your data)\n\n" +
                    "[dim]What we collect:[/]\n" +
                    "  - Command usage frequency\n" +
                    "  - LLM API costs (OpenAI/Anthropic)\n" +
                    "  - Error types (sanitized)\n" +
                    "  - Feature usage patterns\n\n" +
                    "[dim]What we DON'T collect:[/]\n" +
                    "  - Your database data\n" +
                    "  - Credentials or secrets\n" +
                    "  - File paths or connection strings\n" +
                    "  - Personal identifiable information"))
            {
                Header = new PanelHeader(" Telemetry Information ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();

            // Confirm consent
            var consent = AnsiConsole.Confirm(
                "[yellow]Do you consent to privacy-first telemetry?[/]",
                false);

            if (!consent)
            {
                AnsiConsole.MarkupLine("[dim]Telemetry not enabled[/]");
                return 0;
            }

            // Create telemetry config
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var honuaDir = Path.Combine(userHome, ".honua");
            var configPath = Path.Combine(honuaDir, "telemetry-config.json");

            Directory.CreateDirectory(honuaDir);

            var config = new TelemetryOptions
            {
                Enabled = true,
                ConsentTimestamp = DateTime.UtcNow,
                UserId = Guid.NewGuid().ToString("N"),
                Backend = TelemetryBackend.LocalFile,
                LocalFilePath = Path.Combine(honuaDir, "telemetry")
            };

            var json = JsonSerializer.Serialize(config, JsonSerializerOptionsRegistry.WebIndented);
            await File.WriteAllTextAsync(configPath, json);

            AnsiConsole.MarkupLine($"[green]✓[/] Telemetry enabled");
            AnsiConsole.MarkupLine($"[dim]Config: {configPath}[/]");
            AnsiConsole.MarkupLine($"[dim]Data: {config.LocalFilePath}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]You can disable telemetry anytime with:[/] [cyan]honua telemetry disable[/]");
            AnsiConsole.MarkupLine("[dim]View status with:[/] [cyan]honua telemetry status[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to enable telemetry: {ex.Message}");
            return 1;
        }
    }
}
