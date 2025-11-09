// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Analytics;
using Honua.Cli.Services;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Displays the AI learning dashboard showing pattern confidence trends,
/// agent performance, and feedback loop effectiveness.
/// </summary>
public sealed class LearningDashboardCommand : AsyncCommand<LearningDashboardCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly LearningDashboardService? _dashboardService;
    private readonly ILogger<LearningDashboardCommand> _logger;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-c|--connection-string")]
        [Description("PostgreSQL connection string for telemetry database")]
        public string? ConnectionString { get; init; }

        [CommandOption("-w|--weeks")]
        [Description("Number of weeks of data to display (default: 12)")]
        [DefaultValue(12)]
        public int Weeks { get; init; } = 12;

        [CommandOption("-p|--pattern")]
        [Description("Filter by specific pattern ID")]
        public string? PatternId { get; init; }

        [CommandOption("-a|--agent")]
        [Description("Filter by specific agent name")]
        public string? AgentName { get; init; }

        [CommandOption("--insights-only")]
        [Description("Show only actionable insights")]
        public bool InsightsOnly { get; init; }
    }

    public LearningDashboardCommand(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        LearningDashboardService? dashboardService = null,
        ILogger<LearningDashboardCommand>? logger = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _dashboardService = dashboardService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Get connection string from settings or environment
        var connectionString = settings.ConnectionString
            ?? Environment.GetEnvironmentVariable("HONUA_TELEMETRY_CONNECTION_STRING")
            ?? "Host=localhost;Database=honua;Username=postgres;Password=postgres";

        var dashboardService = _dashboardService ?? new LearningDashboardService(
            connectionString,
            _logger);

        try
        {
            _console.Clear();
            _console.Write(
                new FigletText("Learning Dashboard")
                    .LeftJustified()
                    .Color(Color.Blue));

            _console.WriteLine();
            _console.MarkupLine("[dim]AI Feedback Loop Analytics[/]");
            _console.WriteLine();

            // Show insights first if requested
            if (settings.InsightsOnly)
            {
                await DisplayInsightsAsync(dashboardService);
                return 0;
            }

            // Display overall summary
            await DisplaySummaryAsync(dashboardService, TimeSpan.FromDays(settings.Weeks * 7));

            // Display pattern metrics
            await DisplayPatternMetricsAsync(dashboardService, settings.PatternId);

            // Display agent performance
            await DisplayAgentPerformanceAsync(dashboardService, settings.AgentName, settings.Weeks);

            // Display feature importance
            await DisplayFeatureImportanceAsync(dashboardService, settings.PatternId);

            // Display satisfaction trends
            await DisplaySatisfactionTrendsAsync(dashboardService, settings.Weeks);

            // Display actionable insights
            await DisplayInsightsAsync(dashboardService);

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            _logger.LogError(ex, "Failed to display learning dashboard");
            return 1;
        }
    }

    private async Task DisplaySummaryAsync(LearningDashboardService service, TimeSpan timeWindow)
    {
        var summary = await service.GetLearningStatsSummaryAsync(timeWindow);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Metric[/]").Centered())
            .AddColumn(new TableColumn("[bold]Value[/]").Centered());

        table.AddRow("Total Patterns", summary.TotalPatterns.ToString());
        table.AddRow("Total Interactions", summary.TotalInteractions.ToString());
        table.AddRow("Avg Acceptance Rate", $"{summary.AverageAcceptanceRate:P1}");
        table.AddRow("Avg Confidence", $"{summary.AverageConfidence:P1}");
        table.AddRow("Avg Decision Time", $"{summary.AverageDecisionTimeSeconds:F0}s");
        table.AddRow("Modified Patterns", summary.ModifiedCount.ToString());
        table.AddRow("Total Agents", summary.TotalAgents.ToString());
        table.AddRow("Avg Agent Success Rate", $"{summary.AverageAgentSuccessRate:P1}");
        table.AddRow("Total Deployments", summary.TotalDeployments.ToString());
        table.AddRow("Deployment Success Rate", $"{summary.DeploymentSuccessRate:P1}");
        table.AddRow("Avg Cost Accuracy", $"{summary.AverageCostAccuracy:F1}%");

        _console.Write(new Panel(table)
            .Header("[bold cyan]Overall Learning Statistics[/]")
            .BorderColor(Color.Cyan));
        _console.WriteLine();
    }

    private async Task DisplayPatternMetricsAsync(LearningDashboardService service, string? patternId)
    {
        var metrics = await service.GetPatternLearningMetricsAsync(topN: 10);

        if (!metrics.Any())
        {
            _console.MarkupLine("[yellow]No pattern metrics available.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Pattern ID")
            .AddColumn("Recommendations")
            .AddColumn("Acceptance")
            .AddColumn("Modified")
            .AddColumn("Avg Confidence")
            .AddColumn("Avg Decision")
            .AddColumn("Avg Questions")
            .AddColumn("Satisfaction");

        foreach (var metric in metrics.Take(10))
        {
            var acceptanceColor = metric.AcceptanceRate > 0.7 ? "green" : metric.AcceptanceRate < 0.3 ? "red" : "yellow";
            var modifiedColor = metric.ModificationRate > 0.5 ? "yellow" : "white";
            var satisfactionColor = metric.AvgSatisfaction.HasValue
                ? metric.AvgSatisfaction.Value >= 4 ? "green" : metric.AvgSatisfaction.Value <= 2 ? "red" : "yellow"
                : "dim";

            table.AddRow(
                metric.PatternId.Length > 30 ? metric.PatternId.Substring(0, 27) + "..." : metric.PatternId,
                metric.TotalRecommendations.ToString(),
                $"[{acceptanceColor}]{metric.AcceptanceRate:P0}[/]",
                $"[{modifiedColor}]{metric.ModificationRate:P0}[/]",
                $"{metric.AvgConfidence:P0}",
                $"{metric.AvgDecisionTimeSeconds:F0}s",
                $"{metric.AvgQuestions:F1}",
                metric.AvgSatisfaction.HasValue
                    ? $"[{satisfactionColor}]{metric.AvgSatisfaction.Value:F1}/5[/]"
                    : "[dim]N/A[/]"
            );
        }

        _console.Write(new Panel(table)
            .Header("[bold yellow]Top Pattern Learning Metrics[/]")
            .BorderColor(Color.Yellow));
        _console.WriteLine();
    }

    private async Task DisplayAgentPerformanceAsync(LearningDashboardService service, string? agentName, int weeks)
    {
        var trends = await service.GetAgentPerformanceTrendsAsync(agentName, weeks);

        if (!trends.Any())
        {
            _console.MarkupLine("[yellow]No agent performance data available.[/]");
            return;
        }

        // Group by agent and show latest week performance
        var latestPerformance = trends
            .GroupBy(t => t.AgentName)
            .Select(g => g.OrderByDescending(t => t.Week).First())
            .OrderByDescending(t => t.SuccessRate)
            .Take(10);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Agent Name")
            .AddColumn("Latest Week")
            .AddColumn("Executions")
            .AddColumn("Success Rate")
            .AddColumn("Avg Duration");

        foreach (var perf in latestPerformance)
        {
            var successColor = perf.SuccessRate > 0.8 ? "green" : perf.SuccessRate < 0.5 ? "red" : "yellow";

            table.AddRow(
                perf.AgentName,
                perf.Week.ToString("yyyy-MM-dd"),
                perf.ExecutionCount.ToString(),
                $"[{successColor}]{perf.SuccessRate:P0}[/]",
                $"{perf.AvgDurationMs:F0}ms"
            );
        }

        _console.Write(new Panel(table)
            .Header("[bold green]Agent Performance (Latest Week)[/]")
            .BorderColor(Color.Green));
        _console.WriteLine();
    }

    private async Task DisplayFeatureImportanceAsync(LearningDashboardService service, string? patternId)
    {
        var features = await service.GetFeatureImportanceAsync(patternId);

        if (!features.Any())
        {
            _console.MarkupLine("[yellow]No feature importance data available.[/]");
            return;
        }

        // Group by pattern and show top 5 most changed fields per pattern
        var topFeatures = features
            .GroupBy(f => f.PatternId)
            .SelectMany(g => g.OrderByDescending(f => f.ChangeCount).Take(5))
            .Take(20);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Pattern ID")
            .AddColumn("Field Modified")
            .AddColumn("Change Count")
            .AddColumn("% of Changes");

        foreach (var feature in topFeatures)
        {
            table.AddRow(
                feature.PatternId.Length > 25 ? feature.PatternId.Substring(0, 22) + "..." : feature.PatternId,
                feature.ChangedField,
                feature.ChangeCount.ToString(),
                $"{feature.ChangePercentage:F1}%"
            );
        }

        _console.Write(new Panel(table)
            .Header("[bold magenta]Feature Importance (Most Modified Fields)[/]")
            .BorderColor(Color.Magenta));
        _console.WriteLine();
    }

    private async Task DisplaySatisfactionTrendsAsync(LearningDashboardService service, int weeks)
    {
        var trends = await service.GetSatisfactionTrendsAsync(weeks);

        if (!trends.Any())
        {
            _console.MarkupLine("[yellow]No satisfaction data available.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Week")
            .AddColumn("Responses")
            .AddColumn("Avg Rating")
            .AddColumn("Positive")
            .AddColumn("Negative");

        foreach (var trend in trends.Take(12))
        {
            var ratingColor = trend.AvgRating >= 4 ? "green" : trend.AvgRating <= 2 ? "red" : "yellow";

            table.AddRow(
                trend.Week.ToString("yyyy-MM-dd"),
                trend.ResponseCount.ToString(),
                $"[{ratingColor}]{trend.AvgRating:F1}/5[/]",
                $"[green]{trend.PositiveCount}[/]",
                $"[red]{trend.NegativeCount}[/]"
            );
        }

        _console.Write(new Panel(table)
            .Header("[bold blue]User Satisfaction Trends[/]")
            .BorderColor(Color.Blue));
        _console.WriteLine();
    }

    private async Task DisplayInsightsAsync(LearningDashboardService service)
    {
        var insights = await service.GetPatternInsightsAsync();

        if (!insights.Any())
        {
            _console.MarkupLine("[yellow]No actionable insights available.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("Pattern ID")
            .AddColumn("Acceptance")
            .AddColumn("Modified")
            .AddColumn("Decision Time")
            .AddColumn("Questions")
            .AddColumn("Satisfaction")
            .AddColumn("Insight");

        foreach (var insight in insights.Take(15))
        {
            var insightColor = insight.Insight.Contains("well") ? "green"
                : insight.Insight.Contains("Low") || insight.Insight.Contains("High") || insight.Insight.Contains("Long") || insight.Insight.Contains("Many") ? "red"
                : "yellow";

            table.AddRow(
                insight.PatternId.Length > 20 ? insight.PatternId.Substring(0, 17) + "..." : insight.PatternId,
                $"{insight.AcceptanceRate:P0}",
                $"{insight.ModificationRate:P0}",
                $"{insight.AvgDecisionTime:F0}s",
                $"{insight.AvgQuestions:F1}",
                insight.AvgSatisfaction.HasValue ? $"{insight.AvgSatisfaction.Value:F1}/5" : "N/A",
                $"[{insightColor}]{insight.Insight}[/]"
            );
        }

        _console.Write(new Panel(table)
            .Header("[bold red]Actionable Insights[/]")
            .BorderColor(Color.Red));
        _console.WriteLine();
    }
}
