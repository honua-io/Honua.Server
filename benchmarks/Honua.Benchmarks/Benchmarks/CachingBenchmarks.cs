// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using Honua.Benchmarks.Helpers;
using System.Collections.Concurrent;

namespace Honua.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for caching performance including hit/miss scenarios,
/// eviction policies, and different cache implementations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CachingBenchmarks
{
    private IMemoryCache _memoryCache = null!;
    private ConcurrentDictionary<string, CachedItem> _concurrentDictionary = null!;
    private Dictionary<string, CachedItem> _standardDictionary = null!;
    private readonly object _dictionaryLock = new();

    private List<string> _cacheKeys = null!;
    private List<CachedItem> _cacheValues = null!;
    private const int CacheSize = 1000;
    private const int QueryCount = 10000;

    [Params(0.8, 0.5, 0.2)] // Hit ratio
    public double HitRatio { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup Memory Cache
        var options = new MemoryCacheOptions
        {
            SizeLimit = CacheSize * 2 // Allow some headroom
        };
        _memoryCache = new MemoryCache(options);

        // Setup Concurrent Dictionary
        _concurrentDictionary = new ConcurrentDictionary<string, CachedItem>();

        // Setup Standard Dictionary
        _standardDictionary = new Dictionary<string, CachedItem>();

        // Generate test data
        _cacheKeys = new List<string>(CacheSize);
        _cacheValues = new List<CachedItem>(CacheSize);

        for (int i = 0; i < CacheSize; i++)
        {
            var key = $"feature_{i}";
            var (geometry, properties) = GeometryDataGenerator.GenerateFeature(i);
            var item = new CachedItem
            {
                Id = key,
                Geometry = geometry,
                Properties = properties,
                Timestamp = DateTime.UtcNow
            };

            _cacheKeys.Add(key);
            _cacheValues.Add(item);

            // Pre-populate caches
            _memoryCache.Set(key, item, new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });
            _concurrentDictionary.TryAdd(key, item);
            _standardDictionary[key] = item;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _memoryCache.Dispose();
    }

    // ========== CACHE HIT BENCHMARKS ==========

    [Benchmark]
    public int MemoryCache_Get_VariableHitRatio()
    {
        int hits = 0;
        var random = new Random(42);
        var hitThreshold = (int)(CacheSize * HitRatio);

        for (int i = 0; i < QueryCount; i++)
        {
            var keyIndex = random.Next(0, CacheSize + (CacheSize - hitThreshold));
            if (keyIndex < CacheSize)
            {
                var key = _cacheKeys[keyIndex];
                if (_memoryCache.TryGetValue(key, out CachedItem? _))
                {
                    hits++;
                }
            }
        }
        return hits;
    }

    [Benchmark]
    public int ConcurrentDictionary_Get_VariableHitRatio()
    {
        int hits = 0;
        var random = new Random(42);
        var hitThreshold = (int)(CacheSize * HitRatio);

        for (int i = 0; i < QueryCount; i++)
        {
            var keyIndex = random.Next(0, CacheSize + (CacheSize - hitThreshold));
            if (keyIndex < CacheSize)
            {
                var key = _cacheKeys[keyIndex];
                if (_concurrentDictionary.TryGetValue(key, out _))
                {
                    hits++;
                }
            }
        }
        return hits;
    }

    [Benchmark]
    public int StandardDictionary_Get_VariableHitRatio()
    {
        int hits = 0;
        var random = new Random(42);
        var hitThreshold = (int)(CacheSize * HitRatio);

        for (int i = 0; i < QueryCount; i++)
        {
            var keyIndex = random.Next(0, CacheSize + (CacheSize - hitThreshold));
            if (keyIndex < CacheSize)
            {
                var key = _cacheKeys[keyIndex];
                lock (_dictionaryLock)
                {
                    if (_standardDictionary.TryGetValue(key, out _))
                    {
                        hits++;
                    }
                }
            }
        }
        return hits;
    }

    // ========== CACHE SET BENCHMARKS ==========

    [Benchmark]
    public void MemoryCache_Set_1000_Items()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = CacheSize * 2 });
        for (int i = 0; i < CacheSize; i++)
        {
            cache.Set(_cacheKeys[i], _cacheValues[i], new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });
        }
        cache.Dispose();
    }

    [Benchmark]
    public void ConcurrentDictionary_Set_1000_Items()
    {
        var cache = new ConcurrentDictionary<string, CachedItem>();
        for (int i = 0; i < CacheSize; i++)
        {
            cache.TryAdd(_cacheKeys[i], _cacheValues[i]);
        }
    }

    [Benchmark]
    public void StandardDictionary_Set_1000_Items()
    {
        var cache = new Dictionary<string, CachedItem>();
        var lockObj = new object();
        for (int i = 0; i < CacheSize; i++)
        {
            lock (lockObj)
            {
                cache[_cacheKeys[i]] = _cacheValues[i];
            }
        }
    }

    // ========== CACHE EVICTION BENCHMARKS ==========

    [Benchmark]
    public void MemoryCache_WithEviction()
    {
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 500 });

        // Add more items than the cache can hold
        for (int i = 0; i < CacheSize; i++)
        {
            cache.Set(_cacheKeys[i], _cacheValues[i], new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });
        }

        cache.Dispose();
    }

    [Benchmark]
    public void ConcurrentDictionary_ManualEviction()
    {
        var cache = new ConcurrentDictionary<string, CachedItem>();
        const int maxSize = 500;

        for (int i = 0; i < CacheSize; i++)
        {
            cache.TryAdd(_cacheKeys[i], _cacheValues[i]);

            // Manual eviction when cache gets too large
            if (cache.Count > maxSize)
            {
                var keysToRemove = cache.Keys.Take(100).ToList();
                foreach (var key in keysToRemove)
                {
                    cache.TryRemove(key, out _);
                }
            }
        }
    }

    // ========== GEOMETRY CACHING BENCHMARKS ==========

    [Benchmark]
    public List<Geometry> CachedGeometryAccess()
    {
        var results = new List<Geometry>();
        for (int i = 0; i < 100; i++)
        {
            var key = _cacheKeys[i];
            if (_memoryCache.TryGetValue(key, out CachedItem? item))
            {
                results.Add(item!.Geometry);
            }
        }
        return results;
    }

    [Benchmark]
    public List<Geometry> UncachedGeometryGeneration()
    {
        var results = new List<Geometry>();
        for (int i = 0; i < 100; i++)
        {
            var (geometry, _) = GeometryDataGenerator.GenerateFeature(i);
            results.Add(geometry);
        }
        return results;
    }

    // ========== CACHE OPERATION PATTERNS ==========

    [Benchmark]
    public void GetOrCreate_MemoryCache()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var random = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            var key = $"key_{random.Next(0, 100)}";
            cache.GetOrCreate(key, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromMinutes(10);
                return GeometryDataGenerator.GenerateFeature(i);
            });
        }

        cache.Dispose();
    }

    [Benchmark]
    public void TryGetValue_ConcurrentDictionary()
    {
        var cache = new ConcurrentDictionary<string, CachedItem>();
        var random = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            var key = $"key_{random.Next(0, 100)}";
            if (!cache.TryGetValue(key, out _))
            {
                var (geometry, properties) = GeometryDataGenerator.GenerateFeature(i);
                cache.TryAdd(key, new CachedItem
                {
                    Id = key,
                    Geometry = geometry,
                    Properties = properties,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
}

/// <summary>
/// Represents a cached item containing geometry and metadata.
/// </summary>
public class CachedItem
{
    public string Id { get; set; } = string.Empty;
    public Geometry Geometry { get; set; } = null!;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
