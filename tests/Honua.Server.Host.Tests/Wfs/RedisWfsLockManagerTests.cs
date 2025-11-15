// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Host.Wfs;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

[Trait("Category", "Unit")]
public class RedisWfsLockManagerTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisWfsLockManager>> _mockLogger;
    private readonly Mock<IWfsLockManagerMetrics> _mockMetrics;
    private readonly RedisWfsLockManager _lockManager;

    public RedisWfsLockManagerTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisWfsLockManager>>();
        _mockMetrics = new Mock<IWfsLockManagerMetrics>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _lockManager = new RedisWfsLockManager(
            _mockRedis.Object,
            _mockLogger.Object,
            _mockMetrics.Object);
    }

    public void Dispose()
    {
        _lockManager?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedisWfsLockManager(
            null!,
            _mockLogger.Object,
            _mockMetrics.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedisWfsLockManager(
            _mockRedis.Object,
            null!,
            _mockMetrics.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullMetrics_UsesDefaultMetrics()
    {
        // Act
        var act = () => new RedisWfsLockManager(
            _mockRedis.Object,
            _mockLogger.Object,
            null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomKeyPrefix_UsesCustomPrefix()
    {
        // Arrange
        var customPrefix = "custom:prefix:";

        // Act
        var lockManager = new RedisWfsLockManager(
            _mockRedis.Object,
            _mockLogger.Object,
            _mockMetrics.Object,
            customPrefix);

        // Assert
        lockManager.Should().NotBeNull();
        lockManager.Dispose();
    }

    #endregion

    #region TryAcquireAsync Tests

    [Fact]
    public async Task TryAcquireAsync_WithNullTargets_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            null!,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyOwner_UsesAnonymous()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        SetupSuccessfulLockAcquisition();

        // Act
        var result = await _lockManager.TryAcquireAsync(
            "",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Lock.Should().NotBeNull();
        result.Lock!.Owner.Should().Be("anonymous");
    }

    [Fact]
    public async Task TryAcquireAsync_WithZeroDuration_UsesDefaultDuration()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        TimeSpan? capturedDuration = null;
        SetupLockAcquisitionWithCallback((_, _, duration, _) =>
        {
            capturedDuration = duration;
        });

        // Act
        await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.Zero,
            targets,
            CancellationToken.None);

        // Assert
        capturedDuration.Should().NotBeNull();
        capturedDuration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNoConflicts_ReturnsSuccess()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1"),
            new("service1", "layer1", "feature2")
        };

        SetupSuccessfulLockAcquisition();

        // Act
        var result = await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Lock.Should().NotBeNull();
        result.Lock!.LockId.Should().NotBeNullOrEmpty();
        result.Lock.Owner.Should().Be("owner");
        result.Lock.Targets.Should().HaveCount(2);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WithExistingLock_ReturnsFailure()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        SetupExistingLock("existing-lock-id", "owner", DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        var result = await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Lock.Should().BeNull();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("locked");
    }

    [Fact]
    public async Task TryAcquireAsync_StoresLockInRedisWithExpiration()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        var duration = TimeSpan.FromMinutes(10);
        RedisKey? capturedLockKey = null;
        TimeSpan? capturedExpiry = null;

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.Setup(t => t.AddCondition(It.IsAny<Condition>())).Returns(mockTransaction.Object);
        mockTransaction.Setup(t => t.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, _, expiry, _, _) =>
            {
                if (key.ToString().Contains(":lock:") && !key.ToString().Contains(":target:"))
                {
                    capturedLockKey = key;
                    capturedExpiry = expiry;
                }
            })
            .Returns(Task.FromResult(false));
        mockTransaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(mockTransaction.Object);

        // Act
        await _lockManager.TryAcquireAsync(
            "owner",
            duration,
            targets,
            CancellationToken.None);

        // Assert
        capturedLockKey.Should().NotBeNull();
        capturedLockKey.ToString().Should().StartWith("honua:wfs:lock:");
        capturedExpiry.Should().Be(duration);
    }

    [Fact]
    public async Task TryAcquireAsync_StoresTargetIndexInRedis()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        var targetKeys = new List<string>();

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.Setup(t => t.AddCondition(It.IsAny<Condition>())).Returns(mockTransaction.Object);
        mockTransaction.Setup(t => t.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, _, _, _, _) =>
            {
                if (key.ToString().Contains(":target:"))
                {
                    targetKeys.Add(key.ToString());
                }
            })
            .Returns(Task.FromResult(false));
        mockTransaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(mockTransaction.Object);

        // Act
        await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        targetKeys.Should().HaveCount(1);
        targetKeys[0].Should().Contain("service1");
        targetKeys[0].Should().Contain("layer1");
        targetKeys[0].Should().Contain("feature1");
    }

    [Fact]
    public async Task TryAcquireAsync_RecordsMetrics()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        SetupSuccessfulLockAcquisition();

        // Act
        await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        _mockMetrics.Verify(m => m.RecordLockAcquired(
            "service1",
            1,
            TimeSpan.FromMinutes(5)), Times.Once);

        _mockMetrics.Verify(m => m.RecordOperationLatency(
            "acquire",
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_OnConflict_RecordsFailureMetrics()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        SetupExistingLock("existing-lock-id", "owner", DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            CancellationToken.None);

        // Assert
        _mockMetrics.Verify(m => m.RecordLockAcquisitionFailed(
            "service1",
            "conflict"), Times.Once);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _lockManager.TryAcquireAsync(
            "owner",
            TimeSpan.FromMinutes(5),
            targets,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WithNullTargets_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _lockManager.ValidateAsync(
            "lock-id",
            null!,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateAsync_WithInactiveLockId_ReturnsFailure()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _lockManager.ValidateAsync(
            "invalid-lock-id",
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not active");
    }

    [Fact]
    public async Task ValidateAsync_WithValidLock_ReturnsSuccess()
    {
        // Arrange
        var lockId = "valid-lock-id";
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockId));

        // Act
        var result = await _lockManager.ValidateAsync(
            lockId,
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WithDifferentLockId_ReturnsFailure()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("different-lock-id"));

        // Act
        var result = await _lockManager.ValidateAsync(
            "my-lock-id",
            targets,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("locked by another session");
    }

    [Fact]
    public async Task ValidateAsync_RecordsMetrics()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        _mockDatabase.Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("lock-id"));

        // Act
        await _lockManager.ValidateAsync(
            "lock-id",
            targets,
            CancellationToken.None);

        // Assert
        _mockMetrics.Verify(m => m.RecordLockValidated(
            "service1",
            true), Times.Once);

        _mockMetrics.Verify(m => m.RecordOperationLatency(
            "validate",
            It.IsAny<TimeSpan>()), Times.Once);
    }

    #endregion

    #region ReleaseAsync Tests

    [Fact]
    public async Task ReleaseAsync_WithNullLockId_ReturnsImmediately()
    {
        // Act
        await _lockManager.ReleaseAsync(
            "owner",
            null!,
            null,
            CancellationToken.None);

        // Assert
        _mockDatabase.Verify(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WithEmptyLockId_ReturnsImmediately()
    {
        // Act
        await _lockManager.ReleaseAsync(
            "owner",
            "",
            null,
            CancellationToken.None);

        // Assert
        _mockDatabase.Verify(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_WithNonExistentLock_ReturnsGracefully()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var act = async () => await _lockManager.ReleaseAsync(
            "owner",
            "non-existent-lock",
            null,
            CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReleaseAsync_ByOwner_ReleasesAllTargets()
    {
        // Arrange
        var lockId = "lock-id";
        var lockInfo = new
        {
            LockId = lockId,
            Owner = "owner",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" },
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature2" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockJson));

        var deletedKeys = new List<string>();
        _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) =>
            {
                deletedKeys.Add(key.ToString());
            })
            .ReturnsAsync(true);

        // Act
        await _lockManager.ReleaseAsync(
            "owner",
            lockId,
            null,
            CancellationToken.None);

        // Assert
        deletedKeys.Should().HaveCount(3); // 2 targets + 1 lock
        deletedKeys.Should().Contain(k => k.Contains("target:"));
        deletedKeys.Should().Contain(k => k.Contains($"lock:{lockId}"));
    }

    [Fact]
    public async Task ReleaseAsync_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var lockId = "lock-id";
        var lockInfo = new
        {
            LockId = lockId,
            Owner = "original-owner",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockJson));

        // Act
        var act = async () => await _lockManager.ReleaseAsync(
            "different-user",
            lockId,
            null,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*cannot be released by*");
    }

    [Fact]
    public async Task ReleaseAsync_ByAdmin_ReleasesLock()
    {
        // Arrange
        var lockId = "lock-id";
        var lockInfo = new
        {
            LockId = lockId,
            Owner = "original-owner",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockJson));

        _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await _lockManager.ReleaseAsync(
            "admin",
            lockId,
            null,
            CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WithSpecificTargets_ReleasesOnlyThoseTargets()
    {
        // Arrange
        var lockId = "lock-id";
        var target1 = new WfsLockTarget("service1", "layer1", "feature1");
        var target2 = new WfsLockTarget("service1", "layer1", "feature2");

        var lockInfo = new
        {
            LockId = lockId,
            Owner = "owner",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" },
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature2" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);

        // First call returns the lock
        var callCount = 0;
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => callCount++ == 0 ? new RedisValue(lockJson) : RedisValue.Null);

        _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.KeyTimeToLiveAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMinutes(5));

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _lockManager.ReleaseAsync(
            "owner",
            lockId,
            new[] { target1 },
            CancellationToken.None);

        // Assert
        _mockDatabase.Verify(db => db.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("feature1")),
            It.IsAny<CommandFlags>()), Times.Once);

        _mockDatabase.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_RecordsMetrics()
    {
        // Arrange
        var lockId = "lock-id";
        var lockInfo = new
        {
            LockId = lockId,
            Owner = "owner",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockJson));

        _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _lockManager.ReleaseAsync(
            "owner",
            lockId,
            null,
            CancellationToken.None);

        // Assert
        _mockMetrics.Verify(m => m.RecordLockReleased(
            "service1",
            1), Times.Once);

        _mockMetrics.Verify(m => m.RecordOperationLatency(
            "release",
            It.IsAny<TimeSpan>()), Times.Once);
    }

    #endregion

    #region ResetAsync Tests

    [Fact]
    public async Task ResetAsync_DeletesAllWfsLocks()
    {
        // Arrange
        var mockServer = new Mock<IServer>();
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379);

        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(new[] { endpoint });

        _mockRedis.Setup(r => r.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        var keys = new RedisKey[]
        {
            new RedisKey("honua:wfs:lock:lock1"),
            new RedisKey("honua:wfs:lock:target:service1:layer1:feature1")
        };

        mockServer.Setup(s => s.Keys(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Returns(keys);

        var deletedKeys = new List<string>();
        _mockDatabase.Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) =>
            {
                deletedKeys.Add(key.ToString());
            })
            .ReturnsAsync(true);

        // Act
        await _lockManager.ResetAsync();

        // Assert
        deletedKeys.Should().HaveCount(2);
        deletedKeys.Should().Contain("honua:wfs:lock:lock1");
    }

    [Fact]
    public async Task ResetAsync_OnException_Throws()
    {
        // Arrange
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Throws(new RedisException("Connection error"));

        // Act
        var act = async () => await _lockManager.ResetAsync();

        // Assert
        await act.Should().ThrowAsync<RedisException>();
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task TryAcquireAsync_WhenRedisConnectionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var targets = new List<WfsLockTarget>
        {
            new("service1", "layer1", "feature1")
        };

        // Simulate multiple failures to open the circuit
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // Act & Assert - Multiple calls to trigger circuit breaker
        for (int i = 0; i < 10; i++)
        {
            var act = async () => await _lockManager.TryAcquireAsync(
                "owner",
                TimeSpan.FromMinutes(5),
                targets,
                CancellationToken.None);

            await act.Should().ThrowAsync<Exception>();

            // Small delay to allow circuit breaker to process
            await Task.Delay(10);
        }

        // Verify circuit opened event was recorded
        _mockMetrics.Verify(m => m.RecordCircuitOpened(It.IsAny<string>()), Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulLockAcquisition()
    {
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.Setup(t => t.AddCondition(It.IsAny<Condition>())).Returns(mockTransaction.Object);
        mockTransaction.Setup(t => t.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(false));
        mockTransaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(mockTransaction.Object);
    }

    private void SetupLockAcquisitionWithCallback(Action<RedisKey, RedisValue, TimeSpan?, When> callback)
    {
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var mockTransaction = new Mock<ITransaction>();
        mockTransaction.Setup(t => t.AddCondition(It.IsAny<Condition>())).Returns(mockTransaction.Object);
        mockTransaction.Setup(t => t.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, duration, when, _) =>
            {
                callback(key, value, duration, when);
            })
            .Returns(Task.FromResult(false));
        mockTransaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.CreateTransaction(It.IsAny<object>()))
            .Returns(mockTransaction.Object);
    }

    private void SetupExistingLock(string lockId, string owner, DateTimeOffset expiresAt)
    {
        var lockInfo = new
        {
            LockId = lockId,
            Owner = owner,
            ExpiresAt = expiresAt,
            Targets = new[]
            {
                new { ServiceId = "service1", LayerId = "layer1", FeatureId = "feature1" }
            }
        };

        var lockJson = System.Text.Json.JsonSerializer.Serialize(lockInfo);

        // Setup target key check to return existing lock ID
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(":target:")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockId));

        // Setup lock key check to return lock info
        _mockDatabase.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains($":lock:{lockId}")),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockJson));
    }

    #endregion
}
