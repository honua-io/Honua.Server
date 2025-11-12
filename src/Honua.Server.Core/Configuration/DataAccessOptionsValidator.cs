// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates data access configuration options.
/// </summary>
public sealed class DataAccessOptionsValidator : IValidateOptions<DataAccessOptions>
{
    public ValidateOptionsResult Validate(string? name, DataAccessOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("DataAccessOptions cannot be null");
        }

        var failures = new List<string>();

        // Validate command timeouts
        if (options.DefaultCommandTimeoutSeconds <= 0)
        {
            failures.Add($"DataAccess DefaultCommandTimeoutSeconds must be positive. Current: {options.DefaultCommandTimeoutSeconds}. Set 'DataAccess:DefaultCommandTimeoutSeconds' to a value greater than 0. Example: 30");
        }
        else if (options.DefaultCommandTimeoutSeconds > 300)
        {
            failures.Add($"DataAccess DefaultCommandTimeoutSeconds ({options.DefaultCommandTimeoutSeconds}) exceeds recommended maximum of 300 seconds (5 minutes). Long timeouts can mask performance issues. Reduce 'DataAccess:DefaultCommandTimeoutSeconds'.");
        }

        if (options.LongRunningQueryTimeoutSeconds <= 0)
        {
            failures.Add($"DataAccess LongRunningQueryTimeoutSeconds must be positive. Current: {options.LongRunningQueryTimeoutSeconds}. Set 'DataAccess:LongRunningQueryTimeoutSeconds' to a value greater than 0.");
        }
        else if (options.LongRunningQueryTimeoutSeconds < options.DefaultCommandTimeoutSeconds)
        {
            failures.Add($"DataAccess LongRunningQueryTimeoutSeconds ({options.LongRunningQueryTimeoutSeconds}) should be greater than DefaultCommandTimeoutSeconds ({options.DefaultCommandTimeoutSeconds}). Adjust 'DataAccess:LongRunningQueryTimeoutSeconds'.");
        }
        else if (options.LongRunningQueryTimeoutSeconds > 3600)
        {
            failures.Add($"DataAccess LongRunningQueryTimeoutSeconds ({options.LongRunningQueryTimeoutSeconds}) exceeds recommended maximum of 3600 seconds (1 hour). Consider query optimization instead. Reduce 'DataAccess:LongRunningQueryTimeoutSeconds'.");
        }

        if (options.BulkOperationTimeoutSeconds <= 0)
        {
            failures.Add($"DataAccess BulkOperationTimeoutSeconds must be positive. Current: {options.BulkOperationTimeoutSeconds}. Set 'DataAccess:BulkOperationTimeoutSeconds' to a value greater than 0.");
        }
        else if (options.BulkOperationTimeoutSeconds < options.LongRunningQueryTimeoutSeconds)
        {
            failures.Add($"DataAccess BulkOperationTimeoutSeconds ({options.BulkOperationTimeoutSeconds}) should be greater than LongRunningQueryTimeoutSeconds ({options.LongRunningQueryTimeoutSeconds}). Adjust 'DataAccess:BulkOperationTimeoutSeconds'.");
        }

        if (options.TransactionTimeoutSeconds <= 0)
        {
            failures.Add($"DataAccess TransactionTimeoutSeconds must be positive. Current: {options.TransactionTimeoutSeconds}. Set 'DataAccess:TransactionTimeoutSeconds' to a value greater than 0.");
        }

        if (options.HealthCheckTimeoutSeconds <= 0)
        {
            failures.Add($"DataAccess HealthCheckTimeoutSeconds must be positive. Current: {options.HealthCheckTimeoutSeconds}. Set 'DataAccess:HealthCheckTimeoutSeconds' to a value greater than 0.");
        }
        else if (options.HealthCheckTimeoutSeconds > 30)
        {
            failures.Add($"DataAccess HealthCheckTimeoutSeconds ({options.HealthCheckTimeoutSeconds}) exceeds recommended maximum of 30 seconds. Health checks should be fast. Reduce 'DataAccess:HealthCheckTimeoutSeconds'.");
        }

        // Validate SQL Server pool options
        ValidateSqlServerPoolOptions(options.SqlServer, failures);

        // Validate PostgreSQL pool options
        ValidatePostgresPoolOptions(options.Postgres, failures);

        // Validate MySQL pool options
        ValidateMySqlPoolOptions(options.MySql, failures);

        // Validate SQLite options
        ValidateSqliteOptions(options.Sqlite, failures);

