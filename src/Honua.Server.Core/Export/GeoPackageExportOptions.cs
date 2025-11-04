// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Export;

/// <summary>
/// Configuration controlling GeoPackage export behaviour.
/// </summary>
public sealed record GeoPackageExportOptions
{
    /// <summary>
    /// Default options used when no configuration is supplied.
    /// </summary>
    public static GeoPackageExportOptions Default { get; } = new();

    /// <summary>
    /// Maximum number of features to include in a single export. A value of <c>null</c> removes the cap.
    /// </summary>
    public long? MaxFeatures { get; init; } = null;

    /// <summary>
    /// Number of records persisted in a single SQLite transaction batch.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    public GeoPackageExportOptions Validate()
    {
        if (BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "Batch size must be positive.");
        }

        if (MaxFeatures <= 0)
        {
            return this with { MaxFeatures = null };
        }

        return this;
    }
}
