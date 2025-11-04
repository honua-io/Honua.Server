// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.ControlPlane;

public interface ITracingApiClient
{
    Task<JsonDocument> GetTracingConfigurationAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> SetTracingSamplingAsync(ControlPlaneConnection connection, double ratio, CancellationToken cancellationToken);
    Task<JsonDocument> SetTracingExporterAsync(ControlPlaneConnection connection, string exporter, CancellationToken cancellationToken);
    Task<JsonDocument> SetTracingEndpointAsync(ControlPlaneConnection connection, string endpoint, CancellationToken cancellationToken);
    Task<JsonDocument> CreateTestTraceAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> GetActivitySourcesAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> GetPlatformGuidanceAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class TracingApiClient : ControlPlaneApiClientBase, ITracingApiClient
{
    public TracingApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<JsonDocument> GetTracingConfigurationAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/observability/tracing", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected tracing configuration but received null response.");
    }

    public Task<JsonDocument> SetTracingSamplingAsync(
        ControlPlaneConnection connection,
        double ratio,
        CancellationToken cancellationToken)
    {
        if (ratio < 0.0 || ratio > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), "Sampling ratio must be between 0.0 and 1.0");
        }

        return PatchAsJsonDocumentAsync(connection, "/admin/observability/tracing/sampling", new { ratio }, cancellationToken);
    }

    public Task<JsonDocument> SetTracingExporterAsync(
        ControlPlaneConnection connection,
        string exporter,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exporter);
        return PatchAsJsonDocumentAsync(connection, "/admin/observability/tracing/exporter", new { exporter }, cancellationToken);
    }

    public Task<JsonDocument> SetTracingEndpointAsync(
        ControlPlaneConnection connection,
        string endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        return PatchAsJsonDocumentAsync(connection, "/admin/observability/tracing/endpoint", new { endpoint }, cancellationToken);
    }

    public Task<JsonDocument> CreateTestTraceAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        return PostAsJsonDocumentAsync(connection, "/admin/observability/tracing/test", new { activityName = "CLITest", duration = 1000 }, cancellationToken);
    }

    public async Task<JsonDocument> GetActivitySourcesAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/observability/tracing/activity-sources", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected activity sources but received null response.");
    }

    public async Task<JsonDocument> GetPlatformGuidanceAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/observability/tracing/platforms", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected platform guidance but received null response.");
    }
}
