// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data;

/// <summary>
/// Factory interface for creating database provider instances based on provider name.
/// Supports pluggable multi-database architecture allowing runtime selection of data stores.
/// </summary>
/// <remarks>
/// Honua Server supports multiple database providers:
/// - PostgreSQL (with PostGIS extension for spatial data)
/// - MySQL (with spatial extensions)
/// - SQLite (with SpatiaLite extension)
/// - SQL Server (with spatial types)
/// - DuckDB (for analytics and OLAP workloads)
/// - BigQuery, Snowflake, and other cloud data warehouses
///
/// The factory pattern enables dependency injection and makes it easy to add new
/// database providers without modifying existing code.
/// </remarks>
public interface IDataStoreProviderFactory
{
    /// <summary>
    /// Creates a database provider instance for the specified provider name.
    /// </summary>
    /// <param name="providerName">
    /// The name of the database provider (case-insensitive).
    /// Examples: "postgres", "postgresql", "mysql", "sqlite", "sqlserver", "duckdb"
    /// </param>
    /// <returns>An <see cref="IDataStoreProvider"/> instance configured for the specified database.</returns>
    /// <exception cref="System.ArgumentException">Thrown when providerName is null, empty, or not recognized.</exception>
    /// <exception cref="System.NotSupportedException">Thrown when the requested provider is not installed or configured.</exception>
    IDataStoreProvider Create(string providerName);
}
