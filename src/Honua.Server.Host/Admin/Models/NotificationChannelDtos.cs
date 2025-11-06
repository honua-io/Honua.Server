// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Request to create a new notification channel.
/// </summary>
public sealed record CreateNotificationChannelRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; } // sns, slack, email, webhook, etc.
    public required Dictionary<string, string> Configuration { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public List<string> SeverityFilter { get; init; } = new(); // Empty = all severities
}

/// <summary>
/// Request to update an existing notification channel.
/// </summary>
public sealed record UpdateNotificationChannelRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Dictionary<string, string> Configuration { get; init; } = new();
    public bool Enabled { get; init; } = true;
    public List<string> SeverityFilter { get; init; } = new();
}

/// <summary>
/// Response containing notification channel details.
/// </summary>
public sealed record NotificationChannelResponse
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public Dictionary<string, string> Configuration { get; init; } = new();
    public bool Enabled { get; init; }
    public List<string> SeverityFilter { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? ModifiedBy { get; init; }
}

/// <summary>
/// Lightweight notification channel list item for list views.
/// </summary>
public sealed record NotificationChannelListItem
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Enabled { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Request to test a notification channel.
/// </summary>
public sealed record TestNotificationChannelRequest
{
    public string? TestMessage { get; init; }
}

/// <summary>
/// Response from testing a notification channel.
/// </summary>
public sealed record TestNotificationChannelResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public long? LatencyMs { get; init; }
}
