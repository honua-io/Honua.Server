// <copyright file="AlertSilencingRule.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Honua.Server.AlertReceiver.Data;

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
            IsActive = record.IsActive,
        };
    }

    internal AlertSilencingRuleRecord ToRecord()
    {
        return new AlertSilencingRuleRecord
        {
            Id = this.Id,
            Name = this.Name,
            MatchersJson = this.Matchers.Count > 0 ? JsonSerializer.Serialize(this.Matchers) : "{}",
            CreatedBy = this.CreatedBy,
            CreatedAt = this.CreatedAt,
            StartsAt = this.StartsAt,
            EndsAt = this.EndsAt,
            Comment = this.Comment,
            IsActive = this.IsActive,
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
