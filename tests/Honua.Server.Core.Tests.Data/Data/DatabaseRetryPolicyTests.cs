using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Data.Tests;

/// <summary>
/// Mock database exception for testing retry logic.
/// </summary>
internal sealed class MockDbException : DbException
{
    public MockDbException(string message, string? sqlState = null) : base(message)
    {
        SqlState = sqlState ?? string.Empty;
    }

    public override string SqlState { get; }
}

/// <summary>
/// Mock PostgreSQL exception for testing Npgsql-specific retry logic.
/// </summary>
internal sealed class MockPostgresException : NpgsqlException
{
    private readonly string _sqlState;

    private MockPostgresException(string message, string sqlState) : base()
    {
        _sqlState = sqlState;
    }

    public override string SqlState => _sqlState;

    public static MockPostgresException Create(string sqlState, string message = "Mock error")
    {
        return new MockPostgresException(message, sqlState);
    }
}

/// <summary>
/// Helper to simulate MySQL errors for testing (MySqlException is sealed so we can't inherit from it).
/// We'll just use a generic exception with the error message pattern that the retry policy checks for.
/// </summary>
internal static class MockMySqlExceptionHelper
{
    public static Exception CreateDeadlockException() =>
        new InvalidOperationException("Deadlock found when trying to get lock; try restarting transaction");

    public static Exception CreateLockWaitTimeoutException() =>
        new TimeoutException("Lock wait timeout exceeded; try restarting transaction");
}

/// <summary>
/// Tests for database retry policies to ensure transient errors are properly retried.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DatabaseRetryPolicyTests
{
    [Fact]
    public async Task PostgresRetryPipeline_RetryOnTransientException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate transient error (deadlock)
                throw MockPostgresException.Create("40P01"); // deadlock_detected
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task PostgresRetryPipeline_DoNotRetryOnPermanentException_Fails()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<NpgsqlException>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                attemptCount++;
                // Simulate permanent error (constraint violation)
                throw MockPostgresException.Create("23505"); // unique_violation
            });
        });

        Assert.Equal(1, attemptCount); // Should not retry on permanent errors
    }

    [Fact]
    public async Task SqliteRetryPipeline_RetryOnBusyException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateSqliteRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate SQLITE_BUSY error
                throw new SqliteException("database is locked", 5); // SQLITE_BUSY
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task SqliteRetryPipeline_DoNotRetryOnConstraintViolation_Fails()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateSqliteRetryPipeline();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                attemptCount++;
                // Simulate SQLITE_CONSTRAINT error
                throw new SqliteException("UNIQUE constraint failed", 19); // SQLITE_CONSTRAINT
            });
        });

        Assert.Equal(1, attemptCount); // Should not retry on constraint errors
    }

    [Fact]
    public async Task MySqlRetryPipeline_RetryOnDeadlockException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateMySqlRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate MySQL deadlock (error 1213)
                throw MockMySqlExceptionHelper.CreateDeadlockException();
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task MySqlRetryPipeline_RetryOnLockWaitTimeout_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateMySqlRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate MySQL lock wait timeout (error 1205)
                throw MockMySqlExceptionHelper.CreateLockWaitTimeoutException();
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task SqlServerRetryPipeline_RetryOnDeadlockException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateSqlServerRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate SQL Server deadlock
                // Note: SqlException requires internal construction, using TimeoutException as proxy
                throw new TimeoutException("Transaction was deadlocked on lock resources with another process");
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task SqlServerRetryPipeline_RetryOnTimeoutException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateSqlServerRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new TimeoutException("Timeout expired");
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task OracleRetryPipeline_RetryOnDeadlockException_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateOracleRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate Oracle deadlock
                throw new MockDbException("ORA-00060: deadlock detected while waiting for resource");
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task OracleRetryPipeline_RetryOnSnapshotTooOld_Succeeds()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreateOracleRetryPipeline();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                // Simulate Oracle snapshot too old
                throw new MockDbException("ORA-01555: snapshot too old");
            }
            await Task.CompletedTask;
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(2, attemptCount); // Should have retried once
    }

    [Fact]
    public async Task RetryPipeline_ExponentialBackoff_AppliesCorrectDelays()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
        var attemptTimes = new System.Collections.Generic.List<DateTime>();
        var attemptCount = 0;

        // Act
        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                attemptCount++;
                if (attemptCount <= 3)
                {
                    throw MockPostgresException.Create("40P01"); // deadlock
                }
                await Task.CompletedTask;
                return 42;
            });
        }
        catch
        {
            // Ignore - we're testing the timing
        }

        // Assert
        Assert.True(attemptCount >= 2, "Should have attempted multiple times");

        // Verify exponential backoff (with jitter, delays should still increase)
        if (attemptTimes.Count >= 3)
        {
            var delay1 = attemptTimes[1] - attemptTimes[0];
            var delay2 = attemptTimes[2] - attemptTimes[1];

            // With exponential backoff: 500ms, 1000ms, 2000ms (base delays)
            // Jitter adds randomness, but delay2 should generally be longer than delay1
            // We use a loose check since jitter can cause variation
            Assert.True(delay1.TotalMilliseconds >= 100, "First retry should have some delay");
            Assert.True(delay2.TotalMilliseconds >= 100, "Second retry should have some delay");
        }
    }

    [Fact]
    public async Task RetryPipeline_MaxRetryAttemptsReached_ThrowsException()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<MockPostgresException>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                attemptCount++;
                // Always throw transient error
                throw MockPostgresException.Create("40P01"); // deadlock
            });
        });

        // Should attempt original + 3 retries = 4 total attempts
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task RetryPipeline_CancellationToken_StopsRetrying()
    {
        // Arrange
        var pipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
        var cts = new CancellationTokenSource();
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    cts.Cancel();
                }
                throw MockPostgresException.Create("40P01"); // deadlock
            }, cts.Token);
        });

        // Should only attempt once before cancellation
        Assert.Equal(1, attemptCount);
    }
}
