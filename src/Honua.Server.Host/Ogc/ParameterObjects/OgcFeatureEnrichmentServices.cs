// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Optional services for enriching features with additional data.
/// </summary>
/// <remarks>
/// Feature enrichment adds supplementary information to features beyond their core attributes.
/// Currently supports elevation data enrichment, with potential for future enrichment types
/// such as geocoding, demographic data, or computed analytics.
///
/// All enrichment services are optional - features can be served without enrichment if the
/// service is not configured or available.
/// </remarks>
public sealed record OgcFeatureEnrichmentServices
{
    /// <summary>
    /// Enriches features with elevation data if available.
    /// Adds height/altitude information by sampling Digital Elevation Models (DEMs)
    /// at feature coordinate locations. Null if elevation enrichment is not configured.
    /// </summary>
    public Core.Elevation.IElevationService? Elevation { get; init; }
}
