// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Data;

namespace Honua.Server.Enterprise.Sensors.Data;

/// <summary>
/// Factory for creating database connections for SensorThings API operations.
/// This pattern ensures proper resource management and connection lifecycle control.
/// </summary>
public interface ISensorThingsDbConnectionFactory
{
    /// <summary>
    /// Creates a new database connection for SensorThings API operations.
    /// The caller is responsible for disposing the connection (use 'using' or 'await using').
    /// </summary>
    /// <returns>A new database connection instance.</returns>
    IDbConnection CreateConnection();
}
