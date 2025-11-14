// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Data.ConnectionTesting;

/// <summary>
/// Interface for testing connections to different data source providers.
/// Implementations test actual database connectivity and return diagnostic information.
/// </summary>
public interface IConnectionTester
{
    /// <summary>
    /// The provider type this tester supports (e.g., "postgis", "mysql", "sqlserver").
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Tests connection to a data source and returns diagnostic information.
    /// </summary>
    /// <param name="dataSource">The data source to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection test result with success status and diagnostic information.</returns>
    Task<ConnectionTestResult> TestConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a connection test with diagnostic information.
/// </summary>
public sealed record ConnectionTestResult
{
    /// <summary>
    /// Whether the connection test was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// User-friendly message describing the test result.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Detailed error information if the test failed.
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// Time taken to complete the connection test.
    /// </summary>
    public TimeSpan ResponseTime { get; init; }

    /// <summary>
    /// Additional metadata about the connection (version, host, database, etc.).
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }

    /// <summary>
    /// Type of error that occurred (e.g., "authentication", "network", "timeout", "configuration").
    /// </summary>
    public string? ErrorType { get; init; }
}
