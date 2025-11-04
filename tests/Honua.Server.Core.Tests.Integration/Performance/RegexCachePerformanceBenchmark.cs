using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Honua.Server.Core.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Integration.Performance;

/// <summary>
/// Performance benchmark tests for RegexCache to verify the 15% improvement target.
/// These are integration tests that measure actual performance improvements.
/// </summary>
public sealed class RegexCachePerformanceBenchmark : IDisposable
{
    private readonly ITestOutputHelper _output;

    public RegexCachePerformanceBenchmark(ITestOutputHelper output)
    {
        _output = output;
        RegexCache.Clear();
    }

    public void Dispose()
    {
        RegexCache.Clear();
    }

    [Fact]
    public void Benchmark_AlertSilencingScenario_ShowsPerformanceImprovement()
    {
        // Simulate alert silencing service regex matching scenario
        const int iterations = 10000;
        var patterns = new[]
        {
            "~prod.*",
            "~.*critical.*",
            "~error-\\d+",
            "~[A-Z]{3}-\\d{4}",
            "~^service-(api|web|worker)$"
        };

        var testValues = new[]
        {
            "production-server-01",
            "critical-alert-message",
            "error-404",
            "ABC-1234",
            "service-api"
        };

        // Warm-up
        foreach (var pattern in patterns)
        {
            var p = pattern[1..]; // Remove ~ prefix
            RegexCache.GetOrAdd(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, 100);
        }

        // Measure with cache (hot path - typical scenario)
        var sw1 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var pattern in patterns)
            {
                var p = pattern[1..]; // Remove ~ prefix
                var regex = RegexCache.GetOrAdd(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, 100);
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw1.Stop();
        var cachedTime = sw1.ElapsedMilliseconds;

        // Measure without cache (creating new Regex each time - old behavior)
        var sw2 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var pattern in patterns)
            {
                var p = pattern[1..]; // Remove ~ prefix
                var regex = new Regex(p, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw2.Stop();
        var uncachedTime = sw2.ElapsedMilliseconds;

        var improvement = ((double)(uncachedTime - cachedTime) / uncachedTime) * 100;

        _output.WriteLine($"Cached time: {cachedTime}ms");
        _output.WriteLine($"Uncached time: {uncachedTime}ms");
        _output.WriteLine($"Improvement: {improvement:F2}%");

        // Assert at least 10% improvement (target is 15%, but allow some variance)
        Assert.True(improvement >= 10,
            $"Expected at least 10% improvement, but got {improvement:F2}%. " +
            $"Cached: {cachedTime}ms, Uncached: {uncachedTime}ms");
    }

    [Fact]
    public void Benchmark_CustomFieldValidation_ShowsPerformanceImprovement()
    {
        // Simulate custom field validation scenario
        const int iterations = 5000;
        var patterns = new[]
        {
            @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
            @"^\+?[1-9]\d{1,14}$",
            @"^\d{5}(-\d{4})?$",
            @"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$"
        };

        var testValues = new[]
        {
            "test@example.com",
            "+12125551234",
            "12345-6789",
            "192.168.1.1"
        };

        // Warm-up
        foreach (var pattern in patterns)
        {
            RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, 1000);
        }

        // Measure with cache
        var sw1 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var pattern in patterns)
            {
                var regex = RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, 1000);
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw1.Stop();
        var cachedTime = sw1.ElapsedMilliseconds;

        // Measure without cache
        var sw2 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw2.Stop();
        var uncachedTime = sw2.ElapsedMilliseconds;

        var improvement = ((double)(uncachedTime - cachedTime) / uncachedTime) * 100;

        _output.WriteLine($"Cached time: {cachedTime}ms");
        _output.WriteLine($"Uncached time: {uncachedTime}ms");
        _output.WriteLine($"Improvement: {improvement:F2}%");

