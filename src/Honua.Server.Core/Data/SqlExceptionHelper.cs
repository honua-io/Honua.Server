// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Utilities;
using MySqlConnector;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides exception handling utilities for SQL database operations.
/// Consolidates vendor-specific error code checking and exception wrapping.
/// </summary>
public static class SqlExceptionHelper
{
    // MySQL Error Codes
    private const int MySqlDuplicateKeyErrorCode = 1062;
    private const int MySqlDeadlockErrorCode = 1213;
    private const int MySqlLockWaitTimeoutErrorCode = 1205;
    private const int MySqlConnectionErrorCode = 2003;
    private const int MySqlServerGoneErrorCode = 2006;
    private const int MySqlServerLostErrorCode = 2013;

    // SQL Server Error Codes
    private const int SqlServerDuplicateKeyErrorCode = 2627;
    private const int SqlServerUniqueConstraintErrorCode = 2601;
    private const int SqlServerDeadlockErrorCode = 1205;
    private const int SqlServerTimeoutErrorCode = -2;
    private const int SqlServerConnectionErrorCode = 53;

    // SQLite Error Codes
    private const int SqliteConstraintErrorCode = 19; // SQLITE_CONSTRAINT
    private const int SqliteBusyErrorCode = 5; // SQLITE_BUSY
    private const int SqliteLockedErrorCode = 6; // SQLITE_LOCKED

    /// <summary>
    /// Determines if an exception represents a duplicate key violation.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is a duplicate key error</returns>
    public static bool IsDuplicateKeyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MySqlException mySqlEx => mySqlEx.ErrorCode == MySqlErrorCode.DuplicateKeyEntry,
            SqlException sqlEx => sqlEx.Number == SqlServerDuplicateKeyErrorCode || sqlEx.Number == SqlServerUniqueConstraintErrorCode,
            SqliteException sqliteEx => sqliteEx.SqliteErrorCode == SqliteConstraintErrorCode,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an exception represents a deadlock condition.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is a deadlock error</returns>
    public static bool IsDeadlockException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MySqlException mySqlEx => mySqlEx.Number == MySqlDeadlockErrorCode,
            SqlException sqlEx => sqlEx.Number == SqlServerDeadlockErrorCode,
            SqliteException sqliteEx => sqliteEx.SqliteErrorCode == SqliteBusyErrorCode || sqliteEx.SqliteErrorCode == SqliteLockedErrorCode,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an exception represents a connection failure.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is a connection error</returns>
    public static bool IsConnectionException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MySqlException mySqlEx => mySqlEx.Number == MySqlConnectionErrorCode ||
                                      mySqlEx.Number == MySqlServerGoneErrorCode ||
                                      mySqlEx.Number == MySqlServerLostErrorCode,
            SqlException sqlEx => sqlEx.Number == SqlServerConnectionErrorCode,
            _ => exception is DbException || exception is System.Net.Sockets.SocketException
        };
    }

    /// <summary>
    /// Determines if an exception represents a timeout condition.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is a timeout error</returns>
    public static bool IsTimeoutException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MySqlException mySqlEx => mySqlEx.Number == MySqlLockWaitTimeoutErrorCode,
            SqlException sqlEx => sqlEx.Number == SqlServerTimeoutErrorCode,
            _ => exception is TimeoutException
        };
    }

    /// <summary>
    /// Determines if an exception is a transient error that can be retried.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is transient and can be retried</returns>
    public static bool IsTransientException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return IsDeadlockException(exception) ||
               IsConnectionException(exception) ||
               IsTimeoutException(exception);
    }

    /// <summary>
    /// Wraps a database exception with additional context information.
    /// </summary>
    /// <param name="exception">The original exception</param>
    /// <param name="operation">The operation that was being performed</param>
    /// <param name="dataSource">The data source identifier</param>
    /// <param name="layer">The layer identifier</param>
    /// <returns>A wrapped exception with context</returns>
    public static Exception WrapException(Exception exception, string operation, string? dataSource = null, string? layer = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(operation);

        var message = $"Database operation '{operation}' failed";
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            message += $" for data source '{dataSource}'";
        }
        if (!string.IsNullOrWhiteSpace(layer))
        {
            message += $" on layer '{layer}'";
        }
        message += ".";

        // For now, wrap in a simple InvalidOperationException with the original exception
        // In a full implementation, you would use custom exception types from Honua.Server.Core.Exceptions
        return new InvalidOperationException(message, exception);
    }

    /// <summary>
    /// Gets a user-friendly error message from a database exception.
    /// </summary>
    /// <param name="exception">The exception to get a message from</param>
    /// <returns>A user-friendly error message</returns>
    public static string GetUserFriendlyMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsDuplicateKeyException(exception))
        {
            return "A record with the same key already exists.";
        }

        if (IsDeadlockException(exception))
        {
            return "The operation failed due to a database lock conflict. Please try again.";
        }

        if (IsConnectionException(exception))
        {
            return "Unable to connect to the database. Please check your connection and try again.";
        }

        if (IsTimeoutException(exception))
        {
            return "The database operation took too long to complete. Please try again.";
        }

        return "A database error occurred. Please contact support if this persists.";
    }

    /// <summary>
    /// Extracts the SQL error code from a database exception.
    /// </summary>
    /// <param name="exception">The exception to extract the error code from</param>
    /// <returns>The error code, or null if not available</returns>
    public static int? GetSqlErrorCode(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MySqlException mySqlEx => mySqlEx.Number,
            SqlException sqlEx => sqlEx.Number,
            SqliteException sqliteEx => sqliteEx.SqliteErrorCode,
            _ => null
        };
    }

    /// <summary>
    /// Validates that a connection string is not null or empty.
    /// </summary>
    /// <param name="connectionString">The connection string to validate</param>
    /// <param name="dataSourceId">The data source identifier for error messages</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection string is null or empty</exception>
    public static void ValidateConnectionString(string? connectionString, string dataSourceId)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Data source '{dataSourceId}' is missing a connection string.");
        }
    }

    /// <summary>
    /// Validates that a connection string is configured for health checks.
    /// </summary>
    /// <param name="connectionString">The connection string to validate</param>
    /// <param name="dataSourceId">The data source identifier for error messages</param>
    /// <exception cref="InvalidOperationException">Thrown if the connection string is null or empty</exception>
    public static void ValidateConnectionStringForHealthCheck(string? connectionString, string dataSourceId)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Data source '{dataSourceId}' has no connection string configured.");
        }
    }
}
