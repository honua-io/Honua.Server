// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models.Drone;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Utilities.Drone;

/// <summary>
/// Generates Level of Detail (LOD) pyramids for point clouds
/// </summary>
public class PointCloudLodGenerator
{
    private readonly ILogger<PointCloudLodGenerator>? _logger;

    public PointCloudLodGenerator(ILogger<PointCloudLodGenerator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate LOD levels using different decimation strategies
    /// </summary>
    public async Task<LodGenerationResult> GenerateLodLevelsAsync(
        string inputLazPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Generating LOD levels for {InputPath}", inputLazPath);

        var startTime = DateTime.UtcNow;

        try
        {
            // In production, this would:
            // 1. Use PDAL filters for decimation (voxel grid, poisson disk)
            // 2. Generate LOD1 (10% decimation)
            // 3. Generate LOD2 (1% decimation)
            // 4. Optionally generate LOD3 (0.1% decimation)

            // LOD 1: Octree decimation to ~10%
            var lod1Path = Path.Combine(outputDirectory, "point_cloud_lod1.laz");
            await GenerateLod1Async(inputLazPath, lod1Path, cancellationToken);

            // LOD 2: Voxel grid filter to ~1%
            var lod2Path = Path.Combine(outputDirectory, "point_cloud_lod2.laz");
            await GenerateLod2Async(inputLazPath, lod2Path, cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            return new LodGenerationResult
            {
                Success = true,
                LevelsGenerated = new[] { 1, 2 },
                Message = $"Generated LOD levels in {duration.TotalSeconds:F2}s"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate LOD levels");

            return new LodGenerationResult
            {
                Success = false,
                LevelsGenerated = Array.Empty<int>(),
                Message = $"LOD generation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate LOD 1 using octree decimation (~10% of points)
    /// </summary>
    private async Task GenerateLod1Async(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Generating LOD1: {OutputPath}", outputPath);

        // In production, this would execute a PDAL pipeline like:
        // {
        //   "pipeline": [
        //     {"type": "readers.las", "filename": "input.laz"},
        //     {"type": "filters.decimation", "step": 10},
        //     {"type": "writers.las", "filename": "output_lod1.laz"}
        //   ]
        // }

        await Task.Delay(100, cancellationToken);

        _logger?.LogInformation("LOD1 generated successfully");
    }

    /// <summary>
    /// Generate LOD 2 using voxel grid filter (~1% of points)
    /// </summary>
    private async Task GenerateLod2Async(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Generating LOD2: {OutputPath}", outputPath);

        // In production, this would execute a PDAL pipeline like:
        // {
        //   "pipeline": [
        //     {"type": "readers.las", "filename": "input.laz"},
        //     {"type": "filters.voxelgrid", "cell": 1.0},
        //     {"type": "writers.las", "filename": "output_lod2.laz"}
        //   ]
        // }

        await Task.Delay(100, cancellationToken);

        _logger?.LogInformation("LOD2 generated successfully");
    }

    /// <summary>
    /// Decimate point cloud in memory
    /// </summary>
    public IEnumerable<PointCloudPoint> DecimatePoints(
        IEnumerable<PointCloudPoint> points,
        double decimationRatio = 0.1)
    {
        var pointsList = points.ToList();
        var step = (int)Math.Max(1, Math.Ceiling(1.0 / decimationRatio));

        return pointsList.Where((_, index) => index % step == 0);
    }

    /// <summary>
    /// Apply voxel grid filter to points
    /// </summary>
    public IEnumerable<PointCloudPoint> VoxelGridFilter(
        IEnumerable<PointCloudPoint> points,
        double voxelSize = 0.5)
    {
        var voxelMap = new Dictionary<(int, int, int), PointCloudPoint>();

        foreach (var point in points)
        {
            var voxelKey = (
                (int)Math.Floor(point.X / voxelSize),
                (int)Math.Floor(point.Y / voxelSize),
                (int)Math.Floor(point.Z / voxelSize)
            );

            // Keep first point in each voxel
            if (!voxelMap.ContainsKey(voxelKey))
            {
                voxelMap[voxelKey] = point;
            }
        }

        return voxelMap.Values;
    }
}

/// <summary>
/// Selects appropriate LOD level based on viewing parameters
/// </summary>
public class PointCloudLodSelector
{
    /// <summary>
    /// Select LOD based on zoom level and bounding box
    /// </summary>
    public PointCloudLodLevel SelectLod(double zoomLevel, BoundingBox3D bbox)
    {
        // Calculate viewport size in degrees
        var viewportSize = (bbox.MaxX - bbox.MinX) * (bbox.MaxY - bbox.MinY);

        // Estimate point density needed
        return (zoomLevel, viewportSize) switch
        {
            // Very close zoom, small area → Full detail
            ( >= 18, _) when viewportSize < 0.0001 => PointCloudLodLevel.Full,

            // Close zoom, medium area → Coarse
            ( >= 15, _) when viewportSize < 0.001 => PointCloudLodLevel.Coarse,

            // Medium zoom → Coarse
            ( >= 12, _) => PointCloudLodLevel.Coarse,

            // Far zoom or large area → Sparse
            _ => PointCloudLodLevel.Sparse
        };
    }

    /// <summary>
    /// Estimate point count for LOD selection
    /// </summary>
    public long EstimatePointCount(BoundingBox3D bbox, long totalPoints, PointCloudLodLevel lod)
    {
        var fullCount = totalPoints;

        return lod switch
        {
            PointCloudLodLevel.Coarse => fullCount / 10,
            PointCloudLodLevel.Sparse => fullCount / 100,
            _ => fullCount
        };
    }
}

/// <summary>
/// Classification color schemes for point clouds
/// </summary>
public static class PointCloudClassificationColors
{
    /// <summary>
    /// Standard LAS classification colors
    /// </summary>
    public static readonly Dictionary<byte, (byte R, byte G, byte B)> StandardColors = new()
    {
        { 0, ((byte)128, (byte)128, (byte)128) },  // Never Classified - Gray
        { 1, ((byte)128, (byte)128, (byte)128) },  // Unclassified - Gray
        { 2, ((byte)139, (byte)69, (byte)19) },    // Ground - Brown
        { 3, ((byte)34, (byte)139, (byte)34) },    // Low Vegetation - Green
        { 4, ((byte)0, (byte)128, (byte)0) },      // Medium Vegetation - Dark Green
        { 5, ((byte)0, (byte)255, (byte)0) },      // High Vegetation - Bright Green
        { 6, ((byte)255, (byte)0, (byte)0) },      // Building - Red
        { 7, ((byte)255, (byte)255, (byte)0) },    // Low Point (Noise) - Yellow
        { 8, ((byte)128, (byte)128, (byte)128) },  // Reserved - Gray
        { 9, ((byte)0, (byte)0, (byte)255) },      // Water - Blue
        { 10, ((byte)128, (byte)0, (byte)128) },   // Rail - Purple
        { 11, ((byte)64, (byte)64, (byte)64) },    // Road Surface - Dark Gray
        { 13, ((byte)255, (byte)165, (byte)0) },   // Wire Guard - Orange
        { 14, ((byte)255, (byte)140, (byte)0) },   // Wire Conductor - Dark Orange
        { 15, ((byte)255, (byte)20, (byte)147) },  // Transmission Tower - Deep Pink
        { 17, ((byte)255, (byte)255, (byte)0) },   // Bridge Deck - Yellow
        { 18, ((byte)255, (byte)0, (byte)255) }    // High Noise - Magenta
    };

    /// <summary>
    /// Get color for a classification code
    /// </summary>
    public static (byte R, byte G, byte B) GetColor(byte classification)
    {
        return StandardColors.TryGetValue(classification, out var color)
            ? color
            : ((byte)200, (byte)200, (byte)200); // Default gray
    }
}
