// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Build execution progress tracking.
/// </summary>
public sealed record BuildBounds
{
    /// <summary>
    /// Overall job completion percentage (0-100).
    /// </summary>
    public required int ProgressPercent { get; init; }

    /// <summary>
    /// Description of the current build step.
    /// Examples: "Compiling sources", "Running tests", "Packaging artifacts".
    /// </summary>
    public string? CurrentStep { get; init; }
}
