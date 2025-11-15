// <copyright file="AlertDeliveryResult.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

namespace Honua.Server.AlertReceiver.Models;

/// <summary>
/// Represents the result of attempting to deliver an alert to one or more channels.
/// </summary>
public sealed class AlertDeliveryResult
{
    /// <summary>
    /// Gets or sets the list of channels that successfully received the alert.
    /// </summary>
    public List<string> SuccessfulChannels { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of channels that failed to receive the alert.
    /// </summary>
    public List<string> FailedChannels { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether all channels succeeded.
    /// </summary>
    public bool AllSucceeded => this.FailedChannels.Count == 0 && this.SuccessfulChannels.Count > 0;

    /// <summary>
    /// Gets a value indicating whether all channels failed.
    /// </summary>
    public bool AllFailed => this.SuccessfulChannels.Count == 0;

    /// <summary>
    /// Gets a value indicating whether some channels succeeded and some failed.
    /// </summary>
    public bool PartiallyFailed => this.SuccessfulChannels.Count > 0 && this.FailedChannels.Count > 0;
}
