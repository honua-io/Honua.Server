// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.HealthChecks;

/// <summary>
/// Health check that triggers lazy service warmup on first invocation.
/// Useful for Kubernetes readiness probes to ensure services are initialized before receiving traffic.
/// </summary>
/// <remarks>
/// This health check serves two purposes:
/// 1. Triggers warmup of lazy-loaded services on first health check
/// 2. Reports degraded status if warmup is still in progress
/// 3. Reports healthy once all services are warmed up
///
/// Configure in Kubernetes:
/// <code>
/// readinessProbe:
///   httpGet:
///     path: /health/ready
///     port: 8080
///   initialDelaySeconds: 5
///   periodSeconds: 10
/// </code>
/// </remarks>
public sealed class WarmupHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IWarmupService> _warmupServices;
    private readonly ILogger<WarmupHealthCheck> _logger;
    private int _warmupCompleted = 0; // 0 = not started, 1 = in progress, 2 = completed

    public WarmupHealthCheck(
        IEnumerable<IWarmupService> warmupServices,
        ILogger<WarmupHealthCheck> logger)
    {
        _warmupServices = warmupServices ?? throw new ArgumentNullException(nameof(warmupServices));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var warmupStatus = Interlocked.CompareExchange(ref _warmupCompleted, 1, 0);

        // First invocation - trigger warmup
        if (warmupStatus == 0)
        {
            _logger.LogInformation("First health check - triggering service warmup");

            // Don't block health check - run warmup in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await WarmupServicesAsync(cancellationToken);
                    Interlocked.Exchange(ref _warmupCompleted, 2);
                    _logger.LogInformation("Service warmup completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Service warmup encountered errors but will continue");
                    Interlocked.Exchange(ref _warmupCompleted, 2); // Mark as completed anyway
                }
            }, cancellationToken);

            return Task.FromResult(HealthCheckResult.Degraded(
                "Service warmup in progress",
                data: new Dictionary<string, object>
                {
                    ["warmupStatus"] = "in_progress"
                }));
        }

        // Warmup in progress
        if (warmupStatus == 1)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Service warmup in progress",
                data: new Dictionary<string, object>
                {
                    ["warmupStatus"] = "in_progress"
                }));
        }

        // Warmup completed
        return Task.FromResult(HealthCheckResult.Healthy(
            "All services warmed up",
            data: new Dictionary<string, object>
            {
                ["warmupStatus"] = "completed"
            }));
    }

    private async Task WarmupServicesAsync(CancellationToken cancellationToken)
    {
        var services = _warmupServices.ToList();
        if (services.Count == 0)
        {
            _logger.LogDebug("No warmup services registered");
            return;
        }

        _logger.LogInformation("Warming up {Count} services", services.Count);

        foreach (var service in services)
        {
            try
            {
                await service.WarmupAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to warm up service {ServiceType}: {Message}",
                    service.GetType().Name,
                    ex.Message);
            }
        }
    }
}

/// <summary>
/// Interface for services that support warmup operations.
/// Implement this interface to allow services to be warmed up during health checks.
/// </summary>
public interface IWarmupService
{
    /// <summary>
    /// Performs warmup operations for this service.
    /// This method is called once during the first health check.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation.</param>
    Task WarmupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Example warmup service that pre-loads metadata cache.
/// </summary>
public sealed class MetadataCacheWarmupService : IWarmupService
{
    private readonly Metadata.IMetadataRegistry _metadataRegistry;
    private readonly ILogger<MetadataCacheWarmupService> _logger;

    public MetadataCacheWarmupService(
        Metadata.IMetadataRegistry metadataRegistry,
        ILogger<MetadataCacheWarmupService> logger)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Warming up metadata cache");

            // Ensure metadata is loaded into cache
            await _metadataRegistry.EnsureInitializedAsync(cancellationToken);

            // Pre-load snapshot to warm up cache
            _ = await _metadataRegistry.GetSnapshotAsync(cancellationToken);

            _logger.LogDebug("Metadata cache warmed up successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm up metadata cache: {Message}", ex.Message);
            throw;
        }
    }
}
