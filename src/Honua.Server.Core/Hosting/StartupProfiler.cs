// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Hosting;

/// <summary>
/// Profiles application startup time with detailed checkpoint tracking.
/// Helps identify performance bottlenecks during cold starts.
/// </summary>
/// <remarks>
/// Usage in Program.cs:
/// <code>
/// var profiler = new StartupProfiler();
/// profiler.Checkpoint("Builder created");
///
/// builder.ConfigureHonuaServices();
/// profiler.Checkpoint("Services configured");
///
/// var app = builder.Build();
/// profiler.Checkpoint("App built");
///
/// app.ConfigureHonuaRequestPipeline();
/// profiler.Checkpoint("Pipeline configured");
///
/// profiler.LogResults(app.Logger);
/// </code>
/// </remarks>
public sealed class StartupProfiler
{
    private readonly Stopwatch _stopwatch;
    private readonly ConcurrentBag<CheckpointTiming> _timings;
    private readonly Stopwatch _processStopwatch;

    /// <summary>
    /// Initializes a new startup profiler.
    /// </summary>
    public StartupProfiler()
    {
        _stopwatch = Stopwatch.StartNew();
        _timings = new ConcurrentBag<CheckpointTiming>();
        _processStopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records a checkpoint with the current elapsed time.
    /// </summary>
    /// <param name="name">The checkpoint name/description.</param>
    public void Checkpoint(string name)
    {
        var elapsed = _stopwatch.ElapsedMilliseconds;
        _timings.Add(new CheckpointTiming(name, elapsed));
    }

    /// <summary>
    /// Logs the profiling results to the specified logger.
    /// </summary>
    /// <param name="logger">The logger to write results to.</param>
    public void LogResults(ILogger logger)
    {
        _stopwatch.Stop();
        _processStopwatch.Stop();

        var orderedTimings = _timings.OrderBy(t => t.ElapsedMs).ToList();

        logger.LogInformation("=== Startup Performance Profile ===");
        logger.LogInformation("Total startup time: {TotalMs}ms", _stopwatch.ElapsedMilliseconds);
        logger.LogInformation("Process runtime: {ProcessMs}ms", _processStopwatch.ElapsedMilliseconds);

        if (orderedTimings.Count > 0)
        {
            logger.LogInformation("Checkpoint timings:");

            long previousTime = 0;
            foreach (var timing in orderedTimings)
            {
                var delta = timing.ElapsedMs - previousTime;
                logger.LogInformation(
                    "  [{ElapsedMs,6}ms] (+{Delta,5}ms) {Name}",
                    timing.ElapsedMs,
                    delta,
                    timing.Name);
                previousTime = timing.ElapsedMs;
            }
        }

        // Identify slowest checkpoints
        if (orderedTimings.Count >= 2)
        {
            var checkpointDeltas = new List<(string Name, long DeltaMs)>();
            for (int i = 1; i < orderedTimings.Count; i++)
            {
                var delta = orderedTimings[i].ElapsedMs - orderedTimings[i - 1].ElapsedMs;
                checkpointDeltas.Add((orderedTimings[i].Name, delta));
            }

            var slowest = checkpointDeltas.OrderByDescending(c => c.DeltaMs).Take(3).ToList();
            logger.LogInformation("Slowest operations:");
            foreach (var (name, deltaMs) in slowest)
            {
                logger.LogInformation("  {Name}: {DeltaMs}ms", name, deltaMs);
            }
        }

        logger.LogInformation("====================================");
    }

    /// <summary>
    /// Gets the current elapsed time in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets all recorded checkpoints.
    /// </summary>
    public IReadOnlyList<CheckpointTiming> Checkpoints => _timings.OrderBy(t => t.ElapsedMs).ToList();

    /// <summary>
    /// Represents a single checkpoint timing measurement.
    /// </summary>
    /// <param name="Name">The checkpoint name/description.</param>
    /// <param name="ElapsedMs">Elapsed milliseconds since profiler start.</param>
    public sealed record CheckpointTiming(string Name, long ElapsedMs);
}

/// <summary>
/// Hosted service that tracks and logs startup metrics.
/// </summary>
public sealed class StartupMetricsService
{
    private static readonly Stopwatch _startupTimer = Stopwatch.StartNew();
    private static bool _startupLogged = false;

    /// <summary>
    /// Records the startup completion time and logs it.
    /// Call this after app.Build() or when the app starts accepting requests.
    /// </summary>
    /// <param name="logger">The logger to write metrics to.</param>
    public static void RecordStartupComplete(ILogger logger)
    {
        if (_startupLogged)
        {
            return;
        }

        _startupTimer.Stop();
        _startupLogged = true;

        logger.LogInformation(
            "Application startup completed in {StartupTimeMs}ms",
            _startupTimer.ElapsedMilliseconds);

        // Log memory usage
        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        var workingSetMb = workingSet / 1024.0 / 1024.0;

        logger.LogInformation(
            "Memory usage at startup: {WorkingSetMb:F2} MB",
            workingSetMb);

        // Log GC stats
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        logger.LogDebug(
            "GC collections during startup: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}",
            gen0Collections,
            gen1Collections,
            gen2Collections);
    }

    /// <summary>
    /// Gets the elapsed startup time in milliseconds.
    /// </summary>
    public static long ElapsedMilliseconds => _startupTimer.ElapsedMilliseconds;
}
