// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents a process execution request.
/// </summary>
public sealed class ExecuteRequest
{
    /// <summary>
    /// Gets or sets the input values for the process.
    /// </summary>
    [JsonPropertyName("inputs")]
    public Dictionary<string, object>? Inputs { get; init; }

    /// <summary>
    /// Gets or sets the output definitions.
    /// </summary>
    [JsonPropertyName("outputs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, OutputDefinition>? Outputs { get; init; }

    /// <summary>
    /// Gets or sets the subscriber information for callbacks.
    /// </summary>
    [JsonPropertyName("subscriber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubscriberInfo? Subscriber { get; init; }
}

/// <summary>
/// Represents an output definition in an execution request.
/// </summary>
public sealed class OutputDefinition
{
    /// <summary>
    /// Gets or sets the transmission mode (value or reference).
    /// </summary>
    [JsonPropertyName("transmissionMode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransmissionMode { get; init; }

    /// <summary>
    /// Gets or sets the format.
    /// </summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OutputFormat? Format { get; init; }
}

/// <summary>
/// Represents an output format specification.
/// </summary>
public sealed class OutputFormat
{
    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; init; }

    /// <summary>
    /// Gets or sets the encoding.
    /// </summary>
    [JsonPropertyName("encoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Encoding { get; init; }

    /// <summary>
    /// Gets or sets the schema reference.
    /// </summary>
    [JsonPropertyName("schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }
}

/// <summary>
/// Represents subscriber information for async callbacks.
/// </summary>
public sealed class SubscriberInfo
{
    /// <summary>
    /// Gets or sets the success callback URL.
    /// </summary>
    [JsonPropertyName("successUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuccessUri { get; init; }

    /// <summary>
    /// Gets or sets the in-progress callback URL.
    /// </summary>
    [JsonPropertyName("inProgressUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InProgressUri { get; init; }

    /// <summary>
    /// Gets or sets the failure callback URL.
    /// </summary>
    [JsonPropertyName("failedUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FailedUri { get; init; }
}
