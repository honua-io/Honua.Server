// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Index.Quadtree;
using Honua.Benchmarks.Helpers;

namespace Honua.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for spatial indexing structures (STRtree, Quadtree).
/// Tests index creation, insertion, and query performance with various dataset sizes.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class SpatialIndexBenchmarks
{
    private List<(Geometry Geometry, string Id)> _smallDataset = null!; // 100 features
    private List<(Geometry Geometry, string Id)> _mediumDataset = null!; // 1,000 features
    private List<(Geometry Geometry, string Id)> _largeDataset = null!; // 10,000 features
    private Envelope _searchEnvelope = null!;
    private Envelope _largeSearchEnvelope = null!;
    private Point _searchPoint = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate datasets of various sizes
        _smallDataset = GenerateDataset(100);
        _mediumDataset = GenerateDataset(1000);
        _largeDataset = GenerateDataset(10000);

        // Create search envelopes
        _searchEnvelope = new Envelope(45, 55, 45, 55); // 10x10 degree area
        _largeSearchEnvelope = new Envelope(0, 100, 0, 100); // 100x100 degree area
        _searchPoint = GeometryDataGenerator.GeneratePoint(50, 50, 50, 50);
    }

    private static List<(Geometry Geometry, string Id)> GenerateDataset(int count)
    {
        var dataset = new List<(Geometry, string)>(count);
        for (int i = 0; i < count; i++)
        {
            var geometry = GeometryDataGenerator.GeneratePolygon(
                vertexCount: 20,
                centerX: i % 100,
                centerY: i / 100,
                radius: 0.5
            );
            dataset.Add((geometry, $"feature_{i}"));
        }
        return dataset;
    }

    // ========== STRTREE CREATION BENCHMARKS ==========

    [Benchmark]
    public STRtree<string> CreateSTRtree_Small()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _smallDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();
        return index;
    }

    [Benchmark]
    public STRtree<string> CreateSTRtree_Medium()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _mediumDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();
        return index;
    }

    [Benchmark]
    public STRtree<string> CreateSTRtree_Large()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();
        return index;
    }

    // ========== QUADTREE CREATION BENCHMARKS ==========

    [Benchmark]
    public Quadtree<string> CreateQuadtree_Small()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _smallDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        return index;
    }

    [Benchmark]
    public Quadtree<string> CreateQuadtree_Medium()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _mediumDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        return index;
    }

    [Benchmark]
    public Quadtree<string> CreateQuadtree_Large()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        return index;
    }

    // ========== STRTREE QUERY BENCHMARKS ==========

    [Benchmark]
    public List<string> QuerySTRtree_Small_SmallEnvelope()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _smallDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.Query(_searchEnvelope).ToList();
    }

    [Benchmark]
    public List<string> QuerySTRtree_Medium_SmallEnvelope()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _mediumDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.Query(_searchEnvelope).ToList();
    }

    [Benchmark]
    public List<string> QuerySTRtree_Large_SmallEnvelope()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.Query(_searchEnvelope).ToList();
    }

    [Benchmark]
    public List<string> QuerySTRtree_Large_LargeEnvelope()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.Query(_largeSearchEnvelope).ToList();
    }

    // ========== QUADTREE QUERY BENCHMARKS ==========

    [Benchmark]
    public List<string> QueryQuadtree_Small_SmallEnvelope()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _smallDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }

        return index.Query(_searchEnvelope).ToList();
    }

    [Benchmark]
    public List<string> QueryQuadtree_Medium_SmallEnvelope()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _mediumDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }

        return index.Query(_searchEnvelope).ToList();
    }

    [Benchmark]
    public List<string> QueryQuadtree_Large_SmallEnvelope()
    {
        var index = new Quadtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }

        return index.Query(_searchEnvelope).ToList();
    }

    // ========== NEAREST NEIGHBOR BENCHMARKS ==========

    [Benchmark]
    public string NearestNeighbor_STRtree_Small()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _smallDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.NearestNeighbour(_searchPoint.EnvelopeInternal, null, new GeometryItemDistance());
    }

    [Benchmark]
    public string NearestNeighbor_STRtree_Medium()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _mediumDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.NearestNeighbour(_searchPoint.EnvelopeInternal, null, new GeometryItemDistance());
    }

    [Benchmark]
    public string NearestNeighbor_STRtree_Large()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.NearestNeighbour(_searchPoint.EnvelopeInternal, null, new GeometryItemDistance());
    }

    // ========== COMPARISON: LINEAR SEARCH VS INDEXED ==========

    [Benchmark(Baseline = true)]
    public List<string> LinearSearch_Large()
    {
        var results = new List<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            if (geometry.EnvelopeInternal.Intersects(_searchEnvelope))
            {
                results.Add(id);
            }
        }
        return results;
    }

    [Benchmark]
    public List<string> IndexedSearch_STRtree_Large()
    {
        var index = new STRtree<string>();
        foreach (var (geometry, id) in _largeDataset)
        {
            index.Insert(geometry.EnvelopeInternal, id);
        }
        index.Build();

        return index.Query(_searchEnvelope).ToList();
    }

    // Helper class for nearest neighbor distance calculation
    private class GeometryItemDistance : IItemDistance<Envelope, string>
    {
        public double Distance(IBoundable<Envelope, string> item1, IBoundable<Envelope, string> item2)
        {
            return item1.Bounds.Distance(item2.Bounds);
        }
    }
}
