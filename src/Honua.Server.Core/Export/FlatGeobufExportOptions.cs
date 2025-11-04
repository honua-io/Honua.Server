// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Export;

/// <summary>
/// Configuration controlling FlatGeobuf export behaviour.
/// </summary>
public sealed record FlatGeobufExportOptions
{
    /// <summary>
    /// Default options used when no configuration is supplied.
    /// </summary>
    public static FlatGeobufExportOptions Default { get; } = new();

    /// <summary>
    /// Maximum number of features to include in a single export.
    /// </summary>
    public long MaxFeatures { get; init; } = 1_000_000;

    public FlatGeobufExportOptions Validate()
    {
        if (MaxFeatures <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFeatures), MaxFeatures, "MaxFeatures must be positive.");
        }

        return this;
    }
}
