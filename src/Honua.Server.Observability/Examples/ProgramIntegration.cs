// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Observability;
using Honua.Server.Observability.Metrics;
using Honua.Server.Observability.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Honua.Server.Observability.Examples;

/// <summary>
/// Example Program.cs showing how to integrate Honua observability.
/// </summary>
public class ProgramIntegration
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ============================================================
        // 1. Configure Serilog for structured logging
        // ============================================================
        builder.Logging.AddHonuaSerilog(
            serviceName: "Honua.Server",
            minimumLevel: LogEventLevel.Information
        );

        // ============================================================
        // 2. Add Honua observability services
        // ============================================================
        builder.Services.AddHonuaObservability(
            serviceName: "Honua.Server",
            serviceVersion: "1.0.0",
            connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
        );

        // Add other services
        builder.Services.AddControllers();
        // builder.Services.AddEndpointsApiExplorer();
        // builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // ============================================================
        // 3. Configure middleware pipeline
        // ============================================================

        // IMPORTANT: Add correlation ID and metrics middleware FIRST
        // This ensures all requests are tracked with correlation IDs
        app.UseHonuaMetrics();

        // Add health check endpoints
        app.UseHonuaHealthChecks();

        // Add Prometheus metrics endpoint
        app.UsePrometheusMetrics();

        // Standard ASP.NET Core middleware
        // if (app.Environment.IsDevelopment())
        // {
        //     app.UseSwagger();
        //     app.UseSwaggerUI();
        // }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}

/// <summary>
/// Example of using build queue metrics in a service.
/// </summary>
public class BuildService
{
    private readonly BuildQueueMetrics _metrics;
    private readonly ILogger<BuildService> _logger;

    public BuildService(BuildQueueMetrics metrics, ILogger<BuildService> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task EnqueueBuildAsync(string tier, string architecture)
    {
        _logger.LogInformation("Enqueueing build for tier={Tier}, architecture={Architecture}",
            tier, architecture);

        // Record the build being enqueued
        _metrics.RecordBuildEnqueued(tier, architecture);

        // ... actual enqueueing logic ...

        // Update queue depth
        var currentDepth = await GetCurrentQueueDepthAsync();
        _metrics.UpdateQueueDepth(currentDepth);
    }

    public async Task ProcessBuildAsync(string buildId, string tier)
    {
        var startTime = DateTime.UtcNow;
        var queueWaitTime = await GetQueueWaitTimeAsync(buildId);

        _logger.LogInformation("Starting build {BuildId} for tier {Tier}", buildId, tier);

        // Record queue wait time
        _metrics.RecordQueueWaitTime(tier, queueWaitTime);

        try
        {
            // Execute the build
            var result = await ExecuteBuildAsync(buildId, tier);

            var duration = DateTime.UtcNow - startTime;

            // Record successful build
            _metrics.RecordBuildCompleted(
                tier: tier,
                success: true,
                fromCache: result.FromCache,
                duration: duration
            );

            _logger.LogInformation(
                "Build {BuildId} completed successfully in {Duration}ms, FromCache={FromCache}",
                buildId, duration.TotalMilliseconds, result.FromCache);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            // Record failed build
            _metrics.RecordBuildCompleted(
                tier: tier,
                success: false,
                fromCache: false,
                duration: duration,
                errorType: ex.GetType().Name
            );

            _logger.LogError(ex, "Build {BuildId} failed after {Duration}ms",
                buildId, duration.TotalMilliseconds);

            throw;
        }
        finally
        {
            // Update queue depth after processing
            var currentDepth = await GetCurrentQueueDepthAsync();
            _metrics.UpdateQueueDepth(currentDepth);
        }
    }

    private Task<int> GetCurrentQueueDepthAsync() => Task.FromResult(0);
    private Task<TimeSpan> GetQueueWaitTimeAsync(string buildId) => Task.FromResult(TimeSpan.Zero);
    private Task<BuildResult> ExecuteBuildAsync(string buildId, string tier) =>
        Task.FromResult(new BuildResult { FromCache = false });

    public class BuildResult
    {
        public bool FromCache { get; set; }
    }
}

/// <summary>
/// Example of using cache metrics in a service.
/// </summary>
public class CacheService
{
    private readonly CacheMetrics _metrics;
    private readonly ILogger<CacheService> _logger;

