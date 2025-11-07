using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Utilities;

public sealed class RegexCacheTests : IDisposable
{
    public RegexCacheTests()
    {
        // Clear cache before each test
        RegexCache.Clear();
    }

    public void Dispose()
    {
        // Clear cache after each test
        RegexCache.Clear();
    }

    [Fact]
    public void GetOrAdd_SamePattern_ReturnsSameInstance()
    {
        // Arrange
        var pattern = @"^\d{3}-\d{4}$";

        // Act
        var regex1 = RegexCache.GetOrAdd(pattern);
        var regex2 = RegexCache.GetOrAdd(pattern);

        // Assert
        Assert.Same(regex1, regex2);
        Assert.Equal(1, RegexCache.Count);
    }

    [Fact]
    public void GetOrAdd_DifferentPatterns_ReturnsDifferentInstances()
    {
        // Arrange
        var pattern1 = @"^\d{3}-\d{4}$";
        var pattern2 = @"^[A-Z]{2}\d{3}$";

        // Act
        var regex1 = RegexCache.GetOrAdd(pattern1);
        var regex2 = RegexCache.GetOrAdd(pattern2);

        // Assert
        Assert.NotSame(regex1, regex2);
        Assert.Equal(2, RegexCache.Count);
    }

    [Fact]
    public void GetOrAdd_DifferentOptions_ReturnsDifferentInstances()
    {
        // Arrange
        var pattern = @"^test$";

        // Act
        var regex1 = RegexCache.GetOrAdd(pattern, RegexOptions.None);
        var regex2 = RegexCache.GetOrAdd(pattern, RegexOptions.IgnoreCase);

        // Assert
        Assert.NotSame(regex1, regex2);
        Assert.Equal(2, RegexCache.Count);
    }

    [Fact]
    public void GetOrAdd_CompiledOptionAlwaysAdded()
    {
        // Arrange
        var pattern = @"^\d+$";

        // Act
        var regex = RegexCache.GetOrAdd(pattern, RegexOptions.IgnoreCase);

        // Assert
        Assert.True((regex.Options & RegexOptions.Compiled) != 0);
    }

