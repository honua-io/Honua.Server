// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.Services.ControlPlane;

public interface IMetadataApiClient
{
    Task<JsonDocument> ReloadMetadataAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> ValidateMetadataAsync(ControlPlaneConnection connection, string? metadataJson, CancellationToken cancellationToken);
    Task<JsonDocument> DiffMetadataAsync(ControlPlaneConnection connection, string metadataJson, CancellationToken cancellationToken);
    Task<JsonDocument> ApplyMetadataAsync(ControlPlaneConnection connection, string metadataJson, CancellationToken cancellationToken);
    Task<JsonDocument> ListSnapshotsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken);
    Task<JsonDocument> CreateSnapshotAsync(ControlPlaneConnection connection, string label, string? notes, CancellationToken cancellationToken);
    Task<JsonDocument> GetSnapshotAsync(ControlPlaneConnection connection, string label, CancellationToken cancellationToken);
    Task<JsonDocument> RestoreSnapshotAsync(ControlPlaneConnection connection, string label, CancellationToken cancellationToken);
}

public sealed class MetadataApiClient : ControlPlaneApiClientBase, IMetadataApiClient
{
    public MetadataApiClient(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public Task<JsonDocument> ReloadMetadataAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        return PostAsJsonDocumentAsync(connection, "/admin/metadata/reload", null, cancellationToken);
    }

    public Task<JsonDocument> ValidateMetadataAsync(
        ControlPlaneConnection connection,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        return PostAsJsonDocumentAsync(connection, "/admin/metadata/validate", metadataJson, cancellationToken);
    }

    public Task<JsonDocument> DiffMetadataAsync(
        ControlPlaneConnection connection,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataJson);
        return PostAsJsonDocumentAsync(connection, "/admin/metadata/diff", metadataJson, cancellationToken);
    }

    public Task<JsonDocument> ApplyMetadataAsync(
        ControlPlaneConnection connection,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataJson);
        return PostAsJsonDocumentAsync(connection, "/admin/metadata/apply", metadataJson, cancellationToken);
    }

    public async Task<JsonDocument> ListSnapshotsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
    {
        var result = await GetAsJsonDocumentAsync(connection, "/admin/metadata/snapshots", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Expected snapshot list but received null response.");
    }

    public Task<JsonDocument> CreateSnapshotAsync(
        ControlPlaneConnection connection,
        string label,
        string? notes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return PostAsJsonDocumentAsync(connection, "/admin/metadata/snapshots", new { label, notes }, cancellationToken);
    }

    public async Task<JsonDocument> GetSnapshotAsync(
        ControlPlaneConnection connection,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var result = await GetAsJsonDocumentAsync(connection, $"/admin/metadata/snapshots/{label}", cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Snapshot '{label}' not found.");
    }

    public Task<JsonDocument> RestoreSnapshotAsync(
        ControlPlaneConnection connection,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return PostAsJsonDocumentAsync(connection, $"/admin/metadata/snapshots/{label}/restore", null, cancellationToken);
    }
}
