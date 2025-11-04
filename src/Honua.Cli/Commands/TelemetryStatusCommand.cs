// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to show telemetry status and recent data.
/// </summary>
public sealed class TelemetryStatusCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var honuaDir = Path.Combine(userHome, ".honua");
            var configPath = Path.Combine(honuaDir, "telemetry-config.json");
            var telemetryPath = Path.Combine(honuaDir, "telemetry");

            // Check if enabled
            bool enabled = false;
            DateTime? consentTimestamp = null;
            string? userId = null;

            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<TelemetryOptions>(json);
                if (config != null)
                {
                    enabled = config.Enabled;
                    consentTimestamp = config.ConsentTimestamp;
                    userId = config.UserId;
                }
            }

            // Create status panel
            var statusColor = enabled ? Color.Green : Color.Grey;
            var statusText = enabled ? "Enabled" : "Disabled";

            var panelContent = enabled
                ? $"[{statusColor}]Status: {statusText}[/]\n\n" +
                  $"[dim]Consent given:[/] {consentTimestamp:yyyy-MM-dd HH:mm}\n" +
                  $"[dim]User ID:[/] {userId}\n" +
                  $"[dim]Data location:[/] {telemetryPath}\n" +
                  $"[dim]Config:[/] {configPath}\n"
                : $"[{statusColor}]Status: {statusText}[/]\n\n" +
                  "[dim]Telemetry is disabled[/]\n" +
                  "[dim]Enable with:[/] [cyan]honua telemetry enable[/]\n";

            var panel = new Panel(new Markup(panelContent))
            {
                Header = new PanelHeader(" Telemetry Status ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(statusColor)
            };

            AnsiConsole.Write(panel);

            // If enabled, show recent activity
            if (enabled && Directory.Exists(telemetryPath))
            {
                var files = Directory.GetFiles(telemetryPath, "telemetry-*.jsonl")
                    .OrderByDescending(f => f)
                    .Take(7)
                    .ToList();

                if (files.Any())
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[yellow]Recent Activity:[/]");

                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .BorderColor(Color.Grey)
                        .AddColumn("Date")
                        .AddColumn("Events")
                        .AddColumn("Size");

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var lineCount = File.ReadLines(file).Count();
                        var date = Path.GetFileNameWithoutExtension(file).Replace("telemetry-", "");

                        table.AddRow(
                            date,
                            lineCount.ToString(),
                            $"{fileInfo.Length / 1024.0:F1} KB");
                    }

                    AnsiConsole.Write(table);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]View data:[/] [cyan]cat {0}[/]", files.First());
                    AnsiConsole.MarkupLine("[dim]Delete data:[/] [cyan]rm -r {0}[/]", telemetryPath);
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]No telemetry data collected yet[/]");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to check telemetry status: {ex.Message}");
            return 1;
        }
    }
}