    public CacheService(CacheMetrics metrics, ILogger<CacheService> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<CachedBuild?> GetCachedBuildAsync(string cacheKey, string tier, string architecture)
    {
        _logger.LogDebug("Looking up cache key {CacheKey}", cacheKey);

        var result = await LookupInCacheAsync(cacheKey);
        var hit = result != null;

        // Record cache lookup
        _metrics.RecordCacheLookup(hit, tier, architecture);

        if (hit)
        {
            _logger.LogInformation("Cache hit for key {CacheKey}", cacheKey);

            // Record time saved by cache hit
            var estimatedBuildTime = TimeSpan.FromMinutes(5);
            _metrics.RecordCacheSavings(estimatedBuildTime, tier);
        }
        else
        {
            _logger.LogInformation("Cache miss for key {CacheKey}", cacheKey);
        }

        return result;
    }

    public async Task UpdateCacheStatisticsAsync()
    {
        var cacheSize = await GetCacheEntriesCountAsync();
        _metrics.UpdateCacheEntryCount(cacheSize);
    }

    private Task<CachedBuild?> LookupInCacheAsync(string key) => Task.FromResult<CachedBuild?>(null);
    private Task<long> GetCacheEntriesCountAsync() => Task.FromResult(0L);

    public class CachedBuild { }
}

/// <summary>
/// Example of using distributed tracing with activities.
/// </summary>
public class BuildServiceWithTracing
{
    private readonly BuildQueueMetrics _metrics;
    private readonly ILogger<BuildServiceWithTracing> _logger;

    public BuildServiceWithTracing(BuildQueueMetrics metrics, ILogger<BuildServiceWithTracing> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task ProcessBuildWithTracingAsync(string buildId, string tier)
    {
        // Create a custom span for the entire build operation
        using var activity = Tracing.ActivityExtensions.StartBuildActivity(
            "ProcessBuild",
            buildId: buildId,
            tier: tier
        );

        var startTime = DateTime.UtcNow;

        try
        {
            activity?.AddEvent(new System.Diagnostics.ActivityEvent("BuildStarted"));

            // Step 1: Prepare build environment
            using (var prepareActivity = ActivityExtensions.StartBuildActivity(
                "PrepareBuildEnvironment",
                buildId: buildId,
                tier: tier
            ))
            {
                await PrepareBuildEnvironmentAsync();
                prepareActivity?.SetSuccess();
            }

            // Step 2: Execute build
            using (var executeActivity = ActivityExtensions.StartBuildActivity(
                "ExecuteBuild",
                buildId: buildId,
                tier: tier
            ))
            {
                await ExecuteBuildAsync();
                executeActivity?.SetSuccess();
            }

            // Step 3: Upload artifacts
            using (var uploadActivity = ActivityExtensions.StartBuildActivity(
                "UploadArtifacts",
                buildId: buildId,
                tier: tier
            ))
            {
                await UploadArtifactsAsync();
                uploadActivity?.SetSuccess();
            }

            var duration = DateTime.UtcNow - startTime;
            activity?.AddEvent(new System.Diagnostics.ActivityEvent("BuildCompleted"));
            activity?.SetSuccess();

            // Record metrics
            _metrics.RecordBuildCompleted(tier, true, false, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            activity?.RecordException(ex);

            // Record metrics
            _metrics.RecordBuildCompleted(tier, false, false, duration, ex.GetType().Name);

            throw;
        }
    }

    private Task PrepareBuildEnvironmentAsync() => Task.CompletedTask;
    private Task ExecuteBuildAsync() => Task.CompletedTask;
    private Task UploadArtifactsAsync() => Task.CompletedTask;
}
