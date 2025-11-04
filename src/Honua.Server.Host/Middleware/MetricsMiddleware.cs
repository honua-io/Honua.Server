// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// High-performance metrics collection middleware using lock-free operations.
/// PERFORMANCE OPTIMIZED: Uses Interlocked operations and ConcurrentDictionary to eliminate lock contention.
/// </summary>
public sealed class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    // PERFORMANCE FIX: Use Interlocked operations for atomic counter updates (no locks needed)
    private long _totalRequests;
    private long _totalErrors;
    private long _totalDurationMs;

    // PERFORMANCE FIX: Use ConcurrentDictionary for thread-safe metric storage without locks
    private readonly ConcurrentDictionary<string, EndpointMetrics> _endpointMetrics = new();

    // Batch metrics writes to reduce overhead
    private const int MetricsBatchSize = 100;
    private long _requestsSinceLastBatch;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = Guard.NotNull(next);
        _logger = Guard.NotNull(logger);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var statusCode = 200;

        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            // PERFORMANCE FIX: Use Interlocked.Increment for lock-free atomic increment
            Interlocked.Increment(ref _totalErrors);
            statusCode = 500;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;

            // PERFORMANCE FIX: Use Interlocked operations for all counter updates
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Add(ref _totalDurationMs, durationMs);

            // Track per-endpoint metrics
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            var metrics = _endpointMetrics.GetOrAdd(endpoint, _ => new EndpointMetrics());

            // PERFORMANCE FIX: Update endpoint metrics using Interlocked operations
            Interlocked.Increment(ref metrics.RequestCount);
            Interlocked.Add(ref metrics.TotalDurationMs, durationMs);

            if (statusCode >= 400)
            {
                Interlocked.Increment(ref metrics.ErrorCount);
            }

            // PERFORMANCE FIX: Batch metric writes instead of per-request logging
            var requestCount = Interlocked.Increment(ref _requestsSinceLastBatch);
            if (requestCount >= MetricsBatchSize)
            {
                // Reset counter and log batch metrics
                Interlocked.Exchange(ref _requestsSinceLastBatch, 0);
                LogBatchMetrics();
            }
        }
    }

    /// <summary>
    /// Logs aggregated metrics for the current batch.
    /// PERFORMANCE: Only called every N requests to reduce logging overhead.
    /// </summary>
    private void LogBatchMetrics()
    {
        try
        {
            var totalReqs = Interlocked.Read(ref _totalRequests);
            var totalErrs = Interlocked.Read(ref _totalErrors);
            var totalDuration = Interlocked.Read(ref _totalDurationMs);

            var avgDuration = totalReqs > 0 ? totalDuration / (double)totalReqs : 0;
            var errorRate = totalReqs > 0 ? (totalErrs / (double)totalReqs) * 100 : 0;

            _logger.LogInformation(
                "Metrics Batch: {TotalRequests} requests, {AvgDuration:F2}ms avg, {ErrorRate:F2}% error rate",
                totalReqs,
                avgDuration,
                errorRate);
        }
        catch (Exception ex)
        {
            // Don't let metrics logging failures break the application
            _logger.LogWarning(ex, "Failed to log batch metrics");
        }
    }

    /// <summary>
    /// Gets current metrics snapshot (thread-safe, lock-free).
    /// </summary>
    public MetricsSnapshot GetMetrics()
    {
        var totalReqs = Interlocked.Read(ref _totalRequests);
        var totalErrs = Interlocked.Read(ref _totalErrors);
        var totalDuration = Interlocked.Read(ref _totalDurationMs);

        return new MetricsSnapshot
        {
            TotalRequests = totalReqs,
            TotalErrors = totalErrs,
            TotalDurationMs = totalDuration,
            AverageDurationMs = totalReqs > 0 ? totalDuration / (double)totalReqs : 0,
            ErrorRate = totalReqs > 0 ? (totalErrs / (double)totalReqs) * 100 : 0,
            EndpointCount = _endpointMetrics.Count
        };
    }

    /// <summary>
    /// Gets metrics for a specific endpoint (thread-safe, lock-free).
    /// </summary>
    public EndpointMetricsSnapshot? GetEndpointMetrics(string endpoint)
    {
        if (_endpointMetrics.TryGetValue(endpoint, out var metrics))
        {
            var count = Interlocked.Read(ref metrics.RequestCount);
            var duration = Interlocked.Read(ref metrics.TotalDurationMs);
            var errors = Interlocked.Read(ref metrics.ErrorCount);

            return new EndpointMetricsSnapshot
            {
                Endpoint = endpoint,
                RequestCount = count,
                TotalDurationMs = duration,
                AverageDurationMs = count > 0 ? duration / (double)count : 0,
                ErrorCount = errors,
                ErrorRate = count > 0 ? (errors / (double)count) * 100 : 0
            };
        }

        return null;
    }

    /// <summary>
    /// Thread-safe endpoint metrics container using Interlocked operations.
    /// </summary>
    private sealed class EndpointMetrics
    {
        public long RequestCount;
        public long TotalDurationMs;
        public long ErrorCount;
    }
}

/// <summary>
/// Immutable snapshot of overall metrics.
/// </summary>
public sealed class MetricsSnapshot
{
    public long TotalRequests { get; init; }
    public long TotalErrors { get; init; }
    public long TotalDurationMs { get; init; }
    public double AverageDurationMs { get; init; }
    public double ErrorRate { get; init; }
    public int EndpointCount { get; init; }
}

/// <summary>
/// Immutable snapshot of endpoint-specific metrics.
/// </summary>
public sealed class EndpointMetricsSnapshot
{
    public string Endpoint { get; init; } = string.Empty;
    public long RequestCount { get; init; }
    public long TotalDurationMs { get; init; }
    public double AverageDurationMs { get; init; }
    public long ErrorCount { get; init; }
    public double ErrorRate { get; init; }
}
