// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Import;

namespace Honua.Cli.Services.ControlPlane;

public interface IDataIngestionApiClient
{
    Task<DataIngestionJobSnapshot> CreateJobAsync(ControlPlaneConnection connection, string serviceId, string layerId, string filePath, bool overwrite, CancellationToken cancellationToken);
    Task<DataIngestionJobSnapshot?> GetJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<DataIngestionJobSnapshot?> CancelJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataIngestionJobSnapshot>> ListJobsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class DataIngestionApiClient : ControlPlaneApiClientBase, IDataIngestionApiClient
{
    public DataIngestionApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<DataIngestionJobSnapshot> CreateJobAsync(ControlPlaneConnection connection, string serviceId, string layerId, string filePath, bool overwrite, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Dataset '{filePath}' could not be found.", filePath);
        }

        var client = CreateClient(connection, "honua-control-plane");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admin/ingestion/jobs");
        using var content = BuildMultipartContent(serviceId, layerId, filePath, overwrite);
        request.Content = content;

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Control plane rejected ingestion request ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<JobEnvelope>(stream, DefaultSerializerOptions, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Control plane returned an empty response.");

        if (envelope.Job is null)
        {
            throw new InvalidOperationException("Control plane response did not include a job payload.");
        }

        return envelope.Job;
    }

    public async Task<DataIngestionJobSnapshot?> GetJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<JobEnvelope>(connection, $"/v1/admin/ingestion/jobs/{jobId:D}", cancellationToken).ConfigureAwait(false);
        return envelope?.Job;
    }

    public async Task<DataIngestionJobSnapshot?> CancelJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
    {
        var client = CreateClient(connection, "honua-control-plane");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/admin/ingestion/jobs/{jobId:D}");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var envelope = await JsonSerializer.DeserializeAsync<JobEnvelope>(stream, DefaultSerializerOptions, cancellationToken).ConfigureAwait(false);
        return envelope?.Job;
    }

    public async Task<IReadOnlyList<DataIngestionJobSnapshot>> ListJobsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<JobsEnvelope>(connection, "/v1/admin/ingestion/jobs", cancellationToken).ConfigureAwait(false);
        return envelope?.Jobs ?? Array.Empty<DataIngestionJobSnapshot>();
    }

    private static MultipartFormDataContent BuildMultipartContent(string serviceId, string layerId, string filePath, bool overwrite)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(serviceId), "serviceId");
        content.Add(new StringContent(layerId), "layerId");
        content.Add(new StringContent(overwrite ? "true" : "false"), "overwrite");

        var fileName = Path.GetFileName(filePath);
        var stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(fileName));
        content.Add(fileContent, "file", fileName);

        return content;
    }

    private static string ResolveContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".geojson" => "application/geo+json",
            ".gpkg" => "application/geopackage+sqlite3",
            ".zip" => "application/zip",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    private sealed record JobEnvelope(DataIngestionJobSnapshot? Job);
    private sealed record JobsEnvelope(IReadOnlyList<DataIngestionJobSnapshot>? Jobs);
}
