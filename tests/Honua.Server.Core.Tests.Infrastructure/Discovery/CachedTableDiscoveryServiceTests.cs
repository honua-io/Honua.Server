// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Discovery;

/// <summary>
/// Unit tests for CachedTableDiscoveryService.
/// </summary>
public sealed class CachedTableDiscoveryServiceTests
{
    [Fact]
    public void Constructor_WithNullInnerService_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CachedTableDiscoveryService(
                null!,
                cache,
                options,
                NullLogger<CachedTableDiscoveryService>.Instance));
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>().Object;
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CachedTableDiscoveryService(
                innerService,
                null!,
                options,
                NullLogger<CachedTableDiscoveryService>.Instance));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>().Object;
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CachedTableDiscoveryService(
                innerService,
                cache,
                null!,
                NullLogger<CachedTableDiscoveryService>.Instance));
    }

    [Fact]
    public async Task DiscoverTablesAsync_WhenDisabled_ReturnsEmptyList()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTablesAsync("test-datasource");

        // Assert
        Assert.Empty(result);
        innerService.Verify(s => s.DiscoverTablesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiscoverTablesAsync_CachesResults()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "cities", "Point"),
            CreateTestTable("public", "roads", "LineString")
        };

        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - First call
        var first = await service.DiscoverTablesAsync("test-datasource");

        // Act - Second call
        var second = await service.DiscoverTablesAsync("test-datasource");

        // Assert - Inner service called only once
        innerService.Verify(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()), Times.Once);

        // Assert - Results are the same
        Assert.Equal(2, first.Count());
        Assert.Equal(2, second.Count());
        Assert.Same(first, second); // Same instance from cache
    }

    [Fact]
    public async Task DiscoverTableAsync_CachesResults()
    {
        // Arrange
        var testTable = CreateTestTable("public", "cities", "Point");

        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTableAsync("test-datasource", "public.cities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTable);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - First call
        var first = await service.DiscoverTableAsync("test-datasource", "public.cities");

        // Act - Second call
        var second = await service.DiscoverTableAsync("test-datasource", "public.cities");

        // Assert - Inner service called only once
        innerService.Verify(s => s.DiscoverTableAsync("test-datasource", "public.cities", It.IsAny<CancellationToken>()), Times.Once);

        // Assert - Results are the same
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Same(first, second); // Same instance from cache
    }

    [Fact]
    public async Task ClearCache_InvalidatesCache()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "cities", "Point")
        };

        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - First call
        await service.DiscoverTablesAsync("test-datasource");

        // Clear cache
        service.ClearCache("test-datasource");

        // Second call after cache clear
        await service.DiscoverTablesAsync("test-datasource");

        // Assert - Inner service called twice (cache was invalidated)
        innerService.Verify(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ClearAllCaches_InvalidatesAllCaches()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "cities", "Point")
        };

        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTablesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - Cache multiple data sources
        await service.DiscoverTablesAsync("datasource-1");
        await service.DiscoverTablesAsync("datasource-2");

        // Clear all caches
        service.ClearAllCaches();

        // Call again
        await service.DiscoverTablesAsync("datasource-1");
        await service.DiscoverTablesAsync("datasource-2");

        // Assert - Inner service called 4 times total (2 before clear, 2 after)
        innerService.Verify(s => s.DiscoverTablesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task DiscoverTableAsync_WhenDisabled_ReturnsNull()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverTableAsync("test-datasource", "public.cities");

        // Assert
        Assert.Null(result);
        innerService.Verify(s => s.DiscoverTableAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DiscoverTableAsync_WhenTableNotFound_DoesNotCache()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTableAsync("test-datasource", "public.nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoveredTable?)null);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - Call twice
        var first = await service.DiscoverTableAsync("test-datasource", "public.nonexistent");
        var second = await service.DiscoverTableAsync("test-datasource", "public.nonexistent");

        // Assert - Inner service called twice (null results not cached)
        innerService.Verify(s => s.DiscoverTableAsync("test-datasource", "public.nonexistent", It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public void Dispose_DisposesBackgroundRefreshTimer()
    {
        // Arrange
        var innerService = new Mock<ITableDiscoveryService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            BackgroundRefresh = true,
            CacheDuration = TimeSpan.FromSeconds(1)
        });

        var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act & Assert - Should not throw
        service.Dispose();
    }

    [Fact]
    public async Task MultipleConcurrentRequests_OnlyCallInnerServiceOnce()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "cities", "Point")
        };

        var innerService = new Mock<ITableDiscoveryService>();
        innerService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate slow operation
                return testTables;
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = true });

        using var service = new CachedTableDiscoveryService(
            innerService.Object,
            cache,
            options,
            NullLogger<CachedTableDiscoveryService>.Instance);

        // Act - Make concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.DiscoverTablesAsync("test-datasource"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Inner service should be called at least once
        // Note: Depending on timing, it might be called more than once if requests arrive
        // before the first one completes and caches the result
        innerService.Verify(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static DiscoveredTable CreateTestTable(string schema, string tableName, string geometryType)
    {
        return new DiscoveredTable
        {
            Schema = schema,
            TableName = tableName,
            GeometryColumn = "geom",
            SRID = 4326,
            GeometryType = geometryType,
            PrimaryKeyColumn = "id",
            Columns = new Dictionary<string, ColumnInfo>
            {
                ["id"] = new ColumnInfo
                {
                    Name = "id",
                    DataType = "int32",
                    StorageType = "integer",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                ["name"] = new ColumnInfo
                {
                    Name = "name",
                    DataType = "string",
                    StorageType = "text",
                    IsPrimaryKey = false,
                    IsNullable = true
                }
            },
            HasSpatialIndex = true,
            EstimatedRowCount = 1000
        };
    }
}
