// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Health;

public sealed class SqliteDataSourceHealthContributor : IDataSourceHealthContributor
{
    public string Provider => "sqlite";

    public async Task<HealthCheckResult> CheckAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken)
    {
        Guard.NotNull(dataSource);

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            return HealthCheckResult.Unhealthy("SQLite connection string is missing.");
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(dataSource.ConnectionString);
            await using var connection = new SqliteConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["dataSource"] = dataSource.Id ?? string.Empty,
                ["file"] = builder.DataSource ?? string.Empty
            };

            return HealthCheckResult.Healthy("SQLite data source reachable.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to access SQLite data source.", ex);
        }
    }
}
