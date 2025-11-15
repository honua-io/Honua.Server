// <copyright file="AlertHistoryEntry.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Honua.Server.AlertReceiver.Data;

public sealed class AlertHistoryEntry
{
    public long Id { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? Description { get; set; }

    public string Source { get; set; } = string.Empty;

    public string? Service { get; set; }

    public string? Environment { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    public Dictionary<string, object?>? Context { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string[] PublishedTo { get; set; } = Array.Empty<string>();

    public bool WasSuppressed { get; set; }

    public string? SuppressionReason { get; set; }

    public AlertDeliveryStatus DeliveryStatus { get; set; } = AlertDeliveryStatus.Pending;

    public string[] FailedChannels { get; set; } = Array.Empty<string>();

    public int RetryCount { get; set; }

    public DateTimeOffset? LastRetryAttempt { get; set; }

    internal static AlertHistoryEntry FromRecord(AlertHistoryRecord record)
    {
        return new AlertHistoryEntry
        {
            Id = record.Id,
            Fingerprint = record.Fingerprint,
            Name = record.Name,
            Severity = record.Severity,
            Status = record.Status,
            Summary = record.Summary,
            Description = record.Description,
            Source = record.Source,
            Service = record.Service,
            Environment = record.Environment,
            Labels = NormalizeLabels(Deserialize<Dictionary<string, string>>(record.LabelsJson)),
            Context = NormalizeContext(Deserialize<Dictionary<string, object?>>(record.ContextJson)),
            Timestamp = record.Timestamp,
            PublishedTo = Deserialize<string[]>(record.PublishedToJson) ?? Array.Empty<string>(),
            WasSuppressed = record.WasSuppressed,
            SuppressionReason = record.SuppressionReason,
            DeliveryStatus = Enum.TryParse<AlertDeliveryStatus>(record.DeliveryStatus, out var status) ? status : AlertDeliveryStatus.Pending,
            FailedChannels = Deserialize<string[]>(record.FailedChannelsJson) ?? Array.Empty<string>(),
            RetryCount = record.RetryCount,
            LastRetryAttempt = record.LastRetryAttempt,
        };
    }

    internal AlertHistoryRecord ToRecord()
    {
        return new AlertHistoryRecord
        {
            Id = this.Id,
            Fingerprint = this.Fingerprint,
            Name = this.Name,
            Severity = this.Severity,
            Status = this.Status,
            Summary = this.Summary,
            Description = this.Description,
            Source = this.Source,
            Service = this.Service,
            Environment = this.Environment,
            LabelsJson = this.Labels?.Count > 0 ? JsonSerializer.Serialize(this.Labels) : null,
            ContextJson = this.Context is { Count: > 0 } ? JsonSerializer.Serialize(this.Context) : null,
            Timestamp = this.Timestamp,
            PublishedToJson = this.PublishedTo.Length > 0 ? JsonSerializer.Serialize(this.PublishedTo) : "[]",
            WasSuppressed = this.WasSuppressed,
            SuppressionReason = this.SuppressionReason,
            DeliveryStatus = this.DeliveryStatus.ToString(),
            FailedChannelsJson = this.FailedChannels.Length > 0 ? JsonSerializer.Serialize(this.FailedChannels) : "[]",
            RetryCount = this.RetryCount,
            LastRetryAttempt = this.LastRetryAttempt,
        };
    }

    private static Dictionary<string, string>? NormalizeLabels(Dictionary<string, string>? labels)
    {
        if (labels is null)
        {
            return null;
        }

        return new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?>? NormalizeContext(Dictionary<string, object?>? context)
    {
        if (context is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(context, StringComparer.OrdinalIgnoreCase);
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}
