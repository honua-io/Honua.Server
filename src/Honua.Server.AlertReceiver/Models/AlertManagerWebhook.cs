// <copyright file="AlertManagerWebhook.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json.Serialization;

namespace Honua.Server.AlertReceiver.Models;

/// <summary>
/// AlertManager webhook payload structure.
/// </summary>
public sealed class AlertManagerWebhook
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("groupKey")]
    public string GroupKey { get; set; } = string.Empty;

    [JsonPropertyName("truncatedAlerts")]
    public int TruncatedAlerts { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("receiver")]
    public string Receiver { get; set; } = string.Empty;

    [JsonPropertyName("groupLabels")]
    // BUG FIX #34: Use case-insensitive dictionary for label matching
    public Dictionary<string, string> GroupLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("commonLabels")]
    public Dictionary<string, string> CommonLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("commonAnnotations")]
    public Dictionary<string, string> CommonAnnotations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("externalURL")]
    public string ExternalUrl { get; set; } = string.Empty;

    [JsonPropertyName("alerts")]
    public List<Alert> Alerts { get; set; } = new();
}
