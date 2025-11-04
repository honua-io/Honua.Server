// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Represents a process output parameter as defined in OGC API - Processes.
/// </summary>
public sealed class ProcessOutput
{
    /// <summary>
    /// Gets or sets the title of the output.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the description of the output.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the JSON schema defining the output structure.
    /// </summary>
    [JsonPropertyName("schema")]
    public required object Schema { get; init; }

    /// <summary>
    /// Gets or sets keywords associated with the output.
    /// </summary>
    [JsonPropertyName("keywords")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Keywords { get; init; }
}
