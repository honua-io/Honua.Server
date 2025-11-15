// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Diagnostic information for build failures.
/// </summary>
public sealed record BuildDiagnostics
{
    /// <summary>
    /// Error message if the build failed.
    /// Provides details about the failure cause.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
