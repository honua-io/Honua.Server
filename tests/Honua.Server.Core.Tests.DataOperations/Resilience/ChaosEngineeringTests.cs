using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Resilience;

/// <summary>
/// Chaos engineering tests to verify error boundary handling under various failure scenarios.
/// </summary>
[Trait("Category", "Unit")]
public class ChaosEngineeringTests
{
    private readonly ILogger<ResilientServiceExecutor> _logger;
    private readonly ResilientServiceExecutor _executor;

    public ChaosEngineeringTests()
    {
        _logger = NullLogger<ResilientServiceExecutor>.Instance;
        _executor = new ResilientServiceExecutor(_logger);
    }

    [Fact]
    public async Task ExecuteWithFallback_PrimaryFails_FallbackSucceeds()
    {
        // Arrange
        var primaryCalled = false;
        var fallbackCalled = false;

        Task<string> Primary(CancellationToken ct)
        {
            primaryCalled = true;
            throw new ServiceUnavailableException("TestService", "Simulated failure");
        }

        Task<string> Fallback(Exception ex, CancellationToken ct)
        {
            fallbackCalled = true;
            return Task.FromResult("Fallback Value");
        }

        // Act
        var result = await _executor.ExecuteWithFallbackAsync(
            Primary,
            Fallback,
            "TestOperation");

        // Assert
        Assert.True(primaryCalled);
        Assert.True(fallbackCalled);
        Assert.True(result.IsFromFallback);
        Assert.Equal("Fallback Value", result.Value);
        Assert.Equal(FallbackReason.ServiceUnavailable, result.FallbackReason);
    }

    [Fact]
    public async Task ExecuteWithFallback_PrimarySucceeds_FallbackNotCalled()
    {
        // Arrange
        var primaryCalled = false;
        var fallbackCalled = false;

        Task<string> Primary(CancellationToken ct)
        {
            primaryCalled = true;
            return Task.FromResult("Primary Value");
        }

        Task<string> Fallback(Exception ex, CancellationToken ct)
        {
            fallbackCalled = true;
            return Task.FromResult("Fallback Value");
        }

        // Act
        var result = await _executor.ExecuteWithFallbackAsync(
            Primary,
            Fallback,
            "TestOperation");

        // Assert
        Assert.True(primaryCalled);
        Assert.False(fallbackCalled);
        Assert.False(result.IsFromFallback);
        Assert.Equal("Primary Value", result.Value);
    }

    [Fact]
    public async Task ExecuteWithDefault_TransientError_ReturnsDefault()
    {
        // Arrange
        Task<int> Operation(CancellationToken ct)
        {
            throw new ServiceTimeoutException("TestService", TimeSpan.FromSeconds(30));
        }

        // Act
        var result = await _executor.ExecuteWithDefaultAsync(
            Operation,
            defaultValue: -1,
            "TestOperation");

        // Assert
        Assert.True(result.IsFromFallback);
        Assert.Equal(-1, result.Value);
        Assert.Equal(FallbackReason.Timeout, result.FallbackReason);
    }

    [Fact]
    public async Task ExecuteWithDefault_PermanentError_ReturnsDefault()
    {
        // Arrange
        Task<int> Operation(CancellationToken ct)
        {
            throw new InvalidOperationException("Permanent error");
        }

        // Act
        var result = await _executor.ExecuteWithDefaultAsync(
            Operation,
            defaultValue: -1,
            "TestOperation");

        // Assert
        Assert.True(result.IsFromFallback);
        Assert.Equal(-1, result.Value);
        Assert.Equal(FallbackReason.NoFallbackAvailable, result.FallbackReason);
    }

