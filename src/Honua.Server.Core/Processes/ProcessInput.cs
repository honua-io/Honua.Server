// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents a process input parameter as defined in OGC API - Processes.
/// </summary>
public sealed class ProcessInput
{
    /// <summary>
    /// Gets or sets the title of the input.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the description of the input.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the JSON schema defining the input structure.
    /// </summary>
    [JsonPropertyName("schema")]
    public required object Schema { get; init; }

    /// <summary>
    /// Gets or sets the minimum number of occurrences for this input.
    /// </summary>
    [JsonPropertyName("minOccurs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinOccurs { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of occurrences for this input.
    /// Null or "unbounded" means unlimited.
    /// </summary>
    [JsonPropertyName("maxOccurs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOccurs { get; init; }

    /// <summary>
    /// Gets or sets keywords associated with the input.
    /// </summary>
    [JsonPropertyName("keywords")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Keywords { get; init; }
}
