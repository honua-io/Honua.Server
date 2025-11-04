// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Honua.Server.AlertReceiver.Models;

/// <summary>
/// Generic alert format for ingestion from any source.
/// Can be sent directly from application code, logs, health checks, etc.
/// </summary>
public sealed class GenericAlert
{
    /// <summary>
    /// Alert name/title.
    /// </summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Alert name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Alert name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alert severity: critical, high, medium, low, info.
    /// </summary>
    [JsonPropertyName("severity")]
    [Required(ErrorMessage = "Alert severity is required")]
    [StringLength(50, ErrorMessage = "Severity must be 50 characters or less")]
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// Alert status: firing, resolved.
    /// </summary>
    [JsonPropertyName("status")]
    [Required(ErrorMessage = "Alert status is required")]
    [StringLength(50, ErrorMessage = "Status must be 50 characters or less")]
    public string Status { get; set; } = "firing";

    /// <summary>
    /// Short summary of the alert.
    /// </summary>
    [JsonPropertyName("summary")]
    [StringLength(500, ErrorMessage = "Summary must be 500 characters or less")]
    public string? Summary { get; set; }

    /// <summary>
    /// Detailed description.
    /// </summary>
    [JsonPropertyName("description")]
    [StringLength(4000, ErrorMessage = "Description must be 4000 characters or less")]
    public string? Description { get; set; }

    /// <summary>
    /// Source of the alert (e.g., "honua-api", "health-check", "application-logs").
    /// </summary>
    [JsonPropertyName("source")]
    [Required(ErrorMessage = "Alert source is required")]
    [StringLength(256, ErrorMessage = "Source must be 256 characters or less")]
    public string Source { get; set; } = "unknown";

    /// <summary>
    /// Service or component identifier.
    /// </summary>
    [JsonPropertyName("service")]
    [StringLength(256, ErrorMessage = "Service must be 256 characters or less")]
    public string? Service { get; set; }

    /// <summary>
    /// Environment (e.g., "production", "staging", "development").
    /// </summary>
    [JsonPropertyName("environment")]
    [StringLength(100, ErrorMessage = "Environment must be 100 characters or less")]
    public string? Environment { get; set; }

    /// <summary>
    /// Additional labels/tags.
    /// </summary>
    [JsonPropertyName("labels")]
    [MaxLength(50, ErrorMessage = "Maximum 50 labels allowed")]
    // BUG FIX #34: Use case-insensitive dictionary for label matching
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When the alert started.
    /// </summary>
    [JsonPropertyName("timestamp")]
    // BUG FIX #32: Use DateTimeOffset to preserve timezone information
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unique identifier for deduplication.
    /// CRITICAL: Maximum 256 characters (enforced by database schema and validation).
    /// Fingerprints exceeding this limit are rejected to prevent hash collisions and alert storms.
    /// If not provided, auto-generated from source:name:service (always within limit).
    /// For custom fingerprints, use hashed identifiers (e.g., SHA256 hex = 64 chars).
    /// </summary>
    [JsonPropertyName("fingerprint")]
    [StringLength(256, ErrorMessage = "Fingerprint must be 256 characters or less")]
    public string? Fingerprint { get; set; }

    /// <summary>
    /// Additional contextual data.
    /// </summary>
    [JsonPropertyName("context")]
    [MaxLength(100, ErrorMessage = "Maximum 100 context entries allowed")]
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Batch of generic alerts.
/// </summary>
public sealed class GenericAlertBatch
{
    [JsonPropertyName("alerts")]
    [Required(ErrorMessage = "Alerts array is required")]
    [MaxLength(100, ErrorMessage = "Maximum 100 alerts per batch")]
    public List<GenericAlert> Alerts { get; set; } = new();
}
