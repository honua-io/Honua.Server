// <copyright file="AlertSilencingRuleRecord.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

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
