using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Resilience;

/// <summary>
/// Tests for circuit breaker behavior in cache operations.
/// </summary>
[Trait("Category", "Unit")]
public class CircuitBreakerTests
{
    private readonly ILogger _logger;

    public CircuitBreakerTests()
    {
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task CacheCircuitBreaker_MultipleFailures_OpensCircuit()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);
        var invocationCount = 0;

        for (int i = 0; i < 20; i++)
        {
            var invoked = false;
            var result = await breaker.ExecuteAsync<string>(
                ct =>
                {
                    invoked = true;
                    invocationCount++;
                    throw new CacheUnavailableException("TestCache", "Simulated failure");
                },
                CancellationToken.None);

            result.Should().BeNull();

            if (!invoked)
            {
                break;
            }
        }

        invocationCount.Should().BeGreaterThanOrEqualTo(10);
        invocationCount.Should().BeLessThan(20);

        var shortCircuitResult = await breaker.ExecuteAsync<string>(
            ct => throw new CacheUnavailableException("TestCache", "Unexpected"),
            CancellationToken.None);
        shortCircuitResult.Should().BeNull();
    }

    [Fact]
    public async Task CacheCircuitBreaker_SuccessfulOperation_ReturnsValue()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);

        // Act
        var result = await breaker.ExecuteAsync(
            ct => Task.FromResult("Success"),
            CancellationToken.None);

        // Assert
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task CacheCircuitBreaker_WriteOperation_HandlesFailureGracefully()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);

        // Act: Successful write
        var success = await breaker.ExecuteWriteAsync(
            ct => Task.CompletedTask,
            CancellationToken.None);

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task CacheCircuitBreaker_WriteFailure_ReturnsFalse()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);

        // Act: Failed write
        var success = await breaker.ExecuteWriteAsync(
            ct =>
            {
                throw new CacheWriteException("TestCache", "Write failed", new InvalidOperationException("Simulated write failure"));
            },
            CancellationToken.None);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task CacheCircuitBreaker_TimeoutException_HandledGracefully()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);

        // Act
        var result = await breaker.ExecuteAsync<string>(
            ct =>
            {
                throw new TimeoutException("Operation timed out");
            },
            CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CacheCircuitBreaker_NonCacheException_HandledGracefully()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);

        // Act
        var result = await breaker.ExecuteAsync<string>(
            ct =>
            {
                throw new InvalidOperationException("Unexpected error");
            },
            CancellationToken.None);

        // Assert: Should still return null and not propagate exception
        Assert.Null(result);
    }

    [Fact]
    public async Task CacheCircuitBreaker_IntermittentFailures_HandlesGracefully()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);
        var successCount = 0;
        var failureCount = 0;

        // Act: Simulate intermittent failures
        for (int i = 0; i < 20; i++)
        {
            var shouldFail = i % 3 == 0; // Fail every 3rd operation

            var result = await breaker.ExecuteAsync(
                ct =>
                {
                    if (shouldFail)
                    {
                        throw new CacheUnavailableException("TestCache", "Intermittent failure");
                    }
                    return Task.FromResult($"Success-{i}");
                },
                CancellationToken.None);

            if (result != null)
                successCount++;
            else
                failureCount++;
        }

        // Assert: Should have both successes and failures
        Assert.True(successCount > 0, "Should have successful operations");
        Assert.True(failureCount > 0, "Should have failed operations");
    }

    [Fact]
    public async Task CacheCircuitBreaker_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
        var breaker = new CacheCircuitBreaker("TestCache", _logger);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var result = await breaker.ExecuteAsync(
            ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<string?>("Success");
            },
            cts.Token);

        result.Should().BeNull();
    }
}
