// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata.Providers;

/// <summary>
/// Configuration for metadata provider selection and settings.
/// </summary>
public sealed class MetadataProviderConfiguration
{
    /// <summary>
    /// The type of metadata provider to use.
    /// </summary>
    public MetadataProviderType Provider { get; set; } = MetadataProviderType.File;

    /// <summary>
    /// Path to metadata file (for File provider). Default: "./metadata.json"
    /// </summary>
    public string? FilePath { get; set; } = "./metadata.json";

    /// <summary>
    /// Whether to watch for file changes (for File provider). Default: false (disabled in production).
    /// </summary>
    public bool WatchForFileChanges { get; set; }

    /// <summary>
    /// Redis connection string (for Redis provider).
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// SQL Server connection string (for SqlServer provider).
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>
    /// PostgreSQL connection string (for Postgres provider).
    /// </summary>
    public string? PostgresConnectionString { get; set; }

    /// <summary>
    /// Redis-specific options.
    /// </summary>
    public RedisMetadataOptions? Redis { get; set; }

    /// <summary>
    /// SQL Server-specific options.
    /// </summary>
    public SqlServerMetadataOptions? SqlServer { get; set; }

    /// <summary>
    /// PostgreSQL-specific options.
    /// </summary>
    public PostgresMetadataOptions? Postgres { get; set; }
}

/// <summary>
/// Supported metadata provider types.
/// </summary>
public enum MetadataProviderType
{
    /// <summary>
    /// File-based provider (JSON/YAML). Recommended for simple deployments and development.
    /// </summary>
    File,

    /// <summary>
    /// Redis-based provider with pub/sub. RECOMMENDED for production clusters.
    /// Provides real-time synchronization across instances with latency under 100ms.
    /// </summary>
    Redis,

    /// <summary>
    /// SQL Server-based provider. Suitable for enterprise customers using SQL Server.
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL-based provider. For customers preferring PostgreSQL.
    /// </summary>
    Postgres
}

/// <summary>
/// Extension methods for registering metadata providers.
/// </summary>
public static class MetadataProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured metadata provider based on appsettings.json configuration.
    /// </summary>
    /// <example>
    /// appsettings.json:
    /// {
    ///   "MetadataProvider": {
    ///     "Provider": "Redis",
    ///     "RedisConnectionString": "localhost:6379",
    ///     "Redis": {
    ///       "KeyPrefix": "honua:metadata",
    ///       "MaxVersions": 100
    ///     }
    ///   }
    /// }
    /// </example>
    public static IServiceCollection AddMetadataProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var config = new MetadataProviderConfiguration();
        configuration.GetSection("MetadataProvider").Bind(config);

        return AddMetadataProvider(services, config);
    }

    /// <summary>
    /// Registers the metadata provider with explicit configuration.
    /// </summary>
    public static IServiceCollection AddMetadataProvider(
        this IServiceCollection services,
        MetadataProviderConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        switch (configuration.Provider)
        {
            case MetadataProviderType.File:
                return AddFileProvider(services, configuration);

            case MetadataProviderType.Redis:
                return AddRedisProvider(services, configuration);

            case MetadataProviderType.SqlServer:
                return AddSqlServerProvider(services, configuration);

            case MetadataProviderType.Postgres:
                return AddPostgresProvider(services, configuration);

            default:
                throw new ArgumentException($"Unknown metadata provider type: {configuration.Provider}");
        }
    }

    private static IServiceCollection AddFileProvider(
        IServiceCollection services,
        MetadataProviderConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.FilePath))
        {
            throw new InvalidOperationException(
                "FilePath must be specified when using File metadata provider");
        }

        services.AddSingleton<IMetadataProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<JsonMetadataProvider>>();
            logger.LogInformation(
                "Using File metadata provider: {Path} (watch: {Watch})",
                configuration.FilePath, configuration.WatchForFileChanges);

            return new JsonMetadataProvider(configuration.FilePath, configuration.WatchForFileChanges);
        });

        return services;
    }

    private static IServiceCollection AddRedisProvider(
        IServiceCollection services,
        MetadataProviderConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.RedisConnectionString))
        {
            throw new InvalidOperationException(
                "RedisConnectionString must be specified when using Redis metadata provider");
        }

        // Register Redis connection multiplexer if not already registered
        if (!services.Contains(ServiceDescriptor.Singleton<IConnectionMultiplexer>(sp => null!)))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
                logger.LogInformation("Connecting to Redis: {ConnectionString}",
                    MaskConnectionString(configuration.RedisConnectionString));

                return ConnectionMultiplexer.Connect(configuration.RedisConnectionString);
            });
        }

        // Register Redis metadata provider
        services.AddSingleton<IMetadataProvider>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var options = configuration.Redis ?? new RedisMetadataOptions();
            var logger = sp.GetRequiredService<ILogger<RedisMetadataProvider>>();

            logger.LogInformation(
                "Using Redis metadata provider (RECOMMENDED for production clusters)");

            return new RedisMetadataProvider(redis, options, logger);
        });

        // Also expose as IMutableMetadataProvider and IReloadableMetadataProvider
        services.AddSingleton<IMutableMetadataProvider>(sp =>
            (IMutableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());
        services.AddSingleton<IReloadableMetadataProvider>(sp =>
            (IReloadableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());

        return services;
    }

    private static IServiceCollection AddSqlServerProvider(
        IServiceCollection services,
        MetadataProviderConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.SqlServerConnectionString))
        {
            throw new InvalidOperationException(
                "SqlServerConnectionString must be specified when using SqlServer metadata provider");
        }

        services.AddSingleton<IMetadataProvider>(sp =>
        {
            var options = configuration.SqlServer ?? new SqlServerMetadataOptions();
            var logger = sp.GetRequiredService<ILogger<SqlServerMetadataProvider>>();

            logger.LogInformation(
                "Using SQL Server metadata provider (polling: {Polling}s)",
                options.EnablePolling ? options.PollingIntervalSeconds.ToString() : "disabled");

            return new SqlServerMetadataProvider(
                configuration.SqlServerConnectionString,
                options,
                logger);
        });

        // Also expose as IMutableMetadataProvider and IReloadableMetadataProvider
        services.AddSingleton<IMutableMetadataProvider>(sp =>
            (IMutableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());
        services.AddSingleton<IReloadableMetadataProvider>(sp =>
            (IReloadableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());

        return services;
    }

    private static IServiceCollection AddPostgresProvider(
        IServiceCollection services,
        MetadataProviderConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.PostgresConnectionString))
        {
            throw new InvalidOperationException(
                "PostgresConnectionString must be specified when using Postgres metadata provider");
        }

        services.AddSingleton<IMetadataProvider>(sp =>
        {
            var options = configuration.Postgres ?? new PostgresMetadataOptions();
            var logger = sp.GetRequiredService<ILogger<PostgresMetadataProvider>>();

            logger.LogInformation(
                "Using PostgreSQL metadata provider with NOTIFY/LISTEN (notifications: {Notifications})",
                options.EnableNotifications);

            return new PostgresMetadataProvider(
                configuration.PostgresConnectionString,
                options,
                logger);
        });

        // Also expose as IMutableMetadataProvider and IReloadableMetadataProvider
        services.AddSingleton<IMutableMetadataProvider>(sp =>
            (IMutableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());
        services.AddSingleton<IReloadableMetadataProvider>(sp =>
            (IReloadableMetadataProvider)sp.GetRequiredService<IMetadataProvider>());

        return services;
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Simple masking for logging - hide passwords
        var parts = connectionString.Split(',');
        return parts.Length > 0 ? parts[0] : connectionString;
    }
}
