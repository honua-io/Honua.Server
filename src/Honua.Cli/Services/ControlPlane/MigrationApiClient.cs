// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Migration.GeoservicesRest;

namespace Honua.Cli.Services.ControlPlane;

public interface IMigrationApiClient
{
    Task<GeoservicesRestMigrationJobSnapshot> CreateJobAsync(
        ControlPlaneConnection connection,
        Uri sourceServiceUri,
        string targetServiceId,
        string targetFolderId,
        string targetDataSourceId,
        int[]? layerIds,
        bool includeData,
        int? batchSize,
        CancellationToken cancellationToken);

    Task<GeoservicesRestMigrationJobSnapshot?> GetJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<GeoservicesRestMigrationJobSnapshot?> CancelJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class MigrationApiClient : ControlPlaneApiClientBase, IMigrationApiClient
{
    public MigrationApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<GeoservicesRestMigrationJobSnapshot> CreateJobAsync(
        ControlPlaneConnection connection,
        Uri sourceServiceUri,
        string targetServiceId,
        string targetFolderId,
        string targetDataSourceId,
        int[]? layerIds,
        bool includeData,
        int? batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceServiceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetServiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDataSourceId);

        var request = new
        {
            sourceServiceUri = sourceServiceUri.ToString(),
            targetServiceId,
            targetFolderId,
            targetDataSourceId,
            layerIds = layerIds?.ToList(),
            includeData,
            batchSize
        };

        var client = CreateClient(connection, "honua-control-plane");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/migrations/jobs");
        httpRequest.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(request, DefaultSerializerOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected migration request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var envelope = await System.Text.Json.JsonSerializer.DeserializeAsync<JobEnvelope>(stream, DefaultSerializerOptions, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        if (envelope.Job is null)
        {
            throw new InvalidOperationException("Control plane response did not include a job payload.");
        }

        return envelope.Job;
    }

    public async Task<GeoservicesRestMigrationJobSnapshot?> GetJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<JobEnvelope>(connection, $"/admin/migrations/jobs/{jobId:D}", cancellationToken).ConfigureAwait(false);
        return envelope?.Job;
    }

    public async Task<GeoservicesRestMigrationJobSnapshot?> CancelJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/admin/migrations/jobs/{jobId:D}");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var envelope = await System.Text.Json.JsonSerializer.DeserializeAsync<JobEnvelope>(stream, DefaultSerializerOptions, cancellationToken).ConfigureAwait(false);
        return envelope?.Job;
    }

    public async Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<JobsEnvelope>(connection, "/admin/migrations/jobs", cancellationToken).ConfigureAwait(false);
        return envelope?.Jobs ?? Array.Empty<GeoservicesRestMigrationJobSnapshot>();
    }

    private sealed record JobEnvelope(GeoservicesRestMigrationJobSnapshot? Job);
    private sealed record JobsEnvelope(IReadOnlyList<GeoservicesRestMigrationJobSnapshot>? Jobs);
}
