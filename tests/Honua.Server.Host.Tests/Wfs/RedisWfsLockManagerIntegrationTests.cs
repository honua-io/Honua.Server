using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Integration tests for RedisWfsLockManager using Testcontainers.
/// These tests require Docker to be running.
/// </summary>
[Collection("Redis Integration Tests")]
[Trait("Category", "Integration")]
public sealed class RedisWfsLockManagerIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _redis;
    private ILogger<RedisWfsLockManager>? _logger;
    private IWfsLockManagerMetrics? _metrics;

    public RedisWfsLockManagerIntegrationTests()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();

        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<RedisWfsLockManager>();
        _metrics = new WfsLockManagerMetrics();
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WithRealRedis_AcquiresLock()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Lock);
        Assert.NotNull(result.Lock.LockId);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task TryAcquireAsync_WithExistingLock_ReturnsConflict()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Acquire first lock
        var firstResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(firstResult.Success);

        // Act - Try to acquire same target with different owner
        var secondResult = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), targets, CancellationToken.None);

        // Assert
        Assert.False(secondResult.Success);
        Assert.Null(secondResult.Lock);
        Assert.NotNull(secondResult.Error);
        Assert.Contains("locked until", secondResult.Error);
    }

    [Fact]
    public async Task TryAcquireAsync_AfterExpiration_AllowsNewLock()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature2") };

        // Acquire lock with short TTL (1 second)
        var firstResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromSeconds(1), targets, CancellationToken.None);
        Assert.True(firstResult.Success);

        // Poll Redis to check if lock has expired
        var db = _redis!.GetDatabase();
        var lockKey = $"wfs:lock:{targets[0].ServiceId}:{targets[0].LayerId}:{targets[0].FeatureId}";
        var maxAttempts = 20;
        var attempt = 0;
        bool lockExpired = false;

        while (attempt < maxAttempts)
        {
            var exists = await db.KeyExistsAsync(lockKey);
            if (!exists)
            {
                lockExpired = true;
                break;
            }
            await Task.Delay(100);
            attempt++;
        }

        Assert.True(lockExpired, "Lock did not expire within expected time");

        // Act - Try to acquire after expiration
        var secondResult = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), targets, CancellationToken.None);

        // Assert
        Assert.True(secondResult.Success);
        Assert.NotNull(secondResult.Lock);
    }

    [Fact]
    public async Task ValidateAsync_WithValidLock_ReturnsSuccess()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature3") };

        var acquireResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(acquireResult.Success);

        // Act
        var validateResult = await manager.ValidateAsync(acquireResult.Lock!.LockId, targets, CancellationToken.None);

        // Assert
        Assert.True(validateResult.Success);
        Assert.Null(validateResult.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidLock_ReturnsFailure()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature4") };

        // Act
        var validateResult = await manager.ValidateAsync("nonexistent-lock-id", targets, CancellationToken.None);

        // Assert
        Assert.False(validateResult.Success);
        Assert.NotNull(validateResult.ErrorMessage);
        Assert.Contains("not active", validateResult.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithDifferentLockId_ReturnsFailure()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature5") };

        var acquireResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(acquireResult.Success);

        // Act - Validate with different lock ID
        var validateResult = await manager.ValidateAsync("different-lock-id", targets, CancellationToken.None);

        // Assert
        Assert.False(validateResult.Success);
        Assert.NotNull(validateResult.ErrorMessage);
        Assert.Contains("locked by another session", validateResult.ErrorMessage);
    }

    [Fact]
    public async Task ReleaseAsync_RemovesLock()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature6") };

        var acquireResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(acquireResult.Success);

        // Act
        await manager.ReleaseAsync("owner1", acquireResult.Lock!.LockId, null, CancellationToken.None);

        // Assert - Should be able to acquire again
        var secondAcquire = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(secondAcquire.Success);
    }

    [Fact]
    public async Task ReleaseAsync_PartialRelease_UpdatesLock()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var target1 = new WfsLockTarget("service1", "layer1", "feature7a");
        var target2 = new WfsLockTarget("service1", "layer1", "feature7b");
        var targets = new[] { target1, target2 };

        var acquireResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(acquireResult.Success);

        // Act - Release only one target
        await manager.ReleaseAsync("owner1", acquireResult.Lock!.LockId, new[] { target1 }, CancellationToken.None);

        // Assert - First target should be available, second still locked
        var firstTargetAcquire = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), new[] { target1 }, CancellationToken.None);
        Assert.True(firstTargetAcquire.Success);

        var secondTargetAcquire = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), new[] { target2 }, CancellationToken.None);
        Assert.False(secondTargetAcquire.Success);
    }

    [Fact]
    public async Task MultipleTargets_AcquireAndRelease_WorksCorrectly()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[]
        {
            new WfsLockTarget("service1", "layer1", "feature8a"),
            new WfsLockTarget("service1", "layer1", "feature8b"),
            new WfsLockTarget("service1", "layer1", "feature8c")
        };

        // Act - Acquire all targets
        var acquireResult = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(acquireResult.Success);

        // Assert - All targets should be locked
        foreach (var target in targets)
        {
            var validateResult = await manager.ValidateAsync(acquireResult.Lock!.LockId, new[] { target }, CancellationToken.None);
            Assert.True(validateResult.Success);
        }

        // Act - Release all
        await manager.ReleaseAsync("owner1", acquireResult.Lock!.LockId, null, CancellationToken.None);

        // Assert - All targets should be available
        var reacquireResult = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), targets, CancellationToken.None);
        Assert.True(reacquireResult.Success);
    }

    [Fact]
    public async Task ConcurrentAcquisition_OnlyOneSucceeds()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature9") };

        // Act - Try to acquire concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(i => manager.TryAcquireAsync($"owner{i}", TimeSpan.FromMinutes(1), targets, CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed
        var successCount = results.Count(r => r.Success);
        Assert.Equal(1, successCount);
        Assert.Equal(9, results.Count(r => !r.Success));
    }

    [Fact]
    public async Task Reset_ClearsAllLocks()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_redis!, _logger!, _metrics);
        var targets1 = new[] { new WfsLockTarget("service1", "layer1", "feature10a") };
        var targets2 = new[] { new WfsLockTarget("service1", "layer1", "feature10b") };

        await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(1), targets1, CancellationToken.None);
        await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(1), targets2, CancellationToken.None);

        // Act
        await manager.ResetAsync();

        // Assert - Should be able to acquire all targets immediately
        var result1 = await manager.TryAcquireAsync("owner3", TimeSpan.FromMinutes(1), targets1, CancellationToken.None);
        var result2 = await manager.TryAcquireAsync("owner4", TimeSpan.FromMinutes(1), targets2, CancellationToken.None);

        Assert.True(result1.Success);
        Assert.True(result2.Success);
    }
}
