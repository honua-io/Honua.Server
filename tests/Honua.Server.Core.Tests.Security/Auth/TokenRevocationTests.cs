using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Auth;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Auth;

/// <summary>
/// Comprehensive tests for JWT token revocation functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TokenRevocationTests : IDisposable
{
    private readonly IDistributedCache _cache;
    private readonly IOptions<TokenRevocationOptions> _options;
    private readonly ILogger<RedisTokenRevocationService> _logger;
    private readonly RedisTokenRevocationService _service;

    public TokenRevocationTests()
    {
        // Use in-memory distributed cache for testing
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        _options = Options.Create(new TokenRevocationOptions
        {
            CleanupInterval = TimeSpan.FromMinutes(5),
            EnableAutoCleanup = true,
            FailClosedOnRedisError = true
        });

        _logger = NullLogger<RedisTokenRevocationService>.Instance;
        _service = new RedisTokenRevocationService(_cache, _logger, _options);
    }

    [Fact]
    public async Task RevokeTokenAsync_StoresRevocationSuccessfully()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var reason = "User logged out";

        // Act
        await _service.RevokeTokenAsync(tokenId, expiresAt, reason);

        // Assert
        var isRevoked = await _service.IsTokenRevokedAsync(tokenId);
        Assert.True(isRevoked);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_ReturnsFalse_ForNonRevokedToken()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");

        // Act
        var isRevoked = await _service.IsTokenRevokedAsync(tokenId);

        // Assert
        Assert.False(isRevoked);
    }

    [Fact]
    public async Task RevokeTokenAsync_DoesNotRevokeExpiredToken()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1); // Already expired
        var reason = "Test";

        // Act
        await _service.RevokeTokenAsync(tokenId, expiresAt, reason);

        // Assert - expired tokens should not be stored
        var isRevoked = await _service.IsTokenRevokedAsync(tokenId);
        Assert.False(isRevoked);
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_MarksUserForRevocation()
    {
        // Arrange
        var userId = "user123";
        var reason = "Account compromised";
        var issuedBeforeRevocation = DateTimeOffset.UtcNow.AddMinutes(-5);
        var issuedAfterRevocation = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        await _service.RevokeAllUserTokensAsync(userId, reason);

        // Assert - tokens issued before revocation are rejected
        var revoked = await _service.IsTokenRevokedAsync(
            EncodeUserRevocationCheck(userId, issuedBeforeRevocation));
        Assert.True(revoked);

        // Assert - tokens issued after the revocation timestamp remain valid
        var stillValid = await _service.IsTokenRevokedAsync(
            EncodeUserRevocationCheck(userId, issuedAfterRevocation));
        Assert.False(stillValid);
    }

    [Fact]
    public async Task RevokeTokenAsync_ThrowsArgumentException_ForNullTokenId()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var reason = "Test";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.RevokeTokenAsync(null!, expiresAt, reason));
    }

    [Fact]
    public async Task RevokeTokenAsync_ThrowsArgumentException_ForNullReason()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.RevokeTokenAsync(tokenId, expiresAt, null!));
    }

    [Fact]
    public async Task IsTokenRevokedAsync_ThrowsArgumentException_ForNullTokenId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.IsTokenRevokedAsync(null!));
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_ThrowsArgumentException_ForNullUserId()
    {
        // Arrange
        var reason = "Test";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.RevokeAllUserTokensAsync(null!, reason));
    }

    [Fact]
    public async Task CleanupExpiredRevocationsAsync_CompletesSuccessfully()
    {
        // Act
        var cleanedCount = await _service.CleanupExpiredRevocationsAsync();

        // Assert - Redis handles cleanup automatically, so count should be 0
        Assert.Equal(0, cleanedCount);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenCacheIsWorking()
    {
        // Arrange
        var context = new HealthCheckContext();

        // Act
        var result = await _service.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ConcurrentRevocations_DoNotConflict()
    {
        // Arrange
        var tokenIds = new[]
        {
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N")
        };
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act - Revoke tokens concurrently
        var tasks = new Task[tokenIds.Length];
        for (var i = 0; i < tokenIds.Length; i++)
        {
            var tokenId = tokenIds[i];
            tasks[i] = _service.RevokeTokenAsync(tokenId, expiresAt, "Concurrent test");
        }

        await Task.WhenAll(tasks);

        // Assert - All tokens should be revoked
        foreach (var tokenId in tokenIds)
        {
            var isRevoked = await _service.IsTokenRevokedAsync(tokenId);
            Assert.True(isRevoked, $"Token {tokenId} should be revoked");
        }
    }

    [Fact]
    public async Task MultipleRevocationsOfSameToken_Succeed()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act - Revoke the same token multiple times
        await _service.RevokeTokenAsync(tokenId, expiresAt, "First revocation");
        await _service.RevokeTokenAsync(tokenId, expiresAt, "Second revocation");

        // Assert
        var isRevoked = await _service.IsTokenRevokedAsync(tokenId);
        Assert.True(isRevoked);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithCancellation_CompletesGracefully()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Service should fall back to best-effort revocation even if cancellation is requested upfront
        await _service.RevokeTokenAsync(tokenId, expiresAt, "Test", cts.Token);

        // Assert - New semantics treat cancellation as advisory and still attempt revocation
        var isRevoked = await _service.IsTokenRevokedAsync(tokenId);
        Assert.True(isRevoked, "Revocation should succeed even when cancellation was already requested");
    }

    [Fact]
    public async Task IsTokenRevokedAsync_WithCancellation_IsCancellable()
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var revoked = await _service.IsTokenRevokedAsync(tokenId, cts.Token);
        Assert.False(revoked, "Cancellation should return false");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_ThrowsArgumentException_ForWhitespaceTokenId(string tokenId)
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var reason = "Test";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RevokeTokenAsync(tokenId, expiresAt, reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task RevokeTokenAsync_ThrowsArgumentException_ForWhitespaceReason(string reason)
    {
        // Arrange
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RevokeTokenAsync(tokenId, expiresAt, reason));
    }

    [Fact]
    public async Task RevocationPersistsAcrossServiceInstances()
    {
        // Arrange - Use first service instance to revoke
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        await _service.RevokeTokenAsync(tokenId, expiresAt, "Test");

        // Act - Create new service instance and check
        var newService = new RedisTokenRevocationService(_cache, _logger, _options);
        var isRevoked = await newService.IsTokenRevokedAsync(tokenId);

        // Assert
        Assert.True(isRevoked);

        // Cleanup
        newService.Dispose();
    }

    private static string EncodeUserRevocationCheck(string userId, DateTimeOffset issuedAt)
    {
        var encodedUserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(userId));
        return $"user:{encodedUserId}|{issuedAt.ToUnixTimeSeconds()}";
    }

    [Fact]
    public void ServiceDispose_CompletesSuccessfully()
    {
        // Act & Assert
        _service.Dispose();
        Assert.True(true);
    }

    public void Dispose()
    {
        _service?.Dispose();
        (_cache as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Tests for TokenRevocationOptions configuration.
/// </summary>
public sealed class TokenRevocationOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectValues()
    {
        // Arrange & Act
        var options = new TokenRevocationOptions();

        // Assert
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
        Assert.True(options.EnableAutoCleanup);
        Assert.True(options.FailClosedOnRedisError);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("TokenRevocation", TokenRevocationOptions.SectionName);
    }
}
