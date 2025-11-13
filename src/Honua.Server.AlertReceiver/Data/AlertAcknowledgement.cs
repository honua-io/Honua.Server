// <copyright file="AlertAcknowledgement.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

public sealed class AlertAcknowledgement
{
    public long Id { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string AcknowledgedBy { get; set; } = string.Empty;

    public DateTimeOffset AcknowledgedAt { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
