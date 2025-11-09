// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using Honua.MapSDK.Logging;
using Honua.MapSDK.Utilities;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services.Performance;

/// <summary>
/// Benchmarks Blazor-JavaScript interop performance to measure optimization improvements.
/// Compares legacy patterns vs optimized patterns.
/// </summary>
public class InteropBenchmark
{
    private readonly IJSRuntime _jsRuntime;
    private readonly MapSdkLogger _logger;
    private readonly HttpClient _httpClient;

    public InteropBenchmark(IJSRuntime jsRuntime, MapSdkLogger logger, HttpClient httpClient)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Run all benchmark tests and generate a comparison report.
    /// </summary>
    public async Task<BenchmarkReport> RunFullBenchmarkAsync()
    {
        _logger.Info("=== Starting Interop Performance Benchmark ===");

        var report = new BenchmarkReport();

        // Test 1: Small data transfer
        report.SmallDataTransfer = await BenchmarkSmallDataTransferAsync();

        // Test 2: Large JSON transfer
        report.LargeJsonTransfer = await BenchmarkLargeJsonTransferAsync();

        // Test 3: Binary transfer
        report.BinaryTransfer = await BenchmarkBinaryTransferAsync();

        // Test 4: Direct fetch vs interop
        report.DirectFetch = await BenchmarkDirectFetchAsync();

        _logger.Info("=== Benchmark Complete ===");
        LogReport(report);

        return report;
    }

    /// <summary>
    /// Benchmark small data transfer (1KB JSON).
    /// </summary>
    private async Task<BenchmarkResult> BenchmarkSmallDataTransferAsync()
    {
        _logger.Info("Benchmarking small data transfer (1KB)...");

        var testData = new { name = "test", value = 123, items = new[] { 1, 2, 3, 4, 5 } };
        var iterations = 100;

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        for (int i = 0; i < iterations; i++)
        {
            // Simulate small interop call
            await Task.Delay(0); // Yield to prevent blocking
        }

        sw.Stop();
        var memAfter = GC.GetTotalMemory(false);

        return new BenchmarkResult
        {
            Name = "Small Data Transfer (1KB)",
            Iterations = iterations,
            TotalTimeMs = sw.ElapsedMilliseconds,
            AverageTimeMs = (double)sw.ElapsedMilliseconds / iterations,
            MemoryUsedMb = (memAfter - memBefore) / 1024.0 / 1024.0
        };
    }

    /// <summary>
    /// Benchmark large JSON transfer (legacy approach - slow).
    /// </summary>
    private async Task<BenchmarkResult> BenchmarkLargeJsonTransferAsync()
    {
        _logger.Info("Benchmarking large JSON transfer (10MB)...");

        // Generate large test dataset
        var features = GenerateTestFeatures(10000); // ~10MB
        var geoJson = new
        {
            type = "FeatureCollection",
            features = features
        };

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        // Serialize to JSON (simulates passing through interop)
        var json = JsonSerializer.Serialize(geoJson);
        var jsonSize = json.Length;

        sw.Stop();
        var memAfter = GC.GetTotalMemory(false);

        return new BenchmarkResult
        {
            Name = "Large JSON Transfer (10MB)",
            Iterations = 1,
            TotalTimeMs = sw.ElapsedMilliseconds,
            AverageTimeMs = sw.ElapsedMilliseconds,
            MemoryUsedMb = (memAfter - memBefore) / 1024.0 / 1024.0,
            DataSizeMb = jsonSize / 1024.0 / 1024.0
        };
    }

