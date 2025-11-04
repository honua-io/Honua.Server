using System;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterTileCacheDiskQuotaServiceTests
{
    private readonly RasterTileCacheDiskQuotaService _service;
    private readonly InMemoryRasterTileCacheMetadataStore _metadataStore;
    private readonly IRasterTileCacheProvider _cacheProvider;

    public RasterTileCacheDiskQuotaServiceTests()
    {
        _metadataStore = new InMemoryRasterTileCacheMetadataStore();
        _cacheProvider = NullRasterTileCacheProvider.Instance;
        _service = new RasterTileCacheDiskQuotaService(_metadataStore, _cacheProvider, NullLogger<RasterTileCacheDiskQuotaService>.Instance);
    }

    [Fact]
    public async Task GetQuotaStatusAsync_ShouldReturnDefaultQuota()
    {
        var status = await _service.GetQuotaStatusAsync("test-dataset");

        status.DatasetId.Should().Be("test-dataset");
        status.MaxSizeBytes.Should().Be(10L * 1024 * 1024 * 1024); // 10 GB default
        status.CurrentSizeBytes.Should().Be(0);
        status.UsagePercent.Should().Be(0);
        status.IsOverQuota.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinQuotaAsync_ShouldReturnTrueWhenUnderQuota()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _metadataStore.RecordTileCreationAsync(key, 1024);

        var isWithin = await _service.IsWithinQuotaAsync("test-dataset");

        isWithin.Should().BeTrue();
    }

    [Fact]
    public async Task GetQuotaStatusAsync_ShouldCalculateUsagePercent()
    {
        await _service.UpdateQuotaAsync("test-dataset", new DiskQuotaConfiguration(10000));

        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _metadataStore.RecordTileCreationAsync(key, 5000);

        var status = await _service.GetQuotaStatusAsync("test-dataset");

        status.CurrentSizeBytes.Should().Be(5000);
        status.UsagePercent.Should().BeApproximately(50.0, 0.1);
        status.IsOverQuota.Should().BeFalse();
    }

    [Fact]
    public async Task GetQuotaStatusAsync_ShouldDetectOverQuota()
    {
        await _service.UpdateQuotaAsync("test-dataset", new DiskQuotaConfiguration(1000));

        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _metadataStore.RecordTileCreationAsync(key, 2000);

        var status = await _service.GetQuotaStatusAsync("test-dataset");

        status.IsOverQuota.Should().BeTrue();
        status.UsagePercent.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task UpdateQuotaAsync_ShouldUpdateConfiguration()
    {
        var quota = new DiskQuotaConfiguration(
            MaxSizeBytes: 5L * 1024 * 1024 * 1024,
            ExpirationPolicy: QuotaExpirationPolicy.LeastFrequentlyUsed);

        await _service.UpdateQuotaAsync("test-dataset", quota);

        var quotas = await _service.GetAllQuotasAsync();
        quotas.Should().ContainKey("test-dataset");
        quotas["test-dataset"].MaxSizeBytes.Should().Be(5L * 1024 * 1024 * 1024);
        quotas["test-dataset"].ExpirationPolicy.Should().Be(QuotaExpirationPolicy.LeastFrequentlyUsed);
    }

    [Fact]
    public async Task UpdateQuotaAsync_ShouldThrowForInvalidQuota()
    {
        var quota = new DiskQuotaConfiguration(MaxSizeBytes: -1);

        var act = async () => await _service.UpdateQuotaAsync("test-dataset", quota);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("MaxSizeBytes must be greater than 0*");
    }

    [Fact]
    public async Task EnforceQuotaAsync_ShouldNotRemoveTilesWhenWithinQuota()
    {
        await _service.UpdateQuotaAsync("test-dataset", new DiskQuotaConfiguration(10000));

        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _metadataStore.RecordTileCreationAsync(key, 1000);

        var result = await _service.EnforceQuotaAsync("test-dataset");

        result.TilesRemoved.Should().Be(0);
        result.BytesFreed.Should().Be(0);
    }

    [Fact]
    public async Task GetAllQuotasAsync_ShouldReturnAllConfigurations()
    {
        await _service.UpdateQuotaAsync("dataset-1", new DiskQuotaConfiguration(1000));
        await _service.UpdateQuotaAsync("dataset-2", new DiskQuotaConfiguration(2000));

        var quotas = await _service.GetAllQuotasAsync();

        quotas.Should().HaveCount(2);
        quotas.Should().ContainKey("dataset-1");
        quotas.Should().ContainKey("dataset-2");
    }

    [Theory]
    [InlineData(QuotaExpirationPolicy.LeastRecentlyUsed)]
    [InlineData(QuotaExpirationPolicy.LeastFrequentlyUsed)]
    [InlineData(QuotaExpirationPolicy.OldestFirst)]
    public async Task UpdateQuotaAsync_ShouldSupportAllExpirationPolicies(QuotaExpirationPolicy policy)
    {
        var quota = new DiskQuotaConfiguration(1000, policy);

        await _service.UpdateQuotaAsync("test-dataset", quota);

        var quotas = await _service.GetAllQuotasAsync();
        quotas["test-dataset"].ExpirationPolicy.Should().Be(policy);
    }

    private static RasterTileCacheKey CreateCacheKey(string datasetId, int zoom, int row, int column)
    {
        return new RasterTileCacheKey(
            datasetId: datasetId,
            tileMatrixSetId: "WebMercatorQuad",
            zoom: zoom,
            row: row,
            column: column,
            styleId: "default",
            format: "image/png",
            transparent: true,
            tileSize: 256);
    }
}
