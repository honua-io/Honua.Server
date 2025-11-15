// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Unique identifier for a build job.
/// Kept separate from other parameter objects for clarity at call sites.
/// </summary>
public sealed record PerformanceMetrics
{
    /// <summary>
    /// Unique identifier for the build job.
    /// </summary>
    public required Guid Id { get; init; }
}