        // Validate optimistic locking options
        ValidateOptimisticLockingOptions(options.OptimisticLocking, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateSqlServerPoolOptions(SqlServerPoolOptions options, List<string> failures)
    {
        if (options.MinPoolSize < 0)
        {
            failures.Add($"DataAccess SqlServer MinPoolSize cannot be negative. Current: {options.MinPoolSize}. Set 'DataAccess:SqlServer:MinPoolSize' to 0 or positive value.");
        }

        if (options.MaxPoolSize <= 0)
        {
            failures.Add($"DataAccess SqlServer MaxPoolSize must be positive. Current: {options.MaxPoolSize}. Set 'DataAccess:SqlServer:MaxPoolSize' to a value greater than 0.");
        }
        else if (options.MaxPoolSize < options.MinPoolSize)
        {
            failures.Add($"DataAccess SqlServer MaxPoolSize ({options.MaxPoolSize}) must be greater than or equal to MinPoolSize ({options.MinPoolSize}). Adjust 'DataAccess:SqlServer:MaxPoolSize'.");
        }

        if (options.ConnectionLifetime <= 0)
        {
            failures.Add($"DataAccess SqlServer ConnectionLifetime must be positive. Current: {options.ConnectionLifetime}. Set 'DataAccess:SqlServer:ConnectionLifetime' to a value greater than 0.");
        }

        if (options.ConnectTimeout <= 0)
        {
            failures.Add($"DataAccess SqlServer ConnectTimeout must be positive. Current: {options.ConnectTimeout}. Set 'DataAccess:SqlServer:ConnectTimeout' to a value greater than 0.");
        }
    }

    private static void ValidatePostgresPoolOptions(PostgresPoolOptions options, List<string> failures)
    {
        if (options.MinPoolSize < 0)
        {
            failures.Add($"DataAccess Postgres MinPoolSize cannot be negative. Current: {options.MinPoolSize}. Set 'DataAccess:Postgres:MinPoolSize' to 0 or positive value.");
        }

        if (options.MaxPoolSize <= 0)
        {
            failures.Add($"DataAccess Postgres MaxPoolSize must be positive. Current: {options.MaxPoolSize}. Set 'DataAccess:Postgres:MaxPoolSize' to a value greater than 0.");
        }
        else if (options.MaxPoolSize < options.MinPoolSize)
        {
            failures.Add($"DataAccess Postgres MaxPoolSize ({options.MaxPoolSize}) must be greater than or equal to MinPoolSize ({options.MinPoolSize}). Adjust 'DataAccess:Postgres:MaxPoolSize'.");
        }

        if (options.ConnectionLifetime <= 0)
        {
            failures.Add($"DataAccess Postgres ConnectionLifetime must be positive. Current: {options.ConnectionLifetime}. Set 'DataAccess:Postgres:ConnectionLifetime' to a value greater than 0.");
        }

        if (options.Timeout <= 0)
        {
            failures.Add($"DataAccess Postgres Timeout must be positive. Current: {options.Timeout}. Set 'DataAccess:Postgres:Timeout' to a value greater than 0.");
        }
    }

    private static void ValidateMySqlPoolOptions(MySqlPoolOptions options, List<string> failures)
    {
        if (options.MinimumPoolSize < 0)
        {
            failures.Add($"DataAccess MySql MinimumPoolSize cannot be negative. Current: {options.MinimumPoolSize}. Set 'DataAccess:MySql:MinimumPoolSize' to 0 or positive value.");
        }

        if (options.MaximumPoolSize <= 0)
        {
            failures.Add($"DataAccess MySql MaximumPoolSize must be positive. Current: {options.MaximumPoolSize}. Set 'DataAccess:MySql:MaximumPoolSize' to a value greater than 0.");
        }
        else if (options.MaximumPoolSize < options.MinimumPoolSize)
        {
            failures.Add($"DataAccess MySql MaximumPoolSize ({options.MaximumPoolSize}) must be greater than or equal to MinimumPoolSize ({options.MinimumPoolSize}). Adjust 'DataAccess:MySql:MaximumPoolSize'.");
        }

        if (options.ConnectionLifeTime <= 0)
        {
            failures.Add($"DataAccess MySql ConnectionLifeTime must be positive. Current: {options.ConnectionLifeTime}. Set 'DataAccess:MySql:ConnectionLifeTime' to a value greater than 0.");
        }

        if (options.ConnectionTimeout <= 0)
        {
            failures.Add($"DataAccess MySql ConnectionTimeout must be positive. Current: {options.ConnectionTimeout}. Set 'DataAccess:MySql:ConnectionTimeout' to a value greater than 0.");
        }
    }

    private static void ValidateSqliteOptions(SqlitePoolOptions options, List<string> failures)
    {
        if (options.DefaultTimeout <= 0)
        {
            failures.Add($"DataAccess Sqlite DefaultTimeout must be positive. Current: {options.DefaultTimeout}. Set 'DataAccess:Sqlite:DefaultTimeout' to a value greater than 0.");
        }

        var validCacheModes = new[] { "Default", "Private", "Shared" };
        if (!System.Array.Exists(validCacheModes, mode => mode.Equals(options.CacheMode, System.StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add($"DataAccess Sqlite CacheMode '{options.CacheMode}' is invalid. Valid values: {string.Join(", ", validCacheModes)}. Set 'DataAccess:Sqlite:CacheMode'.");
        }
    }

    private static void ValidateOptimisticLockingOptions(OptimisticLockingOptions options, List<string> failures)
    {
        if (options.MaxRetryAttempts < 0)
        {
            failures.Add($"DataAccess OptimisticLocking MaxRetryAttempts cannot be negative. Current: {options.MaxRetryAttempts}. Set 'DataAccess:OptimisticLocking:MaxRetryAttempts' to 0 (no retries) or positive value.");
        }

        if (options.RetryDelayMilliseconds <= 0)
        {
            failures.Add($"DataAccess OptimisticLocking RetryDelayMilliseconds must be positive. Current: {options.RetryDelayMilliseconds}. Set 'DataAccess:OptimisticLocking:RetryDelayMilliseconds' to a value greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(options.VersionColumnName))
        {
            failures.Add("DataAccess OptimisticLocking VersionColumnName is required. Set 'DataAccess:OptimisticLocking:VersionColumnName'. Example: 'row_version'");
        }
    }
}
