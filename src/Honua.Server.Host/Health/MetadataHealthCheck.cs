// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Health;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check for metadata registry availability.
/// Verifies that the metadata registry is initialized and accessible.
/// </summary>
public sealed class MetadataHealthCheck : HealthCheckBase
{
    private readonly IMetadataRegistry _registry;

    public MetadataHealthCheck(IMetadataRegistry registry, ILogger<MetadataHealthCheck> logger)
        : base(logger)
    {
        _registry = Guard.NotNull(registry);
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        await _registry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        data["catalogId"] = snapshot.Catalog.Id;
        data["services"] = snapshot.Services.Count;
        data["layers"] = snapshot.Layers.Count;

        return HealthCheckResult.Healthy("Metadata snapshot loaded.", data);
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        return HealthCheckResult.Unhealthy("Metadata is unavailable.", ex, data);
    }
}
