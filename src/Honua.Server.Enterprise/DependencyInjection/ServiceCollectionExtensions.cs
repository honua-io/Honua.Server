// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Data;
using Honua.Server.Enterprise.Data.BigQuery;
using Honua.Server.Enterprise.Data.CosmosDb;
using Honua.Server.Enterprise.Data.Elasticsearch;
using Honua.Server.Enterprise.Data.MongoDB;
using Honua.Server.Enterprise.Data.Oracle;
using Honua.Server.Enterprise.Data.Redshift;
using Honua.Server.Enterprise.Data.Snowflake;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Enterprise.DependencyInjection;

/// <summary>
/// Extension methods for registering enterprise data providers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all enterprise data store providers (BigQuery, Cosmos DB, MongoDB, Oracle, Redshift, Snowflake)
    /// </summary>
    public static IServiceCollection AddHonuaEnterprise(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // BigQuery - Google Cloud analytics database
        services.AddKeyedSingleton<IDataStoreProvider>(
            BigQueryDataStoreProvider.ProviderKey,
            (_, _) => new BigQueryDataStoreProvider());

        // Cosmos DB - Azure NoSQL database
        services.AddKeyedSingleton<IDataStoreProvider>(
            CosmosDbDataStoreProvider.ProviderKey,
            (_, _) => new CosmosDbDataStoreProvider());

        // MongoDB - Document database
        services.AddKeyedSingleton<IDataStoreProvider>(
            MongoDbDataStoreProvider.ProviderKey,
            (_, _) => new MongoDbDataStoreProvider());

        // Elasticsearch - Geo-enabled search engine
        services.AddKeyedSingleton<IDataStoreProvider>(
            ElasticsearchDataStoreProvider.ProviderKey,
            (_, _) => new ElasticsearchDataStoreProvider());

        // Oracle Spatial - Oracle database with SDO_GEOMETRY
        services.AddKeyedSingleton<IDataStoreProvider>(
            OracleDataStoreProvider.ProviderKey,
            (_, _) => new OracleDataStoreProvider());

        // Redshift - AWS analytics using Redshift Data API
        services.AddKeyedSingleton<IDataStoreProvider>(
            RedshiftDataStoreProvider.ProviderKey,
            (_, _) => new RedshiftDataStoreProvider());

        // Snowflake - Cloud data warehouse
        services.AddKeyedSingleton<IDataStoreProvider>(
            SnowflakeDataStoreProvider.ProviderKey,
            (_, _) => new SnowflakeDataStoreProvider());

        return services;
    }
}
