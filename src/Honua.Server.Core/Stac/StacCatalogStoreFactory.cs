// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Data.SqlServer;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Stac;

public sealed class StacCatalogStoreFactory : ProviderFactoryBase<IStacCatalogStore>
{
    public StacCatalogStoreFactory()
    {
        // Register in-memory provider as singleton
        RegisterProviderInstance("memory", new InMemoryStacCatalogStore(), "inmemory");
    }

    public IStacCatalogStore Create(StacCatalogConfiguration? configuration)
    {
        configuration ??= StacCatalogConfiguration.Default;

        if (!configuration.Enabled)
        {
            return new InMemoryStacCatalogStore();
        }

        var provider = NormalizeProvider(configuration.Provider);
        var connectionString = configuration.ConnectionString;

        return provider switch
        {
            SqliteDataStoreProvider.ProviderKey =>
                new SqliteStacCatalogStore(RequireSqliteConnectionString(connectionString, configuration.FilePath)),
            PostgresDataStoreProvider.ProviderKey =>
                new PostgresStacCatalogStore(RequireConnectionString(provider, connectionString)),
            SqlServerDataStoreProvider.ProviderKey =>
                new SqlServerStacCatalogStore(RequireConnectionString(provider, connectionString)),
            MySqlDataStoreProvider.ProviderKey =>
                new MySqlStacCatalogStore(RequireConnectionString(provider, connectionString)),
            "memory" or "inmemory" =>
                new InMemoryStacCatalogStore(),
            _ => throw new NotSupportedException($"STAC catalog provider '{provider}' is not supported. Supported providers: {string.Join(", ", GetSupportedProviders())}")
        };
    }

    private string NormalizeProvider(string? provider)
    {
        var normalized = provider?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = StacCatalogConfiguration.Default.Provider;
        }

        return normalized switch
        {
            SqliteDataStoreProvider.ProviderKey or "sqlite" => SqliteDataStoreProvider.ProviderKey,
            PostgresDataStoreProvider.ProviderKey or "postgres" or "postgresql" => PostgresDataStoreProvider.ProviderKey,
            SqlServerDataStoreProvider.ProviderKey or "sqlserver" or "mssql" => SqlServerDataStoreProvider.ProviderKey,
            MySqlDataStoreProvider.ProviderKey or "mysql" => MySqlDataStoreProvider.ProviderKey,
            _ => normalized
        };
    }

    private static string[] GetSupportedProviders()
    {
        return new[]
        {
            SqliteDataStoreProvider.ProviderKey,
            PostgresDataStoreProvider.ProviderKey,
            SqlServerDataStoreProvider.ProviderKey,
            MySqlDataStoreProvider.ProviderKey,
            "memory"
        };
    }

    private static string RequireConnectionString(string provider, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidDataException($"STAC catalog provider '{provider}' requires a connection string.");
        }

        return connectionString;
    }

    private static string RequireSqliteConnectionString(string? connectionString, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidDataException("STAC catalog sqlite provider requires a connection string.");
        }

        EnsureDirectoryForSqlite(filePath);
        return connectionString;
    }

    private static void EnsureDirectoryForSqlite(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }
}
