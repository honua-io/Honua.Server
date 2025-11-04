// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.ControlPlane;

public interface ILoggingApiClient
{
    Task<JsonDocument> GetLogLevelsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> SetLogLevelAsync(ControlPlaneConnection connection, string category, string level, CancellationToken cancellationToken);
    Task<JsonDocument> RemoveLogLevelOverrideAsync(ControlPlaneConnection connection, string category, CancellationToken cancellationToken);
    Task<JsonDocument> GetAvailableLogLevelsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
}

public sealed class LoggingApiClient : ControlPlaneApiClientBase, ILoggingApiClient
{
    public LoggingApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public async Task<JsonDocument> GetLogLevelsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/logging/categories", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected log levels but received null response.");
    }

    public Task<JsonDocument> SetLogLevelAsync(
        ControlPlaneConnection connection,
        string category,
        string level,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        return PatchAsJsonDocumentAsync(connection, $"/admin/logging/categories/{category}", new { level }, cancellationToken);
    }

    public async Task<JsonDocument> RemoveLogLevelOverrideAsync(
        ControlPlaneConnection connection,
        string category,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        var result = await DeleteAsJsonDocumentAsync(connection, $"/admin/logging/categories/{category}", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Log level override for category '{category}' not found.");
    }

    public async Task<JsonDocument> GetAvailableLogLevelsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/logging/levels", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected available log levels but received null response.");
    }
}
