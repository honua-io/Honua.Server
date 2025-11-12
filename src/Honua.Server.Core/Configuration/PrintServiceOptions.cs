// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for the MapFish Print Service.
/// </summary>
public sealed class PrintServiceOptions
{
    public const string SectionName = "Honua:Services:Print";

    /// <summary>
    /// Whether the print service is enabled.
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Provider for print configurations (e.g., "json", "inline").
    /// </summary>
    public string Provider { get; init; } = "inline";

    /// <summary>
    /// Path to JSON configuration file if provider is "json".
    /// </summary>
    public string? ConfigurationPath { get; init; }
}
