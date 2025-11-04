// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.ControlPlane;

public interface IRasterTileCacheApiClient
{
    Task<RasterTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, RasterTilePreseedJobRequest request, CancellationToken cancellationToken);
    Task<RasterTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RasterTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<RasterTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<RasterTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, IReadOnlyList<string> datasetIds, CancellationToken cancellationToken);
    Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class RasterTileCacheApiClient : ControlPlaneApiClientBase, IRasterTileCacheApiClient
{
    private static readonly JsonSerializerOptions CustomSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public RasterTileCacheApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<RasterTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, RasterTilePreseedJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DatasetIds.Count == 0)
        {
            throw new ArgumentException("At least one dataset identifier must be provided.", nameof(request));
        }

        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/raster-cache/jobs");
        var payload = JsonSerializer.Serialize(request, CustomSerializerOptions);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected raster cache request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var envelope = await DeserializeAsync<JobEnvelope>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        if (envelope.Job is null)
        {
            throw new InvalidOperationException("Control plane response did not include a job payload.");
        }

        return Normalize(envelope.Job);
    }

    public async Task<RasterTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/admin/raster-cache/jobs/{jobId:D}");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeAsync<JobEnvelope>(response, cancellationToken).ConfigureAwait(false);
        return envelope?.Job is null ? null : Normalize(envelope.Job);
    }

    public async Task<IReadOnlyList<RasterTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/raster-cache/jobs");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var envelope = await DeserializeAsync<JobListEnvelope>(response, cancellationToken).ConfigureAwait(false);
        if (envelope?.Jobs is null)
        {
            return Array.Empty<RasterTilePreseedJob>();
        }

        return envelope.Jobs.Select(Normalize).ToArray();
    }

    public async Task<RasterTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, IReadOnlyList<string> datasetIds, CancellationToken cancellationToken)
    {
        if (datasetIds is null || datasetIds.Count == 0)
        {
            throw new ArgumentException("At least one dataset identifier must be provided.", nameof(datasetIds));
        }

        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/raster-cache/datasets/purge");
        var payload = JsonSerializer.Serialize(new RasterTileCachePurgeRequest(datasetIds), CustomSerializerOptions);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected purge request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var result = await DeserializeAsync<PurgeEnvelope>(response, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new RasterTileCachePurgeResult(result.Purged ?? Array.Empty<string>(), result.Failed ?? Array.Empty<string>());
    }

    public async Task<RasterTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/admin/raster-cache/jobs/{jobId:D}");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var envelope = await DeserializeAsync<JobEnvelope>(response, cancellationToken).ConfigureAwait(false);
        return envelope?.Job is null ? null : Normalize(envelope.Job);
    }

    public async Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/raster-cache/stats");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var envelope = await DeserializeAsync<RasterStatsEnvelope>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new CacheStats(
            envelope.TileCount ?? 0,
            envelope.TotalSizeBytes ?? 0,
            envelope.DatasetCount ?? 0,
            0);
    }

    public async Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/raster-cache/purge-all");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected purge-all request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var envelope = await DeserializeAsync<RasterPurgeAllEnvelope>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new CachePurgeAllResult(envelope.Success ?? false, envelope.TilesPurged ?? 0, envelope.Message);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, CustomSerializerOptions, cancellationToken);
    }

    private static RasterTilePreseedJob Normalize(RasterTilePreseedJob job)
    {
        var datasets = job.DatasetIds ?? Array.Empty<string>();
        return job with { DatasetIds = datasets };
    }

    private sealed record JobEnvelope(RasterTilePreseedJob? Job);
    private sealed record JobListEnvelope(IReadOnlyList<RasterTilePreseedJob>? Jobs);
}

public sealed record RasterTilePreseedJobRequest(
    IReadOnlyList<string> DatasetIds,
    string? TileMatrixSetId,
    int? MinZoom,
    int? MaxZoom,
    string? StyleId,
    bool? Transparent,
    string? Format,
    bool? Overwrite,
    int? TileSize);

public enum RasterTilePreseedJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record RasterTilePreseedJob(
    Guid JobId,
    RasterTilePreseedJobStatus Status,
    double Progress,
    string Stage,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<string>? DatasetIds,
    string TileMatrixSetId,
    int TileSize,
    bool Transparent,
    string Format,
    bool Overwrite,
    long TilesCompleted,
    long TilesTotal);

public sealed record RasterTileCachePurgeRequest(IReadOnlyList<string> DatasetIds);

public sealed record RasterTileCachePurgeResult(IReadOnlyList<string> PurgedDatasets, IReadOnlyList<string> FailedDatasets);

internal sealed record PurgeEnvelope(IReadOnlyList<string>? Purged, IReadOnlyList<string>? Failed);

internal sealed record RasterStatsEnvelope(long? TileCount, long? TotalSizeBytes, int? DatasetCount);

internal sealed record RasterPurgeAllEnvelope(bool? Success, long? TilesPurged, string? Message);
