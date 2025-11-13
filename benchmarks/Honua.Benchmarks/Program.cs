// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Columns;

namespace Honua.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            // Memory diagnostics - enables detection of memory leaks
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(new EventPipeProfiler(EventPipeProfile.GcVerbose))
            .AddDiagnoser(ThreadingDiagnoser.Default)
            // Export results in multiple formats
            .AddExporter(MarkdownExporter.GitHub)
            .AddExporter(JsonExporter.Full)
            .AddExporter(HtmlExporter.Default)
            // Add statistical columns including percentiles
            .AddColumn(StatisticColumn.P95)
            .AddColumn(new PercentileColumn(PercentileKind.P99))
            // Add memory allocation columns
            .AddColumn(new TagColumn("Memory Leak Check", name => "See Gen0-2 collections"));

        // Run all benchmarks if no specific benchmark is specified
        if (args.Length == 0)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
        else
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
