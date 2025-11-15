// <copyright file="AlertHistoryRecord.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

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

    public string DeliveryStatus { get; set; } = "Pending";

    public string? FailedChannelsJson { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset? LastRetryAttempt { get; set; }
}
