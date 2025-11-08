// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Alert rule model for Prometheus-style alerts.
/// </summary>
public sealed class AlertRuleModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("severity")]
    public required AlertSeverity Severity { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("expression")]
    public required string Expression { get; set; }

    [JsonPropertyName("duration")]
    public required string Duration { get; set; } = "5m";

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new();

    [JsonPropertyName("channelIds")]
    public List<string> ChannelIds { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// List item for alert rules.
/// </summary>
public sealed class AlertRuleListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("severity")]
    public required AlertSeverity Severity { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("lastFired")]
    public DateTimeOffset? LastFired { get; set; }

    [JsonPropertyName("firingCount")]
    public int FiringCount { get; set; }

    [JsonPropertyName("channelCount")]
    public int ChannelCount { get; set; }
}

/// <summary>
/// Request to create or update an alert rule.
/// </summary>
public sealed class CreateAlertRuleRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("severity")]
    public required AlertSeverity Severity { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("expression")]
    public required string Expression { get; set; }

    [JsonPropertyName("duration")]
    public required string Duration { get; set; } = "5m";

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new();

    [JsonPropertyName("channelIds")]
    public List<string> ChannelIds { get; set; } = new();
}

/// <summary>
/// Notification channel configuration.
/// </summary>
public sealed class NotificationChannelModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required NotificationChannelType Type { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("configuration")]
    public required Dictionary<string, string> Configuration { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// List item for notification channels.
/// </summary>
public sealed class NotificationChannelListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required NotificationChannelType Type { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("lastUsed")]
    public DateTimeOffset? LastUsed { get; set; }

    [JsonPropertyName("alertCount")]
    public int AlertCount { get; set; }
}

/// <summary>
/// Request to create or update a notification channel.
/// </summary>
public sealed class CreateNotificationChannelRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required NotificationChannelType Type { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("configuration")]
    public required Dictionary<string, string> Configuration { get; set; }
}

/// <summary>
/// Alert history entry.
/// </summary>
public sealed class AlertHistoryModel
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("ruleId")]
    public required string RuleId { get; set; }

    [JsonPropertyName("ruleName")]
    public required string RuleName { get; set; }

    [JsonPropertyName("severity")]
    public required AlertSeverity Severity { get; set; }

    [JsonPropertyName("status")]
    public required AlertStatus Status { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new();

    [JsonPropertyName("startsAt")]
    public required DateTimeOffset StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public DateTimeOffset? EndsAt { get; set; }

    [JsonPropertyName("notifiedChannels")]
    public List<string> NotifiedChannels { get; set; } = new();

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }
}

/// <summary>
/// Alert routing configuration.
/// </summary>
public sealed class AlertRoutingModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("matchers")]
    public List<AlertMatcher> Matchers { get; set; } = new();

    [JsonPropertyName("channelIds")]
    public List<string> ChannelIds { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

/// <summary>
/// Alert matcher for routing.
/// </summary>
public sealed class AlertMatcher
{
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("operator")]
    public required MatcherOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

/// <summary>
/// Alert silence configuration.
/// </summary>
public sealed class AlertSilenceModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("matchers")]
    public List<AlertMatcher> Matchers { get; set; } = new();

    [JsonPropertyName("startsAt")]
    public required DateTimeOffset StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public required DateTimeOffset EndsAt { get; set; }

    [JsonPropertyName("createdBy")]
    public required string CreatedBy { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("status")]
    public required SilenceStatus Status { get; set; }
}

/// <summary>
/// Request to test an alert rule.
/// </summary>
public sealed class TestAlertRequest
{
    [JsonPropertyName("ruleId")]
    public required string RuleId { get; set; }

    [JsonPropertyName("channelIds")]
    public List<string>? ChannelIds { get; set; }
}

/// <summary>
/// Response from testing an alert.
/// </summary>
public sealed class TestAlertResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("results")]
    public List<ChannelTestResult> Results { get; set; } = new();
}

/// <summary>
/// Channel test result.
/// </summary>
public sealed class ChannelTestResult
{
    [JsonPropertyName("channelId")]
    public required string ChannelId { get; set; }

    [JsonPropertyName("channelName")]
    public required string ChannelName { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Alert statistics for dashboard.
/// </summary>
public sealed class AlertStatsModel
{
    [JsonPropertyName("totalRules")]
    public int TotalRules { get; set; }

    [JsonPropertyName("enabledRules")]
    public int EnabledRules { get; set; }

    [JsonPropertyName("firingAlerts")]
    public int FiringAlerts { get; set; }

    [JsonPropertyName("totalChannels")]
    public int TotalChannels { get; set; }

    [JsonPropertyName("enabledChannels")]
    public int EnabledChannels { get; set; }

    [JsonPropertyName("silencedAlerts")]
    public int SilencedAlerts { get; set; }

    [JsonPropertyName("last24Hours")]
    public int Last24Hours { get; set; }

    [JsonPropertyName("last7Days")]
    public int Last7Days { get; set; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Alert status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertStatus
{
    Pending,
    Firing,
    Resolved,
    Silenced
}

/// <summary>
/// Notification channel types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannelType
{
    Email,
    Slack,
    SNS,
    Webhook,
    PagerDuty,
    Teams
}

/// <summary>
/// Matcher operators.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatcherOperator
{
    Equals,
    NotEquals,
    Matches,
    NotMatches
}

/// <summary>
/// Silence status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SilenceStatus
{
    Active,
    Pending,
    Expired
}
