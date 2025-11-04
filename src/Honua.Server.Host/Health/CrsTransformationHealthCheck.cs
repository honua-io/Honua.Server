// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Honua.Server.Host.Health;

public sealed class CrsTransformationHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var (x, y) = CrsTransform.TransformCoordinate(-122.5, 45.5, CrsHelper.Wgs84, CrsHelper.WebMercator);
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("CRS transform returned invalid values."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("CRS transformations are operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("CRS transformation failed.", ex));
        }
    }
}
