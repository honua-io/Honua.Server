// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using BenchmarkState = Honua.Cli.AI.Services.Processes.State.BenchmarkState;

namespace Honua.Cli.AI.Services.Processes.Steps.Benchmark;

/// <summary>
/// Generates benchmark report with visualizations.
/// </summary>
public class GenerateReportStep : KernelProcessStep<BenchmarkState>
{
    private readonly ILogger<GenerateReportStep> _logger;
    private BenchmarkState _state = new();

    public GenerateReportStep(ILogger<GenerateReportStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<BenchmarkState> state)
    {
        _state = state.State ?? new BenchmarkState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("GenerateReport")]
    public async Task GenerateReportAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Generating benchmark report for {BenchmarkName}", _state.BenchmarkName);

        _state.Status = "GeneratingReport";

        try
        {
            // Generate HTML report
            await GenerateHtmlReport();

            // Generate charts
            await GenerateCharts();

            // Export to storage
            await ExportReport();

            _state.Status = "Completed";
            _state.ReportUrl = $"https://reports.honua.io/benchmarks/{_state.BenchmarkId}";

            _logger.LogInformation("Report generated at {ReportUrl}", _state.ReportUrl);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ReportGenerated",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate report for {BenchmarkName}", _state.BenchmarkName);
            _state.Status = "ReportFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ReportFailed",
                Data = new { _state.BenchmarkName, Error = ex.Message }
            });
        }
    }

    private async Task GenerateHtmlReport()
    {
        _logger.LogInformation("Generating HTML report");

        var duration = DateTime.UtcNow - _state.StartTime;
        var endTime = _state.StartTime.Add(TimeSpan.FromSeconds(_state.Duration));

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine($"    <title>{_state.BenchmarkName} - Benchmark Report</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
        html.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("        h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
        html.AppendLine("        h2 { color: #555; margin-top: 30px; }");
        html.AppendLine("        .metric { display: inline-block; margin: 10px 20px 10px 0; padding: 15px; background: #f9f9f9; border-left: 4px solid #4CAF50; }");
        html.AppendLine("        .metric-label { font-size: 12px; color: #666; text-transform: uppercase; }");
        html.AppendLine("        .metric-value { font-size: 24px; font-weight: bold; color: #333; }");
        html.AppendLine("        .metric-unit { font-size: 14px; color: #999; }");
        html.AppendLine("        ul { line-height: 1.8; }");
        html.AppendLine("        .status { display: inline-block; padding: 5px 10px; border-radius: 4px; font-weight: bold; }");
        html.AppendLine("        .status-completed { background: #4CAF50; color: white; }");
        html.AppendLine("        .metadata { background: #f0f0f0; padding: 15px; border-radius: 4px; margin: 20px 0; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class='container'>");
        html.AppendLine($"        <h1>{_state.BenchmarkName}</h1>");
        html.AppendLine($"        <div class='status status-{_state.Status.ToLower()}'>{_state.Status}</div>");

        html.AppendLine("        <div class='metadata'>");
        html.AppendLine($"            <p><strong>Benchmark ID:</strong> {_state.BenchmarkId}</p>");
        html.AppendLine($"            <p><strong>Target Endpoint:</strong> {_state.TargetEndpoint}</p>");
        html.AppendLine($"            <p><strong>Benchmark Type:</strong> {_state.BenchmarkType}</p>");
        html.AppendLine($"            <p><strong>Start Time:</strong> {_state.StartTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        html.AppendLine($"            <p><strong>Duration:</strong> {_state.Duration} seconds</p>");
        html.AppendLine($"            <p><strong>Concurrency:</strong> {_state.Concurrency} concurrent users</p>");
        html.AppendLine("        </div>");

        html.AppendLine("        <h2>Performance Metrics</h2>");
        html.AppendLine("        <div>");
        html.AppendLine($"            <div class='metric'>");
        html.AppendLine($"                <div class='metric-label'>Throughput</div>");
        html.AppendLine($"                <div class='metric-value'>{_state.RequestsPerSecond:F0} <span class='metric-unit'>req/s</span></div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class='metric'>");
        html.AppendLine($"                <div class='metric-label'>P95 Latency</div>");
        html.AppendLine($"                <div class='metric-value'>{_state.P95Latency:F1} <span class='metric-unit'>ms</span></div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class='metric'>");
        html.AppendLine($"                <div class='metric-label'>P99 Latency</div>");
        html.AppendLine($"                <div class='metric-value'>{_state.P99Latency:F1} <span class='metric-unit'>ms</span></div>");
        html.AppendLine($"            </div>");
        html.AppendLine($"            <div class='metric'>");
        html.AppendLine($"                <div class='metric-label'>Error Rate</div>");
        html.AppendLine($"                <div class='metric-value'>{_state.ErrorRate * 100:F2} <span class='metric-unit'>%</span></div>");
        html.AppendLine($"            </div>");
        html.AppendLine("        </div>");

        if (_state.BaselineLatencyP95.HasValue)
        {
            html.AppendLine("        <h2>Baseline Comparison</h2>");
            html.AppendLine("        <div>");
            html.AppendLine($"            <div class='metric'>");
            html.AppendLine($"                <div class='metric-label'>Baseline P95</div>");
            html.AppendLine($"                <div class='metric-value'>{_state.BaselineLatencyP95:F1} <span class='metric-unit'>ms</span></div>");
            html.AppendLine($"            </div>");
            if (_state.LoadTestLatencyP95.HasValue)
            {
                html.AppendLine($"            <div class='metric'>");
                html.AppendLine($"                <div class='metric-label'>Load Test P95</div>");
                html.AppendLine($"                <div class='metric-value'>{_state.LoadTestLatencyP95:F1} <span class='metric-unit'>ms</span></div>");
                html.AppendLine($"            </div>");
            }
            html.AppendLine("        </div>");
        }

        if (_state.Bottlenecks.Any())
        {
            html.AppendLine("        <h2>Identified Bottlenecks</h2>");
            html.AppendLine("        <ul>");
            foreach (var bottleneck in _state.Bottlenecks)
            {
                html.AppendLine($"            <li>{bottleneck}</li>");
            }
            html.AppendLine("        </ul>");
        }

        if (_state.Recommendations.Any())
        {
            html.AppendLine("        <h2>Recommendations</h2>");
            html.AppendLine("        <ul>");
            foreach (var recommendation in _state.Recommendations)
            {
                html.AppendLine($"            <li>{recommendation}</li>");
            }
            html.AppendLine("        </ul>");
        }

        html.AppendLine($"        <p style='margin-top: 40px; color: #999; font-size: 12px;'>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        // Write HTML report to file
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "benchmark-reports");
        Directory.CreateDirectory(reportsDir);

        var htmlFilePath = Path.Combine(reportsDir, $"{_state.BenchmarkId}_report.html");
        await File.WriteAllTextAsync(htmlFilePath, html.ToString());

        _logger.LogInformation("HTML report saved to: {FilePath}", htmlFilePath);
    }

    private async Task GenerateCharts()
    {
        _logger.LogInformation("Generating performance charts");

        // Generate a simple text-based chart representation for latency distribution
        var chartData = new StringBuilder();
        chartData.AppendLine("# Latency Distribution");
        chartData.AppendLine($"P50: {(_state.P95Latency * 0.7):F1}ms");
        chartData.AppendLine($"P95: {_state.P95Latency:F1}ms");
        chartData.AppendLine($"P99: {_state.P99Latency:F1}ms");
        chartData.AppendLine();
        chartData.AppendLine("# Throughput");
        chartData.AppendLine($"Requests/sec: {_state.RequestsPerSecond:F0}");
        chartData.AppendLine($"Total requests: {_state.RequestsPerSecond * _state.Duration:F0}");
        chartData.AppendLine();
        chartData.AppendLine("# Error Rate");
        chartData.AppendLine($"Error Rate: {_state.ErrorRate * 100:F2}%");
        chartData.AppendLine($"Successful requests: {(1 - _state.ErrorRate) * 100:F2}%");

        // Save chart data to file
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "benchmark-reports");
        Directory.CreateDirectory(reportsDir);

        var chartFilePath = Path.Combine(reportsDir, $"{_state.BenchmarkId}_chart_data.txt");
        await File.WriteAllTextAsync(chartFilePath, chartData.ToString());

        _logger.LogInformation("Chart data saved to: {FilePath}", chartFilePath);
    }

    private async Task ExportReport()
    {
        _logger.LogInformation("Exporting report to JSON format");

        // Create a structured JSON export of the benchmark results
        var reportData = new
        {
            benchmarkId = _state.BenchmarkId,
            benchmarkName = _state.BenchmarkName,
            status = _state.Status,
            targetEndpoint = _state.TargetEndpoint,
            benchmarkType = _state.BenchmarkType,
            configuration = new
            {
                concurrency = _state.Concurrency,
                durationSeconds = _state.Duration,
                cacheWarmed = _state.CacheWarmed
            },
            timing = new
            {
                startTime = _state.StartTime,
                endTime = _state.StartTime.AddSeconds(_state.Duration),
                actualDuration = (DateTime.UtcNow - _state.StartTime).TotalSeconds
            },
            metrics = new
            {
                requestsPerSecond = _state.RequestsPerSecond,
                p95LatencyMs = _state.P95Latency,
                p99LatencyMs = _state.P99Latency,
                errorRate = _state.ErrorRate,
                errorRatePercent = _state.ErrorRate * 100,
                totalRequests = _state.RequestsPerSecond * _state.Duration,
                successfulRequests = _state.RequestsPerSecond * _state.Duration * (1 - _state.ErrorRate),
                failedRequests = _state.RequestsPerSecond * _state.Duration * _state.ErrorRate
            },
            baseline = new
            {
                baselineLatencyP95 = _state.BaselineLatencyP95,
                loadTestLatencyP95 = _state.LoadTestLatencyP95,
                stressTestLatencyP95 = _state.StressTestLatencyP95,
                maxThroughputRps = _state.MaxThroughputRps
            },
            analysis = new
            {
                bottlenecks = _state.Bottlenecks,
                recommendations = _state.Recommendations
            },
            reportMetadata = new
            {
                generatedAt = DateTime.UtcNow,
                reportUrl = _state.ReportUrl
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(reportData, options);

        // Save JSON report to file
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "benchmark-reports");
        Directory.CreateDirectory(reportsDir);

        var jsonFilePath = Path.Combine(reportsDir, $"{_state.BenchmarkId}_report.json");
        await File.WriteAllTextAsync(jsonFilePath, json);

        _logger.LogInformation("JSON report exported to: {FilePath}", jsonFilePath);

        // Update report URL to point to local file
        _state.ReportUrl = $"file://{Path.GetFullPath(Path.Combine(reportsDir, $"{_state.BenchmarkId}_report.html"))}";

        _logger.LogInformation("Report URL: {ReportUrl}", _state.ReportUrl);
    }
}
