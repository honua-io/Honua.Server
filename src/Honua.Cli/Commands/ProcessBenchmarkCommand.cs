// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
#pragma warning disable SKEXP0080 // Suppress experimental API warnings for SK Process Framework

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// CLI command to run performance benchmarks using Semantic Kernel Process Framework.
/// Orchestrates benchmark workflow: setup → run → analyze → generate report.
/// </summary>
[Description("Run performance benchmark on Honua deployment")]
public sealed class ProcessBenchmarkCommand : AsyncCommand<ProcessBenchmarkCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly Kernel _kernel;

    public ProcessBenchmarkCommand(IAnsiConsole console, Kernel kernel)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Honua Performance Benchmark[/]");
            _console.WriteLine();

            // Validate required parameters
            if (settings.TargetEndpoint.IsNullOrWhiteSpace())
            {
                _console.MarkupLine("[red]Error: Target endpoint is required (--target-endpoint)[/]");
                return 1;
            }

            // Validate benchmark type
            if (!IsValidBenchmarkType(settings.BenchmarkType))
            {
                _console.MarkupLine($"[red]Error: Invalid benchmark type '{settings.BenchmarkType}'. Valid types: Baseline, Load, Stress[/]");
                return 1;
            }

            // Build benchmark state from settings
            var benchmarkState = new BenchmarkState
            {
                BenchmarkId = Guid.NewGuid().ToString(),
                BenchmarkName = settings.BenchmarkName ?? $"{settings.BenchmarkType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                DeploymentUnderTest = settings.DeploymentName ?? "unknown",
                TargetEndpoint = settings.TargetEndpoint,
                BenchmarkType = settings.BenchmarkType,
                Concurrency = settings.Concurrency,
                Duration = settings.Duration,
                StartTime = DateTime.UtcNow,
                Status = "Starting"
            };

            // Display benchmark configuration
            var configTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Configuration")
                .AddColumn("Value");

            configTable.AddRow("[cyan]Benchmark Name[/]", benchmarkState.BenchmarkName);
            configTable.AddRow("[cyan]Target Endpoint[/]", benchmarkState.TargetEndpoint);
            configTable.AddRow("[cyan]Deployment[/]", benchmarkState.DeploymentUnderTest);
            configTable.AddRow("[cyan]Benchmark Type[/]", FormatBenchmarkType(benchmarkState.BenchmarkType));
            configTable.AddRow("[cyan]Concurrency[/]", benchmarkState.Concurrency.ToString());
            configTable.AddRow("[cyan]Duration[/]", $"{benchmarkState.Duration} seconds");
            configTable.AddRow("[cyan]Benchmark ID[/]", benchmarkState.BenchmarkId);

            _console.Write(configTable);
            _console.WriteLine();

            if (settings.WarmCache)
            {
                _console.MarkupLine("[yellow]Note: Cache warming will be performed before the benchmark.[/]");
            }

            if (!settings.AutoConfirm && !_console.Confirm("Start benchmark with these settings?"))
            {
                _console.MarkupLine("[yellow]Benchmark cancelled by user[/]");
                return 0;
            }

            // Build and start the benchmark process
            var processBuilder = BenchmarkProcess.BuildProcess();
            var process = processBuilder.Build();

            // Start process with initial event
            await _console.Status()
                .StartAsync("Initializing benchmark process...", async ctx =>
                {
                    ctx.Status("Starting performance benchmark workflow...");

                    // Start the process with the benchmark state
                    var processHandle = await process.StartAsync(
                        new KernelProcessEvent
                        {
                            Id = "StartBenchmark",
                            Data = benchmarkState
                        },
                        Guid.NewGuid().ToString());

                    ctx.Status("Benchmark process started successfully");

                    _console.MarkupLine($"[green]Process started successfully[/]");
                    _console.MarkupLine($"[dim]Process ID: {processHandle}[/]");
                });

            _console.WriteLine();
            _console.MarkupLine("[bold green]Benchmark process initiated successfully[/]");
            _console.WriteLine();

            _console.MarkupLine("[bold]Next Steps:[/]");
            _console.MarkupLine($"  1. Monitor benchmark: [cyan]honua process status {benchmarkState.BenchmarkId}[/]");
            _console.MarkupLine("  2. View analytics: [cyan]honua analytics dashboard[/]");
            _console.MarkupLine("  3. Check server health: [cyan]honua status[/]");

            var estimatedTime = benchmarkState.Duration + 60; // Add setup time
            _console.WriteLine();
            _console.MarkupLine($"[yellow]Estimated completion time: ~{estimatedTime / 60} minutes[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            if (settings.Verbose)
            {
                _console.WriteException(ex);
            }
            return 1;
        }
    }

    private static bool IsValidBenchmarkType(string type)
    {
        return type.Equals("Baseline", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Load", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Stress", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBenchmarkType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "baseline" => "[green]Baseline[/] (single user)",
            "load" => "[yellow]Load Testing[/] (normal traffic)",
            "stress" => "[red]Stress Testing[/] (peak traffic)",
            _ => type
        };
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--target-endpoint")]
        [Description("Target endpoint URL to benchmark (required)")]
        public string TargetEndpoint { get; set; } = string.Empty;

        [CommandOption("--deployment-name")]
        [Description("Name of deployment being benchmarked")]
        public string? DeploymentName { get; set; }

        [CommandOption("--benchmark-name")]
        [Description("Name for this benchmark run (auto-generated if not provided)")]
        public string? BenchmarkName { get; set; }

        [CommandOption("--type")]
        [Description("Benchmark type: Baseline, Load, or Stress")]
        [DefaultValue("Load")]
        public string BenchmarkType { get; set; } = "Load";

        [CommandOption("--concurrency")]
        [Description("Number of concurrent requests/users")]
        [DefaultValue(10)]
        public int Concurrency { get; set; } = 10;

        [CommandOption("--duration")]
        [Description("Benchmark duration in seconds")]
        [DefaultValue(300)]
        public int Duration { get; set; } = 300;

        [CommandOption("--warm-cache")]
        [Description("Warm up the cache before running benchmark")]
        [DefaultValue(true)]
        public bool WarmCache { get; set; } = true;

        [CommandOption("--auto-confirm")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool AutoConfirm { get; set; }

        [CommandOption("--verbose")]
        [Description("Show detailed output")]
        [DefaultValue(false)]
        public bool Verbose { get; set; }
    }
}
