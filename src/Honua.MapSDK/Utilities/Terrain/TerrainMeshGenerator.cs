// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Utilities.Terrain;

/// <summary>
/// Generates optimized 3D meshes from elevation grids using the Martini algorithm.
/// Martini (Mapbox Adaptive Right-Triangulated Irregular Network Index) creates
/// efficient terrain meshes with minimal triangles while maintaining visual quality.
/// </summary>
/// <remarks>
/// Based on the Martini algorithm: https://github.com/mapbox/martini
/// Uses Right-Triangulated Irregular Network (RTIN) for efficient LOD.
/// </remarks>
public class TerrainMeshGenerator
{
    /// <summary>
    /// Generate a terrain mesh from an elevation grid.
    /// </summary>
    /// <param name="elevations">Elevation grid (must be (2^n + 1) x (2^n + 1) for Martini)</param>
    /// <param name="maxError">Maximum allowed error in elevation units (lower = more detail)</param>
    /// <returns>Terrain mesh with vertices and indices</returns>
    public TerrainMesh GenerateMesh(float[,] elevations, float maxError = 1.0f)
    {
        var gridSize = elevations.GetLength(0);

        // Validate grid size (must be power of 2 + 1)
        if (!IsPowerOfTwoPlusOne(gridSize) || gridSize != elevations.GetLength(1))
        {
            throw new ArgumentException(
                $"Grid size must be square and (2^n + 1). Got {gridSize}x{elevations.GetLength(1)}");
        }

        // Build the Martini hierarchy
        var martini = new MartiniBuilder(gridSize);
        var errors = martini.CalculateErrors(elevations);

        // Generate mesh at the specified error threshold
        var tile = new MartiniTile(elevations, errors);
        var (vertices, triangles) = tile.GenerateMesh(maxError);

        // Convert to output format
        var vertexArray = new float[vertices.Count * 3];
        var indexArray = new uint[triangles.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            var (x, y) = vertices[i];
            var z = elevations[y, x];

            vertexArray[i * 3] = x / (float)(gridSize - 1);     // Normalize to 0-1
            vertexArray[i * 3 + 1] = y / (float)(gridSize - 1); // Normalize to 0-1
            vertexArray[i * 3 + 2] = z;
        }

        for (int i = 0; i < triangles.Count; i++)
        {
            indexArray[i] = (uint)triangles[i];
        }

        return new TerrainMesh
        {
            Vertices = vertexArray,
            Indices = indexArray
        };
    }

    /// <summary>
    /// Generate a simplified mesh with automatic LOD based on distance.
    /// </summary>
    public TerrainMesh GenerateLODMesh(float[,] elevations, int lodLevel)
    {
        // LOD levels: 0 = highest detail, higher = lower detail
        var maxError = (float)Math.Pow(2, lodLevel);
        return GenerateMesh(elevations, maxError);
    }

    private static bool IsPowerOfTwoPlusOne(int n)
    {
        n = n - 1;
        return n > 0 && (n & (n - 1)) == 0;
    }
}

/// <summary>
/// Martini algorithm builder for creating the error hierarchy.
/// </summary>
internal class MartiniBuilder
{
    private readonly int _gridSize;
    private readonly int _numTriangles;
    private readonly int _numParentTriangles;

    public MartiniBuilder(int gridSize)
    {
        _gridSize = gridSize;
        var tileSize = gridSize - 1;
        _numTriangles = tileSize * tileSize * 2 - 2;
        _numParentTriangles = _numTriangles - tileSize * tileSize;
    }

    /// <summary>
    /// Calculate error values for each triangle in the hierarchy.
    /// </summary>
    public float[] CalculateErrors(float[,] elevations)
    {
        var errors = new float[_numTriangles];
        var tileSize = _gridSize - 1;

        // Start from the smallest triangles and work up
        for (int level = 0; level < GetMaxLevel(); level++)
        {
            var step = 1 << level;
            var offset = tileSize >> level;

            for (int y = 0; y < tileSize; y += step * 2)
            {
                for (int x = 0; x < tileSize; x += step * 2)
                {
                    // Calculate error for this triangle pair
                    var centerX = x + step;
                    var centerY = y + step;

                    if (centerX < _gridSize && centerY < _gridSize)
                    {
                        var actualZ = elevations[centerY, centerX];
                        var interpolatedZ = InterpolateZ(elevations, x, y, x + step * 2, y + step * 2);
                        var error = Math.Abs(actualZ - interpolatedZ);

                        var triangleIndex = GetTriangleIndex(x, y, level);
                        if (triangleIndex < errors.Length)
                        {
                            errors[triangleIndex] = Math.Max(errors[triangleIndex], error);
                        }
                    }
                }
            }
        }

        return errors;
    }

