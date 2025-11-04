using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

/// <summary>
/// Unit tests for RedisWfsLockManager.
/// These tests use mocked Redis connections to test logic without requiring a real Redis instance.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class RedisWfsLockManagerTests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisWfsLockManager>> _mockLogger;
    private readonly Mock<IWfsLockManagerMetrics> _mockMetrics;

    public RedisWfsLockManagerTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisWfsLockManager>>();
        _mockMetrics = new Mock<IWfsLockManagerMetrics>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
    }

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisWfsLockManager(null!, _mockLogger.Object, _mockMetrics.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisWfsLockManager(_mockRedis.Object, null!, _mockMetrics.Object));
    }

    [Fact]
    public void Constructor_WithValidArguments_Succeeds()
    {
        // Act
        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullTargets_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.TryAcquireAsync("owner", TimeSpan.FromMinutes(5), null!, CancellationToken.None));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNoExistingLocks_AcquiresSuccessfully()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.TryAcquireAsync("owner1", TimeSpan.FromMinutes(5), targets, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Lock);
        Assert.NotNull(result.Lock.LockId);
        Assert.Equal(targets, result.Lock.Targets);
        Assert.Null(result.Error);

        // Verify metrics recorded
        _mockMetrics.Verify(m => m.RecordLockAcquired(
            It.IsAny<string>(),
            targets.Length,
            It.IsAny<TimeSpan>()), Times.Once);

        _mockMetrics.Verify(m => m.RecordOperationLatency(
            "acquire",
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_WithExistingLock_ReturnsConflict()
    {
        // Arrange
        var existingLockId = Guid.NewGuid().ToString("N");
        var existingLockJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            lockId = existingLockId,
            owner = "otherOwner",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            targets = new[] { new { serviceId = "service1", layerId = "layer1", featureId = "feature1" } }
        });

        // Setup target already locked
        _mockDatabase.SetupSequence(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)existingLockId) // Target index returns lock ID
            .ReturnsAsync((RedisValue)existingLockJson); // Lock key returns lock info

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.TryAcquireAsync("owner2", TimeSpan.FromMinutes(5), targets, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Lock);
        Assert.NotNull(result.Error);
        Assert.Contains("locked until", result.Error);

        // Verify metrics recorded
        _mockMetrics.Verify(m => m.RecordLockAcquisitionFailed(
            It.IsAny<string>(),
            "conflict"), Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyOwner_UsesAnonymous()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.TryAcquireAsync("", TimeSpan.FromMinutes(5), targets, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Lock);
    }

    [Fact]
    public async Task TryAcquireAsync_WithZeroDuration_UsesDefaultDuration()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        TimeSpan? capturedDuration = null;
        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((_, _, duration, _, _) =>
            {
                capturedDuration = duration;
            })
            .ReturnsAsync(true);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.TryAcquireAsync("owner", TimeSpan.Zero, targets, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedDuration);
        Assert.Equal(TimeSpan.FromMinutes(5), capturedDuration.Value);
    }

    [Fact]
    public async Task ValidateAsync_WithNullTargets_ThrowsArgumentNullException()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.ValidateAsync("lockId", null!, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_WithNonExistentLockId_ReturnsFailure()
    {
        // Arrange
        _mockDatabase.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.ValidateAsync("nonexistent", targets, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not active", result.ErrorMessage);

        // Verify metrics
        _mockMetrics.Verify(m => m.RecordLockValidated(It.IsAny<string>(), false), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WithMatchingLockId_ReturnsSuccess()
    {
        // Arrange
        var lockId = Guid.NewGuid().ToString("N");

        _mockDatabase.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)lockId);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.ValidateAsync(lockId, targets, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        // Verify metrics
        _mockMetrics.Verify(m => m.RecordLockValidated(It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_WithDifferentLockId_ReturnsFailure()
    {
        // Arrange
        var lockId = Guid.NewGuid().ToString("N");
        var differentLockId = Guid.NewGuid().ToString("N");

        _mockDatabase.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)differentLockId);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);
        var targets = new[] { new WfsLockTarget("service1", "layer1", "feature1") };

        // Act
        var result = await manager.ValidateAsync(lockId, targets, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("locked by another session", result.ErrorMessage);

        // Verify metrics
        _mockMetrics.Verify(m => m.RecordLockValidated(It.IsAny<string>(), false), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WithEmptyLockId_DoesNothing()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act
        await manager.ReleaseAsync("user1", "", null, CancellationToken.None);

        // Assert
        _mockDatabase.Verify(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WithNonExistentLock_DoesNothing()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act
        await manager.ReleaseAsync("user1", "nonexistent", null, CancellationToken.None);

        // Assert
        _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WithAllTargets_DeletesLock()
    {
        // Arrange
        var lockId = Guid.NewGuid().ToString("N");
        var lockInfo = new
        {
            lockId,
            owner = "owner1",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            targets = new[]
            {
                new { serviceId = "service1", layerId = "layer1", featureId = "feature1" },
                new { serviceId = "service1", layerId = "layer1", featureId = "feature2" }
            }
        };
        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)lockJson);

        _mockDatabase.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act
        await manager.ReleaseAsync("owner1", lockId, null, CancellationToken.None);

        // Assert - should delete lock key + 2 target keys
        _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Exactly(3));

        // Verify metrics
        _mockMetrics.Verify(m => m.RecordLockReleased(It.IsAny<string>(), 2), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_WithPartialTargets_UpdatesLock()
    {
        // Arrange
        var lockId = Guid.NewGuid().ToString("N");
        var target1 = new WfsLockTarget("service1", "layer1", "feature1");
        var target2 = new WfsLockTarget("service1", "layer1", "feature2");

        var lockInfo = new
        {
            lockId,
            owner = "owner1",
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            targets = new[]
            {
                new { serviceId = target1.ServiceId, layerId = target1.LayerId, featureId = target1.FeatureId },
                new { serviceId = target2.ServiceId, layerId = target2.LayerId, featureId = target2.FeatureId }
            }
        };
        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)lockJson);

        _mockDatabase.Setup(db => db.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMinutes(3));

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act - Release only one target
        await manager.ReleaseAsync("owner1", lockId, new[] { target1 }, CancellationToken.None);

        // Assert - should update lock with remaining targets
        _mockDatabase.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);

        // Verify metrics
        _mockMetrics.Verify(m => m.RecordLockReleased(It.IsAny<string>(), 1), Times.Once);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var manager = new RedisWfsLockManager(_mockRedis.Object, _mockLogger.Object, _mockMetrics.Object);

        // Act & Assert
        manager.Dispose();
    }
}
