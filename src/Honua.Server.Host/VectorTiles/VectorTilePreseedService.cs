// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Store for active vector tile preseed jobs.
/// </summary>
internal sealed class ActiveVectorPreseedJobStore : ActiveJobStore<VectorTilePreseedJob>
{
    protected override Guid GetJobId(VectorTilePreseedJob job) => job.JobId;
}

/// <summary>
/// Store for completed vector tile preseed job snapshots.
/// </summary>
internal sealed class CompletedVectorPreseedJobStore : CompletedJobStore<VectorTilePreseedJobSnapshot>
{
    public CompletedVectorPreseedJobStore() : base(maxCompletedJobs: 100)
    {
    }

    protected override Guid GetJobId(VectorTilePreseedJobSnapshot snapshot) => snapshot.JobId;
}

/// <summary>
/// Background service for preseeding vector tile cache with resource exhaustion protection.
/// </summary>
public sealed class VectorTilePreseedService : BackgroundService, IVectorTilePreseedService
{
    private const int QueueCapacity = 32;
    private const int MaxParallelTiles = 4;

    private readonly Channel<VectorTilePreseedJob> queue;
    private readonly ActiveVectorPreseedJobStore _activeJobs = new();
    private readonly CompletedVectorPreseedJobStore _completedJobs = new();
    private readonly IMemoryCache userRateLimits;
    private readonly IFeatureRepository featureRepository;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly ILogger<VectorTilePreseedService> logger;
    private readonly VectorTilePreseedLimits limits;

    public VectorTilePreseedService(
        IFeatureRepository featureRepository,
        IMetadataRegistry metadataRegistry,
        ILogger<VectorTilePreseedService> logger,
        IOptions<VectorTilePreseedLimits> limits)
    {
        this.featureRepository = Guard.NotNull(featureRepository);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.logger = Guard.NotNull(logger);
        this.limits = Guard.NotNull(limits?.Value);

        this.queue = Channel.CreateBounded<VectorTilePreseedJob>(new BoundedChannelOptions(QueueCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Initialize MemoryCache with automatic expiration for rate limits
        this.userRateLimits = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 10000 // Limit to 10,000 rate limit entries
        });
    }

    public async Task<VectorTilePreseedJobSnapshot> EnqueueAsync(VectorTilePreseedRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        request.EnsureValid();

        // Enforce max zoom level
        if (request.MaxZoom > this.limits.MaxZoomLevel)
        {
            throw new InvalidOperationException(
                $"Maximum zoom level ({request.MaxZoom}) exceeds allowed limit ({this.limits.MaxZoomLevel}). " +
                $"Higher zoom levels generate exponentially more tiles and can cause resource exhaustion.");
        }

        // Calculate total tiles before creating the job
        var totalTiles = CalculateTotalTilesWithLimit(request.MinZoom, request.MaxZoom);

        // Check concurrent job limit
        var activeJobCount = await this._activeJobs.CountAsync(cancellationToken).ConfigureAwait(false);
        if (activeJobCount >= this.limits.MaxConcurrentJobs)
        {
            throw new InvalidOperationException(
                $"Maximum concurrent jobs ({this.limits.MaxConcurrentJobs}) reached. " +
                $"Please wait for existing jobs to complete or cancel them.");
        }

        // Check per-user job limit
        var userKey = $"{request.ServiceId}/{request.LayerId}";
        var allActiveJobs = await this._activeJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var userJobCount = allActiveJobs.Count(j =>
            j.Request.ServiceId == request.ServiceId &&
            j.Request.LayerId == request.LayerId);

        if (userJobCount >= this.limits.MaxJobsPerUser)
        {
            throw new InvalidOperationException(
                $"Maximum jobs per service/layer ({this.limits.MaxJobsPerUser}) reached for {userKey}. " +
                $"Please wait for existing jobs to complete or cancel them.");
        }

        // Rate limiting check with auto-expiring cache entries
        var now = DateTimeOffset.UtcNow;
        if (this.userRateLimits.TryGetValue(userKey, out DateTimeOffset lastSubmission))
        {
            var timeSinceLastSubmission = now - lastSubmission;
            if (timeSinceLastSubmission < this.limits.RateLimitWindow)
            {
                var waitTime = this.limits.RateLimitWindow - timeSinceLastSubmission;
                throw new InvalidOperationException(
                    $"Rate limit exceeded for {userKey}. " +
                    $"Please wait {waitTime.TotalSeconds:F0} seconds before submitting another job.");
            }
        }

        // Set rate limit with automatic expiration after the rate limit window
        this.userRateLimits.Set(userKey, now, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = this.limits.RateLimitWindow,
            Size = 1
        });

