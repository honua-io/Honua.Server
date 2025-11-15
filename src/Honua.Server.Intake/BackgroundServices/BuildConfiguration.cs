// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Intake.BackgroundServices;

/// <summary>
/// Build job configuration and target specifications.
/// </summary>
public sealed record BuildConfiguration
{
    /// <summary>
    /// Path to the build manifest file.
    /// Contains the specification of what to build.
    /// </summary>
    public required string ManifestPath { get; init; }

    /// <summary>
    /// Named build configuration (e.g., "debug", "release", "production").
    /// </summary>
    public required string ConfigurationName { get; init; }

    /// <summary>
    /// Service tier for the build (e.g., "standard", "premium", "enterprise").
    /// Determines resource allocation.
    /// </summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Target system architecture (e.g., "x86_64", "arm64").
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Target cloud provider (e.g., "aws", "azure", "gcp").
    /// </summary>
    public required string CloudProvider { get; init; }
}
