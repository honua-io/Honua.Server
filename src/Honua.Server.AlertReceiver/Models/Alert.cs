// <copyright file="Alert.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json.Serialization;

namespace Honua.Server.AlertReceiver.Models;

public sealed class Alert
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    // BUG FIX #34: Use case-insensitive dictionary for label matching
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("startsAt")]
    // BUG FIX #35: Use DateTimeOffset to preserve timezone information
    public DateTimeOffset StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTimeOffset? EndsAt { get; set; }

    [JsonPropertyName("generatorURL")]
    public string GeneratorUrl { get; set; } = string.Empty;

    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;
}
