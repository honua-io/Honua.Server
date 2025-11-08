// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.VectorSearch;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to show deployment pattern statistics and analytics.
/// </summary>
public sealed class ConsultantPatternsStatsCommand : AsyncCommand<ConsultantPatternsStatsCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IPatternUsageTelemetry? _telemetry;
    private readonly IDeploymentPatternKnowledgeStore? _patternStore;

    public ConsultantPatternsStatsCommand(
        IAnsiConsole console,
        IPatternUsageTelemetry? telemetry = null,
        IDeploymentPatternKnowledgeStore? patternStore = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _telemetry = telemetry;
        _patternStore = patternStore;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (_telemetry == null || _patternStore == null)
        {
            _console.MarkupLine("[red]Pattern telemetry is not configured.[/]");
            _console.MarkupLine("[yellow]Ensure Azure AI services are configured in appsettings.json[/]");
            return 1;
        }

        try
        {
            if (!settings.PatternId.IsNullOrEmpty())
            {
                await ShowPatternStatsAsync(settings.PatternId, settings.Days);
            }
            else
            {
                await ShowTopPatternsAsync(settings.Top, settings.Days);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private async Task ShowPatternStatsAsync(string patternId, int days)
    {
        var period = TimeSpan.FromDays(days);
        var stats = await _telemetry!.GetUsageStatsAsync(patternId, period);

        _console.WriteLine();
        _console.MarkupLine($"[bold]Pattern Statistics: {patternId}[/]");
        _console.MarkupLine($"[dim]Last {days} days[/]");
        _console.WriteLine();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Times Recommended", stats.TimesRecommended.ToString());
        table.AddRow("Times Accepted", $"{stats.TimesAccepted} ({stats.AcceptanceRate:P0})");
        table.AddRow("Times Deployed", stats.TimesDeployed.ToString());
        table.AddRow("Successful Deployments", $"{stats.SuccessfulDeployments} ({stats.SuccessRate:P0})");
        table.AddRow("Failed Deployments", stats.FailedDeployments.ToString());

        _console.Write(table);
        _console.WriteLine();

        // Show acceptance rate indicator
        if (stats.AcceptanceRate >= 0.7)
        {
            _console.MarkupLine("[green]✓ High acceptance rate - users trust this pattern[/]");
        }
        else if (stats.AcceptanceRate >= 0.4)
        {
            _console.MarkupLine("[yellow]⚠ Medium acceptance rate - pattern may need review[/]");
        }
        else if (stats.TimesRecommended > 5)
        {
            _console.MarkupLine("[red]✗ Low acceptance rate - consider updating or deprecating[/]");
        }

        // Show success rate indicator
        if (stats.TimesDeployed > 0)
        {
            if (stats.SuccessRate >= 0.9)
            {
                _console.MarkupLine("[green]✓ Excellent success rate - deployments are reliable[/]");
            }
            else if (stats.SuccessRate >= 0.7)
            {
                _console.MarkupLine("[yellow]⚠ Good success rate - room for improvement[/]");
            }
            else
            {
                _console.MarkupLine("[red]✗ Low success rate - investigate deployment issues[/]");
            }
        }
    }

    private async Task ShowTopPatternsAsync(int top, int days)
    {
        _console.WriteLine();
        _console.MarkupLine($"[bold]Top {top} Deployment Patterns[/]");
        _console.MarkupLine($"[dim]Last {days} days[/]");
        _console.WriteLine();

        // Search for all patterns (using empty requirements to get all)
        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 100,
            ConcurrentUsers = 50
        };

        var patterns = await _patternStore!.SearchPatternsAsync(requirements);
        var period = TimeSpan.FromDays(days);

        // Get stats for each pattern
        var patternStats = new System.Collections.Generic.List<(PatternSearchResult Pattern, PatternUsageStats Stats)>();
        foreach (var pattern in patterns.Take(top))
        {
            var stats = await _telemetry!.GetUsageStatsAsync(pattern.Id, period);
            patternStats.Add((pattern, stats));
        }

        // Sort by recommendation count (most recommended first)
        var sortedStats = patternStats
            .OrderByDescending(x => x.Stats.TimesRecommended)
            .Take(top)
            .ToList();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Pattern");
        table.AddColumn("Confidence");
        table.AddColumn("Recommended");
        table.AddColumn("Acceptance");
        table.AddColumn("Deployed");
        table.AddColumn("Success");

        foreach (var (pattern, stats) in sortedStats)
        {
            var confidence = pattern.GetConfidence();
            var confidenceColor = confidence.Level switch
            {
                "High" => "green",
                "Medium" => "yellow",
                _ => "silver"
            };

            var acceptanceColor = stats.AcceptanceRate switch
            {
                >= 0.7 => "green",
                >= 0.4 => "yellow",
                _ => "silver"
            };

            var successColor = stats.SuccessRate switch
            {
                >= 0.9 => "green",
                >= 0.7 => "yellow",
                _ => stats.TimesDeployed > 0 ? "red" : "silver"
            };

            table.AddRow(
                $"{pattern.PatternName}",
                $"[{confidenceColor}]{confidence.Level}[/]",
                stats.TimesRecommended.ToString(),
                $"[{acceptanceColor}]{stats.AcceptanceRate:P0}[/]",
                stats.TimesDeployed.ToString(),
                $"[{successColor}]{stats.SuccessRate:P0}[/]");
        }

        _console.Write(table);
        _console.WriteLine();

        // Show insights
        var highAcceptance = sortedStats.Count(x => x.Stats.AcceptanceRate >= 0.7);
        var lowAcceptance = sortedStats.Count(x => x.Stats.AcceptanceRate < 0.4 && x.Stats.TimesRecommended > 5);

        _console.MarkupLine($"[bold]Insights:[/]");
        _console.MarkupLine($"• {highAcceptance} patterns have high acceptance (≥70%)");
        if (lowAcceptance > 0)
        {
            _console.MarkupLine($"• [yellow]{lowAcceptance} patterns have low acceptance (<40%) - review recommended[/]");
        }

        var totalDeployments = sortedStats.Sum(x => x.Stats.TimesDeployed);
        var successfulDeployments = sortedStats.Sum(x => x.Stats.SuccessfulDeployments);
        var overallSuccess = totalDeployments > 0 ? (double)successfulDeployments / totalDeployments : 0;
        _console.MarkupLine($"• Overall success rate: {overallSuccess:P0} ({successfulDeployments}/{totalDeployments})");
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--pattern-id <ID>")]
        [Description("Show stats for a specific pattern")]
        public string? PatternId { get; init; }

        [CommandOption("--days <DAYS>")]
        [Description("Time period in days for statistics")]
        [DefaultValue(30)]
        public int Days { get; init; } = 30;

        [CommandOption("--top <N>")]
        [Description("Show top N patterns")]
        [DefaultValue(10)]
        public int Top { get; init; } = 10;
    }
}
