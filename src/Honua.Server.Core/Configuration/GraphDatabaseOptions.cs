// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for Apache AGE graph database integration.
/// </summary>
public sealed class GraphDatabaseOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "GraphDatabase";

    /// <summary>
    /// Gets or sets whether graph database functionality is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the PostgreSQL connection string for graph database operations.
    /// If not specified, uses the main application database connection.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the default graph name for operations.
    /// </summary>
    [Required]
    public string DefaultGraphName { get; set; } = "honua_graph";

    /// <summary>
    /// Gets or sets whether to automatically create the graph on startup if it doesn't exist.
    /// </summary>
    public bool AutoCreateGraph { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable graph database schema initialization on startup.
    /// </summary>
    public bool EnableSchemaInitialization { get; set; } = true;

    /// <summary>
    /// Gets or sets the command timeout in seconds for graph operations.
    /// </summary>
    [Range(1, 3600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to enable query result caching.
    /// </summary>
    public bool EnableQueryCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the query cache expiration time in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int QueryCacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to log Cypher queries for debugging.
    /// </summary>
    public bool LogQueries { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum depth for graph traversal queries to prevent infinite loops.
    /// </summary>
    [Range(1, 100)]
    public int MaxTraversalDepth { get; set; } = 10;
}
