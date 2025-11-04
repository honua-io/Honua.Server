// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents the status information of a job.
/// </summary>
public sealed class StatusInfo
{
    /// <summary>
    /// Gets or sets the job identifier.
    /// </summary>
    [JsonPropertyName("jobID")]
    public required string JobId { get; init; }

    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    [JsonPropertyName("processID")]
    public required string ProcessId { get; init; }

    /// <summary>
    /// Gets or sets the job type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "process";

    /// <summary>
    /// Gets or sets the job status.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required JobStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the message describing the current status.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; init; }

    /// <summary>
    /// Gets or sets the start timestamp.
    /// </summary>
    [JsonPropertyName("started")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Started { get; init; }

    /// <summary>
    /// Gets or sets the finished timestamp.
    /// </summary>
    [JsonPropertyName("finished")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Finished { get; init; }

    /// <summary>
    /// Gets or sets the updated timestamp.
    /// </summary>
    [JsonPropertyName("updated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// Gets or sets progress percentage (0-100).
    /// </summary>
    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Progress { get; init; }

    /// <summary>
    /// Gets or sets links related to the job.
    /// </summary>
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProcessLink>? Links { get; init; }
}
