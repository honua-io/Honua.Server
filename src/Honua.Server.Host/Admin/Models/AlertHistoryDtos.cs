// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Response containing alert history entry details.
/// </summary>
public sealed record AlertHistoryResponse
{
    public long Id { get; init; }
    public required string Fingerprint { get; init; }
    public required string Name { get; init; }
    public required string Severity { get; init; }
    public required string Status { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public required string Source { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
    public Dictionary<string, string>? Labels { get; init; }
    public Dictionary<string, object?>? Context { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public List<string> PublishedTo { get; init; } = new();
    public bool WasSuppressed { get; init; }
    public string? SuppressionReason { get; init; }
    public AlertAcknowledgementDto? Acknowledgement { get; init; }
}

/// <summary>
/// Lightweight alert history list item for list views.
/// </summary>
public sealed record AlertHistoryListItem
{
    public long Id { get; init; }
    public required string Fingerprint { get; init; }
    public required string Name { get; init; }
    public required string Severity { get; init; }
    public required string Status { get; init; }
    public string? Summary { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool WasSuppressed { get; init; }
    public bool IsAcknowledged { get; init; }
}

/// <summary>
/// Request to acknowledge an alert.
/// </summary>
public sealed record AcknowledgeAlertRequest
{
    public required string AcknowledgedBy { get; init; }
    public string? Comment { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Request to silence an alert.
/// </summary>
public sealed record SilenceAlertRequest
{
    public required string Name { get; init; }
    public required string CreatedBy { get; init; }
    public string? Comment { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
}

/// <summary>
/// Alert acknowledgement information.
/// </summary>
public sealed record AlertAcknowledgementDto
{
    public long Id { get; init; }
    public required string AcknowledgedBy { get; init; }
    public DateTimeOffset AcknowledgedAt { get; init; }
    public string? Comment { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Query parameters for filtering alert history.
/// </summary>
public sealed record AlertHistoryQueryRequest
{
    public string? Severity { get; init; }
    public string? Status { get; init; }
    public string? Service { get; init; }
    public string? Environment { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; } = 0;
}