        var job = new VectorTilePreseedJob(Guid.NewGuid(), request);
        await this._activeJobs.PutAsync(job, cancellationToken).ConfigureAwait(false);

        await this.queue.Writer.WriteAsync(job, cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation(
            "Enqueued vector tile preseed job {JobId} for {ServiceId}/{LayerId} z{MinZoom}-{MaxZoom} ({TotalTiles:N0} tiles)",
            job.JobId, request.ServiceId, request.LayerId, request.MinZoom, request.MaxZoom, totalTiles);

        return VectorTilePreseedJobSnapshot.FromJob(job);
    }

    public async Task<VectorTilePreseedJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var activeJob = await this._activeJobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (activeJob is not null)
        {
            return VectorTilePreseedJobSnapshot.FromJob(activeJob);
        }

        var completedJob = await this._completedJobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (completedJob is not null)
        {
            return completedJob;
        }

        return null;
    }

    public async Task<IReadOnlyList<VectorTilePreseedJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = await this._activeJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var completedJobs = await this._completedJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return activeJobs.Select(VectorTilePreseedJobSnapshot.FromJob)
            .Concat(completedJobs)
            .OrderByDescending(s => s.CreatedUtc)
            .ToArray();
    }

    public async Task<VectorTilePreseedJobSnapshot?> CancelAsync(Guid jobId, string? reason = null)
    {
        var job = await this._activeJobs.GetAsync(jobId).ConfigureAwait(false);
        if (job is not null)
        {
            if (job.TryCancel())
            {
                this.logger.LogInformation("Cancelled vector tile preseed job {JobId}: {Reason}", jobId, reason ?? "User requested");
            }

            return VectorTilePreseedJobSnapshot.FromJob(job);
        }

        // If job already completed/cancelled, return the completed snapshot (if any)
        return await this._completedJobs.GetAsync(jobId).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("Vector tile preseed service started. MaxParallelTiles={MaxParallelTiles}, QueueCapacity={QueueCapacity}",
            MaxParallelTiles,
            QueueCapacity);

        await foreach (var job in this.queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                this.logger.LogInformation("Vector tile preseed service stopping gracefully");
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Unexpected error processing preseed job. JobId={JobId}, ServiceId={ServiceId}, LayerId={LayerId}",
                    job.JobId,
                    job.Request.ServiceId,
                    job.Request.LayerId);
                job.MarkFailed($"Unexpected error: {ex.Message}");
                await MoveToCompletedAsync(job, stoppingToken).ConfigureAwait(false);
            }
        }

        var activeJobsCount = await this._activeJobs.CountAsync(stoppingToken).ConfigureAwait(false);
        var completedJobsCount = await this._completedJobs.CountAsync(stoppingToken).ConfigureAwait(false);
        this.logger.LogInformation("Vector tile preseed service stopped. ActiveJobs={ActiveJobs}, CompletedJobs={CompletedJobs}",
            activeJobsCount,
            completedJobsCount);
    }

    private async Task ProcessJobAsync(VectorTilePreseedJob job, CancellationToken stoppingToken)
    {
        var request = job.Request;

        try
        {
            this.logger.LogDebug(
                "Processing preseed job. JobId={JobId}, ServiceId={ServiceId}, LayerId={LayerId}, ZoomRange={MinZoom}-{MaxZoom}",
                job.JobId,
                request.ServiceId,
                request.LayerId,
                request.MinZoom,
                request.MaxZoom);

            // Ensure metadata is loaded
            await this.metadataRegistry.EnsureInitializedAsync(stoppingToken).ConfigureAwait(false);

            // Get layer metadata to determine extent
            var snapshot = await this.metadataRegistry.GetSnapshotAsync(stoppingToken).ConfigureAwait(false);
            if (snapshot is null || !snapshot.TryGetLayer(request.ServiceId, request.LayerId, out var layer))
            {
                var errorMsg = $"Layer {request.ServiceId}/{request.LayerId} not found in metadata";
                job.MarkFailed(errorMsg);
                this.logger.LogWarning(
                    "Preseed job failed: Layer not found. JobId={JobId}, ServiceId={ServiceId}, LayerId={LayerId}",
                    job.JobId,
                    request.ServiceId,
                    request.LayerId);
                return;
            }

            // Calculate total tiles based on extent
            var totalTiles = CalculateTotalTilesForExtent(layer, request.MinZoom, request.MaxZoom);

            // Start the job (this creates the CancellationTokenSource in the job)
            job.MarkStarted(totalTiles);

            // NOW create the linked cancellation token AFTER job.MarkStarted()
            // so it includes the job's CancellationTokenSource
            using var timeoutCts = new CancellationTokenSource(this.limits.JobTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                timeoutCts.Token,
                job.CancellationTokenSource?.Token ?? CancellationToken.None);

            var cancellationToken = linkedCts.Token;

            this.logger.LogInformation(
                "Starting preseed job {JobId}: {TotalTiles:N0} tiles for {ServiceId}/{LayerId} (timeout: {Timeout})",
                job.JobId, totalTiles, request.ServiceId, request.LayerId, this.limits.JobTimeout);

            // Generate tiles for each zoom level within the layer's extent
            for (var zoom = request.MinZoom; zoom <= request.MaxZoom; zoom++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                job.UpdateStage($"Generating zoom {zoom}");

                // Get tile range for this zoom level based on layer extent
                var (minRow, maxRow, minColumn, maxColumn) = GetTileRangeForLayer(layer, zoom);

                if (minRow > maxRow || minColumn > maxColumn)
                {
                    // No tiles in this extent at this zoom
                    this.logger.LogDebug("No tiles in extent for {ServiceId}/{LayerId} at zoom {Zoom}",
                        request.ServiceId, request.LayerId, zoom);
                    continue;
                }

                var tasks = new List<Task>();
                for (var row = minRow; row <= maxRow; row++)
                {
                    for (var column = minColumn; column <= maxColumn; column++)
                    {
                        var tileRow = row;
                        var tileColumn = column;

                        // Throttle parallel tile generation
                        while (tasks.Count >= MaxParallelTiles)
                        {
                            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                            tasks.Remove(completed);
                            await completed.ConfigureAwait(false); // Propagate exceptions
                        }

                        // Note: MVT uses (x, y) while OGC Tiles uses (row, column)
                        // For Web Mercator: x=column, y=row
                        var task = GenerateTileAsync(job, zoom, tileColumn, tileRow, cancellationToken);
                        tasks.Add(task);
                    }
                }

                // Wait for remaining tiles at this zoom level
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            job.MarkCompleted();
            this.logger.LogInformation("Completed preseed job {JobId}: {TilesProcessed}/{TilesTotal} tiles",
                job.JobId, job.TilesProcessed, job.TilesTotal);
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled();
            this.logger.LogInformation(
                "Preseed job cancelled. JobId={JobId}, ServiceId={ServiceId}, LayerId={LayerId}, Progress={Progress}/{Total}",
                job.JobId,
                request.ServiceId,
                request.LayerId,
                job.TilesProcessed,
                job.TilesTotal);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            this.logger.LogError(
                ex,
                "Preseed job failed. JobId={JobId}, ServiceId={ServiceId}, LayerId={LayerId}, Progress={Progress}/{Total}",
                job.JobId,
                request.ServiceId,
                request.LayerId,
                job.TilesProcessed,
                job.TilesTotal);
        }
        finally
        {
            await MoveToCompletedAsync(job, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task GenerateTileAsync(VectorTilePreseedJob job, int zoom, int x, int y, CancellationToken cancellationToken)
    {
        try
        {
            // Generate tile via FeatureRepository (which will cache it)
            await this.featureRepository.GenerateMvtTileAsync(
                job.Request.ServiceId,
                job.Request.LayerId,
                zoom,
                x,
                y,
                job.Request.Datetime,
                cancellationToken).ConfigureAwait(false);

            job.IncrementProgress();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Job was cancelled - propagate to stop tile generation
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "Failed to generate tile. ServiceId={ServiceId}, LayerId={LayerId}, Zoom={Zoom}, X={X}, Y={Y}. Continuing with remaining tiles.",
                job.Request.ServiceId,
                job.Request.LayerId,
                zoom,
                x,
                y);
            // Continue with other tiles even if one fails
            job.IncrementProgress();
        }
    }

    /// <summary>
    /// Calculates total tiles with limit enforcement to prevent resource exhaustion.
    /// </summary>
    private long CalculateTotalTilesWithLimit(int minZoom, int maxZoom)
    {
        long total = 0;
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var tilesAtZoom = 1L << zoom; // 2^zoom
            var tilesAtThisZoom = tilesAtZoom * tilesAtZoom; // tiles in both dimensions
            total += tilesAtThisZoom;

            // Check if we've exceeded the limit
            if (total > this.limits.MaxTilesPerJob)
            {
                throw new InvalidOperationException(
                    $"Requested tile count exceeds maximum allowed ({this.limits.MaxTilesPerJob:N0} tiles). " +
                    $"At zoom {zoom}, the total would be {total:N0} tiles. " +
                    $"Please reduce the zoom range or use a smaller area.");
            }
        }
        return total;
    }

    private static long CalculateTotalTiles(int minZoom, int maxZoom)
    {
        long total = 0;
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var tilesAtZoom = 1L << zoom; // 2^zoom
            total += tilesAtZoom * tilesAtZoom; // tiles in both dimensions
        }
        return total;
    }

    /// <summary>
    /// Calculates total tiles for a layer's extent across the specified zoom range.
    /// </summary>
    private long CalculateTotalTilesForExtent(LayerDefinition layer, int minZoom, int maxZoom)
    {
        long total = 0;
        for (var zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var (minRow, maxRow, minColumn, maxColumn) = GetTileRangeForLayer(layer, zoom);
            if (minRow <= maxRow && minColumn <= maxColumn)
            {
                var rows = maxRow - minRow + 1;
                var columns = maxColumn - minColumn + 1;
                total += rows * columns;
            }
        }

        // If no extent is defined, fall back to global calculation but enforce limits
        if (total == 0)
        {
            total = CalculateTotalTilesWithLimit(minZoom, maxZoom);
        }

        return total;
    }

    /// <summary>
    /// Gets the tile range for a layer at a specific zoom level based on its spatial extent.
    /// Assumes Web Mercator (EPSG:3857) for MVT tiles.
    /// </summary>
    private static (int MinRow, int MaxRow, int MinColumn, int MaxColumn) GetTileRangeForLayer(LayerDefinition layer, int zoom)
    {
        // Check if the layer has a spatial extent defined
        if (layer.Extent?.Bbox is not { Count: > 0 })
        {
            // No extent defined, use global tile matrix
            var maxIndex = (1 << zoom) - 1;
            return (0, maxIndex, 0, maxIndex);
        }

        // Use the first bbox (primary extent)
        var bbox = layer.Extent.Bbox[0];
        if (bbox is not { Length: >= 4 })
        {
            var maxIndex = (1 << zoom) - 1;
            return (0, maxIndex, 0, maxIndex);
        }

        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox[2];
        var maxY = bbox[3];

        // Determine the CRS of the bbox
        var bboxCrs = layer.Extent.Crs ?? "EPSG:4326";

        // For MVT, we use Web Mercator tile matrix
        // If bbox is in WGS84, we need to use the appropriate calculation
        var tileMatrixSetId = bboxCrs.Contains("3857", StringComparison.OrdinalIgnoreCase)
            ? OgcTileMatrixHelper.WorldWebMercatorQuadId
            : OgcTileMatrixHelper.WorldCrs84QuadId;

        return OgcTileMatrixHelper.GetTileRange(tileMatrixSetId, zoom, minX, minY, maxX, maxY);
    }

    private async Task MoveToCompletedAsync(VectorTilePreseedJob job, CancellationToken cancellationToken = default)
    {
        await this._activeJobs.DeleteAsync(job.JobId, cancellationToken).ConfigureAwait(false);

        var snapshot = VectorTilePreseedJobSnapshot.FromJob(job);
        await this._completedJobs.RecordCompletionAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the service and ensures graceful shutdown of the channel.
    /// Thread-safe: Prevents channel disposal race condition by completing writer before disposal.
    /// </summary>
    public override void Dispose()
    {
        try
        {
            // Signal no more writes to the channel to allow graceful drain
            this.queue.Writer.Complete();
        }
        catch (Exception)
        {
            // Channel may already be completed, ignore
        }

        // Dispose of the rate limit cache
        this.userRateLimits?.Dispose();

        base.Dispose();
    }
}
