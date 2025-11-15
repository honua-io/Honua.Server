// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Build output artifacts and result links.
/// </summary>
public sealed record BuildArtifacts
{
    /// <summary>
    /// Path to the output artifact (e.g., S3 bucket location, file system path).
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// URL to the build artifact image or container.
    /// Used for direct download or container registry reference.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// URL for downloading the build artifact.
    /// </summary>
    public string? DownloadUrl { get; init; }
}
