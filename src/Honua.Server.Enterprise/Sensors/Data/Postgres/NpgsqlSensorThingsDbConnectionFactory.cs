// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using Npgsql;

namespace Honua.Server.Enterprise.Sensors.Data.Postgres;

/// <summary>
/// PostgreSQL implementation of the SensorThings database connection factory.
/// Creates NpgsqlConnection instances on demand for proper resource management.
/// </summary>
public sealed class NpgsqlSensorThingsDbConnectionFactory : ISensorThingsDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="NpgsqlSensorThingsDbConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionString is null.</exception>
    public NpgsqlSensorThingsDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Creates a new NpgsqlConnection instance.
    /// The caller is responsible for disposing the connection (use 'using' or 'await using').
    /// </summary>
    /// <returns>A new NpgsqlConnection instance.</returns>
    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
