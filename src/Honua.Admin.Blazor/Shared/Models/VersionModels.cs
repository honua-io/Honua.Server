// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to create a metadata snapshot.
/// </summary>
public sealed class CreateSnapshotRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Metadata snapshot descriptor.
/// </summary>
public sealed class SnapshotDescriptor
{
    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }
}

/// <summary>
/// Snapshot details including metadata content.
/// </summary>
public sealed class SnapshotDetails
{
    [JsonPropertyName("snapshot")]
    public required SnapshotDescriptor Snapshot { get; set; }

    [JsonPropertyName("metadata")]
    public required string Metadata { get; set; }
}

/// <summary>
/// Response from snapshot list endpoint.
/// </summary>
public sealed class SnapshotListResponse
{
    [JsonPropertyName("snapshots")]
    public List<SnapshotDescriptor> Snapshots { get; set; } = new();
}

/// <summary>
/// Response from snapshot create endpoint.
/// </summary>
public sealed class CreateSnapshotResponse
{
    [JsonPropertyName("snapshot")]
    public required SnapshotDescriptor Snapshot { get; set; }
}

/// <summary>
/// Response from snapshot restore endpoint.
/// </summary>
public sealed class RestoreSnapshotResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }
}

/// <summary>
/// Metadata diff result showing changes between snapshots.
/// </summary>
public sealed class MetadataDiffResult
{
    [JsonPropertyName("addedServices")]
    public List<string> AddedServices { get; set; } = new();

    [JsonPropertyName("removedServices")]
    public List<string> RemovedServices { get; set; } = new();

    [JsonPropertyName("modifiedServices")]
    public List<string> ModifiedServices { get; set; } = new();

    [JsonPropertyName("addedLayers")]
    public List<string> AddedLayers { get; set; } = new();

    [JsonPropertyName("removedLayers")]
    public List<string> RemovedLayers { get; set; } = new();

    [JsonPropertyName("modifiedLayers")]
    public List<string> ModifiedLayers { get; set; } = new();

    [JsonPropertyName("addedFolders")]
    public List<string> AddedFolders { get; set; } = new();

    [JsonPropertyName("removedFolders")]
    public List<string> RemovedFolders { get; set; } = new();

    public bool HasChanges()
    {
        return AddedServices.Any() || RemovedServices.Any() || ModifiedServices.Any()
            || AddedLayers.Any() || RemovedLayers.Any() || ModifiedLayers.Any()
            || AddedFolders.Any() || RemovedFolders.Any();
    }

    public int TotalChanges()
    {
        return AddedServices.Count + RemovedServices.Count + ModifiedServices.Count
            + AddedLayers.Count + RemovedLayers.Count + ModifiedLayers.Count
            + AddedFolders.Count + RemovedFolders.Count;
    }
}
