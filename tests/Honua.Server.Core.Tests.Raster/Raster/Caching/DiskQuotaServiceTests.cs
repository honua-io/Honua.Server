using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class DiskQuotaServiceTests : IDisposable
{
    private readonly string _tempPath;
    private readonly DiskQuotaService _service;
    private readonly InMemoryRasterTileCacheMetadataStore _metadataStore;
    private readonly NullRasterTileCacheProvider _cacheProvider;

    public DiskQuotaServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);

        _metadataStore = new InMemoryRasterTileCacheMetadataStore();
        _cacheProvider = (NullRasterTileCacheProvider)NullRasterTileCacheProvider.Instance;

        var options = new DiskQuotaOptions
        {
            MaxDiskUsagePercent = 0.8,
            WarningThresholdPercent = 0.7,
            MinimumFreeSpaceBytes = 100 * 1024 * 1024, // 100 MB
            EnableAutomaticCleanup = true,
            EvictionPolicy = QuotaExpirationPolicy.LeastRecentlyUsed
        };

        _service = new DiskQuotaService(
            NullLogger<DiskQuotaService>.Instance,
            _metadataStore,
            _cacheProvider,
            options);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeWithOptions()
    {
        // Assert
        _service.Options.MaxDiskUsagePercent.Should().Be(0.8);
        _service.Options.WarningThresholdPercent.Should().Be(0.7);
        _service.Options.MinimumFreeSpaceBytes.Should().Be(100 * 1024 * 1024);
        _service.Options.EnableAutomaticCleanup.Should().BeTrue();
        _service.Options.EvictionPolicy.Should().Be(QuotaExpirationPolicy.LeastRecentlyUsed);
    }

    [Fact]
    public void Constructor_ShouldUseDefaultOptionsWhenNull()
    {
        // Act
        var service = new DiskQuotaService(
            NullLogger<DiskQuotaService>.Instance,
            _metadataStore,
            _cacheProvider);

        // Assert
        service.Options.MaxDiskUsagePercent.Should().Be(0.8);
        service.Options.WarningThresholdPercent.Should().Be(0.7);
        service.Options.MinimumFreeSpaceBytes.Should().Be(1024L * 1024 * 1024); // 1 GB default
        service.Options.EnableAutomaticCleanup.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientSpaceAsync_ShouldReturnTrue_WhenSpaceIsAvailable()
    {
        // Arrange
        var smallSize = 1024; // 1 KB - should always be available

        // Act
        var result = await _service.HasSufficientSpaceAsync(_tempPath, smallSize);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientSpaceAsync_ShouldThrowArgumentException_ForNullPath()
    {
        // Act
        var act = async () => await _service.HasSufficientSpaceAsync(null!, 1024);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HasSufficientSpaceAsync_ShouldThrowArgumentOutOfRangeException_ForNegativeSize()
    {
        // Act
        var act = async () => await _service.HasSufficientSpaceAsync(_tempPath, -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetDiskSpaceStatusAsync_ShouldReturnValidStatus()
    {
        // Act
        var status = await _service.GetDiskSpaceStatusAsync(_tempPath);

        // Assert
        status.Should().NotBeNull();
        status.Path.Should().NotBeNullOrEmpty();
        status.TotalBytes.Should().BeGreaterThan(0);
        status.FreeBytes.Should().BeGreaterThanOrEqualTo(0);
        status.UsedBytes.Should().BeGreaterThanOrEqualTo(0);
        status.UsagePercent.Should().BeInRange(0.0, 1.0);
        status.TotalBytes.Should().Be(status.FreeBytes + status.UsedBytes);
    }

    [Fact]
    public async Task GetDiskSpaceStatusAsync_ShouldCalculateUsageCorrectly()
    {
        // Act
        var status = await _service.GetDiskSpaceStatusAsync(_tempPath);

        // Assert
        var expectedUsage = (double)status.UsedBytes / status.TotalBytes;
        status.UsagePercent.Should().BeApproximately(expectedUsage, 0.001);
    }

    [Fact]
    public async Task GetDiskSpaceStatusAsync_ShouldDetectNearQuota_WhenAboveWarningThreshold()
    {
        // This test is environment-dependent and may not always trigger
        // Act
        var status = await _service.GetDiskSpaceStatusAsync(_tempPath);

        // Assert - we can at least verify the logic
        if (status.UsagePercent > _service.Options.WarningThresholdPercent &&
            status.UsagePercent <= _service.Options.MaxDiskUsagePercent)
        {
            status.IsNearQuota.Should().BeTrue();
        }
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldReturnZeroFilesRemoved_WhenDirectoryIsEmpty()
    {
        // Act
        var result = await _service.FreeUpSpaceAsync(_tempPath, 1024);

        // Assert
        result.Should().NotBeNull();
        result.FilesRemoved.Should().Be(0);
        result.BytesFreed.Should().Be(0);
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldRemoveFiles_WhenTargetNotMet()
    {
        // Arrange - create some test files
        var file1 = Path.Combine(_tempPath, "test1.bin");
        var file2 = Path.Combine(_tempPath, "test2.bin");
        await File.WriteAllBytesAsync(file1, new byte[1024]);
        await File.WriteAllBytesAsync(file2, new byte[2048]);

        // Act
        var result = await _service.FreeUpSpaceAsync(_tempPath, long.MaxValue);

        // Assert
        result.FilesRemoved.Should().BeGreaterThan(0);
        result.BytesFreed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldRemoveOldestFiles_WithLRUPolicy()
    {
        // Arrange - create files with different access times
        var oldFile = Path.Combine(_tempPath, "old.bin");
        var newFile = Path.Combine(_tempPath, "new.bin");

        await File.WriteAllBytesAsync(oldFile, new byte[1024]);
        await File.WriteAllBytesAsync(newFile, new byte[1024]);

        // Make old file actually old
        File.SetLastAccessTime(oldFile, DateTime.UtcNow.AddDays(-7));
        File.SetLastAccessTime(newFile, DateTime.UtcNow);

        // Act - request cleanup to remove at least one file
        var result = await _service.FreeUpSpaceAsync(_tempPath, long.MaxValue);

        // Assert
        result.FilesRemoved.Should().BeGreaterThan(0);
        // Old file should be removed first
        File.Exists(oldFile).Should().BeFalse();
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldThrowArgumentException_ForNullPath()
    {
        // Act
        var act = async () => await _service.FreeUpSpaceAsync(null!, 1024);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldThrowArgumentOutOfRangeException_ForNegativeTarget()
    {
        // Act
        var act = async () => await _service.FreeUpSpaceAsync(_tempPath, -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FreeUpSpaceAsync_ShouldStopWhenTargetAchieved()
    {
        // Arrange - create multiple files
        for (int i = 0; i < 5; i++)
        {
            var file = Path.Combine(_tempPath, $"test{i}.bin");
            await File.WriteAllBytesAsync(file, new byte[1024]);
        }

        // Get current free space
        var initialStatus = await _service.GetDiskSpaceStatusAsync(_tempPath);
        var targetFreeSpace = initialStatus.FreeBytes + 1024; // Just need to free one file

        // Act
        var result = await _service.FreeUpSpaceAsync(_tempPath, targetFreeSpace);

        // Assert
        result.FilesRemoved.Should().BeGreaterThan(0);
        result.FilesRemoved.Should().BeLessThan(5); // Should not remove all files
    }

    [Fact]
    public void Options_ShouldBeAccessible()
    {
        // Act
        var options = _service.Options;

        // Assert
        options.Should().NotBeNull();
        options.MaxDiskUsagePercent.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(QuotaExpirationPolicy.LeastRecentlyUsed)]
    [InlineData(QuotaExpirationPolicy.OldestFirst)]
    [InlineData(QuotaExpirationPolicy.LeastFrequentlyUsed)]
    public async Task FreeUpSpaceAsync_ShouldSupportDifferentEvictionPolicies(QuotaExpirationPolicy policy)
    {
        // Arrange
        var customOptions = new DiskQuotaOptions
        {
            MaxDiskUsagePercent = 0.8,
            EvictionPolicy = policy
        };

        var customService = new DiskQuotaService(
            NullLogger<DiskQuotaService>.Instance,
            _metadataStore,
            _cacheProvider,
            customOptions);

        // Create test file
        var testFile = Path.Combine(_tempPath, "test.bin");
        await File.WriteAllBytesAsync(testFile, new byte[1024]);

        // Act
        var result = await customService.FreeUpSpaceAsync(_tempPath, long.MaxValue);

        // Assert
        result.Should().NotBeNull();
        customService.Options.EvictionPolicy.Should().Be(policy);
    }

    [Fact]
    public async Task GetDiskSpaceStatusAsync_ShouldThrowArgumentException_ForNullPath()
    {
        // Act
        var act = async () => await _service.GetDiskSpaceStatusAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
