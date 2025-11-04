// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.Auth;

public static class AuthRepositoryFactory
{
    public static IAuthRepository CreateRepository(
        string basePath,
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics = null,
        IOptions<DataAccessOptions>? dataAccessOptions = null,
        IOptions<ConnectionStringOptions>? connectionStrings = null)
    {
        Guard.NotNull(authOptions);
        Guard.NotNull(loggerFactory);

        var options = authOptions.CurrentValue
            ?? throw new InvalidOperationException("Authentication options have not been configured.");

        var provider = (options.Local.Provider ?? "sqlite").Trim().ToLowerInvariant();

        return provider switch
        {
            "sqlite" => CreateSqliteRepositoryInternal(basePath, authOptions, loggerFactory, metrics, dataAccessOptions),
            "postgres" or "postgresql" => CreatePostgresRepository(authOptions, loggerFactory, metrics, options, connectionStrings),
            "mysql" => CreateMySqlRepository(authOptions, loggerFactory, metrics, options, connectionStrings),
            "sqlserver" => CreateSqlServerRepository(authOptions, loggerFactory, metrics, options, connectionStrings),
            _ => throw new NotSupportedException($"Unsupported local authentication provider '{options.Local.Provider}'.")
        };
    }

    public static IAuthRepository CreateSqliteRepository(
        string basePath,
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics = null,
        IOptions<DataAccessOptions>? dataAccessOptions = null)
    {
        return CreateSqliteRepositoryInternal(basePath, authOptions, loggerFactory, metrics, dataAccessOptions);
    }

    private static IAuthRepository CreateSqliteRepositoryInternal(
        string basePath,
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics,
        IOptions<DataAccessOptions>? dataAccessOptions)
    {
        Guard.NotNullOrEmpty(basePath);
        Guard.NotNull(authOptions);
        Guard.NotNull(loggerFactory);

        return new SqliteAuthRepository(
            basePath,
            authOptions,
            loggerFactory.CreateLogger<SqliteAuthRepository>(),
            metrics,
            dataAccessOptions);
    }

    private static IAuthRepository CreatePostgresRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics,
        HonuaAuthenticationOptions options,
        IOptions<ConnectionStringOptions>? connectionStrings)
    {
        var connectionString = ResolveConnectionString(options.Local, "postgres", connectionStrings);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string is required for the Postgres authentication provider. " +
                "Set honua:authentication:local:connectionString, honua:authentication:local:connectionStringName, " +
                "or a matching entry under ConnectionStrings.");
        }

        return new PostgresAuthRepository(
            authOptions,
            loggerFactory.CreateLogger<PostgresAuthRepository>(),
            metrics,
            connectionString,
            options.Local.Schema);
    }

    private static IAuthRepository CreateMySqlRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics,
        HonuaAuthenticationOptions options,
        IOptions<ConnectionStringOptions>? connectionStrings)
    {
        var connectionString = ResolveConnectionString(options.Local, "mysql", connectionStrings);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string is required for the MySQL authentication provider. " +
                "Set honua:authentication:local:connectionString, honua:authentication:local:connectionStringName, " +
                "or a matching entry under ConnectionStrings.");
        }

        return new MySqlAuthRepository(
            authOptions,
            loggerFactory.CreateLogger<MySqlAuthRepository>(),
            metrics,
            connectionString);
    }

    private static IAuthRepository CreateSqlServerRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILoggerFactory loggerFactory,
        AuthMetrics? metrics,
        HonuaAuthenticationOptions options,
        IOptions<ConnectionStringOptions>? connectionStrings)
    {
        var connectionString = ResolveConnectionString(options.Local, "sqlserver", connectionStrings);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string is required for the SQL Server authentication provider. " +
                "Set honua:authentication:local:connectionString, honua:authentication:local:connectionStringName, " +
                "or a matching entry under ConnectionStrings.");
        }

        return new SqlServerAuthRepository(
            authOptions,
            loggerFactory.CreateLogger<SqlServerAuthRepository>(),
            metrics,
            connectionString,
            options.Local.Schema);
    }

    private static string? ResolveConnectionString(
        HonuaAuthenticationOptions.LocalOptions localOptions,
        string provider,
        IOptions<ConnectionStringOptions>? connectionStrings)
    {
        if (!string.IsNullOrWhiteSpace(localOptions.ConnectionString))
        {
            return localOptions.ConnectionString;
        }

        if (!string.IsNullOrWhiteSpace(localOptions.ConnectionStringName) && connectionStrings?.Value is { } named)
        {
            var normalized = localOptions.ConnectionStringName.Trim();

            if (normalized.Equals(nameof(ConnectionStringOptions.DefaultConnection), StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("DefaultConnection", StringComparison.OrdinalIgnoreCase))
            {
                return named.DefaultConnection;
            }

            if (normalized.Equals(nameof(ConnectionStringOptions.Postgres), StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                return named.Postgres;
            }

            if (normalized.Equals(nameof(ConnectionStringOptions.MySql), StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            {
                return named.MySql;
            }

            if (normalized.Equals(nameof(ConnectionStringOptions.SqlServer), StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("SQLServer", StringComparison.OrdinalIgnoreCase))
            {
                return named.SqlServer;
            }
        }

        if (connectionStrings?.Value is { } fallback)
        {
            return provider switch
            {
                "postgres" or "postgresql" => fallback.Postgres ?? fallback.DefaultConnection,
                "mysql" => fallback.MySql ?? fallback.DefaultConnection,
                "sqlserver" => fallback.SqlServer ?? fallback.DefaultConnection,
                _ => fallback.DefaultConnection
            };
        }

        return null;
    }
}
