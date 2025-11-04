// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents a process description as defined in OGC API - Processes.
/// </summary>
public sealed class ProcessDescription
{
    /// <summary>
    /// Gets or sets the process identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the version of the process.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets the title of the process.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the description of the process.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets keywords associated with the process.
    /// </summary>
    [JsonPropertyName("keywords")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Keywords { get; init; }

    /// <summary>
    /// Gets or sets the job control options (sync-execute, async-execute).
    /// </summary>
    [JsonPropertyName("jobControlOptions")]
    public required List<string> JobControlOptions { get; init; }

    /// <summary>
    /// Gets or sets the output transmission modes (value, reference).
    /// </summary>
    [JsonPropertyName("outputTransmission")]
    public required List<string> OutputTransmission { get; init; }

    /// <summary>
    /// Gets or sets the inputs for the process.
    /// </summary>
    [JsonPropertyName("inputs")]
    public required Dictionary<string, ProcessInput> Inputs { get; init; }

    /// <summary>
    /// Gets or sets the outputs for the process.
    /// </summary>
    [JsonPropertyName("outputs")]
    public required Dictionary<string, ProcessOutput> Outputs { get; init; }

    /// <summary>
    /// Gets or sets links related to the process.
    /// </summary>
    [JsonPropertyName("links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProcessLink>? Links { get; init; }
}
