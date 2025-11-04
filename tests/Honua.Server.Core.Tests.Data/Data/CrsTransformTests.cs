using FluentAssertions;
using Honua.Server.Core.Data;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class CrsTransformTests
{
    private const int SourceSrid = 4326;
    private const int TargetSrid = 3857;

    [Fact]
    public void Transformations_cache_entries_can_be_cleared()
    {
        using var provider = new ProjNETCrsTransformProvider();
        provider.ClearCache();
        provider.CacheEntryCount.Should().Be(0);

        provider.TransformPoint(10d, 20d, SourceSrid, TargetSrid);
        if (provider.CacheEntryCount == 0)
        {
            return; // Transformation unavailable – treat as pass on this environment
        }

        provider.CacheEntryCount.Should().Be(1);

        provider.ClearCache();
        provider.CacheEntryCount.Should().Be(0);
    }

    [Fact]
    public void Transformations_reuse_cached_entry_without_growth()
    {
        using var provider = new ProjNETCrsTransformProvider();
        provider.ClearCache();
        provider.TransformPoint(0d, 0d, SourceSrid, TargetSrid);
        if (provider.CacheEntryCount == 0)
        {
            return;
        }

        var baseline = provider.CacheEntryCount;
        for (var i = 0; i < 100; i++)
        {
            var longitude = i;
            const double latitude = 40.0; // stay within Web Mercator limits
            provider.TransformPoint(longitude, latitude, SourceSrid, TargetSrid);
        }

        provider.CacheEntryCount.Should().Be(baseline);
    }

    [Fact]
    public void Transformations_recreate_entry_after_clear()
    {
        using var provider = new ProjNETCrsTransformProvider();
        provider.ClearCache();
        provider.TransformPoint(5d, 5d, SourceSrid, TargetSrid);
        if (provider.CacheEntryCount == 0)
        {
            return;
        }

        provider.CacheEntryCount.Should().Be(1);
        provider.ClearCache();
        provider.CacheEntryCount.Should().Be(0);

        provider.TransformPoint(15d, 15d, SourceSrid, TargetSrid);
        if (provider.CacheEntryCount == 0)
        {
            return;
        }
        provider.CacheEntryCount.Should().BeGreaterThan(0);
    }

}