    [Fact]
    public void GetOrAdd_WithTimeout_CreatesRegexWithTimeout()
    {
        // Arrange
        var pattern = @"^\d+$";
        var timeoutMs = 500;

        // Act
        var regex = RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, timeoutMs);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(timeoutMs), regex.MatchTimeout);
    }

    [Fact]
    public void GetOrAdd_NullPattern_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => RegexCache.GetOrAdd(null!));
    }

    [Fact]
    public void GetOrAdd_EmptyPattern_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => RegexCache.GetOrAdd(string.Empty));
    }

    [Fact]
    public void GetOrAdd_WhitespacePattern_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => RegexCache.GetOrAdd("   "));
    }

    [Fact]
    public void GetOrAdd_InvalidTimeout_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var pattern = @"^\d+$";

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, -1));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        RegexCache.GetOrAdd(@"^\d+$");
        RegexCache.GetOrAdd(@"^[A-Z]+$");
        RegexCache.GetOrAdd(@"^test$");
        Assert.Equal(3, RegexCache.Count);

        // Act
        RegexCache.Clear();

        // Assert
        Assert.Equal(0, RegexCache.Count);
    }

    [Fact]
    public void GetOrAdd_CacheSizeLimit_EvictsLRU()
    {
        // Arrange
        var originalMaxSize = RegexCache.MaxCacheSize;
        RegexCache.MaxCacheSize = 3;

        try
        {
            // Add 3 patterns
            var regex1 = RegexCache.GetOrAdd(@"^pattern1$");
            var regex2 = RegexCache.GetOrAdd(@"^pattern2$");
            var regex3 = RegexCache.GetOrAdd(@"^pattern3$");

            // Access pattern2 and pattern3 to make pattern1 least recently used
            RegexCache.GetOrAdd(@"^pattern2$");
            RegexCache.GetOrAdd(@"^pattern3$");

            // Act - Add a 4th pattern, should evict pattern1
            RegexCache.GetOrAdd(@"^pattern4$");

            // Assert
            Assert.Equal(3, RegexCache.Count);

            // Try to verify pattern1 is not cached (will create new instance)
            var newRegex1 = RegexCache.GetOrAdd(@"^pattern1$");
            Assert.NotSame(regex1, newRegex1); // Should be different instance
        }
        finally
        {
            RegexCache.MaxCacheSize = originalMaxSize;
        }
    }

    [Fact]
    public void MaxCacheSize_SetToZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => RegexCache.MaxCacheSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => RegexCache.MaxCacheSize = -1);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectValues()
    {
        // Arrange
        RegexCache.GetOrAdd(@"^\d+$");
        RegexCache.GetOrAdd(@"^[A-Z]+$");

        // Act
        var stats = RegexCache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.CacheSize);
        Assert.Equal(RegexCache.MaxCacheSize, stats.MaxCacheSize);
        Assert.True(stats.TotalAccesses >= 2);
        Assert.NotNull(stats.OldestEntryAge);
    }

    [Fact]
    public async Task GetOrAdd_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var pattern = @"^\d{3}-\d{4}$";
        var tasks = new List<Task<Regex>>();

        // Act - Create 100 concurrent requests for the same pattern
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => RegexCache.GetOrAdd(pattern)));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should return the same instance
        var firstRegex = results[0];
        Assert.All(results, regex => Assert.Same(firstRegex, regex));
        Assert.Equal(1, RegexCache.Count);
    }

    [Fact]
    public async Task GetOrAdd_ConcurrentDifferentPatterns_ThreadSafe()
    {
        // Arrange
        var patterns = Enumerable.Range(0, 50).Select(i => $@"^pattern{i}$").ToList();
        var tasks = new List<Task>();

        // Act - Create concurrent requests for different patterns
        foreach (var pattern in patterns)
        {
            tasks.Add(Task.Run(() => RegexCache.GetOrAdd(pattern)));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(50, RegexCache.Count);
    }

    [Fact]
    public void GetOrAdd_ValidPattern_MatchesCorrectly()
    {
        // Arrange
        var pattern = @"^\d{3}-\d{4}$";
        var validInput = "123-4567";
        var invalidInput = "abc-defg";

        // Act
        var regex = RegexCache.GetOrAdd(pattern);

        // Assert
        Assert.True(regex.IsMatch(validInput));
        Assert.False(regex.IsMatch(invalidInput));
    }

    [Fact]
    public void GetOrAdd_WithIgnoreCase_MatchesCorrectly()
    {
        // Arrange
        var pattern = @"^TEST$";
        var regex = RegexCache.GetOrAdd(pattern, RegexOptions.IgnoreCase);

        // Assert
        Assert.True(regex.IsMatch("test"));
        Assert.True(regex.IsMatch("TEST"));
        Assert.True(regex.IsMatch("TeSt"));
    }

    [Fact]
    public void GetOrAdd_ComplexPattern_CachesAndReuses()
    {
        // Arrange
        var pattern = @"^(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*"")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])$";

        // Act
        var regex1 = RegexCache.GetOrAdd(pattern);
        var regex2 = RegexCache.GetOrAdd(pattern);

        // Assert
        Assert.Same(regex1, regex2);
        Assert.True(regex1.IsMatch("test@example.com"));
        Assert.False(regex1.IsMatch("invalid-email"));
    }

    [Fact]
    public void GetOrAdd_WithTimeout_TimesOutOnReDoS()
    {
        // Arrange - ReDoS vulnerable pattern
        var pattern = @"^(a+)+$";
        var input = new string('a', 30) + "!"; // Will cause catastrophic backtracking
        var regex = RegexCache.GetOrAdd(pattern, RegexOptions.Compiled, timeoutMilliseconds: 100);

        // Act & Assert
        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
    }

    [Fact]
    public async Task GetOrAdd_MultipleAccesses_UpdatesAccessTimes()
    {
        // Arrange
        var pattern1 = @"^pattern1$";
        var pattern2 = @"^pattern2$";

        // Act
        RegexCache.GetOrAdd(pattern1);
        await Task.Delay(10); // Small delay to ensure different access times
        RegexCache.GetOrAdd(pattern2);
        await Task.Delay(10);
        RegexCache.GetOrAdd(pattern1); // Access pattern1 again

        var stats = RegexCache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.CacheSize);
        Assert.True(stats.TotalAccesses >= 3);
    }

    [Fact]
    public void GetStatistics_EmptyCache_ReturnsCorrectValues()
    {
        // Arrange
        RegexCache.Clear();

        // Act
        var stats = RegexCache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.CacheSize);
        Assert.Equal(0, stats.TotalAccesses);
        Assert.Null(stats.OldestEntryAge);
    }

    [Fact]
    public void GetOrAdd_InvalidRegexPattern_ThrowsArgumentException()
    {
        // Arrange
        var invalidPattern = @"["; // Unclosed character class

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            RegexCache.GetOrAdd(invalidPattern));
    }
}