    [Fact]
    public async Task ExecuteWithMultipleFallbacks_FirstFallbackSucceeds()
    {
        // Arrange
        Task<string> Primary(CancellationToken ct)
        {
            throw new ServiceUnavailableException("Primary", "Failed");
        }

        var fallback1Called = false;
        var fallback2Called = false;

        Task<string> Fallback1(Exception ex, CancellationToken ct)
        {
            fallback1Called = true;
            return Task.FromResult("Fallback1");
        }

        Task<string> Fallback2(Exception ex, CancellationToken ct)
        {
            fallback2Called = true;
            return Task.FromResult("Fallback2");
        }

        // Act
        var result = await _executor.ExecuteWithMultipleFallbacksAsync(
            Primary,
            new[] { Fallback1, Fallback2 },
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.True(fallback1Called);
        Assert.False(fallback2Called); // Should not reach second fallback
        Assert.Equal("Fallback1", result.Value);
        Assert.True(result.IsFromFallback);
    }

    [Fact]
    public async Task ExecuteWithMultipleFallbacks_AllFallbacksFail_ReturnsDefault()
    {
        // Arrange
        Task<string> Primary(CancellationToken ct)
        {
            throw new ServiceUnavailableException("Primary", "Failed");
        }

        Task<string> Fallback1(Exception ex, CancellationToken ct)
        {
            throw new ServiceUnavailableException("Fallback1", "Failed");
        }

        Task<string> Fallback2(Exception ex, CancellationToken ct)
        {
            throw new ServiceUnavailableException("Fallback2", "Failed");
        }

        // Act
        var result = await _executor.ExecuteWithMultipleFallbacksAsync(
            Primary,
            new[] { Fallback1, Fallback2 },
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.Equal("Default", result.Value);
        Assert.True(result.IsFromFallback);
        Assert.Equal(FallbackReason.NoFallbackAvailable, result.FallbackReason);
    }

    [Fact]
    public async Task ExecuteWithStaleCacheFallback_PrimaryFails_ReturnsStaleData()
    {
        // Arrange
        Task<string> Primary(CancellationToken ct)
        {
            throw new ServiceTimeoutException("TestService", TimeSpan.FromSeconds(30));
        }

        Task<string?> GetStaleCache(CancellationToken ct)
        {
            return Task.FromResult<string?>("Stale Cache Data");
        }

        // Act
        var result = await _executor.ExecuteWithStaleCacheFallbackAsync(
            Primary,
            GetStaleCache,
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.Equal("Stale Cache Data", result.Value);
        Assert.True(result.IsFromFallback);
        Assert.Equal(FallbackReason.StaleCache, result.FallbackReason);
    }

    [Fact]
    public async Task ExecuteWithStaleCacheFallback_NoCacheAvailable_ReturnsDefault()
    {
        // Arrange
        Task<string> Primary(CancellationToken ct)
        {
            throw new ServiceUnavailableException("TestService", "Failed");
        }

        Task<string?> GetStaleCache(CancellationToken ct)
        {
            return Task.FromResult<string?>(null); // No cache available
        }

        // Act
        var result = await _executor.ExecuteWithStaleCacheFallbackAsync(
            Primary,
            GetStaleCache,
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.Equal("Default", result.Value);
        Assert.True(result.IsFromFallback);
        Assert.Equal(FallbackReason.NoFallbackAvailable, result.FallbackReason);
    }

    [Fact]
    public async Task CircuitBreakerOpen_ShouldBeDetected()
    {
        // Arrange
        Task<string> Operation(CancellationToken ct)
        {
            throw new CircuitBreakerOpenException("TestService", TimeSpan.FromSeconds(30));
        }

        // Act
        var result = await _executor.ExecuteWithDefaultAsync(
            Operation,
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.Equal("Default", result.Value);
        Assert.True(result.IsFromFallback);
        Assert.Equal(FallbackReason.CircuitBreakerOpen, result.FallbackReason);
    }

    [Fact]
    public async Task ServiceThrottled_ShouldBeDetected()
    {
        // Arrange
        Task<string> Operation(CancellationToken ct)
        {
            throw new ServiceThrottledException("TestService", TimeSpan.FromSeconds(60));
        }

        // Act
        var result = await _executor.ExecuteWithDefaultAsync(
            Operation,
            defaultValue: "Default",
            operationName: "TestOperation");

        // Assert
        Assert.Equal("Default", result.Value);
        Assert.True(result.IsFromFallback);
        Assert.Equal(FallbackReason.Throttled, result.FallbackReason);
    }

    [Fact]
    public async Task RandomFailures_ShouldHandleGracefully()
    {
        // Arrange: Simulate 50% random failures
        var random = new Random(42);
        var successCount = 0;
        var fallbackCount = 0;

        // Act: Run 100 operations
        for (int i = 0; i < 100; i++)
        {
            var result = await _executor.ExecuteWithDefaultAsync(
                ct =>
                {
                    if (random.Next(100) < 50)
                    {
                        throw new ServiceUnavailableException("TestService", "Random failure");
                    }
                    return Task.FromResult(i);
                },
                defaultValue: -1,
                operationName: "RandomOperation");

            if (result.IsFromFallback)
                fallbackCount++;
            else
                successCount++;
        }

        // Assert: Should handle both successes and failures
        Assert.True(successCount > 0, "Should have some successful operations");
        Assert.True(fallbackCount > 0, "Should have some fallback operations");
        Assert.Equal(100, successCount + fallbackCount);
    }

    [Fact]
    public async Task IntermittentFailures_ShouldEventuallySucceed()
    {
        // Arrange: Fail first 3 times, then succeed
        var attemptCount = 0;

        Task<string> Operation(CancellationToken ct)
        {
            attemptCount++;
            if (attemptCount <= 3)
            {
                throw new ServiceUnavailableException("TestService", "Intermittent failure");
            }
            return Task.FromResult("Success");
        }

        // Act: Try with multiple fallbacks
        var result = await _executor.ExecuteWithMultipleFallbacksAsync(
            Operation,
            new[]
            {
                (Func<Exception, CancellationToken, Task<string>>)(async (ex, ct) => await Operation(ct)),
                async (ex, ct) => await Operation(ct),
                async (ex, ct) => await Operation(ct)
            },
            defaultValue: "Default",
            operationName: "IntermittentOperation");

        // Assert
        Assert.Equal("Success", result.Value);
        Assert.True(result.IsFromFallback); // Succeeded on a fallback attempt
    }
}
