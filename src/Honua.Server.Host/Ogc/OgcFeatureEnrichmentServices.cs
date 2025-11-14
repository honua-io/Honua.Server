// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Optional services for enriching features with additional data.
/// Groups optional post-processing services that augment feature data.
/// </summary>
public sealed record OgcFeatureEnrichmentServices
{
    /// <summary>
    /// Enriches features with elevation data if available.
    /// Used for 3D feature queries (Include3D parameter).
    /// Null if elevation service is not available or configured.
    /// </summary>
    public Core.Elevation.IElevationService? Elevation { get; init; }
}
