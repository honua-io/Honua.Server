// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Benchmarking Process workflow.
/// Tracks performance testing progress and results.
/// </summary>
public class BenchmarkState
{
    public string BenchmarkId { get; set; } = string.Empty;
    public string BenchmarkName { get; set; } = string.Empty;
    public string DeploymentUnderTest { get; set; } = string.Empty;
    public string TargetEndpoint { get; set; } = string.Empty;
    public string BenchmarkType { get; set; } = "Load"; // Baseline, Load, Stress
    public int ConcurrentUsers { get; set; } = 10;
    public int Concurrency { get; set; } = 10;
    public int DurationSeconds { get; set; } = 300;
    public int Duration { get; set; } = 300;
    public double RequestsPerSecond { get; set; }
    public double P95Latency { get; set; }
    public double P99Latency { get; set; }
    public double ErrorRate { get; set; }
    public List<string> Bottlenecks { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime StartTime { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal? BaselineLatencyP95 { get; set; }
    public decimal? LoadTestLatencyP95 { get; set; }
    public decimal? StressTestLatencyP95 { get; set; }
    public int? MaxThroughputRps { get; set; }
    public decimal? ErrorRatePercent { get; set; }
    public string? ReportUrl { get; set; }
    public bool CacheWarmed { get; set; }
}
