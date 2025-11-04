// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Health;

public sealed class PostgresDataSourceHealthContributor : IDataSourceHealthContributor
{
    public string Provider => "postgis";

    public async Task<HealthCheckResult> CheckAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken)
    {
        Guard.NotNull(dataSource);

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            return HealthCheckResult.Unhealthy("PostGIS connection string is missing.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(dataSource.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["dataSource"] = dataSource.Id ?? string.Empty,
                ["host"] = connection.Host ?? string.Empty,
                ["database"] = connection.Database ?? string.Empty
            };

            return HealthCheckResult.Healthy("PostGIS data source reachable.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to access PostGIS data source.", ex);
        }
    }
}

