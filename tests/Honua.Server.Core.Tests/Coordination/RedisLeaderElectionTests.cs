// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Honua.Server.Core.Tests.Coordination;

[Trait("Category", "Unit")]
public class RedisLeaderElectionTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisLeaderElection>> _mockLogger;
    private readonly LeaderElectionOptions _options;
    private readonly RedisLeaderElection _leaderElection;

    public RedisLeaderElectionTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisLeaderElection>>();

        _options = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 30,
            RenewalIntervalSeconds = 10,
            ResourceName = "test-resource",
            KeyPrefix = "honua:leader:",
            EnableDetailedLogging = true
        };

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _leaderElection = new RedisLeaderElection(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(_options));
    }

    public void Dispose()
    {
        _leaderElection?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedisLeaderElection(
            null!,
            _mockLogger.Object,
            Options.Create(_options));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedisLeaderElection(
            _mockRedis.Object,
            null!,
            Options.Create(_options));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RedisLeaderElection(
            _mockRedis.Object,
            _mockLogger.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new LeaderElectionOptions
        {
            LeaseDurationSeconds = 10,
            RenewalIntervalSeconds = 15 // Invalid: greater than lease duration
        };

        // Act
        var act = () => new RedisLeaderElection(
            _mockRedis.Object,
            _mockLogger.Object,
            Options.Create(invalidOptions));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InstanceId_IsNotNullOrEmpty()
    {
        // Assert
        _leaderElection.InstanceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void InstanceId_ContainsMachineName()
    {
        // Assert
        _leaderElection.InstanceId.Should().Contain(Environment.MachineName);
    }

    [Fact]
    public void InstanceId_ContainsProcessId()
    {
        // Assert
        _leaderElection.InstanceId.Should().Contain(Environment.ProcessId.ToString());
    }

    #endregion

    #region TryAcquireLeadershipAsync Tests

    [Fact]
    public async Task TryAcquireLeadershipAsync_WithNullResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.TryAcquireLeadershipAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WithEmptyResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.TryAcquireLeadershipAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WithWhitespaceResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.TryAcquireLeadershipAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_OnFirstAttempt_ReturnsTrue()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.Is<When>(w => w == When.NotExists),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WhenAlreadyAcquiredByAnotherInstance_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.Is<When>(w => w == When.NotExists),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_UsesCorrectRedisKey()
    {
        // Arrange
        RedisKey? capturedKey = null;
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, _, _, _, _) =>
            {
                capturedKey = key;
            })
            .ReturnsAsync(true);

        // Act
        await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        capturedKey.Should().NotBeNull();
        capturedKey.ToString().Should().Be("honua:leader:test-resource");
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_UsesCorrectInstanceId()
    {
        // Arrange
        RedisValue? capturedValue = null;
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((_, value, _, _, _) =>
            {
                capturedValue = value;
            })
            .ReturnsAsync(true);

        // Act
        await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        capturedValue.Should().NotBeNull();
        capturedValue.ToString().Should().Be(_leaderElection.InstanceId);
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_UsesCorrectLeaseDuration()
    {
        // Arrange
        TimeSpan? capturedExpiry = null;
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((_, _, expiry, _, _) =>
            {
                capturedExpiry = expiry;
            })
            .ReturnsAsync(true);

        // Act
        await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        capturedExpiry.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_UsesNotExistsCondition()
    {
        // Arrange
        When? capturedWhen = null;
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((_, _, _, when, _) =>
            {
                capturedWhen = when;
            })
            .ReturnsAsync(true);

        // Act
        await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        capturedWhen.Should().Be(When.NotExists);
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WhenRedisThrowsException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis connection error"));

        // Act
        var result = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WhenRedisThrowsException_LogsError()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis connection error"));

        // Act
        await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAcquireLeadershipAsync_WithCancellationToken_PassesThrough()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _leaderElection.TryAcquireLeadershipAsync("test-resource", cts.Token);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region RenewLeadershipAsync Tests

    [Fact]
    public async Task RenewLeadershipAsync_WithNullResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.RenewLeadershipAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RenewLeadershipAsync_ForOwner_ReturnsTrue()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        var result = await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RenewLeadershipAsync_ForNonOwner_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0));

        // Act
        var result = await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RenewLeadershipAsync_ExecutesLuaScript()
    {
        // Arrange
        string? capturedScript = null;
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        capturedScript.Should().NotBeNull();
        capturedScript.Should().Contain("get");
        capturedScript.Should().Contain("expire");
    }

    [Fact]
    public async Task RenewLeadershipAsync_PassesCorrectKey()
    {
        // Arrange
        RedisKey[]? capturedKeys = null;
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, keys, _, _) =>
            {
                capturedKeys = keys;
            })
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        capturedKeys.Should().NotBeNull();
        capturedKeys.Should().HaveCount(1);
        capturedKeys![0].ToString().Should().Be("honua:leader:test-resource");
    }

    [Fact]
    public async Task RenewLeadershipAsync_PassesCorrectArguments()
    {
        // Arrange
        RedisValue[]? capturedArgs = null;
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, args, _) =>
            {
                capturedArgs = args;
            })
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs.Should().HaveCount(2);
        capturedArgs![0].ToString().Should().Be(_leaderElection.InstanceId);
        capturedArgs[1].ToString().Should().Be("30"); // Lease duration in seconds
    }

    [Fact]
    public async Task RenewLeadershipAsync_WhenRedisThrowsException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis script error"));

        // Act
        var result = await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RenewLeadershipAsync_WhenRedisThrowsException_LogsError()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis script error"));

        // Act
        await _leaderElection.RenewLeadershipAsync("test-resource");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region ReleaseLeadershipAsync Tests

    [Fact]
    public async Task ReleaseLeadershipAsync_WithNullResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.ReleaseLeadershipAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_ForOwner_ReturnsTrue()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        var result = await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_ForNonOwner_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0));

        // Act
        var result = await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_ForNonOwner_DoesNotThrow()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0));

        // Act
        var act = async () => await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_ExecutesLuaScript()
    {
        // Arrange
        string? capturedScript = null;
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((script, _, _, _) =>
            {
                capturedScript = script;
            })
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        capturedScript.Should().NotBeNull();
        capturedScript.Should().Contain("get");
        capturedScript.Should().Contain("del");
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_PassesCorrectInstanceId()
    {
        // Arrange
        RedisValue[]? capturedArgs = null;
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, args, _) =>
            {
                capturedArgs = args;
            })
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs.Should().HaveCount(1);
        capturedArgs![0].ToString().Should().Be(_leaderElection.InstanceId);
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_WhenRedisThrowsException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis script error"));

        // Act
        var result = await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLeadershipAsync_WhenRedisThrowsException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis script error"));

        // Act
        var act = async () => await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region IsLeaderAsync Tests

    [Fact]
    public async Task IsLeaderAsync_WithNullResourceName_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _leaderElection.IsLeaderAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IsLeaderAsync_WhenLeader_ReturnsTrue()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(_leaderElection.InstanceId));

        // Act
        var result = await _leaderElection.IsLeaderAsync("test-resource");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLeaderAsync_WhenFollower_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("different-instance-id"));

        // Act
        var result = await _leaderElection.IsLeaderAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsLeaderAsync_WhenNoLeader_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _leaderElection.IsLeaderAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsLeaderAsync_WhenRedisThrowsException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis connection error"));

        // Act
        var result = await _leaderElection.IsLeaderAsync("test-resource");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsLeaderAsync_WhenRedisThrowsException_LogsError()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis connection error"));

        // Act
        await _leaderElection.IsLeaderAsync("test-resource");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task MultipleInstances_OnlyOneCanAcquireLeadership()
    {
        // This tests the scenario where multiple instances compete
        // The first instance should succeed, subsequent should fail

        // Arrange
        var firstCallToSet = true;
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.Is<When>(w => w == When.NotExists),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                if (firstCallToSet)
                {
                    firstCallToSet = false;
                    return true;
                }
                return false;
            });

        // Act - First attempt
        var firstResult = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Act - Second attempt
        var secondResult = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
    }

    [Fact]
    public async Task LeaseExpiration_AllowsReacquisition()
    {
        // This simulates what happens when a lease expires

        // Arrange - First acquisition succeeds
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.Is<When>(w => w == When.NotExists),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act - Acquire leadership
        var firstAcquire = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Simulate lease expiration by returning false for ownership check
        _mockDatabase.Setup(db => db.StringGetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var isLeaderAfterExpiry = await _leaderElection.IsLeaderAsync("test-resource");

        // Another instance can now acquire
        var reacquire = await _leaderElection.TryAcquireLeadershipAsync("test-resource");

        // Assert
        firstAcquire.Should().BeTrue();
        isLeaderAfterExpiry.Should().BeFalse();
        reacquire.Should().BeTrue();
    }

    [Fact]
    public async Task FullLifecycle_AcquireRenewRelease()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.Is<When>(w => w == When.NotExists),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        var acquired = await _leaderElection.TryAcquireLeadershipAsync("test-resource");
        var renewed = await _leaderElection.RenewLeadershipAsync("test-resource");
        var released = await _leaderElection.ReleaseLeadershipAsync("test-resource");

        // Assert
        acquired.Should().BeTrue();
        renewed.Should().BeTrue();
        released.Should().BeTrue();
    }

    #endregion
}
