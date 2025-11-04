// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.ControlPlane;

public interface IConfigurationApiClient
{
    Task<JsonDocument> GetConfigurationStatusAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> ToggleGlobalProtocolAsync(ControlPlaneConnection connection, string protocol, bool enabled, CancellationToken cancellationToken);
    Task<JsonDocument> ToggleServiceProtocolAsync(ControlPlaneConnection connection, string serviceId, string protocol, bool enabled, CancellationToken cancellationToken);
    Task<JsonDocument> GetServiceConfigurationAsync(ControlPlaneConnection connection, string serviceId, CancellationToken cancellationToken);
}

public sealed class ConfigurationApiClient : ControlPlaneApiClientBase, IConfigurationApiClient
{
    public ConfigurationApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<JsonDocument> GetConfigurationStatusAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/config/status", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected configuration status but received null response.");
    }

    public Task<JsonDocument> ToggleGlobalProtocolAsync(
        ControlPlaneConnection connection,
        string protocol,
        bool enabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocol);
        return PatchAsJsonDocumentAsync(connection, $"/admin/config/services/{protocol}", new { enabled }, cancellationToken);
    }

    public Task<JsonDocument> ToggleServiceProtocolAsync(
        ControlPlaneConnection connection,
        string serviceId,
        string protocol,
        bool enabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(protocol);
        return PatchAsJsonDocumentAsync(connection, $"/admin/config/services/{serviceId}/{protocol}", new { enabled }, cancellationToken);
    }

    public async Task<JsonDocument> GetServiceConfigurationAsync(
        ControlPlaneConnection connection,
        string serviceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        var result = await GetAsJsonDocumentAsync(connection, $"/admin/config/services/{serviceId}", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Service configuration '{serviceId}' not found.");
    }
}
