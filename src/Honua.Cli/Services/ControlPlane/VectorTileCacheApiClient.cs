// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.ControlPlane;

public interface IVectorTileCacheApiClient
{
    Task<VectorTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, VectorTilePreseedJobRequest request, CancellationToken cancellationToken);
    Task<VectorTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VectorTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<VectorTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<VectorTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, VectorTileCachePurgeRequest request, CancellationToken cancellationToken);
    Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class VectorTileCacheApiClient : ControlPlaneApiClientBase, IVectorTileCacheApiClient
{
    private static readonly JsonSerializerOptions CustomSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public VectorTileCacheApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<VectorTilePreseedJob> EnqueueAsync(ControlPlaneConnection connection, VectorTilePreseedJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/vector-cache/jobs");
        var payload = JsonSerializer.Serialize(request, CustomSerializerOptions);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected vector cache request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var job = await DeserializeAsync<VectorTilePreseedJob>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return job;
    }

    public async Task<VectorTilePreseedJob?> GetAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/admin/vector-cache/jobs/{jobId:D}");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await DeserializeAsync<VectorTilePreseedJob>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<VectorTilePreseedJob>> ListAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/vector-cache/jobs");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var jobs = await DeserializeAsync<IReadOnlyList<VectorTilePreseedJob>>(response, cancellationToken).ConfigureAwait(false);
        return jobs ?? Array.Empty<VectorTilePreseedJob>();
    }

    public async Task<VectorTilePreseedJob?> CancelAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/admin/vector-cache/jobs/{jobId:D}");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await DeserializeAsync<VectorTilePreseedJob>(response, cancellationToken);
    }

    public async Task<VectorTileCachePurgeResult> PurgeAsync(ControlPlaneConnection connection, VectorTileCachePurgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ServiceId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("ServiceId must be provided.", nameof(request));
        }

        var client = CreateClient(connection, "honua-control-plane");
        var endpoint = request.LayerId != null
            ? $"/admin/vector-cache/services/{Uri.EscapeDataString(request.ServiceId)}/layers/{Uri.EscapeDataString(request.LayerId)}/purge"
            : $"/admin/vector-cache/services/{Uri.EscapeDataString(request.ServiceId)}/purge";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        var payload = JsonSerializer.Serialize(request, CustomSerializerOptions);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected purge request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var result = await DeserializeAsync<VectorPurgeEnvelope>(response, cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new VectorTileCachePurgeResult(result.Purged ?? Array.Empty<string>(), result.Failed ?? Array.Empty<string>());
    }

    public async Task<CacheStats> GetStatsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/vector-cache/stats");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var envelope = await DeserializeAsync<VectorStatsEnvelope>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new CacheStats(
            envelope.TileCount ?? 0,
            envelope.TotalSizeBytes ?? 0,
            envelope.ServiceCount ?? 0,
            envelope.LayerCount ?? 0);
    }

    public async Task<CachePurgeAllResult> PurgeAllAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/vector-cache/purge-all");
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected purge-all request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        var envelope = await DeserializeAsync<VectorPurgeAllEnvelope>(response, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        return new CachePurgeAllResult(envelope.Success ?? false, envelope.TilesPurged ?? 0, envelope.Message);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, CustomSerializerOptions, cancellationToken);
    }
}

public sealed record VectorTilePreseedJobRequest(
    string ServiceId,
    string LayerId,
    int MinZoom,
    int MaxZoom,
    string? Datetime,
    bool Overwrite);

public enum VectorTilePreseedJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record VectorTilePreseedJob(
    Guid JobId,
    string ServiceId,
    string LayerId,
    VectorTilePreseedJobStatus Status,
    double Progress,
    long TilesProcessed,
    long TilesTotal,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record VectorTileCachePurgeRequest(
    string ServiceId,
    string? LayerId);

public sealed record VectorTileCachePurgeResult(
    IReadOnlyList<string> PurgedTargets,
    IReadOnlyList<string> FailedTargets);

internal sealed record VectorPurgeEnvelope(IReadOnlyList<string>? Purged, IReadOnlyList<string>? Failed);

internal sealed record VectorStatsEnvelope(long? TileCount, long? TotalSizeBytes, int? ServiceCount, int? LayerCount);

internal sealed record VectorPurgeAllEnvelope(bool? Success, long? TilesPurged, string? Message);

public sealed record CacheStats(
    long TileCount,
    long TotalSizeBytes,
    int DatasetOrServiceCount,
    int LayerCount);

public sealed record CachePurgeAllResult(
    bool Success,
    long TilesPurged,
    string? Message);