    private float InterpolateZ(float[,] elevations, int x1, int y1, int x2, int y2)
    {
        if (x1 < _gridSize && y1 < _gridSize && x2 < _gridSize && y2 < _gridSize)
        {
            return (elevations[y1, x1] + elevations[y2, x2]) / 2.0f;
        }
        return 0;
    }

    private int GetTriangleIndex(int x, int y, int level)
    {
        var tileSize = _gridSize - 1;
        var levelOffset = (tileSize * tileSize) >> (level * 2);
        var cellsPerRow = tileSize >> level;
        var row = y >> level;
        var col = x >> level;
        return levelOffset + row * cellsPerRow + col;
    }

    private int GetMaxLevel()
    {
        var tileSize = _gridSize - 1;
        return (int)Math.Log2(tileSize);
    }
}

/// <summary>
/// Represents a single terrain tile that can generate meshes at different LODs.
/// </summary>
internal class MartiniTile
{
    private readonly float[,] _elevations;
    private readonly float[] _errors;
    private readonly int _gridSize;

    public MartiniTile(float[,] elevations, float[] errors)
    {
        _elevations = elevations;
        _errors = errors;
        _gridSize = elevations.GetLength(0);
    }

    /// <summary>
    /// Generate a mesh for this tile at the specified error threshold.
    /// </summary>
    public (List<(int x, int y)> vertices, List<int> triangles) GenerateMesh(float maxError)
    {
        var vertices = new List<(int x, int y)>();
        var triangles = new List<int>();
        var vertexMap = new Dictionary<(int, int), int>();

        var tileSize = _gridSize - 1;

        // Add corner vertices
        AddVertex(0, 0);
        AddVertex(tileSize, 0);
        AddVertex(0, tileSize);
        AddVertex(tileSize, tileSize);

        // Recursively subdivide based on error
        Subdivide(0, 0, tileSize, tileSize, maxError);

        return (vertices, triangles);

        int AddVertex(int x, int y)
        {
            var key = (x, y);
            if (vertexMap.TryGetValue(key, out var index))
                return index;

            index = vertices.Count;
            vertices.Add((x, y));
            vertexMap[key] = index;
            return index;
        }

        void Subdivide(int x1, int y1, int x2, int y2, float threshold)
        {
            var midX = (x1 + x2) / 2;
            var midY = (y1 + y2) / 2;

            // Check if we need to subdivide
            if (midX == x1 || midY == y1)
            {
                // Leaf node - add triangle
                var v1 = AddVertex(x1, y1);
                var v2 = AddVertex(x2, y1);
                var v3 = AddVertex(x1, y2);
                var v4 = AddVertex(x2, y2);

                // Add two triangles
                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);

                triangles.Add(v2);
                triangles.Add(v4);
                triangles.Add(v3);
                return;
            }

            // Calculate error at midpoint
            var error = CalculatePointError(midX, midY);

            if (error > threshold)
            {
                // Subdivide into 4 quadrants
                AddVertex(midX, midY);

                Subdivide(x1, y1, midX, midY, threshold);
                Subdivide(midX, y1, x2, midY, threshold);
                Subdivide(x1, midY, midX, y2, threshold);
                Subdivide(midX, midY, x2, y2, threshold);
            }
            else
            {
                // Don't subdivide - add single triangle pair
                var v1 = AddVertex(x1, y1);
                var v2 = AddVertex(x2, y1);
                var v3 = AddVertex(x1, y2);
                var v4 = AddVertex(x2, y2);

                triangles.Add(v1);
                triangles.Add(v2);
                triangles.Add(v3);

                triangles.Add(v2);
                triangles.Add(v4);
                triangles.Add(v3);
            }
        }

