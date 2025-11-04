// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Honua.Cli.Utilities;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to display analytics dashboard for learning loop metrics.
/// </summary>
public sealed class AnalyticsDashboardCommand : AsyncCommand<AnalyticsDashboardCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly PostgreSqlTelemetryService? _telemetryService;
    private readonly ILogger<AnalyticsDashboardCommand> _logger;

    public AnalyticsDashboardCommand(
        IAnsiConsole console,
        ILogger<AnalyticsDashboardCommand> logger,
        PostgreSqlTelemetryService? telemetryService = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryService = telemetryService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                if (_telemetryService == null)
                {
                    _console.MarkupLine("[red]Analytics dashboard requires PostgreSQL telemetry service to be configured.[/]");
                    _console.MarkupLine("Set connection string in environment: HONUA_TELEMETRY_CONNECTION_STRING");
                    return 1;
                }

                _console.Clear();
                _console.Write(new FigletText("Honua Analytics").Color(Color.Blue));
                _console.WriteLine();

                var timeWindow = TimeSpan.FromDays(settings.Days);

                await RenderLlmProviderStatsAsync(timeWindow, settings.TaskType);
                _console.WriteLine();

                await RenderTopPerformingPatternsAsync(timeWindow, settings.Limit);
                _console.WriteLine();

                if (settings.Refresh > 0)
                {
                    _console.MarkupLine($"[grey]Refreshing every {settings.Refresh} seconds. Press Ctrl+C to exit.[/]");
                    await Task.Delay(settings.Refresh * 1000).ConfigureAwait(false);
                    return await ExecuteAsync(context, settings); // Recursive refresh
                }

                return 0;
            },
            _logger,
            "analytics-dashboard");
    }

    private async Task RenderLlmProviderStatsAsync(TimeSpan timeWindow, string? taskType)
    {
        var panel = new Panel(await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading LLM provider statistics...", async ctx =>
            {
                // Get provider stats for all task types or specific task type
                var allStats = taskType != null
                    ? new[] { (taskType, await _telemetryService!.GetProviderStatsAsync(taskType, timeWindow)) }
                    : new[]
                    {
                        ("deployment", await _telemetryService!.GetProviderStatsAsync("deployment", timeWindow)),
                        ("security-review", await _telemetryService!.GetProviderStatsAsync("security-review", timeWindow)),
                        ("architecture", await _telemetryService!.GetProviderStatsAsync("architecture", timeWindow))
                    };

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Task Type[/]"))
                    .AddColumn(new TableColumn("[bold]Provider[/]"))
                    .AddColumn(new TableColumn("[bold]Requests[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Success Rate[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Avg Latency[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Total Cost[/]").RightAligned());

                foreach (var (task, providerStats) in allStats)
                {
                    if (!providerStats.Any())
                    {
                        table.AddRow(task, "[grey]No data[/]", "-", "-", "-", "-");
                        continue;
                    }

                    var first = true;
                    foreach (var (provider, stats) in providerStats.OrderByDescending(kvp => kvp.Value.TotalRequests))
                    {
                        var successColor = stats.SuccessRate > 0.9 ? "green" : stats.SuccessRate > 0.7 ? "yellow" : "red";
                        var latencyColor = stats.AvgLatencyMs < 2000 ? "green" : stats.AvgLatencyMs < 5000 ? "yellow" : "red";

                        table.AddRow(
                            first ? task : "",
                            provider,
                            stats.TotalRequests.ToString(),
                            $"[{successColor}]{stats.SuccessRate:P0}[/]",
                            $"[{latencyColor}]{stats.AvgLatencyMs:F0}ms[/]",
                            stats.TotalCostUsd > 0 ? $"${stats.TotalCostUsd:F2}" : "-"
                        );
                        first = false;
                    }
                }

                return table;
            }))
        {
            Header = new PanelHeader($"[bold]LLM Provider Performance[/] [grey](Last {timeWindow.Days} days)[/]"),
            Expand = true
        };

        _console.Write(panel);
    }

    private async Task RenderTopPerformingPatternsAsync(TimeSpan timeWindow, int limit)
    {
        var panel = new Panel(await _console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading pattern statistics...", async ctx =>
            {
                var patterns = await _telemetryService!.GetTopPerformingPatternsAsync(limit, timeWindow);

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]#[/]"))
                    .AddColumn(new TableColumn("[bold]Agent[/]"))
                    .AddColumn(new TableColumn("[bold]Pattern[/]"))
                    .AddColumn(new TableColumn("[bold]Executions[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Success Rate[/]").RightAligned())
                    .AddColumn(new TableColumn("[bold]Avg Duration[/]").RightAligned());

                if (!patterns.Any())
                {
                    table.AddRow("", "[grey]No patterns executed yet[/]", "", "", "", "");
                }
                else
                {
                    for (int i = 0; i < patterns.Count; i++)
                    {
                        var pattern = patterns[i];
                        var successColor = pattern.SuccessRate > 0.9 ? "green" : pattern.SuccessRate > 0.7 ? "yellow" : "red";
                        var durationColor = pattern.AvgDurationMs < 1000 ? "green" : pattern.AvgDurationMs < 5000 ? "yellow" : "red";

                        table.AddRow(
                            (i + 1).ToString(),
                            pattern.AgentName,
                            pattern.PatternName,
                            pattern.ExecutionCount.ToString(),
                            $"[{successColor}]{pattern.SuccessRate:P0}[/]",
                            $"[{durationColor}]{pattern.AvgDurationMs:F0}ms[/]"
                        );
                    }
                }

                return table;
            }))
        {
            Header = new PanelHeader($"[bold]Top Performing Patterns[/] [grey](Last {timeWindow.Days} days)[/]"),
            Expand = true
        };

        _console.Write(panel);
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--days <DAYS>")]
        [Description("Number of days to analyze. Default: 7")]
        [DefaultValue(7)]
        public int Days { get; init; } = 7;

        [CommandOption("-t|--task-type <TYPE>")]
        [Description("Filter by specific task type (e.g., deployment, security-review)")]
        public string? TaskType { get; init; }

        [CommandOption("-l|--limit <COUNT>")]
        [Description("Number of top patterns to display. Default: 10")]
        [DefaultValue(10)]
        public int Limit { get; init; } = 10;

        [CommandOption("-r|--refresh <SECONDS>")]
        [Description("Auto-refresh interval in seconds. 0 = no refresh. Default: 0")]
        [DefaultValue(0)]
        public int Refresh { get; init; } = 0;
    }
}
