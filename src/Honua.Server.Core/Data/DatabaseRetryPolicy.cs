// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Polly;
using Polly.Retry;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Provides retry policies for database operations to handle transient failures.
/// </summary>
public static class DatabaseRetryPolicy
{
    private static readonly Meter Meter = new("Honua.Server.Database.Retry", "1.0.0");
    private static readonly Counter<long> RetryAttemptCounter = Meter.CreateCounter<long>("database_retry_attempts_total", "retries", "Total number of database retry attempts");
    private static readonly Counter<long> RetrySuccessCounter = Meter.CreateCounter<long>("database_retry_success_total", "successes", "Total number of successful retries");
    private static readonly Counter<long> RetryExhaustedCounter = Meter.CreateCounter<long>("database_retry_exhausted_total", "exhausted", "Total number of exhausted retries");

    /// <summary>
    /// Creates a retry pipeline for PostgreSQL operations.
    /// Retries on transient exceptions with exponential backoff.
    /// </summary>
    public static ResiliencePipeline CreatePostgresRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<NpgsqlException>(IsTransientPostgresException)
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDbException),
                OnRetry = args =>
                {
                    RecordRetryAttempt("postgres", args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for SQLite operations.
    /// Retries on transient exceptions with exponential backoff.
    /// </summary>
    public static ResiliencePipeline CreateSqliteRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqliteException>(IsTransientSqliteException)
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDbException),
                OnRetry = args =>
                {
                    RecordRetryAttempt("sqlite", args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for MySQL operations.
    /// Retries on transient exceptions with exponential backoff.
    /// </summary>
    public static ResiliencePipeline CreateMySqlRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<MySqlException>(IsTransientMySqlException)
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDbException),
                OnRetry = args =>
                {
                    RecordRetryAttempt("mysql", args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for SQL Server operations.
    /// Retries on transient exceptions with exponential backoff.
    /// </summary>
    public static ResiliencePipeline CreateSqlServerRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(IsTransientSqlServerException)
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDbException),
                OnRetry = args =>
                {
                    RecordRetryAttempt("sqlserver", args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for Oracle operations.
    /// Retries on transient exceptions with exponential backoff.
    /// </summary>
    public static ResiliencePipeline CreateOracleRetryPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<DbException>(ex => IsTransientOracleException(ex))
                    .Handle<TimeoutException>()
                    .Handle<DbException>(IsTransientDbException),
                OnRetry = args =>
                {
                    RecordRetryAttempt("oracle", args.Outcome.Exception?.GetType().Name ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private static void RecordRetryAttempt(string database, string exceptionType)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("database", database),
            new KeyValuePair<string, object?>("exception_type", exceptionType)
        };
        RetryAttemptCounter.Add(1, tags);
    }

    /// <summary>
    /// Determines if a PostgreSQL exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientPostgresException(NpgsqlException ex)
    {
        // Retry on transient errors but not on permanent errors
        // See: https://www.npgsql.org/doc/types/basic.html#error-handling

        if (ex.IsTransient)
        {
            return true;
        }

        // Check specific error codes that are transient
        // PostgreSQL error codes: https://www.postgresql.org/docs/current/errcodes-appendix.html
        return ex.SqlState switch
        {
            // Connection exceptions
            "08000" => true, // connection_exception
            "08003" => true, // connection_does_not_exist
            "08006" => true, // connection_failure
            "08001" => true, // sqlclient_unable_to_establish_sqlconnection
            "08004" => true, // sqlserver_rejected_establishment_of_sqlconnection
            "08007" => true, // transaction_resolution_unknown

            // System errors
            "53000" => true, // insufficient_resources
            "53100" => true, // disk_full
            "53200" => true, // out_of_memory
            "53300" => true, // too_many_connections
            "53400" => true, // configuration_limit_exceeded

            // Lock errors
            "40001" => true, // serialization_failure
            "40P01" => true, // deadlock_detected

            // Operator intervention
            "57P01" => true, // admin_shutdown
            "57P02" => true, // crash_shutdown
            "57P03" => true, // cannot_connect_now

            // Do NOT retry on these:
            // "23xxx" - integrity constraint violations
            // "42xxx" - syntax errors
            // "22xxx" - data exceptions
            _ => false
        };
    }

    /// <summary>
    /// Determines if a SQLite exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientSqliteException(SqliteException ex)
    {
        // SQLite error codes: https://www.sqlite.org/rescode.html
        return ex.SqliteErrorCode switch
        {
            // Transient errors
            5 => true,  // SQLITE_BUSY - database is locked
            6 => true,  // SQLITE_LOCKED - database table is locked
            7 => true,  // SQLITE_NOMEM - out of memory
            10 => true, // SQLITE_IOERR - disk I/O error
            13 => true, // SQLITE_FULL - database or disk is full
            14 => true, // SQLITE_CANTOPEN - unable to open database file
            23 => true, // SQLITE_AUTH - authorization denied

            // Do NOT retry on these:
            // 1 - SQLITE_ERROR - generic error
            // 19 - SQLITE_CONSTRAINT - constraint violation
            // 20 - SQLITE_MISMATCH - data type mismatch
            _ => false
        };
    }

    /// <summary>
    /// Determines if a MySQL exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientMySqlException(MySqlException ex)
    {
        // MySQL error codes: https://dev.mysql.com/doc/mysql-errors/8.0/en/server-error-reference.html
        return ex.Number switch
        {
            // Transient errors
            1205 => true, // ER_LOCK_WAIT_TIMEOUT - lock wait timeout exceeded
            1213 => true, // ER_LOCK_DEADLOCK - deadlock found when trying to get lock
            1020 => true, // ER_CHECKREAD - record has changed since last read
            1027 => true, // ER_FILE_USED - file is being used by another thread
            1040 => true, // ER_CON_COUNT_ERROR - too many connections
            1053 => true, // ER_SERVER_SHUTDOWN - server shutdown in progress
            1158 => true, // ER_NET_READ_ERROR - got an error reading communication packets
            1159 => true, // ER_NET_READ_INTERRUPTED - got timeout reading communication packets
            1160 => true, // ER_NET_ERROR_ON_WRITE - got an error writing communication packets
            1161 => true, // ER_NET_WRITE_INTERRUPTED - got timeout writing communication packets
            2002 => true, // CR_CONNECTION_ERROR - can't connect to MySQL server
            2003 => true, // CR_CONN_HOST_ERROR - can't connect to MySQL server on host
            2006 => true, // CR_SERVER_GONE_ERROR - MySQL server has gone away
            2013 => true, // CR_SERVER_LOST - lost connection to MySQL server during query

            // Do NOT retry on these:
            // 1062 - ER_DUP_ENTRY - duplicate entry for key
            // 1064 - ER_PARSE_ERROR - SQL syntax error
            // 1146 - ER_NO_SUCH_TABLE - table doesn't exist
            _ => false
        };
    }

    /// <summary>
    /// Determines if a SQL Server exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientSqlServerException(SqlException ex)
    {
        // SQL Server error codes: https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors
        foreach (SqlError error in ex.Errors)
        {
            switch (error.Number)
            {
                // Transient errors
                case -2:     // Timeout expired
                case -1:     // Connection broken
                case 2:      // Network name not found
                case 53:     // Could not open a connection
                case 64:     // Error in establishing server connection
                case 233:    // Connection initialization error
                case 1205:   // Transaction was deadlocked
                case 1222:   // Lock request timeout exceeded
                case 3935:   // A FILESTREAM transaction context could not be initialized
                case 4060:   // Cannot open database
                case 4221:   // Login timeout expired
                case 8645:   // Timeout waiting for memory resource
                case 8651:   // Could not perform operation because requested memory is not available
                case 10053:  // Transport-level error
                case 10054:  // Transport-level error on send
                case 10060:  // Network or instance-specific error
                case 10061:  // Network or instance-specific error
                case 10928:  // Resource limit reached
                case 10929:  // Resource limit reached
                case 40197:  // Service encountered error processing request
                case 40501:  // Service is busy
                case 40613:  // Database unavailable
                case 49918:  // Cannot process request - not enough resources
                case 49919:  // Cannot process create or update request - too many operations
                case 49920:  // Cannot process request - too many operations
                    return true;

                // Do NOT retry on these:
                // 2627 - PRIMARY KEY constraint violation
                // 207 - Invalid column name
                // 208 - Invalid object name
                default:
                    continue;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if an Oracle exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientOracleException(DbException ex)
    {
        // Oracle uses Oracle.ManagedDataAccess.Client.OracleException
        // We can check error codes through the message or reflection
        // Oracle error codes: https://docs.oracle.com/en/database/oracle/oracle-database/19/errmg/

        var message = ex.Message;

        // Check for ORA- error codes in the message
        if (message.Contains("ORA-00060", StringComparison.OrdinalIgnoreCase)) return true; // Deadlock detected
        if (message.Contains("ORA-00054", StringComparison.OrdinalIgnoreCase)) return true; // Resource busy
        if (message.Contains("ORA-01012", StringComparison.OrdinalIgnoreCase)) return true; // Not logged on
        if (message.Contains("ORA-01033", StringComparison.OrdinalIgnoreCase)) return true; // Oracle initialization or shutdown in progress
        if (message.Contains("ORA-01034", StringComparison.OrdinalIgnoreCase)) return true; // Oracle not available
        if (message.Contains("ORA-01089", StringComparison.OrdinalIgnoreCase)) return true; // Immediate shutdown in progress
        if (message.Contains("ORA-01090", StringComparison.OrdinalIgnoreCase)) return true; // Shutdown in progress
        if (message.Contains("ORA-01092", StringComparison.OrdinalIgnoreCase)) return true; // Oracle instance terminated
        if (message.Contains("ORA-01555", StringComparison.OrdinalIgnoreCase)) return true; // Snapshot too old
        if (message.Contains("ORA-03113", StringComparison.OrdinalIgnoreCase)) return true; // End-of-file on communication channel
        if (message.Contains("ORA-03114", StringComparison.OrdinalIgnoreCase)) return true; // Not connected to Oracle
        if (message.Contains("ORA-03135", StringComparison.OrdinalIgnoreCase)) return true; // Connection lost contact
        if (message.Contains("ORA-12170", StringComparison.OrdinalIgnoreCase)) return true; // TNS: connect timeout occurred
        if (message.Contains("ORA-12224", StringComparison.OrdinalIgnoreCase)) return true; // TNS: no listener
        if (message.Contains("ORA-12500", StringComparison.OrdinalIgnoreCase)) return true; // TNS: listener failed to start
        if (message.Contains("ORA-12537", StringComparison.OrdinalIgnoreCase)) return true; // TNS: connection closed
        if (message.Contains("ORA-12541", StringComparison.OrdinalIgnoreCase)) return true; // TNS: no listener
        if (message.Contains("ORA-12543", StringComparison.OrdinalIgnoreCase)) return true; // TNS: destination host unreachable
        if (message.Contains("ORA-12571", StringComparison.OrdinalIgnoreCase)) return true; // TNS: packet writer failure
        if (message.Contains("ORA-25408", StringComparison.OrdinalIgnoreCase)) return true; // Cannot safely replay call

        // Do NOT retry on these:
        // ORA-00001 - unique constraint violated
        // ORA-00904 - invalid identifier
        // ORA-00942 - table or view does not exist

        return false;
    }

    /// <summary>
    /// Determines if a generic DbException is transient and should be retried.
    /// </summary>
    private static bool IsTransientDbException(DbException ex)
    {
        // Handle generic timeout scenarios
        return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }
}
