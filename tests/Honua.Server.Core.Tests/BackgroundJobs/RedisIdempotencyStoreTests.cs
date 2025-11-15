// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Honua.Server.Core.BackgroundJobs;
using Microsoft.Extensions.Logging.Nulloggers;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Honua.Server.Core.Tests.BackgroundJobs;

/// <summary>
/// Unit tests for RedisIdempotencyStore
/// </summary>
public sealed class RedisIdempotencyStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisIdempotencyStore _store;

    public RedisIdempotencyStoreTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _store = new RedisIdempotencyStore(
            _redisMock.Object,
            NullLogger<RedisIdempotencyStore>.Instance);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ShouldReturnValue()
    {
        // Arrange
        var key = "test-key";
        var value = new TestResult { JobId = "job-123", Success = true };
        var json = System.Text.Json.JsonSerializer.Serialize(value);

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        // Act
        var result = await _store.GetAsync<TestResult>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("job-123", result.JobId);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var key = "non-existent-key";

        _databaseMock.Setup(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _store.GetAsync<TestResult>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAsync_ShouldCallStringSetWithCorrectParameters()
    {
        // Arrange
        var key = "test-key";
        var value = new TestResult { JobId = "job-456", Success = false };
        var ttl = TimeSpan.FromDays(7);

        _databaseMock.Setup(db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<RedisValue>(),
                ttl,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _store.StoreAsync(key, value, ttl);

        // Assert
        _databaseMock.Verify(db => db.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
            It.IsAny<RedisValue>(),
            ttl,
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var key = "existing-key";

        _databaseMock.Setup(db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var exists = await _store.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var key = "non-existent-key";

        _databaseMock.Setup(db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var exists = await _store.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallKeyDelete()
    {
        // Arrange
        var key = "key-to-delete";

        _databaseMock.Setup(db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _store.DeleteAsync(key);

        // Assert
        _databaseMock.Verify(db => db.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString().EndsWith(key)),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WithNullValue_ShouldThrowArgumentNullException()
    {
        // Arrange
        var key = "test-key";
        TestResult? value = null;
        var ttl = TimeSpan.FromDays(1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _store.StoreAsync(key, value!, ttl));
    }

    [Fact]
    public async Task StoreAsync_WithNegativeTtl_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var key = "test-key";
        var value = new TestResult { JobId = "job-789", Success = true };
        var ttl = TimeSpan.FromDays(-1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _store.StoreAsync(key, value, ttl));
    }

    [Fact]
    public async Task GetAsync_WithEmptyKey_ShouldThrowArgumentException()
    {
        // Arrange
        var key = string.Empty;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _store.GetAsync<TestResult>(key));
    }

    // Helper class for testing
    private class TestResult
    {
        public string JobId { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}
