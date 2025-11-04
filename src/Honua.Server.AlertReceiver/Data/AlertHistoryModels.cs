// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
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
            SuppressionReason = record.SuppressionReason
        };
    }

    internal AlertHistoryRecord ToRecord()
    {
        return new AlertHistoryRecord
        {
            Id = Id,
            Fingerprint = Fingerprint,
            Name = Name,
            Severity = Severity,
            Status = Status,
            Summary = Summary,
            Description = Description,
            Source = Source,
            Service = Service,
            Environment = Environment,
            LabelsJson = Labels?.Count > 0 ? JsonSerializer.Serialize(Labels) : null,
            ContextJson = Context is { Count: > 0 } ? JsonSerializer.Serialize(Context) : null,
            Timestamp = Timestamp,
            PublishedToJson = PublishedTo.Length > 0 ? JsonSerializer.Serialize(PublishedTo) : "[]",
            WasSuppressed = WasSuppressed,
            SuppressionReason = SuppressionReason
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

public sealed class AlertAcknowledgement
{
    public long Id { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string AcknowledgedBy { get; set; } = string.Empty;
    public DateTimeOffset AcknowledgedAt { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class AlertSilencingRule
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Matchers { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;

    internal static AlertSilencingRule FromRecord(AlertSilencingRuleRecord record)
    {
        return new AlertSilencingRule
        {
            Id = record.Id,
            Name = record.Name,
            Matchers = DeserializeMatchers(record.MatchersJson),
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAt,
            StartsAt = record.StartsAt,
            EndsAt = record.EndsAt,
            Comment = record.Comment,
            IsActive = record.IsActive
        };
    }

    internal AlertSilencingRuleRecord ToRecord()
    {
        return new AlertSilencingRuleRecord
        {
            Id = Id,
            Name = Name,
            MatchersJson = Matchers.Count > 0 ? JsonSerializer.Serialize(Matchers) : "{}",
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            StartsAt = StartsAt,
            EndsAt = EndsAt,
            Comment = Comment,
            IsActive = IsActive
        };
    }

    private static Dictionary<string, string> DeserializeMatchers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return result is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

internal sealed class AlertHistoryRecord
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
    public string? LabelsJson { get; set; }
    public string? ContextJson { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? PublishedToJson { get; set; }
    public bool WasSuppressed { get; set; }
    public string? SuppressionReason { get; set; }
}

internal sealed class AlertSilencingRuleRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MatchersJson { get; set; } = "{}";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; }
}
