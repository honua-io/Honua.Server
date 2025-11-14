// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Customer or organization information for a build job.
/// </summary>
public sealed record CustomerInfo
{
    /// <summary>
    /// Unique customer identifier.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Customer display name.
    /// </summary>
    public required string CustomerName { get; init; }

    /// <summary>
    /// Customer email for notifications.
    /// </summary>
    public required string CustomerEmail { get; init; }
}
