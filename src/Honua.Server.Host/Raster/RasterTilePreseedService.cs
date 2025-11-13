// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using Honua.Server.Host.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.Raster;

/// <summary>
/// Store for active raster tile preseed jobs.
/// </summary>
internal sealed class ActiveRasterPreseedJobStore : ActiveJobStore<RasterTilePreseedJob>
{
    protected override Guid GetJobId(RasterTilePreseedJob job) => job.JobId;
}

/// <summary>
/// Store for completed raster tile preseed job snapshots.
/// </summary>
internal sealed class CompletedRasterPreseedJobStore : CompletedJobStore<RasterTilePreseedJobSnapshot>
{
    public CompletedRasterPreseedJobStore() : base(maxCompletedJobs: 100)
    {
    }

    protected override Guid GetJobId(RasterTilePreseedJobSnapshot snapshot) => snapshot.JobId;
}

public interface IRasterTilePreseedService
{
    Task<RasterTilePreseedJobSnapshot> EnqueueAsync(RasterTilePreseedRequest request, CancellationToken cancellationToken = default);
    Task<RasterTilePreseedJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RasterTilePreseedJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);
    Task<RasterTilePreseedJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
    Task<RasterTileCachePurgeResult> PurgeAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default);
}

public sealed class RasterTilePreseedService : BackgroundService, IRasterTilePreseedService
{
    private const int QueueCapacity = 32;
    private const int OverlayCacheSizeLimit = 32;
    private const int MaxParallelismCap = 16;
    private const int WorkerBufferMultiplier = 4;

    private readonly Channel<RasterTilePreseedWorkItem> queue;
    private readonly ActiveRasterPreseedJobStore _jobs = new();
    private readonly CompletedRasterPreseedJobStore _completedJobs = new();
    private readonly IRasterDatasetRegistry rasterRegistry;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IFeatureRepository featureRepository;
    private readonly IRasterRenderer rasterRenderer;
    private readonly IRasterTileCacheProvider cacheProvider;
    private readonly IRasterTileCacheMetrics metrics;
    private readonly ILogger<RasterTilePreseedService> logger;
    private readonly int maxParallelism;

    public RasterTilePreseedService(
        IRasterDatasetRegistry rasterRegistry,
        IMetadataRegistry metadataRegistry,
        IFeatureRepository featureRepository,
        IRasterRenderer rasterRenderer,
        IRasterTileCacheProvider cacheProvider,
        IRasterTileCacheMetrics metrics,
        ILogger<RasterTilePreseedService> logger)
    {
        this.rasterRegistry = Guard.NotNull(rasterRegistry);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.featureRepository = Guard.NotNull(featureRepository);
        this.rasterRenderer = Guard.NotNull(rasterRenderer);
        this.cacheProvider = Guard.NotNull(cacheProvider);
        this.metrics = Guard.NotNull(metrics);
        this.logger = Guard.NotNull(logger);
        this.maxParallelism = Math.Clamp(Environment.ProcessorCount, 1, MaxParallelismCap);

        this.queue = Channel.CreateBounded<RasterTilePreseedWorkItem>(new BoundedChannelOptions(QueueCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task<IReadOnlyList<RasterTilePreseedJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = await this.jobs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var completedJobs = await this.completedJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return activeJobs.Select(job => job.Snapshot)
            .Concat(completedJobs)
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .ToArray();
    }

    public async Task<RasterTilePreseedJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var activeJob = await this.jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (activeJob is not null)
        {
            return activeJob.Snapshot;
        }

        var completedJob = await this.completedJobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (completedJob is not null)
        {
            return completedJob;
        }

        return null;
    }

    public async Task<RasterTilePreseedJobSnapshot?> CancelAsync(Guid jobId, string? reason = null)
    {
        var job = await this.jobs.GetAsync(jobId).ConfigureAwait(false);
        if (job is not null)
        {
            job.RequestCancellation(reason);
            return job.Snapshot;
        }

        var snapshot = await this.completedJobs.GetAsync(jobId).ConfigureAwait(false);
        if (snapshot is not null)
        {
            return snapshot;
        }

        return null;
    }

    public async Task<RasterTileCachePurgeResult> PurgeAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(datasetIds);

        var normalized = datasetIds
            .Select(id => id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return RasterTileCachePurgeResult.Empty;
        }

        var purged = new List<string>(normalized.Length);
        var failures = new List<string>();

        foreach (var datasetId in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await this.cacheProvider.PurgeDatasetAsync(datasetId!, cancellationToken).ConfigureAwait(false);
                purged.Add(datasetId!);
            }
            catch (Exception ex)
            {
                failures.Add(datasetId!);
                this.logger.LogWarning(ex, "Failed to purge raster tile cache for dataset {DatasetId}.", datasetId);
            }
        }

