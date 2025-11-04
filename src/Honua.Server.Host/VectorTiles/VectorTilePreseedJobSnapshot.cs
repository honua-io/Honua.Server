// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json.Serialization;

namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Snapshot of a vector tile preseed job for API responses
/// </summary>
public sealed class VectorTilePreseedJobSnapshot
{
    [JsonPropertyName("jobId")]
    public required Guid JobId { get; init; }

    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; init; }

    [JsonPropertyName("layerId")]
    public required string LayerId { get; init; }

    [JsonPropertyName("minZoom")]
    public int MinZoom { get; init; }

    [JsonPropertyName("maxZoom")]
    public int MaxZoom { get; init; }

    [JsonPropertyName("datetime")]
    public string? Datetime { get; init; }

    [JsonPropertyName("status")]
    public VectorTilePreseedJobStatus Status { get; init; }

    [JsonPropertyName("stage")]
    public string Stage { get; init; } = "Queued";

    [JsonPropertyName("progress")]
    public double Progress { get; init; }

    [JsonPropertyName("tilesProcessed")]
    public long TilesProcessed { get; init; }

    [JsonPropertyName("tilesTotal")]
    public long TilesTotal { get; init; }

    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; init; }

    [JsonPropertyName("startedUtc")]
    public DateTimeOffset? StartedUtc { get; init; }

    [JsonPropertyName("completedUtc")]
    public DateTimeOffset? CompletedUtc { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    public static VectorTilePreseedJobSnapshot FromJob(VectorTilePreseedJob job)
    {
        return new VectorTilePreseedJobSnapshot
        {
            JobId = job.JobId,
            ServiceId = job.Request.ServiceId,
            LayerId = job.Request.LayerId,
            MinZoom = job.Request.MinZoom,
            MaxZoom = job.Request.MaxZoom,
            Datetime = job.Request.Datetime,
            Status = job.Status,
            Stage = job.Stage,
            Progress = job.Progress,
            TilesProcessed = job.TilesProcessed,
            TilesTotal = job.TilesTotal,
            CreatedUtc = job.CreatedUtc,
            StartedUtc = job.StartedUtc,
            CompletedUtc = job.CompletedUtc,
            ErrorMessage = job.ErrorMessage
        };
    }
}
