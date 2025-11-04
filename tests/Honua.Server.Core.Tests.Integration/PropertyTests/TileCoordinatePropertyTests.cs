using System;
using FsCheck;
using FsCheck.Xunit;
using Honua.Server.Host.Ogc;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for tile coordinate calculations and transformations.
/// Tests Z/X/Y to bounding box conversions and inverse operations.
/// </summary>
public class TileCoordinatePropertyTests
{
    // Tile Coordinate to BBox Conversion Properties

    [Property(MaxTest = 500)]
    public Property TileToBBox_ShouldProduceValidBoundingBox()
    {
        return Prop.ForAll(
            GenerateValidTileCoordinate(),
            coord =>
            {
                var (zoom, row, col) = coord;
                var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

                Assert.NotNull(bbox);
                Assert.Equal(4, bbox.Length);

                var (minX, minY, maxX, maxY) = (bbox[0], bbox[1], bbox[2], bbox[3]);

                // MinX < MaxX, MinY < MaxY
                Assert.True(minX < maxX, $"MinX ({minX}) should be < MaxX ({maxX})");
                Assert.True(minY < maxY, $"MinY ({minY}) should be < MaxY ({maxY})");

                // Should be within WGS84 bounds
                Assert.InRange(minX, -180, 180);
                Assert.InRange(maxX, -180, 180);
                Assert.InRange(minY, -90, 90);
                Assert.InRange(maxY, -90, 90);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TileToBBox_AdjacentTiles_ShouldShareEdges()
    {
        return Prop.ForAll(
            GenerateAdjacentTilePair(),
            pair =>
            {
                var ((zoom, row, col), direction) = pair;
                var bbox1 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

                int row2 = row, col2 = col;
                switch (direction)
                {
                    case Direction.East:
                        col2 = col + 1;
                        break;
                    case Direction.West:
                        col2 = col - 1;
                        break;
                    case Direction.North:
                        row2 = row - 1;
                        break;
                    case Direction.South:
                        row2 = row + 1;
                        break;
                }

                if (!OgcTileMatrixHelper.IsValidTileCoordinate(zoom, row2, col2))
                {
                    return true; // Skip invalid neighbors
                }

                var bbox2 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row2, col2);

                // Check that adjacent tiles share edges (within floating point tolerance)
                const double tolerance = 1e-10;
                switch (direction)
                {
                    case Direction.East:
                        Assert.True(Math.Abs(bbox1[2] - bbox2[0]) < tolerance,
                            $"East neighbor should share edge: {bbox1[2]} vs {bbox2[0]}");
                        break;
                    case Direction.West:
                        Assert.True(Math.Abs(bbox1[0] - bbox2[2]) < tolerance,
                            $"West neighbor should share edge: {bbox1[0]} vs {bbox2[2]}");
                        break;
                    case Direction.North:
                        Assert.True(Math.Abs(bbox1[3] - bbox2[1]) < tolerance,
                            $"North neighbor should share edge: {bbox1[3]} vs {bbox2[1]}");
                        break;
                    case Direction.South:
                        Assert.True(Math.Abs(bbox1[1] - bbox2[3]) < tolerance,
                            $"South neighbor should share edge: {bbox1[1]} vs {bbox2[3]}");
                        break;
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TileToBBox_WebMercator_ShouldBeWithinProjectionBounds()
    {
        return Prop.ForAll(
            GenerateValidTileCoordinate(),
            coord =>
            {
                var (zoom, row, col) = coord;
                var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldWebMercatorQuad", zoom, row, col);

                const double webMercatorMin = -20037508.3427892;
                const double webMercatorMax = 20037508.3427892;
                const double tolerance = 1e-6;
                Assert.True(bbox[0] >= webMercatorMin - tolerance && bbox[0] <= webMercatorMax + tolerance,
                    $"MinX {bbox[0]} out of WebMercator bounds");
                Assert.True(bbox[1] >= webMercatorMin - tolerance && bbox[1] <= webMercatorMax + tolerance,
                    $"MinY {bbox[1]} out of WebMercator bounds");
                Assert.True(bbox[2] >= webMercatorMin - tolerance && bbox[2] <= webMercatorMax + tolerance,
                    $"MaxX {bbox[2]} out of WebMercator bounds");
                Assert.True(bbox[3] >= webMercatorMin - tolerance && bbox[3] <= webMercatorMax + tolerance,
                    $"MaxY {bbox[3]} out of WebMercator bounds");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property TileSize_ShouldBeConsistent_AcrossSameZoomLevel()
    {
        return Prop.ForAll(
            GenerateTilePairSameZoom(),
            pair =>
            {
                var ((zoom, row1, col1), (_, row2, col2)) = pair;

                var bbox1 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row1, col1);
                var bbox2 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row2, col2);

                var width1 = bbox1[2] - bbox1[0];
                var height1 = bbox1[3] - bbox1[1];
                var width2 = bbox2[2] - bbox2[0];
                var height2 = bbox2[3] - bbox2[1];

                // All tiles at same zoom should have same dimensions
                const double tolerance = 1e-10;
                Assert.True(Math.Abs(width1 - width2) < tolerance,
                    $"Tile widths should be equal at zoom {zoom}: {width1} vs {width2}");
                Assert.True(Math.Abs(height1 - height2) < tolerance,
                    $"Tile heights should be equal at zoom {zoom}: {height1} vs {height2}");

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property TileSize_ShouldHalve_WhenZoomIncreases()
    {
        return Prop.ForAll(
            GenerateConsecutiveZoomCoordinates(),
            coords =>
            {
                var (zoom1, row, col) = coords;
                var zoom2 = zoom1 + 1;

                var bbox1 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom1, row, col);
                var bbox2 = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom2, row * 2, col * 2);

                var width1 = bbox1[2] - bbox1[0];
                var height1 = bbox1[3] - bbox1[1];
                var width2 = bbox2[2] - bbox2[0];
                var height2 = bbox2[3] - bbox2[1];

                // Tile size should approximately halve
                const double tolerance = 1e-9;
                Assert.True(Math.Abs(width2 * 2 - width1) < tolerance,
                    $"Tile width should halve: {width1} -> {width2}");
                Assert.True(Math.Abs(height2 * 2 - height1) < tolerance,
                    $"Tile height should halve: {height1} -> {height2}");

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TileCount_ShouldDouble_WhenZoomIncreases()
    {
        return Prop.ForAll(
            Gen.Choose(0, 18).ToArbitrary(),
            new Func<int, bool>(zoom =>
            {
                var tilesAtZoom = 1 << zoom;
                var tilesAtNextZoom = 1 << (zoom + 1);

                Assert.Equal(tilesAtZoom * 2, tilesAtNextZoom);

                // Verify total tile count (tiles^2)
                var totalTilesAtZoom = (long)tilesAtZoom * tilesAtZoom;
                var totalTilesAtNextZoom = (long)tilesAtNextZoom * tilesAtNextZoom;

                Assert.Equal(totalTilesAtZoom * 4, totalTilesAtNextZoom);

                return true;
            }));
    }

    [Property(MaxTest = 200)]
    public Property ZoomRange_ShouldBeResolved_FromLevelList()
    {
        return Prop.ForAll(
            GenerateZoomLevelList(),
            zoomLevels =>
            {
                if (zoomLevels.Count == 0)
                {
                    var (min, max) = OgcTileMatrixHelper.ResolveZoomRange(zoomLevels);
                    Assert.Equal(0, min);
                    Assert.Equal(14, max);
                    return true;
                }

                var (minZoom, maxZoom) = OgcTileMatrixHelper.ResolveZoomRange(zoomLevels);

                Assert.True(minZoom <= maxZoom);

                var validLevels = zoomLevels.Where(z => z >= 0).ToList();
                if (validLevels.Any())
                {
                    Assert.Equal(validLevels.Min(), minZoom);
                    Assert.Equal(validLevels.Max(), maxZoom);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property TileCoordinate_Idempotency_RoundTrip()
    {
        return Prop.ForAll(
            GenerateValidTileCoordinate(),
            coord =>
            {
                var (zoom, row, col) = coord;

                // Get bbox
                var bbox = OgcTileMatrixHelper.GetBoundingBox("WorldCRS84Quad", zoom, row, col);

                // Center point of tile
                var centerX = (bbox[0] + bbox[2]) / 2;
                var centerY = (bbox[1] + bbox[3]) / 2;

                // Calculate which tile contains this center point
                var tilesPerAxis = 1 << zoom;
                var tileWidth = 360.0 / tilesPerAxis;
                var tileHeight = 180.0 / tilesPerAxis;

                var calculatedCol = (int)Math.Floor((centerX + 180) / tileWidth);
                var calculatedRow = (int)Math.Floor((90 - centerY) / tileHeight);

                // Should round-trip to same tile
                Assert.Equal(row, calculatedRow);
                Assert.Equal(col, calculatedCol);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property TileMatrixSet_Identification_ShouldWorkForAllVariants()
    {
        return Prop.ForAll(
            GenerateTileMatrixSetVariants(),
            identifier =>
            {
                var isSupported = OgcTileMatrixHelper.IsSupportedMatrixSet(identifier);

                if (identifier.Contains("CRS84", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(OgcTileMatrixHelper.IsWorldCrs84Quad(identifier));
                }
                else if (identifier.Contains("WebMercator", StringComparison.OrdinalIgnoreCase) ||
                         identifier.Contains("3857", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(OgcTileMatrixHelper.IsWorldWebMercatorQuad(identifier));
                }

                return true;
            });
    }

    // FsCheck Generators

    private static Arbitrary<(int, int, int)> GenerateValidTileCoordinate()
    {
        var gen = from zoom in Gen.Choose(0, 18)
                  let dimension = 1 << zoom
                  from row in Gen.Choose(0, dimension - 1)
                  from col in Gen.Choose(0, dimension - 1)
                  select (zoom, row, col);

        return Arb.From(gen);
    }

    private static Arbitrary<((int, int, int), Direction)> GenerateAdjacentTilePair()
    {
        var gen = from zoom in Gen.Choose(0, 15)
                  let dimension = 1 << zoom
                  from row in Gen.Choose(1, dimension - 2) // Not on edges
                  from col in Gen.Choose(1, dimension - 2)
                  from direction in Gen.Elements(
                      Direction.North, Direction.South,
                      Direction.East, Direction.West)
                  select ((zoom, row, col), direction);

        return Arb.From(gen);
    }

    private static Arbitrary<((int, int, int), (int, int, int))> GenerateTilePairSameZoom()
    {
        var gen = from zoom in Gen.Choose(0, 15)
                  let dimension = 1 << zoom
                  from row1 in Gen.Choose(0, dimension - 1)
                  from col1 in Gen.Choose(0, dimension - 1)
                  from row2 in Gen.Choose(0, dimension - 1)
                  from col2 in Gen.Choose(0, dimension - 1)
                  select ((zoom, row1, col1), (zoom, row2, col2));

        return Arb.From(gen);
    }

    private static Arbitrary<(int, int, int)> GenerateConsecutiveZoomCoordinates()
    {
        var gen = from zoom in Gen.Choose(0, 17)
                  let dimension = 1 << zoom
                  from row in Gen.Choose(0, dimension - 1)
                  from col in Gen.Choose(0, dimension - 1)
                  select (zoom, row, col);

        return Arb.From(gen);
    }

    private static Arbitrary<IReadOnlyList<int>> GenerateZoomLevelList()
    {
        var gen = Gen.OneOf(
            Gen.Constant(new List<int>() as IReadOnlyList<int>),
            Gen.ListOf(Gen.Choose(0, 20)).Select(l => (IReadOnlyList<int>)new List<int>(l)),
            Gen.ListOf(Gen.Choose(-5, 25)).Select(l => (IReadOnlyList<int>)new List<int>(l))
        );

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateTileMatrixSetVariants()
    {
        var variants = new[]
        {
            "WorldCRS84Quad",
            "worldcrs84quad",
            "WORLDCRS84QUAD",
            "http://www.opengis.net/def/tms/OGC/1.0/WorldCRS84Quad",
            "WorldWebMercatorQuad",
            "worldwebmercatorquad",
            "WORLDWEBMERCATORQUAD",
            "http://www.opengis.net/def/tms/OGC/1.0/WorldWebMercatorQuad"
        };

        return Arb.From(Gen.Elements(variants));
    }

    private enum Direction
    {
        North,
        South,
        East,
        West
    }
}
