// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.VectorTiles;

/// <summary>
/// Configuration options for vector tile generation
/// </summary>
public sealed record VectorTileOptions
{
    /// <summary>
    /// Tile extent in tile coordinates (default: 4096)
    /// </summary>
    public int Extent { get; init; } = 4096;

    /// <summary>
    /// Buffer size around tile edges in pixels (default: 256)
    /// Prevents geometry clipping at tile boundaries
    /// </summary>
    public int Buffer { get; init; } = 256;

    /// <summary>
    /// Enable overzooming for tiles beyond max zoom level
    /// </summary>
    public bool EnableOverzooming { get; init; } = true;

    /// <summary>
    /// Maximum zoom level for source data (default: 14)
    /// Tiles beyond this use overzooming
    /// </summary>
    public int MaxDataZoom { get; init; } = 14;

    /// <summary>
    /// Maximum zoom level to generate tiles (default: 22)
    /// </summary>
    public int MaxZoom { get; init; } = 22;

    /// <summary>
    /// Minimum zoom level to generate tiles (default: 0)
    /// </summary>
    public int MinZoom { get; init; } = 0;

    /// <summary>
    /// Enable geometry simplification based on zoom level
    /// </summary>
    public bool EnableSimplification { get; init; } = true;

    /// <summary>
    /// Simplification tolerance multiplier (default: 1.0)
    /// Higher values = more aggressive simplification
    /// </summary>
    public double SimplificationTolerance { get; init; } = 1.0;

    /// <summary>
    /// Enable feature reduction at lower zoom levels
    /// </summary>
    public bool EnableFeatureReduction { get; init; } = true;

    /// <summary>
    /// Minimum feature area in tile coordinates for inclusion
    /// Features smaller than this are dropped at lower zooms
    /// </summary>
    public double MinFeatureArea { get; init; } = 4.0;

    /// <summary>
    /// Enable attribute filtering to reduce tile size
    /// </summary>
    public bool EnableAttributeFiltering { get; init; } = false;

    /// <summary>
    /// Maximum number of attributes per feature (default: null = no limit)
    /// </summary>
    public int? MaxAttributesPerFeature { get; init; }

    /// <summary>
    /// Attributes to always include regardless of filtering
    /// </summary>
    public IReadOnlySet<string>? RequiredAttributes { get; init; }

    /// <summary>
    /// Enable automatic layer clustering at low zoom levels
    /// </summary>
    public bool EnableClustering { get; init; } = false;

    /// <summary>
    /// Cluster radius in pixels (default: 50)
    /// </summary>
    public int ClusterRadius { get; init; } = 50;

    /// <summary>
    /// Minimum zoom level for clustering (default: 0)
    /// </summary>
    public int ClusterMinZoom { get; init; } = 0;

    /// <summary>
    /// Maximum zoom level for clustering (default: 8)
    /// </summary>
    public int ClusterMaxZoom { get; init; } = 8;

    /// <summary>
    /// Default options for production use
    /// </summary>
    public static VectorTileOptions Default => new();

    /// <summary>
    /// High-performance options with aggressive optimization
    /// </summary>
    public static VectorTileOptions Performance => new()
    {
        EnableSimplification = true,
        SimplificationTolerance = 1.5,
        EnableFeatureReduction = true,
        MinFeatureArea = 8.0,
        EnableAttributeFiltering = true,
        MaxAttributesPerFeature = 10,
        EnableClustering = true
    };

    /// <summary>
    /// High-quality options with minimal optimization
    /// </summary>
    public static VectorTileOptions Quality => new()
    {
        EnableSimplification = true,
        SimplificationTolerance = 0.5,
        EnableFeatureReduction = false,
        EnableAttributeFiltering = false,
        EnableClustering = false
    };
}
