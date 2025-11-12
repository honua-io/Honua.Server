// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for STAC catalog storage.
/// </summary>
public sealed class StacCatalogOptions
{
    public const string SectionName = "Honua:Services:Stac";

    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "sqlite";
    public string? ConnectionString { get; init; }
    public string? FilePath { get; init; }
}
