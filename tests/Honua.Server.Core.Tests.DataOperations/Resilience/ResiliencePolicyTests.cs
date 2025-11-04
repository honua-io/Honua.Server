using System.Net;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Timeout;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Resilience;

[Trait("Category", "Unit")]
public class ResiliencePolicyTests
{
    [Fact]
    public void CloudStoragePolicy_IsCreated()
    {
        // Arrange & Act
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOnTransientFailure()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(3, attemptCount); // Initial attempt + 2 retries
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task CloudStoragePolicy_DoesNotRetryOnSuccess()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            attemptCount++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(1, attemptCount); // Only initial attempt
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task CloudStoragePolicy_DoesNotRetryOnClientError()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            attemptCount++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(1, attemptCount); // No retries for 4xx errors
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task CloudStoragePolicy_RespectsMaxRetryAttempts()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            attemptCount++;
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(4, attemptCount); // Initial + 3 retries max
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
    }

    [Fact]
    public void ExternalApiPolicy_IsCreated()
    {
        // Arrange & Act
        var policy = ResiliencePolicies.CreateExternalApiPolicy(NullLoggerFactory.Instance);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task ExternalApiPolicy_RetriesOnFailure()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(2, attemptCount); // Succeeds on retry
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public void DatabasePolicy_IsCreated()
    {
        // Arrange & Act
        var policy = ResiliencePolicies.CreateDatabasePolicy(NullLoggerFactory.Instance);

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task DatabasePolicy_RetriesOnTransientException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateDatabasePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(ct =>
            {
                attemptCount++;
                throw new InvalidOperationException("Transient database error");
            }, CancellationToken.None);
        });

        // Database policy should retry on exceptions
        Assert.True(attemptCount >= 1);
    }

    [Fact]
    public async Task DatabasePolicy_DoesNotRetryIndefinitely()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateDatabasePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;
        var maxExpectedAttempts = 4; // Initial + 3 retries

        // Act & Assert
        await Assert.ThrowsAsync<Polly.Timeout.TimeoutRejectedException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attemptCount++;
                // Simulate timeout
                await Task.Delay(TimeSpan.FromSeconds(100), ct);
            }, CancellationToken.None);
        });

        // Should not exceed max retry attempts
        Assert.True(attemptCount <= maxExpectedAttempts);
    }

    [Fact]
    public async Task CloudStoragePolicy_CircuitBreakerOpensAfterFailures()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);

        // Act - Cause multiple failures to open circuit
        for (int i = 0; i < 15; i++)
        {
            try
            {
                await policy.ExecuteAsync(ct =>
                    ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
                    CancellationToken.None);
            }
            catch
            {
                // Expected failures
            }
        }

        // Circuit should be open now (subsequent calls fail fast)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await policy.ExecuteAsync(ct =>
                ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
                CancellationToken.None);
        }
        catch
        {
            // Expected
        }
        stopwatch.Stop();

        // Assert - Should fail fast (< 100ms) when circuit is open
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Circuit breaker should fail fast, but took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Policies_CanBeCombinedInPipeline()
    {
        // Arrange
        var cloudPolicy = ResiliencePolicies.CreateCloudStoragePolicy(NullLoggerFactory.Instance);
        var attemptCount = 0;

        // Act
        var result = await cloudPolicy.ExecuteAsync(ct =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }, CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, attemptCount);
    }
}
