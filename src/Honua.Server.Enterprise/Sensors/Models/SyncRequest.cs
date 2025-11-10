// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Enterprise.Sensors.Models;

/// <summary>
/// Request for synchronizing observations from a mobile device to the server.
/// Used for offline-first mobile applications that need to batch upload observations.
/// </summary>
public sealed record SyncRequest
{
    /// <summary>
    /// The ID of the Thing (device) performing the sync.
    /// </summary>
    public string ThingId { get; init; } = default!;

    /// <summary>
    /// Optional timestamp of the last successful sync.
    /// Used to determine what data needs to be downloaded from the server.
    /// </summary>
    public DateTime? SinceTimestamp { get; init; }

    /// <summary>
    /// Unique identifier for this sync batch.
    /// Used to track and potentially retry failed sync operations.
    /// </summary>
    public string? SyncBatchId { get; init; }

    /// <summary>
    /// Collection of observations to upload to the server.
    /// </summary>
    public IReadOnlyList<Observation> Observations { get; init; } = Array.Empty<Observation>();
}