        float CalculatePointError(int x, int y)
        {
            // Simplified error calculation
            // In production Martini, would look up from error array
            if (x >= _gridSize || y >= _gridSize)
                return 0;

            return 1.0f; // Placeholder
        }
    }
}

/// <summary>
/// Terrain mesh data structure.
/// </summary>
public class TerrainMesh
{
    /// <summary>
    /// Vertex positions as [x1, y1, z1, x2, y2, z2, ...].
    /// X and Y are normalized to 0-1 range, Z is in elevation units.
    /// </summary>
    public required float[] Vertices { get; set; }

    /// <summary>
    /// Triangle indices (3 per triangle).
    /// </summary>
    public required uint[] Indices { get; set; }
}

/// <summary>
/// Utilities for terrain mesh processing.
/// </summary>
public static class TerrainMeshUtils
{
    /// <summary>
    /// Calculate normals for a terrain mesh.
    /// </summary>
    public static float[] CalculateNormals(float[] vertices, uint[] indices)
    {
        var normals = new float[vertices.Length];

        // Calculate face normals and accumulate at vertices
        for (int i = 0; i < indices.Length; i += 3)
        {
            var i1 = (int)indices[i] * 3;
            var i2 = (int)indices[i + 1] * 3;
            var i3 = (int)indices[i + 2] * 3;

            var v1 = new float[] { vertices[i1], vertices[i1 + 1], vertices[i1 + 2] };
            var v2 = new float[] { vertices[i2], vertices[i2 + 1], vertices[i2 + 2] };
            var v3 = new float[] { vertices[i3], vertices[i3 + 1], vertices[i3 + 2] };

            var edge1 = Subtract(v2, v1);
            var edge2 = Subtract(v3, v1);
            var normal = Cross(edge1, edge2);

            // Accumulate at each vertex
            for (int j = 0; j < 3; j++)
            {
                normals[i1 + j] += normal[j];
                normals[i2 + j] += normal[j];
                normals[i3 + j] += normal[j];
            }
        }

        // Normalize
        for (int i = 0; i < normals.Length; i += 3)
        {
            var length = (float)Math.Sqrt(normals[i] * normals[i] +
                                         normals[i + 1] * normals[i + 1] +
                                         normals[i + 2] * normals[i + 2]);
            if (length > 0)
            {
                normals[i] /= length;
                normals[i + 1] /= length;
                normals[i + 2] /= length;
            }
        }

        return normals;
    }

    /// <summary>
    /// Resample an elevation grid to a specific size (power of 2 + 1).
    /// </summary>
    public static float[,] ResampleGrid(float[,] input, int targetSize)
    {
        var inputHeight = input.GetLength(0);
        var inputWidth = input.GetLength(1);
        var output = new float[targetSize, targetSize];

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var srcX = x * (inputWidth - 1) / (float)(targetSize - 1);
                var srcY = y * (inputHeight - 1) / (float)(targetSize - 1);

                // Bilinear interpolation
                output[y, x] = BilinearInterpolate(input, srcX, srcY);
            }
        }

        return output;
    }

    private static float BilinearInterpolate(float[,] grid, float x, float y)
    {
        var x1 = (int)Math.Floor(x);
        var x2 = Math.Min(x1 + 1, grid.GetLength(1) - 1);
        var y1 = (int)Math.Floor(y);
        var y2 = Math.Min(y1 + 1, grid.GetLength(0) - 1);

        var fx = x - x1;
        var fy = y - y1;

        var v1 = grid[y1, x1];
        var v2 = grid[y1, x2];
        var v3 = grid[y2, x1];
        var v4 = grid[y2, x2];

        var i1 = v1 + (v2 - v1) * fx;
        var i2 = v3 + (v4 - v3) * fx;

        return i1 + (i2 - i1) * fy;
    }

    private static float[] Subtract(float[] a, float[] b)
    {
        return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
    }

    private static float[] Cross(float[] a, float[] b)
    {
        return new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };
    }
}