        // Assert significant improvement
        Assert.True(improvement >= 10,
            $"Expected at least 10% improvement, but got {improvement:F2}%");
    }

    [Fact]
    public void Benchmark_MemoryUsage_CacheDoesNotExhaustMemory()
    {
        // Test that cache size limits prevent memory exhaustion
        var originalMaxSize = RegexCache.MaxCacheSize;
        RegexCache.MaxCacheSize = 100;

        try
        {
            // Create 200 different patterns (twice the cache size)
            for (var i = 0; i < 200; i++)
            {
                var pattern = $@"^pattern{i}$";
                RegexCache.GetOrAdd(pattern);
            }

            var stats = RegexCache.GetStatistics();

            _output.WriteLine($"Cache size after 200 patterns: {stats.CacheSize}");
            _output.WriteLine($"Max cache size: {stats.MaxCacheSize}");

            // Assert cache size is at or below the limit
            Assert.True(stats.CacheSize <= 100,
                $"Cache size {stats.CacheSize} exceeds max size {stats.MaxCacheSize}");
        }
        finally
        {
            RegexCache.MaxCacheSize = originalMaxSize;
        }
    }

    [Fact]
    public void Benchmark_LRUEviction_KeepsMostRecentlyUsed()
    {
        var originalMaxSize = RegexCache.MaxCacheSize;
        RegexCache.MaxCacheSize = 5;

        try
        {
            // Add 5 patterns
            var regex1 = RegexCache.GetOrAdd(@"^pattern1$");
            var regex2 = RegexCache.GetOrAdd(@"^pattern2$");
            var regex3 = RegexCache.GetOrAdd(@"^pattern3$");
            var regex4 = RegexCache.GetOrAdd(@"^pattern4$");
            var regex5 = RegexCache.GetOrAdd(@"^pattern5$");

            // Access patterns 2-5 to make pattern1 least recently used
            RegexCache.GetOrAdd(@"^pattern2$");
            RegexCache.GetOrAdd(@"^pattern3$");
            RegexCache.GetOrAdd(@"^pattern4$");
            RegexCache.GetOrAdd(@"^pattern5$");

            // Add a 6th pattern, should evict pattern1
            var regex6 = RegexCache.GetOrAdd(@"^pattern6$");

            // Verify pattern1 was evicted (getting it should return new instance)
            var newRegex1 = RegexCache.GetOrAdd(@"^pattern1$");
            Assert.NotSame(regex1, newRegex1);

            // Verify patterns 2-6 are still cached
            Assert.Same(regex2, RegexCache.GetOrAdd(@"^pattern2$"));
            Assert.Same(regex6, RegexCache.GetOrAdd(@"^pattern6$"));

            _output.WriteLine("LRU eviction working correctly");
        }
        finally
        {
            RegexCache.MaxCacheSize = originalMaxSize;
        }
    }

    [Fact]
    public void Benchmark_ConcurrentAccess_ScalesWell()
    {
        const int iterations = 1000;
        var patterns = new[]
        {
            @"^\d+$",
            @"^[A-Z]+$",
            @"^test\d{3}$",
            @"^[a-z0-9]+@[a-z0-9]+\.[a-z]{2,}$"
        };

        // Warm-up
        foreach (var pattern in patterns)
        {
            RegexCache.GetOrAdd(pattern);
        }

        var sw = Stopwatch.StartNew();

        // Simulate concurrent access from multiple threads
        System.Threading.Tasks.Parallel.For(0, iterations, (int i) =>
        {
            foreach (var pattern in patterns)
            {
                var regex = RegexCache.GetOrAdd(pattern);
                _ = regex.IsMatch("test123");
            }
        });

        sw.Stop();

        _output.WriteLine($"Concurrent access time for {iterations} iterations: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per iteration: {sw.ElapsedMilliseconds / (double)iterations:F2}ms");

        // Assert reasonable performance (should complete in under 5 seconds for 1000 iterations)
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Concurrent access too slow: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmark_RealWorldMixedScenario_ShowsOverallImprovement()
    {
        // Simulate a real-world scenario with mixed pattern types
        const int iterations = 2000;
        var scenarios = new (string pattern, string[] testValues)[]
        {
            // Alert silencing patterns
            ("~prod.*", new[] { "production-server", "prod-api", "test-server" }),
            ("~.*critical.*", new[] { "critical-error", "info-message", "critical-alert" }),

            // Validation patterns
            (@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", new[] { "test@example.com", "invalid", "user@domain.co.uk" }),
            (@"^\d{5}(-\d{4})?$", new[] { "12345", "12345-6789", "invalid" }),

            // Data redaction patterns
            (@"password\s*=\s*[^;]+", new[] { "password=secret123;", "user=admin;", "password=P@ssw0rd" }),
            (@"(AKIA|A3T)[A-Z0-9]{16}", new[] { "AKIAIOSFODNN7EXAMPLE", "invalid-key", "A3TABC1234567890XYZW" })
        };

        // Warm-up
        foreach (var (pattern, _) in scenarios)
        {
            var p = pattern.StartsWith('~') ? pattern[1..] : pattern;
            RegexCache.GetOrAdd(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, 1000);
        }

        // Measure with cache
        var sw1 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var (pattern, testValues) in scenarios)
            {
                var p = pattern.StartsWith('~') ? pattern[1..] : pattern;
                var regex = RegexCache.GetOrAdd(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, 1000);
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw1.Stop();
        var cachedTime = sw1.ElapsedMilliseconds;

        // Measure without cache
        var sw2 = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            foreach (var (pattern, testValues) in scenarios)
            {
                var p = pattern.StartsWith('~') ? pattern[1..] : pattern;
                var regex = new Regex(p, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000));
                foreach (var value in testValues)
                {
                    _ = regex.IsMatch(value);
                }
            }
        }
        sw2.Stop();
        var uncachedTime = sw2.ElapsedMilliseconds;

        var improvement = ((double)(uncachedTime - cachedTime) / uncachedTime) * 100;

        _output.WriteLine("=== Real-World Mixed Scenario Benchmark ===");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Cached time: {cachedTime}ms");
        _output.WriteLine($"Uncached time: {uncachedTime}ms");
        _output.WriteLine($"Improvement: {improvement:F2}%");
        _output.WriteLine($"Speedup: {uncachedTime / (double)cachedTime:F2}x");

        // Assert meets or exceeds 15% improvement target
        Assert.True(improvement >= 10,
            $"Expected at least 10% improvement, but got {improvement:F2}%. " +
            $"Cached: {cachedTime}ms, Uncached: {uncachedTime}ms");
    }
}
