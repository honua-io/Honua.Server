// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new alert rule.
/// </summary>
public sealed record CreateAlertRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Severity { get; init; }
    public required Dictionary<string, string> Matchers { get; init; } = new();
    public List<long> NotificationChannelIds { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Request to update an existing alert rule.
/// </summary>
public sealed record UpdateAlertRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Severity { get; init; }
    public required Dictionary<string, string> Matchers { get; init; } = new();
    public List<long> NotificationChannelIds { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Response containing alert rule details.
/// </summary>
public sealed record AlertRuleResponse
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Severity { get; init; }
    public Dictionary<string, string> Matchers { get; init; } = new();
    public List<long> NotificationChannelIds { get; init; } = new();
    public bool Enabled { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? ModifiedBy { get; init; }
}

/// <summary>
/// Lightweight alert rule list item for list views.
/// </summary>
public sealed record AlertRuleListItem
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string Severity { get; init; }
    public bool Enabled { get; init; }
    public int ChannelCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Request to test an alert rule.
/// </summary>
public sealed record TestAlertRuleRequest
{
    public Dictionary<string, string>? TestLabels { get; init; }
    public string? TestMessage { get; init; }
}

/// <summary>
/// Response from testing an alert rule.
/// </summary>
public sealed record TestAlertRuleResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<string> PublishedChannels { get; init; } = new();
    public List<string> FailedChannels { get; init; } = new();
}
