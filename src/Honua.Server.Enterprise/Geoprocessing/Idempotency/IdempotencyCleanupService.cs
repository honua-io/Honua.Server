// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing.Idempotency;

/// <summary>
/// Background service that periodically cleans up expired idempotency cache entries.
/// Runs every hour by default to remove entries past their 7-day TTL.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    // Configuration
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    public IdempotencyCleanupService(
        IIdempotencyService idempotencyService,
        ILogger<IdempotencyCleanupService> logger)
    {
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "IdempotencyCleanupService starting. Initial delay: {InitialDelay}, Cleanup interval: {Interval}",
            InitialDelay,
            CleanupInterval);

        // Wait before first cleanup to avoid startup load
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("IdempotencyCleanupService stopped during initial delay");
            return;
        }

        // Main cleanup loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Starting idempotency cache cleanup");

                // Get statistics before cleanup
                var statsBefore = await _idempotencyService.GetStatisticsAsync(tenantId: null, ct: stoppingToken);

                _logger.LogInformation(
                    "Idempotency cache statistics before cleanup - Total: {Total}, Expired: {Expired}, Size: {Size:F2} MB",
                    statsBefore.TotalEntries,
                    statsBefore.ExpiredEntries,
                    statsBefore.TotalSizeMB);

                // Perform cleanup
                var deletedCount = await _idempotencyService.CleanupExpiredEntriesAsync(stoppingToken);

                // Get statistics after cleanup
                var statsAfter = await _idempotencyService.GetStatisticsAsync(tenantId: null, ct: stoppingToken);

                _logger.LogInformation(
                    "Idempotency cache cleanup completed - Deleted: {Deleted}, Remaining: {Remaining}, Size: {Size:F2} MB, " +
                    "Expiring in 24h: {ExpiringIn24h}",
                    deletedCount,
                    statsAfter.TotalEntries,
                    statsAfter.TotalSizeMB,
                    statsAfter.ExpiringIn24Hours);

                // Warn if cache is growing too large
                if (statsAfter.TotalSizeMB > 1000) // 1 GB
                {
                    _logger.LogWarning(
                        "Idempotency cache size is large: {Size:F2} MB with {Count} entries. Consider reviewing TTL settings.",
                        statsAfter.TotalSizeMB,
                        statsAfter.TotalEntries);
                }

                // Wait before next cleanup
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("IdempotencyCleanupService stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during idempotency cache cleanup. Will retry in {RetryDelay}",
                    CleanupInterval);

                // Continue running even if cleanup fails
                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("IdempotencyCleanupService stopping during error recovery delay");
                    break;
                }
            }
        }

        _logger.LogInformation("IdempotencyCleanupService stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IdempotencyCleanupService stopping gracefully");

        // Perform one final cleanup before shutdown
        try
        {
            var deletedCount = await _idempotencyService.CleanupExpiredEntriesAsync(cancellationToken);
            _logger.LogInformation(
                "Final cleanup completed - Deleted {Count} expired entries",
                deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during final cleanup on shutdown");
        }

        await base.StopAsync(cancellationToken);
    }
}
