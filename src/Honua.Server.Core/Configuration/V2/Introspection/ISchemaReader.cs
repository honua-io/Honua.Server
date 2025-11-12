// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// Interface for reading database schemas.
/// Implementations exist for PostgreSQL, SQLite, SQL Server, MySQL, etc.
/// </summary>
public interface ISchemaReader
{
    /// <summary>
    /// Gets the provider name (e.g., "postgresql", "sqlite", "sqlserver").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Introspects the database schema.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="options">Introspection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Introspection result containing database schema.</returns>
    Task<IntrospectionResult> IntrospectAsync(
        string connectionString,
        IntrospectionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests if the connection can be established.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
