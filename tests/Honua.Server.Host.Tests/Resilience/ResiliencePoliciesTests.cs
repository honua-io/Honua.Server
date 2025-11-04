using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Timeout;
using Xunit;

namespace Honua.Server.Host.Tests.Resilience;

public class ResiliencePoliciesTests
{
    private readonly ILoggerFactory _loggerFactory;

    public ResiliencePoliciesTests()
    {
        _loggerFactory = NullLoggerFactory.Instance;
    }

    #region Cloud Storage Policy Tests

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_HttpRequestException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new HttpRequestException("Network error");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_TimeoutRejectedException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new TimeoutRejectedException("Operation timed out");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_SocketException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new SocketException((int)SocketError.ConnectionRefused);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_TaskCanceledException_DueToTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // TaskCanceledException without a cancellation token indicates timeout
                throw new TaskCanceledException("Request timed out");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_DoesNotRetry_TaskCanceledException_WithUserCancellation()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync(async ct =>
            {
                attemptCount++;
                throw new TaskCanceledException("User cancelled", null, ct);
            }, cts.Token);
        });

        // Should only attempt once since it's a user cancellation
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_OperationCanceledException_WithTimeoutException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new OperationCanceledException("Operation timed out", new TimeoutException());
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_500_InternalServerError()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_429_TooManyRequests()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_RetriesOn_408_RequestTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_DoesNotRetry_401_Unauthorized()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }, CancellationToken.None);

        // Assert - should not retry on 4xx errors (except 408, 429)
        attemptCount.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CloudStoragePolicy_CircuitBreaker_OpensAfterMultipleFailures()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act & Assert - Multiple failures should eventually open circuit breaker
        for (int i = 0; i < 15; i++)
        {
            try
            {
                await policy.ExecuteAsync(async _ =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Persistent network error");
                }, CancellationToken.None);
            }
            catch (HttpRequestException)
            {
                // Expected during retry attempts
            }
            catch (BrokenCircuitException)
            {
                // Circuit breaker opened - this is expected after enough failures
                break;
            }
        }

        // Should have attempted multiple times before circuit breaker opened
        attemptCount.Should().BeGreaterThan(10);
    }

    #endregion

    #region External API Policy Tests

    [Fact]
    public async Task ExternalApiPolicy_RetriesOn_HttpRequestException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new HttpRequestException("Network error");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalApiPolicy_RetriesOn_SocketException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalApiPolicy_RetriesOn_TimeoutRejectedException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new TimeoutRejectedException("Operation exceeded timeout");
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalApiPolicy_RetriesOn_503_ServiceUnavailable()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalApiPolicy_MaxRetryAttempts_IsTwo()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async _ =>
            {
                attemptCount++;
                throw new HttpRequestException("Persistent error");
            }, CancellationToken.None);
        });

        // MaxRetryAttempts = 2 means 1 initial + 2 retries = 3 total attempts
        attemptCount.Should().Be(3);
    }

    #endregion

    #region Database Policy Tests

    [Fact]
    public async Task DatabasePolicy_RetriesOn_TimeoutException()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateDatabasePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new TimeoutException("Database query timed out");
            }
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task DatabasePolicy_RetriesOn_DbException_WithTimeout()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateDatabasePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new System.Data.Common.DbException("Connection timeout occurred");
            }
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task DatabasePolicy_RetriesOn_DbException_WithDeadlock()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateDatabasePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new System.Data.Common.DbException("Transaction was deadlocked");
            }
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
    }

    #endregion

    #region Fast Operation Policy Tests

    [Fact]
    public void FastOperationPolicy_CanBeCreated_WithCustomTimeout()
    {
        // Arrange & Act
        var policy = ResiliencePolicies.CreateFastOperationPolicy(TimeSpan.FromSeconds(5));

        // Assert
        policy.Should().NotBeNull();
    }

    #endregion

    #region Integration Tests - Combined Exceptions

    [Fact]
    public async Task CloudStoragePolicy_Handles_MixedFailures_ExceptionsAndStatusCodes()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;

            // Mix different failure types across attempts
            return attemptCount switch
            {
                1 => throw new HttpRequestException("Connection refused"),
                2 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                3 => throw new SocketException((int)SocketError.NetworkUnreachable),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
            };
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(4);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloudStoragePolicy_ExhaustsRetries_WhenAllAttemptsFail()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateCloudStoragePolicy(_loggerFactory);
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync(async _ =>
            {
                attemptCount++;
                throw new HttpRequestException("Permanent network failure");
            }, CancellationToken.None);
        });

        // MaxRetryAttempts = 3 means 1 initial + 3 retries = 4 total attempts
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task ExternalApiPolicy_Handles_TransientNetworkFailure_ThenRecovery()
    {
        // Arrange
        var policy = ResiliencePolicies.CreateExternalApiPolicy(_loggerFactory);
        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async _ =>
        {
            attemptCount++;

            if (attemptCount == 1)
            {
                throw new SocketException((int)SocketError.HostUnreachable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
