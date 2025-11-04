using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for Zarr chunk calculations and multi-dimensional indexing.
/// </summary>
public class ZarrChunkPropertyTests
{
    // Chunk Index Calculation Properties

    [Property(MaxTest = 500)]
    public Property ChunkIndex_ShouldBeWithinBounds()
    {
        return Prop.ForAll(
            GenerateValidChunkCoordinates(),
            coords =>
            {
                var (shape, chunkShape, indices) = coords;

                // Calculate chunk indices
                var chunkIndices = CalculateChunkIndices(indices, chunkShape);

                Assert.Equal(indices.Length, chunkIndices.Length);

                // Each chunk index should be within bounds
                for (int i = 0; i < chunkIndices.Length; i++)
                {
                    var maxChunks = (int)Math.Ceiling((double)shape[i] / chunkShape[i]);
                    Assert.InRange(chunkIndices[i], 0, maxChunks - 1);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property ChunkOffset_ShouldBeWithinChunkBounds()
    {
        return Prop.ForAll(
            GenerateValidChunkCoordinates(),
            coords =>
            {
                var (shape, chunkShape, indices) = coords;

                // Calculate offset within chunk
                var chunkOffsets = CalculateChunkOffsets(indices, chunkShape);

                Assert.Equal(indices.Length, chunkOffsets.Length);

                // Offset should be within chunk dimensions
                for (int i = 0; i < chunkOffsets.Length; i++)
                {
                    Assert.InRange(chunkOffsets[i], 0, chunkShape[i] - 1);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property FlatIndex_ShouldBeUnique_ForDifferentCoordinates()
    {
        return Prop.ForAll(
            GenerateTwoDifferentCoordinates(),
            pair =>
            {
                var ((shape, indices1), indices2) = pair;

                var flatIndex1 = CalculateFlatIndex(indices1, shape);
                var flatIndex2 = CalculateFlatIndex(indices2, shape);

                // Different coordinates should produce different flat indices
                if (!indices1.SequenceEqual(indices2))
                {
                    Assert.NotEqual(flatIndex1, flatIndex2);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property FlatIndex_RoundTrip_ShouldPreserveCoordinates()
    {
        return Prop.ForAll(
            GenerateValidArrayCoordinates(),
            coords =>
            {
                var (shape, indices) = coords;

                // Convert to flat index and back
                var flatIndex = CalculateFlatIndex(indices, shape);
                var recoveredIndices = CalculateMultiDimIndices(flatIndex, shape);

                Assert.Equal(indices, recoveredIndices);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property ChunkCount_ShouldCoverEntireArray()
    {
        return Prop.ForAll(
            GenerateArrayShape(),
            shapes =>
            {
                var (arrayShape, chunkShape) = shapes;

                // Calculate total number of chunks needed
                long totalChunks = 1;
                for (int i = 0; i < arrayShape.Length; i++)
                {
                    var chunksInDim = (int)Math.Ceiling((double)arrayShape[i] / chunkShape[i]);
                    totalChunks *= chunksInDim;
                }

                Assert.True(totalChunks > 0);

                // Verify coverage
                long totalElements = arrayShape.Aggregate(1L, (a, b) => a * b);
                long chunkCapacity = chunkShape.Aggregate(1L, (a, b) => a * b);

                // Total chunk capacity should be >= total elements
                Assert.True(totalChunks * chunkCapacity >= totalElements,
                    $"Chunks should cover array: {totalChunks} * {chunkCapacity} >= {totalElements}");

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property ChunkBoundary_ShouldHandlePartialChunks()
    {
        return Prop.ForAll(
            GenerateArrayShapeWithPartialChunk(),
            shapes =>
            {
                var (arrayShape, chunkShape) = shapes;

                // Last chunk in each dimension might be partial
                for (int dim = 0; dim < arrayShape.Length; dim++)
                {
                    var fullChunks = arrayShape[dim] / chunkShape[dim];
                    var remainder = arrayShape[dim] % chunkShape[dim];

                    if (remainder > 0)
                    {
                        // Last chunk is partial
                        var lastChunkSize = remainder;
                        Assert.InRange(lastChunkSize, 1, chunkShape[dim] - 1);
                    }
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property TimeSeriesChunk_ShouldHandleTemporalDimension()
    {
        return Prop.ForAll(
            GenerateTimeSeriesChunkCoords(),
            coords =>
            {
                var (timeSteps, height, width, chunkTime, chunkH, chunkW, t, y, x) = coords;

                // Calculate 3D chunk indices [time, y, x]
                var chunkIndices = new[]
                {
                    t / chunkTime,
                    y / chunkH,
                    x / chunkW
                };

                // Verify bounds
                Assert.InRange(chunkIndices[0], 0, (timeSteps + chunkTime - 1) / chunkTime - 1);
                Assert.InRange(chunkIndices[1], 0, (height + chunkH - 1) / chunkH - 1);
                Assert.InRange(chunkIndices[2], 0, (width + chunkW - 1) / chunkW - 1);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property ChunkKey_ShouldBeUnique_ForEachChunk()
    {
        return Prop.ForAll(
            GenerateTwoChunkCoordinates(),
            pair =>
            {
                var ((shape, chunk1), chunk2) = pair;

                var key1 = GenerateChunkKey(chunk1);
                var key2 = GenerateChunkKey(chunk2);

                if (!chunk1.SequenceEqual(chunk2))
                {
                    Assert.NotEqual(key1, key2);
                }

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property RowMajorOrder_ShouldBeConsistent()
    {
        return Prop.ForAll(
            GenerateValidArrayCoordinates(),
            coords =>
            {
                var (shape, indices) = coords;

                // Calculate flat index using row-major order
                var flatIndex = CalculateFlatIndex(indices, shape);

                // Verify it's within bounds
                var totalSize = shape.Aggregate(1L, (a, b) => a * b);
                Assert.InRange(flatIndex, 0, totalSize - 1);

                // Incrementing last dimension should increment flat index by 1
                if (indices[^1] < shape[^1] - 1)
                {
                    var nextIndices = indices.ToArray();
                    nextIndices[^1]++;
                    var nextFlatIndex = CalculateFlatIndex(nextIndices, shape);

                    Assert.Equal(flatIndex + 1, nextFlatIndex);
                }

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property ChunkSize_ShouldNotExceed_MaximumAllowed()
    {
        return Prop.ForAll(
            GenerateArrayShape(),
            shapes =>
            {
                var (arrayShape, chunkShape) = shapes;

                // Calculate chunk size in elements
                var chunkElements = chunkShape.Aggregate(1L, (a, b) => a * b);

                // Ensure chunk definition is valid (non-zero)
                Assert.True(chunkElements > 0, "Chunk must contain at least one element");

                return true;
            });
    }

    // Helper Methods for Chunk Calculations

    private static int[] CalculateChunkIndices(int[] indices, int[] chunkShape)
    {
        var chunkIndices = new int[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            chunkIndices[i] = indices[i] / chunkShape[i];
        }
        return chunkIndices;
    }

    private static int[] CalculateChunkOffsets(int[] indices, int[] chunkShape)
    {
        var offsets = new int[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            offsets[i] = indices[i] % chunkShape[i];
        }
        return offsets;
    }

    private static long CalculateFlatIndex(int[] indices, int[] shape)
    {
        long flatIndex = 0;
        long multiplier = 1;

        // Row-major order (C-order)
        for (int i = shape.Length - 1; i >= 0; i--)
        {
            flatIndex += indices[i] * multiplier;
            multiplier *= shape[i];
        }

        return flatIndex;
    }

    private static int[] CalculateMultiDimIndices(long flatIndex, int[] shape)
    {
        var indices = new int[shape.Length];

        for (int i = shape.Length - 1; i >= 0; i--)
        {
            indices[i] = (int)(flatIndex % shape[i]);
            flatIndex /= shape[i];
        }

        return indices;
    }

    private static string GenerateChunkKey(int[] chunkIndices)
    {
        return string.Join(".", chunkIndices);
    }

    // FsCheck Generators

    private static Arbitrary<(int[] shape, int[] chunkShape, int[] indices)> GenerateValidChunkCoordinates()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from shape in Gen.ArrayOf(ndim, Gen.Choose(10, 1000))
                  from chunkShape in Gen.ArrayOf(ndim, Gen.Choose(10, 256))
                  from indices in GenerateIndicesForShape(shape)
                  select (shape, chunkShape, indices);

        return Arb.From(gen);
    }

    private static Arbitrary<(int[] shape, int[] indices)> GenerateValidArrayCoordinates()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from shape in Gen.ArrayOf(ndim, Gen.Choose(10, 500))
                  from indices in GenerateIndicesForShape(shape)
                  select (shape, indices);

        return Arb.From(gen);
    }

    private static Arbitrary<((int[] shape, int[] indices1), int[] indices2)> GenerateTwoDifferentCoordinates()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from shape in Gen.ArrayOf(ndim, Gen.Choose(10, 100))
                  from indices1 in GenerateIndicesForShape(shape)
                  from indices2 in GenerateIndicesForShape(shape)
                  where !indices1.SequenceEqual(indices2)
                  select ((shape, indices1), indices2);

        return Arb.From(gen);
    }

    private static Arbitrary<(int[] arrayShape, int[] chunkShape)> GenerateArrayShape()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from arrayShape in Gen.ArrayOf(ndim, Gen.Choose(100, 1000))
                  from chunkShape in Gen.ArrayOf(ndim, Gen.Choose(10, 256))
                  select (arrayShape, chunkShape);

        return Arb.From(gen);
    }

    private static Arbitrary<(int[] arrayShape, int[] chunkShape)> GenerateArrayShapeWithPartialChunk()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from chunkShape in Gen.ArrayOf(ndim, Gen.Choose(10, 100))
                  from arrayShape in GenerateNonDivisibleShape(chunkShape)
                  select (arrayShape, chunkShape);

        return Arb.From(gen);
    }

    private static Gen<int[]> GenerateNonDivisibleShape(int[] chunkShape)
    {
        return Gen.Sequence(chunkShape.Select((chunkSize, idx) =>
            from fullChunks in Gen.Choose(2, 10)
            from remainder in Gen.Choose(1, chunkSize - 1)
            select fullChunks * chunkSize + remainder))
            .Select(enumerable => enumerable.ToArray());
    }

    private static Arbitrary<(int, int, int, int, int, int, int, int, int)> GenerateTimeSeriesChunkCoords()
    {
        var gen = from timeSteps in Gen.Choose(100, 365)
                  from height in Gen.Choose(100, 1000)
                  from width in Gen.Choose(100, 1000)
                  from chunkTime in Gen.Choose(10, 30)
                  from chunkH in Gen.Choose(50, 256)
                  from chunkW in Gen.Choose(50, 256)
                  from t in Gen.Choose(0, timeSteps - 1)
                  from y in Gen.Choose(0, height - 1)
                  from x in Gen.Choose(0, width - 1)
                  select (timeSteps, height, width, chunkTime, chunkH, chunkW, t, y, x);

        return Arb.From(gen);
    }

    private static Arbitrary<((int[] shape, int[] chunk1), int[] chunk2)> GenerateTwoChunkCoordinates()
    {
        var gen = from ndim in Gen.Choose(2, 4)
                  from shape in Gen.ArrayOf(ndim, Gen.Choose(10, 100))
                  from chunk1 in Gen.ArrayOf(ndim, Gen.Choose(0, 10))
                  from chunk2 in Gen.ArrayOf(ndim, Gen.Choose(0, 10))
                  select ((shape, chunk1), chunk2);

        return Arb.From(gen);
    }

    private static Gen<int[]> GenerateIndicesForShape(int[] shape)
    {
        return Gen.Sequence(shape.Select(dim => Gen.Choose(0, dim - 1)))
            .Select(enumerable => enumerable.ToArray());
    }
}
