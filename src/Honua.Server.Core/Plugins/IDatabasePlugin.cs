// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Core.Plugins;

/// <summary>
/// Plugin interface for database providers.
/// Enables extensible database support through the plugin system.
/// </summary>
public interface IDatabasePlugin : IHonuaPlugin
{
    /// <summary>
    /// The unique provider key for this database provider (e.g., "postgresql", "mongodb", "bigquery").
    /// Must match the provider key used in Configuration V2 data_source blocks.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// The type of database provider.
    /// </summary>
    DatabaseProviderType ProviderType { get; }

    /// <summary>
    /// Display name for the database provider (e.g., "PostgreSQL", "MongoDB", "Google BigQuery").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the capabilities supported by this database provider.
    /// </summary>
    IDataStoreCapabilities Capabilities { get; }

    /// <summary>
    /// Registers the database provider with the dependency injection container.
    /// The provider should be registered as a keyed singleton using the ProviderKey.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="context">The plugin context.</param>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context);

    /// <summary>
    /// Validates that the plugin's configuration is correct and the database provider is available.
    /// Should check for required NuGet packages, connection string format, etc.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    PluginValidationResult ValidateConfiguration(IConfiguration configuration);

    /// <summary>
    /// Creates an instance of the database provider.
    /// This is called by the DataStoreProviderFactory when a provider is requested.
    /// </summary>
    /// <returns>A new instance of the IDataStoreProvider.</returns>
    IDataStoreProvider CreateProvider();
}

/// <summary>
/// Database provider types for categorization and filtering.
/// </summary>
public enum DatabaseProviderType
{
    /// <summary>Relational SQL databases (PostgreSQL, MySQL, SQL Server, Oracle, etc.)</summary>
    Relational,

    /// <summary>NoSQL document databases (MongoDB, CosmosDB, CouchDB, etc.)</summary>
    Document,

    /// <summary>NoSQL key-value stores (Redis, DynamoDB, etc.)</summary>
    KeyValue,

    /// <summary>NoSQL column-family stores (Cassandra, HBase, etc.)</summary>
    ColumnFamily,

    /// <summary>Search and analytics engines (Elasticsearch, Solr, etc.)</summary>
    SearchEngine,

    /// <summary>Time-series databases (InfluxDB, TimescaleDB, etc.)</summary>
    TimeSeries,

    /// <summary>Graph databases (Neo4j, ArangoDB, etc.)</summary>
    Graph,

    /// <summary>Cloud data warehouses (BigQuery, Redshift, Snowflake, etc.)</summary>
    DataWarehouse,

    /// <summary>In-memory databases (Redis, Memcached, etc.)</summary>
    InMemory,

    /// <summary>Other/specialized databases</summary>
    Other
}
