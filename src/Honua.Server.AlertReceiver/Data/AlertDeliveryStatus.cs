// <copyright file="AlertDeliveryStatus.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Represents the delivery status of an alert.
/// Used for implementing durable queue pattern with recovery on restart.
/// </summary>
public enum AlertDeliveryStatus
{
    /// <summary>
    /// Alert has been persisted but not yet sent to any channels.
    /// This is the initial state when using the durable queue pattern.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Alert was successfully sent to all configured channels.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Alert was sent to some channels but failed on others.
    /// Check FailedChannels property for details.
    /// </summary>
    PartiallyFailed = 2,

    /// <summary>
    /// Alert failed to send to all channels.
    /// Eligible for retry or DLQ processing.
    /// </summary>
    Failed = 3,
}
