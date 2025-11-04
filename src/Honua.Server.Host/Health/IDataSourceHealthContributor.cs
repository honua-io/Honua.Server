// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Server.Host.Health;

public interface IDataSourceHealthContributor
{
    string Provider { get; }

    Task<HealthCheckResult> CheckAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken);
}