        return new RasterTileCachePurgeResult(purged, failures);
    }

    public async Task<RasterTilePreseedJobSnapshot> EnqueueAsync(RasterTilePreseedRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        request.EnsureValid();

        var job = new RasterTilePreseedJob(request);
        var registered = await this.jobs.RegisterAsync(job, cancellationToken).ConfigureAwait(false);
        if (!registered)
        {
            throw new InvalidOperationException($"Failed to register raster tile preseed job {job.JobId}.");
        }

        var workItem = new RasterTilePreseedWorkItem(request, job);
        await this.queue.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
        return job.Snapshot;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in this.queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var job = workItem.Job;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Token);
            var jobToken = linkedCts.Token;

            if (jobToken.IsCancellationRequested)
            {
                job.MarkCancelled("Cancelled before start.");
                continue;
            }

            try
            {
                await ProcessWorkItemAsync(workItem, jobToken).ConfigureAwait(false);
                job.MarkCompleted($"Seeded {job.Snapshot.TilesCompleted} tiles.");
            }
            catch (OperationCanceledException)
            {
                job.MarkCancelled("Preseed cancelled.");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Raster tile preseed job {JobId} failed.", job.JobId);
                job.MarkFailed(ex.Message);
            }
            finally
            {
                var snapshot = job.Snapshot;
                if (snapshot.Status is RasterTilePreseedJobStatus.Completed or
                    RasterTilePreseedJobStatus.Cancelled or
                    RasterTilePreseedJobStatus.Failed)
                {
                    await RecordJobFinalStateAsync(job, snapshot, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task RecordJobFinalStateAsync(RasterTilePreseedJob job, RasterTilePreseedJobSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await this.jobs.UnregisterAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        job.Dispose();

        await this.completedJobs.RecordCompletionAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessWorkItemAsync(RasterTilePreseedWorkItem workItem, CancellationToken cancellationToken)
    {
        await this.metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var metadataSnapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var job = workItem.Job;
        var request = workItem.Request;

        var datasetPlans = new List<DatasetPreseedPlan>();
        foreach (var datasetId in request.DatasetIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dataset = await this.rasterRegistry.FindAsync(datasetId, cancellationToken).ConfigureAwait(false);
            if (dataset is null)
            {
                this.logger.LogWarning("Preseed job {JobId} skipped unknown dataset {DatasetId}.", job.JobId, datasetId);
                continue;
            }

            if (!dataset.Cache.Enabled)
            {
                this.logger.LogInformation("Preseed job {JobId} skipped dataset {DatasetId} because caching is disabled.", job.JobId, datasetId);
                continue;
            }

            var normalizedMatrix = NormalizeTileMatrix(request.TileMatrixSetId);
            if (normalizedMatrix is null)
            {
                this.logger.LogWarning("Preseed job {JobId} skipped dataset {DatasetId} because tile matrix set {Matrix} is unsupported.", job.JobId, datasetId, request.TileMatrixSetId);
                continue;
            }

            if (!OgcSharedHandlers.TryResolveStyle(dataset, request.StyleId, out var styleId, out var unresolvedStyle))
            {
                this.logger.LogWarning("Preseed job {JobId} skipped dataset {DatasetId} due to unknown style {StyleId}.", job.JobId, datasetId, unresolvedStyle ?? request.StyleId);
                continue;
            }

            var styleDefinition = StyleResolutionHelper.ResolveStyleForRaster(metadataSnapshot, dataset, styleId);
            var (matrixId, _, matrixCrs) = normalizedMatrix.Value;
            var (minZoom, maxZoom) = ResolveZoomRange(dataset, request.MinZoom, request.MaxZoom);

            if (!TryResolveBoundingBox(dataset, metadataSnapshot, out var boundingBox))
            {
                this.logger.LogWarning("Preseed job {JobId} skipped dataset {DatasetId} because no extent is defined compatible with {MatrixCrs}.", job.JobId, datasetId, matrixCrs);
                continue;
            }

            for (var zoom = minZoom; zoom <= maxZoom; zoom++)
            {
                var range = OgcTileMatrixHelper.GetTileRange(matrixId, zoom, boundingBox[0], boundingBox[1], boundingBox[2], boundingBox[3]);
                if (range.MinRow > range.MaxRow || range.MinColumn > range.MaxColumn)
                {
                    continue;
                }

                datasetPlans.Add(new DatasetPreseedPlan(dataset, styleId, styleDefinition, matrixId, matrixCrs, zoom, range, boundingBox, request));
            }
        }

        if (datasetPlans.Count == 0)
        {
            job.MarkCancelled("No eligible datasets to seed.");
            return;
        }

        var exceededDataset = datasetPlans
            .GroupBy(plan => plan.Dataset.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                DatasetId = group.Key,
                Tiles = group.Sum(plan => CountTiles(plan.Range))
            })
            .FirstOrDefault(result => result.Tiles > RasterTilePreseedRequest.MaxTileBudget);

        if (exceededDataset is not null)
        {
            var formattedTiles = exceededDataset.Tiles.ToString("N0", CultureInfo.InvariantCulture);
            var formattedBudget = RasterTilePreseedRequest.MaxTileBudget.ToString("N0", CultureInfo.InvariantCulture);
            throw new InvalidOperationException(
                $"Dataset '{exceededDataset.DatasetId}' would enqueue {formattedTiles} tiles, exceeding the per-dataset limit of {formattedBudget}.");
        }

        var totalTiles = datasetPlans.Aggregate(0L, (accumulator, plan) => accumulator + CountTiles(plan.Range));
        job.SetTotalTiles(totalTiles);
        job.MarkStarted("Seeding raster tiles");

        using var overlayCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = OverlayCacheSizeLimit,
            ExpirationScanFrequency = TimeSpan.FromMinutes(5)
        });

        var workerCount = Math.Min(_maxParallelism, Math.Max(1, datasetPlans.Count));
        var channelCapacity = Math.Clamp(workerCount * WorkerBufferMultiplier, workerCount, 512);
        var tileChannel = Channel.CreateBounded<TileCoordinate>(new BoundedChannelOptions(channelCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var workers = new List<Task>(workerCount);
        for (var i = 0; i < workerCount; i++)
        {
            workers.Add(ProcessTileWorkerAsync(tileChannel.Reader, metadataSnapshot, overlayCache, job, cancellationToken));
        }

        Exception? producerException = null;
        try
        {
            foreach (var plan in datasetPlans)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var row = plan.Range.MinRow; row <= plan.Range.MaxRow; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (var column = plan.Range.MinColumn; column <= plan.Range.MaxColumn; column++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var tile = new TileCoordinate(plan, row, column);
                        await tileChannel.Writer.WriteAsync(tile, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            producerException = ex;
            tileChannel.Writer.TryComplete(ex);
        }
        finally
        {
            if (producerException is null)
            {
                tileChannel.Writer.TryComplete();
            }
        }

        Exception? workerException = null;
        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            workerException = ex;
        }

        if (producerException is not null && workerException is not null)
        {
            throw new AggregateException(producerException, workerException);
        }

        if (workerException is not null)
        {
            throw workerException;
        }

        if (producerException is not null)
        {
            throw producerException;
        }

        job.UpdateStage("Completed");
    }

    private async Task ProcessTileWorkerAsync(
        ChannelReader<TileCoordinate> reader,
        MetadataSnapshot metadataSnapshot,
        IMemoryCache overlayCache,
        RasterTilePreseedJob job,
        CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var tile))
            {
                await ProcessTileAsync(tile, metadataSnapshot, overlayCache, job, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessTileAsync(
        TileCoordinate tile,
        MetadataSnapshot metadataSnapshot,
        IMemoryCache overlayCache,
        RasterTilePreseedJob job,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var plan = tile.Plan;
        var row = tile.Row;
        var column = tile.Column;
        var stage = string.Create(CultureInfo.InvariantCulture, $"{plan.Dataset.Id} z{plan.Zoom} r{row} c{column}");
        job.UpdateStage(stage);

        var cacheKey = new RasterTileCacheKey(
            plan.Dataset.Id,
            plan.MatrixId,
            plan.Zoom,
            row,
            column,
            plan.StyleId,
            plan.Request.Format,
            plan.Request.Transparent,
            plan.Request.TileSize);

        if (!plan.Request.Overwrite)
        {
            try
            {
                var cached = await this.cacheProvider.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                if (cached is not null)
                {
                    job.IncrementTiles(stage);
                    return;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Cache lookup failed for dataset {DatasetId} tile {Zoom}/{Column}/{Row} while preseeding.", plan.Dataset.Id, plan.Zoom, column, row);
            }
        }

        var bbox = OgcTileMatrixHelper.GetBoundingBox(plan.MatrixId, plan.Zoom, row, column);

        IReadOnlyList<Geometry>? overlayGeometries = null;
        if (OgcSharedHandlers.RequiresVectorOverlay(plan.StyleDefinition))
        {
            overlayGeometries = await ResolveVectorOverlayAsync(plan, bbox, metadataSnapshot, overlayCache, cancellationToken).ConfigureAwait(false);
        }

        var renderRequest = new RasterRenderRequest(
            plan.Dataset,
            bbox,
            plan.Request.TileSize,
            plan.Request.TileSize,
            ResolveSourceCrs(plan.Dataset),
            plan.MatrixCrs,
            plan.Request.Format,
            plan.Request.Transparent,
            plan.StyleId,
            plan.StyleDefinition,
            overlayGeometries);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var renderResult = await this.rasterRenderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);

            await using var renderStream = renderResult.Content;
            if (renderStream.CanSeek)
            {
                renderStream.Seek(0, System.IO.SeekOrigin.Begin);
            }

            using var memory = new System.IO.MemoryStream();
            await renderStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            var content = memory.ToArray();

            if (content.Length > 0)
            {
                try
                {
                    var entry = new RasterTileCacheEntry(content, renderResult.ContentType, DateTimeOffset.UtcNow);
                    await this.cacheProvider.StoreAsync(cacheKey, entry, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to store raster tile {DatasetId} {Zoom}/{Column}/{Row}.", plan.Dataset.Id, plan.Zoom, column, row);
                }
            }

            stopwatch.Stop();
            this.metrics.RecordRenderLatency(plan.Dataset.Id, stopwatch.Elapsed, true);
        }
        catch
        {
            stopwatch.Stop();
            this.metrics.RecordRenderLatency(plan.Dataset.Id, stopwatch.Elapsed, false);
            throw;
        }

        job.IncrementTiles(stage);
    }

    private static string ResolveSourceCrs(RasterDatasetDefinition dataset)
        => dataset.Crs.Count > 0 ? dataset.Crs[0] : CrsHelper.DefaultCrsIdentifier;

    private static (int MinZoom, int MaxZoom) ResolveZoomRange(RasterDatasetDefinition dataset, int? requestedMin, int? requestedMax)
    {
        var (datasetMin, datasetMax) = OgcTileMatrixHelper.ResolveZoomRange(dataset.Cache.ZoomLevels);

        var min = requestedMin.HasValue ? Math.Max(0, requestedMin.Value) : datasetMin;
        var max = requestedMax.HasValue ? requestedMax.Value : datasetMax;
        if (max < min)
        {
            max = min;
        }

        return (min, max);
    }

    private static (string MatrixId, string MatrixName, string MatrixCrs)? NormalizeTileMatrix(string tileMatrixSetId)
        => OgcSharedHandlers.NormalizeTileMatrixSet(tileMatrixSetId);

    private static long CountTiles((int MinRow, int MaxRow, int MinColumn, int MaxColumn) range)
    {
        var rows = (long)range.MaxRow - range.MinRow + 1;
        var columns = (long)range.MaxColumn - range.MinColumn + 1;
        return rows * columns;
    }

    private async Task<IReadOnlyList<Geometry>> ResolveVectorOverlayAsync(
        DatasetPreseedPlan plan,
        double[] tileBoundingBox,
        MetadataSnapshot metadataSnapshot,
        IMemoryCache overlayCache,
        CancellationToken cancellationToken)
    {
        var cacheKey = new OverlayCacheKey(plan.Dataset.Id, plan.Zoom);

        if (!overlayCache.TryGetValue(cacheKey, out IReadOnlyList<Geometry>? cachedGeometries))
        {
            cachedGeometries = await overlayCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetSize(1);
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(10));

                var fetchBoundingBox = plan.BoundingBox.Length >= 4 ? plan.BoundingBox : tileBoundingBox;
                var geometries = await OgcSharedHandlers.CollectVectorGeometriesAsync(
                    plan.Dataset,
                    fetchBoundingBox,
                    metadataSnapshot,
                    _featureRepository,
                    cancellationToken).ConfigureAwait(false);

                return geometries.Count == 0 ? Array.Empty<Geometry>() : geometries;
            }).ConfigureAwait(false);
        }

        if (cachedGeometries.IsNullOrEmpty())
        {
            return Array.Empty<Geometry>();
        }

        return FilterGeometriesForTile(cachedGeometries, tileBoundingBox);
    }

    private static IReadOnlyList<Geometry> FilterGeometriesForTile(IReadOnlyList<Geometry> geometries, double[] tileBoundingBox)
    {
        if (geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        if (geometries.Count == 1)
        {
            var geometry = geometries[0];
            if (geometry is null)
            {
                return Array.Empty<Geometry>();
            }

            var envelope = new Envelope(tileBoundingBox[0], tileBoundingBox[2], tileBoundingBox[1], tileBoundingBox[3]);
            return geometry.EnvelopeInternal?.Intersects(envelope) == true
                ? geometries
                : Array.Empty<Geometry>();
        }

        var tileEnvelope = new Envelope(tileBoundingBox[0], tileBoundingBox[2], tileBoundingBox[1], tileBoundingBox[3]);
        var filtered = new List<Geometry>(geometries.Count);

        foreach (var geometry in geometries)
        {
            if (geometry?.EnvelopeInternal?.Intersects(tileEnvelope) == true)
            {
                filtered.Add(geometry);
            }
        }

        if (filtered.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        return filtered.Count == geometries.Count ? geometries : filtered;
    }

    private static bool TryResolveBoundingBox(RasterDatasetDefinition dataset, MetadataSnapshot snapshot, out double[] bbox)
    {
        bbox = Array.Empty<double>();

        if (dataset.Extent is { Bbox.Count: > 0 })
        {
            bbox = dataset.Extent.Bbox[0];
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataset.ServiceId) && !string.IsNullOrWhiteSpace(dataset.LayerId))
        {
            var service = snapshot.Services.FirstOrDefault(s => string.Equals(s.Id, dataset.ServiceId, StringComparison.OrdinalIgnoreCase));
            var layer = service?.Layers.FirstOrDefault(l => string.Equals(l.Id, dataset.LayerId, StringComparison.OrdinalIgnoreCase));
            if (layer?.Extent is { Bbox.Count: > 0 })
            {
                bbox = layer.Extent.Bbox[0];
                return true;
            }
        }

        return false;
    }

    private readonly record struct TileCoordinate(DatasetPreseedPlan Plan, int Row, int Column);

    private readonly record struct OverlayCacheKey(string DatasetId, int Zoom);

    private readonly struct DatasetPreseedPlan
    {
        public DatasetPreseedPlan(
            RasterDatasetDefinition dataset,
            string styleId,
            StyleDefinition? styleDefinition,
            string matrixId,
            string matrixCrs,
            int zoom,
            (int MinRow, int MaxRow, int MinColumn, int MaxColumn) range,
            double[] boundingBox,
            RasterTilePreseedRequest request)
        {
            Dataset = dataset;
            StyleId = styleId;
            StyleDefinition = styleDefinition;
            MatrixId = matrixId;
            MatrixCrs = matrixCrs;
            Zoom = zoom;
            Range = range;
            BoundingBox = boundingBox ?? Array.Empty<double>();
            Request = request;
        }

        public RasterDatasetDefinition Dataset { get; }
        public string StyleId { get; }
        public StyleDefinition? StyleDefinition { get; }
        public string MatrixId { get; }
        public string MatrixCrs { get; }
        public int Zoom { get; }
        public (int MinRow, int MaxRow, int MinColumn, int MaxColumn) Range { get; }
        public double[] BoundingBox { get; }
        public RasterTilePreseedRequest Request { get; }
    }

    private readonly record struct RasterTilePreseedWorkItem(RasterTilePreseedRequest Request, RasterTilePreseedJob Job);

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

        base.Dispose();
    }
}

public sealed record RasterTileCachePurgeResult(IReadOnlyList<string> PurgedDatasets, IReadOnlyList<string> FailedDatasets)
{
    public static RasterTileCachePurgeResult Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>());
}