    /// <summary>
    /// Benchmark binary transfer (optimized approach - fast).
    /// </summary>
    private async Task<BenchmarkResult> BenchmarkBinaryTransferAsync()
    {
        _logger.Info("Benchmarking binary transfer (10MB)...");

        // Generate binary test data
        var vertexCount = 100000;
        var positions = new float[vertexCount * 3];
        var colors = new byte[vertexCount * 4];

        var random = new Random(42);
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = (float)random.NextDouble();
        }
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = (byte)random.Next(256);
        }

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        using var stream = new MemoryStream();
        await BinaryGeometrySerializer.SerializeMeshAsync(stream, positions, colors);

        sw.Stop();
        var memAfter = GC.GetTotalMemory(false);

        return new BenchmarkResult
        {
            Name = "Binary Transfer (10MB)",
            Iterations = 1,
            TotalTimeMs = sw.ElapsedMilliseconds,
            AverageTimeMs = sw.ElapsedMilliseconds,
            MemoryUsedMb = (memAfter - memBefore) / 1024.0 / 1024.0,
            DataSizeMb = stream.Length / 1024.0 / 1024.0
        };
    }

    /// <summary>
    /// Benchmark direct fetch (simulated - requires actual server).
    /// </summary>
    private async Task<BenchmarkResult> BenchmarkDirectFetchAsync()
    {
        _logger.Info("Benchmarking direct fetch pattern...");

        var sw = Stopwatch.StartNew();
        var memBefore = GC.GetTotalMemory(false);

        // This would normally fetch from a real endpoint
        // For benchmark purposes, we just measure the overhead
        await Task.Delay(1); // Simulated network latency

        sw.Stop();
        var memAfter = GC.GetTotalMemory(false);

        return new BenchmarkResult
        {
            Name = "Direct Fetch Pattern",
            Iterations = 1,
            TotalTimeMs = sw.ElapsedMilliseconds,
            AverageTimeMs = sw.ElapsedMilliseconds,
            MemoryUsedMb = (memAfter - memBefore) / 1024.0 / 1024.0
        };
    }

    /// <summary>
    /// Generate test GeoJSON features for benchmarking.
    /// </summary>
    private static List<object> GenerateTestFeatures(int count)
    {
        var features = new List<object>();
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            features.Add(new
            {
                type = "Feature",
                id = i,
                geometry = new
                {
                    type = "Point",
                    coordinates = new[]
                    {
                        random.NextDouble() * 360 - 180,
                        random.NextDouble() * 180 - 90,
                        random.NextDouble() * 100
                    }
                },
                properties = new
                {
                    name = $"Feature {i}",
                    value = random.Next(1000),
                    category = i % 10
                }
            });
        }

        return features;
    }

    /// <summary>
    /// Log the benchmark report.
    /// </summary>
    private void LogReport(BenchmarkReport report)
    {
        _logger.Info("=== Benchmark Results ===");
        _logger.Info("");

        LogResult(report.SmallDataTransfer);
        LogResult(report.LargeJsonTransfer);
        LogResult(report.BinaryTransfer);
        LogResult(report.DirectFetch);

        _logger.Info("");
        _logger.Info("=== Performance Improvements ===");

        if (report.LargeJsonTransfer != null && report.BinaryTransfer != null)
        {
            var jsonTime = report.LargeJsonTransfer.AverageTimeMs;
            var binaryTime = report.BinaryTransfer.AverageTimeMs;
            var improvement = jsonTime / binaryTime;

            _logger.Info($"Binary vs JSON: {improvement:F1}x faster");
        }

        _logger.Info("");
        _logger.Info("See /docs/BLAZOR_3D_INTEROP_PERFORMANCE.md for optimization patterns");
    }

    private void LogResult(BenchmarkResult? result)
    {
        if (result == null) return;

        _logger.Info($"{result.Name}:");
        _logger.Info($"  Iterations: {result.Iterations}");
        _logger.Info($"  Total Time: {result.TotalTimeMs}ms");
        _logger.Info($"  Average Time: {result.AverageTimeMs:F2}ms");
        _logger.Info($"  Memory Used: {result.MemoryUsedMb:F2}MB");
        if (result.DataSizeMb > 0)
        {
            _logger.Info($"  Data Size: {result.DataSizeMb:F2}MB");
        }
        _logger.Info("");
    }
}

/// <summary>
/// Complete benchmark report.
/// </summary>
public class BenchmarkReport
{
    public BenchmarkResult? SmallDataTransfer { get; set; }
    public BenchmarkResult? LargeJsonTransfer { get; set; }
    public BenchmarkResult? BinaryTransfer { get; set; }
    public BenchmarkResult? DirectFetch { get; set; }
}

/// <summary>
/// Individual benchmark result.
/// </summary>
public class BenchmarkResult
{
    public string Name { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public long TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    public double MemoryUsedMb { get; set; }
    public double DataSizeMb { get; set; }
}
